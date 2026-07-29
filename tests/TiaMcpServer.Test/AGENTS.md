# TiaMcpServer.Test Guidelines

- Follow repository style rules in [`../../style.md`](../../style.md).
- Use MSTest attributes `[TestClass]` and `[TestMethod]`.
- Name test files and methods descriptively (e.g., `Test1Portal`).
- For output-format tests, follow
  [`../../docs/output-formats.md`](../../docs/output-formats.md). Cover
  deterministic columns, CSV null and empty-string distinction, bounded TOON
  eligibility, exact payload line endings, compact JSON fallback, incompatible
  explicit overrides and selected-format paging metadata.
- Keep formatter and contract fixtures Siemens-free where possible. Do not
  assert that TOON is universally more accurate than JSON; measure model
  behaviour separately from encoder conformance.
- Offer to run tests, but only execute them after explicit user confirmation. See the root [`AGENTS.md`](../../AGENTS.md) for the full Test Execution Policy.
- Run `dotnet test` from the repository root after modifying tests.
- Store test assets under the `assets/` directory.
