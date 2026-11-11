[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')]
    [string]$Version = '0.1.0-alpha.1'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Assert-PowerShellParses
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $tokens = $null
    $parseErrors = $null
    $null = [System.Management.Automation.Language.Parser]::ParseFile(
        $Path,
        [ref]$tokens,
        [ref]$parseErrors)
    if ($null -ne $parseErrors -and $parseErrors.Count -gt 0)
    {
        $messages = @($parseErrors | ForEach-Object { $_.Message })
        throw "PowerShell script '$Path' has parse errors: $($messages -join '; ')"
    }
}

function Assert-ProfileManifest
{
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest,

        [Parameter(Mandatory = $true)]
        [ValidateSet('read', 'readwrite')]
        [string]$ProfileKey,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedVersion
    )

    if ($Manifest.manifest_version -ne '0.3' -or
        $Manifest.name -ne "tia-portal-mcp-$ProfileKey" -or
        $Manifest.version -cne $ExpectedVersion -or
        $Manifest.server.type -ne 'binary' -or
        $Manifest.server.entry_point -ne 'server/TiaPortalMcp.exe' -or
        $Manifest.tools_generated -ne $true -or
        $Manifest.prompts_generated -ne $true -or
        @($Manifest.compatibility.platforms) -notcontains 'win32')
    {
        throw "The rendered $ProfileKey MCPB manifest does not satisfy the alpha package contract."
    }

    $arguments = @($Manifest.server.mcp_config.args)
    $profileIndex = [System.Array]::IndexOf(
        [object[]]$arguments,
        '--access-profile')
    if ($profileIndex -lt 0 -or
        $profileIndex + 1 -ge $arguments.Count -or
        $arguments[$profileIndex + 1] -cne $ProfileKey)
    {
        throw "The rendered $ProfileKey MCPB manifest is not locked to its profile."
    }
}

function Assert-VsCodeConfiguration
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Read', 'ReadWrite')]
        [string]$Profile
    )

    $configuration = Get-Content -LiteralPath $Path -Raw |
        ConvertFrom-Json
    $profileKey = $Profile.ToLowerInvariant()
    $expectedServerName = "tia-portal-mcp-$profileKey"
    $serverProperty = $configuration.servers.PSObject.Properties[$expectedServerName]
    if ($null -eq $serverProperty)
    {
        throw "VS Code configuration '$Path' is missing '$expectedServerName'."
    }

    $server = $serverProperty.Value
    $arguments = @($server.args)
    $profileIndex = [System.Array]::IndexOf(
        [object[]]$arguments,
        '--access-profile')
    if ($server.type -ne 'stdio' -or
        $server.command -notlike '${input:*}\server\TiaPortalMcp.exe' -or
        $profileIndex -lt 0 -or
        $profileIndex + 1 -ge $arguments.Count -or
        $arguments[$profileIndex + 1] -cne $Profile)
    {
        throw "VS Code configuration '$Path' is not a portable, profile-locked stdio configuration."
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent $PSScriptRoot))
$clientRoot = Join-Path $repositoryRoot 'packaging\clients'
$renderScript = Join-Path $PSScriptRoot 'Render-Packaging.ps1'

if (-not (Test-Path -LiteralPath $clientRoot -PathType Container))
{
    throw "Client packaging source was not found: '$clientRoot'."
}
if (-not (Test-Path -LiteralPath $renderScript -PathType Leaf))
{
    throw "Packaging renderer was not found: '$renderScript'."
}

$scriptFiles = @(
    Get-ChildItem -LiteralPath $clientRoot -Recurse -File -Filter '*.ps1'
)
foreach ($scriptFile in $scriptFiles)
{
    Assert-PowerShellParses -Path $scriptFile.FullName
}

$removalGuideContracts = @(
    [pscustomobject]@{
        RelativePath = 'claude\README.md'
        RequiredText = @('Settings > Extensions', 'Remove')
    },
    [pscustomobject]@{
        RelativePath = 'vscode\README.md'
        RequiredText = @('MCP: List Servers', 'Uninstall')
    },
    [pscustomobject]@{
        RelativePath = 'chatgpt\README.md'
        RequiredText = @(
            'Ctrl+C',
            'Settings > Apps',
            'profile-deletion command'
        )
    }
)
foreach ($guideContract in $removalGuideContracts)
{
    $guidePath = Join-Path $clientRoot $guideContract.RelativePath
    if (-not (Test-Path -LiteralPath $guidePath -PathType Leaf))
    {
        throw "Client removal guide was not found: '$guidePath'."
    }

    $guideText = Get-Content -LiteralPath $guidePath -Raw
    foreach ($requiredText in $guideContract.RequiredText)
    {
        if ($guideText.IndexOf(
            $requiredText,
            [System.StringComparison]::OrdinalIgnoreCase) -lt 0)
        {
            throw "Client removal guide '$guidePath' does not contain required guidance: '$requiredText'."
        }
    }
}

$sourceJsonFiles = @(
    Get-ChildItem `
        -LiteralPath (Join-Path $repositoryRoot 'packaging') `
        -Recurse `
        -File `
        -Filter '*.json'
)
foreach ($jsonFile in $sourceJsonFiles)
{
    try
    {
        $null = Get-Content -LiteralPath $jsonFile.FullName -Raw |
            ConvertFrom-Json
    }
    catch
    {
        throw "Packaging JSON is invalid: '$($jsonFile.FullName)'. $($_.Exception.Message)"
    }
}

$repositorySpecificFragments = New-Object System.Collections.Generic.List[string]
$repositorySpecificFragments.Add($repositoryRoot)
$repositorySpecificFragments.Add($repositoryRoot.Replace('\', '/'))
$userProfile = [System.Environment]::GetFolderPath(
    [System.Environment+SpecialFolder]::UserProfile)
if (-not [string]::IsNullOrWhiteSpace($userProfile))
{
    $repositorySpecificFragments.Add($userProfile)
    $repositorySpecificFragments.Add($userProfile.Replace('\', '/'))
}
foreach ($clientFile in Get-ChildItem -LiteralPath $clientRoot -Recurse -File)
{
    $text = Get-Content -LiteralPath $clientFile.FullName -Raw
    foreach ($fragment in @($repositorySpecificFragments | Sort-Object -Unique))
    {
        if ($text.IndexOf(
            $fragment,
            [System.StringComparison]::OrdinalIgnoreCase) -ge 0)
        {
            throw "Client asset contains a repository-specific local path or account name: '$($clientFile.FullName)'."
        }
    }
}

Assert-VsCodeConfiguration `
    -Path (Join-Path $clientRoot 'vscode\read.mcp.json') `
    -Profile 'Read'
Assert-VsCodeConfiguration `
    -Path (Join-Path $clientRoot 'vscode\readwrite.mcp.json') `
    -Profile 'ReadWrite'

$temporaryParent = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::GetTempPath())
$temporaryRoot = Join-Path `
    $temporaryParent `
    "tia-mcp-client-packaging-$([System.Guid]::NewGuid().ToString('N'))"
$temporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)

try
{
    $null = & $renderScript -Version $Version -OutputDirectory $temporaryRoot

    $readManifest = Get-Content `
        -LiteralPath (Join-Path $temporaryRoot 'read\manifest.json') `
        -Raw |
        ConvertFrom-Json
    $readWriteManifest = Get-Content `
        -LiteralPath (Join-Path $temporaryRoot 'readwrite\manifest.json') `
        -Raw |
        ConvertFrom-Json
    $releaseManifest = Get-Content `
        -LiteralPath (Join-Path $temporaryRoot 'release-manifest.json') `
        -Raw |
        ConvertFrom-Json

    Assert-ProfileManifest `
        -Manifest $readManifest `
        -ProfileKey 'read' `
        -ExpectedVersion $Version
    Assert-ProfileManifest `
        -Manifest $readWriteManifest `
        -ProfileKey 'readwrite' `
        -ExpectedVersion $Version

    $chatGptScriptDirectory = Join-Path `
        $temporaryRoot `
        'readwrite\clients\chatgpt'
    $serverDirectory = Join-Path $temporaryRoot 'readwrite\server'
    $null = New-Item `
        -ItemType Directory `
        -Path $chatGptScriptDirectory `
        -Force
    $null = New-Item `
        -ItemType Directory `
        -Path $serverDirectory `
        -Force
    Copy-Item `
        -LiteralPath (Join-Path `
            $clientRoot `
            'chatgpt\Configure-ChatGptTunnel.ps1') `
        -Destination $chatGptScriptDirectory
    $null = New-Item `
        -ItemType File `
        -Path (Join-Path $serverDirectory 'TiaPortalMcp.exe') `
        -Force
    $quotedCommand = & (Join-Path `
        $chatGptScriptDirectory `
        'Configure-ChatGptTunnel.ps1') `
        -TiaVersion V19 `
        -OutputRoot 'D:\' `
        -PrintMcpCommand
    if (-not $quotedCommand.EndsWith(
        '"D:\\"',
        [System.StringComparison]::Ordinal))
    {
        throw 'The ChatGPT MCP command does not safely quote an output root with a trailing backslash.'
    }

    if ($releaseManifest.productVersion -cne $Version -or
        $releaseManifest.platform -ne 'win-x64' -or
        $releaseManifest.tiaVersionValidation.'17'.runtimeValidated -ne $false -or
        $releaseManifest.tiaVersionValidation.'19'.runtimeValidated -ne $false -or
        $releaseManifest.tiaVersionValidation.'19'.classification -cne
            'experimental-prior-runtime-evidence' -or
        $releaseManifest.tiaVersionValidation.'20'.bundled -ne $false -or
        $releaseManifest.tiaVersionValidation.'20'.classification -cne
            'planned-later-alpha' -or
        $releaseManifest.tiaVersionValidation.'21'.bundled -ne $false -or
        $releaseManifest.clients.vscode.transport -ne 'stdio' -or
        $releaseManifest.clients.chatgpt.serverExecution -ne 'local' -or
        $releaseManifest.clients.chatgpt.connection -ne 'outbound-https-to-openai' -or
        $releaseManifest.clients.chatgpt.directLocalConnection -ne $false)
    {
        throw 'The rendered release manifest does not describe the expected local client delivery contract.'
    }

    Write-Output ([pscustomobject]@{
        Version = $Version
        PowerShellScripts = $scriptFiles.Count
        PackagingJsonFiles = $sourceJsonFiles.Count
        RemovalGuides = $removalGuideContracts.Count
        Profiles = @('Read', 'ReadWrite')
        Clients = @('Claude Desktop', 'VS Code', 'ChatGPT Secure MCP Tunnel')
    })
}
finally
{
    $expectedPrefix = Join-Path $temporaryParent 'tia-mcp-client-packaging-'
    if ((Test-Path -LiteralPath $temporaryRoot) -and
        $temporaryRoot.StartsWith(
            $expectedPrefix,
            [System.StringComparison]::OrdinalIgnoreCase))
    {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
