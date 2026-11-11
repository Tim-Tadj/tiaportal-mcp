# Packaging

This directory contains source definitions for the two public distributions:

- `tia-portal-mcp-read`
- `tia-portal-mcp-readwrite`

Each distribution contains a fixed-profile broker and internal exact-version
workers. `0.1.0-alpha.1` bundles V17, V18 and V19. V20 is planned for a later
alpha, while v0.0.18 remains the legacy V20 release. V21 is unsupported in
this release and requires a dedicated modular adapter before inclusion.

## Source layout

```text
packaging/
|-- clients/
|   |-- claude/
|   |   `-- README.md
|   |-- chatgpt/
|   |   |-- Configure-ChatGptTunnel.ps1
|   |   `-- Start-ChatGptTunnel.ps1
|   `-- vscode/
|       |-- Install-VsCodeMcp.ps1
|       |-- read.mcp.json
|       `-- readwrite.mcp.json
|-- mcpb/
|   |-- read/
|   |   `-- manifest.template.json
|   `-- readwrite/
|       `-- manifest.template.json
`-- release-manifest.template.json
```

The release pipeline replaces `__VERSION__`, stages compiled components
beneath `server/`, and adds the portable client installers beneath `clients/`.
The VS Code configuration copied to `clients\vscode\mcp.json` always matches
the profile of the containing bundle.

## Bundle layout

```text
server/
|-- TiaPortalMcp.exe
`-- workers/
    |-- v17/
    |-- v18/
    `-- v19/

clients/
|-- claude/
|   `-- README.md
|-- chatgpt/
|   |-- Configure-ChatGptTunnel.ps1
|   `-- Start-ChatGptTunnel.ps1
`-- vscode/
    |-- Install-VsCodeMcp.ps1
    `-- mcp.json
```

No Siemens-supplied runtime or object-code DLL is bundled. This includes
`Siemens.Engineering*` and `Siemens.Collaboration.Net*`. The exact worker
resolves the required Siemens assemblies from the matching local TIA Portal
installation. Siemens NuGet packages are build inputs only.

## Packaging flow

Use [the build guide](../build/README.md) to:

1. build both brokers and the exact worker matrix;
2. validate component identity and hashes;
3. verify that no Siemens-supplied runtime or object-code DLL is staged;
4. create the two ZIP bundles and checksums;
5. validate the profile-specific client assets and removal guides;
6. optionally pack and inspect the two Claude Desktop MCPB files.

The release manifest always lists the two ZIPs and the client assets contained
inside them. It lists an MCPB only when that file was produced.

## Client delivery

Claude Desktop receives one `.mcpb` per profile. Each manifest resolves its
broker relative to the installed package, declares Windows compatibility and
locks the profile in the server arguments. The bundled Claude guide documents
removal through **Settings > Extensions**, without deleting application data
manually.

VS Code uses the local stdio transport. Users can run the bundled installer to
add the broker to their current VS Code user profile, or copy the portable
`mcp.json` into a workspace. A VSIX is not required.

ChatGPT cannot connect directly to a local MCP process. The bundled scripts
configure and run the official OpenAI Secure MCP Tunnel with a local stdio
command. They do not contain, persist or print the tunnel runtime API key.
ChatGPT custom MCP apps are currently a web developer-mode workflow rather
than a direct ChatGPT Desktop installation.

Each client guide includes removal instructions. Stop the client-owned process
and remove its registration before deleting an extracted bundle.

The manifests and scripts are experimental inputs. They are not a supported
release until the worker matrix, client installation, signing and runtime
definition of done in [Current Status](../docs/status.md) are complete.
