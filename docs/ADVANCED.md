# Munin Map: advanced

Everything the [README](../README.md) leaves out: every setting, your own
markers, the HTTP API, server load, and building from source.

* [All settings](#all-settings)
* [Your own markers](#your-own-markers)
* [Where things are kept](#where-things-are-kept)
* [Server load and disk](#server-load-and-disk)
* [Textures and meshes for 3D](#textures-and-meshes-for-3d)
* [Pins and bases](#pins-and-bases)
* [Player access](#player-access)
* [Exporting a 3D scene](#exporting-a-3d-scene)
* [HTTP API](#http-api)
* [Caches and Cloudflare](#caches-and-cloudflare)
* [Building from source](#building-from-source)
* [Trying it without a real server](#trying-it-without-a-real-server)

## All settings

File: `BepInEx/config/com.cocopuff2u.muninmap.cfg`. Read at server start only.

| Section | Key | Default | What |
|---|---|---|---|
| Server | `server_port` | 3000 | HTTP port |
| Server | `map_title` | (server name) | title on the page |
| Server | `enable_3d` | true | offer the 3D view |
| Server | `map_url` | | the map's address, used in Discord posts and in game |
| Server | `discord_webhook` | | Discord webhook URL for events |
| Server | `discord_world_events` | true | also post raids and invasions, with a link to the spot |
| Server | `discord_invite_url` | | Discord invite shown on the page |
| Server | `telegram_bot_token` | | Telegram bot token, to post the same notices to a Telegram group; see [Telegram](#telegram) |
| Server | `telegram_chat_id` | | the Telegram group to post to |
| Server | `telegram_topic_id` | | optional: the topic to post in, for a group with topics |
| Server | `telegram_world_events` | true | also post raids and invasions to Telegram, with a button to the spot |
| Server | `telegram_post_chat` | off | relay the game's chat to Telegram: `off`, `shouts` or `all` |
| Server | `telegram_post_address` | | the address on the online post; empty: detected |
| Server | `telegram_post_password` | false | a button to copy the server password on the online post |
| Server | `telegram_post_user_key` | false | a button to copy the user key on the online post |
| Server | `telegram_post_silent` | | kinds of Telegram post sent without sound: `online`, `offline`, `join`, `leave`, `raid`, `chat` |
| Server | `event_log` | true | write events to `events.jsonl` |
| Server | `max_messages` | 100 | how many events to keep in memory for the page |
| Server | `show_vehicles` | true | report boats and carts |
| Server | `tile_webp` | true | store tiles as lossless WebP (25-35% smaller than PNG) |
| Server | `websocket_compression` | false | permessage-deflate on the live feed. Some proxies (IIS ARR) drop every frame with it on |
| Server | `legacy_map` | true | build one big `map.png` for `/map` |
| Server | `cache_server_files` | true | keep web files in memory. `false` for web development |
| Server | `debug` | false | extra log lines |
| Interval | `player_update_interval` | 1 | seconds between player updates to the page |
| Interval | `update_fog_texture_interval` | 2 | seconds between fog updates |
| Texture | `explore_radius` | 110 | metres of fog cleared around a player (the in-game map uses 100) |
| Texture | `reveal_visited` | true | at start, clear the fog everywhere the world save shows players have been |
| Texture | `reveal_visited_margin` | 4 | how tightly that follows players; lower is wider. 4 matched recorded in-game maps best |
| Texture | `default_zoom` | 100 | starting zoom of the page |
| User | `always_map` | true | clear fog where hidden players walk (their position stays hidden) |
| User | `always_visible` | false | ignore players' "hidden" setting |
| User | `show_last_seen_position` | false | offline players' last position in stats |
| User | `web_pins` | true | let the page place pins (right click, long press) |
| User | `max_pins_per_user` | 50 | pins per browser; the oldest go first |
| User | `web_edit_bases` | true | let the page rename or hide bases |
| User | `base_min_pieces` | 30 | pieces in one spot before it counts as a base |
| User | `base_min_per_cell` | 6 | pieces in a 32 m cell before it joins a base |
| Render | `render_threads` | 1 | threads drawing tiles (0 = on the game thread, slowly) |
| Render | `prerender_zoom` | 5 | draw the whole world up to this zoom at first start |
| Render | `max_render_zoom` | 7 | closest zoom over explored ground (7 = 1 m per pixel) |
| Render | `height_max_zoom` | 7 | closest zoom for height tiles (3D) |
| Sweep | `sweep_interval` | 120 | seconds between walks over the world's objects |
| Sweep | `zdos_per_frame` | 3000 | objects looked at per game frame, at most |
| Sweep | `frame_ms` | 4 | milliseconds of each game frame a walk may use while players are on |
| Markers | `reveal_all` | false | adds a **Spoilers** switch to the page (left bar, Settings), off by default for each viewer. With it on: no fog, the whole world, and the places the game has planned but not built yet. Tiles for unexplored ground render as people browse, so the map data grows |
| Models | `export_models` | true | export the game's models for 3D |
| Models | `object_categories` | piece,other,rock,bush,tree | what the 3D view and export get |
| Models | `use_textures` | true | textures on 3D models (off: flat colours) |
| Models | `extract_meshes` | true | read locked meshes out of the game files (off: boxes) |
| Models | `texture_max_size` | 512 | longest texture edge |
| Models | `export_ms_per_frame` | 6 | game-thread milliseconds per frame for model export |
| Server | `data_path` | | where the map keeps its data: a folder, absolute or under BepInEx. Empty: `BepInEx/config/MuninMap`. A map in the old place (`map_data` beside the plugin) is moved there once, when the new folder is empty |
| Server | `preview_image` | | picture for the link preview card (Discord and other apps): PNG, JPEG, WebP or GIF up to 8 MB, a path (under BepInEx if relative; `$VAR`, `%VAR%` and `~` expanded) or a URL |
| Server | `preview_description` | | text on that card. Empty: "Live map of" and the title |
| Server | `check_updates` | true | ask GitHub for the latest release a minute after start, then every 12 hours; a newer one is logged and marked on the page. Off: the server never contacts GitHub |
| Server | `show_github_link` | true | the GitHub button in the page's top bar (with a dot when an update is out). Off hides the button only: the "Munin Map x.y.z is out" note still shows when `check_updates` finds one |
| Server | `test` | false | test only: sends every location found by a Vegvisir or the like to all players, not just the finder |
| User | `web_pings` | true | let the page ping the game (middle click, or Ping in the pin form): once every 10 s per address, 2 s between any two |
| User | `hidden_players` | | character names, comma separated, any case, left off the live map and player count, the stats and player cards, the event feed and trails, and not named as a base's or portal's builder. Still named in Discord join and leave posts, the in-game leave message, and on their beds and tombstones |
| Interval | `save_fog_texture_interval` | 30 | seconds between saves of the explored area (`fog.png`), when it changed |
| Interval | `stats_save_interval` | 60 | seconds between saves of `stats.json` and the activity trails |
| Texture | `texture_size` | 2048 | pixels across the legacy images (`/map`, `/structures`, `/forest`); also how an old `fog.png` with no `fog.grid` beside it is read. Probably don't change |
| Texture | `pixel_size` | 12 | metres per pixel of those images (and of an old `fog.png`); also how finely explored ground is checked for missing close-zoom tiles at start. Probably don't change |
| Render | `main_thread_rows_per_frame` | 24 | with `render_threads = 0`, or when the game refuses sampling off its thread: tile rows sampled per game frame (4 at least) |
| Sweep | `first_sweep_delay` | 5 | seconds after the world loads before the first walk over its objects; buildings and structures are empty on the map until it has run |
| Sweep | `second_sweep_delay` | 30 | seconds after the world loads for one extra walk, to catch what was still loading. 0 (or not more than `first_sweep_delay`) turns it off |
| Debug | `perf_report` | false | every 5 minutes, log how much of the game thread each of Munin's jobs takes |
| User | `require_auth` | false | only claimed players may pin, ping or edit bases from the page, or ask for a sweep by API; see [Player access](#player-access) |
| Sweep | `enable_sweep_api` | true | let anyone ask for a world sweep now (`POST /api/sweep`, `/structures/refresh`). Off: only with the admin token |
| Server | `show_seed` | false | show the world's seed in the stats panel's Server details, for seed-map websites. The seed reveals the whole world there (a spoiler); off, it is not sent at all. Players on the server can get it anyway: the game's `printseeds` console command (F5, with the `-console` launch option) prints it |

## Your own markers

Put a `markers.json` in the world's folder:
`BepInEx/plugins/MuninMap/map_data/<world>/markers.json`. It's read again
whenever it changes.

```json
{ "sets": [
  { "id": "mines", "label": "Mines", "markers": [
    { "x": 2210, "z": 980, "label": "Silver mine", "icon": "mine", "description": "north face" }
  ] }
] }
```

Icons: `pin dot fire mine house cave boss trader dungeon camp village ruin runestone wreck portal tombstone boat cart poi spawn`.

## Where things are kept

Everything is under `BepInEx/plugins/MuninMap/map_data/`. Keep it when you
update.

| Path | What |
|---|---|
| `<world>/tiles/` | the drawn map |
| `<world>/fog.png` | explored area |
| `<world>/stats.json` | player and world stats |
| `<world>/trails.txt` | player trails of the last 24 hours, so a restart keeps them |
| `<world>/events.jsonl` | the events log |
| `<world>/bases.json` | base names you set |
| `<world>/markers.json` | your own markers (you make this one) |
| `<world>/*-cache.*` | caches so a restart serves the map at once |
| `models/` | 3D models, textures and meshes from the game files |
| `runestones.txt` | which locations hold runestones and Vegvisirs |

To start a world's map over, stop the server and delete its folder.

## Server load and disk

Map drawing runs on its own thread (`render_threads`, low priority). On the
game's thread the mod only does small slices of work:

* a walk over the world's objects every `sweep_interval` seconds, at most
  `frame_ms` milliseconds per frame while players are on
* a player update once a second
* fog updates
* model export, within `export_ms_per_frame`

Disk: the whole world at 1 m per pixel would be a few hundred MB, but close
zooms are only drawn where players have been, so a normal world is tens to
hundreds of MB. `max_render_zoom = 6` cuts that by four.

Upload: tiles are WebP and cached by browsers for good. A repeat visit loads
a few KB.

## Textures and meshes for 3D

A dedicated server doesn't let mods read most textures and meshes, so Munin
Map reads them straight out of the game's own files instead. It does this
once per game version, on a low-priority background thread, a few minutes
after the first start. Log:

```
MuninMap: extracting 142 textures from the game files in .../valheim_server_Data
MuninMap: 139 of 142 textures extracted from 61 files in 74s, 3 not found
MuninMap: extracting 1900 meshes from the game files in .../valheim_server_Data
MuninMap: 1880 of 1900 meshes extracted from 800 files in 180s, 20 not found
```

These are the game's own files. They're never in this repo or the zip, and
only ever sit in your `map_data/models/` folder. Only the finished `.glb`
models go to browsers. `use_textures = false` and `extract_meshes = false`
turn this off.

To do it by hand (plain Python 3, no packages):

```
python3 tools/extract_textures.py <valheim_server_Data> <plugins>/MuninMap/map_data/models
python3 tools/extract_meshes.py <valheim_server_Data> <plugins>/MuninMap/map_data/models
```

## Pins and bases

**From the page**: right-click the map (long press on a phone), pick a type,
write a label. Only the browser that made a pin can remove it.
`web_pins = false` turns this off.

**From chat**: `!pin [type] [text]` (types `dot`, `fire`, `mine`, `house`,
`cave`), `!undoPin`, `!deletePin [text]`. The game sends chat from player to
player, so the server only sees it while two or more players are online.

**Bases** are found from what's built. Click a base marker to rename it, hide
it, or go back to the automatic name (a nearby portal's tag, else "Base").
Saved in `bases.json`. If a lone workbench shows up as a base, raise
`base_min_pieces`.

## Player access

With `require_auth = true`, only **claimed players** may place or remove pins,
ping or rename or hide bases from the page, or ask for a world sweep (`POST /api/sweep`). Viewing the
map stays open to anyone who can reach it. The admin token works as always.

**Setting up:** make a file called `user.token` beside `announce.token`
(`BepInEx/config/MuninMap`) with the **user key** in it, a strong secret: a long randomly
generated string, such as one from a password manager or `openssl rand -hex 32`,
not a word. Share it with your players. Without the file, nobody can claim.

**Claiming:** right-click the map (or open a base) and pick your character from
the list, enter the user key, then **Claim**. If your name isn't listed,
join the game server first: the map only knows players it has seen online.
Claim your main character: the claim only puts your name on
pins, pings and base renames. Once claimed, the pin form shows your name in
place of the name field, and middle-click pings as you without asking for one.

**Your Browser ID:** the claim belongs to your browser's ID, shown as **Browser
ID** in the pin form and the base popup (with `require_auth` off, the pin form
calls it **Pin key**). Keep a copy of it. To act as the same player in another
browser or on another device, paste it there in **Browser ID** and press
**Use ID**: no second claim is needed. A browser that has claimed a player only
accepts another ID that holds a claim too, so it can't start over with a fresh one.

**Claimed the wrong player?** Ask the admin to unclaim it, then claim the right
one. The admin unclaims from the players panel, under **Claimed players** (shown
in a browser where the admin key has been entered, as for the server log), or
with `POST /api/unclaim`.

**Bases:** who last renamed or hid each base, and when, is kept in
`bases-edits.json`, and a renamed base's popup names who renamed it.
`GET /api/bases` (admin token) lists every renamed or hidden base with that
record. Hiding a renamed base drops its name, so the list keeps the last one as
`previousLabel`. To unhide a base, or give it back its name, send
`POST /api/base` with `{"x": .., "z": .., "label": ".."}` (or just `x` and `z`
for the automatic name) and the admin token.

**Limitations, accepted to keep this simple:**
* Anyone with the user key can claim any character nobody has claimed yet.
* Two accounts can each have a character of the same name. The list tells them
  apart by when each was first seen; pins and pings show only the name.
* The Browser ID is effectively the player's login. It travels in the pin list's URL
  (`data/pins.json?client=`), so it can show up in proxy and server access logs,
  and whoever can read those can act as those players.

## Exporting a 3D scene

The download button in the top bar exports an area as glTF (Blender, Godot,
three.js), glTF with one object per node (Unreal, Unity), or an Unreal pack
with a heightmap. Fog applies: nothing unexplored leaves the server. See
[EXPORT.md](EXPORT.md) for each editor.

## HTTP API

| Path | Returns |
|---|---|
| `/tiles/map/{z}/{x}/{y}.png` | map tile. 404 + `X-Munin-Tile: pending` while drawing |
| `/tiles/height/{z}/{x}/{y}.png` | height tile, [Terrarium](https://github.com/tilezen/joerd/blob/master/docs/formats.md#terrarium) encoding |
| `/data/players.json` | players online: position, health, gear, state |
| `/data/markers.json` | every marker set |
| `/data/stats.json`, `/data/events.json`, `/data/pins.json` | stats, recent events, pins |
| `/data/structures/index.json`, `/data/structures/{cx}_{cz}.json` | built pieces per 256 m chunk |
| `/data/objects/index.json`, `/data/objects/{cx}_{cz}.bin` | placed objects per chunk (for 3D) |
| `/data/prefabs.json`, `/models/{hash}.glb` | the 3D model library |
| `/data/fog.png` | explored area |
| `/config`, `/api/status` | settings for the page, drawing and sweep status |
| `/map`, `/map.jpg` | the whole world as one image |
| `POST /api/sweep` | walk the world now (with `enable_sweep_api = false`, only with the token) |
| `POST /api/pin`, `POST /api/unpin`, `POST /api/base` | add or remove a pin, rename or hide a base (with `require_auth`, a claimed key) |
| `/data/claim.json?client=`, `/data/claimable.json`, `POST /api/claim` | this browser's claim, the players it may claim, claiming one ([Player access](#player-access)) |

Need the token (`X-Announce-Token: <secret>`, from `announce.token` next to
the DLL):

| Path | Does |
|---|---|
| `POST /announce` | the request body is shown on every player's screen |
| `POST /api/reload` | forget cached web files and refresh every open browser |
| `POST /api/rerender?zoom=N` | draw tiles again from zoom N up |
| `POST /api/reexport` | export every 3D model again |
| `POST /api/bases/reset` | forget every base rename and hide |
| `GET /api/serverlog` | the game's log lines (the page's Server log view) |
| `GET /api/claims`, `POST /api/unclaim` | the claimed players; unclaim one (`{"id"}` or `{"name"}`) |
| `GET /api/bases` | every renamed or hidden base, with who and when |

Websocket at `/ws`: JSON frames `hello`, `players`, `events`, `tiles`,
`world`, `ping`, `pin`, `rmpin`, `reload`.

Coordinates: the map grid is a 21,504 m square around the world's centre.
At zoom 7, one pixel is one metre; each zoom out halves that.

## Caches and Cloudflare

Every script and stylesheet URL carries a hash of the file, so a browser or
CDN can never mix old and new files. Only the page itself (`/` and
`/index.html`) must not be cached for long. Behind Cloudflare, bypass the
cache for those two, or purge after each update.

## Behind a reverse proxy<!-- patches: TASK-16 -->

Munin serves plain HTTP on `server_port`. For HTTPS, put a TLS-terminating
reverse proxy in front of it, at the root of its own host name (a sub-path
hasn't been tried). The proxy needs:

* **Everything forwarded as is:** the page, `/data/`, `/tiles/`, `/models/`,
  `/icons/` and `/api/`, with no path rewriting.
* **WebSocket upgrade on `/ws`.** Served over HTTPS, the page connects with
  `wss://` by itself. The server sends an update every second even with nobody
  online, so a normal idle timeout won't drop the connection, and the page
  reconnects by itself after a server restart.
* **`map_url` set to the public address**, so the link preview card and Discord
  links use it. Without it, Munin builds the address from `X-Forwarded-Proto`
  and `X-Forwarded-Host`, so the proxy must set those, replacing any the
  visitor sent.
* **Request sizes:** the requests Munin accepts carry small bodies. Pins, pings,
  base names and claims are well under 1 KB, and an announcement
  (`POST /announce`, admin only) is as long as the message.

Keep Munin's own port closed to the outside, so it's reachable only through
the proxy.

Web pings are limited per network address. Behind a proxy every visitor shares
the proxy's address, so with `require_auth = false` one ping per 10 seconds is
shared by everyone. With `require_auth = true` the limit is per claimed player.

## Building from source

How the project is laid out:

| Folder | What's in it |
|---|---|
| `src/` | the mod itself (C#): `MuninMap.cs` starts it, `Config.cs` holds the settings, `Server/` serves the page, `World/` and `Tiles/` read and draw the world, `Live/` tracks players and events, `Models/` makes the 3D models |
| `web/` | the map page browsers load (also built into the DLL) |
| `package/` | `manifest.json` (name, version) and `icon.png`, for the release zip and mod managers |
| `tools/` | texture and mesh extractors, a mock server, Docker files |
| `docs/` | these guides and the screenshots |
| `libs/` | build inputs (filled in by the build scripts) |

Needs the .NET SDK (8 or newer) and the Valheim dedicated server's files.

* **Linux / macOS**: `./build.sh --managed <server>/valheim_server_Data/Managed`
* **Windows**: `.\build.ps1` (finds the Steam "Valheim Dedicated Server"
  tool, or pass `-ValheimManaged <path>`)

Output: `dist/MuninMap-<version>.zip`. Add `--deploy <BepInEx/plugins>` (or
`-Deploy`) to copy it straight to a server.

**Releases** are built by GitHub Actions. Set the version in `package/manifest.json`
and `VERSION` in `src/MuninMap.cs`, add a section to `CHANGELOG.md`, then
push a tag:

```
git tag v3.0.1 && git push origin main v3.0.1
```

The workflow downloads the dedicated server with SteamCMD, builds, and
attaches the zip, a `SHA256SUMS` file and the changelog section to the
release.

**Changing only the web page?** Copy the files into `web/` next to the DLL,
then `POST /api/reload` with the token: every open browser refreshes, no game
restart needed. (A copy of the page is also built into the DLL, used when the
`web/` folder is missing.)

## Trying it without a real server

* `node tools/mockserver.js`: the page with a made-up world, fake players and
  fake events, on `http://localhost:3000`. No packages needed.
* `docker compose -f tools/docker-compose.demo.yml up -d --build`: the same
  as a small public demo site. No game files inside.
* `tools/docker-compose.test.yml`: a real dedicated server in Docker with a
  copy of your world. Set `reveal_all = true` in its config and turn on
  Spoilers in the page's Settings to see the whole map.<!-- patches: TASK-1 -->

## Telegram<!-- patches: TASK-18 -->

Munin can post to a Telegram group what it posts to a Discord webhook: the
server coming online and going offline, players joining and leaving, and
raids and invasions starting and ending. Optionally it relays the game's chat
too. It only posts: nothing written in the group reaches the game. Discord and
Telegram can be used together or apart.

**Setting up:**

1. In Telegram, talk to **@BotFather**: `/newbot`, pick a name, and copy the
   **token** it gives you (`123456789:ABC...`). The token is the bot: whoever
   has it can post as it, so keep it out of anything you share. Use a bot that
   nothing else reads.
2. Add the bot to your group.
3. Find the group's **chat ID**:
   1. In the group, send `/start@<your bot's username>`. A plain
      `@<bot> hello` is not enough: a bot only sees commands meant for it and
      replies to its own messages. Removing the bot and adding it back also
      works.
   2. Open `https://api.telegram.org/bot<token>/getUpdates` in a browser.
      The reply has `"chat":{"id":-100...,"title":"..."}`. That number is the
      chat ID.
   3. Still `"result":[]`? Something else is reading this bot's updates
      (another program with the same token), or the bot has a webhook set
      (`getWebhookInfo` shows its `url`).
4. Set `telegram_bot_token` and `telegram_chat_id`, and restart the server.
   For a group with topics, set `telegram_topic_id` to post in one topic.

If Telegram upgrades the group to a supergroup (it does when some settings
change), the chat ID changes to a new `-100...` number. Munin's log then says
which number to use.

**What it posts:**

* **Online:** the server's name and address, with buttons to copy the
  address and, if you turn them on, the password (`telegram_post_password`) and
  the user key (`telegram_post_user_key`, when `user.token` exists), and to
  open the map (when `map_url` is set). The address is detected (the public IP
  and the game port); set `telegram_post_address` to show a DNS name or a
  forwarded port instead: a host name alone gets the game port added, a
  `host:port` is shown as given. Anyone in the group can copy what the buttons
  hold, so turn the password and key on only for a group of your players.
* **Offline:** the online post is struck through and loses its buttons, and an
  offline post follows. This only happens when the server shuts down properly:
  a killed or crashed server never sends it (the same goes for Discord). Munin
  waits at most 3 seconds for it.
* **Joins and leaves,** and **raids and invasions** with a button to the spot
  on the map (`telegram_world_events`).
* **Chat**, with `telegram_post_chat`: `shouts`, or `all` for normal chat too.
  Normal chat only reaches players nearby in the game, so `all` shows it to the
  whole group. Whispers are never posted. A player's game sends chat to the
  server only when other players are online to receive it, so a player alone on
  the server sends Munin no chat, and none is posted.

Players in `hidden_players` are left out: no joins, leaves or chat of theirs,
and they're not named near a raid. With `telegram_post_silent` the kinds you
list arrive without a notification sound, for a busy group.

Posts are sent in the background, a few at a time, as Telegram allows; a
slow or unreachable Telegram never holds up the game. Failures are written to
the server log, at most once a minute.
