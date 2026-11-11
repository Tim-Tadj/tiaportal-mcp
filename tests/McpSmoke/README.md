# Read-Only MCP Smoke Test

`mcp-read-smoke.mjs` validates a built Read bundle against a running TIA Portal
instance with an open project. It calls only:

- `GetCapabilities`
- `Connect`
- `ListProjects`
- `GetDevices`

The test confirms that negotiated list tools are registered exactly once and
that automatic CSV, automatic TOON and explicit JSON results retain matching
format and paging metadata. It does not call save, compile, import, export,
close or any other write-capable tool.

The broker must be in its bundle layout with the exact worker at
`workers\vNN\TiaMcpServer.exe`. Run the test only after the repository test
policy requirements and licensed TIA Portal prerequisites are satisfied:

```powershell
node .\tests\McpSmoke\mcp-read-smoke.mjs `
    --broker C:\path\to\read\TiaPortalMcp.exe `
    --tia-version V19
```

The command prints one JSON summary containing counts and selected formats. It
does not print project names, paths or device data.

For a release-candidate check which loads the exact local Siemens assemblies,
initialises MCP, validates `tools/list` and calls only `GetCapabilities`, add
`--offline-only`. This mode requires the matching TIA Portal installation and
Openness group membership, but it does not connect to or start TIA Portal and
does not require an open project:

```powershell
node .\tests\McpSmoke\mcp-read-smoke.mjs `
    --broker C:\path\to\read\TiaPortalMcp.exe `
    --tia-version V19 `
    --offline-only
```
