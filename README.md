# TIA Portal MCP Server

An MCP server which connects to Siemens TIA Portal.

## Features

- Connect to a TIA Portal instance
- Browse and interact with TIA Portal projects
- Choose a structurally separated Read or ReadWrite access profile
- Use the same local version broker from Claude Desktop, VS Code or ChatGPT
- Inspect native online state, live PLC values and rack hardware
- Configure offline module types and BaseUnit power groups with ReadWrite

## Documentation

- [General AI workflows](docs/general-ai-workflows.md) describes operation discovery,
  bounded engineering inspection, native content and guarded hardware workflows.
- [Openness coverage](docs/openness-coverage.md) distinguishes implemented operations
  from remaining PublicAPI families and exact-version/runtime limitations.

`0.1.0-alpha.3` adds the general public API interface and offline hardware
work described above. See the [public API interface guide](docs/public-api-interface.md)
for discovery, native calls and effect boundaries, and the
[release notes](docs/alpha3-release.md) for build evidence and limitations.
Tests and runtime qualification remain unrun for this release.

- See [Online Tools](docs/online-tools.md) and [Hardware Tools](docs/hardware-tools.md)
  for the native inspection and offline configuration tools.

- See [Project Documentation](docs/README.md) for the architecture, roadmap,
  current status, definition of done and changelog policy.
- See [0.1.0-alpha.3 release](docs/alpha3-release.md) for the current experimental
  matrix and build-only evidence. [Alpha.2](docs/alpha-release.md) retains its
  historical publication checklist.
- See [Model-Facing Output Format Policy](docs/output-formats.md) for the
  normative compact CSV, bounded TOON and JSON result contract.

## Current Status

The experimental Windows x64 prerelease
[`0.1.0-alpha.3`](https://github.com/Tim-Tadj/tiaportal-mcp/releases/tag/0.1.0-alpha.3)
contains separate Read and ReadWrite distributions with exact V17, V18 and V19
workers. It combines common typed workflows with schema-guided public Openness
access across installed domains. The ReadWrite bridge can perform native
online, file and PLC changes and requires explicit acceptance of those effects.

All bundled versions have build-only evidence for these release bits. Tests
and runtime checks were not run. V20 is deferred and V21 is unsupported.
Packages contain no Siemens runtime DLLs.

The alpha is for evaluation and does not claim production support for any
profile or TIA Portal version. See [Current Status](docs/status.md) and the
[release limitations](docs/alpha3-release.md) before installing it.

The stable legacy v0.0.18 release remains the single read-write server compiled
against TIA Portal V20.

## Released v0.0.18 Requirements

These requirements apply to the legacy v0.0.18 release. The
`0.1.0-alpha.3` broker discovers installed TIA Portal versions, selects a
bundled worker and does not require a checkout-specific path or the
`TiaPortalLocation` variable.

- __.net Framework 4.8__ installed
- __Siemens TIA Portal V20__ installed and running on your machine
- Check if under `Environment Variables/User variable for user <name>` the variable `TiaPortalLocation` is set to `C:\Program Files\Siemens\Automation\Portal V20`
- User must be in Windows User Group `Siemens TIA Openness`

The server checks Windows group membership but must not add the user to that
group automatically.

Experimental V17 to V19 profile bundles require Windows x64, .NET Framework
4.8, at least one matching TIA Portal Openness installation and membership of
the `Siemens TIA Openness` group. When several bundled TIA versions are
installed, pass an exact value such as `--tia-version V19`.

The bundles contain no Siemens-supplied runtime or object-code DLLs. Each
worker loads the matching Openness assemblies from the user's local TIA Portal
installation.

## TIA Portal Versions

- V19 is experimental with focused local runtime checks. The broader licensed
  runtime matrix and clean-account client certification remain outstanding.
- V17 and V18 workers are included for build-only evaluation and are
  explicitly runtime-unverified.
- V20 is not included in `0.1.0-alpha.2`. Its exact-version worker and SIMATIC
  SD document operations are planned for a later alpha. The published v0.0.18
  release remains the legacy V20 option.
- V21 is unsupported in `0.1.0-alpha.2` and requires its own modular Openness
  adapter before a later release can include it.
- SIMATIC SD document import and export are unavailable in
  `0.1.0-alpha.2` because those operations require a V20 worker.

## Known Limitations

- As of 2025-09-02: Importing Ladder (LAD) blocks from SIMATIC SD documents requires the companion `.s7res` file to contain en-US tags for all items; otherwise import may fail. This is a known limitation/bug in TIA Portal Openness.
- `ExportBlock` requires a fully qualified `blockPath` like `Group/Subgroup/Name`. If only a name is provided, the MCP server returns `InvalidParams` and may include suggestions for likely full paths.

## Testing

- Run the Siemens-free output contract suite with
  `dotnet test tests\TiaMcp.Contracts.Test\TiaMcp.Contracts.Test.csproj`.
- See `tests/McpSmoke/README.md` for the reusable read-only broker smoke test.
- See `tests/TiaMcpServer.Test/README.md` for licensed integration
  prerequisites and project asset setup.
- Run solution-wide `dotnet test` only when those integration prerequisites
  are available and the user has explicitly confirmed the run.
- The complete test execution policy is in `AGENTS.md`.

## Contributing

- See `AGENTS.md` for guidance on working with agentic assistants and the test execution policy.

## Error Handling (ExportBlock)

- The Portal layer throws `PortalException` with a short message and `PortalErrorCode` (e.g., NotFound, ExportFailed), and attaches `softwarePath`, `blockPath`, `exportPath` in `Exception.Data` while preserving `InnerException` on export failures.
- The MCP layer maps these to `McpException` codes. For `ExportFailed`, it includes a concise reason from the underlying error; for `NotFound`, it returns `InvalidParams` and may suggest likely full block paths if a bare name was provided.
- Consistency required: TIA Portal never exports inconsistent blocks/types. Single export returns `InvalidParams` with a message to compile first. Bulk export skips inconsistent items and returns them in an `Inconsistent` list alongside `Items`.
- Standardisation: Exception context metadata is attached in a single catch per portal method right before rethrow, not at inline throw sites. See `docs/error-model.md`.
- This standardised pattern currently applies to `ExportBlock` and will expand incrementally.

## Model-Facing Output Formats

Eligible bounded list tools expose
`responseFormat=Auto|Toon|Csv|Json`. `Auto` is the default:

- compact flat `Summary` tables use RFC 4180 CSV;
- richer eligible `Standard` tables use the bounded
  `tia-toon-table/1` profile based on TOON v4.1;
- `Full`, nested, compatibility and otherwise ineligible results use compact
  JSON.

Automatic selection falls back to compact JSON whenever a table format would
lose structure. An incompatible explicit `Csv` or `Toon` request returns MCP
`InvalidParams` guidance instead of flattening fields or silently changing
format. Explicit `Json` works for every valid response shape.

Formatted list metadata reports the selected format, `returned`, `hasMore` and
`nextCursor`. Full row data appears once in model-facing text. The TOON profile
is intended to make repeated record structure conspicuous to models, but any
accuracy benefit is model and data dependent and must be evaluated locally.

MCP JSON-RPC, tool schemas, VS Code and Claude configuration, MCPB and release
manifests, and build metadata remain JSON. The complete normative rules are in
[Model-Facing Output Format Policy](docs/output-formats.md).

## Transports

- Supported today: `stdio`
  - Program wires `AddMcpServer().WithStdioServerTransport()`.
  - For stdio, logs must go to stderr to avoid corrupting JSON-RPC.
- Available via SDK: `stream` (custom streams)
  - The SDK exposes `WithStreamServerTransport(Stream input, Stream output)` which can be used to host over TCP sockets or other streams.
  - Not wired in this repo yet.
- Streamable HTTP: not implemented yet
  - It is not required for the ChatGPT alpha. OpenAI Secure MCP Tunnel can
    launch the local stdio broker directly through `--mcp-command`.
  - No TIA MCP component is hosted remotely or exposed through a public
    inbound port. The tunnel client sends MCP work over outbound HTTPS.
  - A bespoke `HttpListener` JSON bridge is not considered a supported MCP transport.

## VS Code

- The current upstream VS Code extension remains available for the released
  single-worker server: [TIA-Portal MCP-Server](https://marketplace.visualstudio.com/items?itemName=JHeilingbrunner.vscode-tiaportal-mcp).
- The published `0.1.0-alpha.2` profile ZIPs support direct stdio installation.
  The broker chooses the exact bundled V17, V18 or V19 worker.
- A VSIX is not required or included in `0.1.0-alpha.2`.
- The packaged installer and workspace configuration instructions are in
  [`packaging/clients/vscode`](packaging/clients/vscode/README.md).

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

  `responseFormat` is a per-tool argument. Do not add it to `mcp.json`; the
  VS Code server configuration remains JSON.

## Claude Desktop

- Download the matching Read or ReadWrite `.mcpb` from the
  [`0.1.0-alpha.2` prerelease](https://github.com/Tim-Tadj/tiaportal-mcp/releases/tag/0.1.0-alpha.2),
  verify it against `SHA256SUMS.txt`, then choose
  **Settings > Extensions > Advanced settings > Install Extension** in Claude
  Desktop and select the file.
  See [Claude's local MCP server guide](https://support.claude.com/en/articles/10949351-getting-started-with-local-mcp-servers-on-claude-desktop).
- The MCPB files are experimental in `0.1.0-alpha.2`. All published alpha
  workers are runtime-unverified. V19 retains only prior runtime evidence from
  before the resolver change.
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

  `responseFormat` is a per-tool argument. Claude configuration and the MCPB
  manifest remain JSON even when eligible tool result text is CSV or TOON.

## ChatGPT

The TIA MCP broker and worker run only on the user's PC. This project does not
host them in a cloud service or expose them through a public inbound port.

ChatGPT does not connect to the local stdio process directly. The
`0.1.0-alpha.2` connection kit uses
[OpenAI Secure MCP Tunnel](https://developers.openai.com/api/docs/guides/secure-mcp-tunnels)
and configures the local `tunnel-client` to launch the selected profile broker
through `--mcp-command`. No Streamable HTTP adapter is required. The tunnel
uses outbound HTTPS, so selected MCP requests and results pass through OpenAI.
The packaged configure, doctor and start workflow is documented in
[`packaging/clients/chatgpt`](packaging/clients/chatgpt/README.md).

Current official full MCP availability is on ChatGPT web. This project
therefore describes the artefact as a custom-app connection kit, not a direct
ChatGPT Desktop stdio installer. See
[OpenAI's developer-mode availability](https://help.openai.com/en/articles/12584461)
before relying on a particular ChatGPT plan or workspace role.

See [ChatGPT Local Connection](docs/chatgpt-local.md) for the process and data
boundary. The TIA-facing processes and project files remain local.
