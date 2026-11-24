---
id: TASK-30
title: Move the fork's web files into web/js/patches/
status: Done
assignee:
  - '@claude'
created_date: '2026-11-23T21:29:09Z'
updated_date: '2026-11-24T04:28:24Z'
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
- [x] #1 The seven fork files are in web/js/patches/ and none is left in web/js/
- [x] #2 Every upstream file still has only its marked one-line import changes from the fork; no other upstream line changes
- [x] #3 In the browser every fork feature still loads and works (Settings group, spoilers, clock, claims, seed line, predicted ore layer, built-by), with no console errors, served from the DLL as on live
- [x] #4 Path mentions in comments, PATCHES.md and docs point at the new paths
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. git mv the seven files into web/js/patches/ (history follows the move).
2. Imports: in upstream files only our marked lines change ('./x.js' -> './patches/x.js', '../x.js' -> '../patches/x.js'); in the moved files, imports of upstream modules gain '../', imports between fork files stay './'.
3. Path mentions: the marked comments in ui.js and vegpack.js, fork C# comments (OreLayer.cs, PredictedOre.cs), docs/PATCHES.md if any.
4. Checks: parse every changed module as ES module; the server's import map and module preloads list the new paths, served from the DLL; every fork feature checked in the browser with no console errors.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-11-24: moved with git mv (history follows). Upstream files: 14 changed lines in app.js, ui.js, net.js, data.js, biomehover.js, layers/markers.js, vegpack.js, every one already marked '// patches: TASK-n' (11 imports, 2 comments naming the file); no unmarked line changed. The moved files' imports of upstream modules gained '../'; imports between fork files stay './'. Checks: all changed modules parse as ES modules; all 135 relative imports under web/js resolve to existing files; the server's import map and module preloads (MapDataServer.StampIndex, built from the files it finds, subfolders included) list the seven files under js/patches/ with no old paths; the files are served from the DLL. The user checked in the browser, served from the DLL: Settings group, spoilers, clock, claims, seed line (show_seed switched on for the check), predicted ore layer and Server details rows, built-by, with no console errors. Fork C# comments (OreLayer.cs, PredictedOre.cs) point at the new path; PATCHES.md and docs named none of the files.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
The fork's seven web files (ore-layer.js, patches-settings.js, seed.js, spoilers.js, claims.js, clock.js, builtby.js) moved from web/js/ into web/js/patches/, so the fork's JavaScript has a folder of its own like src/Patches/. Only our own marked lines changed in upstream files (11 import paths, 2 comments); the moved files' imports of upstream modules gained '../'. No behaviour change: every relative import resolves, the server's import map and preloads pick up the subfolder by themselves, and every fork feature was checked in the browser with no console errors.
<!-- SECTION:FINAL_SUMMARY:END -->
