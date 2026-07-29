# Change Log

## [Unreleased]

No changes yet.

## [0.1.0-alpha.1] - 2026-07-30

### Added

- [RO][RW] Define the experimental `0.1.0-alpha.1` Windows x64 release gate,
  support matrix and explicitly deferred production work.
- [Broker][RO][RW] Add experimental automatic TIA Portal installation
  discovery and exact worker identity validation.
- [Broker][RO][RW] Add a dependency-free .NET Framework broker which launches
  a bundled exact-version worker and proxies MCP standard streams.
- [Contract][RO][RW] Add `GetCapabilities`, compact detail levels and opaque
  paging contracts.
- [Contract][RO][RW] Add canonical paths and paged summary responses to
  project, device, block and type discovery.
- [Contract][RO][RW] Add `responseFormat` with `Auto`, `Toon`, `Csv` and
  `Json` choices, plus selected-format and paging metadata.
- [Claude][VS Code][ChatGPT][RO][RW] Add relocatable local client helpers,
  profile-specific MCPB manifests and an OpenAI Secure MCP Tunnel connection
  kit.
- [Build][Tests] Add a Siemens-free alpha validator and Windows CI workflow
  for profile, packaging, broker-boundary and operation-gate checks.

### Changed

- [Contract][RO][RW] Replace assembly-wide MCP discovery with explicit tool and
  prompt type registration.
- [Build][RO][RW] Pin stable `ModelContextProtocol` 1.4.1 and
  `Microsoft.Extensions.Hosting` 10.0.10 packages.
- [RO][RW] Serialise MCP tool calls through a worker operation gate before they
  reach TIA Portal services.
- [Build][RO][RW] Resolve framework assemblies through a build-only reference
  assemblies package instead of a fixed `C:\Program Files` path.
- [Tests] Resolve project, session and output paths at runtime from the clone,
  temporary storage or explicit environment variables instead of fixed drive
  locations.
- [ChatGPT][RO][RW] Use the local OpenAI tunnel client to launch the selected
  stdio broker through `--mcp-command`. The broker and worker remain on the
  user's PC, with no hosted project component or public inbound endpoint.
- [Contract][RO][RW] Make summary detail the default for `GetDevices`,
  `GetBlocks`, `GetTypes` and `ListProjects`. Use `Full` explicitly for the
  legacy rich fields.
- [Contract][RO][RW] Make point-inspection and bulk-operation results compact
  by default, cap returned batch summaries and bind cursors to their query.
- [Contract][RO][RW] Omit null message, metadata and optional rich-detail
  fields from compact JSON responses.
- [Contract][RO][RW] Treat the compact list defaults as an intentional
  breaking pre-1.0 contract change. Consumers needing the v0.0.18 rich
  unpaged contract must remain on that release while migrating.
- [Contract][RO][RW] Mark whole-tree discovery tools as legacy and direct LLM
  clients towards bounded list operations.
- [Contract][RO][RW] Make `Auto` render eligible `Summary` tables as RFC 4180
  CSV, eligible `Standard` tables with bounded `tia-toon-table/1`, and `Full`,
  nested, compatibility or ineligible results as compact JSON.
- [Contract][RO][RW] Reject incompatible explicit `Csv` and `Toon` requests
  with `InvalidParams` guidance instead of flattening fields or silently
  returning another format. Outer MCP JSON-RPC messages remain JSON.
- [Contract][RO][RW] Preserve CSV scalar types through lexical quoting and
  report stable columns in metadata for canonical empty TOON arrays.
- [Contract][RO][RW] Treat automatic CSV and TOON result rendering as an
  intentional breaking pre-1.0 contract change. Integrations which require a
  JSON tool payload must set `responseFormat=Json`.

### Fixed

- [Build][RO][RW] Make clean SDK builds independent of a separately installed
  .NET Framework 4.8 developer targeting pack and pass SDK-compatible
  per-project intermediate-directory paths with a trailing separator. Select
  one `dotnet` executable deterministically when PATH contains several SDKs.
- [Broker][RO][RW] Flush proxied standard streams per chunk so interactive MCP
  requests reach the worker and responses return before the client closes the
  session.
- [Build][RO][RW] Initialise failed TIA version parses on every code path so
  exact-version workers compile cleanly.
- [Build][V17] Avoid unavailable block and type namespace members so both
  profiles compile against the exact V17 PublicAPI references.
- [Contract][RO][RW] Report missing device, software, block and type paths as
  invalid parameters, and preserve block/type traversal failures instead of
  returning misleading empty results.
- [Contract][RO][RW] Report missing project state as a correctable request
  precondition instead of an internal server error.
- [RO][RW] Clear failed or disposed Portal connections before reconnecting and
  allow read-profile workers to detach cleanly during shutdown.
- [RW] Match already-open projects and local sessions by canonical path rather
  than selecting a same-named project from another directory.
- [RW][V20] Prepare source-only rollback of unchanged SIMATIC SD targets when
  committing an export pair fails part-way through, and preserve recovery
  files when a concurrent change prevents safe rollback. The V20 worker is
  deferred from `0.1.0-alpha.1`, so this is not an available alpha capability.

### Security

- [RO] Exclude save, compile, import, export, close and disconnect tools and
  prompts from read-profile compilation.
- [RO] Add a second profile policy which rejects direct mutation calls even if
  a read worker reaches a Portal command unexpectedly.
- [RW] Contain exports beneath a configured output root and default overwrite
  behaviour to false.
- [RW] Stage XML and SIMATIC SD document exports before replacing an existing
  output, so a failed TIA export does not delete the previous file first.
- [RO][RW] Stop automatically adding the current user to the Siemens TIA
  Openness Windows group.
- [RO][RW] Bound regular-expression length and evaluation time.
- [RO][RW] Resolve exact entity paths without treating point lookups as regular
  expressions, and reject filter timeouts as invalid requests.
- [RO][RW] Normalise Openness attribute values to JSON-safe bounded values.
- [RW] Reject project files from a different TIA major version and avoid
  implicit project upgrade when opening an exact-version file.

### Packaging

- [Claude][RO][RW] Add source templates for the two MCPB packages.
- [RO][RW] Add the two-profile release manifest and packaging metadata
  renderer.
- [RO][RW][V17][V18][V19][V20] Add experimental exact-worker build
  orchestration. The first alpha bundles V17 to V19; V20 moves to a later
  alpha and V21 is unsupported in this release pending its modular adapter.
- [Claude][VS Code][RO][RW] Replace version-specific development samples with
  fixed-profile broker configurations.
- [RO][RW] Add complete redistributable third-party licence payloads and
  public bundles without debug symbols or Siemens-supplied runtime and
  object-code DLLs.

### Compatibility

- [RO][RW][V17][V18] Include experimental build-only workers in the
  `0.1.0-alpha.1` release without claiming runtime validation or support.
- [RO][RW][V19] Retain prior licensed live-project evidence for both profiles,
  while classifying the published Siemens-DLL-free resolver bits as
  `experimental-prior-runtime-evidence` with `runtimeValidated=false`.
- [RO][RW][V20] Defer the exact-version worker to a later alpha. The published
  v0.0.18 release remains the legacy V20 option.
- [RO][RW][V21] Exclude V21 from `0.1.0-alpha.1`; the broker must return a
  clear unsupported-version diagnostic.
- [ChatGPT] Deliver a Secure MCP Tunnel configuration kit for a ChatGPT custom
  app, not a direct ChatGPT Desktop stdio installer.

### Documentation

- [RO][RW] Add architecture, roadmap, status, definition-of-done and changelog
  policy documentation.
- [Contract][RO][RW] Add the normative model-facing output-format policy,
  including lossless fallback rules and the bounded TOON v4.1 profile.
- Consolidate the Gemini-specific project notes into `AGENTS.md` and remove the
  redundant `gemini.md`.

### Tests

- [Contract][RO][RW] Add a Siemens-free response-format conformance and size
  regression suite.
- [RO][RW] Add Siemens-free behavioural checks for concurrent entry, failure
  release and cancelled operation-gate waiters.
- [V19][RO] Add a reusable MCP smoke harness for capabilities, connection and
  negotiated CSV, TOON and JSON list responses.

## [0.0.16] - 2025-09-02

- New: ImportFromDocuments and ImportBlocksFromDocuments (V20+)
- Guard: Version checks for export/import as documents (V20+)
- UX: Pre-check .s7res for missing en-US tags; warnings surfaced in responses
- Docs: README updates, prompts note V20+ and known LAD en-US limitation
- Refactor: Updated all McpException throws to SDK signature with McpErrorCode
- Chore: Added TODOs for tests/docs

## [0.0.15] - 2025-08-30

- prompts improved
- long running tasks as async tasks

## [0.0.14] - 2025-08-18

- better structure/tree format
- new GetSoftwareTree()
- bugfixes

## [0.0.13] - 2025-08-14

- logging integrated
- prompts added

## [0.0.12] - 2025-08-07

- export path fixed

## [0.0.11] - 2025-08-07

- project structure formatted as markdown code

## [0.0.10] - 2025-08-07

- tool responses improved

## [0.0.9] - 2025-08-04

- export of blocks and types with 'preservePath' option
- new tools
- some infos with attributes

## [0.0.8] - 2025-08-01

- improved jsonrpc responses
- updated dependencies

## [0.0.7] - 2025-07-18

- new GetState()
- return values fixed

## [0.0.6] - 2025-07-16

- refactored code to use new TIA Portal API
- only blocks (OB/FB/FC/DB) and types (UDT) are now retrieved from the PLC software
- use regex to filter blocks and types
- import of blocks and types to PLC software

## [0.0.5] - 2025-07-11

- locating of plc software by softwarePath. This makes it possible to access plc software in groups/subgroups
- new tool: retrieving of project structure as text
- new tool: compile plc software

## [0.0.4] - 2025-06-30

- opens local session or projects, depending on project file extension

## [0.0.3] - 2025-06-23

- Release on Visual Studio Code Marketplace

