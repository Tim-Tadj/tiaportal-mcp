# Client configuration samples

These examples launch the broker from an extracted profile bundle. Select one
profile for a client:

- keep the sample as written for the `Read` bundle;
- for `ReadWrite`, change the executable path to the ReadWrite bundle and
  change `--access-profile` to `ReadWrite`.

Do not configure both profiles against the same TIA Portal process at once.
The broker chooses the exact internal V17 to V20 worker. `Auto` succeeds when
exactly one supported TIA Portal version is installed. Use `V17`, `V18`, `V19`
or `V20` when more than one version is installed.

- `vscode/mcp.json` uses the current VS Code stdio configuration schema.
- `claude/claude_desktop_config.json` is a development fallback when an MCPB
  package is not available.

Claude Desktop users should normally install the matching `.mcpb` file
produced by `build\Assemble-Bundles.ps1 -CreateMcpb`.
