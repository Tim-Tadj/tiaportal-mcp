# Changelog Policy

## Purpose

The root `CHANGELOG.md` records user-visible product changes. It is not a commit
log and does not list internal refactors unless they affect behaviour,
compatibility, security, installation or support.

Both public profiles share one application version and one changelog. Internal
exact-version workers also share that application version. Worker revisions are
identified through release metadata rather than separate public product
versions.

## Release Format

Each release uses this structure:

```markdown
## [1.2.0] - 2026-10-15

### Added

- [RO][RW][V20] Add paged PLC tag-table discovery.

### Fixed

- [Broker][V18] Select the V18 worker when V18 and V20 are installed.
```

Keep an `Unreleased` section at the top. Move its entries into a dated release
section only when the artefacts are published. Dates use ISO `YYYY-MM-DD`.

## Categories

Use only the categories which contain entries.

- **Added**: new tools, workflows, supported versions, profiles or installer
  capabilities.
- **Changed**: backward-compatible behaviour or default changes which users
  should understand.
- **Deprecated**: supported behaviour scheduled for removal, including the
  removal release or earliest removal window.
- **Removed**: removed tools, parameters, formats, versions or installers.
- **Fixed**: corrected behaviour, reliability or diagnostics.
- **Security**: access-boundary, path, credential, dependency or operational
  safety changes.
- **Compatibility**: TIA Portal, MCP protocol, client, Windows or .NET support
  changes which are not adequately described by another category.
- **Packaging**: broker, worker selection, signatures, installers, release
  manifests or deployment changes.
- **Documentation**: substantial user-facing documentation corrections. Minor
  spelling and formatting changes may be omitted.

If a change fits more than one category, place it under the category with the
greatest user impact and add appropriate scope tags.

## Scope Tags

Start each entry with the smallest useful set of tags:

- profile: `[RO]`, `[RW]` or `[RO][RW]`;
- component: `[Broker]`, `[Contract]`, `[ChatGPT]`, `[Claude]`, `[VS Code]`;
- TIA version: `[V17]`, `[V18]`, `[V19]`, `[V20]`, `[V21]` or `[All TIA]`;
- module when helpful: `[PLC]`, `[Hardware]`, `[HMI]`, `[Library]`,
  `[Online]`, `[Safety]`.

Do not add a tag which is already clear from the release heading or wording.

## Versioning

Use Semantic Versioning for the public application version:

`MAJOR.MINOR.PATCH`

### Major

Increment `MAJOR` for an incompatible public change, including:

- removing or renaming a tool without a compatibility alias;
- changing required parameters or the meaning of an existing result;
- removing a supported TIA Portal version or client installer;
- changing the read-only or read-write security promise;
- removing the legacy contract after its deprecation period;
- requiring a materially different installation or configuration model.

### Minor

Increment `MINOR` for backward-compatible capability changes, including:

- adding a tool, workflow, module or optional parameter;
- adding support for another TIA Portal version;
- adding a new installer adapter;
- adding a new result field which existing clients may ignore;
- introducing an opt-in contract or transport.

### Patch

Increment `PATCH` for backward-compatible corrections, including:

- bug, reliability, performance or diagnostic fixes;
- security hardening which does not remove supported behaviour;
- packaging fixes which preserve installation inputs;
- documentation corrections;
- worker fixes limited to a particular TIA Portal version.

Before 1.0, incompatible changes still require an explicit migration note.
Prefer a minor increment for a planned breaking 0.x release and do not hide a
contract break in a patch release.

## Compatibility Rules

A change is breaking if a conforming existing client, configuration or
automation can no longer perform the same supported workflow without
modification.

Examples include:

- changing `GetProject` from a list result to a singular result;
- making a previously optional parameter required;
- returning a partial page without a cursor where the old result was complete;
- moving output files without a migration path;
- changing a tool from read-only to mutating;
- dropping a worker from the supported release manifest.

Adding safer behaviour may still be breaking. For example, replacing arbitrary
export paths with an allowed root needs deprecation guidance, an explicit
migration and an appropriate version increment.

The supported profile and TIA version matrix is published in the release
manifest and [Current Status](status.md). Changelog entries describe changes to
that matrix rather than repeating it in full.

## Entry Quality

Each entry should:

- describe the user-visible outcome, not the implementation commit;
- identify affected profiles and versions;
- mention migration or recovery when action is required;
- link the relevant issue or pull request where useful;
- distinguish an experimental capability from a supported one;
- avoid claiming support before the definition of done is met.

Good:

```markdown
- [RW][V20] Reject export paths outside the configured output root. Existing
  absolute-path configurations must set `outputRoot` and use a relative path.
```

Avoid:

```markdown
- Refactored helper classes.
```

## Release Procedure

1. Review `Unreleased` entries against merged user-visible changes.
2. Verify scope tags and compatibility impact.
3. Confirm that every newly supported worker and profile meets the definition
   of done.
4. Choose the Semantic Versioning increment.
5. Add the release date and migration notes.
6. Publish the signed artefacts and release manifest.
7. Add or update comparison links if the changelog uses them.
8. Start a new empty `Unreleased` section.

Documentation-only planning changes do not alter the supported capability
matrix and may remain under `Documentation` until the next release.
