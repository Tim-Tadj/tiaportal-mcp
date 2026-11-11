# Native online tools and version builds

## Build and run

Use the profile-locked broker and exact-version workers described in
[Build and packaging](../build/README.md). A matching installed Siemens
PublicAPI is required to compile a worker; the broker has no Siemens dependency.

```powershell
./build/Build-Workers.ps1 -Version 0.1.0-alpha.2 -TiaVersions 19
```

For a development build of one worker:

```powershell
dotnet build src/TiaMcpServer/TiaMcpServer.csproj -c Release -p:TiaWorkerVersion=19 -p:TiaAccessProfile=ReadWrite '-p:TiaPortalLocation=C:\Program Files\Siemens\Automation\Portal V19'
```

Worker and broker runtime arguments use `--tia-version V19`, with the profile
fixed by the executable. The broker resolves installed versions and launches
the matching bundled worker. The published alpha supports V17 to V19; V20 is
deferred and V21 requires a separate modular adapter. The legacy main-branch
`TiaMajorVersion` build property and `--tia-major-version` argument are replaced
by this broker configuration.

Both profiles expose read-only online state and PLC values. `GoOnlineDevice`
and `GoOfflineDevice` require ReadWrite. A new TIA instance is started only
when `Connect(startIfMissing=true)` is called in ReadWrite. Use `processId`
when more than one TIA instance is running.

Membership of the Windows `Siemens TIA Openness` group is checked without
changing group membership. Siemens may require the user to approve its
application-access dialogue after an executable changes.

## Tools

| Tool | Behaviour |
| --- | --- |
| `ReadPlcValues(targetIpAddress, addresses, ...)` | Read absolute scalar PLC I/Q/M/DB values over S7 TCP, independently of an engineering session. |
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

## Live PLC values

`ReadPlcValues` requires an explicit PLC IPv4 address and reads up to 16
uppercase scalar addresses. Supported forms include `I358.4`, `Q76.5`,
`IB358`, `MW100`, `DB1000.DBX358.4` and `DB1002.DBB76`. B/W/D values
are unsigned. The default CPU is S7300, rack 0, slot 2. S7-1200/1500
require permitted S7 access and non-optimised accessible memory.

```json
{"name":"ReadPlcValues","arguments":{"targetIpAddress":"10.72.72.69","addresses":["I358.4","Q76.5"],"sampleCount":40,"intervalMilliseconds":250,"timeoutMilliseconds":20000}}
```

Samples are time-stamped, but not atomic with a PLC scan. I/Q values are
process images; Q does not prove physical output or contactor state. The
reader issues no PLC writes or motor commands. Timeout includes waiting for
the single-reader gate; an uninterruptible pending TCP connection may finish
cleaning up after the caller receives a timeout. No partial result is returned.
S7netplus 0.20.0 is used under its included MIT licence.

On 10 November 2026, 40 samples from the bench S7-315 at 10.72.72.69
showed transitions on I358.4 and Q76.5 and their DB mirrors. DB171.DBW0
and DB171.DBW2 were zero in all samples. These observations are not a
physical output or complete equipment health test.

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
dotnet test tests/TiaMcpServer.Test/TiaMcpServer.Test.csproj -p:TiaWorkerVersion=19 -p:SiemensEngineeringCopyLocal=true --filter FullyQualifiedName~TestOnlineTools
```

The version suite links only the production build-version and assembly-resolver
files. It needs no TIA installation and can exercise the version-specific
branches for each target:

```powershell
dotnet test tests/TiaMcpServer.Version.Test/TiaMcpServer.Version.Test.csproj -p:TiaWorkerVersion=19
dotnet test tests/TiaMcpServer.Version.Test/TiaMcpServer.Version.Test.csproj -p:TiaWorkerVersion=20
dotnet test tests/TiaMcpServer.Version.Test/TiaMcpServer.Version.Test.csproj -p:TiaWorkerVersion=21
```

Check executed/passed test counts as well as exit status. An absent API can
prevent discovery without producing a failing process exit code. The older
project/session tests require their own configured assets and are not part of
the focused filter above.

## Compatibility evidence

The native online and PLC-value tools were exercised with V19 before broker
integration, including exact-process attachment, online/offline transitions
and read-only S7-315 sampling. Those checks do not prove physical I/O or
equipment operation.

For the combined source, the V19 focused hardware, online and PLC-value unit
tests pass. The shared contracts, operation gate and Siemens-free version
tests also pass. Both broker profiles and V19 workers build without warnings.
The existing release support matrix remains authoritative for other versions;
the integration does not establish new runtime support for them.

References:

- [Siemens V21: setting a PLC online](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/functions-for-accessing-the-data-of-a-plc-device/functions-for-accessing-plc-service/setting-plc-online)
- [Siemens V21: online connection parameters](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/functions-for-accessing-the-data-of-a-plc-device/functions-for-accessing-plc-service/accessing-parameters-of-an-online-connection)
- [Siemens V20: service self-description](https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/general-functions/self-description-support-for-attributes-navigators-actions-and-services)
