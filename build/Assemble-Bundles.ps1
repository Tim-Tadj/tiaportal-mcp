[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')]
    [string]$Version,

    [Parameter()]
    [ValidateSet(17, 18, 19, 20, 21)]
    [int[]]$TiaVersions = @(17, 18, 19, 20),

    [Parameter()]
    [string]$BuildDirectory,

    [Parameter()]
    [string]$OutputDirectory,

    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$BuildConfiguration = 'Release',

    [Parameter()]
    [string]$LicenceReviewMarker,

    [Parameter()]
    [switch]$CreateMcpb
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
        throw 'TIA Portal V21 awaits its dedicated modular adapter and cannot be assembled from the legacy worker project. Request V17 to V20 only.'
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

function Test-PathIsWithin
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ParentPath
    )

    $fullPath = Get-NormalisedFullPath -Path $Path
    $fullParentPath = Get-NormalisedFullPath -Path $ParentPath
    if ($fullPath.Equals($fullParentPath, [System.StringComparison]::OrdinalIgnoreCase))
    {
        return $true
    }

    $parentPrefix = $fullParentPath
    if (-not $parentPrefix.EndsWith([System.IO.Path]::DirectorySeparatorChar))
    {
        $parentPrefix += [System.IO.Path]::DirectorySeparatorChar
    }

    return $fullPath.StartsWith($parentPrefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-SafeEmptyOutputDirectory
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$InputDirectory
    )

    $fullPath = Get-NormalisedFullPath -Path $Path
    $fullRepositoryRoot = Get-NormalisedFullPath -Path $RepositoryRoot
    $fullInputDirectory = Get-NormalisedFullPath -Path $InputDirectory
    $volumeRoot = Get-NormalisedFullPath -Path ([System.IO.Path]::GetPathRoot($fullPath))

    if ($fullPath.Equals($volumeRoot, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "The release output directory cannot be a volume root: '$fullPath'."
    }

    if ($fullPath.Equals($fullRepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "The release output directory cannot be the repository root: '$fullPath'."
    }

    if ((Test-PathIsWithin -Path $fullPath -ParentPath $fullInputDirectory) -or
        (Test-PathIsWithin -Path $fullInputDirectory -ParentPath $fullPath))
    {
        throw "The release output directory and build input directory must not contain one another. Input: '$fullInputDirectory'. Output: '$fullPath'."
    }

    if (Test-Path -LiteralPath $fullPath)
    {
        if (-not (Test-Path -LiteralPath $fullPath -PathType Container))
        {
            throw "The release output path is not a directory: '$fullPath'."
        }

        $existingItem = Get-ChildItem -LiteralPath $fullPath -Force | Select-Object -First 1
        if ($null -ne $existingItem)
        {
            throw "The release output directory is not empty: '$fullPath'. Select a fresh directory so stale files cannot enter the release."
        }
    }
    else
    {
        $null = New-Item -ItemType Directory -Path $fullPath
    }

    return $fullPath
}

function Assert-BuildInputs
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildRoot,

        [Parameter(Mandatory = $true)]
        [object[]]$Profiles,

        [Parameter(Mandatory = $true)]
        [int[]]$Versions,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Debug', 'Release')]
        [string]$ExpectedConfiguration
    )

    if (-not (Test-Path -LiteralPath $BuildRoot -PathType Container))
    {
        throw "The build input directory was not found: '$BuildRoot'. Run build\Build-Workers.ps1 first."
    }

    foreach ($profile in $Profiles)
    {
        $brokerDirectory = Join-Path $BuildRoot "brokers\$($profile.Key)"
        $brokerExecutable = Join-Path $brokerDirectory 'TiaPortalMcp.exe'
        if (-not (Test-Path -LiteralPath $brokerExecutable -PathType Leaf))
        {
            throw "The expected $($profile.Name) broker executable was not found: '$brokerExecutable'."
        }
        Assert-BuildMetadata `
            -OutputDirectory $brokerDirectory `
            -ExpectedComponent 'broker' `
            -ExpectedAccessProfile $profile.Name `
            -ExpectedExecutable 'TiaPortalMcp.exe' `
            -ExpectedReleaseVersion $ReleaseVersion `
            -ExpectedConfiguration $ExpectedConfiguration

        foreach ($tiaVersion in $Versions)
        {
            $workerDirectory = Join-Path $BuildRoot "workers\$($profile.Key)\v$tiaVersion"
            $workerExecutable = Join-Path $workerDirectory 'TiaMcpServer.exe'
            if (-not (Test-Path -LiteralPath $workerExecutable -PathType Leaf))
            {
                throw "The expected TIA Portal V$tiaVersion $($profile.Name) worker executable was not found: '$workerExecutable'."
            }
            Assert-BuildMetadata `
                -OutputDirectory $workerDirectory `
                -ExpectedComponent 'worker' `
                -ExpectedAccessProfile $profile.Name `
                -ExpectedTiaVersion $tiaVersion `
                -ExpectedExecutable 'TiaMcpServer.exe' `
                -ExpectedReleaseVersion $ReleaseVersion `
                -ExpectedConfiguration $ExpectedConfiguration
        }
    }
}

function Assert-BuildMetadata
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateSet('broker', 'worker')]
        [string]$ExpectedComponent,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Read', 'ReadWrite')]
        [string]$ExpectedAccessProfile,

        [Parameter()]
        [int]$ExpectedTiaVersion,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedExecutable,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedReleaseVersion,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Debug', 'Release')]
        [string]$ExpectedConfiguration
    )

    $metadataPath = Join-Path $OutputDirectory 'tia-mcp-build.json'
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf))
    {
        throw "Build metadata was not found: '$metadataPath'. Rebuild this output with build\Build-Workers.ps1."
    }

    try
    {
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    }
    catch
    {
        throw "Build metadata is not valid JSON: '$metadataPath'. $($_.Exception.Message)"
    }

    $requiredProperties = @(
        'schemaVersion',
        'component',
        'accessProfile',
        'configuration',
        'releaseVersion',
        'executable',
        'executableSha256'
    )
    if ($ExpectedComponent -eq 'worker')
    {
        $requiredProperties += 'tiaVersion'
    }

    foreach ($requiredProperty in $requiredProperties)
    {
        if ($null -eq $metadata.PSObject.Properties[$requiredProperty])
        {
            throw "Build metadata is missing '$requiredProperty': '$metadataPath'."
        }
    }

    if ($metadata.schemaVersion -ne 1 -or
        $metadata.component -ne $ExpectedComponent -or
        $metadata.accessProfile -ne $ExpectedAccessProfile -or
        $metadata.configuration -ne $ExpectedConfiguration -or
        $metadata.releaseVersion -cne $ExpectedReleaseVersion -or
        $metadata.executable -ne $ExpectedExecutable)
    {
        throw "Build metadata does not match the expected $ExpectedAccessProfile $ExpectedComponent output: '$metadataPath'."
    }

    if ($ExpectedComponent -eq 'worker' -and $metadata.tiaVersion -ne $ExpectedTiaVersion)
    {
        throw "Build metadata does not match TIA Portal V${ExpectedTiaVersion}: '$metadataPath'."
    }

    $executablePath = Join-Path $OutputDirectory $ExpectedExecutable
    $actualHash = Get-FileHash -LiteralPath $executablePath -Algorithm SHA256
    if ($actualHash.Hash -ne $metadata.executableSha256)
    {
        throw "The executable hash does not match its build metadata: '$executablePath'."
    }

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath)
    if ($versionInfo.ProductVersion -cne $ExpectedReleaseVersion)
    {
        throw "The executable product version '$($versionInfo.ProductVersion)' does not match release version '$ExpectedReleaseVersion': '$executablePath'."
    }
}

function Get-DllPolicyCategory
{
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    if (-not $File.Extension.Equals('.dll', [System.StringComparison]::OrdinalIgnoreCase))
    {
        return 'Other'
    }

    $managedAssemblyName = $null
    try
    {
        $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($File.FullName)
        if (-not [string]::IsNullOrWhiteSpace($assemblyName.Name))
        {
            $managedAssemblyName = $assemblyName.Name
        }
    }
    catch
    {
        Write-Verbose "Could not read a managed assembly identity from '$($File.FullName)'. Filename and company metadata will still be checked."
    }

    if ($File.BaseName.StartsWith('Siemens.Engineering', [System.StringComparison]::OrdinalIgnoreCase) -or
        (-not [string]::IsNullOrWhiteSpace($managedAssemblyName) -and
            $managedAssemblyName.StartsWith('Siemens.Engineering', [System.StringComparison]::OrdinalIgnoreCase)))
    {
        # TIA Openness runtime assemblies are supplied by the local TIA Portal
        # installation and must never be redistributed in these bundles.
        return 'ProprietaryTiaRuntime'
    }

    if (-not [string]::IsNullOrWhiteSpace($managedAssemblyName) -and
        $managedAssemblyName.StartsWith('Siemens.Collaboration.Net', [System.StringComparison]::OrdinalIgnoreCase))
    {
        if ($File.BaseName.StartsWith('Siemens', [System.StringComparison]::OrdinalIgnoreCase) -and
            -not $File.BaseName.StartsWith('Siemens.Collaboration.Net', [System.StringComparison]::OrdinalIgnoreCase))
        {
            return 'UnreviewedSiemens'
        }

        # These resolver dependencies are distinct from Siemens.Engineering.
        # They may be bundled only after a release-specific licence review.
        return 'ReviewableRedistributable'
    }

    $companyName = $null
    try
    {
        $companyName = $File.VersionInfo.CompanyName
    }
    catch
    {
        Write-Verbose "Could not read company metadata from '$($File.FullName)'."
    }

    if (-not [string]::IsNullOrWhiteSpace($managedAssemblyName) -and
        $managedAssemblyName.StartsWith('Siemens', [System.StringComparison]::OrdinalIgnoreCase))
    {
        return 'UnreviewedSiemens'
    }

    if ($File.BaseName.StartsWith('Siemens', [System.StringComparison]::OrdinalIgnoreCase))
    {
        return 'UnreviewedSiemens'
    }

    if (-not [string]::IsNullOrWhiteSpace($companyName) -and
        $companyName.IndexOf('Siemens', [System.StringComparison]::OrdinalIgnoreCase) -ge 0)
    {
        return 'UnreviewedSiemens'
    }

    return 'Other'
}

function Copy-DirectoryContents
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    $sourceRoot = Get-NormalisedFullPath -Path $SourceDirectory
    $destinationRoot = Get-NormalisedFullPath -Path $DestinationDirectory
    $sourcePrefix = $sourceRoot
    if (-not $sourcePrefix.EndsWith([System.IO.Path]::DirectorySeparatorChar))
    {
        $sourcePrefix += [System.IO.Path]::DirectorySeparatorChar
    }
    $null = New-Item -ItemType Directory -Path $destinationRoot -Force

    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File)
    {
        $dllPolicyCategory = Get-DllPolicyCategory -File $file
        if ($dllPolicyCategory -eq 'ProprietaryTiaRuntime')
        {
            Write-Verbose "Excluding proprietary TIA runtime assembly '$($file.FullName)'."
            continue
        }

        if ($dllPolicyCategory -eq 'UnreviewedSiemens')
        {
            throw "The build output contains an unreviewed Siemens DLL which is not on the explicit resolver allow-list: '$($file.FullName)'."
        }

        if (-not $file.FullName.StartsWith($sourcePrefix, [System.StringComparison]::OrdinalIgnoreCase))
        {
            throw "A build file resolved outside its expected source directory: '$($file.FullName)'."
        }

        $relativePath = $file.FullName.Substring($sourcePrefix.Length)
        $destinationPath = Join-Path $destinationRoot $relativePath
        $destinationParent = Split-Path -Parent $destinationPath
        $null = New-Item -ItemType Directory -Path $destinationParent -Force
        Copy-Item -LiteralPath $file.FullName -Destination $destinationPath
    }
}

function Format-TiaVersionList
{
    param(
        [Parameter(Mandatory = $true)]
        [int[]]$Versions
    )

    $labels = @($Versions | ForEach-Object { "V$_" })
    if ($labels.Count -eq 1)
    {
        return $labels[0]
    }

    if ($labels.Count -eq 2)
    {
        return "$($labels[0]) or $($labels[1])"
    }

    $leadingLabels = @($labels[0..($labels.Count - 2)])
    return "$($leadingLabels -join ', ') or $($labels[$labels.Count - 1])"
}

function Write-JsonFile
{
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $json = $Value | ConvertTo-Json -Depth 20
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $json + [System.Environment]::NewLine, $utf8WithoutBom)
}

function Update-RenderedMetadata
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$MetadataDirectory,

        [Parameter(Mandatory = $true)]
        [object[]]$Profiles,

        [Parameter(Mandatory = $true)]
        [int[]]$Versions,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion,

        [Parameter(Mandatory = $true)]
        [string]$RenderedVersionToken
    )

    $releaseManifestPath = Join-Path $MetadataDirectory 'release-manifest.json'
    $releaseManifestText = Get-Content -LiteralPath $releaseManifestPath -Raw
    $releaseManifest = $releaseManifestText.Replace($RenderedVersionToken, $ReleaseVersion) | ConvertFrom-Json
    $releaseManifest.bundledTiaVersions = @($Versions)
    Write-JsonFile -Value $releaseManifest -Path $releaseManifestPath

    $versionList = Format-TiaVersionList -Versions $Versions
    foreach ($profile in $Profiles)
    {
        $manifestPath = Join-Path $MetadataDirectory "$($profile.Key)\manifest.json"
        $manifestText = Get-Content -LiteralPath $manifestPath -Raw
        $manifest = $manifestText.Replace($RenderedVersionToken, $ReleaseVersion) | ConvertFrom-Json
        $manifest.user_config.tia_version.description = "Use auto when exactly one supported version is installed. Otherwise, enter $versionList."
        Write-JsonFile -Value $manifest -Path $manifestPath
    }
}

function Get-SiemensDependencyInventory
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildRoot,

        [Parameter(Mandatory = $true)]
        [object[]]$Profiles,

        [Parameter(Mandatory = $true)]
        [int[]]$Versions
    )

    $directories = New-Object System.Collections.Generic.List[string]
    foreach ($profile in $Profiles)
    {
        $directories.Add((Join-Path $BuildRoot "brokers\$($profile.Key)"))
        foreach ($tiaVersion in $Versions)
        {
            $directories.Add((Join-Path $BuildRoot "workers\$($profile.Key)\v$tiaVersion"))
        }
    }

    $dependenciesByHash = @{}
    foreach ($directory in $directories)
    {
        foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File)
        {
            $category = Get-DllPolicyCategory -File $file
            if ($category -eq 'UnreviewedSiemens')
            {
                throw "The build output contains an unreviewed Siemens DLL which is not on the explicit resolver allow-list: '$($file.FullName)'."
            }

            if ($category -ne 'ReviewableRedistributable')
            {
                continue
            }

            $fileHash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
            $normalisedHash = $fileHash.Hash.ToLowerInvariant()
            if (-not $dependenciesByHash.ContainsKey($normalisedHash))
            {
                $assemblyName = $null
                try
                {
                    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($file.FullName).Name
                }
                catch
                {
                    $assemblyName = $file.BaseName
                }

                $dependenciesByHash[$normalisedHash] = [pscustomobject]@{
                    Name = $file.Name
                    AssemblyName = $assemblyName
                    Sha256 = $normalisedHash
                }
            }
        }
    }

    return @($dependenciesByHash.Values | Sort-Object -Property AssemblyName, Sha256)
}

function Assert-LicenceReviewMarker
{
    # The marker records an explicit release decision, not a general claim that
    # every Siemens-prefixed DLL is redistributable. Its JSON contract is:
    # schemaVersion 1, status "approved", scope "Siemens.Collaboration.Net",
    # the exact releaseVersion, a non-empty reviewedBy value, and an
    # approvedSha256 array covering every resolver dependency in the build.
    param(
        [Parameter()]
        [string]$MarkerPath,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Dependencies,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion
    )

    if ($null -eq $Dependencies -or $Dependencies.Count -eq 0)
    {
        return $null
    }

    if ([string]::IsNullOrWhiteSpace($MarkerPath))
    {
        $requiredHashes = @($Dependencies | ForEach-Object { "$($_.Name) [$($_.AssemblyName)]: $($_.Sha256)" }) -join [System.Environment]::NewLine
        throw "Siemens.Collaboration.Net resolver DLLs are required by the workers. Supply -LicenceReviewMarker with JSON containing schemaVersion 1, status 'approved', scope 'Siemens.Collaboration.Net', releaseVersion '$ReleaseVersion', reviewedBy, and approvedSha256 entries for:$([System.Environment]::NewLine)$requiredHashes"
    }

    $fullMarkerPath = Get-NormalisedFullPath -Path $MarkerPath
    if (-not (Test-Path -LiteralPath $fullMarkerPath -PathType Leaf))
    {
        throw "The licence review marker was not found: '$fullMarkerPath'."
    }

    try
    {
        $marker = Get-Content -LiteralPath $fullMarkerPath -Raw | ConvertFrom-Json
    }
    catch
    {
        throw "The licence review marker is not valid JSON: '$fullMarkerPath'. $($_.Exception.Message)"
    }

    $requiredProperties = @(
        'schemaVersion',
        'status',
        'scope',
        'releaseVersion',
        'reviewedBy',
        'approvedSha256'
    )
    foreach ($requiredProperty in $requiredProperties)
    {
        if ($null -eq $marker.PSObject.Properties[$requiredProperty])
        {
            throw "The licence review marker is missing '$requiredProperty': '$fullMarkerPath'."
        }
    }

    if ($marker.schemaVersion -ne 1 -or
        $marker.status -ne 'approved' -or
        $marker.scope -ne 'Siemens.Collaboration.Net' -or
        $marker.releaseVersion -cne $ReleaseVersion -or
        [string]::IsNullOrWhiteSpace([string]$marker.reviewedBy))
    {
        throw "The licence review marker is not an approved Siemens.Collaboration.Net review for release '$ReleaseVersion': '$fullMarkerPath'."
    }

    if (-not ($marker.approvedSha256 -is [System.Array]))
    {
        throw "The licence review marker property 'approvedSha256' must be a JSON array: '$fullMarkerPath'."
    }

    $invalidApprovedHashes = @(
        $marker.approvedSha256 |
            Where-Object { ([string]$_) -notmatch '^[0-9A-Fa-f]{64}$' }
    )
    if ($invalidApprovedHashes.Count -ne 0)
    {
        throw "Every licence review marker approvedSha256 entry must be a 64-character hexadecimal SHA-256 value: '$fullMarkerPath'."
    }

    $approvedHashes = @(
        $marker.approvedSha256 |
            ForEach-Object { ([string]$_).ToLowerInvariant() } |
            Sort-Object -Unique
    )
    $missingDependencies = @(
        $Dependencies |
            Where-Object { $approvedHashes -notcontains $_.Sha256 }
    )

    if ($missingDependencies.Count -ne 0)
    {
        $missingHashes = @($missingDependencies | ForEach-Object { "$($_.Name) [$($_.AssemblyName)]: $($_.Sha256)" }) -join [System.Environment]::NewLine
        throw "The licence review marker does not approve every required Siemens.Collaboration.Net dependency:$([System.Environment]::NewLine)$missingHashes"
    }

    return $fullMarkerPath
}

function Assert-BundledDllPolicy
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$BundleDirectory,

        [Parameter()]
        [string[]]$ApprovedRedistributableHashes
    )

    foreach ($file in Get-ChildItem -LiteralPath $BundleDirectory -Recurse -File)
    {
        $category = Get-DllPolicyCategory -File $file
        if ($category -eq 'ProprietaryTiaRuntime')
        {
            throw "A proprietary TIA runtime assembly reached the bundle staging directory: '$($file.FullName)'."
        }

        if ($category -eq 'UnreviewedSiemens')
        {
            throw "An unreviewed Siemens DLL reached the bundle staging directory: '$($file.FullName)'."
        }

        if ($category -eq 'ReviewableRedistributable')
        {
            $fileHash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
            if ($ApprovedRedistributableHashes -notcontains $fileHash.Hash.ToLowerInvariant())
            {
                throw "A Siemens.Collaboration.Net DLL is not covered by the release licence review: '$($file.FullName)'."
            }
        }
    }
}

function Get-McpbCommand
{
    $commands = @(
        Get-Command -Name mcpb -All -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandType -eq 'Application' -or $_.CommandType -eq 'ExternalScript' }
    )

    if ($commands.Count -eq 0)
    {
        return $null
    }

    return $commands[0]
}

function Invoke-McpbPack
{
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.CommandInfo]$Command,

        [Parameter(Mandatory = $true)]
        [string]$BundleDirectory,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    $commandPath = $Command.Path
    if ([string]::IsNullOrWhiteSpace($commandPath))
    {
        $commandPath = $Command.Source
    }

    Write-Host "Packing MCPB artefact '$OutputPath'."
    $LASTEXITCODE = 0
    & $commandPath pack $BundleDirectory $OutputPath
    if ($LASTEXITCODE -ne 0)
    {
        throw "mcpb pack failed for '$BundleDirectory' with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf))
    {
        throw "mcpb reported success but did not produce '$OutputPath'."
    }
}

function Remove-OwnedWorkingDirectory
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$OutputRoot
    )

    $fullPath = Get-NormalisedFullPath -Path $Path
    $expectedPath = Get-NormalisedFullPath -Path (Join-Path $OutputRoot '.assembly-work')
    if (-not $fullPath.Equals($expectedPath, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "Refusing to remove an unexpected working directory: '$fullPath'."
    }

    if (-not (Test-PathIsWithin -Path $fullPath -ParentPath $OutputRoot))
    {
        throw "Refusing to remove a working directory outside the release output: '$fullPath'."
    }

    if (Test-Path -LiteralPath $fullPath)
    {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

$repositoryRoot = Get-NormalisedFullPath -Path (Split-Path -Parent $PSScriptRoot)
$versionsToBundle = @(Get-SupportedTiaVersions -Versions $TiaVersions)
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

if ([string]::IsNullOrWhiteSpace($BuildDirectory))
{
    $BuildDirectory = Join-Path $repositoryRoot 'artifacts\build'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory))
{
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\release\$Version"
}

$buildRoot = Get-NormalisedFullPath -Path $BuildDirectory
$renderPackagingScript = Join-Path $PSScriptRoot 'Render-Packaging.ps1'
if (-not (Test-Path -LiteralPath $renderPackagingScript -PathType Leaf))
{
    throw "The packaging renderer was not found: '$renderPackagingScript'."
}

Assert-BuildInputs `
    -BuildRoot $buildRoot `
    -Profiles $profiles `
    -Versions $versionsToBundle `
    -ReleaseVersion $Version `
    -ExpectedConfiguration $BuildConfiguration

$siemensDependencies = @(
    Get-SiemensDependencyInventory `
        -BuildRoot $buildRoot `
        -Profiles $profiles `
        -Versions $versionsToBundle
)
$licenceReviewMarkerPath = Assert-LicenceReviewMarker `
    -MarkerPath $LicenceReviewMarker `
    -Dependencies $siemensDependencies `
    -ReleaseVersion $Version
$approvedRedistributableHashes = @($siemensDependencies | ForEach-Object { $_.Sha256 })

$releaseRoot = Assert-SafeEmptyOutputDirectory `
    -Path $OutputDirectory `
    -RepositoryRoot $repositoryRoot `
    -InputDirectory $buildRoot

$mcpbCommand = $null
if ($CreateMcpb)
{
    $mcpbCommand = Get-McpbCommand
    if ($null -eq $mcpbCommand)
    {
        Write-Warning 'The mcpb CLI was not found on PATH. ZIP artefacts and checksums will still be created; no MCPB files will be produced.'
    }
}

$workingRoot = Join-Path $releaseRoot '.assembly-work'
$metadataRoot = Join-Path $workingRoot 'metadata'
$stagingRoot = Join-Path $workingRoot 'staging'
$workingDirectoryCreated = $false
$createdArtefacts = New-Object System.Collections.Generic.List[string]

try
{
    $null = New-Item -ItemType Directory -Path $workingRoot
    $workingDirectoryCreated = $true
    $null = New-Item -ItemType Directory -Path $metadataRoot
    $null = New-Item -ItemType Directory -Path $stagingRoot

    $renderedVersionToken = '0.0.0-render'
    $renderMessages = @(& $renderPackagingScript -Version $renderedVersionToken -OutputDirectory $metadataRoot)
    foreach ($renderMessage in $renderMessages)
    {
        Write-Verbose $renderMessage
    }
    Update-RenderedMetadata `
        -MetadataDirectory $metadataRoot `
        -Profiles $profiles `
        -Versions $versionsToBundle `
        -ReleaseVersion $Version `
        -RenderedVersionToken $renderedVersionToken

    $renderedReleaseManifest = Join-Path $metadataRoot 'release-manifest.json'
    $releaseManifestOutput = Join-Path $releaseRoot 'release-manifest.json'
    Copy-Item -LiteralPath $renderedReleaseManifest -Destination $releaseManifestOutput
    $createdArtefacts.Add($releaseManifestOutput)

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    foreach ($profile in $profiles)
    {
        $profileStage = Join-Path $stagingRoot $profile.Key
        $serverStage = Join-Path $profileStage 'server'
        $workersStage = Join-Path $serverStage 'workers'
        $null = New-Item -ItemType Directory -Path $profileStage
        $null = New-Item -ItemType Directory -Path $serverStage
        $null = New-Item -ItemType Directory -Path $workersStage

        $renderedProfileManifest = Join-Path $metadataRoot "$($profile.Key)\manifest.json"
        Copy-Item -LiteralPath $renderedProfileManifest -Destination (Join-Path $profileStage 'manifest.json')
        if (-not [string]::IsNullOrWhiteSpace($licenceReviewMarkerPath))
        {
            Copy-Item `
                -LiteralPath $licenceReviewMarkerPath `
                -Destination (Join-Path $profileStage 'siemens-collaboration-net-licence-review.json')
        }

        $brokerInput = Join-Path $buildRoot "brokers\$($profile.Key)"
        Copy-DirectoryContents -SourceDirectory $brokerInput -DestinationDirectory $serverStage

        foreach ($tiaVersion in $versionsToBundle)
        {
            $workerInput = Join-Path $buildRoot "workers\$($profile.Key)\v$tiaVersion"
            $workerStage = Join-Path $workersStage "v$tiaVersion"
            Copy-DirectoryContents -SourceDirectory $workerInput -DestinationDirectory $workerStage
        }

        Assert-BundledDllPolicy `
            -BundleDirectory $profileStage `
            -ApprovedRedistributableHashes $approvedRedistributableHashes

        $zipName = "tia-portal-mcp-$Version-$($profile.Key)-win-x64.zip"
        $zipPath = Join-Path $releaseRoot $zipName
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $profileStage,
            $zipPath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false)

        if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf))
        {
            throw "The expected ZIP artefact was not produced: '$zipPath'."
        }
        $createdArtefacts.Add($zipPath)

        if ($CreateMcpb -and $null -ne $mcpbCommand)
        {
            $mcpbName = "tia-portal-mcp-$Version-$($profile.Key)-win-x64.mcpb"
            $mcpbPath = Join-Path $releaseRoot $mcpbName
            Invoke-McpbPack -Command $mcpbCommand -BundleDirectory $profileStage -OutputPath $mcpbPath
            $createdArtefacts.Add($mcpbPath)
        }
    }

    $finalReleaseManifest = Get-Content -LiteralPath $releaseManifestOutput -Raw |
        ConvertFrom-Json
    foreach ($profile in $profiles)
    {
        $mcpbName = "tia-portal-mcp-$Version-$($profile.Key)-win-x64.mcpb"
        $mcpbPath = Join-Path $releaseRoot $mcpbName
        if (Test-Path -LiteralPath $mcpbPath -PathType Leaf)
        {
            $profileManifestEntry =
                $finalReleaseManifest.profiles.PSObject.Properties[$profile.Key].Value
            Add-Member `
                -InputObject $profileManifestEntry `
                -MemberType NoteProperty `
                -Name 'mcpb' `
                -Value $mcpbName `
                -Force
        }
    }
    Write-JsonFile -Value $finalReleaseManifest -Path $releaseManifestOutput

    $checksumLines = @(
        $createdArtefacts |
            Sort-Object |
            ForEach-Object {
                $fileHash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
                "$($fileHash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($_))"
            }
    )

    $checksumPath = Join-Path $releaseRoot 'SHA256SUMS.txt'
    $checksumEncoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($checksumPath, $checksumLines, $checksumEncoding)

    Write-Output ([pscustomobject]@{
        ReleaseDirectory = $releaseRoot
        Profiles = @($profiles | ForEach-Object { $_.Name })
        TiaVersions = $versionsToBundle
        BuildConfiguration = $BuildConfiguration
        LicenceReviewMarker = $licenceReviewMarkerPath
        ZipFiles = @($createdArtefacts | Where-Object { [System.IO.Path]::GetExtension($_) -ieq '.zip' })
        McpbFiles = @($createdArtefacts | Where-Object { [System.IO.Path]::GetExtension($_) -ieq '.mcpb' })
        ChecksumFile = $checksumPath
    })
}
finally
{
    if ($workingDirectoryCreated)
    {
        Remove-OwnedWorkingDirectory -Path $workingRoot -OutputRoot $releaseRoot
    }
}
