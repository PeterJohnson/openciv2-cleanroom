# Native content format examples

These JSON documents illustrate [version 1](../data-format.md). They are authored
format examples, not imported original data, a full stock ruleset or a playable
pack. Manifest hashes establish integrity, not gameplay completeness.
[Schema validation](../schemas/README.md) passes for the nine complete document
examples and intentionally rejects the rules and council fragments for their
missing coverage. The repository checker verifies those exact omissions; an
explicit file-validation command still rejects either fragment.

Native files describe game concepts. Classic addresses and slot mappings appear
only in the explicitly identified compatibility descriptor; file inventories and
conformance fixtures separately retain the external encodings they document.

| File | Scope |
| --- | --- |
| [content.json](content.json) | Component descriptors and scenario identities, with an explicit optional compatibility descriptor. The sample scenario requires an externally supplied world. |
| [rules.fragment.json](rules.fragment.json) | Named global rules, tribe definitions, actual hit points and a rational terrain defense multiplier. Required rule identities are deliberately incomplete, so a full pack loader **must reject** this pack. |
| [city-names.json](city-names.json) | Named civilizations with ordered city names and a shared fallback pool. Selection uses the live plural name, not a permanent tribe ID. |
| [city-preferences.json](city-preferences.json) | Unchanged human-city defense priority and repeated build preference; the list is not a set. |
| [text.en.json](text.en.json) | Typed messages, controls/defaults, named global-rule help, a capability fact and production advice. No classic filenames, numeric label indices or section bindings. |
| [menus.en.json](menus.en.json) | Ordered action/submenu/separator tree with label references. |
| [documents.en.json](documents.en.json) | Scenario-ID briefing association and four named credits identities. |
| [council.en.json](council.en.json) | Named era/advisor/opinion and explicit included/skipped/chance continuations. Only one of 102 required era/opinion combinations is present. |
| [encyclopedia.en.json](encyclopedia.en.json) | Rule-linked topic, explanatory concept and explicit scenario information-policy collection. |
| [events.json](events.json) | Once-only turn event; changeTreasury executes before showText despite reversed JSON member order. |
| [classic-compatibility.json](classic-compatibility.json) | Adapter metadata for custom rule IDs, indexed labels/help/facts and identity-less classic worlds. Its addresses are not the native content model. |

The unit has two whole movement points and **30 hit points**. With road multiplier
three, normalization produces movement six and HP thirty. The equivalent classic
HP input is three, not thirty; multiplying native HP by ten again is an error.
The terrain multiplier 2/2 means ×1 defense, with classic half-unit value two.
Shield-row costs remain rows because the live row-size rule determines their
shield value. No AI or map-generation rule changes follow from these names/units.

The event starts unconsumed, grants 50 to civilization slot 1 on turn 10, then
shows its text. S02 marks it consumed only after actions complete. A world
civilization slot differs from a tribe definition and is explicitly named as
such in the selector. These are specification-derived expectations, not results
from an implemented game.

The example command IDs are illustrative registrations, not implemented commands.
`scenario.river-valley` is declared in the manifest; its world is explicitly
external. Its briefing references that ID. Only the compatibility descriptor
associates `example.scn` with it; native briefing lookup does not compare filenames.

The council dialogue illustrates conversion of the earlier classic-control case:

| Inputs | Chapters / draws |
| --- | --- |
| Science opinion science-lag | Opening military, science, then military again; three chapters and no draw. The opening speaker was not marked as a responding advisor. The final caption is suppressed. |
| Another science opinion; draw 8 | Only the opening. The explicit skipped path goes through a chance step whose success also ends. There is no parser-acceptance state for a successful draw to overwrite. |
| Another science opinion; draw 5 | Only the opening; the probability 1/2 branch uses the ten-outcome gate and equality takes failure. |

The chance step on the skipped path documents a source decision whose two
continuations both end after conversion. Its continued presence makes the
conversion visible; X01's equivalence rules govern optimizations. Reordering
named dialogue steps must not change these outcomes. Classic `#`, R and initial
versus later `*` handling are conversion details in the format appendix, not
syntax that new content authors must use.

All example references resolve to declared identities. A playable base still
needs all rule/profile mappings, civilization-name coverage, required semantic
text/actions, complete council content and selected graphics/media. Each locale
supplies all five localized components; no per-label cross-language fallback.
