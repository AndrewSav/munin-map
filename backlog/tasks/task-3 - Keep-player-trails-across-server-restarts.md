---
id: TASK-3
title: Keep player trails across server restarts
status: Done
assignee:
  - '@claude'
created_date: '2026-11-18T03:12:00Z'
updated_date: '2026-11-21T06:44:00Z'
labels:
  - plugin
  - docs
dependencies: []
type: feature
ordinal: 3000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Player trails on the activity layer (the last 3 or 24 hours of where each player went) are kept only in the server's memory. A server restart wipes them, so if the server has restarted in the last 24 hours, the 24-hour view does not hold the 24 hours of data it promises.

Trails should survive a restart. A clean shutdown should keep everything recorded up to that point. A crash or kill can lose what was recorded since the last save (`stats_save_interval`, 60 s by default; already documented as covering the activity trails, and the admin's choice). Points older than the trail window (24 hours) are not worth restoring.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 After a clean server restart, the activity layer shows trail points recorded before the restart, up to the 24-hour trail window
- [x] #2 After a crash or a killed process, at most the trail points recorded since the last save (`stats_save_interval`) are lost
- [x] #3 Points older than 24 hours are dropped on load, and players whose whole trail has aged out are not restored
- [x] #4 Trails are kept per world, alongside that world's other map data, so two worlds never share trails
- [x] #5 A missing, empty or unreadable trails file means starting with no trails and a warning in the log, never a failed start
- [x] #6 Saving trails causes no noticeable server hitch
- [x] #7 The "Where things are kept" table in `docs/ADVANCED.md` lists the new file
- [x] #8 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. `Activity.cs` (upstream): add `partial` to the class line, a call at the end of `Load` (after it clears the trails) and one at the top of `Save` (before its dirty check), each marked `// patches: TASK-3`.
2. `src/Patches/Trails.cs` (ours, `partial class Activity` in `MuninMap.Live`): trails file `trails.txt` in the world folder beside `activity.txt`.
   - Load: read it, drop points older than 24 h and players left with none, warn and start empty if missing, empty or unreadable.
   - Save: copy the trails under the lock (cheap), then format and write off the main thread, to a temp file renamed over the old one. Own changed flag, so an unchanged trail set is not rewritten. Saves ride on `stats_save_interval` through the `Save` hook.
   - Shutdown: the last save is written synchronously so the process exit cannot cut it short.
3. Docs: a `trails.txt` row in the "Where things are kept" table in `docs/ADVANCED.md`; a `PATCHES.md` entry.
4. Test on the local test server: points before a clean restart come back; after `docker kill` at most the last interval is lost; a corrupt file only warns; save time logged to check for a hitch.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Trails.cs + 3 marked lines in Activity.cs (partial, LoadTrails after Load's clear since Load returns early when activity.txt is missing, SaveTrails at top of Save). Shutdown: Harmony prefix on ZNet.Shutdown makes the last save synchronous. Tested on local server with a seeded trails.txt: aged-out points/players, out-of-order point, junk line and empty key dropped; 24 h and 3 h views correct; empty file and unreadable file (chmod 000) warn and start empty. Test pitfall: a file copied in while the server runs is overwritten by its shutdown save. Docs: ADVANCED.md row, PATCHES.md entry. Pending: save round trip, docker kill, perf_report timing (need a player moving).

Player test: AdMiN walked, 86 points in memory; trails.txt saved on the 60 s interval. docker kill about 46 s after the last save, then start: 80 points restored, last point at the save time (only points after the last save lost), file byte-identical to before the kill, no .tmp left. perf_report: 'trails save' longest 0.3 ms on the game thread (4 saves, ~90 points). AC2 evidence.

Clean restart with the player connected: 111 points in memory just before, 113 restored with the last at the final steps (newer than the last interval save), so the synchronous shutdown save writes everything. Per world: the file is written in the world's own data folder (Saveshnik/trails.txt beside activity.txt); Load reads it from the folder of the world being loaded. Only one world on the test server, so two worlds were not run side by side. decision-2: upstream files touched are src/Live/Activity.cs (3 marked lines) and docs/ADVANCED.md (one table row, no marker possible).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Player trails on the activity layer now survive a server restart. New src/Patches/Trails.cs (partial of Activity) saves them to <world>/trails.txt every stats_save_interval and at shutdown, and restores them on world load, dropping points older than 24 h, players with nothing left, hidden_players, out-of-order points and junk lines; a missing, empty or unreadable file warns and starts empty. Saves copy the trails on the game thread (0.3 ms at most in perf_report) and format and write on a worker thread through a temp file and rename; the shutdown save is written in place. Upstream edits: partial plus two marked calls in Activity.cs, one row in ADVANCED.md. PATCHES.md entry added. Tested on the local server with a seeded file and a real player: clean restart kept every point, docker kill lost only points since the last save. Not measured: save time with a full day of trails for many players (expected under 1 ms, an array copy). The live server gets it with the next fork release.
<!-- SECTION:FINAL_SUMMARY:END -->
