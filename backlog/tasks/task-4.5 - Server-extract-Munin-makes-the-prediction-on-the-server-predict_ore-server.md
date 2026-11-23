---
id: TASK-4.5
title: >-
  Server extract: Munin makes the prediction on the server (predict_ore =
  server)
status: To Do
assignee: []
created_date: '2026-11-22T22:22:25Z'
updated_date: '2026-11-22T23:07:11Z'
labels:
  - plugin
  - web
  - docs
dependencies:
  - TASK-4.4
  - TASK-4.3
parent_task_id: TASK-4
type: feature
ordinal: 34000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
TASK-4's `predict_ore = server`: Munin makes the extract itself, on the server, so the admin needs no second machine and no manual steps. TASK-4.2 showed this is practical: one process at the lowest priority on one core generated a whole world in about an hour and a half inside a live server's container, with no measurable effect on the game server, at about 1.5 GB of memory rising to about 3.5 GB in the outer biomes. It runs for a long time, so players need to see how far along it is, and a server restart must not throw the work away.

Ref: TASK-4 (approach in its notes), TASK-4.2 (slow mode, resume, memory guard, oom_score_adj, the generator quitting when its tool is gone), TASK-4.4 (the file Munin reads), TASK-4.3 (ore-extract shipped).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 With `predict_ore = server` and `reveal_all = true`, Munin starts the extract by itself when there is no current prediction for the world, on a copy of the save, with no admin action beyond the setting; the live world is never written
- [ ] #2 The extract runs as one process at the lowest CPU priority on one core, with the memory guard and the out-of-memory preference of TASK-4.2, using the server's own install
- [ ] #3 Progress (zones done, percent, time left) and the result (done, failed with the reason) are shown to everyone in the page's server details
- [ ] #4 A server restart doesn't lose the work: after it, Munin resumes the extract where it stopped
- [ ] #5 When the extract finishes, its deposits are shown as TASK-4.4 shows a file extract, without a restart
- [ ] #6 After a game update that changes the game version, Munin makes the prediction again by itself
- [ ] #7 The extract never outlives the server: it stops when the server stops or Munin is unloaded
- [ ] #8 The game server's fps and longest frames during an extract, against a settled baseline, are measured on the test server and show no noticeable effect
- [ ] #9 docs/ADVANCED.md describes the server extract (what it does, its time and memory, where to see progress) and adds it to the comparison of the methods
- [ ] #10 PATCHES.md's single TASK-4 entry, which presents predicted ore as one feature with its modes, covers the server mode: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
