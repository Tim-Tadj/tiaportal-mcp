# 0.1.0-alpha.3 release

13 November 2026. Experimental Windows x64 prerelease with build-only evidence.
This release is unsigned and is not production-qualified.

## What changed

The MCP now combines short typed tools for common workflows with a general
schema-guided interface over the installed public Siemens Openness contracts:

- Discover types, exact member schemas, overloads, fields, properties and services.
- Browse collections and native compositions using bounded session references.
- Invoke public methods and constructors, including generic methods and ref/out values.
- Create native engineering objects, update dynamic attributes and invoke native actions.
- Subscribe to events, poll bounded observations, provide synchronous declarative
  callback decisions, and release disposable native resources.
- Capture paged PLC block/type content and configure offline hardware through typed tools.
- Export local-project device configuration as native AML with its native log.
- Read shared paged hardware/software compile diagnostics with a compile ID.

The generic routes apply across public PLC, hardware, HMI, library,
technology, drive, OPC, safety and collaboration domains when the installed
module, licence, object and session expose the relevant native contracts.
They do not create functionality which Siemens does not expose.

See [the interface guide](public-api-interface.md),
[coverage boundaries](openness-coverage.md) and
[independent review](general-interface-review.md).

## Profiles and effect boundary

Choose Read for inspection and public schema discovery. ReadWrite adds project
changes and the generic native bridge. All generic mutation-capable calls
require an exact expected project context and explicit acceptance of native
effects. Those effects are unclassified and can include file writes, downloads,
CPU commands or security changes. Native exceptions may follow partial effects;
inspect state before retrying. ReadWrite is no longer limited to the earlier
typed offline workflows.

Typed workflow exports remain beneath the output root. Repeat
`--api-file-root <path>` for additional typed native file/directory input roots.
Those grants permit native reads and writes. Native string arguments and
embedded paths are not an operating-system sandbox.

## Compatibility

Existing typed tools remain available in their original profile. The output
contract remains `v2-single-content-paged-diagnostics`.

`CompileHardware` now uses the same paged diagnostic response as software
compilation. Consumers must inspect `State`, `ErrorCount`, `WarningCount`,
`Messages` and page metadata, and follow `GetCompileDiagnostics` using the
returned `CompileId`. The old hardware-specific result shape is replaced.
Compilation does not save or download the project.

API references, member IDs and collection cursors are session/version scoped.
Do not persist or invent them. Unsubscribe synchronous event policies before
changing project selection directly in the TIA UI: external UI changes are
observed on the next MCP context refresh. Missing modules and unsupported value
shapes are reported explicitly. UI-only functions, private APIs and arbitrary
.NET execution remain excluded.

## Package matrix

| TIA version | Read | ReadWrite | Evidence |
| --- | --- | --- | --- |
| V17 | Bundled | Bundled | Build only, runtime-unverified |
| V18 | Bundled | Bundled | Build only, runtime-unverified |
| V19 | Bundled | Bundled | Build only, runtime-unverified |
| V20 | Excluded | Excluded | Deferred |
| V21 | Excluded | Excluded | Dedicated adapter required |

Both profile ZIPs and Claude Desktop MCPBs contain a fixed-profile broker,
exact V17/V18/V19 workers, the interface guide and portable client assets.
The release includes `release-manifest.json` and `SHA256SUMS.txt`. Siemens
runtime DLLs are not distributed; workers resolve the matching local installation.

Requires Windows x64, .NET Framework 4.8, a matching local Siemens Openness
installation, appropriate licences and Siemens TIA Openness group membership.

## Evidence and publication exceptions

Six workers and both brokers are rebuilt for the release version. The V19
ReadWrite test project is compiled only. The implementation received an
independent source review and corrections were re-reviewed. See the
[development build record](general-interface-build.md) for the reviewed source.

At the user's explicit instruction, no tests, standalone release validators,
MCP probes, TIA attachment or PLC calls are run for this release. Automatic
push/PR checks are skipped using GitHub's standard CI-skip marker. Packaging
performs its normal metadata, archive-content and dependency invariants.
Every bundled worker remains `runtimeValidated=false` and
`experimental-build-only`. Earlier release test results are not evidence for
these new binaries. Signing, SBOM/provenance, licensed runtime and clean-client
qualification remain outstanding. The earlier alpha.2 gate is historical;
this release deliberately records the narrower authorised build-only scope.
