# Current Status

Status date: 30 July 2026.

## Repository Baseline

The latest published release is the experimental Windows x64 prerelease
[`0.1.0-alpha.1`](https://github.com/Tim-Tadj/tiaportal-mcp/releases/tag/0.1.0-alpha.1),
with separate Read and ReadWrite packages and internal V17, V18 and V19
workers. v0.0.18 remains the stable legacy V20 release.

The v0.0.18 baseline has:

- one .NET Framework 4.8 MCP server executable;
- one automatically discovered tool surface containing 30 tools;
- 17 automatically discovered prompts;
- stdio transport;
- project, session, device, PLC software, block and type inspection;
- compile, XML import and export, and V20 SIMATIC SD document operations;
- an initial typed Portal exception model for selected export paths.

The v0.0.18 baseline does not implement the two public access profiles and
exact-version workers described in [Architecture](architecture.md). The
`0.1.0-alpha.1` release contains an end-to-end experimental alpha path with:

- a dependency-free .NET Framework broker with strict version and profile
  selection;
- compile-time Read and ReadWrite workers, with mutation tools omitted from the
  Read build and a second direct-call policy gate;
- exact Siemens package selection and experimental V17 to V20 build inputs,
  with V17 to V19 selected for the first alpha;
- compact project discovery and paged device, block and type discovery;
- negotiated model-facing output with `Auto`, `Toon`, `Csv` and `Json`
  response formats;
- bounded attribute and regular-expression handling;
- output-root, reparse-point, staged export and default no-overwrite policies;
- two MCPB manifest templates and bundle assembly inputs.

These changes remain experimental. Before the current resolver change, earlier
V19 Read and ReadWrite workers and their fixed-profile brokers were exercised
against a licensed, running TIA Portal V19 project. Both profiles completed MCP
initialisation, reported the expected capability boundary, connected to the
open project and paged all 18 devices. A bounded Full-detail read also
serialised 182 attributes without the previous internal error.

The resolver then changed to remove every Siemens-supplied DLL from the public
release. The exact published bits have not received another live-project run.
They have build, strong-name assembly preflight and Siemens-free validation
only.
The prior V19 evidence still informs confidence, but it is not validation of
the release bits. The published manifest therefore sets `runtimeValidated=false`
for every bundled worker, classifies V17 and V18 as
`experimental-build-only`, and classifies V19 as
`experimental-prior-runtime-evidence`. V20 is planned for a later alpha and
V21 is unsupported in this release. These are alpha evaluation labels, not
production support claims.

The exact alpha publication checklist and deferred work are in
[0.1.0-alpha.1 Release Gate](alpha-release.md). No further TIA-dependent test
was required for publication because the earlier V19 evidence is labelled as
prior evidence and all exact published workers remain runtime-unverified.

## Current Capability Gaps

| Area | Current state | Target state |
| --- | --- | --- |
| Public profiles | Read and ReadWrite compile-time profiles, explicit MCP host registrations and a second mutation policy exist. Both profiles are included in the alpha scope. A generated capability registry remains a supported-release improvement. | Two supported public bundles with structurally separate read-only and read-write registries. |
| TIA versions | Exact-package V17 to V20 build inputs exist. The alpha bundles V17, V18 and V19, all of which compile in both profiles. V17 and V18 are `experimental-build-only`. V19 has prior live-project evidence from before the resolver change, but the exact published bits are `experimental-prior-runtime-evidence` with `runtimeValidated=false`. V20 is planned for a later alpha and v0.0.18 remains its legacy release. | One internal worker compiled and tested against each supported major version. |
| V21 | Unsupported in `0.1.0-alpha.1`; no dedicated adapter exists. | Dedicated modular V21 adapter and worker in a later release. |
| Tool registration | The host explicitly registers `McpServer`, `McpListTools` and `McpPrompts`; mutating tools are also removed at compilation for Read. | Generated manifests per profile, version and module from the shared metadata registry. |
| Responses | Core project, device, block and type queries are compact. Device, block and type queries are paged. `Auto` selects CSV for eligible `Summary` tables, bounded TOON v4.1 for eligible `Standard` tables and compact JSON for `Full`, nested and compatibility results. Project discovery and legacy trees still need bounded paging, and several command results still need structured warnings. | Compact summaries, bounded detail, canonical paths, paging and lossless negotiated output under the normative format policy. |
| LLM guidance | Core descriptions now include selection, state, safety and paging guidance, and capability and inspection workflows exist. A shared metadata registry and evaluation fixtures remain open. | Shared metadata registry and a small set of workflow prompts. |
| File safety | Export paths are contained beneath a locked root, child reparse points are rejected, overwrite defaults to false and existing files are replaced only after a staged export succeeds. Document-pair failures roll back unchanged targets and preserve recovery files when safe rollback is impossible. Import roots, handle-level race protection, atomic two-file replacement and complete batch failure reasons remain open. | Allowed output root, path containment and explicit overwrite policy. |
| Portal lifecycle | The worker attaches when exactly one process exists and refuses ambiguous multiple-process attachment. Explicit process choice and ownership tracking remain missing. | Deterministic selection, ownership tracking and safe attach semantics. |
| Concurrency | A process-wide operation gate serialises MCP tool calls before they reach the Portal object graph. Siemens-free tests cover concurrent entry, operation failure and cancelled waiters; full concurrent-client stress remains deferred. | One proven scheduler per worker connection context. |
| Packaging | Broker and worker build scripts, bundle assembly, two MCPB templates, direct Claude configurations, VS Code helpers and ChatGPT tunnel helpers exist. Both profile ZIPs and MCPBs were published, and their manifests, checksums and archive contents validate. The alpha packages V17 to V19 and excludes every Siemens-supplied runtime or object-code DLL. Relocatable VS Code and ChatGPT print checks pass. Live clean-account client execution, signing, an SBOM and a VSIX are deferred. | Signed bundles, locally executed client adapters, release manifest, SBOM and checksums for a supported release. |
| ChatGPT | Profile-specific configure and start helpers use OpenAI Secure MCP Tunnel to launch the local stdio broker through `--mcp-command`; no HTTP adapter is required. An authenticated end-to-end tunnel check remains. | A cleanly installable local connection kit with lifecycle controls and documented data boundaries. |
| Validation | The Siemens-free output-contract, operation-gate and Siemens assembly-identity suite passes 47 of 47 tests. The alpha validator passes source, profile, version, strong-name assembly preflight, preflight rejection for V20 and V21, client-manifest and notice checks. It validates both dependency-free broker profiles without starting a worker. The exact V19 Read bundle also passes offline MCP initialisation, signed-assembly preflight, `tools/list` and `GetCapabilities` without connecting to or starting TIA Portal. GitHub Actions run [30429923731](https://github.com/Tim-Tadj/tiaportal-mcp/actions/runs/30429923731) completed successfully for the tagged commit. A repository-local .NET SDK built six workers and two brokers with zero warnings. Both profile ZIPs and both profile MCPBs, their checksums and archive contents validate, and the VS Code and ChatGPT helpers pass actual relocatable print checks. Before the resolver change, an earlier V19 Read broker registered 16 tools and returned an open project plus a bounded device page in negotiated CSV, TOON and JSON. The exact published bits have not received a live TIA run. The existing integration suite still requires prepared project/session assets and includes mutating tests, so it was not run against the user's open project. | Siemens-free contract and policy tests plus exact-version runtime smoke tests. |

The v0.0.18 baseline is compiled against V20 only. The alpha selects an exact
package for each worker build, but `0.1.0-alpha.1` bundles only V17, V18 and
V19. The alpha may expose unverified workers only with the explicit
experimental classifications in
[Alpha Release Gate](alpha-release.md); they must not be advertised as
supported until the licensed runtime matrix passes. Upstream issue #24 records
the V18 `ReflectionTypeLoadException` caused by the old single-binary approach,
and issue #25 records the separate V21 API problem.

## Immediate Priorities

The non-TIA release gate is complete. The
[GitHub prerelease](https://github.com/Tim-Tadj/tiaportal-mcp/releases/tag/0.1.0-alpha.1)
was published on 30 July 2026 with all six validated artefacts and the exact
support matrix in [Alpha Release Gate](alpha-release.md). Live clean-account
client execution and further TIA-dependent tests remain explicitly deferred
from this experimental prerelease.

After the alpha:

1. Generate profile manifests from the shared metadata
   registry.
2. Expand Siemens-free broker, policy, cursor and schema coverage, including
   semantic-equivalence decoder fixtures for every negotiated format.
3. Evaluate TOON and compact JSON on representative traces. Treat improved
   model structural accuracy as a project design goal rather than a universal
   assumption.
4. Return typed capped batch failures instead of retaining reasons only in
   logs.
5. Add the V20 exact-version worker in a later alpha after its matching
   PublicAPI references are available.
6. Implement the V21 modular adapter and run the deferred licensed runtime
   matrix.
7. Add tags and external sources as the first expanded read surfaces.
8. Add signing, provenance and an SBOM for a supported release.

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
