# Build and bundle orchestration

These scripts produce the two public Windows x64 profiles:

- `Read`
- `ReadWrite`

Each profile bundle contains one dependency-free broker and exact internal
workers for the selected TIA Portal versions. The first alpha bundles V17 to
V19. V20 can still be compiled explicitly for future development but is
deferred from this release. V21 is unsupported in this release and is rejected
until its modular adapter is implemented.

## Prerequisites

- Windows with a .NET SDK capable of building SDK-style projects. The
  build-only `Microsoft.NETFramework.ReferenceAssemblies.net48` package supplies
  the .NET Framework 4.8 reference assemblies, so a separately installed
  developer targeting pack is not required.
- exact Siemens.Engineering PublicAPI reference directories for every worker
  version being compiled. These may come from matching local TIA Portal
  installations or an authorised release-build reference source.
- network or package-cache access for the NuGet dependencies
- the repository checked out to a writable location
- the optional `mcpb` CLI when `.mcpb` files are required

TIA Portal licences and a running Portal process are not required for
compilation. The exact Siemens.Engineering PublicAPI reference assemblies are
still required at build time and are not contained in the Siemens
`Packages.Openness` NuGet package.

## Validate client packaging without TIA Portal

The client manifests, portable paths and PowerShell syntax can be checked
without an installed or running TIA Portal:

```powershell
.\build\Validate-Client-Packaging.ps1 -Version 0.1.0-alpha.1
```

This renders metadata into a temporary directory, validates the Read and
ReadWrite MCPB contracts, checks the VS Code profile lock and confirms the
ChatGPT Secure MCP Tunnel disclosure. It does not start a broker or worker.

## Validate the alpha release contracts without TIA Portal

Run the complete Siemens-free release gate from the repository root:

```powershell
.\build\Validate-AlphaRelease.ps1
dotnet test .\tests\TiaMcp.Contracts.Test\TiaMcp.Contracts.Test.csproj `
    --configuration Release
```

The validator checks release versions, exact Read and ReadWrite tool surfaces,
the secondary mutation policy, operation-gate registration, V20 and V21
rejection, release and client manifests, and third-party notice coverage. Pass
a full build directory to also check broker and worker versions, dependencies,
metadata, Siemens DLL exclusion and bounded deferred-version diagnostics:

```powershell
.\build\Validate-AlphaRelease.ps1 `
    -BuildDirectory .\artifacts\ci-brokers
```

The Windows workflow in `.github\workflows\alpha-siemens-free.yml` runs these
checks and builds only the dependency-free brokers. It deliberately does not
build or start an exact-version worker on a hosted runner.

## Build the broker and workers

Run from the repository root:

```powershell
.\build\Build-Workers.ps1 -Version 0.1.0-alpha.1
```

The default matrix builds Read and ReadWrite brokers plus V17, V18 and V19
workers beneath `artifacts\build`. A smaller experimental matrix can be
requested explicitly. V20 remains an explicit development build and is not
part of `0.1.0-alpha.1`; the public bundle assembler rejects it:

```powershell
.\build\Build-Workers.ps1 `
    -Version 0.1.0-alpha.1 `
    -TiaVersions 19 `
    -OutputDirectory C:\temp\tia-mcp-build
```

The output directory must be empty. Each component receives isolated output
and intermediate directories. The script verifies product versions and writes
hash-bound `tia-mcp-build.json` metadata beside each executable.

## Assemble the two bundles

```powershell
.\build\Assemble-Bundles.ps1 `
    -Version 0.1.0-alpha.1 `
    -BuildDirectory .\artifacts\build
```

The assembly script creates exactly two ZIP bundles, a release manifest and
`SHA256SUMS.txt`. It rejects every Siemens-supplied DLL. Workers resolve the
exact signed `Siemens.Engineering*` assemblies from the user's matching local
TIA Portal installation at runtime. The public bundle contains no Siemens
runtime, resolver or object-code DLL.

## Create Claude Desktop MCPB files

Install the current MCPB CLI separately, then request packing:

```powershell
npm install -g @anthropic-ai/mcpb

.\build\Assemble-Bundles.ps1 `
    -Version 0.1.0-alpha.1 `
    -CreateMcpb
```

`-CreateMcpb` is strict. If the MCPB CLI is unavailable, the script stops
without producing ZIP or MCPB artefacts. Run the script without
`-CreateMcpb` when only ZIP bundles are required. The release manifest adds an
`mcpb` entry only for an MCPB file which was actually produced. Before packing,
the script validates the MCPB identity, entry point, profile lock and Windows
compatibility. It then inspects the resulting archive for the manifest, broker
and client assets.

## Install in VS Code

Every extracted ZIP contains a profile-aware local installer:

```powershell
.\clients\vscode\Install-VsCodeMcp.ps1 -TiaVersion V19
```

It resolves `TiaPortalMcp.exe` relative to the bundle and invokes
`code --add-mcp`. Use `-PrintConfiguration` to inspect the generated server
object without modifying VS Code. A portable `clients\vscode\mcp.json` is also
included for workspace configuration.

## Connect ChatGPT through Secure MCP Tunnel

ChatGPT cannot connect directly to a local MCP process. Every extracted ZIP
therefore includes scripts which configure the official OpenAI Secure MCP
Tunnel with the local profile-fixed broker:

```powershell
.\clients\chatgpt\Configure-ChatGptTunnel.ps1 `
    -TunnelId tunnel_0123456789abcdef0123456789abcdef `
    -TiaVersion V19

.\clients\chatgpt\Start-ChatGptTunnel.ps1
```

`CONTROL_PLANE_API_KEY` must be supplied to the PowerShell process through the
user's secret-management process. The scripts neither save nor print it. See
the README inside `clients\chatgpt` for current ChatGPT plan, permission and
web developer-mode requirements.

## Current limits

- The scripts do not create a VS Code VSIX because VS Code can install the
  local stdio server directly.
- ChatGPT requires OpenAI's Secure MCP Tunnel and an eligible web
  developer-mode workspace. It is not a direct ChatGPT Desktop integration.
- They do not sign executables, generate an SBOM or perform licensed TIA Portal
  runtime validation.
- V20 is deferred from this alpha and V21 is unsupported in this release.

See [Current Status](../docs/status.md) for the remaining release gates.
