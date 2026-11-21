---
id: TASK-15
title: Optional world seed in Server details
status: Done
assignee:
  - '@claude'
created_date: '2026-11-19T21:51:00Z'
updated_date: '2026-11-21T05:58:00Z'
labels:
  - plugin
  - web
  - docs
dependencies: []
type: feature
ordinal: 14000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
An admin may want players to be able to look the world up on the seed-map websites without asking for the seed. Munin doesn't show it anywhere today. The seed is the world's generation seed, the short text the game shows when the world is created (World.m_seedName, also on the server as the world's seed).

The seed lets anyone see the whole world on those sites, so it is a spoiler of the same kind as TASK-1's spoiler view. Showing it is therefore the admin's choice, through a new server setting, off by default. With the setting off, the server must not send the seed to the page at all, not just hide it.

Where it shows: the "Server details" section of the stats panel (`web/js/ui.js`, the `<dl class="kv">` list), as one more line. Write it per decision-2: new behaviour in new files, small marked edits in upstream files.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A new server setting (for example `show_seed` in the Server section of the BepInEx config) controls it; it is off by default and its description says the seed reveals the whole world on seed-map sites
- [x] #2 With the setting on, the stats panel's "Server details" section shows a "Seed" line with the world's seed
- [x] #3 With the setting off, nothing changes: no Seed line, and the seed appears in no data the server sends (config, stats or anything else)
- [x] #4 The seed shown is the one the game uses for this world, checked against the world on the test server
- [x] #5 The new setting is listed in `docs/ADVANCED.md`'s settings table
- [x] #6 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [x] #7 Written per decision-2: new behaviour in new files, upstream edits marked where the format allows, and every commit subject starts with the task ID
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. src/Patches/Seed.cs: show_seed (Server, default false), AddTo puts the seed (World.m_seedName) into the client config only when on.
2. Config.cs: Bind and AddTo hooks (marked). web/js/seed.js: a Seed line at the end of Server details; one hook in ui.js renderStats (marked).
3. ADVANCED.md settings row, PATCHES.md entry. 4. Test off (seed nowhere) and on (line shown, matches the world file).
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Tested on the local test server (2026-11-21). The seed was read independently from the world file (worlds_local/<world>/_main.*.fwl2: the 10-character string right after the world name) and only compared, never printed. show_seed off (default): the seed appears in none of 19 public responses nor in the websocket frames (hello carries the config), and /config has no seed key. On: /config carries the seed and it matches the world file; in headless Chrome the Stats tab's Server details ends with a Seed line showing the same 10 characters. Test config restored to show_seed false. Docs note added (user, 2026-11-21): players on the server can get the seed anyway with the game's printseeds console command (F5, -console launch option), so show_seed only decides whether Munin hands it out.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
New server setting show_seed (Server section, default false). On: the world's seed (World.m_seedName) is added to the client config and shown as the last line, Seed, of the stats panel's Server details. Off: the seed is sent nowhere. New files src/Patches/Seed.cs and web/js/seed.js; marked one-line hooks in Config.cs (2) and ui.js (2). docs/ADVANCED.md settings row (with the note that the game's printseeds console command reveals the seed to players anyway) and PATCHES.md entry. Verified on the test server against the seed in the world file, with the setting off (seed absent from every response and websocket frame) and on (shown, matching). Commit 980e1ef.
<!-- SECTION:FINAL_SUMMARY:END -->
