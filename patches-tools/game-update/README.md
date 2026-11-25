# Game-update routine

What to do after every Valheim patch, before the fork's next release (TASK-37). Upstream has no such
routine, and the fork copies game behaviour (in-place ore) and reads game data by hand, which a patch
can change without any error. Run the steps in order; each says how to tell it passed. Record the
results in a backlog task for the patch, and add `- TASK-n: Verified for Valheim X.Y.Z` to
`patches-tools/release-notes.md` when step 5 raises the verified version.

Fork-only tooling: it lives on the `patches` branch, in this folder.

Needs: everything `patches-tools/local-test` needs, and `ilspycmd` (`dotnet tool install -g ilspycmd`).

## 1. Update the test server

```powershell
.\patches-tools\local-test\update.ps1
```

Before steamcmd replaces anything, it keeps the old version's game assemblies in
`patches-tools\local-test\data\managed\<old version>` (git ignores `data\`). Step 2 needs them.

Passes when it prints the same Valheim and BepInEx versions for test and live.

## 2. Diff the game

```powershell
cd patches-tools\game-update
.\diff-game.ps1 -Old ..\local-test\data\managed\<old version> -New ..\local-test\data\server\server\valheim_server_Data\Managed
```

It reports, in order:
- **MuninMap.dll's references the new game doesn't provide.** Each one is a type load failure on
  the new version: merge it into the DLL (`src/ILRepack.targets`). This is how 1.0.30 broke WebP
  tiles (it stopped shipping `System.Memory`). Build first (step 3) so it checks the current DLL.
- **DLLs added to or removed from the game's folder.**
- **Every entry of `watch.txt`: same or CHANGED.** Those are the game code fork features copy
  (in-place ore) and the data Munin decodes by hand (the terrain blob, cartography pins).
- **Every other class that changed**, for a skim: a Harmony patch target or a field Munin reads
  might be among them. `git diff --no-index <out>\old\...\X.cs <out>\new\...\X.cs` shows a change.

Passes when nothing is missing and every CHANGED entry has been read and is either harmless
(a rename, an API change that compiles) or fixed. In particular:
- `TerrainComp.Save` / `Load` changed: `src/Tiles/TerrainPatches.cs` reads the blob field by field.
  Fields added at the end are harmless (it stops after the paint grid); anything before that breaks it.
- Any in-place entry changed: update `src/Patches/InPlaceOre*.cs` to match, then step 5 decides.
- An entry `NOT FOUND`: the game renamed or removed it; fix `watch.txt` (and the code that copies it).

When a fork feature starts copying or parsing more of the game, add it to `watch.txt`.

Compare a plain copy with a plain copy where you can: a publicized one (from `src/obj`) also shows
compiler-generated event fields as changes.

## 3. Rebuild and run on the test server

```powershell
.\patches-tools\local-test\deploy.ps1
```

It builds against the test server's new game assemblies. A compile error is an API change.

Then read the log (`docker logs munin-test`). Passes when:
- `Chainloader startup complete (1 loaded, 0 skipped, 0 failed)`;
- no `ReflectionTypeLoadException`, `TypeLoadException`, `MissingMethodException`,
  `MissingFieldException` or Harmony error mentioning MuninMap;
- Munin's start lines are there as before: `HTTP server listening`, `in-place ore: random numbers
  and terrain match the game's`, `location ground: ... failed 0`, `world sweep #1`;
- no `WebP tiles are off`. To use the WebP code both ways, ask for a stored tile as WebP and as PNG
  and expect 200 for both (`curl -H "Accept: image/png" http://127.0.0.1:3000/tiles/map/4/10/3.png`).

## 4. Windows

The test server is Linux. Valheim ships separate builds, so check the Windows dedicated server
starts Munin too: install it with steamcmd (`+login anonymous +app_update 896660`) into a scratch
folder, add BepInEx (BepInExPack_Valheim: `winhttp.dll`, `doorstop_config.ini` with
`enabled = true`, `BepInEx\core`) and `MuninMap.dll` in `BepInEx\plugins`, set `server_port` to a
free port in `BepInEx\config\com.cocopuff2u.muninmap.cfg`, and start it on a throwaway world:

```
valheim_server.exe -nographics -batchmode -name test -port 2470 -world test -password test12345 -public 0 -savedir <folder> -logFile <file>
```

Passes on the same log checks as step 3 (`BepInEx\LogOutput.log` and the log file).

## 5. In-place ore: measure it

The startup self-check covers Unity's random numbers and the zone terrain only. The rest of in-place
(the placement loop, locations, blocking, the components' random draws) is copied code, and the game's
placement data (vegetation and location settings, which a patch can change without touching code:
1.0.30 moved the Forge of Potential further out) comes from the running game. Measuring it against
the game's own generation covers all of it.

1. A world made on the new version: the step 4 server, with `predict_ore = true` and
   `reveal_all = true` in Munin's config and `-saveinterval 60`, on a new `-world` name. Wait for
   `in-place ore: done` in the log and a `_main.N.ok` in `<savedir>\worlds_local\<world>\`, then
   stop it. In-place's result is `BepInEx\config\MuninMap\<world>\predicted-ore.csv`.
2. The reference: `ore-extract run --save <savedir>\worlds_local\<world> --server-dir <server folder> --out ref.csv`
   (`patches-tools/ore-extract`, about 10 minutes with 4 processes; it says so if the new game
   broke its plugin).
3. Compare: `python -I patches-tools\ore-extract\compare.py predicted-ore.csv ref.csv --base-save <savedir>\worlds_local\<world> --tol 0.5`.

Passes when the whole is about as accurate as last time (the table below; within half a percentage
point or so) and no ore falls clearly outside its spread there. Silver has only about 500 deposits a
world, so one deposit is 0.2 points and it moves by a point between worlds on the same version. Then:
- raise `CheckedAgainst` in `src/Patches/InPlaceOre.cs` to the new version (this clears the "Verified
  on" note in Server details), and the version in PATCHES.md's TASK-4 entry;
- add the row to the table.

If it falls short, don't raise it: find the difference (step 2's diff, then `compare.py`'s misses per
ore), fix in-place, measure again.

| Valheim | World | Copper | Muddy scrap | Silver | Tin | Obsidian | All |
|---|---|---|---|---|---|---|---|
| 1.0.21 | TASK-27 snapshot (generated copy) | 99.8% | 99.4% | 97.0% | 100.0% | 98.1% | 99.2% |
| 1.0.21 | new random seed (extract) | 99.9% | 99.3% | 98.0% | 100.0% | 98.0% | 99.2% |
| 1.0.30 | new world made on 1.0.30, Windows server (extract) | 99.9% | 99.5% | 96.3% | 100.0% | 98.4% | 99.3% |

## 6. Done

Close the patch's task with each step's result. The release that ships the result carries the
`Verified for Valheim X.Y.Z` line, and its notes say so.
