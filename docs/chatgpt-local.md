# ChatGPT Local Connection

Status date: 28 July 2026.

## Required Deployment Boundary

The TIA Portal MCP broker, selected worker, Siemens Openness integration and any
ChatGPT transport adapter must run on the user's Windows PC. This project will
not deploy an MCP server, worker or TIA-facing gateway to a hosted service.

The ChatGPT connection kit must:

- install the selected Read or ReadWrite bundle locally;
- start the fixed-profile broker and exact-version worker locally;
- run any Streamable HTTP adapter and tunnel client locally;
- bind a local HTTP listener to loopback only;
- make no public inbound port available;
- use an outbound Secure MCP Tunnel connection;
- stop the local adapter and worker when the user disables the connection;
- retain logs and configuration locally.

Services such as public reverse proxies, hosted containers, cloud functions and
third-party tunnels are outside the supported architecture.

## Current ChatGPT Constraint

ChatGPT cannot currently connect directly to a local MCP server or launch this
stdio executable. OpenAI directs private-network, on-premises and developer-PC
servers to use Secure MCP Tunnel so that the server is not exposed to the public
internet. See
[Developer mode and MCP apps in ChatGPT](https://help.openai.com/en/articles/12584461).

The same guidance currently describes full MCP support on ChatGPT web for
Business and Enterprise/Edu workspaces. This project must not advertise direct
ChatGPT Desktop installation until OpenAI documents that capability. The local
connection kit remains useful because every TIA-facing component still runs on
the user's PC.

The intended connection is:

```text
ChatGPT
  -> OpenAI Secure MCP Tunnel
  -> local tunnel client on the user's PC
  -> loopback-only MCP transport adapter on the user's PC
  -> local fixed-profile broker
  -> local exact-version worker
  -> local TIA Portal instance
```

The OpenAI service and its tunnel relay are necessarily involved because
ChatGPT itself is not a fully local application. No `tia-portal-mcp` component
is hosted there. If the requirement is that no MCP request or response content
may leave the PC, ChatGPT cannot satisfy that requirement; use a fully local MCP
client instead.

## Data Boundary

The connection kit must not upload project files, export directories, logs or
Siemens assemblies. It must not perform background indexing or synchronisation.

Tool names, arguments and the selected result content are sent to ChatGPT when
the user invokes the MCP app. Users must therefore avoid requesting sensitive
project details unless their ChatGPT workspace policy permits that content.
ReadWrite actions remain subject to the profile boundary and ChatGPT action
confirmation.

## Packaging Plan

The ChatGPT package is a local connection kit, not a hosted deployment. It will
contain:

1. the selected signed profile bundle;
2. a loopback-only Streamable HTTP adapter if the Secure MCP Tunnel client
   cannot launch the stdio broker directly;
3. local service or tray-process lifecycle management;
4. tunnel registration instructions;
5. doctor, start, stop, update and uninstall commands;
6. a clear display of the active profile, TIA version and local process IDs.

The final transport shape must be verified against the current Secure MCP Tunnel
client before release. No bespoke `HttpListener` JSON bridge is accepted as an
MCP transport.

## Definition of Done

The ChatGPT adapter is releasable only when:

- all project components execute on the user's PC;
- the MCP endpoint is not reachable from another machine;
- no public DNS name, public inbound firewall rule or third-party hosting is
  required;
- the Read and ReadWrite installations cannot be confused;
- stopping the connection terminates the local adapter, broker and owned worker;
- a clean-machine installation and uninstall have been exercised;
- captured network traffic confirms that only the tunnel connection leaves the
  machine;
- documentation states which MCP request and response content is sent to
  ChatGPT.
