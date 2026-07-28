# Build and bundle orchestration

These scripts produce the two public Windows x64 profiles:

- `Read`
- `ReadWrite`

Each profile bundle contains one dependency-free broker and exact internal
workers for the selected TIA Portal versions. V17 to V20 use the current legacy
worker source. V21 is rejected until its modular adapter is implemented.

## Prerequisites

- Windows with a .NET SDK capable of building .NET Framework 4.8 projects
- network or package-cache access for the NuGet dependencies
- the repository checked out to a writable location
- the optional `mcpb` CLI when `.mcpb` files are required
- a completed Siemens Collaboration dependency licence review before release

TIA Portal installations and licences are required for runtime validation, but
not for compilation.

## Build the broker and workers

Run from the repository root:

```powershell
.\build\Build-Workers.ps1 -Version 0.1.0-dev
```

The default matrix builds Read and ReadWrite brokers plus V17, V18, V19 and V20
workers beneath `artifacts\build`. A smaller experimental matrix can be
requested explicitly:

```powershell
.\build\Build-Workers.ps1 `
    -Version 0.1.0-dev `
    -TiaVersions 19,20 `
    -OutputDirectory C:\temp\tia-mcp-build
```

The output directory must be empty. Each component receives isolated output
and intermediate directories. The script verifies product versions and writes
hash-bound `tia-mcp-build.json` metadata beside each executable.

## Assemble the two bundles

```powershell
.\build\Assemble-Bundles.ps1 `
    -Version 0.1.0-dev `
    -BuildDirectory .\artifacts\build
```

The assembly script creates exactly two ZIP bundles, a release manifest and
`SHA256SUMS.txt`. It excludes `Siemens.Engineering*` runtime assemblies because
they must be resolved from the matching local TIA Portal installation.

The workers also depend on `Siemens.Collaboration.Net*` resolver libraries.
Those libraries are retained only when every detected hash is approved by a
release-specific licence review marker. If they are present and no marker is
supplied, the script stops and prints the required hashes.

The marker is JSON with this contract:

```json
{
  "schemaVersion": 1,
  "status": "approved",
  "scope": "Siemens.Collaboration.Net",
  "releaseVersion": "0.1.0-dev",
  "reviewedBy": "name or review reference",
  "approvedSha256": [
    "64-character-lowercase-or-uppercase-sha256"
  ]
}
```

Pass it with:

```powershell
.\build\Assemble-Bundles.ps1 `
    -Version 0.1.0-dev `
    -LicenceReviewMarker C:\approved\tia-mcp-licence-review.json
```

## Create Claude Desktop MCPB files

Install the current MCPB CLI separately, then request packing:

```powershell
npm install -g @anthropic-ai/mcpb

.\build\Assemble-Bundles.ps1 `
    -Version 0.1.0-dev `
    -LicenceReviewMarker C:\approved\tia-mcp-licence-review.json `
    -CreateMcpb
```

The script still creates the ZIPs when `-CreateMcpb` is specified but the CLI
is unavailable. The release manifest adds an `mcpb` entry only for an MCPB file
which was actually produced.

## Current limits

- The scripts do not create a VS Code VSIX.
- The scripts do not create the ChatGPT Streamable HTTP gateway or tunnel kit.
- They do not sign executables, generate an SBOM or perform licensed TIA Portal
  runtime validation.
- V21 is intentionally blocked.

See [Current Status](../docs/status.md) for the remaining release gates.
