# Architecture

Status date: 29 July 2026.

## Goals

The target design has four primary goals:

- provide a compact, predictable MCP surface for LLM clients;
- enforce a genuine read-only boundary;
- support each advertised TIA Portal version through an exact-version worker;
- offer straightforward installation for ChatGPT through a locally executed
  connection kit, Claude Desktop and VS Code without asking users to choose an
  internal binary.

The supported-release target worker set is TIA Portal V17, V18, V19, V20 and
V21 on Windows x64. The narrower `0.1.0-alpha.1` package includes V17 to V20:
V19 is runtime-validated, V17, V18 and V20 are build-only and
runtime-unverified, and V21 is excluded. An experimental worker may be
advertised for alpha evaluation with that label, but support requires the full
release definition of done.

## Two Public Packages

There are two public server packages:

1. `tia-portal-mcp-read`, the read-only package
2. `tia-portal-mcp-readwrite`, the read-write package

Client-specific installers may wrap these bundles, but they must not introduce
additional capability profiles. A user chooses the security boundary once,
during installation. The user does not choose or manage an internal
version-specific executable.

The read-only package contains only query capabilities. It does not register or
ship callable project mutation services. File exports are excluded because they
write to the local file system.

The read-write package contains the query capabilities and controlled mutation
capabilities. It applies explicit path, overwrite, conflict and approval
policies to every operation with side effects.

## Version Broker and Workers

Each public package contains a version broker and a set of internal workers:

```text
MCP client
  -> selected public profile
  -> version broker
  -> exact-version worker
  -> installed TIA Portal instance
```

The broker has no compile-time dependency on `Siemens.Engineering`. It is
responsible for:

- detecting installed TIA Portal versions;
- accepting an optional explicit version or process selection;
- validating that the requested version is supported by the package;
- launching the matching worker for the selected access profile;
- forwarding MCP traffic without altering request or response semantics;
- reporting clear preflight errors when TIA Portal, .NET Framework, licensing
  or Windows group membership is unsuitable.

Each worker is compiled against the exact Siemens Openness package for one TIA
Portal major version. The worker must reject a mismatched runtime rather than
attempt to redirect a V20 assembly reference to a V18 assembly.

V17 through V20 may share a legacy adapter source set where their API contracts
permit it, but they remain separate build outputs. V21 uses a dedicated adapter
because its modular Openness API is not treated as a binary-compatible upgrade
from V20.

Workers are implementation details. They share the public application version
and are selected by the broker. They are not separate products or separate
user-facing downloads.

## Component Boundaries

The intended solution boundaries are:

```text
TiaMcp.Contracts
  Compact DTOs, output formats, filters, paging, errors and capability metadata

TiaMcp.Application
  Query services, command services, policy checks and response mapping

TiaMcp.Openness.Legacy
  Shared V17 to V20 adapter source, built once for each exact version

TiaMcp.Openness.V21
  V21-specific modular API adapter

TiaMcp.Tools.ReadOnly
  Explicit query tool registry

TiaMcp.Tools.ReadWrite
  Explicit command tool registry plus the query registry

TiaMcp.Broker
  Installation detection, process selection and worker lifecycle

TiaMcp.Worker
  Profile and version-specific MCP host
```

Assembly-wide tool discovery is not used for the public profiles. The alpha
host explicitly registers its tool and prompt types, with mutation tools
removed from the Read compilation. The supported-release target generates an
explicit profile manifest from a single metadata registry.

## Access Profiles

### Read Only

The read-only profile may:

- attach to a user-selected TIA Portal process without taking ownership;
- report server, Portal, project and session state;
- inspect project, device, software, block, type, tag, hardware, network,
  library and HMI data where supported;
- search and page through project entities;
- return content through MCP tools or resources without writing files.

The read-only profile must not:

- create, open with upgrade, close, save or save-as a project;
- compile, import, export, download, upload, force or modify objects;
- add the user to a Windows group;
- register a command service or a mutating tool;
- close a TIA Portal instance or project that it did not create.

The boundary is enforced twice: the command assemblies are not registered, and
the application policy rejects mutation if a command is reached unexpectedly.

### Read Write

The read-write profile adds deliberate mutation workflows. Each mutating tool
must declare its side effects, preconditions, version support and conflict
behaviour. Destructive or operationally dangerous areas such as delete,
download, force, online control and safety changes remain separate modules and
are disabled by default until they have their own approval and validation
model.

File-writing operations use a configured allowed root. They accept a filename
or a path relative to that root, normalise and validate the final path, default
to no overwrite, and return the final path in the result.

## Connection and Concurrency

TIA Portal Openness is stateful. The alpha worker serialises MCP tool calls
through a process-wide operation gate before they reach the Portal object
graph. The supported-release target owns one scheduler per connection context.
Concurrent MCP requests may wait independently, but they must not call the same
Portal object graph concurrently from arbitrary thread-pool threads.

Attach and ownership are explicit:

- attaching to an existing process does not give the worker permission to close
  it;
- a process or project started by the read-write worker is tracked as
  worker-owned;
- process selection is deterministic when more than one compatible instance is
  running;
- a failed read operation does not clear a healthy connection.

## Compact MCP Contract and Output Formats

List operations return summaries by default. The underlying contract remains a
typed, bounded response even when its model-facing text is rendered as CSV or
TOON:

```json
{
  "items": [
    {
      "id": "opaque-entity-id",
      "path": "Program blocks/Main",
      "name": "Main",
      "kind": "OB",
      "language": "LAD",
      "consistent": true
    }
  ],
  "page": {
    "returned": 1,
    "hasMore": false,
    "nextCursor": null
  }
}
```

The contract follows these rules:

- `detail=summary|standard|full`, with `summary` as the list default;
- `responseFormat=Auto|Toon|Csv|Json`, with `Auto` as the default;
- deterministic ordering and opaque cursors;
- a default page size of 50 and a bounded maximum;
- canonical paths and stable entity references in every summary;
- attributes are normalised to JSON-safe primitives and requested separately
  or through full detail;
- whole-project trees are replaced by bounded child browsing;
- timestamps, repeated success flags, redundant descriptions and default null
  fields are omitted;
- bulk commands return counts and capped warnings or failures by default;
- compilation returns severity counts, with diagnostics exposed as a paged
  result.

`Auto` uses CSV for eligible compact `Summary` tables, the bounded
`tia-toon-table/1` TOON v4.1 profile for eligible richer `Standard` tables, and
compact JSON for `Full`, nested, heterogeneous and compatibility results. CSV
and TOON are used only for homogeneous scalar rows with explicit stable
columns. The TOON profile is limited to 200 rows and 32 columns.

Automatic selection falls back to compact JSON whenever the preferred table
format cannot preserve the actual result. Explicit `Csv` or `Toon` requests
for an ineligible result return MCP `InvalidParams` with recovery guidance.
They never discard, flatten or stringify fields to force a table shape.
Explicit `Json` is the universal lossless override.

Formatted list metadata reports the selected format, `returned`, `hasMore` and
`nextCursor`. Full row data appears once in model-facing text content. Outer
MCP JSON-RPC messages, tool schemas, client configuration, manifests and build
metadata remain JSON regardless of `responseFormat`.

The complete normative rules, including RFC 4180 handling and the bounded TOON
eligibility profile, are in [Model-Facing Output Format
Policy](output-formats.md). TOON's intended structural-accuracy benefit is a
design goal for project evaluations, not a claim of universal superiority over
JSON.

The compact contract is an intentional breaking pre-1.0 change from the
v0.0.18 rich, unpaged responses. The released v0.0.18 binary remains the
migration fallback, but this branch does not register side-by-side legacy tool
aliases. Truncation is always explicit and a partial page never appears to be a
complete result.

## Tool Instructions

One metadata registry defines:

- purpose and selection guidance;
- when the tool should not be used;
- required state and minimum TIA Portal version;
- read-only, mutating, destructive and idempotent annotations;
- parameter grammar, path examples, defaults and limits;
- compact result shape, output-format eligibility and recommended next action;
- profile and module availability.

Tool schemas, server instructions, capability output, documentation and schema
tests are generated from this registry. Prompts are reserved for genuine
multi-step workflows instead of duplicating individual tools.

Global server guidance instructs an LLM to:

1. call capabilities and state first;
2. discover entities through bounded list or search operations;
3. reuse returned IDs or canonical paths;
4. request only the pages and detail needed;
5. retain `responseFormat=Auto` unless full, nested or integration-sensitive
   data requires `Json`;
6. follow `nextCursor` only when the next page is needed;
7. avoid mutation without clear user intent;
8. report version or profile limitations rather than trying an unavailable
   tool.

## Installation Adapters

The same two public profiles are exposed through client-specific installation
adapters:

- ChatGPT: an OpenAI Secure MCP Tunnel connection kit whose local
  `tunnel-client` launches the chosen stdio broker through `--mcp-command`;
- Claude Desktop: an MCPB package with a bundled Windows binary;
- VS Code: direct stdio configuration and installation helpers for the selected
  bundle. A later extension may automate selection and upgrades.

The adapters use the same capability manifests and worker artefacts. They do
not maintain separate implementations.

Stdio remains the local transport baseline. ChatGPT does not install the local
stdio MCP server directly, so its delivery remains a connection kit rather
than an MCPB-style installer. The local tunnel client can launch the broker
directly; `0.1.0-alpha.1` does not require a Streamable HTTP adapter or local
listener. The broker, worker and tunnel client run on the user's PC, and no
project component is hosted remotely or exposed through a public inbound port.

OpenAI Secure MCP Tunnel provides the outbound connection to ChatGPT. The
tunnel client forwards MCP JSON-RPC between the local stdio command and OpenAI
over outbound HTTPS. A bespoke HTTP or pipe bridge is not part of the core
architecture. The full boundary and release checks are in [ChatGPT Local
Connection](chatgpt-local.md).

Client packaging decisions are based on the current
[MCPB manifest specification](https://github.com/modelcontextprotocol/mcpb/blob/main/MANIFEST.md)
and [OpenAI guidance for custom MCP apps](https://help.openai.com/en/articles/12584461).
Those client capabilities are release inputs and should be rechecked before
publishing because they can change independently of this repository.

## Supported-Release Integrity

Every public package includes:

- a release manifest mapping supported TIA versions to internal workers;
- checksums and signatures;
- an SBOM and third-party notices;
- exact Windows, .NET Framework, TIA Portal and licensing prerequisites;
- a capability manifest for the selected profile;
- a doctor command which performs read-only preflight checks.

Siemens assemblies are located from an installed TIA Portal environment and are
not redistributed without an explicit licence review.

The experimental `0.1.0-alpha.1` gate is intentionally narrower. It permits
unsigned artefacts and defers the formal SBOM, provenance and complete licensed
runtime matrix, while still requiring checksums, dependency notices, an
explicit Siemens redistribution decision and accurate experimental version
labels. See [Alpha Release Gate](alpha-release.md).
