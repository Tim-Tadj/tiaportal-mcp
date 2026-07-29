[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [Parameter()]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory))
{
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\packaging'
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$packagingRoot = Join-Path $repositoryRoot 'packaging'

$templates = @{
    'read\manifest.json' = Join-Path $packagingRoot 'mcpb\read\manifest.template.json'
    'readwrite\manifest.json' = Join-Path $packagingRoot 'mcpb\readwrite\manifest.template.json'
    'release-manifest.json' = Join-Path $packagingRoot 'release-manifest.template.json'
}
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)

foreach ($template in $templates.GetEnumerator())
{
    if (-not (Test-Path -LiteralPath $template.Value -PathType Leaf))
    {
        throw "Packaging template not found: $($template.Value)"
    }

    $destination = Join-Path $resolvedOutputDirectory $template.Key
    $destinationDirectory = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null

    $rendered = (Get-Content -Raw -LiteralPath $template.Value).Replace('__VERSION__', $Version)
    $null = $rendered | ConvertFrom-Json
    [System.IO.File]::WriteAllText($destination, $rendered, $utf8WithoutBom)
}

Write-Output "Rendered packaging metadata in '$resolvedOutputDirectory'."
