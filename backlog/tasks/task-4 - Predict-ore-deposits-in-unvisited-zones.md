---
id: TASK-4
title: Predict ore deposits in unvisited zones
status: To Do
assignee: []
created_date: '2026-11-18T03:12:00Z'
updated_date: '2026-11-19T10:31:00Z'
labels:
  - plugin
  - web
  - docs
dependencies: []
type: feature
ordinal: 4000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The map shows ore deposits only from saved world objects, and those exist only in zones that have already been generated. Even with `reveal_all = true` (TASK-1 adds planned locations there), ore in unvisited zones stays invisible, and ore is much of what players go looking for.

The game places vegetation, ore deposits included, when it generates a zone, from the world seed and its own placement rules. Reproducing that placement could predict the deposits in zones nobody has visited yet. Whether this can be made accurate enough is not known: placement depends on terrain, on what else already stands in the zone, and on rules that can change between game versions. The zones already generated in a real save are the ground truth to check the predictions against.

This came up as a proposal on the user's server and has not been tried.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Accuracy is measured first: predictions for zones already generated in a real save are compared with the deposits saved there, and the match rate is recorded in the task notes before the feature goes further
- [ ] #2 With `reveal_all = true`, the map shows predicted ore deposits in zones that have not been generated yet
- [ ] #3 Predicted deposits look different from known ones and are labelled as predicted
- [ ] #4 Once a zone is generated, its predicted deposits are replaced by the real ones from the save
- [ ] #5 With `reveal_all = false`, nothing changes: no predicted deposits are shown or sent
- [ ] #6 The docs say predictions follow the unmodded game's rules and may be wrong on servers with mods that change world generation
- [ ] #7 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->
