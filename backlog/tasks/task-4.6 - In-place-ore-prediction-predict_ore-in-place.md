---
id: TASK-4.6
title: In-place ore prediction (predict_ore = in-place)
status: To Do
assignee: []
created_date: '2026-11-22T22:22:28Z'
updated_date: '2026-11-22T23:07:12Z'
labels:
  - plugin
  - web
  - docs
dependencies:
  - TASK-4.4
parent_task_id: TASK-4
type: feature
ordinal: 35000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
TASK-4's `predict_ore = in-place`: Munin works out the deposits itself, from the game's own placement data and random functions, with no extract, no extra process and no setup, so it should become the usual choice. doc-2 records the placement rules (1.0.21) and how valheim.tools reaches high accuracy by re-running them; what can't be reproduced exactly is blocking by objects placed earlier in the zone, the zone's terrain blend, and the facing of randomly rotated locations. The extract methods (TASK-4.4/4.5) give the reference to check it against. If its accuracy falls clearly short during the work, stop and re-evaluate with the user.

Ref: TASK-4 (approach in its notes), doc-2 (placement rules, Ghost mode, how valheim.tools does it), TASK-4.4 (display), TASK-27 snapshot, TASK-4.2 compare.py.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 With `predict_ore = in-place` and `reveal_all = true`, Munin works out the deposits of ungenerated zones itself and shows them as TASK-4.4 shows an extract, with no extra process
- [ ] #2 Its accuracy per ore, against the TASK-27 snapshot or a whole-world extract of the same world, is measured and recorded before it ships; if it falls clearly short, the work stops for a decision with the user
- [ ] #3 The game server shows no noticeable effect while it works (fps and longest frames against a settled baseline on the test server), and the time to cover a whole world is recorded
- [ ] #4 It follows the running game's own data, so it needs no re-run after a game update beyond working out zones again
- [ ] #5 docs/ADVANCED.md describes it, says why it is less exact than an extract (blocking by earlier objects, terrain, randomly rotated locations) with the measured accuracy, and completes the comparison of the methods
- [ ] #6 PATCHES.md's single TASK-4 entry, which presents predicted ore as one feature with its modes, covers the in-place mode: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
