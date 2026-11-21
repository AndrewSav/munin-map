# Changes from upstream

This fork ([AndrewSav/munin-map](https://github.com/AndrewSav/munin-map), branch `patches`) is
Munin Map ([cocopuff2u-gaming/munin-map](https://github.com/cocopuff2u-gaming/munin-map)) with
the product changes below on top. Each is rebased onto every upstream release.

Only changes to what the plugin and the map do are listed here. The fork's own tooling (the
local test server, CI, the backlog) is not. Each entry names the backlog task with the full
details, in `backlog/tasks/`.

## TASK-1: Spoiler toggle

With `reveal_all = true`, the left bar gets a **Settings** group with a **Spoilers** switch,
off by default for each viewer. Flipping it reloads the page, which comes back at the same view.

- **Spoilers off:** the map looks exactly as it does with `reveal_all = false`: fog of war, and
  only explored ground and what stands on it.
- **Spoilers on:** no fog, the whole generated world, and also the locations the game has
  planned but not built yet. These are boss altars, mini-boss lairs, dungeons, traders,
  camps & resources and landmarks.
  - A planned location's popup says it hasn't been generated yet.
  - Some locations get several candidate sites and are only ever built once: the traders and
    the Forge of Potential. For these, the popup says it is one of N possible sites.
- **With `reveal_all = false`,** nothing changes.

How it differs from upstream:
- Upstream `reveal_all` removes the fog for every visitor. Here, turning it on only makes
  the switch available, and each viewer decides.
- Upstream never shows locations the game hasn't built yet.
- No new settings.
- **Keeps the record of explored ground.** Upstream's `reveal_all` is meant for testing, and
  while it is on, the saved record of explored ground becomes "the whole world", so turning
  `reveal_all` off later leaves the map fully revealed. This fork makes `reveal_all` an everyday
  setting, so it always saves the real record. A world that has run with upstream's
  `reveal_all` can be repaired: stop the server, delete the world's `fog.png` and `fog.grid` in
  Munin's data folder, and start it again. Munin rebuilds the record from the zones players have
  visited, cartography tables and traces, with 64 m-square edges instead of exact trails.

## TASK-2: In-game day and time

A **Clock** switch in the left bar's **Settings** group, off by default and remembered in each
browser. When on, a chip at the left end of the bottom-left row (beside coordinates, biome and
wind) shows the game's day and time, such as "Day 205 · 06:34", with a moon at night.

- It uses the game's own clock: 06:00 is sunrise and 18:00 sunset, and the day number matches
  the stats panel's.
- It stands still while nobody is on the server (the game pauses its clock then), and runs fast
  while everyone sleeps and the game fast-forwards to morning.
- It is hidden while the page is disconnected, and on a server that doesn't send the time.

How it differs from upstream:
- Upstream has no clock on the map. Off by default, because the game itself shows none.
- The players update gains a `clock` field (world time, day length, day, time of day, night).
  Nothing existing changes. No new server settings.

## TASK-19: Steam IDs left out of the public stats

The public stats (`data/stats.json`) no longer give away players' Steam IDs. Nothing changes on
the page: it finds players by name, as before.

How it differs from upstream:
- Upstream's public stats give each player a `key` of `<SteamID64>/<character name>`. Here the
  public stats leave `key` out, the way upstream already leaves out `playerId`.
- The stats Munin saves to disk keep the full key, so characters stay apart and existing stats
  files load as before. No new settings.

## TASK-20: Player access

Two new settings. At their defaults the server behaves as upstream:

- **`enable_sweep_api`** (default true): false makes `POST /api/sweep` and `/structures/refresh`
  admin-token only.
- **`require_auth`** (default false): true lets only **claimed players** place or remove pins,
  ping or rename or hide bases from the page, or ask for a world sweep by API. Viewing stays open.
  - Players claim their character from the right-click pin form or a base
    popup: pick it from the list of players Munin has seen, enter the **user key** (a long
    random string in `user.token` beside `announce.token`, shared by the admin; no file, no
    claiming). Players are expected to claim their main character: the claim only puts a name
    on pins, pings and base renames, so which of their characters they claim makes little
    difference.
  - The claim belongs to the browser's ID (upstream's **Pin key**, shown as **Browser ID** in the
    pin form and, new, in the base popup); entering it in another browser makes that the same
    player. One player per ID, one ID per player. A browser whose ID holds a claim only switches
    to another ID that holds one.
  - Pins and pings carry the claimed name: the pin form has no name field and middle-click no
    name prompt (the server ignores any name a request sends, and uses the name corresponding
    to the browser's ID instead). Middle-click without a claim says to claim first.
  - The admin unclaims from the players panel (**Claimed players**, in a browser with the admin
    key) or by API, for example after someone claimed the wrong player.
  - Accepted limitations: anyone with the user key can claim any unclaimed character;
    same-name characters on two accounts look alike; the Browser ID is a login
    that travels in a URL query and can land in access logs.

Also, whatever `require_auth` says:
- Who last renamed or hid each base, and when, is kept (`bases-edits.json`), and a renamed base's
  popup names who renamed it ("admin" for the admin token; nothing shown for an anonymous edit).
- `GET /api/bases` (admin token) lists every renamed or hidden base with that record. Upstream
  drops a base's custom name when it is hidden; the list keeps the last one as `previousLabel`,
  so the admin can restore it with `POST /api/base`.
- New endpoints: `data/claim.json`, `data/claimable.json`, `api/claim`, and admin-only
  `api/claims`, `api/unclaim`, `api/bases`.

How it differs from upstream:
- Upstream lets anyone who can reach the map pin, ping and edit bases under any name, and run
  sweeps. Its switches (`web_pins`, `web_pings`, `web_edit_bases`) keep their meaning here: off
  means off for everyone except the admin token holder; `require_auth` only adds the claim on top.
- Chat pins (`!pin`, `!undopin`, `!deletepin`) are unchanged: the game has already identified the player.
- The README and docs describe the admin token as a long random string rather than a word.

## TASK-22: Per-player web ping limit

Upstream limits web pings to one per visitor every 10 seconds (and 2 seconds between any two),
telling visitors apart by the connection's network address.

- **With `require_auth = true`:** the 10 seconds are per claimed player, so one player's ping
  doesn't make another wait.
- **With `require_auth = false`:** unchanged. Behind a reverse proxy every visitor arrives from
  the proxy's address, so there the limit is shared by everyone.

How it differs from upstream: upstream always keys the limit by network address. No new settings.
