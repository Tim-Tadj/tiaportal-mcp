# Model-Facing Output Format Policy

Status: Normative.

Last updated: 29 July 2026.

## Scope

This policy defines how eligible MCP tool results are rendered for an LLM. It
applies to the model-facing result text after a tool has produced its bounded
response data.

It does not change the MCP protocol envelope. JSON-RPC requests and responses,
tool schemas, client configuration, MCPB manifests, release manifests and
build metadata remain JSON. A format choice also does not change tool
semantics, paging, access-profile enforcement or the underlying response DTO.

The words MUST, MUST NOT, SHOULD and MAY in this document are normative.

## Public Parameter

Tools which support negotiated rendering expose:

```text
responseFormat = Auto | Toon | Csv | Json
```

`Auto` is the default. Existing calls which omit `responseFormat` therefore
receive the format selected by the rules below.

The returned format identifier is one of:

- `csv`
- `toon-v4.1`
- `json`

## Automatic Selection

`Auto` selects the preferred lossless representation which is suitable for the
requested detail and result shape:

| Result | Automatic format |
| --- | --- |
| `Summary` list which meets the flat-table eligibility rules | CSV |
| `Standard` list which meets the `tia-toon-table/1` eligibility rules | TOON v4.1 |
| `Full` detail | Compact JSON |
| Nested, heterogeneous or compatibility result | Compact JSON |
| A `Summary` or `Standard` result which is not eligible for its preferred table format | Compact JSON |

Automatic selection MUST inspect the actual bounded result. It MUST fall back
to compact JSON whenever CSV or the bounded TOON profile cannot preserve the
result exactly. It MUST NOT flatten nested values, discard fields, rename
columns, coerce structured values to display strings or imply that a partial
page is complete.

## Explicit Overrides

`Json` is the universal lossless override and MUST work for every valid result
shape.

`Csv` requests RFC 4180 table output. `Toon` requests the bounded TOON v4.1
profile. These overrides succeed only when the requested detail and actual
result meet the relevant eligibility rules.

An explicit `Csv` or `Toon` request for `Full`, nested, heterogeneous or
otherwise ineligible data MUST return MCP `InvalidParams`. The error SHOULD
identify the incompatible shape and guide the caller to `responseFormat=Json`,
`responseFormat=Auto` or a lower detail level. An explicit override MUST NOT
silently return another format because doing so hides a caller error and makes
format-sensitive integrations unreliable.

## Shared Table Eligibility

A result is table-eligible only when all of these conditions hold:

- it is a bounded list result, not a scalar, object graph or multi-table
  envelope;
- every row uses the same explicit columns in a deterministic order;
- every cell is null, a string, a Boolean or an integral numeric value;
- `DateTime` and `DateTimeOffset` values are normalised to ISO 8601 strings
  before table eligibility is assessed;
- it has no nested object, list, tuple or map cell;
- it does not require field omission, flattening or value coercion;
- the page metadata can be represented separately from the row payload.

Empty eligible lists retain their stable column declaration. Column order MUST
come from the response contract, not reflection order or the first row
encountered at runtime.

## CSV Profile

CSV output MUST:

- use an explicit stable header row;
- follow RFC 4180 quoting and escaping;
- preserve Unicode text;
- quote every string data cell, while leaving Boolean and integral numeric
  cells unquoted, so the profile decoder preserves scalar types;
- represent null as an unquoted empty field and an empty string as `""`, so
  the profile decoder preserves their distinction;
- contain only the current bounded page;
- use CRLF between records and no trailing record terminator;
- use the same column order for the same contract version.

The MCP result text contains compact paging metadata followed by the RFC 4180
table content. The metadata is not a table row. Consumers which extract the
table MUST treat only the table segment as CSV.

In `Auto`, CSV is used only for eligible `Summary` lists. It is designed for
the smallest routine discovery responses.

## TOON Bounded Profile

The supported profile is `tia-toon-table/1`, based on TOON v4.1. It is
deliberately narrower than the complete TOON specification:

- no more than 200 rows;
- no more than 32 columns;
- an explicit, stable column declaration;
- scalar cells limited to null, string, Boolean and integral numeric values;
- `DateTime` and `DateTimeOffset` values normalised to ISO 8601 strings;
- one homogeneous table per result;
- no nested or mixed-shape rows;
- a comma delimiter, UTF-8 text and LF inside the TOON payload;
- two-space indentation and strict TOON v4.1 quoting and escaping;
- `items: []` for an empty table;
- report the stable column declaration in result metadata when the TOON array
  is empty, because canonical TOON empty-array syntax has no field list;
- no trailing newline.

If any bound or structural rule is not met, `Auto` uses compact JSON and an
explicit `Toon` request returns `InvalidParams`.

In `Auto`, TOON is used for eligible `Standard` lists where the additional
fields remain a homogeneous table. The profile is intended to reduce
syntactic noise while making repeated record structure conspicuous to a
model. Improved model structural accuracy is a design goal to be measured
with this project's evaluation traces. It is not a claim that TOON is
universally more accurate than JSON for every model, prompt or data shape.
Compact JSON remains the authoritative lossless representation.

Changing the bounds, cell types or structural features requires a new profile
revision and a compatibility assessment.

## Compact JSON

Compact JSON MUST:

- preserve the complete bounded response shape;
- retain nested structures and compatibility fields;
- use stable property names from the public contract;
- avoid pretty-printing and redundant whitespace;
- omit optional null or default fields only where the response contract
  already permits omission;
- retain explicit paging and truncation information.

`Full` detail always uses compact JSON. This prevents a richer response from
appearing tabular after nested or optional data has been discarded.

## Result Metadata and Paging

Every formatted list result reports:

- `format`, the selected format;
- `returned`;
- `hasMore`;
- `nextCursor`.

The selected format reflects what was actually returned, not merely what was
requested. The identifiers are `csv`, `toon-v4.1` and `json`.
An empty `toon-v4.1` result also reports `columns` in stable contract order
because its canonical `items: []` payload cannot carry a TOON field list.

Full row data appears once in model-facing text content. It MUST NOT be
duplicated in a second structured copy merely to carry format or paging
metadata. Paging remains authoritative: `returned` describes the current
page, `hasMore` states whether another page exists, and `nextCursor` is opaque.

## Compatibility and Change Control

The format policy is part of the public MCP result contract. Changes to the
default `Auto` selection, explicit override behaviour, format identifiers,
TOON profile, CSV null handling or metadata fields require a changelog entry
and compatibility review under [Changelog Policy](changelog-policy.md).

Introducing automatic CSV and TOON rendering changes the model-facing payload
for callers which previously assumed JSON. It is an intentional breaking
pre-1.0 contract change. Format-sensitive integrations MUST set
`responseFormat=Json` while migrating, rather than infer a payload format from
the requested detail level.

Tool descriptions and server instructions SHOULD tell an LLM to:

1. keep `Auto` for normal inspection;
2. request only the detail and page size needed;
3. use `Json` for full, nested or integration-sensitive results;
4. follow `nextCursor` only when more data is needed;
5. correct an `InvalidParams` format request rather than retrying it
   unchanged.

## References

- [TOON Specification v4.1](https://github.com/toon-format/spec/blob/main/SPEC.md)
- [RFC 4180 CSV](https://www.rfc-editor.org/rfc/rfc4180)
- [MCP tool result content](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
- [Notation Matters: A Benchmark Study of Token-Optimized Formats in Agentic AI Systems](https://arxiv.org/abs/2605.29676)

The TOON project reports favourable retrieval results for some models and data
shapes, while independent agentic evaluation reports accuracy and multi-turn
failure risks for broad format substitution. That mixed evidence is why this
repository uses a bounded, deterministic TOON profile, keeps CSV limited to
flat summaries, retains compact JSON as the universal lossless format and
requires project-specific model evaluations.
