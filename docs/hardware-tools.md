# Hardware tools

These tools use the installed Siemens Openness API and the open project.
Use full group/device/item paths from `GetProjectTree`.

| Tool | Purpose |
| --- | --- |
| `GetAccessibleDevices` | Discover devices on one selected PC interface, returning names, IPs, MACs and device series. Select the mode, interface name and number from `GetOnlineConnectionOptions`. |
| `GetHardwareNetworkInfo` | Read a module's attributes, I/O addresses, network nodes and IO-controller assignment. Read the interface item for network details and the nested I/O submodule for its addresses. |
| `ChangeHardwareType` | Replace an offline module using Siemens `ChangeType`, provided its current type matches `expectedTypeIdentifier`. Supply the complete order-number and firmware identifier from hardware read-back. |
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
dotnet test tests/TiaMcpServer.Test/TiaMcpServer.Test.csproj -p:TiaMajorVersion=19 -p:SiemensEngineeringCopyLocal=true --filter FullyQualifiedName~TestHardwareTools
```

Network discovery depends on the selected adapter and network visibility.
Compilation verifies the engineering configuration; device runtime status
and individual I/O operation require separate physical checks.
