---
id: TASK-4
title: Predict ore deposits in unvisited zones
status: To Do
assignee: []
created_date: '2026-11-18T03:12:00Z'
updated_date: '2026-11-21T08:55:00Z'
labels:
  - plugin
  - web
  - docs
dependencies: []
documentation:
  - >-
    backlog/docs/doc-2 -
    Ore-placement-how-the-game-places-vegetation-in-a-zone.md
type: feature
ordinal: 4000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The map shows ore deposits only from saved world objects, and those exist only in zones that have already been generated. Even with `reveal_all = true` (TASK-1 adds planned locations there), ore in unvisited zones stays invisible, and ore is much of what players go looking for.

The game places vegetation, ore deposits included, when it generates a zone, from the world seed and its own placement rules. doc-2 records those rules from the game code (1.0.21). In short:
- Each vegetation type is seeded per zone from the world seed, and Munin runs inside the game, so it can call Unity's random functions and `WorldGenerator` directly. Nothing has to be ported.
- What can't be reproduced directly is blocking: a spot is rejected if it hits an object placed earlier in the same zone. The zone's real terrain differs a little too.
- Copper and buried iron scrap (Swamp) are the most reproducible: fixed draws, and only one rock type placed before each. Wrong calls give extra or missing deposits at real candidate spots, never shifted ones. Tin and obsidian are harder, and silver is the right zone but maybe the wrong place.
- In zones with a randomly rotated location, the rotation isn't fixed by the seed, so a prediction there can't be exact.

Two ways to predict, both in doc-2:
1. **Re-run the placement rules in Munin,** approximating the blockers.
2. **Let the game generate the zones for real on a throwaway copy of the world,** in a second server process, and read off where the ore landed. This is exact apart from location rotation, and picks up the server's own mods.

Never generate zones on the live world: it can't be fully undone (deleted trader sites, location icons already sent to players).

The zones already generated in a real save are the ground truth to check predictions against. This came up as a proposal on the user's server and has not been tried.
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
- [ ] #8 The approach is chosen and recorded in the task before building: re-running the placement rules in Munin, or generating zones on a throwaway copy of the world (doc-2), and which ores are in scope first
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
