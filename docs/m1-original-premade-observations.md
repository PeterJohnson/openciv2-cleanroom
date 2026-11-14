# Original premade-map radius observation

On 2026-11-14, the supplied original Civilization II Multiplayer Gold executable
ran on Windows in the isolated `build/original-setup-runtime` directory. Normal
UI input opened an independently authored raw map and saved the resulting game.
No executable code, process-memory reads, injected calls or historical harnesses
were used. The [retained report](conformance/reports/original-premade-windows.json)
identifies the executable, data, authored map, output and 15 UI captures by hash.
Binary inputs, output and captures remain local under ignored `build/` paths.

## Input and procedure

The authored map follows F06's raw-map layout: a 14-byte little-endian header
with doubled width 100, height 80, area 4,000, cylindrical topology, seed 24 and
coarse dimensions 25 by 20; 21 X and 21 Y starting hints all equal to -1; then
4,000 identical six-byte tiles `02 00 ff 00 00 f0`. These represent grassland,
no features, all city-radius bits set, region/revelation zero, and Unowned with
fertility zero. There is no trailing data. This deliberately probes retained
metadata without needing an existing city or a game-derived input map.

Through Start on Premade World, the original opened that file, displayed the
resource-randomization prompt and accepted No (keep the seed). It did not display
a starting-location prompt. Subsequent choices were Warlord, seven civilizations,
Roving Bands, Standard Rules, Male, Romans, Caesar and Classical Forum. The game
reached the Roman map at 4000 B.C. It was saved immediately, before moving a
human unit, founding a city or ending a turn. The original displayed its saved
game confirmation. The comparison session was then closed normally.

## Result and scope

The original save contains 4,000 tiles, seven unit slots and **zero city slots**.
Every tile's full radius byte is **zero**, including all low five bits that were
set in the input. Width, height, topology and seed remain 100, 80, 0 and 24.
The C++ stored decoder's offsets/bytes were checked against the actual save
bytes, and the existing C++ save roundtrip driver reproduced all 69,803 bytes
exactly, including its ordered auxiliary comparisons. Stored validation passed
its available checks; rule-dependent and gameplay validation remain incomplete.

This observation resolves the F05 section 3 phrase “clear city-radius claims”
to clear the entire metadata byte, including the low five retained bits. The
capture observes the complete premade-entry path, not its private intermediate
state. Attribution to the preparation step also uses W02's explicit retained-map
contract, which preserves radius metadata. The absence of cities avoids later
city-radius claims in this run. W01's ordinary claimant setter retains its own
narrower contract.

The run does not establish W02 terrain/fertility generation, random-order
compatibility, every setup branch, hint acceptance, arbitrary radius patterns,
flat topology or interrupted-read behavior. Those remain separate contracts and
tests. Runtime rule/text files retain the earlier text observation's declared
name probes; their exact hashes are included rather than claiming pristine data.
