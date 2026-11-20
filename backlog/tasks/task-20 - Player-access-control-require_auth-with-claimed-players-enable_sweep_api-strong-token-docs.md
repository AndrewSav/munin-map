---
id: TASK-20
title: >-
  Player access control: require_auth with claimed players, enable_sweep_api,
  strong-token docs
status: To Do
assignee: []
created_date: '2026-11-20T07:11:00Z'
updated_date: '2026-11-20T22:58:00Z'
labels:
  - plugin
  - web
  - docs
  - security
milestone: m-1
dependencies: []
references:
  - src/Server/MapDataServer.cs
  - web/js/layers/markers.js
  - src/World/Markers.cs
type: feature
ordinal: 19000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
From TASK-12's findings. Today anyone who can reach the map can place pins and ping the game under any name, rename or hide any base (and a hidden base has no way back from the page), and trigger world sweeps without limit. A visitor can also invent a new key per request, which gets round the per-key limits. Before the map is exposed to the internet, the server should be able to require that these actions come from a known player. The design below was agreed with the user on 2026-11-20.

**Admin token (docs only):** describe the admin token, and the new player token, as a strong secret, a long randomly generated string, instead of a "secret word". No complexity rules and no limit on attempts.

**Sweep API:** a new setting `enable_sweep_api` (default true) turns `POST /api/sweep` and `/structures/refresh` on or off. When off, the admin token still works, as with `web_pins`, `web_pings` and `web_edit_bases`.

**Player access:** a new setting `require_auth`, default false. When false, everything behaves exactly as now. When true, no action that changes something is available without a claimed player:
- A **player token**, a secret like the admin token that the admin shares with their players, lets a browser **claim** an unclaimed player from the players Munin knows. The server records the browser's **key** against that player. The key is the browser's client ID, the value the pin popup already shows as the "pin key". One key per player: claiming a claimed player is refused.
- The claim belongs to the key. Another device of the same person enters the same key and acts as the same player, with no second claim. With `require_auth` on, the key is effectively the player's login.
- The **admin can unclaim** a player in the players list, for example after a lost key, so the player can claim again.
- With `require_auth` on, pins, pin removal, pings, base renames and hides, and `/api/sweep` and `/structures/refresh` refuse keys with no claim. The admin token is accepted as today. A pin's "by" and a ping's name are the claimed player's name, never free text.
- **Pin and ping UI:** it still offers entering an existing key, as today. If the browser has no claim, it offers a dropdown of unclaimed players and a field for the player token, with a note that if your name isn't listed, you need to join the game server first (Munin only knows players it has seen online).
- **Bases:** the rename, hide and unhide UI gets the same claim flow. Base overrides record who renamed or hid a base and when, and a renamed base's popup names who renamed it. `GET /api/bases` (admin token) lists every override: name, coordinates, hidden or not, who hid or renamed it if known, and when it was last hidden and last renamed. There is no page list: unhiding stays `POST /api/base` with x and z plus the admin token.

**Unchanged:** the existing switches keep their meaning: off means off for everyone except the admin token holder, and `require_auth` only adds the claim requirement on top. Chat pins (`!pin`, `!undopin`, `!deletepin`) stay as they are: the game has already authenticated the player.

**Accepted limitations** (the user's decisions):
- one person can potentially claim several players;
- the key travels in a URL query (`data/pins.json?client=`), so it can appear in proxy and server access logs, and whoever can read those can act as those players. Moving it to a header would add hunks in two busy upstream files for little benefit;
- with `require_auth` off, other websites can still trigger the open actions through a visitor's browser, as today. With it on they can't, because the key is in browser storage, not a cookie.

Do it in three parts, each its own commit under this task's ID: settings, docs and the sweep switch; claiming (player token, claim and unclaim, the claim checks, base override records, `GET /api/bases`); the UI.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 README and `docs/ADVANCED.md` describe the admin token and the player token as a strong secret, a long randomly generated string, not a "secret word"
- [ ] #2 `enable_sweep_api` (default true) is in the config with a description: when false, `/api/sweep` and `/structures/refresh` are refused unless the admin token is sent; when true they behave as today (subject to `require_auth`)
- [ ] #3 `require_auth` (default false) is in the config with a description; with it false, every action and the whole UI behave exactly as upstream
- [ ] #4 A player token (a secret file like the admin token; none means claiming is closed) lets a browser claim one unclaimed known player; the server keeps the browser's key against that player, and refuses to claim a player that is already claimed
- [ ] #5 A second browser using the same key acts as the same claimed player, with no second claim
- [ ] #6 The admin can unclaim a player from the players list (admin token), after which the player can be claimed again
- [ ] #7 With `require_auth` true, pin, unpin, ping, base rename, hide and unhide, `/api/sweep` and `/structures/refresh` refuse keys with no claim (the admin token is accepted as today), and a pin's "by" and a ping's name are the claimed player's name, never free text
- [ ] #8 Base overrides record who renamed or hid a base and when; a renamed base's popup names who renamed it
- [ ] #9 `GET /api/bases` (admin token only) lists every base override: name, coordinates, hidden or not, who hid or renamed it if known, and when it was last hidden and last renamed
- [ ] #10 With `require_auth` true, the pin and ping UI offers entering an existing key, and, when the browser has no claim, a dropdown of unclaimed players plus a player token field, with a note that a player not listed must join the game server first; the free-text name is gone
- [ ] #11 With `require_auth` true, the base popup uses the same claim flow for rename, hide and unhide
- [ ] #12 `web_pins`, `web_pings` and `web_edit_bases` keep their meaning (off for everyone except the admin token holder), and chat pins work as before
- [ ] #13 The accepted limitations (one person can potentially claim several players; the key in URL queries and access logs; the key as a login; cross-site requests with require_auth off) are written in the docs
- [ ] #14 Tested on the local test server with require_auth off (as upstream) and on (claim, actions, unclaim, a second device with the same key, refusals without a claim)
- [ ] #15 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [ ] #16 Written per decision-2 in three parts (settings and docs; claiming; UI), each a commit starting with the task ID; new behaviour in new files, upstream edits marked where the format allows
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Decided 2026-11-21: require_auth gates actions only; the map's data (who is online, events, stats, tiles) stays public, and hiding it from strangers is not planned.

Decided 2026-11-21, claims:
- A claim is for one character (a Munin player), not a Steam account. Claims only put a name on pins, pings and base edits, so which of a person's characters they claim makes little difference; players are expected to claim their main.
- One character per client ID, enforced by the server too: a claim from a client ID that already holds one is refused.
- Page: a browser whose key holds a claim acts as that character and never offers to claim again. Entering an existing key adopts that key's claim (or offers the claim flow if it has none). Clearing browser storage gives a new key with no claim, so the flow comes back.
- Accepted gaps (docs and PATCHES.md): with the player token, anyone can claim any unclaimed character, including someone's alt, or a second character after clearing storage. The admin unclaims to fix it. The old claim stays on the old key, so clearing storage does not free your main: re-enter the old key or ask the admin. Two accounts can each have a character of the same name: the dropdown tells them apart (e.g. first seen), and attribution shows both by the name only (noted, not designed around).
- "One person can claim several players" in the description is an artifact of this simple design, not a goal.

Docs scope, 2026-11-21 (replaces the "clearing storage" wording in the claims note above): the docs do not walk through clearing browser data. They explain (1) your key: where to find it, how to keep it, and how to use it in another browser or device to act as the same player; (2) claimed the wrong player by mistake: ask the admin to unclaim it, then claim the right one. The accepted gaps stay in PATCHES.md and the limitations list in short form: anyone with the player token can claim any unclaimed character; same-name characters on two accounts look alike.

UI, decided 2026-11-21: no separate settings section. The claim flow lives in the right-click pin popup, which already has the Ping button beside Add pin (markers.js), so it covers pins and pings; the base popup gets the same flow (AC #11). With require_auth on: middle-click with no claim does not ping and shows a toast pointing to right-click to claim (proposed; user may prefer silent); with a claim, middle-click pings as the claimed player. Today webPing (app.js) posts api/ping with a prompted, remembered name and no client ID: with require_auth on it must send X-Munin-Client, and the server takes the name from the claim.

Naming, 2026-11-21: "key" is the backlog's term only. The page keeps upstream's "Pin key" label, and the wire keeps the `X-Munin-Client` header and the `client` query and body fields, unchanged unless there is a real need, so the fork deviates from upstream as little as possible.
<!-- SECTION:NOTES:END -->
