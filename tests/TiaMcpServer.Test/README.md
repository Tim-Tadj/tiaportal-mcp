# TiaMcpServer.Test

MSTest project verifying portal connectivity, project handling, devices, and MCP server behavior.

## Environment Prerequisites

- .NET Framework 4.8 installed
- Siemens TIA Portal V20 installed and running
- User in Windows group "Siemens TIA Openness"
- Env var `TiaPortalLocation` set to `C:\\Program Files\\Siemens\\Automation\\Portal V20`

## Test Assets
- `assets/TestProject1.zap20` – local project used in tests.
- `TestSession1.als20` – create this multi-user session manually for session tests.

See `Settings.cs` for configuration options such as project paths and timeouts.

## Focused online tests

The online-tool suite does not open projects or request device operations.
It still needs matching Siemens assemblies available to test discovery:

```powershell
dotnet test tests/TiaMcpServer.Test/TiaMcpServer.Test.csproj -p:TiaMajorVersion=19 -p:SiemensEngineeringCopyLocal=true --filter FullyQualifiedName~TestOnlineTools
```

Run from the repository root and check the executed/passed counts. The
separate `TiaMcpServer.Version.Test` project can test V19/V20/V21 version and
resolver branches without Siemens assemblies. See
[online tools and version builds](../../docs/online-tools.md) for commands
and the distinction between these checks and native/live verification.

## Test Execution Policy

- Offer to run tests, but only execute them after explicit user confirmation. See root `AGENTS.md` for details.
