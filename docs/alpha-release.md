# 0.1.0-alpha.1 Release Gate

Status date: 29 July 2026.

## Purpose

`0.1.0-alpha.1` is an experimental Windows x64 prerelease for early evaluation.
It is not a supported release and must be published as a GitHub prerelease.

The alpha proves the two-profile package design, version broker, compact MCP
contract and local client installation paths. It deliberately does not claim
runtime validation for every bundled worker.

## Package and TIA Portal Matrix

Both public packages contain exact-version workers selected by the broker.
Users choose an access profile, not a version-specific download.

This is the target publication matrix. The release must not be published until
every listed bundled worker has compiled in the prepared release environment.

| TIA Portal version | Read | ReadWrite | Alpha classification |
| --- | --- | --- | --- |
| V17 | Bundled | Bundled | Experimental, build-only and runtime-unverified |
| V18 | Bundled | Bundled | Experimental, build-only and runtime-unverified |
| V19 | Bundled | Bundled | Experimental and runtime-validated on a licensed open project |
| V20 | Bundled | Bundled | Experimental, build-only and runtime-unverified |
| V21 | Excluded | Excluded | Unsupported in this alpha |

The V19 evidence covers MCP initialisation, profile capability reporting,
connection to an open project and bounded read queries through fixed-profile
brokers. It does not turn either profile into a supported production release.

The broker must reject V21 and missing or incompatible installations before a
Portal operation, with a concise recovery message. Release notes, manifests
and client installers must use the classifications above without describing
V17, V18 or V20 as tested or supported.

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

The alpha may be published when all of the following non-TIA checks pass:

- [ ] product, informational, bundle, MCPB and release-manifest versions are
  `0.1.0-alpha.1`, while numeric Windows assembly and file versions are
  `0.1.0.0`;
- [ ] Read and ReadWrite workers build for V17, V18, V19 and V20 on Windows
  x64;
- [ ] Siemens-free contract, broker, schema and profile-boundary checks pass;
- [ ] offline `tools/list` checks prove that Read exposes no mutation tools;
- [ ] Siemens-free checks prove that the worker operation gate serialises MCP
  tool calls before they reach TIA-facing services;
- [ ] both profile ZIPs and both profile MCPBs are assembled and their
  manifests validate;
- [ ] the VS Code helper renders a relocatable direct-stdio configuration for
  each extracted profile without launching TIA Portal;
- [ ] the ChatGPT helper renders a relocatable `--mcp-command` for each profile
  and its tunnel-client arguments are validated without invoking TIA Portal;
- [ ] each client package contains removal instructions, and a clean-account
  check confirms that its client registration and owned local process can be
  removed without deleting unrelated configuration;
- [ ] every package manifest and build-metadata record maps the fixed profile
  and bundled workers correctly;
- [ ] missing TIA Portal, unsupported V21 and profile mismatches return bounded
  actionable diagnostics;
- [ ] release manifest, SHA-256 checksums, prerequisites, third-party notices
  and Siemens redistribution decision are published;
- [ ] the GitHub release is marked as a prerelease and prominently links this
  support matrix.

Installation checks may use a clean Windows account or VM without TIA Portal.
They should verify extraction, configuration and missing-installation
diagnostics, then follow the client-specific removal guide:

- remove a VS Code registration through **MCP: List Servers**, or remove only
  its entry from workspace `mcp.json`;
- remove each Claude Desktop MCPB through **Settings > Extensions**;
- stop the ChatGPT tunnel process, disconnect or disable the custom app, and
  remove the tunnel endpoint and local profile through official tunnel
  management guidance.

The check should confirm that no client still refers to the extracted bundle
before deleting it. It must not invent a `tunnel-client` profile-deletion
command or delete shared client configuration. These checks do not need to
launch a worker or invoke Siemens Openness.

## Runtime Testing Position

No additional TIA-dependent test is required to publish this alpha. The
release freezes the existing V19 evidence and reports V17, V18 and V20 as
runtime-unverified. Any failure report for an unverified worker is an alpha
compatibility finding, not a regression against a support promise.

A later alpha may promote another worker only after its matching licensed TIA
Portal runtime has been exercised. Production support still requires the full
validation matrix in [Current Status](status.md).

## Explicitly Deferred

The following work does not block `0.1.0-alpha.1`:

- further licensed TIA Portal runtime testing;
- V21 and its modular Openness adapter;
- production support claims for any profile or TIA Portal version;
- a VS Code extension or VSIX;
- direct ChatGPT Desktop stdio installation;
- an authenticated ChatGPT-to-TIA end-to-end tool scan, while the tunnel helper
  itself is validated without TIA Portal;
- code signing, formal SBOM and provenance attestations, provided the alpha is
  clearly identified as unsigned and includes checksums and dependency
  notices;
- large-project performance and model-accuracy evaluation;
- broader tool surfaces and remaining upstream pull-request adaptations;
- generated metadata manifests and clean-account TIA runtime certification
  required by the supported-release definition of done.
- handle-level output-path race hardening, a separate import-source root and
  atomic two-file replacement. ReadWrite remains an experimental profile and
  should use a dedicated output root with backups until these controls are
  complete.

## Artefact Names

Release automation should produce unambiguous profile-specific names:

```text
tia-portal-mcp-0.1.0-alpha.1-read-win-x64.zip
tia-portal-mcp-0.1.0-alpha.1-readwrite-win-x64.zip
tia-portal-mcp-0.1.0-alpha.1-read-win-x64.mcpb
tia-portal-mcp-0.1.0-alpha.1-readwrite-win-x64.mcpb
SHA256SUMS.txt
release-manifest.json
```

The VS Code and ChatGPT helpers may be shipped as scripts or configuration
files alongside these artefacts. They must select one immutable profile and
must not modify package contents after checksum verification.
