---
id: TASK-39
title: 'Valheim 1.0.31: run the game-update routine'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-26T17:08:47Z'
updated_date: '2026-11-26T17:25:59Z'
labels:
  - game-update
dependencies: []
type: chore
ordinal: 45000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Live updated to Valheim 1.0.31 (2026-11-26). Run patches-tools/game-update/README.md (TASK-37) for it and record each step here.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Step 1: test server on 1.0.31, versions match live
- [x] #2 Step 2: diff-game.ps1 1.0.30 vs 1.0.31 run; nothing missing from MuninMap.dll references; every CHANGED watch entry read and explained or fixed
- [x] #3 Step 3: rebuilt against 1.0.31; test server log clean and WebP served both ways
- [x] #4 Step 4: Windows dedicated server 1.0.31 starts Munin cleanly
- [x] #5 Step 5: in-place measured on a fresh 1.0.31 world against ore-extract; CheckedAgainst raised to 1.0.31 if it holds (table row added)
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
1. update.ps1: test l-1.0.31 = live l-1.0.31, BepInEx 5.4.2360, image 1.4.1 (l-1.0.30 assemblies already kept in data/managed).
2. diff-game.ps1 l-1.0.30 vs l-1.0.31: no MuninMap.dll reference missing; same DLL list; every watch.txt entry same. Other changes: BuildUi, BuildUiFavoriteCategoryCheckButton, CookingStation (a craft stat when m_recordCrafter is off), Version (1.0.31); Munin uses none of them.
3. deploy.ps1 built against 1.0.31; log: Chainloader 1 loaded 0 failed, no type-load/missing-member/Harmony errors, HTTP server, in-place self-check, location ground 81/81 failed 0, world sweep #1; tile 4/10/3 200 as WebP (540 B) and PNG (3824 B).
4. Windows dedicated server 1.0.31, fresh world wintest31: same checks clean; 572 tiles converted to WebP; tile served as WebP (41158 B) and PNG (59380 B).

5. Fresh world wintest31 made on 1.0.31 (Windows server), in-place vs ore-extract at 0.5 m: copper 99.9, muddy scrap 98.9, silver 97.9, tin 99.9, obsidian 98.6, all 99.3%. Muddy scrap is 13 misses in ~1130 (0.1 point each) with the game code unchanged: spread between worlds. Holds; CheckedAgainst and PATCHES.md raised to 1.0.31, table row added; test server sends game 1.0.31 = checked 1.0.31.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Game-update routine for Valheim 1.0.31: all five steps pass. No game code Munin copies or parses changed (only BuildUi, CookingStation, Version); Linux and Windows load clean with WebP both ways; in-place measured at 99.3% on a fresh 1.0.31 world, so the verified version is raised to 1.0.31.
<!-- SECTION:FINAL_SUMMARY:END -->
