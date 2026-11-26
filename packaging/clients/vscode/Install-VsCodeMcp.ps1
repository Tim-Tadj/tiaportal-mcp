[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Auto', 'V17', 'V18', 'V19', 'V20')]
    [string]$TiaVersion = 'Auto',

    [Parameter()]
    [string]$OutputRoot,

    [Parameter()]
    [switch]$PrintConfiguration
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

function Get-CommandPath
{
    $commands = @(
        Get-Command -Name 'code.cmd', 'code' -All -ErrorAction SilentlyContinue |
            Where-Object {
                $_.CommandType -eq 'Application' -or
                $_.CommandType -eq 'ExternalScript'
            }
    )

    if ($commands.Count -eq 0)
    {
        throw "The VS Code command-line tool was not found on PATH. Run the 'Shell Command: Install code command in PATH' command in VS Code, or use clients\vscode\mcp.json directly."
    }

    $commandPath = $commands[0].Path
    if ([string]::IsNullOrWhiteSpace($commandPath))
    {
        $commandPath = $commands[0].Source
    }

    return $commandPath
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
$serverName = $manifest.name
$serverArguments = @(
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

    $resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
    $serverArguments += @(
        '--output-root',
        $resolvedOutputRoot
    )
}
elseif (-not [string]::IsNullOrWhiteSpace($OutputRoot))
{
    throw '-OutputRoot applies only to the ReadWrite bundle.'
}

$serverConfiguration = [ordered]@{
    name = $serverName
    type = 'stdio'
    command = $brokerPath
    args = $serverArguments
    env = [ordered]@{}
}
$serverConfigurationJson = $serverConfiguration | ConvertTo-Json -Depth 10 -Compress

if ($PrintConfiguration)
{
    Write-Output $serverConfigurationJson
    return
}

$codeCommand = Get-CommandPath
Write-Host "Adding '$serverName' to the current VS Code user profile."
& $codeCommand --add-mcp $serverConfigurationJson
$installExitCode = $LASTEXITCODE
if ($installExitCode -ne 0)
{
    throw "VS Code failed to add the MCP server with exit code $installExitCode."
}

Write-Output ([pscustomobject]@{
    Client = 'VS Code'
    Server = $serverName
    AccessProfile = $accessProfile
    TiaVersion = $TiaVersion
    Broker = $brokerPath
})
