# General interface static review

13 November 2026. An independent Terra subagent reviewed the completed
`0.1.0-alpha.3-dev.2` implementation and connected code against baseline
`3af62ba`, then re-read the source corrections. This was a source-only review.

## Findings and resolutions

| Finding | Source correction |
| --- | --- |
| Returned API references included a descriptive type which input conversion rejected. | The codec accepts the canonical reference with an optional exact matching runtime type, rejecting unknown and duplicate fields. |
| Dictionary key/value child references lost their binding replay. | Projection composes the original accessor with each nested pair key/value accessor and checks the original object identity when replay exists. |
| Projected key/value pairs could not be passed back unchanged. | Projection uses the canonical `{key,value}` shape accepted by the codec. |
| Native method/constructor exceptions obscured partial effects. | Invocation failures have sanitised inspect-before-retry guidance; completed calls distinguish output projection failure. |
| Event policies remained active after observed project selection changes. | Policies check captured Portal/project identity, stale observations are dropped, and context refresh expires and detaches stale subscriptions. Policies remain disabled if detachment fails. |
| Array/list/dictionary expected values compared by identity. | Bounded structural comparison now handles arrays, framework lists/dictionaries and pairs. Native objects retain native equality. |
| The first structural comparison budget rejected supported maximum arrays and reverse-ordered dictionaries. | A separately bounded candidate-comparison budget accounts for the root and worst-case flat dictionary search. Source regression cases cover 1,024-element arrays and reversed 1,024-entry dictionaries. |

The final targeted source re-review reported no remaining must-fix finding
among the reported cases. This does not establish runtime correctness of all
Siemens members.

## Deliberate event boundary

The reviewer identified that closing a project directly in TIA is observed
only when the worker next refreshes its native context. The handler avoids
re-entering Portal discovery from inside a synchronous native callback.
The guide explicitly requires unsubscribing callback policies before changing
project selection in the UI. Observed changes and worker-driven changes disable
the old policy. This remains a documented runtime limitation.

## Earlier navigation review

The dev.1 review also found missing ungrouped devices, name/index-based stale
native references and incorrect XML declaring-type parsing when parameter
names contained full stops. Shared lookup now includes ungrouped devices,
references validate native object identity, and the inventory parser removes
the parameter signature before splitting the declaring type. Those corrections
remain in this implementation.

## Evidence boundary

The reviewer ran no builds, tests, validators, MCP probes, TIA attachment or PLC
calls. The coordinating agent compiled all six workers, both brokers and the
V19 ReadWrite test project separately. Tests and runtime qualification remain
unrun. Native proxy equality, callback policies, modules/licences and partial
native effects require a suitable licensed runtime before release confidence
can be established.
