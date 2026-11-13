# Classic built-in gameplay defaults

These six complete first-wave components transcribe the repository's classic
MGE 5.4.0f, patch 3 specifications. Start at [gameplay.json](gameplay.json).
They contain no original text-file payloads and require no original installation.
They are not the text-backed `rules.json` tables or a complete playable pack.

The [format and consumer bindings](../../../docs/content/gameplay-format.md)
define each field, normalization and customization limits. The
[provenance ledger](../../../docs/content/gameplay-defaults.json) pins the source
sections, files and semantic fingerprint. Repository licensing applies to this
owned transcription; no original-game execution is claimed.

From the repository root:

```sh
python tools/check_gameplay.py
```

Changing these operands requires a separately identified bundle in explicit
custom mode and updated descriptor hashes. Original peers cannot acquire custom
built-in rules from classic TXT or save files. Runtime loading and gameplay
integration are not implemented yet.
