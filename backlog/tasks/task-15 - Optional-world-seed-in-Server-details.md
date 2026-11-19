---
id: TASK-15
title: Optional world seed in Server details
status: To Do
assignee: []
created_date: '2026-11-19T21:51:00Z'
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
- [ ] #1 A new server setting (for example `show_seed` in the Server section of the BepInEx config) controls it; it is off by default and its description says the seed reveals the whole world on seed-map sites
- [ ] #2 With the setting on, the stats panel's "Server details" section shows a "Seed" line with the world's seed
- [ ] #3 With the setting off, nothing changes: no Seed line, and the seed appears in no data the server sends (config, stats or anything else)
- [ ] #4 The seed shown is the one the game uses for this world, checked against the world on the test server
- [ ] #5 The new setting is listed in `docs/ADVANCED.md`'s settings table
- [ ] #6 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [ ] #7 Written per decision-2: new behaviour in new files, upstream edits marked where the format allows, and every commit subject starts with the task ID
<!-- AC:END -->
