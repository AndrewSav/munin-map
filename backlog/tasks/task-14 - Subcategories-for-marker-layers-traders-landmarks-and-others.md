---
id: TASK-14
title: Subcategories for marker layers (traders, landmarks and others)
status: To Do
assignee: []
created_date: '2026-11-19T10:18:00Z'
updated_date: '2026-11-19T10:31:00Z'
labels:
  - web
dependencies: []
references:
  - web/js/vegpack.js
  - web/js/ui.js
type: feature
ordinal: 13000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The vegetation layers already have subcategories: Trees, Ore, Mushrooms and the other groups each list their kinds (copper, tin, silver…; red, yellow, magecap…), and each kind can be shown or hidden on its own (`VEG_GROUPS` in `web/js/vegpack.js`). The marker layers in the left bar don't. Traders is one layer holding Haldor, Hildir and the Bog Witch. Landmarks is one layer holding the Forge of Potential, Ancestral Memorials, Orbs of Fimbulvinter and hot springs. You can't show just one of them, and the layer's "go to the next one" cycling mixes them all. With the spoiler view (TASK-1) that gets worse: one trader's up to 10 candidate sites are interleaved with the others'.

Give marker layers subcategories where that helps, in the same spirit as the vegetation groups: per-kind counts, show and hide per kind, and cycling within a kind.

Before starting: review every current marker layer and decide which would benefit from subcategories. List them all clearly in this task, with the subcategories each would get, and agree the list with the user before implementing. Traders and Landmarks are the two obvious examples. Candidates to look at include Boss altars, Mini-boss lairs, Dungeons and Camps & resources, which already sort their locations into kinds with their own icons, plus anything else that mixes several kinds in one layer.

Write it per decision-2.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Before implementing, every current marker layer is reviewed, and the layers that get subcategories are listed in the task notes with the subcategories of each; the list is agreed with the user
- [ ] #2 Traders and Landmarks get subcategories, one per kind (for example Haldor, Hildir and the Bog Witch), unless the review agrees otherwise
- [ ] #3 Each agreed layer shows its subcategories in the left bar with a count each, and each one can be shown or hidden on its own, as the vegetation kinds can
- [ ] #4 "Go to the next one" can cycle within a single subcategory, so for example one trader's sites come one after another
- [ ] #5 Turning a whole layer on or off, and the choices people already have saved for it, keep working as before
- [ ] #6 With the spoiler view (TASK-1), subcategory counts and cycling follow what is currently shown, as for whole layers
- [ ] #7 Written per decision-2
- [ ] #8 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->
