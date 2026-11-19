---
id: TASK-2
title: Show in-game day and time in the map HUD
status: To Do
assignee: []
created_date: '2026-11-18T03:09:00Z'
updated_date: '2026-11-19T10:59:00Z'
labels:
  - plugin
  - web
milestone: Initial release
dependencies: []
type: feature
ordinal: 2000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The admin wants to see the server's current in-game day and time at a glance on the map, without opening the stats panel. It belongs with the other labels at the bottom left of the map (coordinates, biome, wind).

The plugin already works out the day number and the game's day clock for the stats panel, but stats refresh too rarely for a live clock. When everyone sleeps, the game skips the night, and a clock driven only by stats refreshes would lag or drift until the next one.

The game's clock is rescaled: the daylight part of the day maps to day fractions 0.25 to 0.75, so on a 24-hour clock 06:00 is sunrise and 18:00 is sunset. The day length comes from the game's environment manager (1800 s by default) and can differ per world or mod.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 A chip at the left end of the bottom-left HUD row (coordinates, biome, wind) reads "Day N · HH:MM" on a 24-hour clock
- [ ] #2 The time follows the game's rescaled day clock: 06:00 at sunrise, 18:00 at sunset
- [ ] #3 A moon symbol shows in the chip between dusk and dawn
- [ ] #4 The day number matches the stats panel's day tile
- [ ] #5 The chip appears with the first live update after the page connects, even with nobody online
- [ ] #6 The minutes advance at game speed: with the default 1800 s day, one game minute is about 1.25 real seconds
- [ ] #7 After the players sleep through the night, the chip shows the morning within about a second
- [ ] #8 A world or mod with a non-default day length is respected
- [ ] #9 The chip is empty and hidden while the page is disconnected, and when the server sends no time (an older plugin)
- [ ] #10 No new config option; the stats panel day tile is unchanged; existing server payloads only gain fields, none change
- [ ] #11 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->
