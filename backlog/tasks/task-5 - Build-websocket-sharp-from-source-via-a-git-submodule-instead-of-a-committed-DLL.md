---
id: TASK-5
title: >-
  Build websocket-sharp from source via a git submodule instead of a committed
  DLL
status: To Do
assignee: []
created_date: '2026-11-18T03:31:00Z'
updated_date: '2026-11-18T03:46:00Z'
labels:
  - build
dependencies:
  - TASK-6
references:
  - https://github.com/sta/websocket-sharp
type: chore
ordinal: 5000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
`libs/websocket-sharp.dll` is the only prebuilt library committed to the repo, and the plugin depends on it for its whole web server, not just the websocket: its `HttpServer` serves the page, the data and the tiles, and its websocket service carries the live updates. The DLL can't be audited or rebuilt. It is a build of sta/websocket-sharp (MIT) made on 2021-04-08, assembly version 1.0.2.29017. It came over unchanged from Valheim WebMap's first commit (h0tw1r3/valheim-webmap has the identical git blob `140cbc4`), and the source commit it was built from was never recorded. The last websocket-sharp commit before that build is `5b1f6be`. More than 100 commits have gone in since, and the library is still maintained.

NuGet is not a good source: the official `WebSocketSharp` package has only pre-releases, the last of them (1.0.3-rc11) from July 2016, older than the DLL in use. The other packages are third-party repackagings.

The approach chosen: add sta/websocket-sharp as a git submodule pinned to a commit, and compile its source files directly into MuninMap. websocket-sharp's own project file is an old-style .NET 3.5 project with a strong-name key, and it does not build under `dotnet build` on Linux CI. That is why its sources go into MuninMap's build instead of the build referencing that project.

Pin choice: `5b1f6be` is the closest to today's behaviour. The latest master brings five years of fixes but needs more testing of the web server and the live connection.

This touches upstream files (`src/MuninMap.csproj`, `src/ILRepack.targets`, `libs/README.md`, build docs), so it is a source of rebase conflicts on `patches` and a good candidate to offer upstream as a pull request.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 `libs/websocket-sharp.dll` is gone from the repo, and no websocket-sharp DLL is committed, downloaded or referenced by the build
- [ ] #2 websocket-sharp comes from a git submodule pinned to a specific sta/websocket-sharp commit; `libs/README.md` names the commit and why it was chosen
- [ ] #3 `build.ps1` and `build.sh` produce a working plugin from a fresh `git clone --recursive`
- [ ] #4 Building from a clone without the submodule fails with a message saying to run `git submodule update --init`
- [ ] #5 The package still ships a single `MuninMap.dll`, and the websocket-sharp types inside it cannot clash with another mod's copy of websocket-sharp
- [ ] #6 The fork's CI build checks out the submodule and succeeds
- [ ] #7 On a real server, everything websocket-sharp carries still works: the map page, its data and tiles, the live updates, reconnecting after a server restart, the link preview and the Discord webhook
- [ ] #8 `THIRD-PARTY-NOTICES.md` still credits websocket-sharp and says it is compiled from source at the pinned commit
<!-- AC:END -->
