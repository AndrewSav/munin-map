---
id: TASK-5
title: >-
  Build websocket-sharp from source via a git submodule instead of a committed
  DLL
status: Done
assignee:
  - '@claude'
created_date: '2026-11-18T03:31:00Z'
updated_date: '2026-11-19T22:55:00Z'
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

The approach chosen (agreed 2026-11-20): add sta/websocket-sharp as a git submodule at `libs/websocket-sharp/`, pinned to `a260fe1`, and build it with a small SDK-style project of our own, `libs/websocket-sharp.csproj`, using the library's own settings (net35, DEBUG defined, optimisation off, its signing key). websocket-sharp's own project file is an old-style .NET 3.5 project that does not build under `dotnet build` on Linux CI. `MuninMap.csproj` references our project instead of the DLL. Its output is still `websocket-sharp.dll` in Munin's output folder, so the ILRepack merge into the single MuninMap.dll is unchanged. Compiling the sources straight into MuninMap was considered and dropped: it needs more upstream edits, and it cannot reproduce the Debug build without defining DEBUG for Munin's own code too.

Pin: `a260fe1` built as Debug, which reproduces today's DLL exactly. Moving to the latest master (five years of fixes, changed behaviour in the web server everything runs through) is left for a separate task.

Upstream files touched: one reference line in `src/MuninMap.csproj`, the text in `libs/README.md` and `THIRD-PARTY-NOTICES.md`, and the deleted `libs/websocket-sharp.dll`. Keep those edits small, per decision-2.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 `libs/websocket-sharp.dll` is gone from the repo, and no websocket-sharp DLL is committed, downloaded or referenced by the build
- [x] #2 websocket-sharp comes from a git submodule pinned to a specific sta/websocket-sharp commit; `libs/README.md` names the commit and why it was chosen
- [x] #3 `build.ps1` and `build.sh` produce a working plugin from a fresh `git clone --recursive`
- [x] #4 Building from a clone without the submodule fails with a message saying to run `git submodule update --init`
- [x] #5 The package still ships a single `MuninMap.dll`, and the websocket-sharp types inside it cannot clash with another mod's copy of websocket-sharp
- [x] #6 On a real server, everything websocket-sharp carries still works: the map page, its data and tiles, the live updates, reconnecting after a server restart, the link preview and the Discord webhook
- [x] #7 `THIRD-PARTY-NOTICES.md` still credits websocket-sharp and says it is compiled from source at the pinned commit
- [x] #8 Before choosing the pin: websocket-sharp built from `5b1f6be` is compared with `libs/websocket-sharp.dll` by compiled code (types, members and IL; not bytes, which differ between any two builds), and the result is recorded in the notes: same code, or what differs
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. git submodule add sta/websocket-sharp at libs/websocket-sharp, checked out at a260fe1.
2. New libs/websocket-sharp.csproj: SDK-style, net35, compiles only the submodule's websocket-sharp/**/*.cs, DEBUG defined, optimisation off, signed with the library's key, assembly name websocket-sharp. A target fails the build with "run git submodule update --init" when the submodule is missing.
3. src/MuninMap.csproj: the HintPath reference becomes a ProjectReference to it (marked). The output is still websocket-sharp.dll in Munin's output folder, so ILRepack.targets is unchanged.
4. git rm libs/websocket-sharp.dll; update libs/README.md and THIRD-PARTY-NOTICES.md.
5. Verify: the built websocket-sharp.dll decompiles identically to the old committed DLL; the merged MuninMap.dll is still one DLL with the websocket-sharp types internal; the local test server works (page, data, tiles, live updates, reconnect after restart, link preview, Discord webhook against a local listener); a fresh clone --recursive builds with build.ps1; a clone without the submodule fails with the message.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Provenance of libs/websocket-sharp.dll, checked 2026-11-20. Method: built sta/websocket-sharp from source with a minimal SDK-style project (only its .cs files, its own signing key, optimisation off as in its original project), decompiled both DLLs with ILSpy (ilspycmd 11.2, one file per type) and compared the 77 type files.
- 5b1f6be (the last commit before the DLL's build time), Release: 4 files differ in real source (HttpListener _ctxRegistry vs _contextRegistry; ChunkStream WantMore/ChunkLeft vs WantsMore; ChunkedRequestStream's Decoder property; LogData line numbers). So not built from it.
- 511927c (2021-03-23, the newest commit with the DLL's names), Release: 3 differ. LogData's line numbers are #if DEBUG, so the DLL is a Debug build; ChunkLeft was removed in 897a579 and 84a41f2.
- a260fe1 (2021-03-20, "[Modify] 2021"), Debug: 0 of 77 type files differ; AssemblyInfo differs only in the auto-generated build number (1.0.2.29017 vs 1.0.2.20877). a260fe1 only changed a copyright comment in ChunkStream.cs, so its parent e52d894 compiles to the same code; either reproduces the DLL.
Conclusion: the committed DLL is a Debug build of the code at a260fe1 / e52d894, built on 2021-04-08 from a checkout about three weeks old. 897a579 (Debug) differs in 1 file and 84a41f2 in 2.

Verification, 2026-11-20:
- The websocket-sharp.dll built by libs/websocket-sharp.csproj decompiles identically to the old committed DLL (all 77 type files; AssemblyInfo differs only in the auto-generated build number).
- The merged MuninMap.dll holds the websocket-sharp types: HttpServer internal, WebSocket public. Upstream's released 3.4.0 MuninMap.dll is the same, so this is how its ILRepack setup has always worked. They live inside MuninMap.dll, so they cannot clash with another mod's copy.
- Test server: the page (with its 5 og: link-preview tags), markers.json and a map tile all 200. A websocket client got hello, players and events, lost the connection on a server restart, retried every 2 s and reconnected as soon as the server was back. The Discord webhook, pointed at a local listener, received the "online" post on startup.
- A fresh git clone --recursive of patches built and packaged with build.ps1 on Windows, and with build.sh on Linux (cloned inside mcr.microsoft.com/dotnet/sdk:8.0): one MuninMap.dll in the zip. A clone without the submodule fails with "websocket-sharp's source is missing (the git submodule libs/websocket-sharp). Run: git submodule update --init".
- Seen along the way, unrelated to this task: build.ps1 copies only some of the game assemblies the project references, so a fresh build needs them all in libs/valheim first (deploy.ps1 already does this). A Windows checkout gives build.sh CRLF line endings, which bash rejects; a Linux checkout, as in CI, is fine.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
websocket-sharp is now built from source instead of the prebuilt libs/websocket-sharp.dll. The source is a git submodule at libs/websocket-sharp, pinned to sta/websocket-sharp a260fe1. A new libs/websocket-sharp.csproj (SDK-style, net35, DEBUG, optimisation off, the library's own key) builds it, and src/MuninMap.csproj references it instead of the DLL (one marked line). The output is still websocket-sharp.dll in Munin's output folder, so ILRepack still merges it into the single MuninMap.dll. libs/README.md and THIRD-PARTY-NOTICES.md say where it comes from.

The pin reproduces the plugin's code exactly. The old DLL was found to be a Debug build of a260fe1 / e52d894 (2021-03-20), and the new build decompiles identically to it.

Verified: identical decompiled code; same merge result as upstream's 3.4.0 release; page, data, tiles, live updates, reconnect after restart, link preview and Discord webhook on the test server; fresh recursive clones build with build.ps1 (Windows) and build.sh (Linux); a clone without the submodule fails with a clear message.

Follow-up worth considering: moving to websocket-sharp's latest master (five years of fixes) as a separate task, with testing of the web server.
<!-- SECTION:FINAL_SUMMARY:END -->
