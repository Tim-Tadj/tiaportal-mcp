# Issue #1 changes in 0.1.0-alpha.2

This experimental prerelease addresses the inspection, diagnostics and source
workflow problems in [issue #1](https://github.com/Tim-Tadj/tiaportal-mcp/issues/1).
Both profiles contain exact V17, V18 and V19 workers. V20 and V21 remain excluded.

| Reported problem | Change |
| --- | --- |
| Session state lost between calls | Retain one worker Portal service, refresh open projects, compare project identities rather than transient Siemens proxies, and expose workerProcessId in GetState. No implicit launch or switch to another TIA process. |
| Lists show only metadata | One complete text block contains metadata, LF, then rows. Metadata-only structuredContent is omitted. |
| Compile errors have no diagnostics | CompileSoftware returns state, error/warning counts and native hierarchical diagnostics. Follow GetCompileDiagnostics cursors without recompiling. |
| XML-only import | ImportBlockFromSource generates blocks/types from .scl, .awl or .db files through Siemens external sources. |
| References missing | FindReferences queries the native service on an exact selected block or tag. Search accepts block name/path, DB number, or tag name/address. |
| Software tree is too large | GetSoftwareTree defaults to 50 lines, capped at 200, and includes tag tables. Cursors are bound to the tree snapshot. |
| Device details lack hardware identity | Standard GetDeviceInfo includes up to 200 module type identifiers, order numbers, firmware and classifications where exposed. modulesTruncated reports further items. |
| PLC tags/constants absent | GetPlcTagTables and GetPlcTags provide paged tables, tags and user constants using the shared formatter. |
| GetProject serialisation fails | The compatibility alias returns scalar project DTOs; GetProjectInfo also converts FileInfo paths to strings. |
| Inconsistent export is generic | Single export explains that compilation is required. Batch exports return per-block reasons with counts and truncation flags. Discovery paths now round-trip through block/type lookup. |

## Compatibility

The capability contract is `v2-single-content-paged-diagnostics`. To consume a
formatted list, split `content[0].text` at its first LF: parse the first line
as metadata JSON, then decode the remaining text according to metadata.format.
This also applies to explicit responseFormat=Json. The alpha.1 two-block preview
and metadata-only structuredContent are replaced. CSV/TOON encoding and paging
metadata fields retain their existing meanings.

Software trees are paged by default. Compile errors are successful tool calls
with state=Error and diagnostics, so check state and errorCount. Operational
failures still return MCP errors. Compiler paths and descriptions are native;
network or line fields unavailable through this API are not invented.

Source generation is an offline project mutation. It can replace existing
blocks and requires acceptBlockReplacement=true. A generation failure does not
guarantee restoration of replaced blocks. Use a project copy, inspect the
result and compile before explicitly saving. Nothing is downloaded by this tool.

Native cross-references require V18/V19 and applicable STEP 7 objects. V17
returns unsupported. Accessible-device discovery requires V19; ChangeType
requires V18. These limits follow the exact installed PublicAPI. Native reference
data does not prove absence of indirect accesses or references outside its scope.

Compile snapshots expire on another compile, disconnect or a change of selected
project. Other paged inventories are queried again on each request; restart
paging if their project contents change.

## Validation

Validation passed 38 focused worker tests (including the disposable TIA fixture),
47 Siemens-free contract tests and two version tests: 87 in total. All six workers
and both brokers build with zero warnings and errors.

Release validation covers the six exact-version worker builds, both brokers,
profile separation, signed local Siemens assembly resolution, client manifests,
archive contents, third-party notices and SHA-256 checksums. No Siemens runtime
DLL is redistributed. Offline MCP checks exercise both extracted ZIP profiles.

Focused unit coverage includes first-content-block row delivery in all formats,
empty TOON columns, cursor isolation, compile error paging, source validation,
canonical/legacy group paths and the existing hardware/online/value checks.
The Siemens-free contract suite and worker-version checks run separately.

The authorised V19 integration fixture uses a marked disposable project copy in
its own hidden TIA instance. It checks repeated queries, project serialisation,
module metadata, tags, native references, source generation, successful/error
compile diagnostics, inconsistent export reasons and project switching. It never
downloads to a PLC or operates outputs. V17 and V18 are build-only; this is not
production certification or a clean-account client installation test.

To run that fixture, explicitly supply TIA_MCP_DISPOSABLE_PROJECT,
TIA_MCP_TEST_SOFTWARE_PATH and TIA_MCP_TEST_REFERENCE, and place a
`.tia-mcp-disposable-test` marker in the copied project folder. Run only
TestProjectSourceIntegration with the matching worker version. It writes test
blocks and uses the suite's temporary output root. Do not point it at a working
project.
