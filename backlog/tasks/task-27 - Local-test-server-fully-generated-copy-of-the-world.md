---
id: TASK-27
title: 'Local test server: fully generated copy of the world'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-21T10:23:00Z'
updated_date: '2026-11-21T20:58:46Z'
labels:
  - tooling
dependencies: []
type: chore
ordinal: 26000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The test world is a copy of live, so only the zones live players have visited exist in it. TASK-26 (resource objects in Mistlands, Ashlands and Deep North) and TASK-4 (comparing predicted ore with real ore) both need zones that nobody on live has reached. Upgrade World (JereKuusela, `zones_generate`) generates every zone of a world on the server; the whole world takes hours.

Generate the whole world once on the test server with Munin switched off, so its sweeps, tiles and caches don't slow generation or fill up with half-generated state. Then keep the result as a snapshot, so the test server can run either a fresh copy of live or the fully generated world. Munin's performance on the full world is tested afterwards; if it struggles, a partial generation with Upgrade World's `biomes=`/`min=`/`max=` limits is the fallback. Fork tooling only: no product code and no upstream files.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Upgrade World and its dependency Server Devcommands are installed on the test server and on the admin's client (the Steam install's BepInEx), and both load on the server
- [x] #2 Munin is switched off on the test server for the generation run, and comes back on with the next deploy.ps1
- [x] #3 The user has instructions to start, watch and stop the generation from the in-game console
- [x] #4 The whole world is generated, and the result is kept as a snapshot outside the folder pull-save.ps1 replaces
- [x] #5 The test server can be switched between a fresh copy of live and the generated snapshot, and README.md in patches-tools/local-test says how
- [x] #6 Afterwards the client is back as it was: BepInEx switched off again (doorstop_config.ini enabled = false; backup doorstop_config.ini.bak-munin) and the two mods removed from its plugins folder
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Install Upgrade World 1.87.0 and Server Devcommands 1.112.0 (Thunderstore) on the test server (data/config/bepinex/plugins) and the client (Steam install, BepInEx/plugins, one folder each); add the admin's Steam ID to the test adminlist.txt.
2. Munin off: MuninMap.dll moved to data/config/bepinex/plugins-off/ and the copy in the server's plugins deleted (fast-start.sh copies *.dll in but never deletes). deploy.ps1 copies it back.
3. User runs zones_generate (a short trial first, then the whole world) from the F5 console, then save.
4. Stop the server cleanly, copy data/config/worlds_local/<world> to a snapshot folder outside what pull-save.ps1 replaces; document switching between live and the snapshot in README.md.
5. Munin back on; perf check on the full world separately; remove the client mods unless the user keeps them.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Installed and verified: server log 'Loading [Server Devcommands 1.112]', 'Loading [Upgrade World 1.87]', 'Chainloader startup complete (2 loaded, 0 skipped, 0 failed)'; MuninMap not loaded. upgrade_world.cfg defaults: Automatic start false (zones_generate needs 'start'), World radius 10500, Root users empty (all admins). Server Devcommands 1.111+ disables the game's cheat tracking for commands by default. Downloads kept in the session scratchpad only.

Client BepInEx was off (doorstop_config.ini enabled = false; last LogOutput.log 2026-11-04). Switched on for the run, original kept as doorstop_config.ini.bak-munin. The user plays without client mods and enables BepInEx only for utility and testing, so it must be switched off again when this task is done.

Trial: zones_generate max=1500 generated 752 of 754 zones in 57 s (about 13/s); the same 2 zones fail on every run ('2 errors'). Upgrade World counts an error when SpawnZone(zone, Ghost) returns false for 10 s and never logs which zone. Munin switched on briefly with reveal_all + spoiler cookie: after a sweep, exactly 2 planned locations within 1500 m are still unbuilt: TrollCave02 at (-771.7, -1223) zone -12,-19, and MountainCave02 at (1408, -512) zone 22,-8. Other instances of both types built fine. Asked the game code why SpawnZone stays false. Munin is currently ON (plugins/MuninMap.dll); switch it off again before the full run.

Debug build of Upgrade World (local clone, working tree only: references to munin-map/libs, Krafs.Publicizer, actions/GenerateDebug.cs logging refused zones) found the cause: first-time load of a location's prefab, and for dungeons its rooms (94 for MountainCave02, about 4/s), takes longer than Upgrade World's 10 s no-success limit, so the zone is counted as an error and skipped. The game drops the unfinished load once the zone stops being asked for, so later runs fail the same way. TrollCave02 at -12,-19 generated in a later run once other TrollCave02s had loaded it; DrakeNest01 at 24,-6 failed the same way. Terrain thread alive throughout. Live is not affected: the game's own generation retries every update with no limit. Plan agreed with the user: full run with the released 1.87 as shipped, then a cleanup run with a local build that waits 120 s instead of 10 s (parked as plugins-off/UpgradeWorld-debug-120s.dll). Released DLL kept as plugins-off/UpgradeWorld-1.87-release.dll. Munin off again for the run.

Average about 450 zones/min; slowest in the Mistlands (about 280/min) and during autosaves. Next: cleanup run with the 120 s build.

Cleanup run with the local 120 s build: 329 zones targeted, 329 generated, 0 errors, 17 min (MorkBorg zones about 30 s each, MountainCave02 about 20 s). Server: Upgrade World and Server Devcommands removed from data/config/bepinex/plugins and from the server volume, parked in plugins-off with both Upgrade World builds; MuninMap.dll back in plugins. README.md: 'A fully generated world' section (use, go back, how it was made). Munin's performance on the full world is a separate step.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
The test server now has a fully generated copy of the world as a snapshot (data/snapshots/<world>-generated-2026-11-22, about 4.1 million objects), next to the usual fresh copy of live. README says how to switch to the snapshot and back to live, and how it was made. Mods removed from server and client afterwards; client BepInEx off again. Not done here: Munin's performance on the full world.
<!-- SECTION:FINAL_SUMMARY:END -->
