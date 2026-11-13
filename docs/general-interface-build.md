# General interface development build

13 November 2026, `0.1.0-alpha.3-dev.2`.

Built in the isolated `feature/general-ai-interface` checkout based on
`3af62ba`. Changes remain uncommitted for review.

## Delivered interface

The typed workflows from dev.1 are retained: operation discovery, native
navigation, PLC content snapshots, offline hardware construction/configuration,
CAx export and shared compile diagnostics.

The general bridge now adds public type/member discovery, properties,
indexers, fields, collections, declared services, methods and generic methods,
constructors, native dynamic creation/actions/attributes, events, synchronous
declarative callback policies and explicit resource disposal. The same routes
apply across installed public Siemens domains. `$portal` supports operations
before a project is selected. See [the interface guide](public-api-interface.md).

Read and ReadWrite remain structurally separated. Generic native writes have
expected-context guards and explicit acceptance of unclassified native effects,
including possible PLC changes. Configured file-root grants apply to typed
native file inputs. The bridge does not claim to sandbox native string paths.

## Build evidence

The reviewed source compiled with zero warnings and zero errors:

| Component | Read | ReadWrite |
| --- | --- | --- |
| V17 worker | Compiled | Compiled |
| V18 worker | Compiled | Compiled |
| V19 worker | Compiled | Compiled |
| Fixed-profile broker | Compiled | Compiled |

The V19 ReadWrite test project also compiled with zero warnings and errors.
Compilation did not execute its tests. No tests, validators, MCP probes,
TIA attachment or PLC operations were run. The user's build-only instruction
was followed throughout the implementation and review.

Build output is `artifacts/full-interface-reviewed`. The matrix used
`build/Build-Workers.ps1`, the workspace-local .NET SDK and the installed
Portal V19 `PublicAPI/V17`, `V18` and `V19` references. Early compiler errors
and warnings were corrected before this final build.

`build/Assemble-Bundles.ps1` stages separate Read and ReadWrite ZIPs, their
interface guide, release manifest and `SHA256SUMS.txt` under
`artifacts/full-interface-packages`. The manifest marks every version
`runtimeValidated=false` and `experimental-build-only`. Siemens runtime
assemblies are loaded from the installed product and are not redistributed.

The XML inventory generator was rerun without loading or invoking Siemens
assemblies. V17/V18/V19 have 19,973/22,658/23,468 documented records under
`artifacts/public-api-inventory`. Member routes describe dispatch paths;
they are not individual runtime results or an exhaustive reflection audit.

## Review and limitations

An independent Terra reviewer checked the completed bridge and connected code.
Reported reference, callback-lifecycle, partial-effect and collection-comparison
issues were corrected and re-reviewed. The final targeted pass reported no
remaining must-fix finding. See [the review record](general-interface-review.md).

Runtime availability still depends on installed modules, licences and native
object state. Optional missing assemblies and unsupported value shapes are
reported explicitly. Generic open type targets, private APIs, arbitrary .NET
execution and UI-only editor functionality are outside the interface. V20 and
V21 are not delivered because matching references/adapters are unavailable.
External TIA UI project changes are observed at the next context refresh;
unsubscribe synchronous event policies before changing project selection in
TIA. These boundaries are documented in the guide.

The installed MCP registration, original repository at
`D:\code\tiaportal-mcp`, engineering projects and PLCs were not changed.
