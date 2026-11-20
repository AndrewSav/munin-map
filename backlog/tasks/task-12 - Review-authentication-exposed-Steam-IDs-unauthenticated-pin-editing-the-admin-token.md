---
id: TASK-12
title: >-
  Review authentication: exposed Steam IDs, unauthenticated pin editing, the
  admin token
status: Done
assignee:
  - '@claude'
created_date: '2026-11-18T09:50:00Z'
updated_date: '2026-11-20T20:32:00Z'
labels:
  - plugin
  - web
  - security
milestone: m-1
dependencies: []
type: spike
ordinal: 11000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Review what the map exposes to anyone who can open it, and what it lets them change. The map has no login: anyone who can reach it sees and can do everything it offers.

The user's current questions:
1. **Steam IDs are visible to everyone.** Players' Steam IDs appear on the map page or in its data. Is that necessary for anything the page does, or could the page work without them (or with an opaque ID)?
2. **Pin editing has no authentication.** With pin editing enabled (`web_pins`), anyone who can open the page can add, move or delete pins, and there is no setting to require a login or a token for it.
3. **What does the admin token do?** The plugin has a token (`announce.token` in the map's data folder appears to be one). Find out what it unlocks, how it is passed (an API call, a page setting), where it is documented, and whether it could also protect pin editing.

This is a review first. Record the findings and a proposal in this task and agree it with the user before changing anything. Changes to how authentication works would touch upstream code, so keep them rebase-friendly per decision-2.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Every place the page or its data shows or sends a Steam ID (or another platform ID) is listed, with what each use needs it for, and whether the page could work without it
- [x] #2 Everything a visitor can change without credentials is listed (pins, base names, anything else), with the settings that turn each on or off
- [x] #3 What the admin token does is written down: what it unlocks, how it is created and passed, where it is documented, and whether it could protect pin editing too
- [x] #4 The findings and a proposal are recorded in the task notes and agreed with the user (or the user decides to leave things as they are) before any change starts
- [x] #5 If implemented: `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
## Findings, 2026-11-20 (from the server code, src/Server/MapDataServer.cs, Announce.cs, WebPing.cs, Config.cs, Live/Stats.cs, MuninMap.cs; and a scan of every public response on a real server)

### The admin token
- It comes from a file, `announce.token`, in Munin's data folder (BepInEx/config/MuninMap) or next to the DLL. It is re-read when the file changes. With no file, or an empty one, everything token-gated is closed to everyone.
- It is sent as a request header (`X-Munin-Token` or `X-Announce-Token`) and compared as plain text. Over plain HTTP anyone on the path can read it. The page keeps it in browser storage once entered.
- What it unlocks:
  - Integration: `POST /announce`, a message of up to 300 characters on every player's screen and in the event feed. This was the token's original purpose, hence the file name.
  - Maintenance, done by hand or by script (no UI): `POST /api/rerender?zoom=N` redraws tiles after render settings or the renderer change; `POST /api/reexport` redoes 3D models, for example after running the tools/ texture and mesh extraction scripts; `POST /api/reload` drops cached web files and reloads every open browser.
  - Moderation, the counterpart of the open actions below: `POST /api/bases/reset` (undo every base rename and hide), removing anyone's pin (`/api/unpin`; the page offers it once the key is stored), and the owner column of `GET /pins`.
  - Admin reading: `GET /api/serverlog` (the page's Server log tab). The game's console names accounts and IP addresses.
  - Pins, pings and base edits even when their switches are off.
- The page uses the token in two places only: the Server log tab, and "Remove pin" on other people's pins. Everything else is documented for curl (README "Admin extras", docs/ADVANCED.md "HTTP API").

### What anyone can change without the token
| Action | Default | Setting | Limit |
|---|---|---|---|
| Place a pin, `POST /api/pin` | on | `web_pins` | one per 2 s per browser ID; `max_pins_per_user` (50) |
| Remove "own" pin, `POST /api/unpin` | on | `web_pins` | only pins carrying the same browser ID |
| Ping the map in game, `POST /api/ping` | on | `web_pings` | once per 10 s per IP address, plus a global limit |
| Rename or hide any detected base, `POST /api/base` (for everyone) | on | `web_edit_bases` | one per 2 s per browser ID |
| Start a world sweep now, `POST /api/sweep` and `GET`/`HEAD /structures/refresh` | always | none | none |
- The browser ID (`X-Munin-Client` header, `client` in the body or `?client=`) is whatever the request claims. So anyone can claim another browser's ID (remove their pins, if known or guessed) and get round the 2 s limits and possibly the pin cap by changing it. Base edits need no ownership at all.
- The websocket accepts one message, `"players"`, which asks for the player list. Read-only.

### Verbs and cross-site requests
- The server handles GET, HEAD and POST only; any other verb gets 501 from websocket-sharp before reaching a handler.
- 10 handlers require POST (`if (!post) return false`). `/structures/refresh` changes state on GET and HEAD with no check, so a link, an image tag or a prefetch can fire it. `/api/serverlog` answers GET and POST (a read).
- Every response carries `Access-Control-Allow-Origin: *`, and there is no OPTIONS handler. Token calls need a custom header, so a browser on another site must ask the server's permission first; that permission request gets 501, so they are safe from other sites. But pin, unpin, ping, base edits and sweeps can all be sent as simple cross-site POSTs (the browser ID can go in the body), which a browser sends from any website without asking. So any page a viewer opens can do those through the viewer's browser, even while the map is LAN-only.
- So the review treats every state change as reachable by any verb the server accepts, and from any website.

### The world sweep
- One time-sliced walk over every object in the world: about 3000 objects and 4 ms per server frame (`zdos_per_frame`, `frame_ms`), then sliced publishing. It rebuilds buildings, bases, trees, ruins, portals, tombstones, vehicles, 3D objects and terraformed zones, and queues tile re-renders. It runs a few seconds after load, then every 120 s (`sweep_interval`).
- `/api/sweep` and `/structures/refresh` end the wait early. Requests can't stack sweeps, but calling them continuously means sweeps back to back, forever: a constant share of every server frame, more garbage collection pauses, and map data re-downloaded by every page.
- No reason found for anyone but an admin to trigger it. The page never calls it, the map refreshes every 2 minutes anyway, and an integration that wants it can hold the token. `/api/sweep` is documented as public API; `/structures/refresh` is an old endpoint kept from Valheim WebMap.

### Account identifiers and other personal data
- Steam IDs: the only public response with them is `data/stats.json`. Each player's `key` is `<SteamID64>/<character name>`, built from the connection's host name (Players.KeyOf). The page never reads `key` (it matches players by name), so it is avoidable. The server needs it only to keep characters apart in its own saved stats, which are written separately (WritePlayer full: true), and upstream already leaves `playerId` out of the public response.
- Not public: IP addresses, Xbox, PlayFab or other platform IDs (none in the 20 public responses scanned). The players update's `id` is the per-connection ID; `platform` only says "Steam". The token-only server log has Steam IDs and IPs. Chat-placed pins store the placer's Steam ID as owner in `pins.csv`: the public `/pins` blanks it and the scan found none in `data/pins.json`, but that server may simply have had no chat pins (to confirm).
- Personal but not an identifier, public in `stats.json`: an hour-of-the-week histogram of each player's play time (`week`), current session start, last seen, longest session, ping, chat count, deaths, AFK time, time at base. Together these show a player's real-life routine. Last-seen position is off by default (`show_last_seen_position`).
- Hidden players: public trails and heat maps skip them. With `always_map` on (default), their position still clears fog and feeds their totals but is never published as coordinates. Possible small leak, unconfirmed: their current biome (`lastBiome`) may keep updating while they are online.

### Pins: Munin, in-game and cartography tables
- In-game pins live in each player's character file on their own PC. The server never receives them; Munin can't show them, and Munin pins never appear in game.
- Cartography table pins live in the table, in the world save. Munin shows them read-only ("Cartography table pins", one per spot) and merges each table's explored map into its fog. It never writes to a table.
- Munin pins live only in Munin (`pins.csv`) and show only on the web map. They come from the page (owner `web:<browser ID>`) or from chat (`!pin [type] text`, `!undopin`, `!deletepin text`; owner the player's Steam ID). Chat only reaches the server while two or more players are online.
- Web pings are the one path from the map into the game: the ping shows on every player's map, sent to each player as if from their own account (to pass the game's permission check) under the visitor's chosen name plus "(web)". Once the map is public, anyone can put a ping on every player's screen.

### Behind a reverse proxy (with TASK-16)
- The ping limit counts by IP address. Behind a proxy every visitor arrives from the proxy's address, so they would all share one ping per 10 s. `X-Forwarded-For` is only used to log a visitor's address, in debug mode.

### Candidates for the proposal (not yet agreed)
- Sweep triggers behind the token, and `/structures/refresh` POST-only (or dropped); `/api/sweep` moved to the docs' token table.
- Leave the Steam ID out of the public `stats.json` (one line, as upstream does for `playerId`), or replace it with a per-server salted hash so scripts still have a stable ID.
- Personal data: keep the totals; drop or put behind a setting the hour-of-week histogram and session start times; stop hidden players' `lastBiome` updating.
- Cross-site requests: an origin or same-site check on every state-changing request, and narrowing `Access-Control-Allow-Origin: *` where it isn't needed.
- Pin and base ownership: a server-issued browser ID (for example a signed cookie) instead of a self-claimed one; a decision on whether base edits stay open, and on web pings when public.
- The token only over HTTPS once public (TLS at the edge).

Decisions on the privacy findings, 2026-11-20: stop publishing Steam IDs, and add a player_activity setting (default true) that hides the player card's Activity tab and its data. Both are in TASK-19. The rest of the privacy findings are accepted as they are: no change.

## More findings, 2026-11-20

### The browser ID behind web pins (correction to "what anyone can change")
- The page makes it up the first time it's needed (`clientId()` in web/js/layers/markers.js): 10 bytes from `crypto.getRandomValues`, written as 20 characters, about 80 random bits. It is kept in the browser's localStorage (`munin-client`). The server never issues, signs or records IDs; it takes whatever the request sends (`X-Munin-Client` header, `client` in the body, or `?client=`).
- Placing a pin stores its owner as `web:<ID>` in `pins.csv`. Listing pins (`data/pins.json?client=<ID>`) marks that browser's own pins `mine` and never sends owners (PinsJson: "a published owner id is all it takes to remove someone's pin"). Removing a pin needs a matching owner, or the admin token. Requests with no ID share the owner `web:anon` (the page always sends one). Chat pins use the player's Steam ID as owner.
- The page's "pin key" lets you copy the ID into another browser (`setClientId`), so other devices can manage your pins too.
- Correction: removing someone else's pin is not realistically possible. It needs their ID, which is about 80 random bits, never published, and seen only by that browser and the server. The earlier note "if known or guessed" overstated it.
- The real weakness is the other direction. Anyone can invent a new ID per request, so the 2 s limit and the 50-pin cap (`max_pins_per_user`, counted per owner, oldest dropped first) don't bound one visitor; base edits have the same per-ID limit and no ownership at all. The ID protects your pins from others but doesn't stop one visitor flooding pins or base edits.
- Minor: the ID travels in a URL query (`?client=`) on every pin list request, so it can land in proxy and server access logs; behind the reverse proxy, the edge's logs would hold every visitor's pin key.
- So the "server-issued browser ID" candidate is about limits, not ownership. What's missing is a cap a visitor can't reset by changing IDs, for example per IP address. Behind the proxy that means trusting the real visitor address from `X-Forwarded-For` (with TASK-16).

### Names on web pins and pings
- The pin form's "Your name" is free text (16 characters, remembered in the browser). The server only cleans it (letters, digits, spaces; "web" if empty) and shows it as "by <name>" in the pin's popup. Web pings take a name the same way ("Someone" if empty); the game shows it with "(web)" after it. Munin holds the real player names (the stats, the players update, NameOfPlayerId) but never checks pin or ping names against them. So a visitor can pin, or ping every player's game, under a real player's name. Only chat pins (`!pin`) carry a genuine name, taken from the game connection.
- Builder names on portals and bases (`by`) come from the game's records and can't be faked; the page shows them only on the player card (TASK-17 adds them to the popups).
- Options noted: mark web pins visibly as web, as pings are; turn web pins and pings off and keep chat pins only; or tie them to a login.

### The admin token's strength
- The whole file is read and trimmed (`File.ReadAllText(path).Trim()`): leading and trailing whitespace go, inner spaces and line breaks stay. A token with a line break can never match, because a header value can't contain one. The file is read as UTF-8; non-ASCII characters may not survive a header. It is re-read when the file's modification time changes. An empty or whitespace-only file means no token.
- No complexity requirements: any non-empty text works, even one character, and the README's "secret word" invites a short one. There is no limit on attempts, no lockout and no logging of failures: a wrong token just gets 403. The comparison is plain string equality (a theoretical timing leak, not practical over the internet). It is plain text in the header, the file and the browser's storage.
- Options noted: a docs note to use a long random token; a per-IP limit on failed attempts. No complexity rule (the user is not after one).

### Hiding bases, 2026-11-20
- A hide (or rename) is an override in `<world>/bases.json`, stored at the base marker's position (the centre of its cluster of buildings) and matched to a detected base by distance, nearest within 60 m (`MATCH_RADIUS`, Markers.FindOverride).
- A hidden base is left out of the markers entirely. So there is no marker or popup to undo it from, and no list of hidden bases anywhere on the page: for a visitor a hide is effectively one-way.
- Undoing: `POST /api/base` with only `{x, z}` removes the nearest override within 60 m. It is open to anyone while `web_edit_bases` is on, else it needs the token. It has no UI, but the base's buildings stay drawn, so a point near their middle can be read from the coordinate readout. It answers `{"ok":true}` even when nothing was within 60 m, and if the nearest override is another base's rename, that is what gets removed. `POST /api/bases/reset` (token) clears every rename and hide. By hand: edit or delete `bases.json` with the server stopped.
- Overrides record no author and no time.
- With base edits open to everyone and no ownership, hiding is the strongest vandalism tool on the page.
- Tried on a real world: a hidden base was restored with `POST /api/base` and the token, and the same override in the test server's copy was cleared with `/api/bases/reset` after creating a test token.

Proposal agreed with the user, 2026-11-20:
- privacy changes are TASK-19 (stop publishing Steam IDs; a player_activity setting);
- access control is TASK-20 (require_auth with claimed players and a player token; enable_sweep_api, with the admin token still working; docs advice for long random tokens; base overrides recording who and when; GET /api/bases for the admin);
- accepted as they are: the other privacy findings, no limit on failed token attempts, the client ID in URL queries, one person claiming several players, and cross-site requests with require_auth off. Chat pins and the existing web_* switches are unchanged.
Related: TASK-16 (behind the reverse proxy), TASK-17 (builder names in popups), TASK-18 (the Discord online message includes the server password).

Follow-up, 2026-11-21: TASK-19 was cut to the Steam IDs only. The player_activity setting was dropped: the public event feed and polling who is online give the same information.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Review done. Every place a Steam ID or other account ID is published, everything a visitor can change without credentials, what the admin token unlocks and how strong it is, the world sweep, verbs and cross-site requests, pins (Munin, in-game and cartography table), the browser ID, names on pins and pings, and hiding bases are all recorded in the notes, from the code and from scanning a real server's public responses.

Outcome, agreed with the user: TASK-19 (privacy) and TASK-20 (access control), both in Internet readiness, with the accepted limitations listed in the notes. Nothing was implemented in this task. Criterion 5 ("if implemented: PATCHES.md") does not apply here: TASK-19 and TASK-20 carry their own PATCHES.md criteria.
<!-- SECTION:FINAL_SUMMARY:END -->
