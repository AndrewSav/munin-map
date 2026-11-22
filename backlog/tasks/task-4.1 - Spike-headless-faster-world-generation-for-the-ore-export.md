---
id: TASK-4.1
title: 'Spike: headless, faster world generation for the ore export'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-22T03:09:49Z'
updated_date: '2026-11-22T19:39:10Z'
labels:
  - tooling
  - plugin
dependencies: []
documentation:
  - >-
    backlog/docs/doc-2 -
    Ore-placement-how-the-game-places-vegetation-in-a-zone.md
parent_task_id: TASK-4
type: spike
ordinal: 30000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
TASK-4's copy-based method reads every ore deposit from a fully generated copy of the world. Done with Upgrade World (TASK-27) it took about 3 hours (about 450 zones a minute) on one dedicated server, started from an admin client's console, which few admins would do per world. doc-2 ("Generating a copy faster") sets out ideas that could make it practical: a client-free generator (a small plugin on a dedicated server started on a copy, without networking, that generates, exports the deposits and exits), skipping work ore does not need (ocean zones, dungeon interiors, vegetation after the last ore entry), not holding back for gameplay, and several processes on disjoint shares of the zones whose deposit lists are joined; or the same generator run slowly as a low-priority second process next to the live server. Unity only allows the work on its main thread, so one process is sequential. Nothing is measured yet: the key unknown is how much of today's speed is waiting versus real work. This spike measures it and recommends whether the copy method is worth building.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A client-free generator runs on a dedicated server started on a copy of a world: it generates the ungenerated zones in a loop, writes the ore deposits it created (prefab, position, zone) to a file, and exits; no game client, console or networking involved
- [x] #2 Zones per minute and memory are measured for the plain loop (as Upgrade World does it) and for each speed-up separately: back-to-back zones, lifted frame-rate cap, terrain requested ahead, no autosaves, land zones only, no dungeon interiors, vegetation stopped after the last ore entry
- [x] #3 The exported deposits match the TASK-27 snapshot for the same zones, apart from the differences the seed does not decide (near randomly rotated locations, unique sites)
- [x] #4 Two or more processes on disjoint shares of the zones are measured together: total zones per minute, memory per process, and the joined deposit list
- [x] #5 A low-priority generator run next to a running test server is measured for its effect on that server (fps, longest frame)
- [x] #6 Results and a recommendation (build the copy method or not, and in which form) are recorded in TASK-4 and doc-2
- [x] #7 Spike code is not merged into the product; anything kept as tooling lives in patches-tools
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. patches-tools/zonegen: a separate BepInEx plugin (not Munin, not shipped), built against munin-map/libs with Krafs.Publicizer like Munin. Off unless its config enables it.
2. On a dedicated server, once the world is loaded: build the zone list (ungenerated zones within the world radius; options: land only, share k of N, limit), then generate in a coroutine with SpawnZone(Ghost), retrying a refused zone up to a long limit. Options per run: zones per frame or a ms budget, frame-rate cap lifted, terrain requested ahead, autosaves off, no dungeon interiors, vegetation list cut after the last ore entry.
3. Log progress every 30 s (zones, zones/min, memory); at the end write the ore deposits in the run's zones (prefab, position, zone) to a CSV, plus a summary, then quit. An export-only mode writes the deposits of an already generated world (the TASK-27 snapshot) for comparison.
4. Test on the local test server's image with a fresh live copy (most zones ungenerated), Munin off: plain loop first (Upgrade World-like), then each speed-up alone, then combined; a limited zone count per run to keep runs short.
5. Compare the exported deposits with the snapshot's for the same zones.
6. Parallel: extra containers from the same image, each with its own copy of the world and config, disjoint shares; measure together.
7. Low priority: a generator container at low CPU priority next to the running test server; measure the server's fps and longest frame.
8. Record results and a recommendation in TASK-4 and doc-2.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Generator built (patches-tools/zonegen: ZoneGen.cs plugin switched on by ZONEGEN_* env vars; run.ps1 starts it in its own container from the test image with its own /config copy of the live world in data/zonegen/base and its own plugins folder, sharing only the installed server volume; bench.ps1 runs a series). No client: a smoke test generated a few hundred zones, wrote their deposits and quit.
Bench 1 (the 2,000 nearest ungenerated zones): the first non-plain loop let a refused zone go to the back and tried every pending zone per frame, so the heightmap builder (16 ready) was asked for hundreds at once and dropped finished heightmaps: slow, failing, timing-dependent runs. Fixed with a window of at most 8 zones in progress.

Window fix confirmed (bench 2, the same 2,000 zones, each setting twice), speed against the plain Upgrade World-like loop: zones back to back within a 30 ms frame budget 5.2x, plus the frame-rate cap lifted 9.3x, every speed-up 10x; no saves, no dungeon interiors and the vegetation cut each made little difference alone (1-1.14x); skipping ocean zones was slower, not faster. No failures outside plain (whose 10 s limit still skips slow-loading zones). Memory about 2 GB per process.
Snapshot comparison (export mode on the TASK-27 world, whose deposits match Munin's own counts exactly). Same 2,000 zones: plain 99.9% (1 silver vein differs); windowed runs about 99% (some obsidian differs): with several zones in flight the generation order changes, so neighbouring zones' objects near zone edges block differently. Live generates in players' travel order, so live differs from any copy in the same way.
Parallel (every speed-up): 2 processes 1.85x one, 4 processes 3.4x (about 10% per-process loss at 4).
Whole world (4 processes, every speed-up, every ungenerated zone of the live copy): all generated, no failures, about 7.4 minutes of generation (9.2 with start-up), per-process rate varying by about 2x over the run (heavy regions slower), about 3 GB per process at the peak. Upgrade World took 3 hours plus a 17-minute cleanup: about 24x faster, no client. Deposits matched the snapshot 99.3% in the same zones: copper 100.0%, tin 99.9%, scrap 99.7%, silver 98.7%, obsidian 98.2%.

Low priority (the test server on the generated world with Munin and perf_report, and a low-priority generator sharing its CPUs): the server's fps was the same with and without the generator, and its longest sweep frames were in the same range (the window without it came straight after a restart, so its sweeps were slower anyway); the generator kept nearly its full speed, because the server's main thread needs only part of one core. A session exit left the first test script running and a restarted one replaced its generator midway; both stopped and the windows were read from the server log by time instead. perf_report off again afterwards. Results and recommendation written to doc-2 ('Measured') and TASK-4 notes.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
A client-free world generator works and makes the generated-copy method practical. patches-tools/zonegen holds a small BepInEx plugin (switched on by environment variables: generate the ungenerated zones with SpawnZone(Ghost) in a loop, export the ore deposits to CSV, quit), run.ps1 (one generator container on its own copy of the world, from the test server's image) and bench.ps1. About 10x Upgrade World's loop per process with a small in-progress window, zones back to back and the frame-rate cap lifted; 4 processes generated the whole world in about 7.4 minutes (about 3 GB each, no failures) against Upgrade World's 3 hours; at low priority next to a busy server it didn't hurt the server. Deposits match the Upgrade World snapshot 99.3% (copper 100%, obsidian 98.2%); the misses are the parts the seed doesn't decide. Recommendation in TASK-4: build the copy method (headless export tool plus Munin loading the file). Spike code stays in patches-tools; nothing in the product.
<!-- SECTION:FINAL_SUMMARY:END -->
