# ChatGPT Local Connection

Status date: 29 July 2026.

## Required Deployment Boundary

The TIA Portal MCP broker, selected worker, Siemens Openness integration and
OpenAI tunnel client run on the user's Windows PC. This project will not deploy
an MCP server, worker or TIA-facing gateway to a hosted service.

The ChatGPT connection kit must:

- install the selected Read or ReadWrite bundle locally;
- start the fixed-profile broker and exact-version worker locally;
- run the OpenAI `tunnel-client` locally;
- launch the selected stdio broker through `--mcp-command`;
- make no public inbound port available;
- use an outbound Secure MCP Tunnel connection;
- stop the local tunnel client and owned worker when the user disables the
  connection;
- retain logs and configuration locally.

Services such as public reverse proxies, hosted containers, cloud functions and
third-party tunnels are outside the supported architecture.

## Current ChatGPT Constraint

ChatGPT cannot currently connect directly to a local MCP server or launch this
stdio executable itself. OpenAI directs private-network, on-premises and
developer-PC servers to use
[Secure MCP Tunnel](https://developers.openai.com/api/docs/guides/secure-mcp-tunnels)
so that the server is not exposed to the public internet. The current tunnel
client supports a local stdio command directly through `--mcp-command`, so this
alpha does not require a Streamable HTTP adapter.

The same guidance currently describes full MCP support on ChatGPT web for
Business and Enterprise/Edu workspaces. This project must not advertise direct
ChatGPT Desktop installation until OpenAI documents that capability. The local
connection kit remains useful because every TIA-facing component still runs on
the user's PC. See
[Developer mode and MCP apps in ChatGPT](https://help.openai.com/en/articles/12584461)
for current plan, role and workspace requirements.

The intended connection is:

```text
ChatGPT
  -> OpenAI Secure MCP Tunnel
  -> local tunnel client on the user's PC
  -> local fixed-profile stdio broker launched by --mcp-command
  -> local exact-version worker
  -> local TIA Portal instance
```

The OpenAI service and its tunnel relay are necessarily involved because
ChatGPT itself is not a fully local application. No `tia-portal-mcp` component
is hosted there. If the requirement is that no MCP request or response content
may leave the PC, ChatGPT cannot satisfy that requirement; use a fully local MCP
client instead.

The tunnel client opens outbound HTTPS to OpenAI and forwards queued MCP
JSON-RPC work to the local stdio command. The MCP server needs no HTTP listener,
public DNS name or inbound firewall rule.

## Data Boundary

The connection kit must not upload project files, export directories, logs or
Siemens assemblies. It must not perform background indexing or synchronisation.

Tool names, arguments and the selected result content are sent to ChatGPT when
the user invokes the MCP app. Users must therefore avoid requesting sensitive
project details unless their ChatGPT workspace policy permits that content.
ReadWrite actions remain subject to the profile boundary and ChatGPT action
confirmation.

Compact CSV or TOON rendering can reduce syntactic overhead, but it does not
change this data boundary. Values returned by a tool can still leave the PC as
part of the selected result content. The tunnel path must preserve the server's
`responseFormat` choice and selected-format, `returned`, `hasMore` and
`nextCursor` metadata without reformatting or duplicating full rows.

The model-facing format policy is defined in [Model-Facing Output Format
Policy](output-formats.md). MCP JSON-RPC traffic, tool schemas, tunnel
configuration, installer configuration and manifests remain JSON. CSV and
TOON apply only to eligible successful tool result text inside that protocol.

## Packaging Plan

The ChatGPT package is a local connection kit, not a hosted deployment or a
direct ChatGPT Desktop installer. For `0.1.0-alpha.1` it contains:

1. the selected profile bundle;
2. separate Read and ReadWrite `tunnel-client` profile templates using
   `--mcp-command`;
3. tunnel and ChatGPT custom-app registration instructions;
4. a configure helper and a start helper which runs the tunnel doctor first;
5. a clear display of the active profile and broker command.

The operator supplies a tunnel ID and runtime API key according to OpenAI's
instructions. Secrets must not be committed, embedded in the release or printed
by diagnostics. The helper must derive its broker path from the extracted
installation directory rather than a developer checkout.

The start helper uses
`tunnel-client doctor --profile <profile> --explain` before running
`tunnel-client run --profile <profile>`. The operator stops the foreground
tunnel with Ctrl+C. The installer must keep the Read and ReadWrite profiles
distinct.

Removal starts by stopping that foreground process with Ctrl+C. The user then
disconnects the custom app from **Settings > Apps**, or a workspace
administrator disables it in **Workspace settings > Apps**. An unused hosted
tunnel endpoint is removed through OpenAI Platform tunnel settings. OpenAI's
current public guide does not document a local `tunnel-client` profile-deletion
command, so users must follow the profile or configuration guidance supplied
with their installed official tunnel tooling. The project must not invent a
command or delete a shared tunnel configuration directory.

The source configuration and launcher scripts are documented in the
[ChatGPT client packaging guide](../packaging/clients/chatgpt/README.md). Bundle
assembly copies these helpers into each fixed-profile release artefact.

## Definition of Done

The ChatGPT alpha kit is releasable only when:

- all project components execute on the user's PC;
- the tunnel client launches the selected local stdio broker;
- no MCP listener is required or reachable from another machine;
- no public DNS name, public inbound firewall rule or third-party hosting is
  required;
- the Read and ReadWrite installations cannot be confused;
- stopping the connection terminates the local tunnel client, broker and owned
  worker;
- a non-TIA installation, doctor and stop flow have been exercised;
- the documented app, hosted endpoint and local profile removal flow has been
  reviewed without inventing a tunnel-client command;
- documentation states which MCP request and response content is sent to
  ChatGPT;
- the tunnel path passes `responseFormat` through unchanged and preserves the
  returned format and paging metadata;
- JSON-RPC, configuration and manifests remain JSON even when tool result text
  is CSV or TOON.

Code signing, a formal SBOM, production lifecycle management and further
TIA-dependent runtime tests are deferred from `0.1.0-alpha.1`. The complete
supported-release requirements remain in [Current Status](status.md).
