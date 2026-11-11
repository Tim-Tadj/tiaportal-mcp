# Claude Desktop MCPB installation and removal

The `0.1.0-alpha.1` MCPBs contain workers for TIA Portal V17, V18 and V19
only. V20 is planned for a later alpha; v0.0.18 remains the legacy V20
release. V21 is unsupported in this release.

## Windows download trust

The alpha MCPBs are unsigned. Before installing one, verify it against its
entry in `SHA256SUMS.txt`, using the matching profile filename:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath `
    .\tia-portal-mcp-0.1.0-alpha.1-read-win-x64.mcpb
```

Normal MCPB installation through Claude Desktop does not require a PowerShell
setup script. If you also extract the companion ZIP and run one of its unsigned
PowerShell helpers, verify that ZIP first, then use `Unblock-File` only on the
specific extracted script if Windows blocks it. Do not change the global or
user execution policy.

## Remove the MCPB

Claude Desktop owns the installed copy of an MCPB and its local server
process. Remove each profile through Claude Desktop rather than deleting files
from Claude's application-data directories.

1. Open **Settings > Extensions > All extensions** in Claude Desktop.
2. Open **TIA Portal MCP - Read** or **TIA Portal MCP - Read and Write**.
3. Select **Remove** and confirm. Repeat this for the other profile if both
   were installed.
4. Confirm that the extension is no longer listed before deleting the
   downloaded `.mcpb` file or a separately extracted profile ZIP.

For an organisation-managed extension, an owner may also need to open
**Organisation settings > Connectors > Desktop**, use the extension's more
options menu and select **Remove from allowlist**. Removing an extension from
an organisation allowlist can force-delete existing client installations, so
use that action only when removal for the organisation is intended.

See Claude's current extension guidance:

- https://support.claude.com/en/articles/10949351-getting-started-with-local-mcp-servers-on-claude-desktop
- https://support.claude.com/en/articles/12592343-enabling-and-using-the-desktop-extension-allowlist
