---
id: TASK-19
title: >-
  Privacy: stop publishing Steam IDs; player_activity setting for the Activity
  tab
status: To Do
assignee: []
created_date: '2026-11-20T05:40:00Z'
updated_date: '2026-11-20T05:42:00Z'
labels:
  - plugin
  - web
  - docs
milestone: m-1
dependencies: []
references:
  - src/Live/Stats.cs
  - web/js/playercard.js
type: enhancement
ordinal: 18000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Two privacy improvements, from TASK-12's findings (agreed with the user 2026-11-20).

1. The public stats give away players' Steam IDs. In `data/stats.json`, each player's `key` is `<SteamID64>/<character name>`, built from the connection's host name (Live/Players.cs KeyOf). The page never reads `key`; it matches players by name. The server needs the key only to keep characters apart in the stats it saves to disk, which are written separately (Live/Stats.cs WritePlayer with full: true). Upstream already leaves `playerId` out of the public response the same way.

2. When people play is public too. The player card's Activity tab shows when each player plays: play time per day over the last two weeks, an hour-of-the-week histogram, the latest sessions, the current session's start and the longest session. Together these show a player's real-life routine. A new server setting, `player_activity`, controls it. It defaults to true, so a server that doesn't set it behaves as upstream does. With it false, the Activity tab is not shown, and the server doesn't send the data behind it.

The Activity tab (`activityHtml` in `web/js/playercard.js`) uses these stats fields, and only it uses them: `days`, `week`, `recent`, `sessionStart`, `longest`.

The rest of TASK-12's privacy findings were accepted as they are, with no change: the other totals in the stats, last-seen position (already off by default), hidden players' totals.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 No public response carries a Steam ID (or another platform ID): `data/stats.json` no longer includes the account part of a player's key, checked by scanning every public response on a server with players
- [ ] #2 The stats saved to disk still keep the full key, so characters stay apart, and existing stats files load as before
- [ ] #3 The page works as before without it: player cards, the recent players list and everything else that shows stats
- [ ] #4 A new server setting `player_activity` (default true) is in the BepInEx config with a description, and is listed in `docs/ADVANCED.md`'s settings table
- [ ] #5 With `player_activity = true`, the Activity tab and its data are exactly as upstream
- [ ] #6 With `player_activity = false`, the player card has no Activity tab, and `data/stats.json` carries none of `days`, `week`, `recent`, `sessionStart` or `longest`
- [ ] #7 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [ ] #8 Written per decision-2: new behaviour in new files, upstream edits marked where the format allows, and every commit subject starts with the task ID
<!-- AC:END -->
