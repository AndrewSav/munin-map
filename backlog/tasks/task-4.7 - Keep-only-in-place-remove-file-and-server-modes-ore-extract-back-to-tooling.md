---
id: TASK-4.7
title: 'Keep only in-place: remove file and server modes, ore-extract back to tooling'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-24T04:38:02Z'
updated_date: '2026-11-24T05:02:16Z'
labels:
  - plugin
  - web
  - docs
  - ci
dependencies: []
parent_task_id: TASK-4
type: feature
ordinal: 37000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
TASK-4 built three ways to predict ore (TASK-4.3 to 4.6). In-place (TASK-4.6) turned out as hoped: about 99% of deposits right, the same as an extract, usually in under a minute inside the game server, with nothing to set up. That leaves file mode (an extract made by hand) and server mode (Munin running ore-extract on the server, half an hour to an hour and a half, up to 3.5 GB) with no practical use, while they cost a second release asset, an embedded library in MuninMap.dll, a child process, extra Server details rows and a setting with modes. So they go, and predicted ore becomes one feature that is on or off. Building them was worthwhile: what was learned stays, the code stays in git history, and it is what let in-place be built and measured with confidence.

ore-extract itself stays, as fork tooling: it writes the same CSV format as predicted-ore-inplace.csv, it is the reference in-place is measured against (compare.py), and it pulls the dedicated server and BepInEx by itself, which makes it handy for running tests on another machine. The game-update routine (DRAFT-10) relies on it.

Ref: TASK-4.3 (shipping ore-extract), TASK-4.4 (file mode), TASK-4.5 (server mode, ore-extract/core merged into MuninMap.dll through src/Directory.Build.targets), TASK-4.6 (in-place), decision-2.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 predict_ore is a true/false setting like the others, default false; true means in-place prediction. A config still holding an old value (off, file, server, in-place) is read sensibly, without an error
- [x] #2 File and server modes are gone from Munin: no reading of a hand-made predicted-ore.csv, no server extract, no data/ore-extract.json, and ore-extract's library is no longer built into or merged into MuninMap.dll
- [x] #3 ore-extract (the CLI, its shared library and its game-server plugin) lives under patches-tools/ and still builds and runs there, with its README updated for its use as tooling
- [x] #4 The release workflow no longer builds or attaches the ore-extract assets, and SHA256SUMS lists only what is released
- [x] #5 Server details show only the rows that still apply (no prediction mode, prediction version or Update ETA rows, no notes for the removed modes)
- [x] #6 docs/ADVANCED.md and PATCHES.md's single TASK-4 entry describe predicted ore as one on/off feature, with nothing left about file or server mode; README and other docs that pointed at ore-extract as a product say so no longer
- [x] #7 In-place works as before after the change (a whole world worked out, shown on the map), and the page shows no errors
- [x] #8 In-place's result file is renamed from predicted-ore-inplace.csv to predicted-ore.csv; an old file under either name (a hand-made extract, a server extract, an earlier in-place run) never breaks it: one it can't resume from is replaced by its first run
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Move ore-extract/ (README, build.sh, cli, core, plugin, .gitignore) into patches-tools/ore-extract/ beside the spike scripts with git mv; fix the plugin's path to ../libs and the comments that say it ships; README rewritten for its use as tooling (reference extracts, compare.py, remote test runs).
2. Munin: delete Patches/ServerExtract.cs; src/Directory.Build.targets keeps only the physics module reference (no ore-extract/core reference or ILRepack input); MuninMap.OnDestroy back to upstream's line.
3. PredictedOre: predict_ore becomes bool (default false); an old string value (off/file/server/in-place) is read from the config file and turned into false/true, then saved as a bool; the file it reads is predicted-ore.csv, written by in-place; no data/ore-extract.json; no prediction-version handling beyond refusing another world's file.
4. InPlaceOre: writes predicted-ore.csv; at start an old predicted-ore-inplace.csv is moved to the new name (keeps resume) unless a current in-place file is already there; a file it can't resume from is replaced by its run.
5. Page (web/js/patches/ore-layer.js): no server-mode polling or rows, no Prediction mode or Prediction version rows; Prediction update while in-place runs, Verified on row and note stay.
6. Release workflow: no ore-extract build, version check or assets; SHA256SUMS lists MuninMap.zip only.
7. Docs: ADVANCED.md (settings row, files table, API table, Predicted ore section as one on/off feature, no method comparison), PATCHES.md's TASK-4 entry; any doc pointing at ore-extract as a product.
8. Checks: Munin builds; ore-extract builds from its new place (build.sh); on a running server: an old 'predict_ore = in-place' config read as true, an old predicted-ore-inplace.csv picked up, in-place works out the world and the map shows it; data/ore-extract.json gone; the user checks the page.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-11-24, with the user: the result file is renamed to predicted-ore.csv (one mode now); DRAFT-9 (priority for server mode) archived as moot.

Done and checked:
- ore-extract (README, build.sh, cli, core, plugin) moved with git mv into patches-tools/ore-extract/ beside the spike scripts and compare.py; the plugin's libs path and the comments that said it ships updated; README rewritten as tooling. build.sh builds both platforms from there; 'ore-extract version' runs (0.0.0-local). Its core keeps its net48 target and hosted mode (unused now, said so in a comment).
- Munin: Patches/ServerExtract.cs deleted; src/Directory.Build.targets keeps only the physics module reference; MuninMap.OnDestroy back to upstream's exact line (one fork edit fewer in upstream files). MuninMap.dll 88 KB smaller; no ore-extract code left in it.
- predict_ore is a bool (default false). An old mode value is taken out of BepInEx's orphaned entries before binding (no 'could not be parsed' warning) and saved as a bool: off -> false, any mode -> true. Checked: a config with predict_ore = in-place became predict_ore = true (Boolean, new description), logged once.
- The result file is predicted-ore.csv. At start an in-place file under the old name takes the new name unless an in-place file is already there; a file in-place didn't write is replaced. Checked: an old server extract as predicted-ore.csv plus a complete predicted-ore-inplace.csv -> renamed, resumed complete; an old extract alone as predicted-ore.csv -> shown until in-place's full run replaced it.
- data/ore-extract.json gone (404); predicted-ore.json without mode, madeFor or mismatch; the page config sends predict_ore: true.
- Page: no server-mode polling, no Prediction mode, Prediction version or Update ETA rows, no version banner; Prediction update while a run goes, Verified on row and note stay. Checked by the user: Server details show only Server version and Predicted ore when complete; no console errors.
- Release workflow: MuninMap.zip and SHA256SUMS only, as before TASK-4.3 (YAML parses).
- Docs: ADVANCED.md settings, files and API rows; Predicted ore as one feature (no method sections or comparison), with the mods line TASK-4 #6 asks for, and the non-WebGL map described accurately (real ore in the Trees & rocks layer, no Ore row); PATCHES.md's TASK-4 entry rewritten as one on/off feature.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Predicted ore is now one feature, predict_ore = true/false (default false), worked out in-place: file and server modes are removed. Munin no longer carries ore-extract's library (MuninMap.dll 88 KB smaller), starts no child process and serves no data/ore-extract.json; Server details lost the mode, version and ETA rows and the version banner; the release workflow is back to MuninMap.zip and SHA256SUMS. Old settings (off/file/server/in-place) are read as false/true and saved as a bool; in-place's file is now predicted-ore.csv, and an old file under either name is taken over or replaced. ore-extract stays as fork tooling in patches-tools/ore-extract/ (same CSV format as predicted-ore.csv, the reference compare.py measures in-place against, handy for remote tests). ADVANCED.md and PATCHES.md describe one on/off feature. Checked on a running server and by the user in the browser.
<!-- SECTION:FINAL_SUMMARY:END -->
