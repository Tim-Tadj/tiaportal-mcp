[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')]
    [string]$Version,

    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [ValidateSet(17, 18, 19, 20, 21)]
    [int[]]$TiaVersions = @(17, 18, 19, 20),

    [Parameter()]
    [string]$OutputDirectory,

    [Parameter()]
    [string]$WorkerProject,

    [Parameter()]
    [string]$BrokerProject
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-NormalisedFullPath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPath = [System.IO.Path]::GetPathRoot($fullPath)
    if ($fullPath.Equals($rootPath, [System.StringComparison]::OrdinalIgnoreCase))
    {
        return $rootPath
    }

    return $fullPath.TrimEnd([char[]]'\/')
}

function Get-SupportedTiaVersions
{
    param(
        [Parameter(Mandatory = $true)]
        [int[]]$Versions
    )

    if ($null -eq $Versions -or $Versions.Count -eq 0)
    {
        throw 'At least one TIA Portal version must be requested.'
    }

    $normalisedVersions = @($Versions | Sort-Object -Unique)
    if ($normalisedVersions -contains 21)
    {
        throw 'TIA Portal V21 awaits its dedicated modular adapter and cannot be built by the legacy worker project. Request V17 to V20 only.'
    }

    foreach ($tiaVersion in $normalisedVersions)
    {
        if ($tiaVersion -lt 17 -or $tiaVersion -gt 20)
        {
            throw "TIA Portal V$tiaVersion is unsupported. Request V17 to V20 only."
        }
    }

    return $normalisedVersions
}

function Assert-SafeEmptyOutputDirectory
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $fullPath = Get-NormalisedFullPath -Path $Path
    $fullRepositoryRoot = Get-NormalisedFullPath -Path $RepositoryRoot
    $volumeRoot = Get-NormalisedFullPath -Path ([System.IO.Path]::GetPathRoot($fullPath))

    if ($fullPath.Equals($volumeRoot, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "The build output directory cannot be a volume root: '$fullPath'."
    }

    if ($fullPath.Equals($fullRepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "The build output directory cannot be the repository root: '$fullPath'."
    }

    if (Test-Path -LiteralPath $fullPath)
    {
        if (-not (Test-Path -LiteralPath $fullPath -PathType Container))
        {
            throw "The build output path is not a directory: '$fullPath'."
        }

        $existingItem = Get-ChildItem -LiteralPath $fullPath -Force | Select-Object -First 1
        if ($null -ne $existingItem)
        {
            throw "The build output directory is not empty: '$fullPath'. Select a fresh directory so stale binaries cannot enter a release."
        }
    }
    else
    {
        $null = New-Item -ItemType Directory -Path $fullPath
    }

    return $fullPath
}

function Get-BuildEngine
{
    $dotNetCommand = Get-Command `
        -Name dotnet `
        -CommandType Application `
        -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($null -ne $dotNetCommand)
    {
        $installedSdks = @(& $dotNetCommand.Path --list-sdks 2>$null)
        if ($LASTEXITCODE -eq 0 -and $installedSdks.Count -gt 0)
        {
            return [pscustomobject]@{
                Kind = 'dotnet'
                Path = $dotNetCommand.Path
            }
        }
    }

    $msBuildCommand = Get-Command `
        -Name msbuild `
        -CommandType Application `
        -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($null -eq $msBuildCommand)
    {
        $programFilesX86 = [System.Environment]::GetFolderPath(
            [System.Environment+SpecialFolder]::ProgramFilesX86)
        $vsWherePath = Join-Path `
            $programFilesX86 `
            'Microsoft Visual Studio\Installer\vswhere.exe'

        if (Test-Path -LiteralPath $vsWherePath -PathType Leaf)
        {
            $msBuildPaths = @(
                & $vsWherePath `
                    -latest `
                    -products '*' `
                    -requires Microsoft.Component.MSBuild `
                    -find 'MSBuild\**\Bin\MSBuild.exe'
            )
            if ($LASTEXITCODE -eq 0 -and $msBuildPaths.Count -gt 0)
            {
                $msBuildCommand = [pscustomobject]@{
                    Path = $msBuildPaths[0]
                }
            }
        }
    }

    if ($null -ne $msBuildCommand)
    {
        $msBuildRoot = Split-Path `
            -Parent `
            (Split-Path `
                -Parent `
                (Split-Path -Parent $msBuildCommand.Path))
        $sdkDirectory = Join-Path $msBuildRoot 'Sdks\Microsoft.NET.Sdk\Sdk'
        if (Test-Path -LiteralPath $sdkDirectory -PathType Container)
        {
            return [pscustomobject]@{
                Kind = 'msbuild'
                Path = $msBuildCommand.Path
            }
        }
    }

    throw 'No compatible build engine was found. Install a .NET SDK or Visual Studio Build Tools with MSBuild.'
}

function Invoke-ProjectBuild
{
    param(
        [Parameter(Mandatory = $true)]
        [object]$BuildEngine,

        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$BuildConfiguration,

        [Parameter(Mandatory = $true)]
        [hashtable]$Properties,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ($BuildEngine.Kind -eq 'dotnet')
    {
        $arguments = @(
            'build',
            $ProjectPath,
            '--configuration',
            $BuildConfiguration
        )
    }
    else
    {
        $arguments = @(
            $ProjectPath,
            '-restore',
            '-target:Build',
            "-property:Configuration=$BuildConfiguration"
        )
    }

    foreach ($propertyName in @($Properties.Keys | Sort-Object))
    {
        $arguments += "-property:$propertyName=$($Properties[$propertyName])"
    }

    Write-Host "Building $Description."
    & $BuildEngine.Path @arguments
    $buildExitCode = $LASTEXITCODE
    if ($buildExitCode -ne 0)
    {
        throw "$($BuildEngine.Kind) build failed for $Description with exit code $buildExitCode."
    }
}

function Write-BuildMetadata
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateSet('broker', 'worker')]
        [string]$Component,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Read', 'ReadWrite')]
        [string]$AccessProfile,

        [Parameter()]
        [int]$TiaVersion,

        [Parameter(Mandatory = $true)]
        [string]$Executable,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Debug', 'Release')]
        [string]$BuildConfiguration
    )

    $executablePath = Join-Path $OutputDirectory $Executable
    $executableHash = Get-FileHash -LiteralPath $executablePath -Algorithm SHA256
    $metadata = [ordered]@{
        schemaVersion = 1
        component = $Component
        accessProfile = $AccessProfile
        configuration = $BuildConfiguration
        releaseVersion = $ReleaseVersion
        executable = $Executable
        executableSha256 = $executableHash.Hash.ToLowerInvariant()
    }

    if ($Component -eq 'worker')
    {
        $metadata['tiaVersion'] = $TiaVersion
    }

    $metadataPath = Join-Path $OutputDirectory 'tia-mcp-build.json'
    $json = $metadata | ConvertTo-Json -Depth 5
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText(
        $metadataPath,
        $json + [System.Environment]::NewLine,
        $utf8WithoutBom)
}

function Assert-ExecutableVersion
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedVersion
    )

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExecutablePath)
    if ($versionInfo.ProductVersion -cne $ExpectedVersion)
    {
        throw "The executable product version '$($versionInfo.ProductVersion)' does not match release version '$ExpectedVersion': '$ExecutablePath'."
    }
}

$repositoryRoot = Get-NormalisedFullPath -Path (Split-Path -Parent $PSScriptRoot)
$versionsToBuild = @(Get-SupportedTiaVersions -Versions $TiaVersions)

if ([string]::IsNullOrWhiteSpace($OutputDirectory))
{
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\build'
}

if ([string]::IsNullOrWhiteSpace($WorkerProject))
{
    $WorkerProject = Join-Path $repositoryRoot 'src\TiaMcpServer\TiaMcpServer.csproj'
}

if ([string]::IsNullOrWhiteSpace($BrokerProject))
{
    $BrokerProject = Join-Path $repositoryRoot 'src\TiaMcpBroker\TiaMcpBroker.csproj'
}

$workerProjectPath = Get-NormalisedFullPath -Path $WorkerProject
$brokerProjectPath = Get-NormalisedFullPath -Path $BrokerProject

if (-not (Test-Path -LiteralPath $workerProjectPath -PathType Leaf))
{
    throw "The worker project was not found: '$workerProjectPath'."
}

if (-not (Test-Path -LiteralPath $brokerProjectPath -PathType Leaf))
{
    throw "The broker project was not found: '$brokerProjectPath'."
}

$buildEngine = Get-BuildEngine
Write-Host "Using $($buildEngine.Kind) build engine: $($buildEngine.Path)"

$buildRoot = Assert-SafeEmptyOutputDirectory -Path $OutputDirectory -RepositoryRoot $repositoryRoot
$profiles = @(
    [pscustomobject]@{
        Name = 'Read'
        Key = 'read'
    },
    [pscustomobject]@{
        Name = 'ReadWrite'
        Key = 'readwrite'
    }
)

foreach ($profile in $profiles)
{
    foreach ($tiaVersion in $versionsToBuild)
    {
        $workerOutputDirectory = Join-Path $buildRoot "workers\$($profile.Key)\v$tiaVersion"
        $workerIntermediateDirectory =
            (Join-Path $buildRoot "obj\workers\$($profile.Key)\v$tiaVersion") +
            [System.IO.Path]::DirectorySeparatorChar
        $null = New-Item -ItemType Directory -Path $workerOutputDirectory -Force
        $null = New-Item -ItemType Directory -Path $workerIntermediateDirectory -Force

        Invoke-ProjectBuild `
            -BuildEngine $buildEngine `
            -ProjectPath $workerProjectPath `
            -BuildConfiguration $Configuration `
            -Description "TIA Portal V$tiaVersion $($profile.Name) worker" `
            -Properties @{
                AppendTargetFrameworkToOutputPath = 'false'
                BaseOutputPath = $workerOutputDirectory
                IncludeSourceRevisionInInformationalVersion = 'false'
                InformationalVersion = $Version
                OutputPath = $workerOutputDirectory
                TiaAccessProfile = $profile.Name
                TiaMcpIntermediateRoot = $workerIntermediateDirectory
                TiaWorkerVersion = $tiaVersion
                Version = $Version
            }

        $workerExecutable = Join-Path $workerOutputDirectory 'TiaMcpServer.exe'
        if (-not (Test-Path -LiteralPath $workerExecutable -PathType Leaf))
        {
            throw "The expected worker executable was not produced: '$workerExecutable'."
        }
        Assert-ExecutableVersion -ExecutablePath $workerExecutable -ExpectedVersion $Version

        Write-BuildMetadata `
            -OutputDirectory $workerOutputDirectory `
            -Component 'worker' `
            -AccessProfile $profile.Name `
            -TiaVersion $tiaVersion `
            -Executable 'TiaMcpServer.exe' `
            -ReleaseVersion $Version `
            -BuildConfiguration $Configuration
    }

    $brokerOutputDirectory = Join-Path $buildRoot "brokers\$($profile.Key)"
    $brokerIntermediateDirectory =
        (Join-Path $buildRoot "obj\brokers\$($profile.Key)") +
        [System.IO.Path]::DirectorySeparatorChar
    $null = New-Item -ItemType Directory -Path $brokerOutputDirectory -Force
    $null = New-Item -ItemType Directory -Path $brokerIntermediateDirectory -Force

    Invoke-ProjectBuild `
        -BuildEngine $buildEngine `
        -ProjectPath $brokerProjectPath `
        -BuildConfiguration $Configuration `
        -Description "$($profile.Name) broker" `
        -Properties @{
            AppendTargetFrameworkToOutputPath = 'false'
            BaseOutputPath = $brokerOutputDirectory
            IncludeSourceRevisionInInformationalVersion = 'false'
            InformationalVersion = $Version
            OutputPath = $brokerOutputDirectory
            TiaAccessProfile = $profile.Name
            TiaMcpIntermediateRoot = $brokerIntermediateDirectory
            Version = $Version
        }

    $brokerExecutable = Join-Path $brokerOutputDirectory 'TiaPortalMcp.exe'
    if (-not (Test-Path -LiteralPath $brokerExecutable -PathType Leaf))
    {
        throw "The expected broker executable was not produced: '$brokerExecutable'."
    }
    Assert-ExecutableVersion -ExecutablePath $brokerExecutable -ExpectedVersion $Version

    Write-BuildMetadata `
        -OutputDirectory $brokerOutputDirectory `
        -Component 'broker' `
        -AccessProfile $profile.Name `
        -Executable 'TiaPortalMcp.exe' `
        -ReleaseVersion $Version `
        -BuildConfiguration $Configuration
}

Write-Output ([pscustomobject]@{
    BuildDirectory = $buildRoot
    Configuration = $Configuration
    ReleaseVersion = $Version
    Profiles = @($profiles | ForEach-Object { $_.Name })
    TiaVersions = $versionsToBuild
})
