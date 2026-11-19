---
id: TASK-8
title: Cut the local test loop time with a purpose-built server image
status: Done
assignee:
  - '@claude'
created_date: '2026-11-18T05:47:00Z'
updated_date: '2026-11-19T10:59:00Z'
labels:
  - testing
  - build
milestone: Initial release
dependencies: []
references:
  - patches-tools/local-test/README.md
  - https://github.com/community-valheim-tools/valheim-server-docker
type: chore
ordinal: 7000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The local test loop from TASK-7 (`patches-tools/local-test/`) works, but each round of change → build → deploy → restart → rejoin takes about 78 s, and most of that is the server starting. The goal is the shortest debug loop possible. The user accepts trade-offs to get there: no update check on start, and an image that is not the live one (a locally built image is fine).

Where the 78 s goes now (TASK-7, round 3, 2026-11-18):
- Build: 8 s.
- Container recreate: 1–15 s.
- Image setup before the game (bootstrap, chown/chmod passes, plugin sync): about 5 s.
- steamcmd update check: 13 s.
- BepInEx merge check and server process start: 13 s.
- Valheim start to "Game server connected", world load included: 34 s.
- Munin answering: 2 s.

What the current image does on every start that a test server may not need: chown of `/config` and `/opt/valheim`, steamcmd (`STEAMCMD_ARGS` is already empty), the BepInEx-pack check against Thunderstore, a merge of the server and BepInEx installs, and supervisord with its cron, backup and updater services. It also recreates the whole container just to pick up a new plugin DLL.

What must not be lost: the mod has to run on the same Valheim build as live (it uses game internals, and live updates itself), on a comparable BepInEx (live gets the latest BepInExPack_Valheim), against the copied live world, with the Munin settings `pull-save.ps1` produces.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A full round (`deploy.ps1` from start until the map answers, with a code change built and deployed) is timed before and after, and is clearly faster than the 78 s baseline; the README records both and where the remaining time goes
- [x] #2 The time from a deploy until the user can rejoin in game is measured and recorded the same way
- [x] #3 The server runs the same Valheim build as live and a comparable BepInEx, and the version it runs is shown or logged so a mismatch with live is easy to spot
- [x] #4 There is a documented, one-command way to move the test server to a new Valheim or BepInEx version when live updates
- [x] #5 The copied live world loads, Munin loads with the pulled settings, and the user can join from the client and appear on the map, as with TASK-7
- [x] #6 `pull-save.ps1` and `deploy.ps1` keep working as the entry points; any image definition and scripts live in `patches-tools/`, and no upstream file changes
- [x] #7 Every new difference from live is listed in the README's differences table with its reason
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. `image/` (Dockerfile + fast-start.sh, done as an experiment): FROM the live image, the server's user ID and home ownership set at build time, CMD starts the merged server directly.
2. `compose.yml`, two services. `valheim` (the default): the local image, built from ./image, init: true; /config bind mount on the Windows drive, server files on a `server` Docker volume, munin-data volume. `update` (compose profile): the stock image with its own startup and the server files on the Windows-drive bind mount (data/server), as now.
3. `update.ps1`: stop the fast server; run the update service until steamcmd and the BepInEx merge are done (`valheim-server: started`), then stop it. Copy data/server/bepinex into the `server` volume, rebuild the image, and print the test server's Valheim and BepInEx versions next to live's (over ssh).
4. `deploy.ps1`: run update.ps1 if the volume is empty. Build against data/server Managed, copy the DLL, recreate the valheim service, and wait for the game server and Munin. Print the test server's Valheim version and warn if live differs.
5. Measure full rounds plus the rejoin time, and update the README (loop, differences table, timings).
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Experiments, 2026-11-18 (local image munin-test-fast: FROM the live image, the server's user ID and /home/valheim ownership set at build time, CMD runs valheim_server.x86_64 from the already-merged /opt/valheim/bepinex, under --init).
- Direct start with the server files on the Windows-drive bind mount: Game server connected and Munin answering at 39-40 s (was 66 s with the stock startup).
- The same with the merged install (2.1 GB) copied into a Docker volume (copy 17 s): 31 s. Breakdown: preloader at 1 s, Unity asset load about 15 s, Steam and DungeonDB about 3 s, world load about 2 s, about 6 s more until "Game server connected".
- docker stop: 4 s, with a clean world save (tini forwards SIGTERM).
- A first attempt as a non-root user without the image changes segfaulted in dlopen: /home/valheim is root-only (0700) until the stock bootstrap chowns it.
- BepInEx logs the Valheim version and the BepInExPack version, so the test server's versions can be read from its log.

Built as planned. update.ps1 from an empty volume: 58 s (image build 3 s, stock startup until BepInEx loaded 39 s, volume copy 16 s); the Valheim version, BepInEx pack and base image all match live. Normal deploy.ps1 round with a running server: build 5 s, restart 6 s (stop with world save included), game server 31 s, Munin 0 s, 42 s in total (was 78 s). A throwaway banner round proved the new DLL is served; reverted. Fixes found along the way: the volume must be created by compose (otherwise compose warns); deploy polls 127.0.0.1 rather than localhost; build output is quiet unless it fails or warns. Waiting on the user for the rejoin timing.

User check, 2026-11-18: from starting deploy.ps1 to standing in the world took about 1 min 10 s (deploy about 42 s, then client join and world load about 28 s); the user appeared on the map. Not measured on the stock image, so there is no before figure. Added to the README timings.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
A deploy.ps1 round on the local test server now takes about 42 s, down from 78 s. From the start of a deploy to standing in the world takes about 70 s (timed by the user).

What changed in `patches-tools/local-test/`:
- `image/`: a local image built FROM the live image. The server's user ID and home ownership are set at build time, and `fast-start.sh` starts the already-merged server and BepInEx directly. That skips supervisord, the chown/chmod passes, steamcmd and the BepInEx check, about 35 s per start.
- `compose.yml`: two services. `valheim` is the fast server, with its server files on a Docker volume, about 9 s faster to load than from the Windows drive. `update` (profile) is the stock image with its own startup.
- `update.ps1`: the one command to follow live. It rebuilds the image with --pull, lets the stock startup update and merge, then copies the result into the volume.
- `common.ps1`: shared helpers, including the version comparison with live over ssh.
- `deploy.ps1`: uses the fast server, runs update.ps1 on the first run, keeps the build quiet, and warns when the Valheim version, the BepInEx pack or the base image differ from live.

Verified: update.ps1 from an empty volume (58 s). Repeated deploy rounds (42 s). A throwaway banner proved the new DLL is served (reverted). The world loads, Munin answers, and the user joined and appeared on the map. Versions match live.

Risks: the test server never updates on its own, so after a live update run update.ps1, which deploy warns about. The remaining ~31 s start is the game itself, mostly Unity asset loading.
<!-- SECTION:FINAL_SUMMARY:END -->
