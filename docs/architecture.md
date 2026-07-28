# Architecture

## Goals

The target design has four primary goals:

- provide a compact, predictable MCP surface for LLM clients;
- enforce a genuine read-only boundary;
- support each advertised TIA Portal version through an exact-version worker;
- offer straightforward installation for ChatGPT through a local-only
  connection kit, Claude Desktop and VS Code without asking users to choose an
  internal binary.

The initial target worker set is TIA Portal V17, V18, V19, V20 and V21 on
Windows x64. A version is advertised only after its worker has passed the
release definition of done. Earlier versions remain best-effort or
community-supported until suitable build and runtime environments exist.

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
  Compact DTOs, filters, paging, errors and capability metadata

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

Assembly-wide tool discovery is not used for the public profiles. Each profile
has an explicit tool manifest generated from a single metadata registry.

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

TIA Portal Openness is stateful. A worker owns one connection context and
serialises Openness operations through a dedicated scheduler. Concurrent MCP
requests may wait independently, but they must not call the same Portal object
graph concurrently from arbitrary thread-pool threads.

Attach and ownership are explicit:

- attaching to an existing process does not give the worker permission to close
  it;
- a process or project started by the read-write worker is tracked as
  worker-owned;
- process selection is deterministic when more than one compatible instance is
  running;
- a failed read operation does not clear a healthy connection.

## Compact MCP Contract

List operations return summaries by default:

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
- compact result shape and recommended next action;
- profile and module availability.

Tool schemas, server instructions, capability output, documentation and schema
tests are generated from this registry. Prompts are reserved for genuine
multi-step workflows instead of duplicating individual tools.

Global server guidance instructs an LLM to:

1. call capabilities and state first;
2. discover entities through bounded list or search operations;
3. reuse returned IDs or canonical paths;
4. request only the pages and detail needed;
5. avoid mutation without clear user intent;
6. report version or profile limitations rather than trying an unavailable
   tool.

## Installation Adapters

The same two public profiles are exposed through client-specific installation
adapters:

- ChatGPT: a loopback-only Streamable HTTP adapter and Secure MCP Tunnel
  connection kit which runs the chosen profile bundle on the user's PC;
- Claude Desktop: an MCPB package with a bundled Windows binary;
- VS Code: one extension which detects installed TIA versions, asks for the
  access profile, and launches the matching bundle.

The adapters use the same capability manifests and worker artefacts. They do
not maintain separate implementations.

Stdio remains the local transport baseline. ChatGPT does not currently install
a local stdio MCP server directly, so its adapter is a connection kit rather
than an MCPB-style installer. The broker, worker, tunnel client and any
Streamable HTTP adapter run on the user's PC. The adapter binds to loopback
only, and no project component is hosted remotely or exposed through a public
inbound port.

OpenAI Secure MCP Tunnel provides the outbound connection to ChatGPT. The
adapter must use a compliant transport with protocol version negotiation,
session handling, request limits and origin controls. A bespoke pipe bridge is
not part of the core architecture. The full boundary and release checks are in
[ChatGPT Local Connection](chatgpt-local.md).

Client packaging decisions are based on the current
[MCPB manifest specification](https://github.com/modelcontextprotocol/mcpb/blob/main/MANIFEST.md)
and [OpenAI guidance for custom MCP apps](https://help.openai.com/en/articles/12584461).
Those client capabilities are release inputs and should be rechecked before
publishing because they can change independently of this repository.

## Release Integrity

Every public package includes:

- a release manifest mapping supported TIA versions to internal workers;
- checksums and signatures;
- an SBOM and third-party notices;
- exact Windows, .NET Framework, TIA Portal and licensing prerequisites;
- a capability manifest for the selected profile;
- a doctor command which performs read-only preflight checks.

Siemens assemblies are located from an installed TIA Portal environment and are
not redistributed without an explicit licence review.
