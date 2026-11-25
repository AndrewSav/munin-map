---
id: TASK-37
title: 'Game-update routine: what to check after a Valheim patch'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-23T21:29:36Z'
updated_date: '2026-11-25T13:46:40Z'
labels:
  - docs
dependencies: []
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Upstream has no routine for a new Valheim version, and the fork now copies game behaviour that a patch can change silently. Put one together, out of upstream's way: patches-tools/game-update/ with a README of the steps (and any scripts they need). Each fork feature that depends on game internals adds its own checks; it is run after every Valheim update, alongside rebuilding against the new game assemblies.

First entry, in-place ore prediction (TASK-4.6): it checks Unity's random generator and the zone terrain against the game at start, but the placement loop (ZoneSystem.PlaceVegetation), location placement (PlaceLocations, SpawnLocation, RandomSpawn, RandomObject), the block and ground checks (IsBlocked, GetGroundData, GetTerrainDelta), Heightmap.GetBiome and the components that draw random numbers in Awake (LodFadeInOut, MineRock5, Pickable, ResourceRoot, ZSFX, Floating) are copied from 1.0.21 and not checked at run time. Steps:
1. Diff those parts of the decompiled game code against the previous version.
2. An ore-extract reference for a world, then in-place in all-zones mode (MUNIN_INPLACE_ALL=1), compared per ore with patches-tools/ore-extract/compare.py (about 30 min).
3. If it holds, raise in-place's 'checked against' game version (TASK-4.6), which clears the note in Server details.

Other candidates to list: ore-extract's generation plugin, map pins (doc-1), anything else reading game internals.

Ref: TASK-4.6, doc-2.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 patches-tools/game-update/README.md gives the steps to run after every Valheim patch, in order, each with how to tell it passed
- [x] #2 It covers loading: rebuild against the new game assemblies, compare the game Managed DLL list with the previous version against what ILRepack leaves out (the System.Memory case), and read the BepInEx log on Linux and Windows for load, Harmony and type errors
- [x] #3 It covers game data Munin parses by hand (TerrainComp blob, ZDO data and any other format) and the game code fork features copy, with the list of classes and functions to diff
- [x] #4 A script decompiles the previous and new assembly_valheim and assembly_utils and reports which of the listed classes and functions changed
- [x] #5 It covers in-place ore: the measured comparison (fresh world, ore-extract reference, compare.py) and raising CheckedAgainst, with the accuracy bar that counts as holding
- [x] #6 The routine has been run for Valheim 1.0.30 and its results recorded in this task; CheckedAgainst is raised to 1.0.30 if in-place holds
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Write patches-tools/game-update/README.md: the steps after a patch, each with its pass check.
2. diff-game.ps1 + watch.txt: plugin references missing from the new game, DLL list, watched classes/members, other changed classes.
3. update.ps1 keeps the old game assemblies (data/managed/<version>) before updating.
4. Run it for 1.0.30, including the in-place measurement; raise CheckedAgainst if it holds.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Run for Valheim 1.0.30 (2026-11-26):
- Step 2 (diff-game.ps1, 1.0.21 publicized copy vs 1.0.30): MuninMap.dll of patches-3.4.0-10 is missing System.Memory 4.0.1.1 and Unsafe 5.0.0.0 (TASK-36); the fixed build is missing nothing. Watched: ZoneSystem.SpawnLocation changed only in how the prefab is instantiated (SoftReference API); Heightmap and ZSFX show only the publicizer's event fields; TerrainComp.Save/Load: format v2 appends a double after the paint grid, which TerrainPatches.Decode never reaches. Everything else in watch.txt same.
- Step 3 (Linux test server) and step 4 (Windows dedicated server): clean, see TASK-36.
- Step 5: fresh world made on 1.0.30 (Windows server, seed EBq60vlKjk), in-place vs ore-extract at 0.5 m: copper 99.9, muddy scrap 99.5, silver 96.3, tin 100.0, obsidian 98.4, all 99.3% (1.0.21: 99.2%). Silver is ~500 deposits a world (0.2 points each) and moved a point between the two 1.0.21 worlds, so this holds. CheckedAgainst raised to 1.0.30; the test server sends checked 1.0.30 = game 1.0.30, so the Verified on note is gone.
- update.ps1 now keeps the old version's assemblies; kept by hand this time: data/managed/l-1.0.30 (all) and l-1.0.21 (assembly_valheim and assembly_utils, publicized, from src/obj).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Game-update routine in patches-tools/game-update: README with the steps after each Valheim patch and how each passes, diff-game.ps1 (plugin references the new game lacks, DLL list, watch.txt entries, other changed classes) and watch.txt; update.ps1 keeps the old game assemblies for the diff. Run for 1.0.30: in-place ore measured at 99.3% on a fresh 1.0.30 world, CheckedAgainst raised to 1.0.30 (Verified on note gone).
<!-- SECTION:FINAL_SUMMARY:END -->
