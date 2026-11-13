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
4. For a requested native operation, use ReadWrite, the returned schema, the
   exact `expectedProjectPath` and `acceptNativeEffects=true`. Arguments are a
   JSON array in parameter order. Supply `null` placeholders for out parameters;
   ref/out results are returned separately. Generic methods accept ordered
   `genericTypes` names. The worker checks native generic constraints.
5. Inspect the result and current target state. Release exclusive-access and
   similar native resources with `ReleaseApiObject`. Follow the native operation's
   save, compile, transaction and online requirements explicitly.

An empty expected project path is permitted only with no project or local
session open. A changed or ambiguous selection is rejected. Member IDs include
the declaring contract and signature; do not invent or reuse IDs across versions.
References expire after eviction, project/session changes or a changed parent
binding. Native proxy equality and collection order are checked where replay is
available. Method-created objects and nested values are session-bound references,
so native deletion can still make a later call fail. Re-discover after such failure.

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

`WriteApiProperty` and `WriteApiField` compare `expectedValue` before writing.
For a write-only property, send `expectedValue=null`; the result states that
the prior value could not be checked. Sensitive names and SecureString outputs
are redacted. Name-based redaction cannot identify every possible secret in
arbitrary native payloads.

## Native dynamic metadata

`DescribeEngineeringSurface` and `ReadEngineeringSurfaceAttributes` accept
native or API references to engineering objects. `DescribeEngineeringCreation`
returns creation types, named parameters and mandatory flags for an object or
composition. Use `CreateEngineeringObject` with those exact names. Siemens
creation metadata does not always declare parameter types; use a constrained
explicit typed value when a type cannot be inferred.

`UpdateEngineeringAttributes` takes named changes with `expectedValue` and
`value`. Preflight completes before attribute writes start; a native failure
can still leave earlier fields changed. `DescribeEngineeringActions` exposes
ordered argument types for `InvokeEngineeringAction`, including native
composition and association actions. Describing an action never invokes it.

## Events and callbacks

Events appear in `DescribeApiObject`. `SubscribeApiEvent` creates a bounded
subscription, `ReadApiEvents` polls after the last sequence and
`UnsubscribeApiEvent` releases it. A shared 512-event ring reports dropped
observations. Native event arguments can expire before polling. Up to 64
subscriptions are retained and released when disconnecting.
Project selection changes observed by the worker expire subscriptions and disable
their policies even if native detachment fails. An external TIA UI close is only
observed on the next MCP context refresh. Avoid editing project selection in the
UI while a synchronous callback policy is subscribed; unsubscribe first. The
event handler does not re-enter Portal discovery from inside a native callback.

Native operations can require a synchronous decision before they return.
Provide a declarative delegate argument rather than waiting for a later poll:

```json
{"$callback":{"argument":0,"set":{"CurrentSelection":"ExactNativeEnumValue"}}}
```

Use exact runtime-type cases when one callback receives different native
configuration subclasses, as happens in download workflows:

```json
{"$callback":{"argument":0,"cases":[
  {"type":"Siemens.Engineering.ExactConfigurationType",
   "policy":{"set":{"CurrentSelection":"ExactNativeEnumValue"}}}
]}}
```

The type and enum names above are placeholders to discover from the installed
schema. Unknown runtime types fail closed unless an explicit `default` policy
is supplied. Policies support a fixed `return`, property `set` rules and
`out` values keyed by ref/out parameter name. Non-void callbacks need an
explicit return; `{"$default":true}` explicitly chooses the native default.
Task-return callbacks return completed tasks after the policy executes.
Policies do not execute arbitrary expressions or code.

## Effects and files

All generic methods, constructors, setters, native actions, subscriptions and
disposal are ReadWrite operations. Native effects are unclassified and may
include project changes, file writes, PLC downloads, CPU commands or security
changes. Operation metadata conservatively marks those possible effects. No
generic invocation is advertised as an offline-only or read-only method.

Typed workflow exports stay beneath the configured output root. Repeat
`--api-file-root <path>` to grant additional roots for typed native file and
directory inputs, for example an existing project or library location. These
grants permit native reads and writes. Ordinary string parameters remain
literal because they may represent logical names rather than filesystem paths.
The native bridge is not an operating-system sandbox: native methods can use
embedded paths, existing project locations or strings. Review the selected
native contract and its effects before accepting the call.

Successful calls report `nativeCallCompleted=true`; a result projection failure
is reported separately. A native exception can occur after partial effects.
Read state back before retrying a write, creation, download or native action.
Readback and result projection do not roll back an operation.

## Availability

The interface routes public contracts in installed, trusted, exactly referenced
Siemens assemblies. Missing modules, licences, unsupported native states and
UI-only functions cannot be supplied by the bridge. Private/internal APIs,
arbitrary .NET methods and open generic type targets are excluded. Read
indexers accept scalar and enum inputs. Unsupported value shapes fail explicitly.

V17, V18 and V19 are the build targets for this development package. V20 and
V21 are not delivered. These additions have build and static-review evidence
only. No tests, live TIA calls or PLC operations were run.
