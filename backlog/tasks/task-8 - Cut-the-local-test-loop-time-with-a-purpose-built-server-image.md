---
id: TASK-8
title: Cut the local test loop time with a purpose-built server image
status: To Do
assignee: []
created_date: '2026-11-18T05:47:00Z'
labels:
  - testing
  - build
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
- [ ] #1 A full round (`deploy.ps1` from start until the map answers, with a code change built and deployed) is timed before and after, and is clearly faster than the 78 s baseline; the README records both and where the remaining time goes
- [ ] #2 The time from a deploy until the user can rejoin in game is measured and recorded the same way
- [ ] #3 The server runs the same Valheim build as live and a comparable BepInEx, and the version it runs is shown or logged so a mismatch with live is easy to spot
- [ ] #4 There is a documented, one-command way to move the test server to a new Valheim or BepInEx version when live updates
- [ ] #5 The copied live world loads, Munin loads with the pulled settings, and the user can join from the client and appear on the map, as with TASK-7
- [ ] #6 `pull-save.ps1` and `deploy.ps1` keep working as the entry points; any image definition and scripts live in `patches-tools/`, and no upstream file changes
- [ ] #7 Every new difference from live is listed in the README's differences table with its reason
<!-- AC:END -->
