---
id: TASK-5
title: >-
  Build websocket-sharp from source via a git submodule instead of a committed
  DLL
status: To Do
assignee: []
created_date: '2026-11-18T03:31:00Z'
updated_date: '2026-11-19T22:41:00Z'
labels:
  - build
milestone: Initial release
dependencies: []
references:
  - https://github.com/sta/websocket-sharp
type: chore
ordinal: 5000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
`libs/websocket-sharp.dll` is the only prebuilt library committed to the repo, and the plugin depends on it for its whole web server, not just the websocket: its `HttpServer` serves the page, the data and the tiles, and its websocket service carries the live updates. The DLL can't be audited or rebuilt. It is a build of sta/websocket-sharp (MIT) made on 2021-04-08, assembly version 1.0.2.29017. It came over unchanged from Valheim WebMap's first commit (h0tw1r3/valheim-webmap has the identical git blob `140cbc4`), and the source commit it was built from was never recorded. Its source is now known (see the notes): it is a Debug build (DEBUG defined, so log lines carry source line numbers) whose compiled code is identical to sta/websocket-sharp `a260fe1` (2021-03-20), which only changed a comment from its parent `e52d894`. So it was built on 2021-04-08 from a checkout about three weeks old. More than 100 commits have gone in since, and the library is still maintained.

NuGet is not a good source: the official `WebSocketSharp` package has only pre-releases, the last of them (1.0.3-rc11) from July 2016, older than the DLL in use. The other packages are third-party repackagings.

The approach chosen: add sta/websocket-sharp as a git submodule pinned to a commit, and compile its source files directly into MuninMap. websocket-sharp's own project file is an old-style .NET 3.5 project with a strong-name key, and it does not build under `dotnet build` on Linux CI. That is why its sources go into MuninMap's build instead of the build referencing that project.

Pin choice: `a260fe1` built in Debug reproduces today's DLL exactly. The latest master brings five years of fixes but needs more testing of the web server and the live connection. A Release build of the same commit differs only in leaving out the source line numbers in log lines.

This touches upstream files (`src/MuninMap.csproj`, `src/ILRepack.targets`, `libs/README.md`, build docs), so it is a source of rebase conflicts on `patches`: keep those edits small, per decision-2.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 `libs/websocket-sharp.dll` is gone from the repo, and no websocket-sharp DLL is committed, downloaded or referenced by the build
- [ ] #2 websocket-sharp comes from a git submodule pinned to a specific sta/websocket-sharp commit; `libs/README.md` names the commit and why it was chosen
- [ ] #3 `build.ps1` and `build.sh` produce a working plugin from a fresh `git clone --recursive`
- [ ] #4 Building from a clone without the submodule fails with a message saying to run `git submodule update --init`
- [ ] #5 The package still ships a single `MuninMap.dll`, and the websocket-sharp types inside it cannot clash with another mod's copy of websocket-sharp
- [ ] #6 On a real server, everything websocket-sharp carries still works: the map page, its data and tiles, the live updates, reconnecting after a server restart, the link preview and the Discord webhook
- [ ] #7 `THIRD-PARTY-NOTICES.md` still credits websocket-sharp and says it is compiled from source at the pinned commit
- [x] #8 Before choosing the pin: websocket-sharp built from `5b1f6be` is compared with `libs/websocket-sharp.dll` by compiled code (types, members and IL; not bytes, which differ between any two builds), and the result is recorded in the notes: same code, or what differs
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Provenance of libs/websocket-sharp.dll, checked 2026-11-20. Method: built sta/websocket-sharp from source with a minimal SDK-style project (only its .cs files, its own signing key, optimisation off as in its original project), decompiled both DLLs with ILSpy (ilspycmd 11.2, one file per type) and compared the 77 type files.
- 5b1f6be (the last commit before the DLL's build time), Release: 4 files differ in real source (HttpListener _ctxRegistry vs _contextRegistry; ChunkStream WantMore/ChunkLeft vs WantsMore; ChunkedRequestStream's Decoder property; LogData line numbers). So not built from it.
- 511927c (2021-03-23, the newest commit with the DLL's names), Release: 3 differ. LogData's line numbers are #if DEBUG, so the DLL is a Debug build; ChunkLeft was removed in 897a579 and 84a41f2.
- a260fe1 (2021-03-20, "[Modify] 2021"), Debug: 0 of 77 type files differ; AssemblyInfo differs only in the auto-generated build number (1.0.2.29017 vs 1.0.2.20877). a260fe1 only changed a copyright comment in ChunkStream.cs, so its parent e52d894 compiles to the same code; either reproduces the DLL.
Conclusion: the committed DLL is a Debug build of the code at a260fe1 / e52d894, built on 2021-04-08 from a checkout about three weeks old. 897a579 (Debug) differs in 1 file and 84a41f2 in 2.
<!-- SECTION:NOTES:END -->
