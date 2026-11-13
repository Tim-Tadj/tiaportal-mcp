# TIA Portal MCP

**AI-assisted engineering for Siemens TIA Portal.**

Connect your AI assistant to a TIA Portal project. Explore PLC code, trace
references, inspect hardware and networks, and carry out requested engineering
changes through the Model Context Protocol.

Use focused tools for everyday work and a general public Openness interface
for additional workflows. The assistant can discover the native objects,
services and exact method schemas available in your installation, rather than
needing a new C# helper for each task.

[Download 0.1.0-alpha.3](https://github.com/Tim-Tadj/tiaportal-mcp/releases/tag/0.1.0-alpha.3)
| [Install](#installation)
| [TIA versions](#tia-portal-versions)
| [Interface guide](docs/public-api-interface.md)

> Experimental Windows release. V17, V18 and V19 workers are included in both
> profiles. These release binaries have build-only evidence; tests and live
> TIA/PLC checks were not run. See the [release notes](docs/alpha3-release.md).

## What you can do

| Engineering task | MCP capabilities |
| --- | --- |
| Understand an unfamiliar project | Explore devices, PLC software, program blocks, data types, tag tables and constants. |
| Explain PLC logic | Inspect block/type metadata, capture native XML or supported generated source, and follow native cross-references. Content capture requires ReadWrite because Siemens exports through a temporary file. |
| Inspect hardware and networks | Read module identities, I/O addresses, IP/node settings and PROFINET controller assignments. Search the hardware catalogue on V19. |
| Build and configure offline hardware | Create catalogue-verified devices, plug modules, configure addresses and nodes, and create or assign PROFINET I/O systems. |
| Compile and review changes | Compile software or hardware and page through native errors, warnings and diagnostic details. Export local-project device configuration as AML with its native log. |
| Work beyond PLC code | Discover public HMI, library, technology-object, drive, OPC, safety and collaboration surfaces through the general Openness bridge, where the installed Siemens API provides them. |
| Inspect online information | Read native engineering connection state or make bounded PLC value reads against an explicit target. |

For example, ask your assistant:

- “Explain how `FC_Pump` works and find the symbols it references.”
- “Show the I/O addresses and PROFINET controller assignments for this station.”
- “Create the requested module configuration, compile it, and show the diagnostics before saving.”
- “Find the public library and HMI services available in this project.”

Content capture and engineering changes require ReadWrite.
Availability depends on the project, installed modules, licences and TIA version.

## What is in the MCP interface?

Two layers share the same connection and project context:

**Task-focused tools** provide short, named operations for common workflows.
**The general public API bridge** lets an AI discover and use additional public
Siemens contracts through a small, consistent set of operations.

| Interface area | Representative tools |
| --- | --- |
| Capabilities and tool discovery | `GetCapabilities`, `GetOperations`, `GetOperationInfo` |
| Connection and project state | `GetTiaProcesses`, `Connect`, `GetState`, `ListProjects` |
| Project and PLC browsing | `GetDevices`, `GetBlocks`, `GetTypes`, `GetPlcTagTables`, `GetPlcTags` |
| Logic and content | `ReadBlockContent`, `ReadTypeContent`, `FindReferences`, `GetExternalSources` |
| Hardware workflows | `GetHardwareNetworkInfo`, `FindHardwareCatalog`, `CreateHardwareDevice`, `PlugHardwareModule`, `ConfigureIoAddress`, `ConfigureHardwareNode` |
| Compile, import and export | `CompileSoftware`, `CompileHardware`, `GetCompileDiagnostics`, `ImportBlock`, `ExportBlock`, `ExportDeviceConfiguration` |
| Public API discovery and reads | `ListApiTypes`, `DescribeApiObject`, `ReadApiProperty`, `ReadApiField`, `BrowseApiCollection`, `GetApiService` |
| Public native operations | `InvokeApiMethod`, `CreateApiObject`, `WriteApiProperty`, `CreateEngineeringObject`, `UpdateEngineeringAttributes`, `InvokeEngineeringAction` |
| Events, callbacks and resources | `SubscribeApiEvent`, `ReadApiEvents`, `UnsubscribeApiEvent`, `ReleaseApiObject`; declarative callback arguments for synchronous native decisions |

The worker reports its exact available tools and parameter schemas at runtime.
Object references and paged results help the assistant reuse discovered targets
and request only the data it needs. The bridge uses public Siemens contracts;
it does not evaluate arbitrary C# or provide UI-only editor functions.

See [the complete interface guide](docs/public-api-interface.md) for native
creation/action schemas, generic methods, ref/out values, callback policies,
file access and reference lifetimes.

## Choose your access profile

| Profile | Use it for | What it exposes |
| --- | --- | --- |
| **Read** | Project exploration and inspection | Query tools, public API schemas, property/field reads, collection browsing and declared services. |
| **ReadWrite** | Deliberate engineering operations | Read tools plus project lifecycle, content capture, compile, import/export, hardware changes and guarded generic native calls. |

The profiles are separate binaries. Installing Read does not expose the
ReadWrite commands. Enable one profile for the same TIA Portal process at a time.

Generic ReadWrite native calls require an exact expected project context and
explicit acceptance of unclassified effects. Depending on the selected native
method, those effects can include files, downloads, CPU commands or security
changes. ReadWrite extends beyond the typed offline workflows; inspect native
results before retrying a failed operation.

## TIA Portal versions

**The current release bundles exact workers for TIA Portal V17, V18 and V19.**

| TIA Portal version | Included in alpha.3 | Current evidence |
| --- | --- | --- |
| **V17** | Read and ReadWrite | Compiled; runtime-unverified |
| **V18** | Read and ReadWrite | Compiled; runtime-unverified |
| **V19** | Read and ReadWrite | Compiled; runtime-unverified |
| V20 | No | Deferred to a later release |
| V21 | No | Requires a dedicated adapter |
| V16 and earlier | No | No worker included |

Choose an access profile, then select a TIA version. The included broker starts
the matching worker. `Auto` succeeds only when exactly one bundled version is
installed; otherwise set `V17`, `V18` or `V19` explicitly.

Some functions vary by version: native cross-references and hardware type
replacement require V18 or newer; catalogue lookup, catalogue-verified device
creation and accessible-device discovery require V19. SIMATIC SD document
operations are not included in this release.

Bundled means an exact worker is provided, not that every native workflow has
been runtime-qualified. Siemens runtime DLLs are loaded from your local
installation and are not included in the downloads.

## Installation

### 1. Prepare the Windows PC

Install and run the client on the Windows PC where TIA Portal is installed.
You need:

- Windows x64 and .NET Framework 4.8.
- TIA Portal V17, V18 or V19 with its matching Openness API installed.
- Licences and installed Siemens modules for the engineering functions you use.
- Your Windows account in the **Siemens TIA Openness** local group.
- An MCP client, such as Claude Desktop or a local VS Code/Copilot session.

<details>
<summary>Set up Siemens Openness group membership</summary>

Ask your administrator to open **Computer Management**, locate the local
**Siemens TIA Openness** group under **Local Users and Groups**, and add your
Windows account. Fully sign out of Windows and sign in again afterwards.
The MCP does not change group membership for you.

See [Siemens' group setup instructions](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/basics/installation/adding-users-to-the-siemens-tia-openness-user-group).
This Windows access setup also applies to the bundled earlier versions; the
link's V21 documentation does not imply a V21 worker is included.

</details>

Open the project in the matching TIA Portal version. Respond to TIA's Openness
authorisation prompt if it appears when connecting.

### 2. Download a profile

Open the [alpha.3 release](https://github.com/Tim-Tadj/tiaportal-mcp/releases/tag/0.1.0-alpha.3)
and download the package matching your client and intended access:

| Client | Read package | ReadWrite package |
| --- | --- | --- |
| Claude Desktop | `tia-portal-mcp-0.1.0-alpha.3-read-win-x64.mcpb` | `tia-portal-mcp-0.1.0-alpha.3-readwrite-win-x64.mcpb` |
| VS Code, other local stdio clients, or the ChatGPT tunnel kit | `tia-portal-mcp-0.1.0-alpha.3-read-win-x64.zip` | `tia-portal-mcp-0.1.0-alpha.3-readwrite-win-x64.zip` |

Use a profile package from the release assets, rather than GitHub's automatic
source-code ZIP. No source checkout, .NET SDK, Node.js or Python is needed to
run the packaged broker and workers.

The packages are unsigned. Download `SHA256SUMS.txt` from the same release and
compare the matching entry with your file's hash, for example:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath `
    .\tia-portal-mcp-0.1.0-alpha.3-read-win-x64.zip
```

### 3A. Claude Desktop

1. Download the chosen `.mcpb`.
2. In Claude Desktop settings, open the desktop extension manager. Current
   versions place it under **Desktop app**, next to **Extensions**; older
   layouts have an **Extensions** settings page.
3. Open **Advanced settings**, choose **Install Extension**, and select the file.
4. Set the TIA version to `V17`, `V18` or `V19`, or use `Auto` for one installed
   bundled version. The ReadWrite package also asks for an export output directory.
5. Enable the extension for the conversation and continue with the first
   connection sequence below.

Organisation policies may require an administrator to allow the extension.
See [Claude's installation guidance](https://support.claude.com/en/articles/10949351-getting-started-with-local-mcp-servers-on-claude-desktop)
and the [profile installation/removal guide](packaging/clients/claude/README.md).

### 3B. VS Code / Copilot

1. Extract the whole profile ZIP to a permanent folder, for example
   `C:\Tools\TiaPortalMcp\Read`. Keep `server`, `clients` and `manifest.json` together.
2. Open a local Windows VS Code session. In PowerShell from that extracted
   folder, run the packaged installer with your exact TIA version:

```powershell
Set-Location 'C:\Tools\TiaPortalMcp\Read'
.\clients\vscode\Install-VsCodeMcp.ps1 -TiaVersion V19
```

3. The installer uses `code --add-mcp` to add the fixed profile to VS Code's
   user-profile configuration. With **MCP: List Servers**, locate the TIA server,
   start it and review the client trust prompt. Make its tools available to the
   intended agent session.

The `code` command must be on PATH. The helper's `-PrintConfiguration` option
shows the generated configuration without installing it. For newer native
Copilot configuration destinations, use **MCP: Add Server**, choose a stdio
command, and supply the broker command and arguments below. Select the
configuration destination used by your intended session.

See [VS Code's current MCP configuration guidance](https://code.visualstudio.com/docs/agents/reference/mcp-configuration)
and the [full VS Code guide](packaging/clients/vscode/README.md).

### 3C. Other local MCP clients

Register a stdio server using the extracted broker. A Read/V19 example is:

```json
{
  "command": "C:\\Tools\\TiaPortalMcp\\Read\\server\\TiaPortalMcp.exe",
  "args": ["--access-profile", "Read", "--tia-version", "V19"]
}
```

Place those fields inside the configuration structure your client expects.
For ReadWrite, use its extracted broker and matching `ReadWrite` argument.
Add `--output-root` and an approved directory when choosing another export root.
The version-selection broker removes the need to configure a different worker
executable for every TIA version.

### 3D. ChatGPT through Secure MCP Tunnel

The ZIP contains a local connection kit for OpenAI Secure MCP Tunnel. TIA Portal
and the MCP remain on your Windows PC; selected MCP requests and results travel
through OpenAI over outbound HTTPS. No public inbound listener is required.

1. Use [OpenAI's tunnel setup guide](https://developers.openai.com/api/docs/guides/secure-mcp-tunnels)
   to create a tunnel, associate the intended workspace, obtain the required
   permissions and download `tunnel-client` onto the Windows PC.
2. Follow the [packaged ChatGPT guide](packaging/clients/chatgpt/README.md) to
   provide its runtime API key to the current PowerShell process. From the
   extracted profile, configure and start it:

```powershell
.\clients\chatgpt\Configure-ChatGptTunnel.ps1 `
    -TunnelId tunnel_0123456789abcdef0123456789abcdef `
    -TiaVersion V19
.\clients\chatgpt\Start-ChatGptTunnel.ps1
```

3. Replace the example tunnel ID with your own and keep the launcher running.
   In ChatGPT web, add a custom MCP server from **Plugins**, select the **Tunnel**
   connection and your tunnel, complete creation, then install/select the plugin.
   Workspace permissions apply.

Use the [official custom MCP server instructions](https://developers.openai.com/api/docs/guides/custom-mcp-server)
for current account access and menus. This kit is a tunnel integration, rather
than a direct ChatGPT Desktop stdio installer.

## Your first connection

Once the client has the server available, ask:

> Use TIA Portal MCP to report its capabilities and installed worker version.
> List the running TIA Portal processes, connect to the instance I select,
> show the selected project and list its devices. Make no changes.

The assistant should discover `GetCapabilities`, use `GetTiaProcesses` and
`Connect` with an exact process ID, then check `GetState`. Confirm the returned
access profile, TIA version and project path before starting engineering work.
These are user-run connection steps; they were not executed for this release.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| Several TIA versions are installed | Set the client to an exact `V17`, `V18` or `V19` instead of `Auto`. |
| Access denied or `EngineeringSecurityException` | Check the Openness group membership, sign out/in after changes, and respond to TIA's connection prompt. |
| No running TIA process | Start the matching TIA version and open the project. Read attaches to an existing instance. |
| Client cannot find the executable | Keep the complete extracted bundle in place and check the absolute `server\TiaPortalMcp.exe` path. Use a local Windows client session. |
| A PowerShell helper is blocked | After verifying the ZIP, use `Unblock-File` on that specific extracted helper if Mark-of-the-Web is the cause. Keep the global execution policy unchanged. |
| A content or engineering tool is missing | Check the selected profile and `GetOperations`. Native content capture needs ReadWrite; some tools need V18/V19 or an installed Siemens module. |
| A native call fails | Re-read the target state before retrying a write. Native operations can have partial effects. See the returned diagnostics and interface guide. |

## Documentation and development

- [Public API interface](docs/public-api-interface.md): schemas, values, callbacks and native effects.
- [General AI workflows](docs/general-ai-workflows.md): worked sequences and proposed evaluation cases.
- [Coverage map](docs/openness-coverage.md): exact-version and native API boundaries.
- [Release notes](docs/alpha3-release.md): packages, compatibility and build-only evidence.
- [Hardware tools](docs/hardware-tools.md) and [online tools](docs/online-tools.md).
- [Build guide](build/README.md), [test prerequisites](tests/TiaMcpServer.Test/README.md) and [contributor instructions](AGENTS.md).
- [Output contract](docs/output-formats.md) and [error model](docs/error-model.md).

The release includes no Siemens runtime DLLs. Private APIs, UI-only actions and
arbitrary .NET execution are outside the interface. Tests, client installation
qualification and licensed runtime coverage remain outstanding. The project is
licensed under [MIT](LICENSE.txt).
