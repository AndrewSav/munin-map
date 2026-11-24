---
id: DRAFT-10
title: 'Game-update routine: what to check after a Valheim patch'
status: Draft
assignee: []
created_date: '2026-11-23T21:29:36Z'
updated_date: '2026-11-24T03:17:52Z'
labels:
  - docs
dependencies: []
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Upstream has no routine for a new Valheim version, and the fork now copies game behaviour that a patch can change silently. Put one together, out of upstream's way: patches-tools/game-update/ with a README of the steps (and any scripts they need). Each fork feature that depends on game internals adds its own checks; it is run after every Valheim update, alongside rebuilding against the new game assemblies.

First entry, in-place ore prediction (TASK-4.6): it checks Unity's random generator and the zone terrain against the game at start, but the placement loop (ZoneSystem.PlaceVegetation), location placement (PlaceLocations, SpawnLocation, RandomSpawn, RandomObject), the block and ground checks (IsBlocked, GetGroundData, GetTerrainDelta), Heightmap.GetBiome and the components that draw random numbers in Awake (LodFadeInOut, MineRock5, Pickable, ResourceRoot, ZSFX, Floating) are copied from 1.0.21 and not checked at run time. Steps:
1. Diff those parts of the decompiled game code against the previous version.
2. An ore-extract reference for a world, then in-place in all-zones mode (MUNIN_INPLACE_ALL=1), compared per ore with patches-tools/ore-extract/compare.py (about 30 min).
3. If it holds, raise in-place's 'checked against' game version (TASK-4.6), which clears the note in Server details.

Other candidates to list: ore-extract's generation plugin, map pins (doc-1), anything else reading game internals.

Ref: TASK-4.6, doc-2.
<!-- SECTION:DESCRIPTION:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
