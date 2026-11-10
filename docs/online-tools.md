# Native online tools and version builds

## Build and run

Build the server against the PublicAPI assemblies for the intended TIA version.
The NuGet reference package locates an installation; it does not supply the
licensed Siemens API assemblies. A matching API is required for a full build.
Building and testing also require a .NET SDK. If NuGet has no configured
package sources, restore with `--source https://api.nuget.org/v3/index.json`
and the same `TiaMajorVersion` property before building.

```powershell
dotnet build src/TiaMcpServer/TiaMcpServer.csproj -c Release -p:TiaMajorVersion=19 '-p:TiaPortalLocation=C:\Program Files\Siemens\Automation\Portal V19'
```

Use `20` or `21` and the corresponding installation path for those builds.
Outputs are separated under `src/TiaMcpServer/bin/V19/Release/net48`,
`bin/V20/Release/net48` and `bin/V21/Release/net48`. Intermediate files are
also separate and excluded from source compilation across all three versions.

Set the runtime environment variable `TiaPortalLocation` to the matching
installation if registry-based discovery is unsuitable. V21 uses the `net48`
API directory, rather than its modern .NET assemblies. Siemens assemblies
are not copied into release output by default.

```powershell
$env:TiaPortalLocation = 'C:\Program Files\Siemens\Automation\Portal V19'
& './src/TiaMcpServer/bin/V19/Release/net48/TiaMcpServer.exe' --tia-major-version 19
```

The version argument is optional and defaults to the compiled version. A
mismatched argument is refused before attachment. Membership of the Windows
`Siemens TIA Openness` group is checked without changing group membership.
Siemens may require the user to approve its local Openness application-access
dialogue, including after an executable changes.

## Tools

| Tool | Behaviour |
| --- | --- |
| `Connect(processId?, startIfMissing=false)` | Attach to an existing TIA instance. Multiple instances require an explicit process ID. Starting a new instance is opt-in. |
| `GetDeviceOnlineState(deviceItemPath)` | Read native connection state, current service availability and declared service metadata. |
| `GetOnlineConnectionOptions(deviceItemPath)` | Enumerate connection modes and PC interface names/numbers without changing settings. |
| `GoOnlineDevice(deviceItemPath, ...)` | Reuse the existing TIA connection by default and wait for a native connection result. |
| `GoOfflineDevice(deviceItemPath, timeoutSeconds=30)` | Disconnect the engineering session and wait for its state. |

Online tools require an exact group/device/item path. Nested items are
supported. Ungrouped devices use `UngroupedDevices/device/item`. Bare names,
empty segments and `.`/`..` segments are refused. Unlike the older general
item lookup, these tools never resolve only a prefix of a supplied path.

Example requests, using names from the engineering project:

```json
{"name":"Connect","arguments":{"processId":1234}}
{"name":"GetDeviceOnlineState","arguments":{"deviceItemPath":"Station/CPU"}}
{"name":"GoOnlineDevice","arguments":{"deviceItemPath":"Station/CPU"}}
```

To change the connection, first inspect `GetOnlineConnectionOptions`, then
supply all four options together: `mode`, `pcInterfaceName`,
`pcInterfaceNumber` and `targetIpAddress`. The target must be a complete
dotted IPv4 address. Partial options fail before a native online request.
TIA can refuse `ApplyConfiguration` while another session is online. The
tool does not disconnect other devices to work around that restriction.
Omit all four options to use the existing connection instead.

```json
{
  "name": "GoOnlineDevice",
  "arguments": {
    "deviceItemPath": "Station/CPU",
    "mode": "PN/IE",
    "pcInterfaceName": "Exact name returned by GetOnlineConnectionOptions",
    "pcInterfaceNumber": 1,
    "targetIpAddress": "192.168.0.10",
    "timeoutSeconds": 30
  }
}
```

The online tools do not save the project, download parameters, start or stop
a CPU, or issue motor commands. Changing connection settings is an explicit
engineering-session operation. The native Siemens call is synchronous and
cannot be cancelled by the polling timeout. A pending transition is reported
as pending, with `RequestedStateReached=false`; read the state before deciding
whether another action is appropriate.

## Diagnostic limits

`OnlineState` is the engineering connection state. It is not a health check,
CPU RUN/STOP state, motor state, fault list or diagnostic buffer. The response
explicitly reports `RuntimeDiagnosticsAvailable=false` for this implementation.
Use the device's online diagnostics/commissioning view for runtime health.

Service booleans describe what `GetService<T>()` returns at the time of the
read. For example, a download service may be unavailable while a device is
online and available after taking its engineering session offline. Declared
`Services` metadata can include types that are currently unavailable; the
availability booleans are separate. A non-null service does not prove that a
particular operation will be accepted by the device. These tools do not yet
implement a native download workflow.

If an item does not expose `OnlineProvider`, the read reports that explicitly.
An unsupported operation is not classified as a device fault. Motor, safety,
I/O and protection testing remain separate from engineering connectivity.

## Tests

The focused suite needs the matching Siemens API for test discovery, but its
tests do not attach to TIA or make device requests. Copying the locally
installed API into test output enables discovery. Do not redistribute it.

```powershell
dotnet test tests/TiaMcpServer.Test/TiaMcpServer.Test.csproj -p:TiaMajorVersion=19 -p:SiemensEngineeringCopyLocal=true --filter FullyQualifiedName~TestOnlineTools
```

The version suite links only the production build-version and assembly-resolver
files. It needs no TIA installation and can exercise the version-specific
branches for each target:

```powershell
dotnet test tests/TiaMcpServer.Version.Test/TiaMcpServer.Version.Test.csproj -p:TiaMajorVersion=19
dotnet test tests/TiaMcpServer.Version.Test/TiaMcpServer.Version.Test.csproj -p:TiaMajorVersion=20
dotnet test tests/TiaMcpServer.Version.Test/TiaMcpServer.Version.Test.csproj -p:TiaMajorVersion=21
```

Check executed/passed test counts as well as exit status. An absent API can
prevent discovery without producing a failing process exit code. The older
project/session tests require their own configured assets and are not part of
the focused filter above.

## Compatibility evidence

Checked on 10 November 2026:

- V19 release builds with zero warnings and errors against the installed API.
- Eleven focused online-tool tests pass. Two version/resolver tests pass for
  each of V19, V20 and V21.
- V19 stdio initialisation, tool discovery and exact-process attachment work.
  Live device reads cover an S7-315 PN/DP and SIMOCODE pro V PN.
- The SIMOCODE engineering-session offline/online cycle succeeds using the
  existing connection, including while the CPU has another online session.
- The S7-315 engineering-session offline/online cycle also succeeds. Both
  reconnects reach the requested state without a polling timeout.
- The bench project's existing unsaved state and last-saved timestamp remain
  unchanged across the live checks; no project save or hardware download is
  performed by this verification.
- The explicit connection-setting attempt exposed TIA's online-mode
  restriction; the default reuse path was added and retested successfully.
- V20/V21 reference packages restore and their version-specific build
  properties are checked. Full builds correctly refuse the missing Siemens
  APIs. V20/V21 full compilation and live operation remain unverified.
- Siemens' V20/V21 documentation supports the shared online service and
  connection model. Documentation review and API-independent tests do not
  replace compiling or running against those versions.

References:

- [Siemens V21: setting a PLC online](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/functions-for-accessing-the-data-of-a-plc-device/functions-for-accessing-plc-service/setting-plc-online)
- [Siemens V21: online connection parameters](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/functions-for-accessing-the-data-of-a-plc-device/functions-for-accessing-plc-service/accessing-parameters-of-an-online-connection)
- [Siemens V20: service self-description](https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/general-functions/self-description-support-for-attributes-navigators-actions-and-services)
