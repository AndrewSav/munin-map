---
id: TASK-21
title: Document the settings missing from docs/ADVANCED.md's settings table
status: Done
assignee:
  - '@claude'
created_date: '2026-11-20T22:04:00Z'
updated_date: '2026-11-20T23:03:00Z'
labels:
  - docs
dependencies: []
references:
  - docs/ADVANCED.md
  - src/Config.cs
  - README.md
type: docs
ordinal: 20000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
docs/ADVANCED.md has a settings table meant as the one place to look a setting up, but 16 of upstream 3.4.0's 59 settings (src/Config.cs) are not in it, so an admin can miss them; some are mentioned elsewhere, in README prose or the README's Discord table, where they are easy to overlook. Found 2026-11-21 by matching every `config.Bind` key against the table's Key column (upstream/main ee49d16):

- not documented anywhere: Texture `texture_size`, `pixel_size`; Interval `save_fog_texture_interval`, `stats_save_interval`; User `web_pings`, `hidden_players`; Debug `perf_report`; Server `test`, `show_github_link`, `check_updates`; Render `main_thread_rows_per_frame`; Sweep `first_sweep_delay`, `second_sweep_delay`;
- only in README prose: Server `data_path`;
- only in the README's Discord table: Server `preview_image`, `preview_description`.

The new rows go together as one block at the end of the table, not interleaved by section, so the edit to this upstream file stays one hunk that rebases easily (decision-2). Each row's text must say what the code actually does, not just repeat the config description: some descriptions are vague ("Enable test features (bugs).") or may be out of date. Where code and description disagree, the table says what the code does and the disagreement goes in this task's notes; fixing the code or the config description is out of scope.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Every setting bound in src/Config.cs (fork branch, at the time of the change) has a row in docs/ADVANCED.md's settings table, checked by a script that compares the `config.Bind` keys with the table's Key column
- [x] #2 The new rows are one contiguous block at the end of the table, with no change to existing rows, and use the table's columns (Section, Key, Default, What)
- [x] #3 Each new row's default matches the code, and its "What" is checked against the code that reads the setting, with the place in the code recorded in the task notes for each
- [x] #4 Any setting whose config description does not match what the code does is listed in the task notes, with what the code actually does
- [x] #5 Committed with a subject starting with the task ID
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Trace each of the 16 settings to the code that reads it (fork branch).
2. Add one row per setting as a single block at the end of the ADVANCED.md table, worded from the code; defaults from Config.cs.
3. Re-run the Config.cs vs table comparison.
4. Record code locations and description/code mismatches in the notes.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Where each setting is read (fork branch, 2026-11-21):
- data_path: MuninMap.cs DataFolder (absolute or under BepInEx; empty = BepInEx/config/MuninMap; moves old map_data and announce.token once).
- preview_image, preview_description: Server/Preview.cs (FilePath, Image: PNG/JPEG/WebP/GIF, 8 MB cap, env vars and ~ expanded; Tags: description default "Live map of " + title).
- check_updates: Server/UpdateCheck.cs Start/Loop (first check 60 s after start, then every 12 h; logs a newer release; page gets latest_version via config).
- show_github_link: Config.cs config["github_link"] -> web/js/app.js showGithub (hides #btn-github only).
- test: Patches/ZroutedRpc_Patch.cs (DiscoverLocationRespons RPC sent to everybody). Its only use.
- web_pings: Server/MapDataServer.cs /api/ping gate; Server/WebPing.cs Try (PER_VISITOR 10 s per remote address, ANY 2 s); web/js/app.js webPing and the pin form's Ping button.
- hidden_players: Config.cs IsHidden (case-insensitive); filtered in Live/Players.cs Build (dots and count), Live/Stats.cs (public players, NameOfPlayerId for builders), Live/Events.cs Add (feed), Live/Activity.cs (trails).
- save_fog_texture_interval, stats_save_interval: MuninMap.cs SaveLoop (5 s ticks; fog saved only when dirty; stats save also saves Activity).
- texture_size, pixel_size: MuninMap.cs legacy world render (/map), Server/MapDataServer.cs BuildMapJpg, Legacy/ForestMap.cs, Legacy/StructureMap.cs, World/WorldSweep.cs (legacy cells), World/Fog.cs Load (old fog.png without fog.grid), MuninMap.cs prerender stride. The fog grid itself is fixed (Fog.GRID 2048, CELL 12).
- main_thread_rows_per_frame: Tiles/TileStore.cs MainThreadPump (min 4), used when render_threads <= 0 or after the engine refuses off-thread sampling.
- first_sweep_delay, second_sweep_delay: World/WorldSweep.cs Loop (second only when greater than first).
- perf_report: Util/Perf.cs (Report every 5 minutes from SaveLoop).

Config descriptions that do not match the code (table says what the code does; descriptions left alone):
- hidden_players: "never shown on the map, in the stats or in the event feed". The name still goes to Discord join/leave posts and the in-game leave message (MuninMap.cs NotifyJoin/NotifyLeave post before Events.Add filters), and appears as the owner on bed and tombstone markers (World/Markers.cs, unfiltered).
- show_github_link: describes the link only; with it off, the bottom-right "Munin Map x.y.z is out" note still shows (app.js showGithub returns after hiding the button only... the note logic runs regardless).
- texture_size: "How large is the map texture?" It now sizes only the legacy images (/map, /structures, /forest) and reads old fog.png files; the live map's tiles and fog grid do not use it.
- check_updates: "when the server starts" is 60 s after start.
- test: "Enable test features (bugs)" is a single feature: discovered locations sent to all players.
- second_sweep_delay: "0 turns it off"; so does any value not greater than first_sweep_delay.
- web_pings: "once every 10 seconds each": each = per remote address (not per browser), plus 2 s between any two pings.

Check: all 59 config.Bind keys in src/Config.cs (fork) have a row in the table, each under the same section; no table row without a setting. git diff of docs/ADVANCED.md: 16 added lines at the end of the table, nothing else changed.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Added the 16 settings missing from docs/ADVANCED.md's settings table as one block at its end (commit 1c980da): data_path, preview_image, preview_description, check_updates, show_github_link, test, web_pings, hidden_players, save_fog_texture_interval, stats_save_interval, texture_size, pixel_size, main_thread_rows_per_frame, first_sweep_delay, second_sweep_delay, perf_report. Each row is worded from the code that reads the setting, with defaults from Config.cs; a script confirms all 59 settings now have a row under the right section. Where a config description disagrees with the code (notably hidden_players, which still names players in Discord join/leave posts and on beds and tombstones, and show_github_link, which leaves the update note), the table follows the code and the notes list the mismatch. Code and config descriptions unchanged.
<!-- SECTION:FINAL_SUMMARY:END -->
