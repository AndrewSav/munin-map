---
id: TASK-31
title: 'Fork CI build fails: UnityEngine.PhysicsModule isn''t copied into libs/'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-11-24T05:08:23Z'
updated_date: '2026-11-24T05:08:57Z'
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
- [ ] #3 The fork's Actions build passes on patches
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-11-24: one marked line each in build.sh and build.ps1 copies UnityEngine.PhysicsModule.dll from the game's Managed folder into libs/valheim/. Checked the way CI builds: with the DLL removed from libs/valheim/, build.sh copied it and the build succeeded; build.ps1 parses. #3 waits for the next Actions run on patches (after a push).
<!-- SECTION:NOTES:END -->
