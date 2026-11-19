# libs

Build inputs. `build.ps1` / `build.sh` fill this folder:

* `libs/valheim/` — game assemblies copied from the dedicated server
  (`valheim_server_Data/Managed`). Publicized at build time by
  Krafs.Publicizer.
* `libs/BepInEx/` — BepInEx 5 downloaded on first build.
* `websocket-sharp/` — the source of websocket-sharp, a git submodule pinned to
  sta/websocket-sharp `a260fe1` (the code of the DLL this folder used to hold: a Debug
  build of that commit). `websocket-sharp.csproj` builds it; the plugin merges it in.
  After cloning, run `git submodule update --init` (or clone with `--recursive`).

Nothing here is committed except `websocket-sharp.csproj` and the submodule's pin.
<!-- patches: TASK-5 (was: websocket-sharp.dll, shipped with the plugin; the only file committed here) -->
