# 0.1.0-alpha.2 Release Gate

Status date: 11 November 2026.

## Purpose

`0.1.0-alpha.2` is an experimental Windows x64 prerelease for early evaluation.
It is not a supported release. It was
[published as a GitHub prerelease](https://github.com/Tim-Tadj/tiaportal-mcp/releases/tag/0.1.0-alpha.2)
on 11 November 2026.

This update patches [issue #1](issue-1.md). The alpha retains the two-profile
package design, version broker, compact MCP
contract and local client installation paths. It does not claim that any
published worker passed a live-project runtime test.

## Package and TIA Portal Matrix

Both public packages contain exact-version workers selected by the broker.
Users choose an access profile, not a version-specific download.

This is the published package matrix. Publication required every listed
bundled worker to compile in the prepared release environment.

| TIA Portal version | Read | ReadWrite | Alpha classification |
| --- | --- | --- | --- |
| V17 | Bundled | Bundled | `experimental-build-only`; runtime-unverified |
| V18 | Bundled | Bundled | `experimental-build-only`; runtime-unverified |
| V19 | Bundled | Bundled | `experimental-prior-runtime-evidence`; `runtimeValidated=false` |
| V20 | Excluded | Excluded | Planned for a later alpha; use legacy v0.0.18 for the current V20 build |
| V21 | Excluded | Excluded | Unsupported in this alpha |

Historical alpha.1 V19 Read and ReadWrite builds completed MCP initialisation, profile
capability reporting, connection to an open project and bounded read queries
through fixed-profile brokers. That evidence predates the Siemens-DLL-free
resolver used by the published bits and informs confidence only. The exact V19
release bits have build, strong-name preflight and Siemens-free validation, but
have not been run against a live TIA Portal project.

The broker must reject V20, V21 and missing or incompatible installations
before a Portal operation when no matching worker is bundled, with a concise
recovery message. Release notes, manifests and client installers must use the
classifications above without describing V17 or V18 as tested or supported, or
the published V19 bits as runtime-validated.

No Siemens-supplied runtime or object-code DLL is included in either package.
The selected worker loads its exact Openness assemblies from the user's
matching local TIA Portal installation.

## Client Scope

| Client | Alpha delivery | Boundary |
| --- | --- | --- |
| Claude Desktop | Separate Read and ReadWrite `.mcpb` packages | The MCPB installs and launches the selected local profile bundle. |
| VS Code | Direct stdio configuration and install helpers for each profile | No VSIX is required or included in the alpha. |
| ChatGPT | Local tunnel-client configuration for a custom MCP app using OpenAI Secure MCP Tunnel | The tunnel client launches the selected stdio broker with `--mcp-command`. This is not a direct ChatGPT Desktop installer. |

For ChatGPT, the tunnel client, broker, worker and TIA Portal integration run
on the user's PC. The tunnel client launches the local stdio broker directly,
so this alpha does not require an HTTP adapter or listener. No project
component is hosted by this project and no public inbound port is opened. The
tunnel makes an outbound HTTPS connection, and MCP tool requests and selected
results pass through OpenAI when the custom app is used. See
[ChatGPT Local Connection](chatgpt-local.md).

## Required Publication Gate

The alpha publication gate required all of the following non-TIA checks:

- [x] product, informational, bundle, MCPB and release-manifest versions are
  `0.1.0-alpha.2`, while numeric Windows assembly and file versions are
  `0.1.0.0`;
- [x] Read and ReadWrite workers build for V17, V18 and V19 on Windows x64;
- [x] Siemens-free contract, broker, schema, profile-boundary and strong-name
  assembly preflight checks pass;
- [x] offline `tools/list` checks prove that Read exposes no mutation tools;
- [x] Siemens-free checks prove that the worker operation gate serialises MCP
  tool calls before they reach TIA-facing services;
- [x] both profile ZIPs and both profile MCPBs are assembled and their
  manifests validate;
- [x] the VS Code helper renders a relocatable direct-stdio configuration for
  each extracted profile without launching TIA Portal;
- [x] the ChatGPT helper renders a relocatable `--mcp-command` for each profile
  and its tunnel-client arguments are validated without invoking TIA Portal;
- [x] each client package contains reviewed removal instructions which avoid
  deleting unrelated client configuration;
- [x] every package manifest and build-metadata record maps the fixed profile
  and bundled workers correctly;
- [x] missing TIA Portal, not-bundled V20, unsupported V21 and profile
  mismatches return bounded actionable diagnostics;
- [x] the published release contains its manifest, SHA-256 checksums,
  prerequisites and third-party notices, and package inspection confirms no
  Siemens-supplied runtime or object-code DLL is present;
- [x] the
  [GitHub release](https://github.com/Tim-Tadj/tiaportal-mcp/releases/tag/0.1.0-alpha.2)
  is marked as a prerelease and prominently links this support matrix.

The blocking client check for this alpha is static and package-level. It
validates each archive, renders the relocatable VS Code and ChatGPT helper
output, checks missing-installation diagnostics and reviews the
client-specific removal guidance:

- remove a VS Code registration through **MCP: List Servers**, or remove only
  its entry from workspace `mcp.json`;
- remove each Claude Desktop MCPB through **Settings > Extensions**;
- stop the ChatGPT tunnel process, disconnect or disable the custom app, and
  remove the tunnel endpoint and local profile through official tunnel
  management guidance.

The guidance must tell users to confirm that no client still refers to the
extracted bundle before deleting it. It must not invent a `tunnel-client`
profile-deletion command or delete shared client configuration. Live
clean-account installation and removal execution is deferred from this
experimental alpha. These static checks do not launch a worker or invoke
Siemens Openness.

## Runtime Testing Position

Focused V19 tests for issue #1 use a disposable copy of the bench project.
See [Issue #1 changes](issue-1.md) for the tested paths and remaining limits.
These checks do not constitute the full supported-release runtime matrix.
The manifest therefore retains `runtimeValidated=false` for each worker.
V17 and V18 remain `experimental-build-only`; V19 retains the conservative
`experimental-prior-runtime-evidence` classification.

The exact release packages receive assembly, profile and client packaging
checks. No PLC downloads or output operations are part of this release gate.
Production support still requires the full matrix in [Current Status](status.md).

## Explicitly Deferred

The following work does not block `0.1.0-alpha.2`:

- further licensed TIA Portal runtime testing;
- the V20 exact-version worker and its SIMATIC SD document operations, planned
  for a later alpha;
- unsupported V21 and its modular Openness adapter;
- production support claims for any profile or TIA Portal version;
- a VS Code extension or VSIX;
- direct ChatGPT Desktop stdio installation;
- an authenticated ChatGPT-to-TIA end-to-end tool scan, while the tunnel helper
  itself is validated without TIA Portal;
- live clean-account client installation, start, stop and removal execution;
- code signing, formal SBOM and provenance attestations, provided the alpha is
  clearly identified as unsigned and includes checksums and dependency
  notices;
- large-project performance and model-accuracy evaluation;
- broader tool surfaces and remaining upstream pull-request adaptations;
- generated metadata manifests and clean-account TIA runtime certification
  required by the supported-release definition of done;
- handle-level output-path race hardening, a separate import-source root and
  atomic two-file replacement. ReadWrite remains an experimental profile and
  should use a dedicated output root with backups until these controls are
  complete.

## Artefact Names

The published GitHub prerelease contains exactly these six assets:

```text
tia-portal-mcp-0.1.0-alpha.2-read-win-x64.zip
tia-portal-mcp-0.1.0-alpha.2-readwrite-win-x64.zip
tia-portal-mcp-0.1.0-alpha.2-read-win-x64.mcpb
tia-portal-mcp-0.1.0-alpha.2-readwrite-win-x64.mcpb
SHA256SUMS.txt
release-manifest.json
```

The VS Code and ChatGPT helpers are contained within each profile package. They
select one immutable profile and do not modify package contents after checksum
verification.
