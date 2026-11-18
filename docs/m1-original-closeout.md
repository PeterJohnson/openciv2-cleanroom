# Original-game file acceptance procedure

Original-game observations are independent evidence and must be repeated against
the exact C# candidate before claiming current file interoperability. Authored
fixtures, headless CI and byte-round-trip tests alone do not establish that the
original executable accepts a newly exported file.

1. Build the root solution and record revision, working-tree changes, SDK/runtime,
   input hashes and invoked helper artifacts. Use an isolated original-game data
   copy and new output filenames; never overwrite the source corpus.
2. Run `OpenCiv2.MapRoundTrip` and `OpenCiv2.SaveRoundTrip` from `tests/` on the fixed
   admitted maps/current-format saves. Compare every output byte and source hash.
   Record unsupported versions explicitly rather than silently excluding them.
3. Run `OpenCiv2.LiveSave HOME INPUT.SAV NEW_OUTPUT.SAV --gold CIV VALUE` with selected
   classic rules/labels and an explicit small treasury edit. This is a bounded
   core load/edit/save helper, not a turn engine or full post-load workflow.
4. Load the new file through the original program's normal UI. Record city,
   civilization, treasury and year. Advance one original turn, save to a new path,
   then decode/re-encode that resave with the current C# helpers. Reopen a second
   live export in the original UI and record the outcome.
5. Test a newly re-encoded raw map with the original Start on Premade World path;
   preserve resource/starting-location choices and record the reached setup stage.
6. Retain executable identity, exact text-source identities, captures, process
   output, observation sequence and artifact hashes. A failed or absent external
   run remains a gap; do not replace it with implementation agreement.

Recorded original-program observations include current-save loading with an edited
treasury, normal turn advancement/resaving and premade-map difficulty selection.
They do not establish scenario, legacy-save, multiplayer, peer-session or complete
map-gameplay acceptance. The original supplies the turn gameplay; the helper does
not. See the [text observations](m1-original-text-observations.md),
[live-save procedure](m1-original-live-save.md) and
[premade observations](m1-original-premade-observations.md) for the bounded UI paths.

Current C# automated evidence and outstanding platform work are recorded in
[validation](validation/README.md); full compatibility retains its later milestones.
