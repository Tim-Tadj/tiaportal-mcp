# TIA Portal MCP Server

An MCP server which connects to Siemens TIA Portal.

## Features

- Connect to a TIA Portal instance
- Browse and interact with TIA Portal projects
- Perform basic project operations from within VS Code

## Documentation

- See [Project Documentation](docs/README.md) for the architecture, roadmap,
  current status, definition of done and changelog policy.

## Current Status

The released v0.0.18 implementation is a single read-write server compiled
against TIA Portal V20. Work is in progress on two public distributions,
`tia-portal-mcp-read` and `tia-portal-mcp-readwrite`, with automatic selection
of isolated V17 to V20 workers. V21 remains planned behind a dedicated modular
adapter.

See [Current Status](docs/status.md) before relying on an earlier TIA Portal
version. Accepting `--tia-major-version` does not make the current V20 binary
compatible with that version.

## Released v0.0.18 Requirements

- __.net Framework 4.8__ installed
- __Siemens TIA Portal V20__ installed and running on your machine
- Check if under `Environment Variables/User variable for user <name>` the variable `TiaPortalLocation` is set to `C:\Program Files\Siemens\Automation\Portal V20`
- User must be in Windows User Group `Siemens TIA Openness`

The server checks Windows group membership but must not add the user to that
group automatically.

Experimental V17 to V20 profile bundles require Windows x64, .NET Framework
4.8, at least one matching TIA Portal Openness installation and membership of
the `Siemens TIA Openness` group. When several supported TIA versions are
installed, pass an exact value such as `--tia-version V20`.

## TIA Portal Versions

- __V20__ is the currently compiled and supported baseline.
- V17, V18 and V19 exact-version worker builds are being added but remain
  experimental until they pass the release matrix.
- V21 requires its own modular Openness adapter.
- Export as documents (.s7dcl/.s7res) via `ExportAsDocuments`/`ExportBlocksAsDocuments` requires TIA Portal V20 or newer.
- Import from documents (.s7dcl/.s7res) via `ImportFromDocuments`/`ImportBlocksFromDocuments` also requires TIA Portal V20 or newer.

## Known Limitations

- As of 2025-09-02: Importing Ladder (LAD) blocks from SIMATIC SD documents requires the companion `.s7res` file to contain en-US tags for all items; otherwise import may fail. This is a known limitation/bug in TIA Portal Openness.
- `ExportBlock` requires a fully qualified `blockPath` like `Group/Subgroup/Name`. If only a name is provided, the MCP server returns `InvalidParams` and may include suggestions for likely full paths.

## Testing

- See `tests/TiaMcpServer.Test/README.md` for environment prerequisites and test asset setup.
- Standard command: `dotnet test` (run from the repo root).
- Test execution policy: offer to run tests, but only execute after explicit user confirmation. Details in `AGENTS.md`.

## Contributing

- See `AGENTS.md` for guidance on working with agentic assistants and the test execution policy.

## Error Handling (ExportBlock)

- The Portal layer throws `PortalException` with a short message and `PortalErrorCode` (e.g., NotFound, ExportFailed), and attaches `softwarePath`, `blockPath`, `exportPath` in `Exception.Data` while preserving `InnerException` on export failures.
- The MCP layer maps these to `McpException` codes. For `ExportFailed`, it includes a concise reason from the underlying error; for `NotFound`, it returns `InvalidParams` and may suggest likely full block paths if a bare name was provided.
- Consistency required: TIA Portal never exports inconsistent blocks/types. Single export returns `InvalidParams` with a message to compile first. Bulk export skips inconsistent items and returns them in an `Inconsistent` list alongside `Items`.
- Standardization: Exception context metadata is attached in a single catch per portal method right before rethrow, not at inline throw sites. See `docs/error-model.md`.
- This standardized pattern currently applies to `ExportBlock` and will expand incrementally.

## Transports

- Supported today: `stdio`
  - Program wires `AddMcpServer().WithStdioServerTransport()`.
  - For stdio, logs must go to stderr to avoid corrupting JSON-RPC.
- Available via SDK: `stream` (custom streams)
  - The SDK exposes `WithStreamServerTransport(Stream input, Stream output)` which can be used to host over TCP sockets or other streams.
  - Not wired in this repo yet.
- Streamable HTTP: not implemented yet
  - A later ChatGPT connection kit will use a standards-compliant,
    loopback-only local adapter and OpenAI Secure MCP Tunnel.
  - No TIA MCP component will be hosted remotely or exposed through a public
    inbound port.
  - A bespoke `HttpListener` JSON bridge is not considered a supported MCP transport.

## VS Code

- The current upstream VS Code extension remains available for the released
  single-worker server: [TIA-Portal MCP-Server](https://marketplace.visualstudio.com/items?itemName=JHeilingbrunner.vscode-tiaportal-mcp).
- This branch supports a direct stdio installation from either extracted
  profile ZIP. The broker chooses the exact bundled V17 to V20 worker.
- The planned fork extension will automate profile selection, extraction and
  upgrades. It is not built yet.

  Add the selected broker to your user or workspace `mcp.json`:

  ```json
  {
    "servers": {
      "tia-portal-mcp-read": {
        "type": "stdio",
        "command": "C:\\path\\to\\tia-portal-mcp-read\\server\\TiaPortalMcp.exe",
        "args": [
          "--access-profile",
          "Read",
          "--tia-version",
          "Auto"
        ],
        "env": {}
      }
    }
  }
  ```

  A copyable example is available in
  [`samples/vscode/mcp.json`](samples/vscode/mcp.json). Select only one
  profile for a TIA Portal process.

## Claude Desktop

- Two MCPB manifests and an optional packing step are defined for the Read and
  ReadWrite bundles. After a release build is assembled with `-CreateMcpb`,
  choose **Settings > Extensions > Advanced settings > Install Extension** in
  Claude Desktop, select the matching `.mcpb` file, then review and install it.
  See [Claude's local MCP server guide](https://support.claude.com/en/articles/10949351-getting-started-with-local-mcp-servers-on-claude-desktop).
- The MCPB files remain experimental until the exact worker matrix and clean
  account installation checks pass.
- For development, add the matching broker directly to
  `C:\Users\<user>\AppData\Roaming\Claude\claude_desktop_config.json`. A
  complete example is in
  [`samples/claude/claude_desktop_config.json`](samples/claude/claude_desktop_config.json).

  ```json
  {
    "mcpServers": {
      "tia-portal-mcp-read": {
        "command": "C:\\path\\to\\tia-portal-mcp-read\\server\\TiaPortalMcp.exe",
        "args": [
          "--access-profile",
          "Read",
          "--tia-version",
          "Auto"
        ],
        "env": {}
      }
    }
  }
  ```

## ChatGPT

The TIA MCP broker, worker and any transport adapter will run only on the user's
PC. This project will not host them in a cloud service or expose them through a
public inbound port.

ChatGPT cannot currently launch a local stdio MCP server directly. The planned
connection kit will use an outbound
[OpenAI Secure MCP Tunnel](https://help.openai.com/en/articles/12584461) from a
local tunnel client to the selected local profile. If a Streamable HTTP adapter
is required, it will bind to loopback only. The connection kit is not yet
implemented on this branch. Current official full MCP availability is on
ChatGPT web; this project will not claim direct ChatGPT Desktop installation
until OpenAI documents it.

See [ChatGPT Local Connection](docs/chatgpt-local.md) for the process and data
boundary. Selected MCP requests and results still pass to ChatGPT when tools are
used; the TIA-facing processes and project files remain local.
