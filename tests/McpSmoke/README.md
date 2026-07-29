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
