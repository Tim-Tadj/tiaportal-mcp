# Third-Party Notices

This notice covers the direct runtime dependencies and build inputs of TIA
Portal MCP `0.1.0-alpha.1`. It does not replace the licence files supplied
with each dependency.

## MCP and Microsoft Runtime Components

- `ModelContextProtocol` 1.4.1 is provided under the Apache-2.0 licence.
- `Microsoft.Extensions.Hosting` 10.0.10 and its Microsoft.Extensions and
  System runtime dependencies are provided under the MIT licence and their
  accompanying third-party notices.
- `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 is a build-time
  reference package provided under the MIT licence. It is not a TIA Portal
  runtime component.

The public bundles include the complete Apache 2.0 and Microsoft MIT licence
texts plus Microsoft's bundled third-party notices under each worker's
`third-party-licenses` directory.

## Siemens Build Inputs

The worker build uses
`Siemens.Collaboration.Net.TiaPortal.Packages.Openness` for the exact worker
major version. This package provides build targets which reference the
matching local TIA Portal PublicAPI assemblies. It is a build input and no
Siemens-supplied runtime or object-code DLL from it is included in the public
bundles.

The package also carries Siemens licence material. Those terms govern the
build input but are not a grant to redistribute Siemens object code. Package
inspection must reject `Siemens.Engineering*` and
`Siemens.Collaboration.Net*` DLLs. At runtime, the worker resolves the matching
Siemens assemblies from the user's installed TIA Portal environment.

## Test-Only Dependencies

`Microsoft.NET.Test.Sdk`, MSTest and their transitive dependencies are used by
the repository test projects. They are not included in the public application
bundles.
