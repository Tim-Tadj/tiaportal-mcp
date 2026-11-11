# Current Status

Status date: 11 November 2026.

## Issue #1 release update

The latest experimental prerelease is
[0.1.0-alpha.2](https://github.com/Tim-Tadj/tiaportal-mcp/releases/tag/0.1.0-alpha.2),
published on 11 November 2026. Changes, compatibility notes and validation are
in [Issue #1 changes](issue-1.md). The implementation adds five tools and keeps
both public profiles. V17/V18 are build-only; V19 focused checks do not certify
all operations or client installation paths. Historical alpha.1 evidence
remains in its changelog and GitHub release.

## Release scope

Both profiles bundle exact V17, V18 and V19 workers. V17/V18 have compile-time
coverage only. V20 is deferred; V21 needs its own modular adapter. The legacy
v0.0.18 V20 release is separate.

Read exposes 24 inspection tools. ReadWrite exposes 43 tools, adding controlled
lifecycle, source/XML import, export, compilation and offline hardware changes.
The broker and worker remain local, and Siemens runtime DLLs are not bundled.

The output contract is `v2-single-content-paged-diagnostics`. Formatted lists
now put metadata and rows in one text block, and software trees are paged.
Clients parsing alpha.1's two content blocks must migrate. See
[Output formats](output-formats.md) and [Issue #1 changes](issue-1.md).

## Validation and remaining work

The release remains experimental. `runtimeValidated=false` is retained in the
manifest because the full licensed runtime and clean-account client matrix is
outstanding. Focused V19 testing uses an explicitly disposable project copy;
it does not download to a PLC or operate outputs. Alpha.1 historical build and
runtime evidence remains recorded in the changelog and GitHub release.

V17 does not expose native cross-references or ChangeType. Accessible-device
discovery requires V19. Older workers return explicit unsupported responses.
Native cross-references are scoped to a selected block or tag and are not a
proof that indirect accesses are absent.

Further work includes V20 and V21 adapters, cross-client evaluation,
large-project performance, signed releases, provenance/SBOM, generated
capability manifests and the remaining path hardening recorded in the roadmap.

## Supported-Release Definition of Done

The two-profile release is supported only when all of the following conditions
are met. The deliberately narrower alpha gate is defined in
[0.1.0-alpha.2 Release Gate](alpha-release.md).

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
