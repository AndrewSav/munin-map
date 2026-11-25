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
- Chat pins (`!pin`, `!undopin`, `!deletepin`) are unchanged: the game has already identified the
  player.
- The README and docs describe the admin token as a long random string rather than a word.

## TASK-22: Per-player web ping limit

Upstream limits web pings to one per visitor every 10 seconds (and 2 seconds between any two),
telling visitors apart by the connection's network address.

- **With `require_auth = true`:** the 10 seconds are per claimed player, so one player's ping
  doesn't make another wait.
- **With `require_auth = false`:** unchanged. Behind a reverse proxy every visitor arrives from
  the proxy's address, so there the limit is shared by everyone.

How it differs from upstream: upstream always keys the limit by network address. No new settings.

## TASK-15: World seed in Server details

A new server setting, **`show_seed`** (default false). With it on, the stats panel's **Server
details** gets a last line, **Seed**, with the world's seed, so players can look the world up on
seed-map websites without asking. The seed reveals the whole world on those sites, so it is a
spoiler and off by default. With it off, the seed is not sent to the page at all (the config
carries it only when the setting is on). It is not secret from players on the server, though: the
game's own `printseeds` console command prints it (F5, with the `-console` launch option).

How it differs from upstream: upstream never shows or sends the seed.

## TASK-3: Player trails kept across restarts

The activity layer's player trails (the last 3 or 24 hours of where each player went) now
survive a server restart. They are saved to `trails.txt` in the world's folder, beside
`activity.txt`, every `stats_save_interval` (60 s by default) and at shutdown.

- **Clean shutdown:** everything recorded up to then is kept.
- **Crash or killed process:** what was recorded since the last save is lost.
- **On start:** points older than 24 hours are dropped, and so are players listed in
  `hidden_players`. A missing, empty or unreadable file means starting with no trails and a
  warning in the log.

The file holds each player's key (which can be a Steam ID) and name, as `stats.json` does. It
stays on the server and is not sent to the page.

How it differs from upstream: upstream keeps trails in memory only, so every restart empties
them. No new settings.

## TASK-17: Who built a portal or base

A portal's popup and a player base's popup (the editable one included) get a line **Built by
<name>**. A base's builder is the player who placed most of its pieces. The name is the
character's, from the game's own record of who placed the piece, so visitors can't fake it. It
is shown only for a player the map has seen (one in the stats) who is not in `hidden_players`;
otherwise the popup has no builder line.

How it differs from upstream: upstream sends the builder too, but shows it only on the player
card's list of what a player built, not in the popups. No server change, no new settings.

## TASK-18: Telegram group posts

Munin can post to a Telegram group what upstream posts to a Discord webhook: the server going
online and offline, players joining and leaving, and raids and invasions. Optionally it relays
the game's chat (shouts, or all chat). A bot made with @BotFather posts; nothing written in the
group reaches the game. Setup and settings: docs/ADVANCED.md, "Telegram".

- **Online:** the server's name and address, with buttons to copy the address and, when turned
  on, the server password and the user key, and a button to the map. The address can be set
  (`telegram_post_address`) for a DNS name or a forwarded port.
- **Offline:** the online post is struck through and loses its buttons, and an offline post
  follows. Only on a proper shutdown; Munin waits at most 3 s for it.
- **Raids and invasions:** with a button to the spot on the map.
- **Chat:** shouts, or all chat (`telegram_chat_scope`); whispers never. A player's game sends
  chat to the server only when other players are online to receive it, so a player alone sends
  no chat to post.
- **Players in `hidden_players`** are left out of every Telegram post, including the names near
  a raid.
- **Which kinds are posted** (`telegram_post`: online, offline, join, leave, raid, chat) and which
  arrive without a notification sound (`telegram_post_silent`, the same names) are set per kind.
  Without offline the online post stays as it is at shutdown; without online the offline post is
  sent on its own.
- Posts go out in the background, so a slow or unreachable Telegram never holds up the game.

How it differs from upstream:
- Upstream has Discord only. Discord is unchanged here and works as before, alone or with
  Telegram.
- New settings: `telegram_bot_token`, `telegram_chat_id`, `telegram_topic_id`,
  `telegram_post`, `telegram_chat_scope`, `telegram_post_address`,
  `telegram_post_password`, `telegram_post_user_key`, `telegram_post_silent`.
- The Telegram posts differ from the Discord ones: the password is posted only when turned on,
  the port shown is read from the game's `-port` (the Discord post shows `:1`), hidden players
  are never named, and chat can be relayed (Discord gets no chat).

## TASK-4: Predicted ore deposits

With `reveal_all = true` and the new setting `predict_ore = true` (default false), the map shows
where ore deposits will be in zones nobody has visited yet, to viewers who have turned Spoilers on.
The game places ore from the world seed and its own rules when a zone is first generated, so Munin
works the deposits out in advance: inside the game server, it re-runs the game's placement rules for
each zone not generated yet, with the server's own game data (what is placed where, prefabs and
their colliders, the planned locations). A whole world usually takes under a minute, on two
lowest-priority background threads (longer on a slow or busy server; reading the location prefabs it
needs costs about 100 MB of memory); zones a viewer zooms into go first; a restart resumes; after a
game update it works the zones out again by itself. About 99% of deposits match the real ones; near
locations it can be wrong, since which way a location faces is often not decided by the seed. Its
rules are copied from the game's code and verified for Valheim 1.0.30.

On the map, real and predicted ore are one layer, driven by the Ore row:
- **Style**, in the Ore row's checklist, remembered per browser: *Classic* (the default, upstream's
  small filled dots; predicted ones hollow) or *Shapes* (a shape per ore, filled for real and hollow
  for predicted, easy to see zoomed out; ore inside Sunken Crypts is shown in Classic only).
- **Predicted ore**, in the same checklist, shows or hides the predictions; the per-ore checkboxes
  and the Ore switch apply to both.
- A generated zone shows only its real deposits. One a player generates keeps its predictions until
  Munin's sweep has its real ones, so the spot is never empty in between.
- Server details get rows for the server's game version and the predicted deposits, the progress
  while a prediction is being worked out, and a Verified on row with a note when the server runs
  another game version.

How it differs from upstream:
- Upstream shows no predicted ore. New setting `predict_ore`; new endpoints `data/ore.json` and
  `data/predicted-ore.json`; a new file, `predicted-ore.csv`, in the world's data folder.
- Munin runs two background threads at the lowest priority (on Linux through `setpriority`) and
  loads location prefabs in the background. Every tile a browser asks for is passed on to it (one
  line in upstream's `TileStore.Get`), so it can work those zones out first.
- On the WebGL map the fork draws the ore itself (upstream's GPU ore dots are switched off while it
  does), in upstream's look by default. The 3D view and the non-WebGL map keep upstream's ore and
  show no predictions.
- A fork-owned `src/Directory.Build.targets` adds a reference to Unity's physics module; upstream's
  project files are unchanged.

## TASK-32: The fork's name, version and releases on the page

The page names the fork: the GitHub button links to the fork's repository ("Munin Map Patches
3.4.0-17 on GitHub"), and Server details show "Munin Map Patches" with the fork's release. The
update check (`check_updates`) follows the fork's releases instead of upstream's, so the "is out"
notice and its link point at the fork's next release.

How it differs from upstream: upstream links to, shows and checks its own repository and version.
Upstream files get marked one-line edits; the rest is in `src/Patches/ForkVersion.cs`.

## TASK-36: Loads on Valheim 1.0.30 (System.Memory merged into the DLL)

Valheim 1.0.30 stopped shipping `System.Memory.dll` and `System.Runtime.CompilerServices.Unsafe.dll`.
The fork merges both into `MuninMap.dll` with the other libraries, so ImageSharp loads: WebP tiles
work, a WebP tile can still be served as PNG, and HarmonyX no longer logs a
`ReflectionTypeLoadException` for MuninMap at startup.

How it differs from upstream: upstream leaves those two DLLs out of the merge because older
versions of the game had them. Upstream files get a marked two-line edit in `src/ILRepack.targets`.
