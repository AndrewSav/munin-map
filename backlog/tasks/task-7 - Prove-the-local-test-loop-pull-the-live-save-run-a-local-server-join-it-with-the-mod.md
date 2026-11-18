---
id: TASK-7
title: >-
  Prove the local test loop: pull the live save, run a local server, join it
  with the mod
status: To Do
assignee: []
created_date: '2026-11-18T04:38:00Z'
updated_date: '2026-11-18T04:41:00Z'
labels:
  - testing
  - build
dependencies: []
references:
  - tools/docker-compose.test.yml
priority: high
type: chore
ordinal: 500
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Every deploy to the live server restarts it, so changes need testing somewhere else first. The plan is a local test server on the user's Windows PC (Docker Desktop, with the Valheim client installed). This task proves that whole loop works smoothly before any feature work depends on it. If it doesn't, the fallback is to publish a release for every test iteration (TASK-6) and test that on the live server. The user is fine with that fallback, but wants to know which way it goes first.

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
- [ ] #1 One command on this PC fetches the newest consistency-checked world backup from the live server over SSH into the local test server's world folder; it never copies the live world folder and changes nothing on the live server
- [ ] #2 The local server matches live where it matters: the same pinned image tag, user ID, `BEPINEX=true`, plugin folder, world modifiers (`SERVER_ARGS`) and Munin settings
- [ ] #3 The local server differs from live where it must: not public, no backups, and no restart or update schedule
- [ ] #4 One command builds the plugin against the local server's own game assemblies and deploys it to the local server
- [ ] #5 The user joins the local server from the Valheim client on this PC and is in the copied live world, with the build under test loaded
- [ ] #6 The map at http://localhost:3000 shows that world and the user as a live player
- [ ] #7 A second round (change, build, deploy, restart, rejoin) works with no manual fix-ups, and the time each step took is recorded
- [ ] #8 All of the fork's own tooling for this (scripts, compose file, notes) lives in the `patches-tools/` folder at the repo root, so it never conflicts on a rebase; no upstream file changes, including `tools/docker-compose.test.yml`
- [ ] #9 The world, the server files and the test data are never committed
- [ ] #10 The steps are written down well enough to repeat the loop without this task's history
- [ ] #11 The final summary records the verdict: local testing is the default, or (with reasons) a release per test iteration is used instead
<!-- AC:END -->
