---
id: TASK-4.1
title: 'Spike: headless, faster world generation for the ore export'
status: To Do
assignee: []
created_date: '2026-11-22T03:09:49Z'
labels:
  - tooling
  - plugin
dependencies: []
documentation:
  - >-
    backlog/docs/doc-2 -
    Ore-placement-how-the-game-places-vegetation-in-a-zone.md
parent_task_id: TASK-4
type: spike
ordinal: 30000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
TASK-4's copy-based method reads every ore deposit from a fully generated copy of the world. Done with Upgrade World (TASK-27) it took about 3 hours (about 450 zones a minute) on one dedicated server, started from an admin client's console, which few admins would do per world. doc-2 ("Generating a copy faster") sets out ideas that could make it practical: a client-free generator (a small plugin on a dedicated server started on a copy, without networking, that generates, exports the deposits and exits), skipping work ore does not need (ocean zones, dungeon interiors, vegetation after the last ore entry), not holding back for gameplay, and several processes on disjoint shares of the zones whose deposit lists are joined; or the same generator run slowly as a low-priority second process next to the live server. Unity only allows the work on its main thread, so one process is sequential. Nothing is measured yet: the key unknown is how much of today's speed is waiting versus real work. This spike measures it and recommends whether the copy method is worth building.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 A client-free generator runs on a dedicated server started on a copy of a world: it generates the ungenerated zones in a loop, writes the ore deposits it created (prefab, position, zone) to a file, and exits; no game client, console or networking involved
- [ ] #2 Zones per minute and memory are measured for the plain loop (as Upgrade World does it) and for each speed-up separately: back-to-back zones, lifted frame-rate cap, terrain requested ahead, no autosaves, land zones only, no dungeon interiors, vegetation stopped after the last ore entry
- [ ] #3 The exported deposits match the TASK-27 snapshot for the same zones, apart from the differences the seed does not decide (near randomly rotated locations, unique sites)
- [ ] #4 Two or more processes on disjoint shares of the zones are measured together: total zones per minute, memory per process, and the joined deposit list
- [ ] #5 A low-priority generator run next to a running test server is measured for its effect on that server (fps, longest frame)
- [ ] #6 Results and a recommendation (build the copy method or not, and in which form) are recorded in TASK-4 and doc-2
- [ ] #7 Spike code is not merged into the product; anything kept as tooling lives in patches-tools
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
