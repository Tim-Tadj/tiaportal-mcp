# Change Log

## [Unreleased]

### Added

- [Broker][RO][RW] Add experimental automatic TIA Portal installation
  discovery and exact worker identity validation.
- [Broker][RO][RW] Add a dependency-free .NET Framework broker which launches
  a bundled exact-version worker and proxies MCP standard streams.
- [Contract][RO][RW] Add `GetCapabilities`, compact detail levels and opaque
  paging contracts.
- [Contract][RO][RW] Add canonical paths and paged summary responses to
  project, device, block and type discovery.

### Changed

- [ChatGPT][RO][RW] Require the ChatGPT adapter, broker and worker to run on the
  user's PC behind an outbound Secure MCP Tunnel, with no hosted project
  component or public inbound endpoint.
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

### Fixed

- [Contract][RO][RW] Report missing device, software, block and type paths as
  invalid parameters, and preserve block/type traversal failures instead of
  returning misleading empty results.
- [Contract][RO][RW] Report missing project state as a correctable request
  precondition instead of an internal server error.
- [RO][RW] Clear failed or disposed Portal connections before reconnecting and
  allow read-profile workers to detach cleanly during shutdown.
- [RW] Match already-open projects and local sessions by canonical path rather
  than selecting a same-named project from another directory.
- [RW][V20] Roll back unchanged SIMATIC SD targets when committing an export
  pair fails part-way through, and preserve recovery files when a concurrent
  change prevents safe rollback.

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
- [RO][RW][V17][V18][V19][V20] Add experimental worker-build and bundle
  assembly orchestration. V21 remains blocked on its modular adapter.
- [Claude][VS Code][RO][RW] Replace version-specific development samples with
  fixed-profile broker configurations.

### Documentation

- [RO][RW] Add architecture, roadmap, status, definition-of-done and changelog
  policy documentation.
- Consolidate the Gemini-specific project notes into `AGENTS.md` and remove the
  redundant `gemini.md`.

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

- Release on Visual Studio Code Narketplace

