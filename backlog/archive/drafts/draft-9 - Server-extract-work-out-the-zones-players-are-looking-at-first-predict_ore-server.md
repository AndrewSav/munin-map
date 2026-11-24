---
id: DRAFT-9
title: >-
  Server extract: work out the zones players are looking at first (predict_ore =
  server)
status: Draft
assignee: []
created_date: '2026-11-23T16:17:52Z'
updated_date: '2026-11-24T06:43:01Z'
labels:
  - plugin
  - web
dependencies: []
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
In predict_ore = server (TASK-4.5) a new prediction takes half an hour to an hour and a half on one low-priority core (after the first start, and again after a game update), and until it finishes players see the old prediction or none. The zones a player is looking at on the map should be generated first, as tiles a browser asks for already jump TileStore's queue and as TASK-4.6 does for in-place.

Already there (TASK-4.6): the signal. src/Patches/ViewedZones.cs is called from TileStore.Get for every tile a browser asks for (the one upstream hook) and raises ViewedZones.Zone(zx, zy) for each zone under a close-zoom tile (at most 1 km across). In-place subscribes to it; server mode would subscribe too, with no further upstream edit.

What is left, and the difficulty: the work runs in a separate game-server process that ore-extract starts with its zones fixed up front, and the result appears only when the whole output is joined at the end. So this needs ore-extract to take a priority list while it runs (for example a file the generation plugin polls, written by Munin from ViewedZones), and Munin to show deposits as zones finish, not only at the end, with resume still exact.

Lower priority than TASK-4.6: if in-place turns out as hoped it becomes the usual mode, and server mode the choice for admins who want an extract's accuracy.

Ref: TASK-4.5 (ore-extract supervisor in ore-extract/core, Patches/ServerExtract.cs), TASK-4.6 criterion #7 (the in-place version, Patches/ViewedZones.cs), src/Tiles/TileStore.cs.
<!-- SECTION:DESCRIPTION:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Archived 2026-11-24: no longer applies. TASK-4.7 removed predict_ore = server (and the file mode), keeping only in-place prediction, which already does this: ViewedZones puts the zones players are looking at first.
<!-- SECTION:NOTES:END -->
