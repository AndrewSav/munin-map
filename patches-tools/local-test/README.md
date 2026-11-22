# Local test server

the Munin Map build from this checkout. Use it to test a change before it goes to the live
server, where every deploy means a restart.

Fork-only tooling: it lives on the `patches` branch, in this folder, so it never conflicts
with upstream on a rebase. Everything the scripts create goes in `data/`, `settings.psd1` and
`.env` (git ignores all three) or in the Docker volumes `munin-test_server` and
`munin-test_munin-data`.

## Needs

- A live server running `ghcr.io/community-valheim-tools/valheim-server` with BepInEx and
  Munin Map, with its image backups on (`BACKUPS=true`, the default).
- Docker Desktop, running.
- Windows OpenSSH (`C:\Program Files\OpenSSH`) with a key in the ssh-agent that logs in to the
  live server. The ssh that comes with Git Bash does not use the agent.
- The .NET SDK (8 or later), for the repo's `build.ps1`.
- The Valheim client, with a **throwaway character**. Your character is saved on your PC, not
  on the server, so anything that happens to it in the test world (items taken or lost,
  skills lost on death) carries over to the live server.

## Setup

Copy `settings.example.psd1` to `settings.psd1` and fill in the live server's ssh host and
its compose folder. Everything else (the world name, world modifiers and user ID) is read
from the live server's `docker-compose.yml` by `pull-save.ps1`.

## The loop

```powershell
.\patches-tools\local-test\pull-save.ps1   # now and then: fresh copy of the live world and its Munin data
.\patches-tools\local-test\deploy.ps1      # after each code change: build, deploy, restart (~42 s)
.\patches-tools\local-test\update.ps1      # when live updates Valheim, BepInEx or its image
```

Then, in Valheim: **Join game → Add server → `127.0.0.1:2456`**, with no password. The map is
at <http://localhost:3000>. To show up on it, tick **"Visible to other players"** on the
in-game map (`M`). Munin hides players who don't share their position, as on live.

`deploy.ps1 -NoBuild` restarts with the plugin that is already deployed.

None of the scripts asks before restarting: whoever is on the test server is disconnected.

## Two images

`compose.yml` has two services:

- **`valheim`**, the test server, built from `image/`. It is the live image with its startup
  replaced by `image/fast-start.sh`, which starts the already-merged server + BepInEx install
  directly. The live image's startup runs supervisord, an owner and permission pass over
  every file, steamcmd, and a BepInEx check and merge. It cost about 35 s on every start here.
- **`update`**, the live image as it is, with its own startup. Only `update.ps1` runs it, to
  let steamcmd update the server and BepInEx be merged in.

So the test server never updates itself. `deploy.ps1` prints the test server's Valheim and
BepInEx versions and base image next to live's (read over ssh) and warns about any
difference. To catch up, run `update.ps1`. If the base image differs, first change
`x-base-image` at the top of `compose.yml` to match live.

## What the scripts do

**`pull-save.ps1`** changes nothing on the live server. It:
1. Reads the live `docker-compose.yml` for `WORLD_NAME`, `SERVER_ARGS`, `PUID` and `PGID`,
   and writes them to `.env`, which `compose.yml` reads.
2. Finds the newest `config/backups/AUTOBACKUP-<world>-*` in the live compose folder. That is
   the image's daily copy, taken only from a committed save. It never copies the live world
   folder, because a copy taken mid-save can be broken.
3. Checks that the backup's `_main.N.ok` marker matches its `_main.N.db2`.
4. Copies it down with scp and checks the file count.
5. Copies the live Munin settings too, changing three lines:
   - `discord_webhook` is blanked, so the test server never posts to the real Discord.
   - `hidden_players` is blanked, so test characters show on the map even when live hides them.
   - `data_path` points at the `munin-test_munin-data` volume (see below).
6. Copies Munin's own data for the world: the top-level files of the world's folder in live's
   `config/bepinex/MuninMap` (stats, activity, trails, events, bases, claims, build history and
   caches; about 20 MB). Munin names builders, players and trails from these, so without them the
   test server can't name anyone who only played on live. Tiles, models and icons stay behind
   (hundreds of MB; the test server makes its own). A `.json` file that doesn't parse was caught
   mid-write on live: run it again. Only live's default `data_path` (empty) is supported.
   `-WorldOnly` skips this step and keeps the test server's own Munin data.
7. Stops the local server, if it is running, and swaps in the world and the settings. The Munin
   files replace the test server's own top-level files in `munin-test_munin-data`; its tiles stay.

**`update.ps1`**:
1. Rebuilds the `valheim` image, pulling the base image again.
2. Runs the `update` service until BepInEx has loaded, which means steamcmd has updated
   `data/server` and the merge is done, then removes it.
3. Copies the merged install (`data/server/bepinex`, about 2 GB) into the `munin-test_server`
   volume. From the volume the game starts about 9 s faster than from a Windows drive.
4. Prints the versions next to live's.

**`deploy.ps1`**:
1. The first time, or when the server volume is missing, runs `update.ps1`.
2. Builds with the repo's `build.ps1`, against the game assemblies in
   `data/server/server/valheim_server_Data/Managed`. The output is quiet unless the build fails
   or warns.
3. Copies only `MuninMap.dll` into `data/config/bepinex/plugins/`, as on live, so the web app
   is served from inside the DLL. It prints the start of the DLL's SHA-256.
4. Recreates the test server, whose start copies the plugin in. The world is saved on the
   stop.
5. Waits for "Game server connected" and for the map to answer, timing each step, then
   compares the versions with live.

## A fully generated world

A copy of live only has the zones live players have been to. For work that needs the rest
(Mistlands, Ashlands, Deep North, or comparing against every zone), there is a snapshot of
the same world with every zone generated, in `data/snapshots/<world>-generated-<date>/` (git
ignores `data/`): the world's save files in `world/`, and Munin's map tiles drawn for it in
`tiles/`.

**Use it:**

```powershell
.\patches-tools\local-test\use-snapshot.ps1                       # lists the snapshots
.\patches-tools\local-test\use-snapshot.ps1 <world>-generated-2026-11-22
.\patches-tools\local-test\deploy.ps1 -NoBuild
```

**Go back to live:** run `pull-save.ps1` as usual. It replaces only `worlds_local`, so the
snapshot stays.

**Why the tiles move with the world.** Munin draws trees, rocks and changed ground into its map
tiles at zoom 5-7, and the first sweep after a start only records what is there as a baseline,
so it never redraws tiles that were drawn for the other world. So `pull-save.ps1` deletes the
zoom 5-7 tiles and the vegetation and ground overlays on every pull (Munin redraws them for the
live copy in a few minutes), and `use-snapshot.ps1` puts the snapshot's own tiles back. Without
them, a full redraw of the generated world takes about 20 minutes on one render thread, though
tiles someone is looking at are drawn first. Zoom 0-4 have no vegetation and the worlds share
one seed, so they stay.

The generated world is much bigger than the live copy: about 4.1 million objects instead of
about 270 thousand, about 150 MB on disk. It loads in under a minute, and a manual save takes
about 17 s. Munin's sweeps and drawing have a lot more to do on it.

**How it was made** (2026-11-22, Valheim 1.0.21), in case it needs doing again:

1. Upgrade World 1.87 and its dependency Server Devcommands (both by JereKuusela, from
   Thunderstore) went into `data/config/bepinex/plugins` and into the admin's own client. The
   admin's Steam ID went into `data/config/adminlist.txt`.
2. Munin was switched off for the run: its DLL moved out of `data/config/bepinex/plugins`, and
   the copy inside the server deleted (the start copies DLLs in but never deletes them).
   `deploy.ps1` puts it back.
3. The
   console only updates when reopened; `verbose` switches the progress to a zone count, and
   the server log has it too.
4. 329 zones failed. Upgrade World gives up on a zone after 10 s without success, and some
   location types (frost caves, `MorkBorg` and others) take longer than that to load their
   files the first time. A second `zones_generate` with a local build of Upgrade World that
   waits 120 s instead generated all 329 in 17 minutes. The game's own generation has no such
   limit, so live is not affected.
5. `save` in the console, a clean stop, and a copy of `worlds_local/<world>` to
   `data/snapshots/<name>/world/`; later (TASK-29) Munin's redrawn tiles went into `tiles/` next to it.
6. The two mods came off the server and the client again.

Upgrade World's `biomes=`, `min=` and `max=` limit a run to part of the world.

## How the test server differs from live

It matches live in the game build, BepInEx pack, base image, user ID, world, world modifiers
and Munin settings. It changes these:

| Test server | Why |
|---|---|
| Starts the game directly (`image/fast-start.sh`) | about 35 s saved per start |
| Updates only when `update.ps1` runs | part of the fast start; `deploy.ps1` warns when versions differ |
| Server files on the `munin-test_server` volume | about 9 s faster to load than from a Windows drive |
| Crossplay off | with crossplay the server takes no inbound UDP, so it can't be joined by IP |
| No backups, scheduled restarts or status page | test only; the status page also needs a public server |
| `STEAMCMD_ARGS` empty | re-checking every server file through a Windows-drive mount took ~38 s |
| Munin `data_path` on a Docker volume | the image's file passes over tens of thousands of map files on a Windows drive cost ~40 s per start |
| Munin's Discord webhook blank | never post test events to the real Discord |

To start Munin's data afresh: stop the server, then run `docker volume rm munin-test_munin-data`.

The world, the plugin, the settings and the server install stay under `data/`, where you can
browse them.

## Timings (2026-11-18)

| | Stock image | Fast image |
|---|---|---|
| Build | 8 s | 4–7 s |
| Stop + recreate | 1–15 s | 6 s |
| Game server up | 66 s | 30–31 s |
| Map answering | 2 s | 0 s |
| **`deploy.ps1` round** | **about 78 s** | **about 42 s** |
| From `deploy.ps1` to standing in the world (timed by hand) | n/a | about 70 s |
| `update.ps1` (nothing new on Steam) | n/a | 60–75 s |
| `pull-save.ps1` | about 9 s with the Munin data, 1.5 s with `-WorldOnly` (plus about 5 s when it stops a running server) | same |

Where the 31 s goes: Unity loading the game's assets about 15 s, Steam and the dungeon
database about 4 s, the world about 2 s, then about 6 s until "Game server connected". All of
that is the game itself.
