# Project Documentation

This directory records the product direction for the TIA Portal MCP server and
separates planned architecture from currently shipped behaviour.

## Document Set

- [Architecture](architecture.md): the two public profiles, exact-version worker
  model, safety boundaries and response design.
- [Model-Facing Output Format Policy](output-formats.md): the normative
  `responseFormat` contract, automatic selection, CSV and bounded TOON
  profiles, compact JSON fallback and paging metadata.
- [ChatGPT Local Connection](chatgpt-local.md): the local-only process,
  transport and data boundary for ChatGPT.
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
