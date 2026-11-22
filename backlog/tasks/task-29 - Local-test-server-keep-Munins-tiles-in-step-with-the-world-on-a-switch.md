---
id: TASK-29
title: 'Local test server: keep Munin''s tiles in step with the world on a switch'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-22T01:45:30Z'
updated_date: '2026-11-22T01:55:19Z'
labels:
  - tooling
dependencies: []
type: chore
ordinal: 28000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Munin draws the map into tiles, with trees, rocks and changed ground drawn in at zoom 5-7. The first sweep after each start only records a baseline (WorldSweep.cs:294), so tiles are not redrawn for vegetation that was already there when Munin started. When the test server switches between a copy of live and the fully generated snapshot (TASK-27), the tiles from the other world stay: after `pull-save.ps1` the live copy can show forests in zones it doesn't have, and after switching to the generated world its new zones look bare (TASK-28 had to delete tiles to force a redraw). A full redraw of the generated world takes about 20 minutes on one render thread. Zoom 0-4 carry no vegetation or ground changes and both worlds share one seed, so they stay valid. Fork tooling only (patches-tools/local-test), no upstream files.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 `pull-save.ps1` clears the zoom 5-7 map tiles and the vegetation and ground overlays (veg, terra) in the test server's Munin data, with or without -WorldOnly, so the live copy's map is redrawn from its own world
- [x] #2 The generated snapshot keeps its world and its Munin tiles together, and switching to it restores both, so its map is right without a 20-minute redraw
- [x] #3 Switching to a snapshot is one script, with the files owned by the server's user, and README.md in patches-tools/local-test explains why the tiles are cleared or restored
- [x] #4 Tested both ways: after a pull the map has no leftover vegetation from the generated world, and after switching to the snapshot the generated map shows vegetation without a redraw
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
New use-snapshot.ps1: lists snapshots without an argument; with one, stops the server, replaces worlds_local/<world> with world/, and copies tiles/ into the volume as root with chown valheim (or clears z5-7 if the snapshot has no tiles). Tested: pull-save left map/5-7, veg and terra empty (zoom 4 and height kept), and Munin started redrawing from the live copy (441 overview tiles, 195 veg tiles in the first minute). use-snapshot took 108 s; all tiles back, owned valheim; README section updated (use-snapshot, why the tiles move with the world).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Switching the test server between a copy of live and the generated snapshot now keeps Munin's map tiles in step with the world. pull-save.ps1 clears the zoom 5-7 tiles and the vegetation and ground overlays on every pull, so the live copy's map is redrawn from its own world; the snapshot keeps its world and its tiles together, and the new use-snapshot.ps1 restores both (108 s, no 20-minute redraw). README explains why. Tested in both directions.
<!-- SECTION:FINAL_SUMMARY:END -->
