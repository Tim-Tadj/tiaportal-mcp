# VS Code local installation

This bundle runs TIA Portal MCP locally over standard input and output. No
extension or VSIX is required.

The `0.1.0-alpha.3` bundles contain workers for TIA Portal V17, V18 and V19
only. V20 is planned for a later alpha; v0.0.18 remains the legacy V20
release. V21 is unsupported in this release.

## Windows download trust

The alpha and its PowerShell helper are unsigned. Before running an extracted
script, verify the downloaded ZIP against its entry in `SHA256SUMS.txt`, using
the matching profile filename:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath `
    .\tia-portal-mcp-0.1.0-alpha.3-read-win-x64.zip
```

If Windows then blocks the verified script because it carries
Mark-of-the-Web, remove that mark only from this helper:

```powershell
Unblock-File -LiteralPath .\clients\vscode\Install-VsCodeMcp.ps1
```

Do not change the global or user execution policy.

From PowerShell in the extracted bundle, run:

```powershell
.\clients\vscode\Install-VsCodeMcp.ps1
```

The installer reads `manifest.json`, so it cannot change a Read bundle into a
ReadWrite bundle. It resolves the broker relative to the extracted bundle and
adds the server to the current VS Code user profile with `code --add-mcp`.

Use an exact version when several bundled TIA Portal versions are installed:

```powershell
.\clients\vscode\Install-VsCodeMcp.ps1 -TiaVersion V19
```

The ReadWrite bundle defaults exports to
`Documents\TIA Portal MCP\Exports`. Override that boundary explicitly when
needed:

```powershell
.\clients\vscode\Install-VsCodeMcp.ps1 `
    -TiaVersion V19 `
    -OutputRoot D:\ApprovedTiaExports
```

To review the generated server object without changing VS Code:

```powershell
.\clients\vscode\Install-VsCodeMcp.ps1 -PrintConfiguration
```

For editor-managed workspace configuration, copy `clients\vscode\mcp.json`
from the extracted release bundle into `.vscode\mcp.json`. This profile-specific
file is created during assembly; the repository source templates are
[read.mcp.json](read.mcp.json) and [readwrite.mcp.json](readwrite.mcp.json).
The extracted file uses an input variable for the absolute bundle path and
defaults to `Auto`; choose an exact version when several are installed.

For the newer native Copilot configuration destinations, use **MCP: Add Server**
and select the destination for the intended session. The helper uses
`code --add-mcp`, which targets editor-managed user-profile configuration.
See the current [VS Code configuration reference](https://code.visualstudio.com/docs/agents/reference/mcp-configuration).

VS Code asks the user to trust a newly added local MCP server. Windows does not
currently provide VS Code MCP sandboxing, so use the Read bundle unless write
tools are deliberately required.

## Remove the server

For a server installed in the VS Code user profile:

1. Run **MCP: List Servers** from the Command Palette.
2. Select `tia-portal-mcp-read` or `tia-portal-mcp-readwrite`.
3. Stop the server, then choose **Uninstall**.

For a workspace installation, stop the server and remove only its matching
entry from `.vscode\mcp.json`. Delete that file only when it contains no other
server configuration.

After the server no longer appears in **MCP: List Servers**, the extracted
profile bundle can be deleted. Repeat the steps if both profiles were
installed. Do not delete a bundle while another client still refers to it.

See VS Code's current server-management guidance:

- https://code.visualstudio.com/docs/agent-customization/mcp-servers#_manage-mcp-servers
