---
id: TASK-7
title: >-
  Prove the local test loop: pull the live save, run a local server, join it
  with the mod
status: Done
assignee:
  - '@claude'
created_date: '2026-11-18T04:38:00Z'
updated_date: '2026-11-19T10:59:00Z'
labels:
  - testing
  - build
milestone: Initial release
dependencies: []
references:
  - tools/docker-compose.test.yml
priority: high
type: chore
ordinal: 500
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Every deploy to the live server restarts it, so changes need testing somewhere else first. The plan is a local test server in Docker, joined from the Valheim client. This task proves that whole loop works smoothly before any feature work depends on it. If it doesn't, the fallback is to publish a release for every test iteration (TASK-6) and test that on the live server. The user is fine with that fallback, but wants to know which way it goes first.

What the local server needs from the live one:
- The same image, `ghcr.io/community-valheim-tools/valheim-server`, at the tag live pins. Older images (including upstream's untagged `lloesche/valheim-server`) corrupted Valheim 1.0 worlds when run under a non-root PUID.
- The same plugin layout: `config/bepinex/plugins/MuninMap.dll`, which the image copies into its BepInEx install on each start, with Munin's settings in `config/bepinex/com.cocopuff2u.muninmap.cfg`.
- The same world modifiers (`SERVER_ARGS`) and Munin settings as live.
- A copy of the live world. A Valheim 1.0 world is a folder of chunk files plus `_main.N.db2`, `.fwl2`, `.chunks` and an `_main.N.ok` marker for each committed save. Copying the live world folder while the game is saving can produce a broken world, so take the copy from the image's daily consistency-checked backup (`config/backups/AUTOBACKUP-<world>-<date>/`) instead.
- SSH access to the live server, to fetch it.

Upstream's `tools/docker-compose.test.yml` is a starting point, but it uses the old untagged image and a different plugin folder than live does, so it is not a faithful copy of live. The `valheim-test/` folder is already gitignored. Without a local Valheim install, a local build has no game assemblies to compile against until the test server's own server files provide them.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 One command fetches the newest consistency-checked world backup from the live server over SSH into the local test server's world folder; it never copies the live world folder and changes nothing on the live server
- [x] #2 The local server matches live where it matters: the same pinned image tag, user ID, `BEPINEX=true`, plugin folder, world modifiers (`SERVER_ARGS`) and Munin settings
- [x] #3 The local server differs from live where it must: not public, no backups, and no restart or update schedule
- [x] #4 One command builds the plugin against the local server's own game assemblies and deploys it to the local server
- [x] #5 The user joins the local server from the Valheim client and is in the copied live world, with the build under test loaded
- [x] #6 The map at http://localhost:3000 shows that world and the user as a live player
- [x] #7 A second round (change, build, deploy, restart, rejoin) works with no manual fix-ups, and the time each step took is recorded
- [x] #8 All of the fork's own tooling for this (scripts, compose file, notes) lives in the `patches-tools/` folder at the repo root, so it never conflicts on a rebase; no upstream file changes, including `tools/docker-compose.test.yml`
- [x] #9 The world, the server files and the test data are never committed
- [x] #10 The steps are written down well enough to repeat the loop without this task's history
- [x] #11 The final summary records the verdict: local testing is the default, or (with reasons) a release per test iteration is used instead
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Folder `patches-tools/local-test/` (fork-only): `compose.yml`, `.gitignore` (ignores `data/`), `pull-save.ps1`, `deploy.ps1`, `README.md`.
2. `compose.yml` copies live (pinned image, user ID, world, world modifiers, BepInEx). Test-only settings: not public, no password, ports on 127.0.0.1, no backups, restarts, update cron or status page, and STEAMCMD_ARGS empty. `/config` and `/opt/valheim` are bind mounts under `data/`. Munin data is on the `munin-data` Docker volume.
3. `pull-save.ps1`: over Windows OpenSSH, take the newest AUTOBACKUP and check its .ok marker; scp it plus the live Munin cfg (discord_webhook blanked, data_path pointed at the volume); stop the server; swap them in.
4. `deploy.ps1`: on the first run, start the server to install it. Build against its Managed assemblies, copy only MuninMap.dll, recreate the container, and wait for the game server and Munin, timing each step.
5. Proven in three rounds, each timed. README written.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Round 1, 2026-11-18. pull-save.ps1: 1.5 s (the newest daily backup, 30 files). deploy.ps1 first run: 412 s in total. Server install 236 s (first time only), build 30 s, restart 14 s, game server up 129 s, Munin answering 2 s later. World loaded, Munin HTTP on :3000, web served from the DLL. Container at 1.9 GiB RAM while models export. Everything is on bind mounts on a Windows drive. Note: build.ps1 copies only some referenced game assemblies, so deploy.ps1 copies all of Managed into libs/valheim first. Waiting for the user to join.

Round 2, 2026-11-18. Throwaway red banner added to web/index.html (uncommitted, to be reverted). deploy.ps1: build 9 s, restart 9 s, game server up 97 s, Munin 2 s, 119 s in total. The banner is served at :3000. With STEAMCMD_ARGS="" steamcmd took 15 s (38 s with validate). The rest of the start goes to the image's permission passes over Docker Desktop's Windows-drive (9p) mount: chown -R of /config and /opt/valheim in the bootstrap (~14 s), plus chmod of every file under /config/bepinex in the updater (~26 s). Munin's data dir (config/bepinex/MuninMap, 16,756 files after one session and growing) dominates both. The game itself takes about 40 s.

Round 3, 2026-11-18. Banner reverted (git checkout). Munin data_path moved to a Docker volume (/home/valheim/munin-data); pull-save.ps1 sets it in the copied cfg, and the old data/config/bepinex/MuninMap was deleted. pull-save.ps1 9.6 s (it stopped the server). deploy.ps1: build 8 s, restart 1 s, game server up 66 s, Munin 2 s, 78 s in total. The image setup steps now take about 5 s instead of about 40 s: supervisord 4 s after container start, updater 1 s later, steamcmd 13 s, server start 13 s, world and connect 34 s. Munin writes to the volume as the valheim user. README.md written with the loop, the differences from live and the timings.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Verdict: the local test loop works smoothly, so local testing is the default. A release per test iteration is not needed.

Added `patches-tools/local-test/`:
- `pull-save.ps1`: fetches the newest consistency-checked AUTOBACKUP from the live server over SSH (read-only there), plus the live Munin settings with the Discord webhook blanked and data_path pointed at a local Docker volume.
- `compose.yml`: the same pinned image and mod-relevant settings as live; test-only differences are listed in the README.
- `deploy.ps1`: builds against the test server's own game assemblies, deploys only MuninMap.dll, restarts, and times each step.
- `README.md`.

Verified with the user over three rounds on 2026-11-18. The user joined the copied live world with a throwaway character and appeared on http://localhost:3000. A throwaway banner in round 2 showed that the rebuilt DLL was the one served, and it was reverted afterwards. A round now takes 78 s (build 8 s, server up 66 s), close to the live server's own restart time.

Two findings shaped the setup: steamcmd validate is slow over Docker Desktop's Windows-drive mount, and the image walks every file under /config on each start, where Munin's growing data was costing about 40 s.

Risks: the test server takes whatever game build Steam has on each start, so it can briefly differ from live. Scripts restart without asking. build.ps1 copies only some referenced game assemblies, which deploy.ps1 works around. Follow-up: TASK-8 (purpose-built image to cut the loop further).
<!-- SECTION:FINAL_SUMMARY:END -->
