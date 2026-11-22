---
id: TASK-4.3
title: Ship ore-extract with the fork's releases
status: To Do
assignee: []
created_date: '2026-11-22T22:22:18Z'
updated_date: '2026-11-22T22:34:07Z'
labels:
  - tooling
  - release
dependencies: []
parent_task_id: TASK-4
type: feature
ordinal: 32000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
ore-extract (TASK-4.2's ore-extract, renamed) generates a copy of a world headless and writes its ore deposits; it is fork tooling in patches-tools today. TASK-4's approach makes it part of the product: admins run it by hand for `predict_ore = file`, and the server extract (`predict_ore = server`) needs it on the server. So it has to come with each release, built from the same commit as Munin, for Windows and Linux, and live where product code lives rather than in patches-tools.

Ref: TASK-4 (approach in its notes), TASK-4.2 (patches-tools/zonegen, build.sh, README), .github/workflows/patches.yml (the fork's release workflow), decision-1 (release tags).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 zonegen is renamed ore-extract throughout what ships: folder, binaries and bundles (ore-extract-<version>-win-x64.zip, -linux-x64.tar.gz), the project, the BepInEx plugin inside it (OreExtract, with its own ID), its environment variables (OREEXTRACT_*), its work folder and its README; TASK-4.1/4.2 and the history keep the old name
- [ ] #2 Each fork release built by the release workflow carries the ore-extract bundles for Windows x64 and Linux x64 as release assets next to MuninMap.zip, listed in SHA256SUMS
- [ ] #3 ore-extract is built from the same commit as Munin, and its version names the Munin release it belongs to
- [ ] #4 ore-extract moves from patches-tools/zonegen to a new top-level ore-extract/ folder (plugin/, cli/, its README and build script), a folder upstream doesn't have, so a rebase can't conflict there; patches-tools keeps only what isn't shipped (decision-2)
- [ ] #5 A release without the bundles fails the workflow instead of publishing half a release
- [ ] #6 PATCHES.md has an entry: what admins get and how it differs from upstream
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
