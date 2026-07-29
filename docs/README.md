# Project Documentation

This directory records the product direction for the TIA Portal MCP server and
separates planned architecture from currently shipped behaviour.

## Document Set

- [Architecture](architecture.md): the two public profiles, exact-version worker
  model, safety boundaries and response design.
- [Model-Facing Output Format Policy](output-formats.md): the normative
  `responseFormat` contract, automatic selection, CSV and bounded TOON
  profiles, compact JSON fallback and paging metadata.
- [ChatGPT Local Connection](chatgpt-local.md): the local process, outbound
  tunnel and data boundary for ChatGPT.
- [Alpha Release](alpha-release.md): the exact scope, evidence, completed
  publication checklist and deferred work for `0.1.0-alpha.1`.
- [Roadmap](roadmap.md): phased delivery plan and the disposition of upstream
  pull requests and issues.
- [Current Status](status.md): the current repository baseline, known gaps and
  the release definition of done.
- [Changelog Policy](changelog-policy.md): release categories, versioning and
  compatibility notation.
- [Error Model](error-model.md): the existing Portal and MCP exception mapping
  conventions.

## Reading Guide

Start with [Current Status](status.md) to understand what exists today. Read
[Architecture](architecture.md) for the intended end state, then use
[Roadmap](roadmap.md) for delivery order and upstream integration decisions.
Use [Model-Facing Output Format Policy](output-formats.md) when implementing,
testing or consuming tool result rendering.

The architecture and roadmap are design commitments. A capability is not
considered supported until it meets the definition of done in
[Current Status](status.md) and appears in a published release manifest.
The narrower `0.1.0-alpha.1` prerelease contract is defined separately in
[Alpha Release Gate](alpha-release.md). Alpha inclusion means that a component
is available for evaluation, not that it meets the supported-release
definition of done.
