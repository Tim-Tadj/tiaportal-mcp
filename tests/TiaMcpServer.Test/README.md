# TiaMcpServer.Test

MSTest project verifying portal connectivity, project handling, devices and MCP
server behaviour.

## Environment prerequisites

- .NET Framework 4.8 installed.
- The TIA Portal version selected with `TiaWorkerVersion` installed and running.
- The user is in the `Siemens TIA Openness` Windows group.
- `TiaPortalLocation` points to that TIA Portal installation when the Siemens
  resolver cannot discover it automatically.

No source or test setting assumes a particular clone directory.

## Test paths

The test suite resolves its paths at runtime:

- `TIA_MCP_TEST_PROJECT_PATH` can select an existing local project (`.apXX`).
  When it is unset, `assets/TestProject1.zap20` is discovered relative to the
  repository and extracted into the temporary test output root.
- `TIA_MCP_TEST_SESSION_PATH` selects an existing local multiuser session
  (`.alsXX`). When it is unset, the suite looks below `assets/` and then uses
  `assets/TestSession1/TestSession1_LS_1.als20` as the expected path.
- `TIA_MCP_TEST_OUTPUT_ROOT` selects the root used by save-as, export and
  temporary project operations. When it is unset, a clone-specific directory
  below the Windows temporary directory is used so test discovery and execution
  resolve the same paths.

Relative environment-variable values are resolved from the repository root, so
the same commands work from any clone location. For example:

```powershell
$env:TIA_MCP_TEST_SESSION_PATH = `
    'tests\TiaMcpServer.Test\assets\TestSession1\TestSession1_LS_1.als20'
$env:TIA_MCP_TEST_OUTPUT_ROOT = Join-Path $env:TEMP 'TIA-Portal-MCP-Tests'
```

The project tests can save or compile the selected project. Use a disposable
copy when setting `TIA_MCP_TEST_PROJECT_PATH`. Session tests require the
multiuser session to be created separately.

The bundled project archive targets TIA Portal V20. Set the project and session
variables to matching assets when testing another worker version.

## Running tests

From the repository root, select the required worker version:

```powershell
dotnet test -p:TiaWorkerVersion=20
```

Follow the root `AGENTS.md` test execution policy and obtain explicit
confirmation before running the suite.
