---
id: TASK-31
title: 'Fork CI build fails: UnityEngine.PhysicsModule isn''t copied into libs/'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-24T05:08:23Z'
updated_date: '2026-11-24T05:11:34Z'
labels:
  - ci
  - build
dependencies: []
type: bug
ordinal: 38000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The fork's build (Actions run 45325791370, on the push of TASK-4.6's merge) fails: src/Patches/InPlaceOre.cs uses Collider, which is in UnityEngine.PhysicsModule. src/Directory.Build.targets references it at libs/valheim/UnityEngine.PhysicsModule.dll, but build.sh and build.ps1 copy only a fixed list of game assemblies into libs/valheim/, and that list doesn't have it. Local builds passed only because the test server's deploy script copies every game assembly into libs/, which hid the gap. Every build from a clean checkout fails the same way, in CI and for anyone building the fork from source.

Ref: TASK-4.6 (in-place prediction), decision-2 (small marked upstream edits).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 build.sh and build.ps1 copy UnityEngine.PhysicsModule.dll into libs/valheim/, each through one line marked as a fork edit
- [x] #2 A build from a libs/valheim/ without the physics module succeeds with build.sh, the way CI builds
- [x] #3 The fork's Actions build passes on patches
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-11-24: one marked line each in build.sh and build.ps1 copies UnityEngine.PhysicsModule.dll from the game's Managed folder into libs/valheim/. Checked the way CI builds: with the DLL removed from libs/valheim/, build.sh copied it and the build succeeded; build.ps1 parses. #3 waits for the next Actions run on patches (after a push).

#3: Actions run 45331300315 on 3460c5b passed (the first green build on patches since TASK-4.6's merge, covering TASK-30 and TASK-4.7 too); its artifact holds MuninMap.zip and SHA256SUMS only, as TASK-4.7 intended.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
The fork's build failed from a clean checkout since TASK-4.6: in-place uses UnityEngine.PhysicsModule, referenced at libs/valheim/, but build.sh and build.ps1 copy only a fixed list of game assemblies there. One marked line in each now copies the physics module too. Checked with a build from a libs/ without it, and by the next Actions run on patches, which passed.
<!-- SECTION:FINAL_SUMMARY:END -->
