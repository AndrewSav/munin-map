---
id: DRAFT-8
title: 'Show what is inside locations: chest contents and loose items, from the save'
status: To Do
assignee: []
created_date: '2026-11-22T01:45:34Z'
updated_date: '2026-11-22T19:42:17Z'
labels:
  - plugin
  - web
dependencies: []
type: feature
ordinal: 29000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Munin's dungeon popups already list the loose pickables and mineables left inside a generated dungeon and count its chests (src/World/Dungeons.cs), but not what the chests hold, and outdoor locations (ruins, camps, towers, burial sites, shipwrecks) get nothing. The game decides a location's contents when its zone is generated, ghost generation included, and keeps them in the save: chest contents in the ZDO key "items" (Inventory.Save format, stacks already scaled by the world's loot setting), pickables with their rolled item, mineables, pots, nests and spawners. A random object that lost its roll leaves nothing, so the save is exact and no chances need modelling. Checked on a fully generated test world: under 1% of dungeon chests were empty and none were unfilled, including chests deep in dungeons nobody has visited. Not in the save yet: creatures, and what pots, piles and rocks drop when broken. Ungenerated places stay unknowable. So for every generated location a map can show exactly what is there, which other maps (working from the seed outside the game) do not. Reference for which locations and objects matter, from the game data (Valheim 1.0.21): drop tables for 27 kinds of chest, contents of about 80 outdoor locations and of each dungeon theme, and the burial sites (dolmens, ship settings, graves, memorials, shipwrecks).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 A dungeon's popup lists what its chests still hold (by item, with counts) alongside the loose items it already shows
- [ ] #2 Outdoor locations worth it (at least ruins, camps, towers, burial sites and shipwrecks) get a popup saying what is there: chest contents and loose pickables or mineables, matched to the location they belong to
- [ ] #3 Emptied or taken things disappear from the popup once players take them
- [ ] #4 When contents are shown is decided before work starts and recorded in the task (see notes: spoilers)
- [ ] #5 The comment in Dungeons.cs that says chest contents are made by the first player to load them is corrected (if the file is touched; upstream file, decision-2)
- [ ] #6 Checked on the fully generated test world and on a live copy; the sweep cost stays acceptable on the generated world (compare TASK-28)
- [ ] #7 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Not a shaped task yet, just an idea (user, 2026-11-22). Open question to settle when shaping it, if it is ever picked up: spoilers. The game fills every chest when its zone is generated, so the save knows the contents of chests nobody has opened, and anyone with the public map could see which burial chamber or ruin has the best loot before going there. Earlier spoiler handling to keep consistent with: the spoiler switch for planned locations (TASK-1) and the world seed hidden by default (TASK-15). Options: always show; show only once a player has visited or opened the place; show only with the spoiler switch on (reveal_all + the viewer's switch). An earlier draft criterion "an unvisited location's contents show without anyone having been there" was removed, as it presumed the answer.

Spoilers, more (game code, 1.0.21): a location's contents (optional objects, chest loot, pickable items, most dungeon interiors) are seeded from the world seed and the zone, not drawn from a shared random state. So they are the same in every generation of the world: a generated copy of the world (TASK-27) knows what live's chests will hold before anyone gets near them, not only after the zone is generated on live. Exceptions: burial chambers and Sunken crypts (interior turns with the random facing, about 775 dungeons) and the choice of unique sites (traders). This makes the spoiler question sharper: contents can leak from a test copy as well as from the live save.
<!-- SECTION:NOTES:END -->
