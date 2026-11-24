---
id: TASK-4.6
title: In-place ore prediction (predict_ore = in-place)
status: Done
assignee:
  - '@claude'
created_date: '2026-11-22T22:22:28Z'
updated_date: '2026-11-24T03:19:07Z'
labels:
  - plugin
  - web
  - docs
dependencies:
  - TASK-4.4
parent_task_id: TASK-4
type: feature
ordinal: 35000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
TASK-4's `predict_ore = in-place`: Munin works out the deposits itself, from the game's own placement data and random functions, with no extract, no extra process and no setup, so it should become the usual choice. doc-2 records the placement rules (1.0.21) and how valheim.tools reaches high accuracy by re-running them; what can't be reproduced exactly is blocking by objects placed earlier in the zone, the zone's terrain blend, and the facing of randomly rotated locations. The extract methods (TASK-4.4/4.5) give the reference to check it against. If its accuracy falls clearly short during the work, stop and re-evaluate with the user.

Ref: TASK-4 (approach in its notes), doc-2 (placement rules, Ghost mode, how valheim.tools does it), TASK-4.4 (display), TASK-27 snapshot, TASK-4.2 compare.py.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 With `predict_ore = in-place` and `reveal_all = true`, Munin works out the deposits of ungenerated zones itself and shows them as TASK-4.4 shows an extract, with no extra process
- [x] #2 Its accuracy per ore, against the TASK-27 snapshot or a whole-world extract of the same world, is measured and recorded before it ships; if it falls clearly short, the work stops for a decision with the user
- [x] #3 The game server shows no noticeable effect while it works (fps and longest frames against a settled baseline), and the time to cover a whole world is recorded
- [x] #4 It follows the running game's own data, so it needs no re-run after a game update beyond working out zones again
- [x] #5 docs/ADVANCED.md describes it, says why it is less exact than an extract (blocking by earlier objects, terrain, randomly rotated locations) with the measured accuracy, and completes the comparison of the methods
- [x] #6 PATCHES.md's single TASK-4 entry, which presents predicted ore as one feature with its modes, covers the in-place mode: what changes for players and admins, and how it differs from upstream
- [x] #7 Zones under the tiles a browser is asking for are worked out first, ahead of the fixed order; resume records which zones are done, so a restart neither redoes nor skips any
- [x] #8 In-place records the Valheim version it was verified on (1.0.21, raised in a Munin release once the game-update routine, DRAFT-10, has been run for a new version); on a server running another version (a game patch before a Munin release) it still works, and the page's Server details shows the verified version and notes that this server runs another
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Read the running game's data: ore entries and the entries before them (index, rules, colliders).
2. Placement loop in fork-owned files (src/Patches/InPlaceOre*.cs): per zone and entry, reseed as PlaceVegetation does, same draws in the same order, the same checks, heights and normals from the zone terrain as HeightmapBuilder builds it. Never generates a zone, never creates a ZDO.
3. Per-ore accuracy against a generated copy of a world (the TASK-27 snapshot) and ore-extract whole-world CSVs (compare.py); no ship decision until all five ores are measured.
4. Blocking: prototype both (a) ray tests against the prefabs' colliders and (b) collider-only copies in a private physics scene; compare accuracy, time and memory, then keep one.
5. Location parts as blockers, if measurements show they matter; measure loading the location prefabs (time, memory) first.
6. Output into TASK-4.4's display path (predict_ore = in-place, reveal_all only), in its own file, stored per world and game version.
7. Docs (ADVANCED.md: the mode, why it is less exact, measured accuracy, the methods comparison) and PATCHES.md's TASK-4 entry.
8. Last, with the user: the perf run (fps and longest frames against a settled baseline, whole-world time).
9. Priority (#7): zones under the tiles a browser requests go first; progress kept as a set of done zones.
10. Verified-on version (#8): a constant for the game version in-place was last verified on; Server details note when the server runs another.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Design, as built (2026-11-24):
- In-place re-runs ZoneSystem.PlaceVegetation for each zone not generated yet, on worker threads at the lowest priority (Thread.Priority; on Linux each worker sets nice 19 on itself with setpriority, since Mono ignores Thread.Priority there). The game thread only reads the game's data at the start and collects results.
- Copies of what Unity allows only on its main thread, checked against the game's own code at start; in-place refuses to run and says why if a check fails: Unity's random generator (Xorshift128; value, int and float Range, insideUnitCircle, many seeds; float Range only matched once every float step was rounded explicitly, since Mono keeps intermediates at higher precision) and the zone terrain per vertex with HeightmapBuilder.Build's formula (bit-exact on every vertex of several zones; only about 5% of a zone's vertices are ever needed).
- Blocking as ray tests against the prefabs' own colliders (box, sphere, capsule, mesh; convex meshes as their 2D outline from above). An accepted object blocks the rest of its own entry as well as later entries.
- Random draws made by components' Awake when the game instantiates a placed object (LodFadeInOut with a LODGroup when a main camera is over 20 m away, MineRock5, Pickable with a respawn time, ResourceRoot, ZSFX, Floating), counted per prefab: without them every tin deposit after the first in a zone was shifted (doc-2 corrected).
- Location parts: in Ghost mode a location's networked parts are instantiated before vegetation and block it (read in the game's code). Each location prefab a pending zone needs is loaded once with SoftReference.LoadAsync, as the game loads it, copied in pieces of about 1 ms a frame (synchronous loads stalled a frame by up to about 0.2 s), and released; about 100 MB of memory stays. The RandomSpawn/RandomObject rolls are reproduced from the zone seed; a slope-rotated location gets the facing the game's own slope test makes most likely; a randomly rotated one is left out. Unplaced candidate sites of unique locations are left out. A finished prediction loads no prefabs at start.
- Zones out to the world's edge at 10,500 m. Result in its own file, predicted-ore-inplace.csv, which PredictedOre shows like an extract; progress as a set of done zones, written every minute, within 3 s of viewed zones finishing, and when the server stops; a restart resumes.
- Priority: src/Patches/ViewedZones.cs, called from TileStore.Get (one marked line), raises each zone under a close-zoom tile; in-place works those zones out first, and their location prefabs too. DRAFT-9 can reuse the signal.
- Server details: Prediction mode row (from data/predicted-ore.json, so an open page follows a mode change), Prediction update while a run is going, Verified on and a note when the server runs another game version than InPlaceOre.CheckedAgainst (1.0.21), 'No prediction file found.' for a missing file. Server mode's rows were made the same way (no tooltips, update rows only while running).
- Test knobs (environment only): MUNIN_INPLACE_ALL, _BLOCK, _THREADS, _FRESH, _STOP_AFTER, _LOCATIONS, _MEASURE_LOCATIONS, _CHECKED, _ZONE_DELAY_MS, _DELAY_S, MUNIN_FRAME_LOG.
- Prototype (b), a private physics scene with collider-only copies, kept in patches-tools/inplace-prototypes/ (96.7% before the later fixes).

Accuracy (#2), compare.py at 0.5 m:
- Against the TASK-27 generated copy: copper 99.8%, muddy scrap 99.4%, silver 97.0%, tin 100.0%, obsidian 98.1%, all 99.2% (an extract of the same world: 99.3%). Steps on the way: 53% (missing Awake draws), 96.7%, 97.9% with location parts, 99.2% with unique locations left out and entries blocking themselves.
- A second world from a new random seed, against an extract from that seed: copper 99.9%, muddy scrap 99.3%, silver 98.0%, tin 100.0%, obsidian 98.0%, all 99.2%. The fixes carry over.
- Spot check by the user in an area generated by play: the extract, in-place and valheim.tools agreed on every tin deposit.

Verification:
- #1: in normal mode, generated zones skipped, the rest worked out in about 25 s on 2 threads, read by PredictedOre within a second, shown hollow on the map (checked by the user).
- #3: with the user playing, in-place ran for about 18 s: fps at least 29.6, longest frame 79 ms, against 164 ms and 270 ms for the player's own worst frames before and after; frames over 50 ms within the baseline's spread. No noticeable effect. Whole world in about 18-25 s.
- #4: a saved prediction made for another game version is discarded at start and worked out again by itself.
- #7: a far-off zone needing a location prefab, requested early in a fresh run, was in the file 3.3 s later, long before the normal order reached it; results identical with or without priority, after a stop and resume, and after a graceful restart.
- #8: Verified on row and note shown with the version overridden; the mode row followed a mode change without a page reload.
- Munin also ran on a Windows dedicated server for the first time (in-place worked there).
- The sidebar jump after a restart is upstream's statistics panel emptying and rebuilding (doc-3); the fork's rows no longer change height while a run goes.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
predict_ore = in-place: Munin works the ore deposits of zones not generated yet out itself, inside the game server, by re-running the game's vegetation placement (ZoneSystem.PlaceVegetation) with the running game's own data, on two lowest-priority worker threads. No extract, no second process, no setup. Copies of Unity's random generator and the zone terrain are checked against the game's code at start; blocking uses ray tests against the prefabs' colliders, placed objects block later candidates of their own and later entries, the random draws made by placed objects' Awake are counted, and locations' networked parts are reproduced (prefabs loaded asynchronously once, rolls from the zone seed, most likely facing for slope-rotated locations, random ones and unbuilt unique sites left out). A whole world takes about 18-25 s; zones a viewer zooms into go first (src/Patches/ViewedZones.cs, one marked line in TileStore.Get); progress is saved as a set of done zones and resumed. Accuracy 99.2% on two worlds (an extract: 99.3%); no noticeable effect on the game server with a player connected. Server details gained a Prediction mode row, update rows only while a prediction is being made (server mode too), and a Verified on row with a note when the game version differs from the one in-place was verified on (1.0.21). Docs: ADVANCED.md section and method comparison, PATCHES.md's TASK-4 entry; doc-2 corrected (instantiation draws, Ghost mode). Follow-ups: DRAFT-9 (server mode priority, reuses ViewedZones), DRAFT-10 (game-update routine), TASK-30 (web files into web/js/patches/), doc-3 (upstream's sidebar jump).
<!-- SECTION:FINAL_SUMMARY:END -->
