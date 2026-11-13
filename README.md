# OpenCiv2 Cleanroom

A new implementation of classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**, built from behavioral algorithm specifications.

The compatibility target preserves classic gameplay, AI decisions and map
generation, with original save/map/scenario interchange, TCP/IP multiplayer
with original-game peers, and hotseat play. Presentation and internal
representation may differ within the compatibility contract. Equivalent internal
work may be reordered while preserving behaviorally significant dependencies;
X01 and X02 define that convention and its conformance checks.

- [Algorithm specifications](docs/algorithms/README.md): subsystem catalog,
  ownership, writing conventions and source evidence.
- [Compatibility contract (X01)](docs/algorithms/X01-classic-compatibility-contract.md):
  required behavior, supported inputs and permitted differences.
- [Conformance (X02)](docs/algorithms/X02-whole-game-conformance.md): fixtures,
  comparison rules, integration cases and evidence gaps.

These documents define requirements; they do not establish completed
interoperability or whole-game validation.

[MIT license](LICENSE). The specification collection's
[source provenance](docs/algorithms/README.md#source-provenance) identifies the
external reference repositories.
