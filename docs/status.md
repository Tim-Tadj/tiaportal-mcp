# Current Status

Status date: 29 July 2026.

## Repository Baseline

The currently published release is v0.0.18. The current branch targets the
experimental Windows x64 prerelease `0.1.0-alpha.1`, with separate Read and
ReadWrite packages and internal V17 to V20 workers.

The v0.0.18 baseline has:

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
- negotiated model-facing output with `Auto`, `Toon`, `Csv` and `Json`
  response formats;
- bounded attribute and regular-expression handling;
- output-root, reparse-point, staged export and default no-overwrite policies;
- two MCPB manifest templates and bundle assembly inputs.

These changes remain experimental. The V19 Read and ReadWrite workers and their
fixed-profile brokers have now been built and exercised against a licensed,
running TIA Portal V19 project. Both profiles completed MCP initialisation,
reported the expected capability boundary, connected to the open project and
paged all 18 devices. A bounded Full-detail read also serialised 182 attributes
without the previous internal error. V17 and V18 now compile in both profiles
and remain runtime-unverified. V20 is still blocked in the current release
environment because its exact PublicAPI references are absent, so the alpha
artefacts cannot yet be described as complete or published. The target
manifest classifies V17, V18 and V20 as build-only and runtime-unverified, and
excludes V21. These classifications are alpha evaluation labels, not
production support claims.

The exact alpha publication checklist and deferred work are in
[0.1.0-alpha.1 Release Gate](alpha-release.md). No further TIA-dependent test
is required for that prerelease if the existing V19 evidence is frozen and
every other bundled version remains clearly marked runtime-unverified.

## Current Capability Gaps

| Area | Current state | Target state |
| --- | --- | --- |
| Public profiles | Read and ReadWrite compile-time profiles, explicit MCP host registrations and a second mutation policy exist. Both profiles are included in the alpha scope. A generated capability registry remains a supported-release improvement. | Two supported public bundles with structurally separate read-only and read-write registries. |
| TIA versions | Exact-package V17 to V20 build inputs exist. V17, V18 and V19 compile in both profiles. V19 workers and brokers pass a licensed live-project smoke test. V17 and V18 are runtime-unverified, while V20 awaits its exact PublicAPI references before alpha publication. | One internal worker compiled and tested against each supported major version. |
| V21 | Excluded from `0.1.0-alpha.1`; no dedicated adapter exists. | Dedicated modular V21 adapter and worker in a later release. |
| Tool registration | The host explicitly registers `McpServer`, `McpListTools` and `McpPrompts`; mutating tools are also removed at compilation for Read. | Generated manifests per profile, version and module from the shared metadata registry. |
| Responses | Core project, device, block and type queries are compact. Device, block and type queries are paged. `Auto` selects CSV for eligible `Summary` tables, bounded TOON v4.1 for eligible `Standard` tables and compact JSON for `Full`, nested and compatibility results. Project discovery and legacy trees still need bounded paging, and several command results still need structured warnings. | Compact summaries, bounded detail, canonical paths, paging and lossless negotiated output under the normative format policy. |
| LLM guidance | Core descriptions now include selection, state, safety and paging guidance, and capability and inspection workflows exist. A shared metadata registry and evaluation fixtures remain open. | Shared metadata registry and a small set of workflow prompts. |
| File safety | Export paths are contained beneath a locked root, child reparse points are rejected, overwrite defaults to false and existing files are replaced only after a staged export succeeds. Document-pair failures roll back unchanged targets and preserve recovery files when safe rollback is impossible. Import roots, handle-level race protection, atomic two-file replacement and complete batch failure reasons remain open. | Allowed output root, path containment and explicit overwrite policy. |
| Portal lifecycle | The worker attaches when exactly one process exists and refuses ambiguous multiple-process attachment. Explicit process choice and ownership tracking remain missing. | Deterministic selection, ownership tracking and safe attach semantics. |
| Concurrency | A process-wide operation gate serialises MCP tool calls before they reach the Portal object graph. Siemens-free tests cover concurrent entry, operation failure and cancelled waiters; full concurrent-client stress remains deferred. | One proven scheduler per worker connection context. |
| Packaging | Broker and worker build scripts, bundle assembly, two MCPB templates, direct Claude configurations, VS Code helpers and ChatGPT tunnel helpers exist. Both V19 fixed-profile broker paths pass live standard-stream proxy tests. Final alpha artefact validation, the V20 compile and the Siemens resolver redistribution decision remain; signing, an SBOM and a VSIX are deferred. | Signed bundles, locally executed client adapters, release manifest, SBOM and checksums for a supported release. |
| ChatGPT | Profile-specific configure and start helpers use OpenAI Secure MCP Tunnel to launch the local stdio broker through `--mcp-command`; no HTTP adapter is required. An authenticated end-to-end tunnel check remains. | A cleanly installable local connection kit with lifecycle controls and documented data boundaries. |
| Validation | The Siemens-free output contract and operation-gate suite passes 43 tests. The alpha validator passes source, profile, version, V21, client-manifest and notice checks, and validates both dependency-free broker profiles without starting a worker. A Windows workflow defines the same Siemens-free checks but has not yet been observed on GitHub. A repository-local .NET SDK built both profiles for V17, V18 and V19 plus both brokers. A live Read broker registered 16 tools and returned an open project plus a bounded device page in negotiated CSV, TOON and JSON. The existing integration suite still requires prepared project/session assets and includes mutating tests, so it was not run against the user's open project. | Siemens-free contract and policy tests plus exact-version runtime smoke tests. |

The v0.0.18 baseline is compiled against V20 only. This branch selects an exact
package for each V17 to V20 worker build. The alpha may expose unverified
workers only with the explicit experimental classifications in
[Alpha Release Gate](alpha-release.md); they must not be advertised as
supported until the licensed runtime matrix passes. Upstream issue #24 records
the V18 `ReflectionTypeLoadException` caused by the old single-binary approach,
and issue #25 records the separate V21 API problem.

## Immediate Priorities

1. Complete the non-TIA `0.1.0-alpha.1` publication gate: clean builds,
   profile schema checks, package validation, client configuration checks,
   checksums and release notes.
2. Prove the explicit Read tool boundary and operation gate through Siemens-free
   `tools/list` and concurrent request checks.
3. Assemble and validate both ZIPs, both MCPBs, the VS Code helpers and the
   ChatGPT `--mcp-command` tunnel profiles.
4. After the alpha, generate profile manifests from the shared metadata
   registry.
5. Expand Siemens-free broker, policy, cursor and schema coverage, including
   semantic-equivalence decoder fixtures for every negotiated format.
6. Evaluate TOON and compact JSON on representative traces. Treat improved
   model structural accuracy as a project design goal rather than a universal
   assumption.
7. Return typed capped batch failures instead of retaining reasons only in
   logs.
8. Implement the V21 modular adapter and run the deferred licensed runtime
   matrix.
9. Add tags and external sources as the first expanded read surfaces.
10. Add signing, provenance and an SBOM for a supported release.

Detailed sequencing and upstream dispositions are recorded in
[Roadmap](roadmap.md).

## Supported-Release Definition of Done

The two-profile release is supported only when all of the following conditions
are met. The deliberately narrower alpha gate is defined in
[0.1.0-alpha.1 Release Gate](alpha-release.md).

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
- `responseFormat` accepts only `Auto`, `Toon`, `Csv` and `Json`, with `Auto`
  as the default.
- Eligible `Summary` and `Standard` tables use CSV and `tia-toon-table/1`
  respectively under `Auto`; `Full`, nested, compatibility and ineligible
  results use compact JSON without data loss.
- Explicit incompatible `Csv` and `Toon` requests return `InvalidParams`
  guidance rather than flattening data or silently changing format.
- Format metadata reports the selected format, `returned`, `hasMore` and
  `nextCursor`, and full row data occurs once in model-facing text.
- Errors use stable codes, retain diagnostic context in logs and do not
  disconnect a healthy session after a recoverable read failure.
- Legacy contract behaviour and its deprecation period are documented.

### Tool Guidance

- A single metadata registry generates tool descriptions, annotations,
  capabilities and reference documentation.
- Profile and version availability are accurate.
- Workflow evaluations cover discovery, ambiguity, pagination, version
  mismatch, read-only refusal, mutation intent and output-format recovery.
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
- The ChatGPT tunnel connection kit, Claude Desktop MCPB and VS Code adapter
  use the same signed profile bundles.
- The ChatGPT tunnel client, broker and worker execute on the user's PC, and no
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
