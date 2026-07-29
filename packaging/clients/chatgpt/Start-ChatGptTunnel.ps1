[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]*$')]
    [string]$TunnelProfile,

    [Parameter()]
    [string]$TunnelClientCommand = 'tunnel-client',

    [Parameter()]
    [switch]$SkipDoctor
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

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

if ([string]::IsNullOrWhiteSpace($env:CONTROL_PLANE_API_KEY))
{
    throw 'CONTROL_PLANE_API_KEY is not set for this PowerShell session. The tunnel runtime key must remain available while tunnel-client is running.'
}

$bundleRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$manifestPath = Join-Path $bundleRoot 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf))
{
    throw "The bundle manifest was not found: '$manifestPath'. Keep this script inside the extracted bundle."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($TunnelProfile))
{
    $TunnelProfile = $manifest.name
}

$tunnelClientPath = Get-ExternalCommandPath -Name $TunnelClientCommand
if (-not $SkipDoctor)
{
    Write-Host "Checking OpenAI Secure MCP Tunnel profile '$TunnelProfile'."
    & $tunnelClientPath doctor --profile $TunnelProfile --explain
    $doctorExitCode = $LASTEXITCODE
    if ($doctorExitCode -ne 0)
    {
        throw "tunnel-client doctor failed with exit code $doctorExitCode. Correct the reported problem before starting the tunnel."
    }
}

Write-Host "Starting OpenAI Secure MCP Tunnel profile '$TunnelProfile'. Keep this window open while ChatGPT uses TIA Portal MCP."
& $tunnelClientPath run --profile $TunnelProfile
$runExitCode = $LASTEXITCODE
if ($runExitCode -ne 0)
{
    throw "tunnel-client run stopped with exit code $runExitCode."
}
