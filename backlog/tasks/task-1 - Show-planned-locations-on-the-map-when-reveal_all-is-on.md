---
id: TASK-1
title: Show planned locations on the map when reveal_all is on
status: To Do
assignee: []
created_date: '2026-11-18T03:09:00Z'
labels:
  - plugin
  - docs
dependencies: []
type: feature
ordinal: 1000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
With `reveal_all = true` the fog goes, but the location layers still list only locations the game has already built, that is, in zones a player has been near. Boss altars, dungeons, traders and the rest of the unexplored world never appear. An admin who turns on reveal_all wants a full spoiler map, like the seed-map websites.

The game plans every location when the world is created and keeps all of them, placed or not, in the world's location registry in the save. Read the unplaced ones from there instead of recomputing from the seed. The map then matches the real world, including any re-planning after a game update or an admin running `genloc`.

Out of scope: trees, rocks, ore, world-generated ruins and player builds in ungenerated zones. Munin reads those from saved world objects, which exist only once a zone has been generated.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 With `reveal_all = true`, the Boss altars, Mini-boss lairs, Dungeons, Traders, Camps & resources and Landmarks layers include locations in zones no player has visited, taken from the world location registry in the save
- [ ] #2 With `reveal_all = true`, `data/markers.json` includes the same unvisited-zone locations
- [ ] #3 With `reveal_all = false`, the markers and `data/markers.json` are identical to stock: built locations on explored ground only
- [ ] #4 The "boss altar found" and "trader found" world events still fire only for built locations; planned locations never trigger them
- [ ] #5 Modded locations appear only when Munin's existing name classification recognises them, the same as for built locations
- [ ] #6 After `genloc` moves planned locations, the markers follow the registry on the next marker rebuild
- [ ] #7 No new config option: `reveal_all` alone controls it
- [ ] #8 The `reveal_all` entry in `docs/ADVANCED.md` says it also shows planned locations and warns that it is a full spoiler
<!-- AC:END -->
