# ChatGPT Secure MCP Tunnel kit

ChatGPT cannot connect directly to a local MCP server. This kit configures the
official OpenAI Secure MCP Tunnel to start the profile-fixed broker locally over
standard input and output. TIA Portal, the broker and every worker remain on
the user's Windows computer. The tunnel makes an outbound HTTPS connection to
OpenAI and does not open an inbound firewall port.

This is currently a ChatGPT web custom-app workflow, not a direct ChatGPT
Desktop installation.

The `0.1.0-alpha.1` bundles contain workers for TIA Portal V17, V18 and V19
only. V20 is planned for a later alpha; v0.0.18 remains the legacy V20
release. V21 is unsupported in this release.

## Windows download trust

The alpha and its PowerShell helpers are unsigned. Before running an extracted
script, verify the downloaded ZIP against its entry in `SHA256SUMS.txt`, using
the matching profile filename:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath `
    .\tia-portal-mcp-0.1.0-alpha.1-read-win-x64.zip
```

If Windows then blocks the verified scripts because they carry
Mark-of-the-Web, remove that mark only from the two ChatGPT helpers:

```powershell
Unblock-File -LiteralPath `
    .\clients\chatgpt\Configure-ChatGptTunnel.ps1
Unblock-File -LiteralPath `
    .\clients\chatgpt\Start-ChatGptTunnel.ps1
```

Do not change the global or user execution policy.

## Prerequisites

- an eligible ChatGPT plan and developer-mode permission;
- Tunnels Read + Use permission in the associated OpenAI Platform
  organisation;
- a tunnel associated with the intended ChatGPT workspace;
- the latest `tunnel-client` downloaded from OpenAI Platform tunnel settings
  or the official `openai/tunnel-client` release page;
- a tunnel runtime API key made available to `tunnel-client`.

Current requirements and availability can change during the beta. Check:

- https://help.openai.com/en/articles/12584461
- https://developers.openai.com/api/docs/guides/secure-mcp-tunnels

## Configure once

Make the runtime API key available only to the current PowerShell session. The
following avoids writing it into the command history or a configuration file:

```powershell
$tunnelSecret = Read-Host 'Tunnel runtime API key' -AsSecureString
$tunnelCredential = [System.Management.Automation.PSCredential]::new(
    'tunnel',
    $tunnelSecret)
$env:CONTROL_PLANE_API_KEY = $tunnelCredential.GetNetworkCredential().Password
```

Configure the tunnel profile from the extracted bundle:

```powershell
.\clients\chatgpt\Configure-ChatGptTunnel.ps1 `
    -TunnelId tunnel_0123456789abcdef0123456789abcdef `
    -TiaVersion V19
```

The script resolves the broker from the bundle, fixes the Read or ReadWrite
profile from `manifest.json`, and passes the local command to
`tunnel-client init`. It does not save or print the runtime API key.

To inspect the quoted local command without calling `tunnel-client` or
requiring a runtime API key:

```powershell
.\clients\chatgpt\Configure-ChatGptTunnel.ps1 `
    -TiaVersion V19 `
    -PrintMcpCommand
```

For a ReadWrite bundle, the default export boundary is
`Documents\TIA Portal MCP\Exports`. An administrator can choose another
boundary with `-OutputRoot`.

## Start when needed

Open PowerShell, provide the runtime API key for that process, then run:

```powershell
.\clients\chatgpt\Start-ChatGptTunnel.ps1
```

The launcher runs `tunnel-client doctor --explain` before starting the
long-running tunnel. Keep the PowerShell window open while using the app.

In ChatGPT web, create a developer-mode app, choose **Tunnel** as the
connection, select the associated tunnel and scan the tools. Create separate
apps for Read and ReadWrite. Prefer Read unless project changes are deliberate.

Clear the key from the PowerShell process when finished:

```powershell
Remove-Item Env:\CONTROL_PLANE_API_KEY
```

## Stop and remove the connection

1. Press Ctrl+C in the PowerShell window running
   `Start-ChatGptTunnel.ps1`. Wait for `tunnel-client` and its owned broker to
   stop.
2. Clear `CONTROL_PLANE_API_KEY` from that PowerShell process using the command
   above.
3. In ChatGPT, open **Settings > Apps**, locate the TIA Portal MCP custom app
   and disconnect it. For a managed workspace app, an owner or administrator
   disables it from **Workspace settings > Apps**.
4. If the OpenAI-hosted tunnel endpoint is no longer needed, remove it from
   OpenAI Platform tunnel settings using an account with Tunnels Read + Manage.
5. Remove the named local `tunnel-client` profile using the profile or
   configuration guidance supplied with the installed official tunnel tooling.
   The current public Secure MCP Tunnel guide does not document a
   profile-deletion command, so this project deliberately does not invent one.
   Do not delete a shared tunnel configuration directory which may contain
   other profiles.
6. Delete the extracted profile bundle only after the tunnel is stopped and no
   other client configuration refers to it.

Disconnecting or disabling the app prevents future access but does not remove
existing ChatGPT conversations which used it.
