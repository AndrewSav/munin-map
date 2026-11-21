---
id: TASK-23
title: 'Local test server: pull-save also copies Munin''s per-world data from live'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-21T07:00:00Z'
updated_date: '2026-11-21T07:07:00Z'
labels:
  - tooling
dependencies: []
type: chore
ordinal: 22000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
pull-save.ps1 copies the live world backup and Munin's settings, but not Munin's own data, so the test server's stats.json only knows characters that joined the test server. Munin names builders (portal and base `by`, player card), claims, trails and stats from that data, so with live's world but not live's stats, none of the live builders can be named and the test server doesn't look like live. Copy the top-level files of live's Munin world folder (stats, activity, events, bases, claims, build history, caches; about 20 MB) into the test server's munin-data volume as well. Tiles, models and icons are left out: hundreds of MB, and the test server builds its own. Fork tooling only (patches-tools/local-test), no upstream files.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 pull-save.ps1 copies the top-level files of live's Munin folder for the world into the test server's munin-data volume, replacing the test server's own top-level files there; subfolders (tiles) are left alone
- [x] #2 The copied files belong to the container's valheim user, who runs the server, so Munin can write them
- [x] #3 Live's data folder is worked out from live's data_path (empty: BepInEx/config/MuninMap); a data_path the script can't map to the live host stops it with a clear message
- [x] #4 A JSON file that doesn't parse (caught mid-write on live) stops the script with a message to run it again
- [x] #5 A -WorldOnly switch keeps the test server's own Munin data, as before
- [x] #6 README.md in patches-tools/local-test says what is copied
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. pull-save.ps1: read live data_path from the copied Munin settings (empty only: config/bepinex/MuninMap on the live host), list the world folder's top-level files over ssh, scp them to data/config/munin-incoming, check the count and that every .json parses.
2. After stopping the server: a one-off compose run of the valheim service as root replaces the top-level files in munin-data/<world>, chowns them to valheim and sets 644; subfolders stay.
3. -WorldOnly skips both. README: what is copied, timing.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Tested: full run copied 26 files (about 20 MB) in about 9 s; in the volume all owned valheim:valheim, 644, tiles/ kept, the test server's own top-level files (bases-edits.json, test trails.txt) gone; Munin rewrote stats.json after start with no write errors; live builders now named (16/16 portals, 11/12 bases with by). -WorldOnly: no Munin copy, no staging folder, test stats kept. Error paths run against the script's own checks: a truncated stats.json gives the run-again message; data_path = /srv/munin gives the unsupported-data_path message. Chown by name (valheim) rather than live's PUID/PGID, which mean nothing on the Windows host. cp without -p and an explicit chmod 644: files staged on the Windows drive came over as 777. decision-2: fork tooling only, no upstream files; not a product change, so no PATCHES.md entry.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
pull-save.ps1 now also copies the top-level files of live's Munin world folder (stats, activity, trails, events, bases, claims, build history, caches) into the test server's munin-data volume, so the test server names live's builders and players. Tiles, models and icons stay behind. Files go in through a one-off root container, owned by valheim with 644. A non-default live data_path or a half-written JSON file stops the script with a message; -WorldOnly keeps the old behaviour. README updated.
<!-- SECTION:FINAL_SUMMARY:END -->
