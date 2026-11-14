# M1-04 current original-file evidence

Status: **passed** for implementation candidate `56be096` on Windows, 2026-11-14.
The fixed corpus and original UI procedure were repeated after the cancellation
and Windows short-path portability fixes. Binary hashes are retained in the reports.

The [corpus report](conformance/reports/m1-original-corpus-windows.json) rechecks
the fixed seven maps and 21 current-format saves. Each input hash matches its
historical report, each source remains unchanged, and all 28 stored round trips
are byte-exact. Three legacy stored saves remain outside this gate.

The [execution report](conformance/reports/m1-original-execution-windows.json)
records the unmodified MGE 5.4.0f patch 3 executable, source/output/driver hashes,
text inputs, observed UI outcomes and the resave decode. The original ran from
an isolated copy under `build/original-text-runtime`.

- The current live driver loaded original `Ab_Auto.SAV` and exported a new current
  single-player save with civilization 5 gold changed from 50 to 888. Normal
  original loading showed Abe Lincoln of the Americans at 4000 B.C. and 888 gold.
- Skipping the active settler advanced one original turn to 3980 B.C.; the
  treasury remained 888. Normal Save Game wrote a new destination and confirmed
  that year. Our current stored codec decoded and re-encoded that resave exactly.
  The core driver independently decoded turn 2, year -3980 and civilization 5
  gold 888. The original had advanced the city count from three to five.
- Normal Start on Premade World accepted the freshly re-encoded `EUROPE.MP`.
  After preserving its resources and starting locations, the original reached
  difficulty selection without error. This map observation stops at setup.

The original supplies all turn gameplay. The cleanroom driver remains a bounded
F03/F06 wrapper/core-load/export tool, with no post-load admission or gameplay
claim. The isolated runtime retains an earlier English leader-name text probe;
the selected save uses Americans, and exact text-source hashes are recorded.
No executable patch, process-memory observation, reconstruction code or
source-derived harness was used. This evidence does not add scenario, legacy,
multiplayer, peer-session or map-gameplay acceptance claims. Original data and
screenshots remain ignored local artifacts; the report preserves their hashes.
