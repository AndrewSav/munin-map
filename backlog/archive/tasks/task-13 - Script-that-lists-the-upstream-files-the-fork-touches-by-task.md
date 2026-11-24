---
id: TASK-13
title: Script that lists the upstream files the fork touches, by task
status: To Do
assignee: []
created_date: '2026-11-19T10:14:00Z'
updated_date: '2026-11-24T06:42:47Z'
labels:
  - build
  - tooling
dependencies: []
references:
  - backlog/decisions/decision-2 - Write-patches-so-they-rebase-easily.md
type: chore
ordinal: 12000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Before rebasing `patches` onto a new upstream release, it helps to see at a glance where the fork touches upstream's files, because those are the only places a rebase can conflict. Decision-2 makes git the complete record: every commit subject starts with its task ID, and upstream edits stay small. But reading that out of `git log` and `git diff` by hand is tedious, and it is easy to miss a file.

A small script in `patches-tools/` should list every upstream file the fork changes compared with the release `patches` is based on, grouped by the task whose commits change it. It should also cross-check that list against each task's modified-files field (`--modified-file`), so a task that forgot to record a file, or records one it no longer touches, shows up.

"Upstream files" means everything outside the fork's own paths: `backlog/`, `patches-tools/` and `CLAUDE.md`.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 One command in `patches-tools/` lists every upstream file `patches` changes compared with the upstream release it is based on (found automatically, e.g. with git describe), with how many lines are added and removed
- [ ] #2 Each file is grouped under the task(s) whose commits change it, taken from the task ID at the start of the commit subject; commits without a task ID are listed separately so they stand out
- [ ] #3 The fork's own paths (`backlog/`, `patches-tools/`, `CLAUDE.md`) are left out
- [ ] #4 It warns when a task's modified-files field is missing an upstream file its commits change, or lists one they no longer change
- [ ] #5 It changes nothing: read-only on the repo and the backlog
- [ ] #6 The local test README or a short README in `patches-tools/` says when to run it (before a rebase, and before moving a task to Done)
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Archived 2026-11-19: not needed. Git already shows what the fork touches: the two commands now in CLAUDE.md (git diff --stat against the upstream release, and git log on a file for the tasks behind it). The one thing the script added, a cross-check against a hand-kept modified-files list in each task, was dropped along with that list.
<!-- SECTION:NOTES:END -->
