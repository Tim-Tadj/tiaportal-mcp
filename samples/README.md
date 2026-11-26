# Client configuration samples

These examples launch the broker from an extracted profile bundle. Select the
sample which matches the bundle:

- `vscode/mcp.json` is the portable Read workspace configuration;
- `vscode/readwrite.mcp.json` is the portable ReadWrite workspace
  configuration and prompts for its output boundary;
- `claude/claude_desktop_config.json` is the Read development fallback;
- `claude/claude_desktop_config.readwrite.json` is the ReadWrite development
  fallback.

Do not configure both profiles against the same TIA Portal process at once.
The 0.2.0-alpha.2 packages include V17 to V20. `Auto` requires exactly one valid
supported installation and its matching worker. Choose an exact version such
as `V20` when multiple
versions are installed.

The VS Code examples use input variables instead of a repository-specific or
machine-specific path. VS Code prompts for the extracted bundle path when the
server starts.

Claude Desktop users should normally install the matching `.mcpb` file
produced by `build\Assemble-Bundles.ps1 -CreateMcpb`.

Release bundles also contain profile-aware installers beneath `clients\`:

- `clients\vscode\Install-VsCodeMcp.ps1` adds the extracted local broker to the
  current VS Code user profile;
- `clients\chatgpt\Configure-ChatGptTunnel.ps1` and
  `Start-ChatGptTunnel.ps1` configure the official OpenAI Secure MCP Tunnel.

The ChatGPT workflow is currently available through ChatGPT web developer-mode
apps. ChatGPT does not connect directly to a local MCP process.
