# src/TiaMcpServer Guidelines

- Adhere to repository-wide style rules in [`../../style.md`](../../style.md).
- Place new MCP tools under `ModelContextProtocol/` and Siemens API wrappers under `Siemens/`.
- Route eligible tool results through the shared output formatter. Do not add
  tool-local CSV, TOON or JSON serialisation.
- Follow the normative
  [`../../docs/output-formats.md`](../../docs/output-formats.md) contract,
  including exact `responseFormat` values, lossless eligibility checks and
  stable format and paging metadata.
- Keep MCP JSON-RPC, schemas, client configuration and manifests as JSON.
- Do not describe TOON as universally more accurate. Its structural-accuracy
  benefit is a design goal which requires project-specific evaluation.

## Agent Policy

- Follow the root [`AGENTS.md`](../../AGENTS.md) for general guidance.
- Tests and operations that depend on the user environment (e.g., TIA Portal) should only be run after explicit user confirmation.
