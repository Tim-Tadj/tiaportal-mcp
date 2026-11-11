# Hardware tools

These tools use the installed Siemens Openness API and the open project.
Use full group/device/item paths from `GetProjectTree`.

| Tool | Purpose |
| --- | --- |
| `GetAccessibleDevices` | Discover devices on one selected PC interface, returning names, IPs, MACs and device series. Select the mode, interface name and number from `GetOnlineConnectionOptions`. |
| `GetHardwareNetworkInfo` | Read a module's attributes, I/O addresses, network nodes and IO-controller assignment. Read the interface item for network details and the nested I/O submodule for its addresses. |
| `ChangeHardwareType` | Replace an offline module using Siemens `ChangeType`, provided its current type matches `expectedTypeIdentifier`. Supply the complete order-number and firmware identifier from hardware read-back. |
| `SetBaseUnitPotentialGroup` | Set a light/new or dark/continued power group after checking the expected current value. |
| `CompileHardware` | Compile the selected hardware item and return its result, error/warning counts and recursive compiler messages. |

After replacing a module, read back the module order, I/O addresses, subnet
and controller assignment, then compile and inspect the messages before using
`SaveProject`. Hardware replacement and compilation leave saving explicit.
I/O address attributes retain Siemens' native units, including bit lengths.

The hardware read-back, head replacement and compilation were exercised with
TIA Portal V19 and an ET 200SP station containing two digital input cards and
two analogue input cards. Unit tests cover stale-type rejection, incomplete
type identifiers, missing-project handling and tool annotations.

Run the focused tests from the repository root with matching Siemens assemblies:

```powershell
dotnet test tests/TiaMcpServer.Test/TiaMcpServer.Test.csproj -p:TiaWorkerVersion=19 -p:TiaAccessProfile=ReadWrite -p:SiemensEngineeringCopyLocal=true --filter FullyQualifiedName~TestHardwareTools
```

Network discovery depends on the selected adapter and network visibility.
Compilation verifies the engineering configuration; device runtime status
and individual I/O operation require separate physical checks.

## Access profiles and BaseUnits

Hardware inspection is available in both profiles. `ChangeHardwareType`,
`SetBaseUnitPotentialGroup` and `CompileHardware` require ReadWrite.
`SetBaseUnitPotentialGroup` checks the expected current value (0 dark,
1 light) before updating the offline project. The setting must match the
fitted BaseUnit. Light bases begin new power groups; dark bases continue the
group to their left. Compile and read back before saving. No hardware
download, network assignment or CPU control is included in these tools.

The new hardware and online tools have been exercised with V19. The existing
release support matrix still applies to other worker versions.
