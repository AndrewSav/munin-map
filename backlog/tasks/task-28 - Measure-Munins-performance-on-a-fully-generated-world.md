---
id: TASK-28
title: Measure Munin's performance on a fully generated world
status: Done
assignee:
  - '@claude'
created_date: '2026-11-21T21:02:26Z'
updated_date: '2026-11-21T21:46:39Z'
labels:
  - plugin
dependencies: []
type: spike
ordinal: 27000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
TASK-27 made a fully generated copy of the world on the test server: about 4.1 million objects, against about 270 thousand in the live copy. Munin walks every object on each world sweep, draws trees and rocks into the tiles, keeps a vegetation cache and builds marker lists, so a world explored much further than live today may cost the server far more. Live will get there as players explore, and TASK-26 and TASK-4 need the full world on the test server anyway. Measure what Munin costs on it, compared with the live copy, and decide whether anything needs fixing or whether a partial generation is the better test world.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Munin runs on the generated world on the test server with perf_report on, through its first full sweep and its tile drawing
- [x] #2 Recorded, against the live copy: sweep time and objects, longest frame on the game thread, game-thread ms per second by job (perf_report), server fps, memory, start-up time, and the size and response time of the main data endpoints
- [x] #3 The map works on the generated world in a browser: tiles, markers and vegetation layers load
- [x] #4 A conclusion is recorded: acceptable as is, or which parts need work (as proposed follow-up tasks, not created without approval), or a partial generation instead
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
- Start-up: deploy -NoBuild ready in 56 s (live copy about 42 s); world load 4.1 M ZDOs about 45 s.
- Sweep #1 (cache build): 191.8 s, longest frame 41.3 ms (GC), 27 fps. Steady sweeps #2-#4: 29-31 s each (live copy 2.2 s), longest frame 5.0-5.9 ms, 0 GC, 28 fps (live 30).
- perf_report steady 5 min: game thread 19.8 ms/s, sweep 19.6 ms/s, longest step 5.9 ms (live copy 3.9 ms/s, longest 40 ms). First 5 min incl. sweep #1 and model export: 40.5 ms/s.
- Memory: 5.3 GB plateau after sweep #1 (game alone on this world about 3 GB during generation); CPU idles about 15%.
- Endpoints via 127.0.0.1: players 80 ms, markers.json 48 ms / 1.06 MB, activity 4 ms, stats 570 ms cold then 5 ms. (localhost costs about 2 s per request on this machine: IPv6 first.)
- Tiles: all zoom 0-7 already on disk (reveal_all draws everything). The first sweep after a start only sets a baseline (WorldSweep.cs:294), so vegetation that appeared while Munin was off (all the generated zones) was never redrawn: veg tiles were stale.

Deleting only tiles/veg drew nothing: a veg overlay is made by the same job as its map tile, and Enqueue(onlyIfMissing) skips tiles whose map image exists. The map stayed usable: four undrawn close tiles in far corners were drawn and served in 0.26-0.53 s during the redraw (browser requests jump the queue). During the redraw garbage collections rose to about 40 per 2 min (from 0-1) and some landed in Munin's sweep steps on the game thread: longest step 60-76 ms per 5-minute report (184 ms once, sweep #1 after the restart); after it, 7 per 2 min. User checked the map in a browser: tiles, markers and layers load. perf_report switched off again in the test config.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Munin on the fully generated test world (4.36 M objects, 19x the live copy) is acceptable as is; no partial generation needed. Steady state: sweeps 29-31 s (live 2.2 s) in steps of at most about 6 ms, 19.8 ms/s of the game thread (live 3.9 ms/s), 28 fps (live 30), 5.3 GB memory (about 2 GB of it Munin), data endpoints in milliseconds (markers.json 1.06 MB in 48 ms). Costs that grow with the world: the first sweep after each restart (about 3 min, one 41 ms frame), and a full tile redraw (about 20 min on one render thread, map usable throughout as requested tiles jump the queue, but garbage collection during it gives occasional 60-180 ms hitches). Two behaviours seen, both Munin's existing design: vegetation that changes while Munin is not running is not redrawn (the first sweep after a start only sets a baseline), and the fog rebuild at start counts every generated zone as visited, so the generated test world has almost no fog. No follow-up tasks proposed; the GC hitches during a full redraw are the one thing that could be worth a look if it shows on live.
<!-- SECTION:FINAL_SUMMARY:END -->
