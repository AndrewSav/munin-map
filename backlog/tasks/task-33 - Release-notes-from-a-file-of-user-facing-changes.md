---
id: TASK-33
title: Release notes from a file of user-facing changes
status: Done
assignee:
  - '@claude'
created_date: '2026-11-24T06:32:24Z'
updated_date: '2026-11-24T06:50:19Z'
labels:
  - ci
dependencies: []
type: feature
ordinal: 40000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The fork's release notes are made from commit subjects (patches-tools/ci/release-tag.sh next writes them into the tag's message): developer wording, tooling commits mixed in, changes repeated per commit. Instead, keep a hand-written list of user-facing changes in patches-tools/release-notes.md and build each release's notes from it.

The file: one line per user-facing task or subtask, '- TASK-n: <line>', with a PATCHES.md heading as the line where it fits. A line is added when a user-facing task is finished; before adding it, lines already released (those in the file at the latest patches-* tag) are removed, so the file holds what the next release will announce. Internal and tooling tasks get no line.

Ref: decision-1 (release tags), TASK-6 (the release workflow).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 patches-tools/release-notes.md exists, with its format and upkeep described at the top, and lines for the user-facing changes made since the last release
- [x] #2 A release's notes are: 'Munin Map <version> with this fork's changes (see PATCHES.md).' with PATCHES.md linked, then the file's lines, each with its task ID linked to the task's file; links point at the release's tag
- [x] #3 The publish step builds the notes from the file at the tagged commit, for both Run workflow and a hand-pushed tag
- [x] #4 A release fails, before anything is tagged or published, when the file is the same as at the previous patches-* tag
- [x] #5 No upstream files change
- [x] #6 A release on a new upstream version fails unless the file has the line '- Updated to Munin Map X.Y.Z'; lines without a task are published as they are
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. patches-tools/release-notes.md: format and upkeep at the top, seeded with the user-facing changes since patches-3.4.0-7 (TASK-4.6, 4.7, 32).
2. release-tag.sh: check and next fail when the file's '- TASK-' lines equal those at the latest other patches-* tag; next's tag message is the header line only; new 'notes <tag> <repo-url>' prints the header (PATCHES.md linked) and the lines with task IDs linked, all at the tag.
3. patches.yml: the publish step takes its notes from 'release-tag.sh notes'.
4. Test in a throwaway clone: next with new lines, next with nothing new, check, notes at a tag with and without the file; a generated task link resolves on GitHub.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Tested in a throwaway clone: next made patches-3.4.0-8 with new lines and refused a second release with nothing new; check accepted the tag; notes rendered the header and three linked lines (and the header only at an old tag without the file); a %20-encoded task link returned 200 on GitHub. AC 3 (the publish step in CI) is checked at the first real release.

Release on a new upstream version (side check raised by a review): needs '- Updated to Munin Map X.Y.Z', added after the rebase. Tested in a throwaway clone with the manifest bumped to 3.5.0: refused without the line, released with it; the line is published as is.

First real release, patches-3.4.0-8 (Run workflow): published with the header line and the three lines, PATCHES.md and task links resolving at the tag.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Release notes now come from patches-tools/release-notes.md, a hand-kept list of user-facing changes (one line per task or subtask, plus 'Updated to Munin Map X.Y.Z' after a rebase onto a new upstream version). release-tag.sh builds them at the release's tag: the header line with PATCHES.md linked, then the lines with their tasks linked. A release fails before tagging when the list has nothing new since the previous release, or when it moves to a new upstream version without the Updated line. The upkeep rule is in CLAUDE.md. Verified in a throwaway clone (all refusal and release cases) and by the first real release, patches-3.4.0-8.
<!-- SECTION:FINAL_SUMMARY:END -->
