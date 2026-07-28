# Agents Guide

This repository can be used with agentic coding assistants. Follow these guidelines to collaborate safely and efficiently.

## Project Overview

TIA Portal MCP is a .NET Framework 4.8 console application which exposes the
Siemens TIA Portal Openness API through the Model Context Protocol. LLM clients
use MCP tools to inspect TIA Portal state and, in the ReadWrite profile, perform
controlled project operations.

The target distribution has two public profiles:

- `Read`, which contains query capabilities only;
- `ReadWrite`, which adds controlled lifecycle, compile, import and export
  operations.

The dependency-free broker in `src/TiaMcpBroker` detects installed TIA Portal
versions and launches an exact-version worker. Users choose a profile, not a
version-specific download. See `docs/architecture.md` for the design and
`docs/status.md` for implementation and support status.

## Repository Map

- `src/TiaMcpBroker/`: profile-locked version detection, worker selection and
  stdio forwarding. It must not reference Siemens assemblies.
- `src/TiaMcpServer/ModelContextProtocol/`: MCP tools, workflow prompts,
  compact response contracts and capability metadata.
- `src/TiaMcpServer/Siemens/`: the high-level TIA Portal wrapper and Openness
  initialisation.
- `src/TiaMcpServer/Runtime/`: worker command-line parsing and runtime version
  selection.
- `src/TiaMcpServer/Security/`: access-profile, output-path and bounded-input
  policies.
- `tests/TiaMcpServer.Test/`: tests which may require TIA Portal, a suitable
  licence and project assets.
- `build/` and `packaging/`: exact-worker builds and the two public bundle
  definitions.
- `docs/`: architecture, roadmap, current status and changelog policy.

Keep protocol concerns in `ModelContextProtocol` and Siemens API concerns in
`Siemens`. Stdio standard output is reserved for MCP traffic. Diagnostics belong
on standard error or in configured logs.

## Functional Surface

The server covers connection and state, projects and sessions, devices, PLC
software, blocks and types. The ReadWrite profile also exposes controlled
project lifecycle, compilation, XML import and export, and compatible SIMATIC
SD document operations. Treat `GetCapabilities` as the authoritative runtime
view because profile and TIA Portal version affect availability.

When changing session discovery, verify that `GetOpenSessions` returns reliable
full project paths for both local and remote multiuser sessions. The Openness
API can vary between these session types, so this requires licensed runtime
coverage before it is described as supported.

## Test Execution Policy

- Offer to run tests, but only run them after explicit user confirmation.
- Tests may require user-specific environment conditions, such as an installed
  TIA Portal, licences and PLC project assets, so do not assume they will pass
  in your environment.
- When offering to run tests, clearly state prerequisites and potential side effects.
- If the user declines or does not respond, provide concise instructions for the user to run tests locally instead of running them yourself.

### Standard Commands

```powershell
dotnet test
```

If tests need to write to temporary locations or access external resources, note these requirements up front.

## How To Ask For Confirmation

Use clear, actionable language. For example:

- "I can run `dotnet test` to validate the changes. Some tests require TIA Portal and project assets on this machine. Do you want me to run them now?"
- If approved: proceed and summarize results. If not approved: provide steps the user can run.

## Environment Considerations

- Respect the user's environment constraints, such as offline operation,
  restricted permissions and licensed software.
- If a command fails due to environment limitations, do not retry destructively; report the exact failure and suggest alternatives.
- Document any meaningful limitations or deviations in the commit message or relevant README, per the Contributor Guidelines.


## Formatting & Encoding

- Preserve existing indentation style (tabs vs. spaces).
- Do not modify file encodings; keep UTF-8 BOM where present.
- Ensure Windows CRLF line endings are retained when editing files.
