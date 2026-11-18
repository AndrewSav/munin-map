# Local test server

A local Valheim dedicated server in Docker running a copy of the live world
with the Munin Map build from this checkout. Use it to test a
change before it goes to the live server, where every deploy means a restart.

Fork-only tooling: it lives on the `patches` branch, in this folder, so it never conflicts
with upstream on a rebase. Everything the scripts create goes in `data/`, which git ignores.

## Needs

- Docker Desktop, running.
- Windows OpenSSH (`C:\Program Files\OpenSSH`) with a key in the ssh-agent that logs in to
  the live server. The ssh that comes with Git Bash does not use the agent.
- The .NET SDK (8 or later), for the repo's `build.ps1`.
- The Valheim client, with a **throwaway character**. Your character is saved on your PC, not
  on the server, so anything that happens to it in the test world (items taken or lost,
  skills lost on death) carries over to the live server.

## The loop

```powershell
.\patches-tools\local-test\pull-save.ps1   # now and then: fresh copy of the live world
.\patches-tools\local-test\deploy.ps1      # after each code change: build, deploy, restart
```

Then, in Valheim: **Join game → Add server → `127.0.0.1:2456`**, with no password. The map is
at <http://localhost:3000>. To show up on it, tick **"Visible to other players"** on the
in-game map (`M`). Munin hides players who don't share their position, as on live.

`deploy.ps1 -NoBuild` restarts with the plugin that is already deployed.

Neither script asks before restarting: whoever is on the test server is disconnected.

## What the scripts do

**`pull-save.ps1`** changes nothing on the live server. It:
1. Finds the newest `config/backups/AUTOBACKUP-<world>-*` in the live compose folder. That is the image's daily copy, taken only from a committed save. It
   never copies the live world folder, because a copy taken mid-save can be broken.
2. Checks that the backup's `_main.N.ok` marker matches its `_main.N.db2`.
3. Copies it down with scp and checks the file count.
4. Copies the live Munin settings too, changing two lines:
   - `discord_webhook` is blanked, so the test server never posts to the real Discord.
   - `data_path` points at the `munin-data` Docker volume (see below).
5. Stops the local server, if it is running, and swaps in the world and the settings.

**`deploy.ps1`**:
1. On the first run only, starts the server so steamcmd installs it (about 4 minutes).
2. Builds with the repo's `build.ps1`, against that install's own game assemblies in
   `data/server/server/valheim_server_Data/Managed`.
3. Copies only `MuninMap.dll` into `data/config/bepinex/plugins/`, as on live, so the web app
   is served from inside the DLL. It prints the start of the DLL's SHA-256.
4. Recreates the container, which copies the plugin in as it starts.
5. Waits for "Game server connected" in the log and for the map to answer, timing each step.

## How the test server differs from live

It matches live in the image, user ID, world, world modifiers and Munin settings. It changes
these:

| Test server | Why |
|---|---|
| Named `<world> test`, not public, no password | ports are bound to 127.0.0.1, so only the local machine can connect |
| No backups, scheduled restarts or update checks | test only; the server still updates on each start |
| No status page | it needs a public server |
| `STEAMCMD_ARGS` empty | re-checking every server file through a Windows-drive mount took ~38 s |
| Munin `data_path` on a Docker volume | see below |
| Munin's Discord webhook blank | never post test events to the real Discord |

On every start the image walks every file in `/config` and `/opt/valheim` to fix their
owner and permissions. Through Docker Desktop's Windows-drive mount that is slow, and Munin's data is
tens of thousands of files that keep growing. With the data on a Windows drive, a start
took about 40 s longer. On a Docker volume the walk skips it. To clear Munin's data, stop the
server and run `docker volume rm munin-test_munin-data`.

The world, the plugin, the settings and the server install stay under `data/`, where you can
browse them.

## Timings (2026-11-18)

| Step | Time |
|---|---|
| `pull-save.ps1` | 1.5 s (about 10 s when it stops a running server) |
| First `deploy.ps1` (installs the server) | 412 s |
| Build | 8–9 s (30 s the first time) |
| Restart the container | 1–15 s |
| Game server up | 66 s |
| Map answering | 2 s |
| **`deploy.ps1` round, start to finish** | **about 78 s** |

The live server's in-container restart takes about 73 s.
