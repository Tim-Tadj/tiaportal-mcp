# Third-Party Notices

This notice covers the direct runtime dependencies of TIA Portal MCP
`0.1.0-alpha.1`. It does not replace the licence files supplied with each
dependency or the release-specific redistribution review required by the
bundle assembler.

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

## Siemens Collaboration Packages

The worker build uses these Siemens-supplied packages:

- `Siemens.Collaboration.Net.OperatingSystem.Windows` 3.0.1725521661;
- `Siemens.Collaboration.Net.TiaPortal.Openness.Resolver` 1.1.1725480302;
- `Siemens.Collaboration.Net.TiaPortal.Packages.Openness` for the exact worker
  major version.

Their NuGet packages contain the Siemens "Royalty-free Software provided by
Siemens on sharing platforms for developers/users of Siemens products"
conditions. Source-code portions identify an MIT licence, while object-code
portions have separate Siemens conditions and intended-purpose restrictions.

Each worker carries the Siemens package conditions and ReadMe OSS material
under its `third-party-licenses` directory. This notice and those copied terms
do not authorise redistribution of Siemens object code. Release assembly
remains blocked until a named reviewer supplies the
`Siemens.Collaboration.Net` licence review marker required by
`build/Assemble-Bundles.ps1`. That marker must approve the exact hashes included
in the release. Siemens.Engineering assemblies from an installed TIA Portal
remain excluded from the bundle.

## Test-Only Dependencies

`Microsoft.NET.Test.Sdk`, MSTest and their transitive dependencies are used by
the repository test projects. They are not included in the public application
bundles.
