# Roadmap

Status date: 29 July 2026.

## Delivery Principles

- Safety and exact-version correctness come before a larger tool count.
- The read-only boundary is structural, not a command-line preference.
- New surfaces use the compact contract and shared metadata registry.
- Upstream work is adapted in small reviewable commits rather than merged as a
  large feature bundle.
- A planned capability is not advertised as supported until its exact-version
  worker passes the definition of done in [Current Status](status.md).
  Prerelease-only workers must carry their experimental validation label.

## Alpha Cut: 0.1.0-alpha.1

The first prerelease is a deliberately narrower evaluation milestone:

- publish separate Read and ReadWrite Windows x64 bundles;
- bundle V17 to V20 workers through the broker;
- label V19 runtime-validated and V17, V18 and V20 build-only and
  runtime-unverified;
- exclude V21 with a clear unsupported-version diagnostic;
- publish two Claude Desktop MCPBs and direct VS Code stdio helpers;
- publish ChatGPT Secure MCP Tunnel profiles which launch the local broker
  through `--mcp-command`;
- complete the Siemens-free build, schema, profile-boundary, packaging and
  installation checks in [Alpha Release Gate](alpha-release.md).

No further TIA-dependent test blocks this alpha. Signing, a formal SBOM,
provenance, V21, a VSIX, further runtime matrices and broader tool coverage move
to later prereleases or the supported-release gate.

## Phase 0: Baseline and Decisions

- Snapshot the current tool, prompt and response schemas.
- Record architecture decisions for the two profiles, broker, workers,
  supported version policy and legacy contract.
- Classify every current tool as query, lifecycle, file write, project
  mutation, online or high risk.
- Turn the upstream decisions below into tracked work items.

Exit condition: the current contract can be compared automatically with later
releases, and every existing tool has an owner and access classification.

## Phase 1: Version and Safety Foundation

- Create a broker with no Siemens API reference.
- Create exact-version workers for V17 to V20, then add V21 through its
  dedicated adapter after the alpha.
- Use a dedicated V21 adapter while sharing legacy source where practical.
- Generate read-only and read-write manifests from the explicit registered tool
  types and shared metadata registry.
- Add a second policy gate for mutation.
- Add deterministic Portal process selection and ownership tracking.
- Serialise Openness calls through a worker operation scheduler.
- Stop automatically changing Windows group membership.
- Add central output-root, traversal, overwrite and conflict policies.
- Add `GetCapabilities` and a read-only doctor or preflight command.

Exit condition: two profile bundles can select the correct internal worker, and
automated allow-list checks prove that the read-only worker cannot call a
mutation service.

Initial installation detection, broker selection and packaging templates are
in progress on the architecture branch. They are scaffolding and do not yet
satisfy this phase's exit condition.

## Phase 2: Compact Contract and Reliability

- Introduce summary, standard and full detail levels.
- Add stable entity references, canonical paths, deterministic paging and
  bounded filters.
- Replace unbounded tree results with child browsing.
- Normalise Openness attributes to JSON-safe values.
- Add consistent typed errors and structured partial-result warnings to all
  read tools.
- Ensure read failures do not disconnect a healthy Portal session.
- Add compact bulk and compilation result contracts.
- Add the normative `responseFormat=Auto|Toon|Csv|Json` policy. Use CSV for
  eligible `Summary` tables, the bounded `tia-toon-table/1` TOON v4.1 profile
  for eligible `Standard` tables, and compact JSON for `Full`, nested and
  compatibility results.
- Add lossless format eligibility checks, explicit override errors, stable
  format and paging metadata, and compact JSON fallback for automatic
  selection.
- Publish a migration guide and retain v0.0.18 as the legacy fallback while
  consumers move to the compact pre-1.0 contract.

Exit condition: synthetic large-project evaluations stay within documented
response budgets; format conformance and fallback tests cover flat, nested,
empty and boundary-sized results; and schema snapshots cover both profiles and
every supported worker.

## Phase 3: LLM Guidance and Evaluation

- Build the shared tool metadata registry.
- Generate tool descriptions, annotations, capability output and reference
  documentation from it.
- Replace duplicate one-tool prompts with workflows for safe inspection,
  selected export, import and compile, and compile diagnosis.
- Add trace fixtures for discovery, ambiguous paths, pagination, profile
  refusal, version mismatch and mutation intent.
- Compare compact JSON and bounded TOON on project trace fixtures, including
  field association, row boundaries and paging decisions. Treat improved model
  structural accuracy as a measured design goal, not a universal assumption.
- Teach tool instructions to retain `Auto` for normal inspection, request
  `Json` for full or nested data, and recover from an incompatible explicit
  format without retrying unchanged.

Exit condition: evaluation traces reliably choose the correct profile-safe
workflow without guessing software or block paths.

## Phase 4: Broader Read Coverage

Add surfaces in this order:

1. PLC tag tables and tags;
2. external sources and software groups;
3. hardware modules, addresses, network interfaces, subnets and topology;
4. project and global libraries;
5. WinCC classic and Unified screens, tags, alarms, connections, scripts and
   text lists.

Each surface starts with read tools and an exact-version capability matrix.
Unavailable APIs are omitted from the worker manifest rather than registered as
tools which always fail.

Exit condition: each advertised read surface has compact schemas, version
guards, Siemens-free unit coverage and licensed runtime smoke coverage.

## Phase 5: Controlled Write Coverage

- Add tag, source, block, type, hardware, network, library and HMI mutations
  only after their read models are stable.
- Require dry-run or preview support where the API permits it.
- Make overwrite and conflict behaviour explicit.
- Constrain all file output to an allowed root.
- Keep delete, download, force, online control and safety changes disabled by
  default and separately gated.

Exit condition: every write tool has side-effect annotations, policy tests,
audit logging, version-specific runtime coverage and recovery guidance.

## Phase 6: Packaging and Release

- Produce the two signed public bundles with internal exact-version workers.
- Add the release manifest, checksums, SBOM, provenance and licence notices.
- Publish a locally executed ChatGPT Secure MCP Tunnel connection kit, Claude
  Desktop MCPB and VS Code installation adapter without forking the
  implementation.
- Keep the ChatGPT tunnel client, broker and worker on the user's PC. Launch
  the stdio broker through `--mcp-command` and prohibit hosted or publicly
  exposed project components.
- Validate installation from a clean Windows account with no source checkout.
- Add compliant Streamable HTTP only if a future client requires it after the
  local stdio packages are stable.

Exit condition: both profiles install cleanly in every supported client and
select the correct worker on each advertised TIA Portal version.

## Upstream Pull Request Dispositions

The local baseline already contains every upstream merge through v0.0.18.
The upstream inventory was rechecked on 29 July 2026 and contains 11 pull
requests, three open and eight closed. Every pull request is listed below.

| Pull request | Disposition | Roadmap action |
| --- | --- | --- |
| [#2](https://github.com/heilingbrunner/tiaportal-mcp/pull/2) | Superseded | Keep the historical resolver work, but replace backward assembly redirection with exact-version workers. |
| [#6](https://github.com/heilingbrunner/tiaportal-mcp/pull/6) | Already integrated | Retain open-project detection and hierarchy behaviour while moving output to the compact contract. |
| [#10](https://github.com/heilingbrunner/tiaportal-mcp/pull/10) | Already integrated | Reuse its version and test groundwork in the worker test matrix. |
| [#14](https://github.com/heilingbrunner/tiaportal-mcp/pull/14) | Already integrated through #15 | Replace runtime HMI guards with worker capability manifests where API availability differs. |
| [#15](https://github.com/heilingbrunner/tiaportal-mcp/pull/15) | Already integrated | Preserve the repository and sample layout where it fits the new solution boundaries. |
| [#16](https://github.com/heilingbrunner/tiaportal-mcp/pull/16) | Already integrated | Register SIMATIC SD import and export only in compatible read-write workers. |
| [#19](https://github.com/heilingbrunner/tiaportal-mcp/pull/19) | Superseded by #20 | Keep the resulting error-model documentation and expand the pattern to read tools. |
| [#20](https://github.com/heilingbrunner/tiaportal-mcp/pull/20) | Already integrated | Use its typed error groundwork as the baseline for Phase 2. |
| [#26](https://github.com/heilingbrunner/tiaportal-mcp/pull/26) | Reject as a whole, defer individual ideas | Do not import non-functional stubs or the broad high-risk surface. Re-specify and validate useful read families one at a time in Phase 4. |
| [#28](https://github.com/heilingbrunner/tiaportal-mcp/pull/28) | Adapt selected code | Port the root block-group fix and external-source concepts with typed errors, structured logging, path policy and exact-version tests. |
| [#30](https://github.com/heilingbrunner/tiaportal-mcp/pull/30) | Partially integrated; adapt remaining work separately | Safe attribute serialisation and the principal device, block and type read guards are integrated. Adapt tag reads next, preserve compatible tool intent where practical, document the compact pre-1.0 contract changes and redesign the bespoke HTTP work separately. |

The three open pull requests share the same base and modify overlapping areas.
Pull requests #26 and #30 both change `McpServer.cs`, `Responses.cs` and
`Portal.cs`; #28 overlaps their `McpServer.cs` and `Portal.cs` changes. They
must not be merged sequentially without manual re-specification and conflict
resolution.

## Upstream Issue Dispositions

The same audit found 11 upstream issues, seven open and four closed. Every issue
is listed below.

| Issue | Disposition | Roadmap action |
| --- | --- | --- |
| [#1](https://github.com/heilingbrunner/tiaportal-mcp/issues/1) | Superseded by #24 and #25 | Deliver exact-version workers and validate the requested version against the selected worker. |
| [#3](https://github.com/heilingbrunner/tiaportal-mcp/issues/3) | Baseline integrated | Replace the unbounded software tree with bounded structured browsing and optional rich detail. |
| [#4](https://github.com/heilingbrunner/tiaportal-mcp/issues/4) | Baseline integrated | Keep Unicode text trees as an optional bounded format, not the default response. |
| [#5](https://github.com/heilingbrunner/tiaportal-mcp/issues/5) | Already integrated | Preserve open-project detection and canonical project paths. |
| [#7](https://github.com/heilingbrunner/tiaportal-mcp/issues/7) | Adapt | Address path ambiguity and duplicate prompts through the metadata registry and workflow prompts. |
| [#18](https://github.com/heilingbrunner/tiaportal-mcp/issues/18) | Partially integrated with an adapted contract | The central output root, reparse checks and default no-overwrite policy now exist. The architecture deliberately permits relative or root-contained absolute paths instead of adopting the proposed filename-only contract. Handle-level race protection and a separate import-source policy are explicitly deferred for the experimental alpha and remain required before a supported ReadWrite release. |
| [#22](https://github.com/heilingbrunner/tiaportal-mcp/issues/22) | Adapt | Add external-source import, generate and export as separate version-aware operations. |
| [#24](https://github.com/heilingbrunner/tiaportal-mcp/issues/24) | In progress and alpha-gated | Exact-package workers and clear mismatch diagnostics exist. V17 to V19 compile in the current environment; V20 still needs its exact PublicAPI references in the prepared release environment before the alpha can be published. |
| [#25](https://github.com/heilingbrunner/tiaportal-mcp/issues/25) | Deferred from the alpha; supported-release critical | V21 is rejected explicitly in `0.1.0-alpha.1`. Implement and validate its dedicated adapter before advertising V21 or completing the supported-release gate. |
| [#27](https://github.com/heilingbrunner/tiaportal-mcp/issues/27) | Defer | Evaluate `TiaFileFormat` only as an optional, separately licensed offline read adapter. Its published scope and commercial terms require separate review. |
| [#29](https://github.com/heilingbrunner/tiaportal-mcp/issues/29) | Partially integrated | Bounded JSON-safe attribute normalisation and typed guards now cover the main device, block and type reads. Complete read-tool logging and structured partial-result handling before expanding the surface. |

## Near-Term Integration Order

1. Extend compact output-format conformance with reference decoder fixtures,
   then evaluate CSV, TOON and compact JSON on representative LLM workflows.
2. Complete issue #29 read logging and structured partial-result handling after
   the integrated safe serialisation and typed read-error work.
3. Generate explicit profile manifests from the registered tool types. After
   the alpha, complete the remaining issue #18 handle-level race protection
   and define a separate import source policy. The broker and central output
   policy are already present.
4. Port PR #30 tag reads and PR #28 external-source operations in isolated
   commits.
5. Evaluate selected PR #26 read families only after their contracts and
   capability tests exist.
