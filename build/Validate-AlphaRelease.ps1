[CmdletBinding()]
param(
    [Parameter()]
    [string]$Version,

    [Parameter()]
    [string]$BuildDirectory
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Assert-Condition
{
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition)
    {
        throw $Message
    }
}

function Assert-ExactSequence
{
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Actual,

        [Parameter(Mandatory = $true)]
        [object[]]$Expected,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $actualText = @($Actual) -join ','
    $expectedText = @($Expected) -join ','
    if ($actualText -cne $expectedText)
    {
        throw "$Description must be '$expectedText', but was '$actualText'."
    }
}

function Get-XmlPropertyValue
{
    param(
        [Parameter(Mandatory = $true)]
        [xml]$Document,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    $node = $Document.SelectSingleNode(
        "/Project/PropertyGroup/$PropertyName")
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText))
    {
        throw "MSBuild property '$PropertyName' is missing."
    }

    return $node.InnerText.Trim()
}

function Get-NumericFourPartVersion
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion
    )

    $match = [regex]::Match(
        $ReleaseVersion,
        '^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:[-+].*)?$')
    if (-not $match.Success)
    {
        throw "Release version '$ReleaseVersion' is not valid SemVer."
    }

    return "$($match.Groups['major'].Value).$($match.Groups['minor'].Value).$($match.Groups['patch'].Value).0"
}

function Assert-VersionConsistency
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter()]
        [string]$RequestedVersion
    )

    $propsPath = Join-Path $RepositoryRoot 'Directory.Build.props'
    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    $releaseVersion = Get-XmlPropertyValue `
        -Document $props `
        -PropertyName 'TiaMcpReleaseVersion'

    if (-not [string]::IsNullOrWhiteSpace($RequestedVersion) -and
        $RequestedVersion -cne $releaseVersion)
    {
        throw "Requested release version '$RequestedVersion' does not match Directory.Build.props '$releaseVersion'."
    }

    $numericVersion = Get-NumericFourPartVersion -ReleaseVersion $releaseVersion
    $versionProperty = Get-XmlPropertyValue `
        -Document $props `
        -PropertyName 'Version'
    $informationalVersion = Get-XmlPropertyValue `
        -Document $props `
        -PropertyName 'InformationalVersion'
    $assemblyVersion = Get-XmlPropertyValue `
        -Document $props `
        -PropertyName 'AssemblyVersion'
    $fileVersion = Get-XmlPropertyValue `
        -Document $props `
        -PropertyName 'FileVersion'

    Assert-Condition `
        -Condition ($versionProperty -ceq '$(TiaMcpReleaseVersion)') `
        -Message 'The Version property must be sourced from TiaMcpReleaseVersion.'
    Assert-Condition `
        -Condition ($informationalVersion -ceq '$(TiaMcpReleaseVersion)') `
        -Message 'The InformationalVersion property must be sourced from TiaMcpReleaseVersion.'
    Assert-Condition `
        -Condition ($assemblyVersion -ceq $numericVersion) `
        -Message "AssemblyVersion must be '$numericVersion'."
    Assert-Condition `
        -Condition ($fileVersion -ceq $numericVersion) `
        -Message "FileVersion must be '$numericVersion'."

    $projectVersionOverrides = @(
        Get-ChildItem `
            -LiteralPath $RepositoryRoot `
            -Recurse `
            -File `
            -Filter '*.csproj' |
        Where-Object {
            $text = Get-Content -LiteralPath $_.FullName -Raw
            $text -match '<(?:Version|InformationalVersion|AssemblyVersion|FileVersion)>'
        }
    )
    if ($projectVersionOverrides.Count -gt 0)
    {
        $paths = @($projectVersionOverrides | ForEach-Object { $_.FullName })
        throw "Project-local release version properties are not allowed: $($paths -join ', ')."
    }

    $workerProjectPath = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpServer\TiaMcpServer.csproj'
    [xml]$workerProject = Get-Content -LiteralPath $workerProjectPath -Raw
    $hostingReference = $workerProject.SelectSingleNode(
        "/Project/ItemGroup/PackageReference[@Include='Microsoft.Extensions.Hosting']")
    $mcpReference = $workerProject.SelectSingleNode(
        "/Project/ItemGroup/PackageReference[@Include='ModelContextProtocol']")
    $opennessBuildReference = $workerProject.SelectSingleNode(
        "/Project/ItemGroup/PackageReference[@Include='Siemens.Collaboration.Net.TiaPortal.Packages.Openness']")
    Assert-Condition `
        -Condition ($null -ne $hostingReference -and
            $hostingReference.GetAttribute('Version') -ceq '10.0.10') `
        -Message 'Microsoft.Extensions.Hosting must remain pinned to stable version 10.0.10 for this alpha.'
    Assert-Condition `
        -Condition ($null -ne $mcpReference -and
            $mcpReference.GetAttribute('Version') -ceq '1.4.1') `
        -Message 'ModelContextProtocol must remain pinned to stable version 1.4.1 for this alpha.'
    Assert-Condition `
        -Condition ($null -ne $opennessBuildReference -and
            $opennessBuildReference.GetAttribute('Version') -ceq
                '$(TiaOpennessPackageVersion)') `
        -Message 'The Siemens Openness package must remain a version-selected build-only reference.'
    foreach ($forbiddenRuntimePackage in @(
        'Siemens.Collaboration.Net.OperatingSystem.Windows',
        'Siemens.Collaboration.Net.TiaPortal.Openness.Resolver'
    ))
    {
        Assert-Condition `
            -Condition ($null -eq $workerProject.SelectSingleNode(
                "/Project/ItemGroup/PackageReference[@Include='$forbiddenRuntimePackage']")) `
            -Message "Runtime package '$forbiddenRuntimePackage' must not be distributed by this alpha."
    }
    Assert-Condition `
        -Condition ((Get-XmlPropertyValue `
            -Document $workerProject `
            -PropertyName 'TiaWorkerVersion') -ceq '19') `
        -Message 'The default local worker must target the latest bundled version, TIA Portal V19.'

    $changelog = Get-Content `
        -LiteralPath (Join-Path $RepositoryRoot 'CHANGELOG.md') `
        -Raw
    Assert-Condition `
        -Condition ($changelog.Contains("Target prerelease: ``$releaseVersion``.")) `
        -Message "CHANGELOG.md does not identify '$releaseVersion' as the target prerelease."

    return [pscustomobject]@{
        ReleaseVersion = $releaseVersion
        NumericVersion = $numericVersion
    }
}

function Test-SymbolExpression
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Expression,

        [Parameter(Mandatory = $true)]
        [string[]]$Symbols
    )

    $orTerms = [regex]::Split($Expression.Trim(), '\s*\|\|\s*')
    foreach ($orTerm in $orTerms)
    {
        $termMatches = $true
        $andFactors = [regex]::Split($orTerm, '\s*&&\s*')
        foreach ($andFactor in $andFactors)
        {
            $factor = $andFactor.Trim().Trim('(', ')').Trim()
            $negated = $factor.StartsWith(
                '!',
                [System.StringComparison]::Ordinal)
            if ($negated)
            {
                $factor = $factor.Substring(1).Trim()
            }

            if ($factor -notmatch '^[A-Za-z_][A-Za-z0-9_]*$')
            {
                throw "Unsupported compiler-symbol expression '$Expression'."
            }

            $isDefined = $Symbols -contains $factor
            if ($negated)
            {
                $isDefined = -not $isDefined
            }
            $termMatches = $termMatches -and $isDefined
        }

        if ($termMatches)
        {
            return $true
        }
    }

    return $false
}

function Get-CompiledToolNames
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$SourcePaths,

        [Parameter(Mandatory = $true)]
        [string[]]$Symbols
    )

    $toolNames = New-Object System.Collections.Generic.List[string]
    foreach ($sourcePath in $SourcePaths)
    {
        $parentStates = New-Object System.Collections.Generic.List[bool]
        $active = $true
        foreach ($line in [System.IO.File]::ReadLines($sourcePath))
        {
            $directive = [regex]::Match(
                $line,
                '^\s*#(?<kind>if|elif|else|endif)(?:\s+(?<expression>.*))?$')
            if ($directive.Success)
            {
                $kind = $directive.Groups['kind'].Value
                switch ($kind)
                {
                    'if'
                    {
                        $parentStates.Add($active)
                        $condition = Test-SymbolExpression `
                            -Expression $directive.Groups['expression'].Value `
                            -Symbols $Symbols
                        $active = $active -and $condition
                        break
                    }

                    'endif'
                    {
                        if ($parentStates.Count -eq 0)
                        {
                            throw "Unexpected #endif in '$sourcePath'."
                        }

                        $lastIndex = $parentStates.Count - 1
                        $active = $parentStates[$lastIndex]
                        $parentStates.RemoveAt($lastIndex)
                        break
                    }

                    default
                    {
                        throw "Tool source '$sourcePath' uses unsupported #$kind branching. Extend the release validator before changing the profile boundary."
                    }
                }

                continue
            }

            if ($active)
            {
                $attribute = [regex]::Match(
                    $line,
                    '\[McpServerTool\(Name\s*=\s*"(?<name>[^"]+)"')
                if ($attribute.Success)
                {
                    $toolNames.Add($attribute.Groups['name'].Value)
                }
            }
        }

        if ($parentStates.Count -ne 0)
        {
            throw "Unclosed compiler directive in '$sourcePath'."
        }
    }

    return @($toolNames)
}

function Assert-ToolSet
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Actual,

        [Parameter(Mandatory = $true)]
        [string[]]$Expected,

        [Parameter(Mandatory = $true)]
        [string]$ProfileDescription
    )

    $duplicates = @(
        $Actual |
        Group-Object |
        Where-Object { $_.Count -gt 1 }
    )
    if ($duplicates.Count -gt 0)
    {
        throw "$ProfileDescription has duplicate tool names: $(@($duplicates.Name) -join ', ')."
    }

    $differences = @(
        Compare-Object `
            -ReferenceObject @($Expected | Sort-Object) `
            -DifferenceObject @($Actual | Sort-Object) `
            -CaseSensitive
    )
    if ($differences.Count -gt 0)
    {
        $details = @(
            $differences |
            ForEach-Object { "$($_.InputObject) $($_.SideIndicator)" }
        )
        throw "$ProfileDescription tool boundary changed: $($details -join '; ')."
    }
}

function Assert-SourceProfileBoundaries
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $serverSource = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpServer\ModelContextProtocol\McpServer.cs'
    $listSource = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpServer\ModelContextProtocol\McpListTools.cs'
    $toolSources = @($serverSource, $listSource)

    $readTools = @(
        'Connect',
        'GetCapabilities',
        'GetState',
        'GetProject',
        'GetProjectTree',
        'GetDeviceInfo',
        'GetDeviceItemInfo',
        'GetSoftwareInfo',
        'GetSoftwareTree',
        'GetBlockInfo',
        'GetBlocksWithHierarchy',
        'GetTypeInfo',
        'ListProjects',
        'GetDevices',
        'GetBlocks',
        'GetTypes'
    )
    $readWriteTools = @(
        $readTools
        'Disconnect',
        'OpenProject',
        'SaveProject',
        'SaveAsProject',
        'CloseProject',
        'CompileSoftware',
        'ExportBlock',
        'ImportBlock',
        'ExportBlocks',
        'ExportType',
        'ImportType',
        'ExportTypes'
    )
    $v20DocumentTools = @(
        'ExportAsDocuments',
        'ExportBlocksAsDocuments',
        'ImportFromDocuments',
        'ImportBlocksFromDocuments'
    )

    $actualRead = Get-CompiledToolNames `
        -SourcePaths $toolSources `
        -Symbols @('TIA_MCP_V19')
    $actualReadWrite = Get-CompiledToolNames `
        -SourcePaths $toolSources `
        -Symbols @('TIA_MCP_READ_WRITE', 'TIA_MCP_V19')
    $actualReadWriteV20 = Get-CompiledToolNames `
        -SourcePaths $toolSources `
        -Symbols @('TIA_MCP_READ_WRITE', 'TIA_MCP_V20')

    Assert-ToolSet `
        -Actual $actualRead `
        -Expected $readTools `
        -ProfileDescription 'Read'
    Assert-ToolSet `
        -Actual $actualReadWrite `
        -Expected $readWriteTools `
        -ProfileDescription 'ReadWrite V17 to V19'
    Assert-ToolSet `
        -Actual $actualReadWriteV20 `
        -Expected @($readWriteTools + $v20DocumentTools) `
        -ProfileDescription 'ReadWrite V20'

    $programPath = Join-Path $RepositoryRoot 'src\TiaMcpServer\Program.cs'
    $program = Get-Content -LiteralPath $programPath -Raw
    foreach ($registration in @(
        '.WithTools<McpServer>()',
        '.WithTools<McpListTools>()',
        '.WithPrompts<McpPrompts>()',
        '.WithRequestFilters(',
        '.AddCallToolFilter(',
        'AddSingleton<TiaOperationGate>()'
    ))
    {
        Assert-Condition `
            -Condition ($program.Contains($registration)) `
            -Message "The MCP host is missing explicit registration '$registration'."
    }
    Assert-Condition `
        -Condition (-not $program.Contains('WithToolsFromAssembly')) `
        -Message 'Assembly-wide MCP tool discovery is not permitted for the alpha profiles.'
    Assert-Condition `
        -Condition (-not $program.Contains('WithPromptsFromAssembly')) `
        -Message 'Assembly-wide MCP prompt discovery is not permitted for the alpha profiles.'
    Assert-Condition `
        -Condition ($program.Contains(
            'Openness.Initialize(') -and
            $program.Contains('runtimeSelection.TiaMajorVersion') -and
            $program.Contains('runtimeSelection.InstallPath')) `
        -Message 'Every worker must preflight Siemens.Engineering from the locked local TIA Portal installation.'

    $gatePath = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpServer\Runtime\TiaOperationGate.cs'
    $gate = Get-Content -LiteralPath $gatePath -Raw
    Assert-Condition `
        -Condition ($gate.Contains('new SemaphoreSlim(1, 1)')) `
        -Message 'The process-wide TIA operation gate must permit only one active tool call.'

    $gateTestsPath = Join-Path `
        $RepositoryRoot `
        'tests\TiaMcp.Contracts.Test\TiaOperationGateTests.cs'
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $gateTestsPath -PathType Leaf) `
        -Message 'Siemens-free operation-gate behavioural tests are missing.'
    $gateTests = Get-Content -LiteralPath $gateTestsPath -Raw
    foreach ($requiredGateTest in @(
        'RunAsync_SerialisesConcurrentOperations',
        'RunAsync_ReleasesGateAfterOperationFailure',
        'RunAsync_CancelledWaiterDoesNotReleaseAnotherOperation'
    ))
    {
        Assert-Condition `
            -Condition ($gateTests.Contains($requiredGateTest)) `
            -Message "The operation-gate test '$requiredGateTest' is missing."
    }
    $gateTestProjectPath = Join-Path `
        $RepositoryRoot `
        'tests\TiaMcp.Contracts.Test\TiaMcp.Contracts.Test.csproj'
    [xml]$gateTestProject = Get-Content `
        -LiteralPath $gateTestProjectPath `
        -Raw
    $gateCompile = $gateTestProject.SelectSingleNode(
        "/Project/ItemGroup/Compile[@Include='..\..\src\TiaMcpServer\Runtime\TiaOperationGate.cs']")
    Assert-Condition `
        -Condition ($null -ne $gateCompile -and
            ([string]$gateCompile.Link) -ceq
                'Runtime\TiaOperationGate.cs') `
        -Message 'The Siemens-free contract test project must compile the production operation gate.'

    $opennessSource = Get-Content `
        -LiteralPath (Join-Path `
            $RepositoryRoot `
            'src\TiaMcpServer\Siemens\Openness.cs') `
        -Raw
    Assert-Condition `
        -Condition (-not $opennessSource.Contains('Siemens.Collaboration.Net') -and
            $opennessSource.Contains('WindowsIdentity.GetCurrent()') -and
            $opennessSource.Contains('Environment.MachineName') -and
            $opennessSource.Contains('SecurityIdentifier') -and
            $opennessSource.Contains(
                'new WindowsPrincipal(identity).IsInRole(') -and
            $opennessSource.Contains(
                'Engineering.Configure(selectedVersion, tiaInstallPath)') -and
            $opennessSource.Contains('Engineering.Preflight()')) `
        -Message 'Openness group membership must compare the exact local group SID without Siemens Collaboration runtime code.'

    $assemblyPolicyPath = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpServer\Runtime\SiemensEngineeringAssemblyPolicy.cs'
    $assemblyPolicyTestsPath = Join-Path `
        $RepositoryRoot `
        'tests\TiaMcp.Contracts.Test\SiemensEngineeringAssemblyPolicyTests.cs'
    $assemblyPolicyCompile = $gateTestProject.SelectSingleNode(
        "/Project/ItemGroup/Compile[@Include='..\..\src\TiaMcpServer\Runtime\SiemensEngineeringAssemblyPolicy.cs']")
    Assert-Condition `
        -Condition ((Test-Path -LiteralPath $assemblyPolicyPath -PathType Leaf) -and
            (Test-Path -LiteralPath $assemblyPolicyTestsPath -PathType Leaf) -and
            $null -ne $assemblyPolicyCompile -and
            ([string]$assemblyPolicyCompile.Link) -ceq
                'Runtime\SiemensEngineeringAssemblyPolicy.cs') `
        -Message 'The exact Siemens assembly identity policy and Siemens-free tests are required.'

    $engineeringSource = Get-Content `
        -LiteralPath (Join-Path `
            $RepositoryRoot `
            'src\TiaMcpServer\Siemens\Engineering.cs') `
        -Raw
    foreach ($requiredResolverText in @(
        'GetConfiguredInstallPath()',
        '.SelectMany(directory => FindAssembliesRecursive(',
        'SiemensEngineeringAssemblyPolicy.FindFirstMatch('
    ))
    {
        Assert-Condition `
            -Condition ($engineeringSource.Contains($requiredResolverText)) `
            -Message "The local Siemens assembly resolver is missing '$requiredResolverText'."
    }
    Assert-Condition `
        -Condition (-not $engineeringSource.Contains('RegistryKey')) `
        -Message 'The Siemens resolver must use the locked expanded installation path rather than re-reading the registry.'

    $policyPath = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpServer\Security\ProfileAccessPolicy.cs'
    $policy = Get-Content -LiteralPath $policyPath -Raw
    Assert-Condition `
        -Condition ($policy.Contains('#if TIA_MCP_READ_WRITE') -and
            $policy.Contains('read-only worker rejected the mutating operation')) `
        -Message 'The secondary Read mutation policy is missing.'

    $portalPath = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpServer\Siemens\Portal.cs'
    $portal = Get-Content -LiteralPath $portalPath -Raw
    $demandWriteCount = [regex]::Matches(
        $portal,
        'ProfileAccessPolicy\.DemandWrite\(').Count
    Assert-Condition `
        -Condition ($demandWriteCount -ge 18) `
        -Message "Only $demandWriteCount direct mutation policy checks were found in Portal.cs."

    $brokerProjectPath = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpBroker\TiaMcpBroker.csproj'
    $brokerProject = Get-Content -LiteralPath $brokerProjectPath -Raw
    Assert-Condition `
        -Condition ($brokerProject -notmatch '(?i)Siemens') `
        -Message 'The dependency-free broker project must not reference Siemens components.'
    Assert-Condition `
        -Condition ($brokerProject -notmatch '<(?:PackageReference|ProjectReference|Reference)\b') `
        -Message 'The dependency-free broker project unexpectedly gained an assembly or package reference.'

    return [pscustomobject]@{
        ReadToolCount = $actualRead.Count
        ReadWriteToolCount = $actualReadWrite.Count
        ReadWriteV20ToolCount = $actualReadWriteV20.Count
        DirectMutationPolicyChecks = $demandWriteCount
    }
}

function Get-RejectionMessage
{
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Operation
    )

    $message = $null
    try
    {
        & $Operation | Out-Null
    }
    catch
    {
        $message = $_.Exception.Message
    }

    return $message
}

function Assert-DeferredVersionBoundaries
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion
    )

    $temporaryParent = [System.IO.Path]::GetFullPath(
        [System.IO.Path]::GetTempPath())
    $temporaryRoot = Join-Path `
        $temporaryParent `
        "tia-mcp-version-rejection-$([System.Guid]::NewGuid().ToString('N'))"
    $temporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
    $expectedPrefix = Join-Path $temporaryParent 'tia-mcp-version-rejection-'

    try
    {
        $buildScript = Join-Path $RepositoryRoot 'build\Build-Workers.ps1'
        $buildMessage = Get-RejectionMessage -Operation {
            & $buildScript `
                -Version $ReleaseVersion `
                -TiaVersions 21 `
                -OutputDirectory (Join-Path $temporaryRoot 'build')
        }
        Assert-Condition `
            -Condition (-not [string]::IsNullOrWhiteSpace($buildMessage) -and
                $buildMessage -match 'V21.*dedicated modular adapter') `
            -Message "Build-Workers.ps1 did not reject V21 at validation time. Message: '$buildMessage'."

        $assembleScript = Join-Path $RepositoryRoot 'build\Assemble-Bundles.ps1'
        $v20AssembleMessage = Get-RejectionMessage -Operation {
            & $assembleScript `
                -Version $ReleaseVersion `
                -TiaVersions 20 `
                -BuildDirectory (Join-Path $temporaryRoot 'missing-build') `
                -OutputDirectory (Join-Path $temporaryRoot 'release-v20')
        }
        Assert-Condition `
            -Condition (-not [string]::IsNullOrWhiteSpace(
                    $v20AssembleMessage) -and
                $v20AssembleMessage -match 'V20.*planned for a later alpha') `
            -Message "Assemble-Bundles.ps1 did not reject V20 at validation time. Message: '$v20AssembleMessage'."

        $v21AssembleMessage = Get-RejectionMessage -Operation {
            & $assembleScript `
                -Version $ReleaseVersion `
                -TiaVersions 21 `
                -BuildDirectory (Join-Path $temporaryRoot 'missing-build') `
                -OutputDirectory (Join-Path $temporaryRoot 'release-v21')
        }
        Assert-Condition `
            -Condition (-not [string]::IsNullOrWhiteSpace(
                    $v21AssembleMessage) -and
                $v21AssembleMessage -match 'V21.*unsupported') `
            -Message "Assemble-Bundles.ps1 did not reject V21 at validation time. Message: '$v21AssembleMessage'."
    }
    finally
    {
        if ((Test-Path -LiteralPath $temporaryRoot) -and
            $temporaryRoot.StartsWith(
                $expectedPrefix,
                [System.StringComparison]::OrdinalIgnoreCase))
        {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }

    $resolverPath = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpBroker\TiaVersionResolver.cs'
    $resolver = Get-Content -LiteralPath $resolverPath -Raw
    $v20CheckIndex = $resolver.IndexOf(
        'request == TiaVersionRequest.V20',
        [System.StringComparison]::Ordinal)
    $v21CheckIndex = $resolver.IndexOf(
        'request == TiaVersionRequest.V21',
        [System.StringComparison]::Ordinal)
    $installationScanIndex = $resolver.IndexOf(
        'installationDetector.Scan()',
        [System.StringComparison]::Ordinal)
    Assert-Condition `
        -Condition ($v20CheckIndex -ge 0 -and
            $v21CheckIndex -gt $v20CheckIndex -and
            $installationScanIndex -gt $v21CheckIndex) `
        -Message 'The broker must reject deferred V20 and unsupported V21 before scanning TIA Portal installations.'
}

function Assert-ReleaseManifest
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion
    )

    $manifestPath = Join-Path `
        $RepositoryRoot `
        'packaging\release-manifest.template.json'
    $manifestText = Get-Content -LiteralPath $manifestPath -Raw
    $manifest = $manifestText.Replace('__VERSION__', $ReleaseVersion) |
        ConvertFrom-Json

    Assert-Condition `
        -Condition ($manifest.schemaVersion -eq 1 -and
            $manifest.productVersion -ceq $ReleaseVersion -and
            $manifest.platform -ceq 'win-x64' -and
            $manifest.supportStatus -ceq 'experimental') `
        -Message 'The release manifest has inconsistent alpha identity or platform metadata.'
    Assert-ExactSequence `
        -Actual @($manifest.bundledTiaVersions) `
        -Expected @(17, 18, 19) `
        -Description 'bundledTiaVersions'
    Assert-ExactSequence `
        -Actual @($manifest.plannedTiaVersions) `
        -Expected @(20, 21) `
        -Description 'plannedTiaVersions'

    $expectedValidation = @{
        '17' = @($true, $false, 'experimental-build-only')
        '18' = @($true, $false, 'experimental-build-only')
        '19' = @($true, $false, 'experimental-prior-runtime-evidence')
        '20' = @($false, $false, 'planned-later-alpha')
        '21' = @($false, $false, 'unsupported-in-this-release')
    }
    foreach ($versionKey in @('17', '18', '19', '20', '21'))
    {
        $entry = $manifest.tiaVersionValidation.PSObject.Properties[$versionKey].Value
        $expected = $expectedValidation[$versionKey]
        Assert-Condition `
            -Condition ($entry.bundled -eq $expected[0] -and
                $entry.runtimeValidated -eq $expected[1] -and
                $entry.classification -ceq $expected[2]) `
            -Message "The release manifest has an incorrect V$versionKey validation classification."
    }

    Assert-Condition `
        -Condition ($manifest.profiles.read.package -ceq
            "tia-portal-mcp-$ReleaseVersion-read-win-x64.zip") `
        -Message 'The Read ZIP package name is inconsistent.'
    Assert-Condition `
        -Condition ($manifest.profiles.readwrite.package -ceq
            "tia-portal-mcp-$ReleaseVersion-readwrite-win-x64.zip") `
        -Message 'The ReadWrite ZIP package name is inconsistent.'
    Assert-Condition `
        -Condition ($manifest.clients.claudeDesktop.transport -ceq 'stdio' -and
            $manifest.clients.vscode.transport -ceq 'stdio' -and
            $manifest.clients.chatgpt.serverExecution -ceq 'local' -and
            $manifest.clients.chatgpt.directLocalConnection -eq $false) `
        -Message 'The release manifest does not preserve the local client execution contract.'
    Assert-Condition `
        -Condition ($manifest.siemensRuntime.runtimeOrObjectCodeDllsBundled -eq
            $false -and
            $manifest.siemensRuntime.assemblySource -ceq
                'local-tia-portal-installation') `
        -Message 'The release manifest must state that Siemens runtime DLLs come from the local TIA Portal installation.'

    $clientValidator = Join-Path `
        $RepositoryRoot `
        'build\Validate-Client-Packaging.ps1'
    $clientValidation = @(& $clientValidator -Version $ReleaseVersion)
    Assert-Condition `
        -Condition ($clientValidation.Count -gt 0) `
        -Message 'The client packaging validator returned no result.'
}

function Assert-ThirdPartyNoticeCoverage
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $licencePath = Join-Path $RepositoryRoot 'LICENSE.txt'
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $licencePath -PathType Leaf) `
        -Message 'LICENSE.txt is required for the alpha release.'
    $licence = Get-Content -LiteralPath $licencePath -Raw
    Assert-Condition `
        -Condition ($licence.Length -ge 1000 -and
            $licence.Contains('MIT License') -and
            $licence.Contains('Permission is hereby granted') -and
            $licence.Contains('THE SOFTWARE IS PROVIDED "AS IS"')) `
        -Message 'LICENSE.txt appears incomplete.'

    $noticePath = Join-Path $RepositoryRoot 'THIRD-PARTY-NOTICES.md'
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $noticePath -PathType Leaf) `
        -Message 'THIRD-PARTY-NOTICES.md is required for the alpha release.'

    $notice = Get-Content -LiteralPath $noticePath -Raw
    foreach ($requiredText in @(
        'ModelContextProtocol',
        'Microsoft.Extensions.Hosting',
        'Apache-2.0',
        'MIT',
        'Siemens.Collaboration.Net.TiaPortal.Packages.Openness',
        'build',
        'runtime or object-code DLL',
        'not a grant to redistribute Siemens object code',
        'installed TIA Portal',
        'complete Apache 2.0',
        'Microsoft MIT licence'
    ))
    {
        Assert-Condition `
            -Condition ($notice.IndexOf(
                $requiredText,
                [System.StringComparison]::OrdinalIgnoreCase) -ge 0) `
            -Message "THIRD-PARTY-NOTICES.md does not cover '$requiredText'."
    }

    $thirdPartyLicenceRoot = Join-Path `
        $RepositoryRoot `
        'THIRD-PARTY-LICENSES'
    $requiredLicenceFiles = @(
        [pscustomobject]@{
            Name = 'Apache-2.0.txt'
            MinimumLength = 10000
            RequiredText = 'Apache License'
        },
        [pscustomobject]@{
            Name = 'Microsoft-MIT.txt'
            MinimumLength = 1000
            RequiredText = 'Permission is hereby granted'
        }
    )
    foreach ($requiredLicenceFile in $requiredLicenceFiles)
    {
        $requiredLicencePath = Join-Path `
            $thirdPartyLicenceRoot `
            $requiredLicenceFile.Name
        Assert-Condition `
            -Condition (Test-Path `
                -LiteralPath $requiredLicencePath `
                -PathType Leaf) `
            -Message "The complete $($requiredLicenceFile.Name) text is required."

        $requiredLicenceText = Get-Content `
            -LiteralPath $requiredLicencePath `
            -Raw
        Assert-Condition `
            -Condition ($requiredLicenceText.Length -ge
                $requiredLicenceFile.MinimumLength -and
                $requiredLicenceText.Contains(
                    $requiredLicenceFile.RequiredText)) `
            -Message "$($requiredLicenceFile.Name) appears incomplete."
    }

    $serverProjectPath = Join-Path `
        $RepositoryRoot `
        'src\TiaMcpServer\TiaMcpServer.csproj'
    [xml]$serverProject = Get-Content -LiteralPath $serverProjectPath -Raw
    $hostingPackageReference = $serverProject.SelectSingleNode(
        "/Project/ItemGroup/PackageReference[@Include='Microsoft.Extensions.Hosting']")
    Assert-Condition `
        -Condition ($null -ne $hostingPackageReference -and
            $hostingPackageReference.GetAttribute('GeneratePathProperty') -ceq
                'true') `
        -Message "Package 'Microsoft.Extensions.Hosting' must expose its resolved path for legal-payload staging."

    $requiredWorkerPayload = @(
        [pscustomobject]@{
            Include = '..\..\THIRD-PARTY-LICENSES\Apache-2.0.txt'
            Link = 'third-party-licenses\Apache-2.0.txt'
        },
        [pscustomobject]@{
            Include = '..\..\THIRD-PARTY-LICENSES\Microsoft-MIT.txt'
            Link = 'third-party-licenses\Microsoft-MIT.txt'
        },
        [pscustomobject]@{
            Include = '$(PkgMicrosoft_Extensions_Hosting)\THIRD-PARTY-NOTICES.TXT'
            Link = 'third-party-licenses\Microsoft-THIRD-PARTY-NOTICES.txt'
        }
    )
    foreach ($payloadEntry in $requiredWorkerPayload)
    {
        $payloadNode = $serverProject.SelectSingleNode(
            "/Project/ItemGroup/None[@Include='$($payloadEntry.Include)']")
        Assert-Condition `
            -Condition ($null -ne $payloadNode -and
                ([string]$payloadNode.Link) -ceq $payloadEntry.Link -and
                ([string]$payloadNode.CopyToOutputDirectory) -ceq
                    'PreserveNewest') `
            -Message "Worker output does not stage '$($payloadEntry.Link)' from its authoritative source."
    }

    $assemblerPath = Join-Path $RepositoryRoot 'build\Assemble-Bundles.ps1'
    $assembler = Get-Content -LiteralPath $assemblerPath -Raw
    foreach ($requiredAssemblyText in @(
        'LICENSE.txt',
        'THIRD-PARTY-NOTICES.md',
        'Apache-2.0.txt',
        'Microsoft-MIT.txt',
        'Microsoft-THIRD-PARTY-NOTICES.txt',
        'Get-DllPolicyCategory',
        'A Siemens DLL reached the public bundle',
        "'.pdb'"
    ))
    {
        Assert-Condition `
            -Condition ($assembler.Contains($requiredAssemblyText)) `
            -Message "The bundle assembler does not enforce '$requiredAssemblyText'."
    }

    foreach ($rootPayload in @(
        'LICENSE.txt',
        'THIRD-PARTY-NOTICES.md'
    ))
    {
        Assert-Condition `
            -Condition ($assembler.Contains(
                "Join-Path `$repositoryRoot '$rootPayload'") -and
                $assembler.Contains(
                    "Join-Path `$profileStage '$rootPayload'")) `
            -Message "The bundle assembler does not stage root '$rootPayload' at the package root."
    }

    foreach ($forbiddenLicenceMarkerText in @(
        'LicenceReviewMarker',
        'approvedSha256',
        'Siemens-Collaboration-Net-LICENSE.md',
        'Siemens-Collaboration-Net-ReadMe-OSS.html'
    ))
    {
        Assert-Condition `
            -Condition (-not $assembler.Contains($forbiddenLicenceMarkerText)) `
            -Message "The public assembler must not rely on obsolete Siemens redistribution marker '$forbiddenLicenceMarkerText'."
    }
}

function Assert-SiemensFreeWorkflowBoundary
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $workflowPath = Join-Path `
        $RepositoryRoot `
        '.github\workflows\alpha-siemens-free.yml'
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $workflowPath -PathType Leaf) `
        -Message 'The Windows Siemens-free alpha workflow is missing.'

    $workflow = Get-Content -LiteralPath $workflowPath -Raw
    foreach ($requiredWorkflowText in @(
        'runs-on: windows-2022',
        '8.0.x',
        '10.0.x',
        'build\Validate-AlphaRelease.ps1',
        'tests\TiaMcp.Contracts.Test\TiaMcp.Contracts.Test.csproj',
        'src\TiaMcpBroker\TiaMcpBroker.csproj',
        "Name = 'Read'",
        "Name = 'ReadWrite'",
        '-BuildDirectory'
    ))
    {
        Assert-Condition `
            -Condition ($workflow.Contains($requiredWorkflowText)) `
            -Message "The Siemens-free workflow is missing '$requiredWorkflowText'."
    }

    foreach ($forbiddenWorkflowText in @(
        'tests\TiaMcpServer.Test',
        'build\Build-Workers.ps1',
        'build\Assemble-Bundles.ps1',
        'src\TiaMcpServer\TiaMcpServer.csproj'
    ))
    {
        Assert-Condition `
            -Condition (-not $workflow.Contains($forbiddenWorkflowText)) `
            -Message "The hosted workflow must not invoke Siemens-dependent input '$forbiddenWorkflowText'."
    }
}

function Get-MetadataAssemblyReferenceNames
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath
    )

    try
    {
        Add-Type -AssemblyName System.Reflection.Metadata -ErrorAction Stop
        $peReaderType = [System.Type]::GetType(
            'System.Reflection.PortableExecutable.PEReader, System.Reflection.Metadata',
            $false)
        if ($null -ne $peReaderType)
        {
            $stream = [System.IO.File]::OpenRead($AssemblyPath)
            $peReader = $null
            try
            {
                $peReader = New-Object `
                    System.Reflection.PortableExecutable.PEReader($stream)
                $metadataReader =
                    [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader(
                        $peReader)
                $references = New-Object System.Collections.Generic.List[string]
                foreach ($handle in $metadataReader.AssemblyReferences)
                {
                    $reference = $metadataReader.GetAssemblyReference($handle)
                    $references.Add($metadataReader.GetString($reference.Name))
                }

                return @($references)
            }
            finally
            {
                if ($null -ne $peReader)
                {
                    $peReader.Dispose()
                }
                $stream.Dispose()
            }
        }
    }
    catch
    {
        Write-Verbose "System.Reflection.Metadata inspection was unavailable: $($_.Exception.Message)"
    }

    $windowsPowerShell = Join-Path `
        $env:WINDIR `
        'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $windowsPowerShell -PathType Leaf))
    {
        throw "Could not inspect managed assembly references for '$AssemblyPath': System.Reflection.Metadata and Windows PowerShell are unavailable."
    }

    $inspectionCommand = @'
$ErrorActionPreference = 'Stop'
$assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom(
    $env:TIA_MCP_ASSEMBLY_TO_INSPECT)
$assembly.GetReferencedAssemblies() |
    ForEach-Object { $_.Name }
'@
    $encodedCommand = [System.Convert]::ToBase64String(
        [System.Text.Encoding]::Unicode.GetBytes($inspectionCommand))
    $previousInspectionPath = [System.Environment]::GetEnvironmentVariable(
        'TIA_MCP_ASSEMBLY_TO_INSPECT')
    try
    {
        $env:TIA_MCP_ASSEMBLY_TO_INSPECT = $AssemblyPath
        $references = @(
            & $windowsPowerShell `
                -NoLogo `
                -NoProfile `
                -NonInteractive `
                -EncodedCommand $encodedCommand
        )
        if ($LASTEXITCODE -ne 0)
        {
            throw "Windows PowerShell reference inspection exited with code $LASTEXITCODE."
        }

        return @(
            $references |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )
    }
    catch
    {
        throw "Could not inspect managed assembly references for '$AssemblyPath': $($_.Exception.Message)"
    }
    finally
    {
        [System.Environment]::SetEnvironmentVariable(
            'TIA_MCP_ASSEMBLY_TO_INSPECT',
            $previousInspectionPath)
    }
}

function Assert-BrokerBuildBoundary
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildRoot,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion,

        [Parameter(Mandatory = $true)]
        [string]$NumericVersion
    )

    $fullBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $fullBuildRoot -PathType Container) `
        -Message "Broker build directory was not found: '$fullBuildRoot'."

    foreach ($profile in @(
        [pscustomobject]@{ Key = 'read'; Name = 'Read' },
        [pscustomobject]@{ Key = 'readwrite'; Name = 'ReadWrite' }
    ))
    {
        $brokerDirectory = Join-Path `
            $fullBuildRoot `
            "brokers\$($profile.Key)"
        $brokerExecutable = Join-Path $brokerDirectory 'TiaPortalMcp.exe'
        Assert-Condition `
            -Condition (Test-Path -LiteralPath $brokerExecutable -PathType Leaf) `
            -Message "The $($profile.Name) broker executable was not found: '$brokerExecutable'."

        $siemensFiles = @(
            Get-ChildItem `
                -LiteralPath $brokerDirectory `
                -Recurse `
                -File |
            Where-Object { $_.Name -like 'Siemens*' }
        )
        Assert-Condition `
            -Condition ($siemensFiles.Count -eq 0) `
            -Message "The $($profile.Name) broker output contains Siemens files."

        $references = @(
            Get-MetadataAssemblyReferenceNames -AssemblyPath $brokerExecutable
        )
        $siemensReferences = @(
            $references |
            Where-Object {
                $_.StartsWith(
                    'Siemens',
                    [System.StringComparison]::OrdinalIgnoreCase)
            }
        )
        Assert-Condition `
            -Condition ($siemensReferences.Count -eq 0) `
            -Message "The $($profile.Name) broker references Siemens assemblies: $($siemensReferences -join ', ')."

        $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
            $brokerExecutable)
        Assert-Condition `
            -Condition ($versionInfo.ProductVersion -ceq $ReleaseVersion) `
            -Message "The $($profile.Name) broker ProductVersion is '$($versionInfo.ProductVersion)', expected '$ReleaseVersion'."
        Assert-Condition `
            -Condition ($versionInfo.FileVersion -ceq $NumericVersion) `
            -Message "The $($profile.Name) broker FileVersion is '$($versionInfo.FileVersion)', expected '$NumericVersion'."

        foreach ($rejection in @(
            [pscustomobject]@{
                Version = 'V20'
                Diagnostic = 'planned for a later alpha'
            },
            [pscustomobject]@{
                Version = 'V21'
                Diagnostic = 'unsupported in this release'
            }
        ))
        {
            $startInfo = New-Object System.Diagnostics.ProcessStartInfo
            $startInfo.FileName = $brokerExecutable
            $startInfo.Arguments = "--tia-version $($rejection.Version)"
            $startInfo.WorkingDirectory = $brokerDirectory
            $startInfo.UseShellExecute = $false
            $startInfo.CreateNoWindow = $true
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $process = New-Object System.Diagnostics.Process
            $process.StartInfo = $startInfo
            try
            {
                Assert-Condition `
                    -Condition $process.Start() `
                    -Message "The $($profile.Name) broker could not be started for its $($rejection.Version) rejection check."
                $standardOutput = $process.StandardOutput.ReadToEnd()
                $standardError = $process.StandardError.ReadToEnd()
                $process.WaitForExit()
                Assert-Condition `
                    -Condition ($process.ExitCode -eq 3 -and
                        $standardError.IndexOf(
                            $rejection.Version,
                            [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
                        $standardError.IndexOf(
                            $rejection.Diagnostic,
                            [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
                        [string]::IsNullOrWhiteSpace($standardOutput)) `
                    -Message "The $($profile.Name) broker did not reject $($rejection.Version) with the bounded preflight diagnostic."
            }
            finally
            {
                $process.Dispose()
            }
        }

        $metadataPath = Join-Path $brokerDirectory 'tia-mcp-build.json'
        if (Test-Path -LiteralPath $metadataPath -PathType Leaf)
        {
            $metadata = Get-Content -LiteralPath $metadataPath -Raw |
                ConvertFrom-Json
            $actualHash = (Get-FileHash `
                -LiteralPath $brokerExecutable `
                -Algorithm SHA256).Hash.ToLowerInvariant()
            Assert-Condition `
                -Condition ($metadata.schemaVersion -eq 1 -and
                    $metadata.component -ceq 'broker' -and
                    $metadata.accessProfile -ceq $profile.Name -and
                    $metadata.releaseVersion -ceq $ReleaseVersion -and
                    $metadata.executable -ceq 'TiaPortalMcp.exe' -and
                    $metadata.executableSha256 -ceq $actualHash) `
                -Message "The $($profile.Name) broker build metadata is inconsistent."
        }
    }

    $validatedWorkerCount = 0
    $workerRoot = Join-Path $fullBuildRoot 'workers'
    if (Test-Path -LiteralPath $workerRoot -PathType Container)
    {
        foreach ($profile in @(
            [pscustomobject]@{ Key = 'read'; Name = 'Read' },
            [pscustomobject]@{ Key = 'readwrite'; Name = 'ReadWrite' }
        ))
        {
            foreach ($tiaVersion in @(17, 18, 19))
            {
                $workerDirectory = Join-Path `
                    $workerRoot `
                    "$($profile.Key)\v$tiaVersion"
                $workerExecutable = Join-Path `
                    $workerDirectory `
                    'TiaMcpServer.exe'
                Assert-Condition `
                    -Condition (Test-Path `
                        -LiteralPath $workerExecutable `
                        -PathType Leaf) `
                    -Message "The V$tiaVersion $($profile.Name) worker executable was not found: '$workerExecutable'."

                $siemensWorkerFiles = @(
                    Get-ChildItem `
                        -LiteralPath $workerDirectory `
                        -Recurse `
                        -File |
                    Where-Object { $_.Name -like 'Siemens*.dll' }
                )
                Assert-Condition `
                    -Condition ($siemensWorkerFiles.Count -eq 0) `
                    -Message "The V$tiaVersion $($profile.Name) worker output contains Siemens DLLs."

                $workerReferences = @(
                    Get-MetadataAssemblyReferenceNames `
                        -AssemblyPath $workerExecutable
                )
                $collaborationReferences = @(
                    $workerReferences |
                    Where-Object {
                        $_.StartsWith(
                            'Siemens.Collaboration.Net',
                            [System.StringComparison]::OrdinalIgnoreCase)
                    }
                )
                Assert-Condition `
                    -Condition ($collaborationReferences.Count -eq 0) `
                    -Message "The V$tiaVersion $($profile.Name) worker references Siemens Collaboration runtime assemblies: $($collaborationReferences -join ', ')."

                $workerVersionInfo =
                    [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
                        $workerExecutable)
                Assert-Condition `
                    -Condition ($workerVersionInfo.ProductVersion -ceq
                        $ReleaseVersion -and
                        $workerVersionInfo.FileVersion -ceq $NumericVersion) `
                    -Message "The V$tiaVersion $($profile.Name) worker version is inconsistent with '$ReleaseVersion'."

                $workerMetadataPath = Join-Path `
                    $workerDirectory `
                    'tia-mcp-build.json'
                $workerMetadata = Get-Content `
                    -LiteralPath $workerMetadataPath `
                    -Raw |
                    ConvertFrom-Json
                $workerHash = (Get-FileHash `
                    -LiteralPath $workerExecutable `
                    -Algorithm SHA256).Hash.ToLowerInvariant()
                Assert-Condition `
                    -Condition ($workerMetadata.schemaVersion -eq 1 -and
                        $workerMetadata.component -ceq 'worker' -and
                        $workerMetadata.accessProfile -ceq $profile.Name -and
                        $workerMetadata.tiaVersion -eq $tiaVersion -and
                        $workerMetadata.releaseVersion -ceq $ReleaseVersion -and
                        $workerMetadata.executable -ceq 'TiaMcpServer.exe' -and
                        $workerMetadata.executableSha256 -ceq $workerHash) `
                    -Message "The V$tiaVersion $($profile.Name) worker build metadata is inconsistent."

                $validatedWorkerCount++
            }
        }
    }

    return [pscustomobject]@{
        Brokers = 2
        Workers = $validatedWorkerCount
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent $PSScriptRoot))
$versionResult = Assert-VersionConsistency `
    -RepositoryRoot $repositoryRoot `
    -RequestedVersion $Version
$profileResult = Assert-SourceProfileBoundaries `
    -RepositoryRoot $repositoryRoot
Assert-DeferredVersionBoundaries `
    -RepositoryRoot $repositoryRoot `
    -ReleaseVersion $versionResult.ReleaseVersion
Assert-ReleaseManifest `
    -RepositoryRoot $repositoryRoot `
    -ReleaseVersion $versionResult.ReleaseVersion
Assert-ThirdPartyNoticeCoverage -RepositoryRoot $repositoryRoot
Assert-SiemensFreeWorkflowBoundary -RepositoryRoot $repositoryRoot

$builtBoundary = [pscustomobject]@{
    Brokers = 0
    Workers = 0
}
if (-not [string]::IsNullOrWhiteSpace($BuildDirectory))
{
    $builtBoundary = Assert-BrokerBuildBoundary `
        -BuildRoot $BuildDirectory `
        -ReleaseVersion $versionResult.ReleaseVersion `
        -NumericVersion $versionResult.NumericVersion
}

Write-Output ([pscustomobject]@{
    Version = $versionResult.ReleaseVersion
    NumericVersion = $versionResult.NumericVersion
    ReadTools = $profileResult.ReadToolCount
    ReadWriteTools = $profileResult.ReadWriteToolCount
    ReadWriteV20Tools = $profileResult.ReadWriteV20ToolCount
    DirectMutationPolicyChecks = $profileResult.DirectMutationPolicyChecks
    BuiltBrokersValidated = $builtBoundary.Brokers
    BuiltWorkersValidated = $builtBoundary.Workers
    V20 = 'Deferred and rejected before installation discovery'
    V21 = 'Rejected before installation discovery or build'
    ClientPackaging = 'Validated'
    WorkerRuntime = 'Not invoked'
})
