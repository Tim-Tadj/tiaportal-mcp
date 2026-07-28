# Packaging

This directory contains source definitions for the two public distributions:

- `tia-portal-mcp-read`
- `tia-portal-mcp-readwrite`

Each distribution contains a fixed-profile broker and internal exact-version
workers. The current experimental legacy worker set is V17, V18, V19 and V20.
V21 remains planned until its modular adapter is implemented.

## Source layout

```text
packaging/
|-- mcpb/
|   |-- read/
|   |   `-- manifest.template.json
|   `-- readwrite/
|       `-- manifest.template.json
`-- release-manifest.template.json
```

The release pipeline replaces `__VERSION__` and stages compiled components
beneath `server/`.

## Bundle layout

```text
server/
|-- TiaPortalMcp.exe
`-- workers/
    |-- v17/
    |-- v18/
    |-- v19/
    `-- v20/
```

`Siemens.Engineering*` runtime assemblies are never bundled. The exact worker
resolves them from the matching local TIA Portal installation.
`Siemens.Collaboration.Net*` resolver dependencies are a separate category and
may be included only with a release-specific, hash-bound licence approval.

## Packaging flow

Use [the build guide](../build/README.md) to:

1. build both brokers and the exact worker matrix;
2. validate component identity and hashes;
3. supply the required dependency licence review;
4. create the two ZIP bundles and checksums;
5. optionally pack the two Claude Desktop MCPB files.

The release manifest always lists the two ZIPs. It lists an MCPB only when that
file was produced. VS Code and local-only ChatGPT tunnel adapters are not listed
until their artefacts exist.

The manifests and scripts are experimental inputs. They are not a supported
release until the worker matrix, client installation, signing and runtime
definition of done in [Current Status](../docs/status.md) are complete.
