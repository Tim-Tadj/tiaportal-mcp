# General public API interface

Use the typed tools for common tasks and the public API bridge for the rest.
The bridge follows the installed Siemens contracts across domains instead of
maintaining thousands of separate MCP tools. It executes no arbitrary C#.

## Discover, select, act

1. Call `GetCapabilities` and `GetOperations`. Attach with `Connect` to an exact
   running Portal process, then select the project with the existing project tools.
2. `DescribeApiObject` with empty `entityRef` describes the selected project.
   `$portal` selects the attached Portal, including projects and global libraries
   before a project is selected. `$type:<fullName>` selects static members and
   constructors. Find type names with paged `ListApiTypes` filters.
3. Select an exact returned member ID. `ReadApiProperty` and `ReadApiField`
   return values or `{"$ref":"api:...","type":"..."}` references. Browse a
   returned collection with `BrowseApiCollection` and describe its child references.
   Service rows have `kind=service` and an exact `typeName` for `GetApiService`.
4. A method is executable only when its schema reports `callable=true`.
   Pass its ID, ordered JSON arguments, the exact owning `expectedProjectPath`
   and `acceptNativeEffects=true`. The worker checks project ownership for
   the target and any native object arguments.
5. Inspect the operation's result. Keep saving and compilation explicit.

Member IDs include the declaring contract and signature. Use IDs from the
selected worker because versions can differ. Issued references expire after
eviction or a session or project change. A changed parent binding also
invalidates references issued with a replay accessor.

## Values

| Native input | JSON form |
| --- | --- |
| Primitive, string, nullable | Corresponding JSON scalar or `null` |
| Enum or flags | Exact enum names, for example `"One, Two"` |
| Existing object | `{"$ref":"api:..."}` using an assignable issued reference |
| `object` with an exact numeric or native type | `{"$type":"System.Int32","value":7}` or an allowed Siemens type |
| Arrays and supported list interfaces | JSON arrays; rectangular nested arrays for multidimensional inputs |
| Dictionary | Array of `{"key":...,"value":...}` entries |
| DTO construction | Object keyed by constructor parameter names and writable property names from the returned schema |
| File or directory | A path string under `--output-root` or an explicit `--api-file-root` |
| Date/time, duration, GUID | Invariant string representations shown by the schema |
| CultureInfo and Version | Culture name or version string |
| SecureString | String input; output is redacted |

Values are bounded to 1,024 collection elements, 32,768 UTF-16 string units and
16 nested levels. API collections page at up to 200 rows, with an explicit
5,000-item navigation cap. References are limited to 1,000 per session and
20 parent levels. Truncation and missing optional assemblies are reported.
Use domain-specific content paging for large block/type source text.

## Native metadata and execution

`DescribeEngineeringSurface`, `DescribeEngineeringCreation` and
`DescribeEngineeringActions` expose metadata for native attributes and
operations. Constructors, setters, arbitrary actions and event callbacks
remain available for discovery. They require a reviewed worker operation
before they can execute through MCP.

The V19 method registry enables the two-argument `PlcWatchTable.Export`
overload in ReadWrite. It shares validation with `ExportWatchTable` and
writes one new SimaticML file beneath the output root. Other method signatures
are reported as discovery-only. The worker's typed watch-table tools list,
export and explicitly create or replace offline watch tables. Import accepts
one named table from an XML file under the output root or an authorised
`--api-file-root`. Inspect the exported table before saving a replacement.

## Availability

The interface routes public contracts in installed, trusted, exactly referenced
Siemens assemblies. Missing modules, licences, unsupported native states and
UI-only functions cannot be supplied by the bridge. Private/internal APIs,
arbitrary .NET methods and open generic type targets are excluded. Read
indexers accept scalar and enum inputs. Unsupported value shapes fail explicitly.

Development bundles include exact V17 to V20 workers by default. Use the
matching worker for your installed TIA Portal version. V20 is experimental
and its licensed project workflows still require validation. V21 requires a
dedicated adapter.
