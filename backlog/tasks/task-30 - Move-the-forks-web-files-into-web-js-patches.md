---
id: TASK-30
title: Move the fork's web files into web/js/patches/
status: To Do
assignee: []
created_date: '2026-11-23T21:29:09Z'
updated_date: '2026-11-23T21:29:27Z'
labels:
  - web
dependencies: []
type: chore
ordinal: 36000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The fork's C# lives in src/Patches/, but its JavaScript sits loose in web/js/ beside upstream's files: ore-layer.js, patches-settings.js, seed.js, spoilers.js, claims.js, clock.js, builtby.js. Move them into web/js/patches/ so the fork's web code is in one folder of its own, like the C#.

Checked (2026-11-24): nothing on the server names these files by path (the DLL embeds web/** recursively, src/MuninMap.csproj:29); C# mentions them only in comments. The upstream lines that change are our own marked import lines (11 of them in app.js, ui.js, data.js, net.js, biomehover.js, layers/markers.js, each already '// patches: TASK-n'); the moved files' imports of upstream modules become '../x.js'. Fork-only restructuring, no behaviour change, so no PATCHES.md entry beyond path mentions.

Ref: decision-2 (rebases easily).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 The seven fork files are in web/js/patches/ and none is left in web/js/
- [ ] #2 Every upstream file still has only its marked one-line import changes from the fork; no other upstream line changes
- [ ] #3 In the browser every fork feature still loads and works (Settings group, spoilers, clock, claims, seed line, predicted ore layer, built-by), with no console errors, served from the DLL as on live
- [ ] #4 Path mentions in comments, PATCHES.md and docs point at the new paths
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
