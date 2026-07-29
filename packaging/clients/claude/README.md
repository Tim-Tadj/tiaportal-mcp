# Claude Desktop MCPB removal

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
