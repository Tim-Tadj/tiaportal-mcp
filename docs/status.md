# Current Status

Status date: 28 July 2026.

## Repository Baseline

The current implementation is v0.0.18 at commit `c2429b1`. The fork and
upstream `main` branches are aligned at this commit.

The repository currently has:

- one .NET Framework 4.8 MCP server executable;
- one automatically discovered tool surface containing 30 tools;
- 17 automatically discovered prompts;
- stdio transport;
- project, session, device, PLC software, block and type inspection;
- compile, XML import and export, and V20 SIMATIC SD document operations;
- an initial typed Portal exception model for selected export paths.

The two public access profiles and exact-version workers described in
[Architecture](architecture.md) are not implemented end to end. The current
architecture branch now contains:

- a dependency-free .NET Framework broker with strict version and profile
  selection;
- compile-time Read and ReadWrite workers, with mutation tools omitted from the
  Read build and a second direct-call policy gate;
- exact Siemens package selection and experimental V17 to V20 build
  orchestration;
- compact project discovery and paged device, block and type discovery;
- bounded attribute and regular-expression handling;
- output-root, reparse-point, staged export and default no-overwrite policies;
- two MCPB manifest templates and bundle assembly inputs.

These changes remain experimental. The V19 Read and ReadWrite workers and their
fixed-profile brokers have now been built and exercised against a licensed,
running TIA Portal V19 project. Both profiles completed MCP initialisation,
reported the expected capability boundary, connected to the open project and
paged all 18 devices. A bounded Full-detail read also serialised 182 attributes
without the previous internal error. V17, V18 and V20 still require their
licensed runtime matrix, the V21 adapter does not exist, and the client adapters
are not releasable.

## Current Capability Gaps

| Area | Current state | Target state |
| --- | --- | --- |
| Public profiles | Read and ReadWrite compile-time profiles and a second mutation policy exist, but explicit dependency-injection registries and releasable bundles do not. | Two public bundles with structurally separate read-only and read-write registries. |
| TIA versions | Exact-package V17 to V20 build inputs exist. V19 Read and ReadWrite workers and brokers pass a licensed live-project smoke test; V17, V18 and V20 remain untested. | One internal worker compiled and tested against each advertised major version. |
| V21 | No dedicated adapter. | Dedicated modular V21 adapter and worker. |
| Tool registration | Assembly-wide discovery remains, with mutating tools removed at compilation for Read. | Explicit generated manifests per profile, version and module. |
| Responses | Core project, device, block and type queries are compact. Device, block and type queries are paged; project discovery and legacy trees still need bounded paging, and several command results still need structured warnings. | Compact summaries, bounded detail, canonical paths and paging. |
| LLM guidance | Core descriptions now include selection, state, safety and paging guidance, and capability and inspection workflows exist. A shared metadata registry and evaluation fixtures remain open. | Shared metadata registry and a small set of workflow prompts. |
| File safety | Export paths are contained beneath a locked root, child reparse points are rejected, overwrite defaults to false and existing files are replaced only after a staged export succeeds. Document-pair failures roll back unchanged targets and preserve recovery files when safe rollback is impossible. Import roots, handle-level race protection, atomic two-file replacement and complete batch failure reasons remain open. | Allowed output root, path containment and explicit overwrite policy. |
| Portal lifecycle | The worker attaches when exactly one process exists and refuses ambiguous multiple-process attachment. Explicit process choice and ownership tracking remain missing. | Deterministic selection, ownership tracking and safe attach semantics. |
| Concurrency | No dedicated serial Openness scheduler. | One scheduler per worker connection context. |
| Packaging | Broker, worker build scripts, bundle assembly, two MCPB templates and direct Claude/VS Code configurations exist. Both V19 fixed-profile broker paths pass live standard-stream proxy tests. Signing, SBOM, a VSIX and the local-only ChatGPT tunnel kit do not. | Signed bundles, local-only installer adapters, release manifest, SBOM and checksums. |
| ChatGPT | No adapter exists. The accepted design keeps the adapter, broker and worker on the user's PC and uses an outbound Secure MCP Tunnel without a public inbound endpoint. | A cleanly installable local connection kit with loopback-only transport, lifecycle controls and documented data boundaries. |
| Validation | Static source and packaging checks are possible. A portable .NET 8 SDK built both V19 profiles and brokers, and live read-only smoke calls passed through each broker. The existing integration suite still requires prepared project/session assets and includes mutating tests, so it was not run against the user's open project. | Siemens-free contract and policy tests plus exact-version runtime smoke tests. |

The v0.0.18 baseline is compiled against V20 only. This branch selects an exact
package for each V17 to V20 worker build, but those workers must not be
advertised until the clean build and licensed runtime matrix passes. Upstream
issue #24 records the V18 `ReflectionTypeLoadException` caused by the old
single-binary approach, and issue #25 records the separate V21 API problem.

## Immediate Priorities

1. Build V17, V18 and V20 Read and ReadWrite workers in clean environments,
   repeat V19 in the release matrix, and fix exact-package API differences.
2. Replace assembly-wide discovery with explicit profile manifests and a
   serial Openness operation scheduler.
3. Add Siemens-free broker, policy, cursor, serialisation and schema tests.
4. Return typed capped batch failures instead of retaining reasons only in
   logs.
5. Implement the V21 modular adapter.
6. Add tags and external sources as the first expanded read surfaces.
7. Build the VS Code adapter and local-only ChatGPT Secure MCP Tunnel connection
   kit, then add signing, checksums, provenance and an SBOM.

Detailed sequencing and upstream dispositions are recorded in
[Roadmap](roadmap.md).

## Definition of Done

The two-profile release is complete only when all of the following conditions
are met.

### Architecture and Versioning

- The broker has no Siemens API reference and selects a worker deterministically.
- Every advertised TIA Portal version has an exact-package worker.
- V21 uses its dedicated adapter.
- A mismatched or unavailable version fails before Portal operations begin and
  provides a clear recovery action.
- The release manifest accurately maps profile and TIA version to worker.

### Read-Only Boundary

- The read-only worker registers only its explicit query allow-list.
- No command service is available through dependency injection.
- A second policy gate rejects mutation if a command is reached unexpectedly.
- Contract tests prove that save, compile, import, export, project lifecycle
  mutation, delete, online, download, upload, force and safety mutation are
  absent.
- The worker never closes user-owned Portal or project state.

### Read-Write Safety

- Every mutating tool declares side effects and profile requirements.
- File operations are contained within an allowed root and default to no
  overwrite.
- Conflict behaviour is explicit and covered by tests.
- High-risk modules are disabled by default and have an additional approval
  boundary.
- Mutations produce useful audit logs without exposing secrets.

### MCP Contract

- Stdio output contains protocol traffic only.
- List and search operations are deterministically paged and bounded.
- Summaries include canonical paths or stable entity references.
- Attribute values are JSON-safe and bounded.
- Errors use stable codes, retain diagnostic context in logs and do not
  disconnect a healthy session after a recoverable read failure.
- Legacy contract behaviour and its deprecation period are documented.

### Tool Guidance

- A single metadata registry generates tool descriptions, annotations,
  capabilities and reference documentation.
- Profile and version availability are accurate.
- Workflow evaluations cover discovery, ambiguity, pagination, version
  mismatch, read-only refusal and mutation intent.
- No advertised tool is an implementation stub which always fails.

### Validation

- Siemens-free unit tests cover broker selection, profile allow-lists, policy,
  serialisation, paging, filters and schema snapshots.
- Each worker builds in a clean release environment.
- Each advertised worker passes smoke tests on its actual TIA Portal major
  version with documented licence prerequisites.
- Large synthetic projects meet response-size and latency budgets.
- Stateful operations are serialised and concurrent-client tests do not race
  the Portal object graph.

### Packaging and Documentation

- Both public bundles install from a clean Windows account without a source
  checkout.
- The local-only ChatGPT connection kit, Claude Desktop MCPB and VS Code adapter
  use the same signed profile bundles.
- The ChatGPT adapter, broker and worker execute on the user's PC, and no
  project component requires hosted infrastructure or a public inbound port.
- Executables and packages are signed and checksums verify.
- An SBOM, third-party notices, prerequisites and support matrix are published.
- The changelog follows [Changelog Policy](changelog-policy.md).
- Installation, doctor, troubleshooting and uninstall instructions are
  complete.

## Status Labels

Roadmap and issue tracking should use these meanings consistently:

- **Planned**: accepted design, implementation has not started.
- **In progress**: implementation exists on a working branch but is not
  releasable.
- **Experimental**: available for evaluation but excluded from the supported
  capability manifest.
- **Supported**: meets the definition of done for the named profile and TIA
  version.
- **Deprecated**: supported temporarily with a documented removal release.
- **Unsupported**: intentionally unavailable or removed.
