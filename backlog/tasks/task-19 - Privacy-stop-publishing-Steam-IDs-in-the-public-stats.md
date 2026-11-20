---
id: TASK-19
title: 'Privacy: stop publishing Steam IDs in the public stats'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-20T05:40:00Z'
updated_date: '2026-11-20T22:22:00Z'
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
From TASK-12's findings (agreed with the user 2026-11-20).

The public stats give away players' Steam IDs. In `data/stats.json`, each player's `key` is `<SteamID64>/<character name>`, built from the connection's host name (Live/Players.cs KeyOf). The page never reads `key`; it matches players by name. The server needs the key only to keep characters apart in the stats it saves to disk, which are written separately (Live/Stats.cs WritePlayer with full: true). Upstream already leaves `playerId` out of the public response; `key` is now left out the same way.

The rest of TASK-12's privacy findings were accepted as they are, with no change: the other totals in the stats, last-seen position (already off by default), hidden players' totals.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 No public response carries a Steam ID (or another platform ID): `data/stats.json` no longer includes a player's `key`, checked by scanning every public response on a server with players
- [x] #2 The stats saved to disk still keep the full key, so characters stay apart, and existing stats files load as before
- [x] #3 The page works as before without it: player cards, the recent players list and everything else that shows stats
- [x] #4 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [x] #5 Written per decision-2: upstream edits marked where the format allows, and every commit subject starts with the task ID
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Stats.WritePlayer: write `key` only when full (one marked line).
2. PATCHES.md entry.
3. Build, test server: scan public responses for Steam IDs; check the player card and players list; check the saved stats.json still has keys.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Scope cut, 2026-11-21: a `player_activity` setting to hide the player card's Activity tab (and the stats behind it) was considered and dropped. The same information can be had by watching the event stream (the public feed keeps every join and leave with its time) or by polling who is online, and we want to keep the changes to upstream small. Real protection there is TASK-20's require_auth.

Verified on the local test server (2026-11-21): 17 public responses (config, players, data/players.json, data/stats.json, events.json, events/older.json, pins.json, /pins, markers.json, hotspots.json, activity.json 3h/24h, building.json, structures/stats, vehicles, messages, structures/index.json) carry no 17-digit Steam ID and no `key` field. The saved stats.json on disk still has `key` = `<SteamID64>/AdMiN`, and the server loaded the existing file after the restart. The page code has no reference to a player's `key` (grep web/js); still to see in a browser: player card and players list.

Player card and players list checked in the browser by the user on the test server (2026-11-21): as before.

Correction, 2026-11-21: the scope-cut note above says TASK-20's require_auth is the real protection against polling. It is not: require_auth only gates actions, and everything readable (who is online, the event feed, stats) stays public. The user decided not to hide the map's data from strangers.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
The public stats (`data/stats.json`) no longer carry players' Steam IDs: `Stats.WritePlayer` writes `key` (`<SteamID64>/<character name>`) only to the stats saved on disk, the way upstream already treats `playerId`. One marked line in src/Live/Stats.cs; PATCHES.md entry added. Verified on the test server: 17 public responses have no Steam ID and no `key`; the saved file keeps the full key and loads after a restart; the page (player card, players list) works as before. Scope cut: the planned `player_activity` setting was dropped (see notes).
<!-- SECTION:FINAL_SUMMARY:END -->
