---
id: TASK-36
title: 'Valheim 1.0.30 dropped System.Memory: merge it into MuninMap.dll'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-25T12:57:40Z'
updated_date: '2026-11-25T13:22:32Z'
labels:
  - server
dependencies: []
type: bug
ordinal: 43000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Valheim patch l-1.0.30 removed System.Memory.dll, System.Runtime.CompilerServices.Unsafe.dll and System.IO.Hashing.dll from valheim_server_Data/Managed (l-1.0.21 had them). src/ILRepack.targets left System.Memory and Unsafe out of the merge because the game used to ship them, so on l-1.0.30 the merged ImageSharp types that use Span/Memory fail to load. At startup HarmonyX logs AccessTools.GetTypesFromAssembly: assembly MuninMap => ReflectionTypeLoadException (14 types, System.Memory 4.0.1.1 not found). WebP.cs catches the failure on first use and turns WebP tiles off (PNG fallback), and ToPng for browsers without WebP fails too. Seen on the live server after the patch, 2026-11-25.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 On Valheim l-1.0.30 the server log has no ReflectionTypeLoadException or System.Memory load error from MuninMap
- [x] #2 WebP tile conversion works on l-1.0.30 (a tile converts to WebP and a WebP tile can be served as PNG), with no "WebP tiles are off" warning
- [x] #3 The rest of Munin starts as before on l-1.0.30: Harmony patches apply, world sweep, tiles, in-place ore, HTTP server
- [x] #4 The build still loads on a game that does ship its own System.Memory (no clash with the game copy)
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Reproduce on the local test server updated to l-1.0.30.
2. Add System.Memory and System.Runtime.CompilerServices.Unsafe to the ILRepack inputs (internalized like the other System DLLs); check the merged DLL no longer references them.
3. Deploy to the test server, check the log and exercise WebP both ways.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Reproduced on the local test server after update.ps1 to l-1.0.30 with the old DLL: HarmonyX ReflectionTypeLoadException (System.Memory 4.0.1.1); a stored WebP tile requested with Accept: image/png gave 500 and "WebP tiles are off" (TypeInitializationException in SixLabors.ImageSharp.Configuration).
Fixed build (compiled against the l-1.0.30 assemblies): assembly refs no longer include System.Memory or System.Runtime.CompilerServices.Unsafe; no ReflectionTypeLoad/System.Memory line in the log; tile 4/10/3 served as WebP (540 B) and as PNG (3824 B); the same tile put back as PNG was converted to WebP on start ("converted 1 PNG tiles to WebP"). Harmony/Chainloader 1 loaded 0 failed, in-place ore self-check passed, location ground 81/81, runestones, HTTP server all as before.
AC4 is shown statically: the merged DLL references neither assembly, so a game copy is never bound (the merged types are internal). Not run on an older game, as the test server is now on l-1.0.30.
Patch review (decompiled assembly_valheim l-1.0.21 vs l-1.0.30): ZoneSystem only renames SoftReference.Name to .name and makes global keys case-insensitive; PlaceVegetation, WorldGenerator, Heightmap and Location are unchanged, so in-place ore needs nothing. The Forge of Potential move is location data the game applies when it places locations on a new world; Munin reads m_locationInstances. TerrainComp data format v2 appends a double after the paint grid; TerrainPatches.Decode stops before it, so it still decodes.
Upstream fix: branch fix/system-memory (off upstream/main), commit 003cc8a.

Windows check: the Windows 1.0.30 client and dedicated server don't ship System.Memory, Unsafe or System.IO.Hashing either. The fixed DLL on a Windows 1.0.30 dedicated server (steamcmd 896660, fresh world): no ReflectionTypeLoad or System.Memory line, 629 fresh tiles converted to WebP, a tile served as WebP (40936 B) and as PNG (59139 B).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Valheim l-1.0.30 dropped System.Memory and Unsafe from Managed; src/ILRepack.targets now merges both (internalized) so ImageSharp loads. Verified on the l-1.0.30 test server: no ReflectionTypeLoadException, WebP encode and WebP-to-PNG work, the rest of startup as before. Reviewed the game diff for anything else Munin depends on: nothing else needs changing. Same fix on branch fix/system-memory for upstream.
<!-- SECTION:FINAL_SUMMARY:END -->
