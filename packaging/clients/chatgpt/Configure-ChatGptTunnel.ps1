[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern(
        '^tunnel_[0-9a-f]{32}$',
        Options = [System.Text.RegularExpressions.RegexOptions]::None)]
    [string]$TunnelId,

    [Parameter()]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]*$')]
    [string]$TunnelProfile,

    [Parameter()]
    [ValidateSet('Auto', 'V17', 'V18', 'V19')]
    [string]$TiaVersion = 'Auto',

    [Parameter()]
    [string]$OutputRoot,

    [Parameter()]
    [string]$TunnelClientCommand = 'tunnel-client',

    [Parameter()]
    [switch]$PrintMcpCommand
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-BundleProfile
{
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest
    )

    switch ($Manifest.name)
    {
        'tia-portal-mcp-read'
        {
            return 'Read'
        }
        'tia-portal-mcp-readwrite'
        {
            return 'ReadWrite'
        }
        default
        {
            throw "The bundle manifest has an unknown profile name: '$($Manifest.name)'."
        }
    }
}

function Get-ExternalCommandPath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $commands = @(
        Get-Command -Name $Name -All -ErrorAction SilentlyContinue |
            Where-Object {
                $_.CommandType -eq 'Application' -or
                $_.CommandType -eq 'ExternalScript'
            }
    )
    if ($commands.Count -eq 0)
    {
        throw "The command '$Name' was not found on PATH. Download tunnel-client from OpenAI Platform tunnel settings or the official openai/tunnel-client release page."
    }

    $commandPath = $commands[0].Path
    if ([string]::IsNullOrWhiteSpace($commandPath))
    {
        $commandPath = $commands[0].Source
    }

    return $commandPath
}

function ConvertTo-WindowsCommandArgument
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    if ($Value.IndexOf('"') -ge 0)
    {
        throw 'Windows command arguments cannot contain a double-quote character.'
    }

    $trailingBackslashCount = 0
    for ($index = $Value.Length - 1;
        $index -ge 0 -and $Value[$index] -eq [char]'\';
        $index--)
    {
        $trailingBackslashCount++
    }

    $escapedValue = $Value
    if ($trailingBackslashCount -gt 0)
    {
        $escapedValue = $Value.PadRight(
            $Value.Length + $trailingBackslashCount,
            [char]'\')
    }

    return '"' + $escapedValue + '"'
}

$bundleRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$manifestPath = Join-Path $bundleRoot 'manifest.json'
$brokerPath = Join-Path $bundleRoot 'server\TiaPortalMcp.exe'

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf))
{
    throw "The bundle manifest was not found: '$manifestPath'. Keep this script inside the extracted bundle."
}

if (-not (Test-Path -LiteralPath $brokerPath -PathType Leaf))
{
    throw "The TIA Portal MCP broker was not found: '$brokerPath'. Re-extract the complete bundle."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$accessProfile = Get-BundleProfile -Manifest $manifest
if ([string]::IsNullOrWhiteSpace($TunnelProfile))
{
    $TunnelProfile = $manifest.name
}

$mcpArguments = @(
    '--access-profile',
    $accessProfile,
    '--tia-version',
    $TiaVersion
)

if ($accessProfile -eq 'ReadWrite')
{
    if ([string]::IsNullOrWhiteSpace($OutputRoot))
    {
        $documentsPath = [System.Environment]::GetFolderPath(
            [System.Environment+SpecialFolder]::MyDocuments)
        if ([string]::IsNullOrWhiteSpace($documentsPath))
        {
            throw 'No default Documents directory is available. Supply -OutputRoot explicitly.'
        }

        $OutputRoot = Join-Path $documentsPath 'TIA Portal MCP\Exports'
    }

    $mcpArguments += @(
        '--output-root',
        [System.IO.Path]::GetFullPath($OutputRoot)
    )
}
elseif (-not [string]::IsNullOrWhiteSpace($OutputRoot))
{
    throw '-OutputRoot applies only to the ReadWrite bundle.'
}

$quotedCommandParts = New-Object System.Collections.Generic.List[string]
$quotedCommandParts.Add((ConvertTo-WindowsCommandArgument -Value $brokerPath))
foreach ($mcpArgument in $mcpArguments)
{
    $quotedCommandParts.Add((ConvertTo-WindowsCommandArgument -Value $mcpArgument))
}
$mcpCommand = $quotedCommandParts -join ' '

if ($PrintMcpCommand)
{
    Write-Output $mcpCommand
    return
}

if ([string]::IsNullOrWhiteSpace($TunnelId))
{
    throw '-TunnelId is required unless -PrintMcpCommand is specified. Use the 32-character lowercase hexadecimal tunnel ID from OpenAI Platform tunnel settings.'
}

if ([string]::IsNullOrWhiteSpace($env:CONTROL_PLANE_API_KEY))
{
    throw 'CONTROL_PLANE_API_KEY is not set for this PowerShell session. Use a tunnel runtime API key supplied through your approved secret-management process. The key is not written by this script.'
}

$tunnelClientPath = Get-ExternalCommandPath -Name $TunnelClientCommand
Write-Host "Configuring OpenAI Secure MCP Tunnel profile '$TunnelProfile' for the local $accessProfile broker."
& $tunnelClientPath init `
    --sample sample_mcp_stdio_local `
    --profile $TunnelProfile `
    --tunnel-id $TunnelId `
    --mcp-command $mcpCommand
$initialiseExitCode = $LASTEXITCODE
if ($initialiseExitCode -ne 0)
{
    throw "tunnel-client init failed with exit code $initialiseExitCode."
}

Write-Output ([pscustomobject]@{
    TunnelId = $TunnelId
    TunnelProfile = $TunnelProfile
    AccessProfile = $accessProfile
    TiaVersion = $TiaVersion
    Broker = $brokerPath
})
