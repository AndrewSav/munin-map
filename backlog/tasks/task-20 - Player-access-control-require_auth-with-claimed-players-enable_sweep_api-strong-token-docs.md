---
id: TASK-20
title: >-
  Player access control: require_auth with claimed players, enable_sweep_api,
  strong-token docs
status: To Do
assignee: []
created_date: '2026-11-20T07:11:00Z'
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
From TASK-12's findings. Today anyone who can reach the map can place pins and ping the game under any name, rename or hide anyone's base (and a hidden base has no way back from the page), and trigger world sweeps without limit. A visitor can also invent a new browser ID per request, which gets round the per-ID limits. Before the map is exposed to the internet, the server should be able to require that these actions come from a known player. The design below was agreed with the user on 2026-11-20.

**Admin token (docs only):** describe the admin token, and the new player token, as a strong secret, a long randomly generated string, instead of a "secret word". No complexity rules and no limit on attempts.

**Sweep API:** a new setting `enable_sweep_api` (default true) turns `POST /api/sweep` and `/structures/refresh` on or off. When off, the admin token still works, as with `web_pins`, `web_pings` and `web_edit_bases`.

**Player access:** a new setting `require_auth`, default false. When false, everything behaves exactly as now. When true, no action that changes something is available without a claimed player:
- A **player token**, a secret like the admin token that the admin shares with their players, lets a browser **claim** an unclaimed player from the players Munin knows. The server records the browser's client ID against that player. One client ID per player: claiming a claimed player is refused.
- The claim belongs to the client ID. Another device of the same person uses the same key (the "pin key" the pin popup already shows), with no second claim. With `require_auth` on, that key is effectively the player's login.
- The **admin can unclaim** a player in the players list, for example after a lost key, so the player can claim again.
- With `require_auth` on, pins, pin removal, pings, base renames and hides, and `/api/sweep` and `/structures/refresh` refuse unclaimed client IDs. The admin token is accepted as today. A pin's "by" and a ping's name are the claimed player's name, never free text.
- **Pin and ping UI:** it still offers entering an existing key, as today. If the browser has no claim, it offers a dropdown of unclaimed players and a field for the player token, with a note that if your name isn't listed, you need to join the game server first (Munin only knows players it has seen online).
- **Bases:** the rename, hide and unhide UI gets the same claim flow. Base overrides record who renamed or hid a base and when, and a renamed base's popup names who renamed it. `GET /api/bases` (admin token) lists every override: name, coordinates, hidden or not, who hid or renamed it if known, and when it was last hidden and last renamed. There is no page list: unhiding stays `POST /api/base` with x and z plus the admin token.

**Unchanged:** the existing switches keep their meaning: off means off for everyone except the token holder, and `require_auth` only adds the claim requirement on top. Chat pins (`!pin`, `!undopin`, `!deletepin`) stay as they are: the game has already authenticated the player.

**Accepted limitations** (the user's decisions):
- one person can claim several players;
- the client ID travels in a URL query (`data/pins.json?client=`), so it can appear in proxy and server access logs, and whoever can read those can act as those players. Moving it to a header would add hunks in two busy upstream files for little benefit;
- with `require_auth` off, other websites can still trigger the open actions through a visitor's browser, as today. With it on they can't, because the client ID is in browser storage, not a cookie.

Do it in three parts, each its own commit under this task's ID: settings, docs and the sweep switch; claiming (player token, claim and unclaim, the claim checks, base override records, `GET /api/bases`); the UI.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 README and `docs/ADVANCED.md` describe the admin token and the player token as a strong secret, a long randomly generated string, not a "secret word"
- [ ] #2 `enable_sweep_api` (default true) is in the config with a description: when false, `/api/sweep` and `/structures/refresh` are refused unless the admin token is sent; when true they behave as today (subject to `require_auth`)
- [ ] #3 `require_auth` (default false) is in the config with a description; with it false, every action and the whole UI behave exactly as upstream
- [ ] #4 A player token (a secret file like the admin token; none means claiming is closed) lets a browser claim one unclaimed known player; the server keeps the client ID against that player, and refuses to claim a player that is already claimed
- [ ] #5 A second browser using the same key acts as the same claimed player, with no second claim
- [ ] #6 The admin can unclaim a player from the players list (admin token), after which the player can be claimed again
- [ ] #7 With `require_auth` true, pin, unpin, ping, base rename, hide and unhide, `/api/sweep` and `/structures/refresh` refuse unclaimed client IDs (the admin token is accepted as today), and a pin's "by" and a ping's name are the claimed player's name, never free text
- [ ] #8 Base overrides record who renamed or hid a base and when; a renamed base's popup names who renamed it
- [ ] #9 `GET /api/bases` (admin token only) lists every base override: name, coordinates, hidden or not, who hid or renamed it if known, and when it was last hidden and last renamed
- [ ] #10 With `require_auth` true, the pin and ping UI offers entering an existing key, and, when the browser has no claim, a dropdown of unclaimed players plus a player token field, with a note that a player not listed must join the game server first; the free-text name is gone
- [ ] #11 With `require_auth` true, the base popup uses the same claim flow for rename, hide and unhide
- [ ] #12 `web_pins`, `web_pings` and `web_edit_bases` keep their meaning (off for everyone except the token holder), and chat pins work as before
- [ ] #13 The accepted limitations (one person can claim several players; the client ID in URL queries and access logs; the key as a login; cross-site requests with require_auth off) are written in the docs
- [ ] #14 Tested on the local test server with require_auth off (as upstream) and on (claim, actions, unclaim, a second device with the same key, refusals without a claim)
- [ ] #15 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [ ] #16 Written per decision-2 in three parts (settings and docs; claiming; UI), each a commit starting with the task ID; new behaviour in new files, upstream edits marked where the format allows
<!-- AC:END -->
