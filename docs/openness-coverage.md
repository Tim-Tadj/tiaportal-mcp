# Openness coverage

Status: 13 November 2026, development version `0.1.0-alpha.3-dev.2`.

The interface combines short typed tools for common work with one schema-guided
bridge across the trusted public Siemens API. Domains do not need individual
handwritten wrappers before an AI can discover and operate their public objects.
There is no C# evaluation or private API access.

## Public surface routes

| Public surface | Discovery | Execution |
| --- | --- | --- |
| Types, including HMI and optional referenced modules | `ListApiTypes` | `$type:<fullName>` selects static members and constructors |
| Properties and indexers | `DescribeApiObject` | `ReadApiProperty`, `WriteApiProperty` |
| Fields and enum constants | `DescribeApiObject` | `ReadApiField`, `WriteApiField` |
| Methods, overloads and generic methods | `DescribeApiObject` | `InvokeApiMethod` with exact member ID and ordered arguments |
| Public constructors and DTOs | `DescribeApiObject` on a type anchor | `CreateApiObject`, or schema-guided inline values |
| Declared engineering services | `DescribeApiObject` | `GetApiService` |
| Collections | Public properties or native composition browsing | `BrowseApiCollection`, `BrowseEngineeringObject` |
| Native dynamic attributes | `DescribeEngineeringSurface`, `ReadEngineeringSurfaceAttributes` | `UpdateEngineeringAttributes` |
| Native composition creation | `DescribeEngineeringCreation` | `CreateEngineeringObject` |
| Native actions on objects, compositions and associations | `DescribeEngineeringActions` | `InvokeEngineeringAction` |
| Events and synchronous native callbacks | `DescribeApiObject`, callback parameter schema | `SubscribeApiEvent`, `ReadApiEvents`, `UnsubscribeApiEvent`, declarative callback arguments |
| Disposable native resources | Returned API reference | `ReleaseApiObject`; Portal connection tools own Portal lifetime |

The bridge applies the same routes to projects, devices, hardware, PLC blocks,
types, tags and constants, external sources, classic HMI, Unified HMI,
libraries, technology objects, drive surfaces, OPC, safety and collaboration
objects when their public contracts exist in the installed API and are
reachable through a project, service, constructor or static member. It adds
no Siemens functionality which the installed product, licence, object or
session does not expose. UI-only editor operations remain outside Openness.

`Read` contains public schema queries, property/field reads, collections,
self-description and declared service acquisition. Arbitrary public methods,
constructors, setters, creation, actions, subscriptions and resource disposal
are structurally restricted to `ReadWrite`. All generic native writes require
an exact expected project context and explicit acceptance of unclassified
native effects. These effects can include file writes, downloads, online
operations and PLC changes. They are not labelled safe offline operations.

See [the interface guide](public-api-interface.md) for value encodings, native
reference lifetimes, callbacks, file grants and recovery.

## Typed workflows retained

Common workflows keep their simpler domain tools: project selection and state;
devices and software trees; block/type content capture; tag/constant reads;
cross-references; native online state; bounded PLC value reads; hardware
catalogue lookup, construction, I/O addresses, node/network configuration and
PROFINET assignment; hardware/software compile reports; and CAx exports.

Cross-references and hardware replacement require V18 or newer. Catalogue
search, catalogue-verified device creation and accessible-device discovery
require V19. External-source listing gives identity metadata. Siemens V19
`PlcExternalSource` does not provide a native body-export reader. The block and
type content tools capture bounded snapshots through native exports in
ReadWrite. No source content is invented where Siemens has no reader.

## Version and evidence boundary

V17, V18 and V19 have exact-version workers in both profiles. The current
change is build-only: no tests, validators, MCP probes, TIA attachment or PLC
operation was authorised or performed. Compilation and static review cannot
establish runtime availability of every public member. `GetCapabilities`,
`GetOperations` and generated member schemas are the runtime discovery entry
points, and all target availability remains unchecked until a real call.

V20 is deferred and V21 requires its own adapter. Their matching Siemens
assemblies are unavailable in this environment, so they are not delivered or
claimed by this build. Optional modules not referenced by the installed API
or missing dependencies appear as assembly issues in `ListApiTypes` rather
than being treated as covered. Private, internal and arbitrary .NET surfaces
are intentionally excluded. Generic type definitions cannot be used as open
static or constructor targets; use concrete native instances. Read indexer
inputs are limited to scalars and enums to avoid executing constructors during
queries. Bounded conversion rejects unsupported shapes explicitly.

## Documented member inventory

`tools/export_public_api_inventory.py` parses the installed V17/V18/V19
Siemens XML documentation and hashes matching assemblies without loading or
invoking them. Generated identifiers are kept under
`artifacts/public-api-inventory`, rather than adding approximately 25 MiB to
the repository. The supplied versions contain 19,973, 22,658 and 23,468
records respectively. Siemens prose is omitted by default.

Every record has a route disposition, mapped dispatch tool, unsupported reason
and `runtimeSupport=notAssertedByXmlInventory`. This records the available
interface route, not a successful call or exhaustive runtime qualification.
XML documentation can omit public members. The live reflection schema is the
member selection authority. Field/event routes now point at the implemented
bridge; property setters point at `WriteApiProperty`, constructors at
`CreateApiObject`, native actions at `InvokeEngineeringAction`, and disposables
at the controlled release tool.

```powershell
$apiRoot = 'C:\Program Files\Siemens\Automation\Portal V19\PublicAPI'
foreach ($tiaVersion in 17, 18, 19) {
    $apiDirectory = Join-Path $apiRoot "V$tiaVersion"
    python .\tools\export_public_api_inventory.py `
        --documentation-dir $apiDirectory --reference-dir $apiDirectory `
        --tia-version $tiaVersion `
        --output ".\artifacts\public-api-inventory\V$tiaVersion.json"
}
```

Parser and codec regression tests were authored but remain unrun. The build
record and independent review are in [build evidence](general-interface-build.md)
and [review findings](general-interface-review.md).
