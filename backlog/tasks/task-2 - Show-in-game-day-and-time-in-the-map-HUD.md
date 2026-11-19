---
id: TASK-2
title: Show in-game day and time in the map HUD
status: Done
assignee:
  - '@claude'
created_date: '2026-11-18T03:09:00Z'
updated_date: '2026-11-19T21:50:00Z'
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
- [x] #1 A chip at the left end of the bottom-left HUD row (coordinates, biome, wind) reads "Day N · HH:MM" on a 24-hour clock
- [x] #2 The time follows the game's rescaled day clock: 06:00 at sunrise, 18:00 at sunset
- [x] #3 A moon symbol shows in the chip between dusk and dawn
- [x] #4 The day number matches the stats panel's day tile
- [x] #5 The chip appears with the first live update after the page connects, even with nobody online
- [x] #6 The minutes advance at game speed: with the default 1800 s day, one game minute is about 1.25 real seconds
- [x] #7 A world or mod with a non-default day length is respected
- [x] #8 The chip is empty and hidden while the page is disconnected, and when the server sends no time (an older plugin)
- [x] #9 No new config option; the stats panel day tile is unchanged; existing server payloads only gain fields, none change
- [x] #10 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [x] #11 While the players sleep, the clock follows the game's fast-forward to morning: it runs fast, updated at least every second, rather than stopping and jumping once the sleep is over, and it shows the morning within about a second of the game reaching it
- [x] #12 A "Clock" switch in the fork's Settings group (web/js/patches-settings.js, present whatever reveal_all is), off by default, shows or hides the chip without reloading the page; the choice is remembered in this browser across reloads (its own storage key, not upstream's)
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Server: new src/Patches/WorldClock.cs writes "clock": {worldTime, dayLength, day, dayFraction, night} into the players update (one marked hook line in Live/Players.cs Build). It uses the same formula as the stats panel day tile (Stats.DayFraction).
2. Page: new web/js/clock.js, imported from app.js (one marked line). It inserts the chip as the first div of .hud (the HUD styles every div there, so no CSS edit), reads "clock" from each players frame, and registers the Clock switch with patches-settings.js (off by default, localStorage key munin-fork-clock).
3. Between updates the chip runs at the pace the game's time moved between the last two updates: 0 while the server has nobody on, 1 normally, fast during the sleep fast-forward.
4. Test on the local test server: empty server (frozen), joined (ticking at 1.25 s per game minute), sleeping (fast-forward), disconnect (hidden), switch off and on (no reload, remembered).
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Found while testing, 2026-11-20: with nobody online the game's clock does not move (worldTime stayed at 369330.2 over several seconds in both the players update and the stats; the user confirmed). The first version of the chip ran the clock on at game speed between updates, so it would have ticked forward and snapped back every second on an empty server. It now runs at the measured pace (capped at 200x, and never more than 3 s past an update).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
A Clock switch in the fork's Settings group (off by default, remembered per browser) shows the in-game day and time as a chip at the left end of the bottom-left HUD row, for example "Day 205 · 06:34", with a moon at night.

Server: src/Patches/WorldClock.cs adds "clock" {worldTime, dayLength, day, dayFraction, night} to the players update, using the stats panel's formula (one marked hook line in Live/Players.cs). Page: web/js/clock.js, imported from app.js (one marked line). The chip is a div inserted into .hud, which already styles it, so no CSS edit. Between updates it runs at the pace the game's time moved between the last two updates: still on an empty server (the game pauses its clock then), normal when someone is on, fast during the sleep fast-forward.

Verified: on the test server, the clock day matched the stats day tile (205), and worldTime stood still with nobody online. The user tested the switch (no reload, remembered), the chip, ticking while joined, and sleeping through to morning. Covered by the code only, not tested: a non-default day length (read from EnvMan.m_dayLengthSec), and hiding while disconnected or when an older plugin sends no time.
<!-- SECTION:FINAL_SUMMARY:END -->
