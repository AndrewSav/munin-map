---
id: TASK-38
title: >-
  Next rebase: drop TASK-36 if upstream merged it, and write down what the
  rebase teaches
status: To Do
assignee: []
created_date: '2026-11-25T13:56:41Z'
updated_date: '2026-11-25T13:57:42Z'
labels:
  - rebase
dependencies: []
type: chore
ordinal: 44000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
TASK-36 merges System.Memory and System.Runtime.CompilerServices.Unsafe into MuninMap.dll (two marked lines in src/ILRepack.targets) because Valheim 1.0.30 stopped shipping them. The same fix was offered upstream on branch fix/system-memory. On the next rebase onto an upstream release, check whether upstream now merges them; if it does, the fork change is no longer needed.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 On the next rebase, upstream src/ILRepack.targets is checked for System.Memory and Unsafe in its InputAssemblies
- [ ] #2 If upstream has them: the TASK-36 lines are gone from src/ILRepack.targets (the rebase drops or resolves them) and the TASK-36 entry is removed from PATCHES.md
- [ ] #3 If upstream does not: this task stays open for the rebase after
- [ ] #4 Everything learned during the rebase (the steps taken, each conflict and how it was resolved, anything that surprised or had to be redone) is written down for the next one, as a rebase routine in patches-tools/ like patches-tools/game-update/README.md
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
