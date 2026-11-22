---
id: TASK-4.2
title: 'Standalone ore-export CLI: fast and background generation from a seed'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-11-22T06:02:37Z'
updated_date: '2026-11-22T11:08:21Z'
labels:
  - tooling
dependencies: []
documentation:
  - >-
    backlog/docs/doc-2 -
    Ore-placement-how-the-game-places-vegetation-in-a-zone.md
parent_task_id: TASK-4
type: task
ordinal: 31000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
To judge TASK-4's methods (criterion #8: the copy method against re-running placement in Munin), the generator has to work where admins would run it, without that setup. This task makes it a standalone tool and measures it there. Hooking it into Munin is a later step and not part of this task.

Two modes from one tool. Fast: on a machine nobody else needs, as many processes as the CPU and memory allow, minutes for a whole world. Slow: one low-priority process next to a running game server, where hours are acceptable. The natural home of the slow mode is the game server's own container, where a dedicated server is already installed.

Platforms: game clients are almost always on Windows and dedicated servers often in Linux containers, so both matter. Docker is not required: steamcmd and the dedicated server for the current platform are enough.

Why the input is a copy of the world, not just its seed (game code, 1.0.21): `WorldGenerator` reads only the seed (`World.m_seed`, the stable hash of the seed name) and `World.m_worldGenVersion` (2 for every world created since that field was added; 0 and 1 change mountain distance, Mistlands noise and swamp distance). Alt-biome sectors are derived from the seed too; `m_uid` and the world name play no part. But the location plan lives in the save: every planned location instance with its position and whether it is placed (its zone generated). On load, if the save's location version differs from the game's (`ZoneSystem.m_locationVersion`: 1 in the code, but the scene sets it, and the live save of 2026-11-22 records 32), the unplaced instances are dropped and planned again around the placed ones (`ClearNonPlacedLocations`, `GenerateLocations`); planning is seeded per location type from the world seed. So a copy of the save, loaded by the same game version, gets exactly live's plan, while a fresh world from the seed plans everything with nothing placed and can differ on a world that has been re-planned. The seed-only route is therefore exact only for worlds never re-planned, and is kept as an option for a world the admin knows is fresh (just created, or never through a location-version change), where a seed and version are simpler than copying files. The tool must run the same game version (and world-generation mods) as the world's server.

Ref: TASK-4.1 (patches-tools/zonegen: ZoneGen.cs, run.ps1, bench.ps1), doc-2 'Measured'.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 One tool with the same options runs on Windows x64 and Linux x64, from a release bundle, without Docker or build tools
- [ ] #3 Output is one file of deposits (prefab, position, zone) for a configurable prefab list whose default is the ore deposits, headed by the input (world copy or seed), seed, world-generation version, game version, tool version and zone count; the output of all processes is joined into it
- [ ] #4 Fast mode defaults to a process count that uses 80% of the logical CPU threads (divided by the threads one process uses, as measured), lowered so the processes fit in available memory; a command-line option sets the count
- [ ] #5 Peak memory per process is measured across at least three seeds and several process counts, and the memory limit in fast mode is based on that measurement
- [ ] #6 Slow mode runs one process at the lowest CPU priority, and can be held to a single core
- [ ] #7 The tool works only on its own copy in its own folder: it never writes the world files it was given or any other save, and its server process does not take ports or a server listing that clash with a game server running on the same host or in the same container
- [ ] #8 The tool, not a server next to it, is what runs out of memory first: on Linux its server process sets its own OOM score to the maximum (oom_score_adj 1000, no privileges needed), so the kernel picks it over any other process in the same container or host; on Windows it runs under a job object with a memory limit
- [ ] #9 A memory guard reads the memory actually available to it (the container cgroup limit and use where there is one, otherwise the system), refuses to start below a margin, and stops its processes cleanly if headroom falls under the margin during a run
- [ ] #10 Accuracy, against the TASK-27 snapshot over the zones live had not explored: a run on a copy of the live world and a seed-only run are both compared, and their deposit match rates, and the location match rate of the seed-only run, are recorded
- [ ] #12 Before any test on a live server, a Linux test outside it: the tool runs in a memory-capped container next to a process standing in for a game server, and it is shown that the memory guard stops the tool and that, with the guard off, the kernel kills the tool and not the stand-in
- [ ] #14 A README covers both modes, platforms, downloads and disk use, and warns that fast mode uses most of the CPU and memory
- [ ] #15 Results are recorded in TASK-4 notes as input to criterion #8; the tool lives in patches-tools and nothing goes into the product
- [ ] #16 The README says the tool must run the same game version and world-generation mods as the world server, and that the seed-only input is exact only for worlds whose location plan was never redone by a location-version change
- [ ] #17 The main input is a copy of a world save: the world folder of the current chunked format (_main.N.fwl2, .db2, .chunks, .ok and .chunk files, one save number) or the older .fwl and .db pair; the tool generates the zones that world has not generated yet and keeps its own locations. A seed name plus world-generation version (default 2) is accepted instead for a world known to be fresh. A utility command prints, from a save, the seed, world-generation version and location version (from .db2 or .db), next to the location version of the installed game, and warns when they differ (the server re-plans unplaced locations on its next start)
- [ ] #18 Each process writes its deposits as it goes: a zone is written once it and its neighbours are generated, so a stopped or killed run keeps everything written so far
- [ ] #19 zonegen run --resume continues a stopped run: zones already written are kept and skipped, the rest are generated, and the output is the same as an uninterrupted run apart from generation-order differences near the skipped zones
- [ ] #20 A process drops the objects of zones that are written and have no ungenerated neighbour left; its effect on memory and accuracy is measured against keeping them and recorded
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Layout: patches-tools/zonegen/plugin (the TASK-4.1 BepInEx plugin, evolved) and patches-tools/zonegen/cli (a .NET 8 console app, self-contained single-file for win-x64 and linux-x64, invariant globalization, the plugin DLL embedded as a resource). Spike scripts run.ps1/bench.ps1 stay for the local test image.
2. Plugin: still driven by ZONEGEN_* variables, defaults = the measured best (back to back, 30 ms budget, window 8, uncapped, terrain ahead, no saves, no dungeon interiors). New: ZONEGEN_PREFABS (list, default the five ores); vegetation cut after the last listed entry (none if a prefab is not vegetation); ZONEGEN_SEED + ZONEGEN_GENVER (Harmony prefix on World.GetCreateWorld creates the world from that seed when there is no save); probe mode (postfix on ZoneSystem.Awake logs m_locationVersion and quits); logs the game build, location versions and whether locations were re-planned on load; output CSV per process.
3. CLI commands: `run` (--save PATH | --seed NAME [--gen-version 2]; --out; --processes N | --slow [--cores N]; --prefabs; --server-dir; --work-dir; --keep), `info --save PATH` (seed, gen version, location version from .fwl2/.db2; the game location version via a probe, cached per server build), `setup` (downloads only).
4. Downloads into the work dir (LocalAppData / XDG data): steamcmd for the platform, app 896660 anonymous, BepInExPack_Valheim 5.4.2360 from Thunderstore, sha256 6bca77b1016acfe0ffaebb6534c84b12ae8d12e5a96abbf59ec6e4526204fc6f. Linux preflight: report missing system libraries (steamcmd 32-bit, libatomic, libpulse) with the install line.
5. Per process: its own BepInEx root (copy of the pack core + our plugin only, so no other plugins such as Munin load), own -savedir with its own copy of the world, own free UDP port pair, -public 0, random password. Launch: Linux env DOORSTOP_ENABLED/DOORSTOP_TARGET_ASSEMBLY/LD_PRELOAD from our pack (nothing written to the server dir, so an existing install such as a container's is used read-only); Windows winhttp.dll in the managed install + --doorstop-target-assembly (an existing Windows install without the doorstop dll is refused rather than changed).
6. Save copy: newest save number with .ok; copy its _main files and chunk files; recheck the .ok and retry if it vanished. Old .fwl/.db pair copied as is.
7. Resources: default processes = floor(0.8 x logical CPUs / threads per process, measured), capped by available memory / measured peak; memory guard (Linux cgroup v2/v1 limit and use, else MemAvailable; Windows GlobalMemoryStatusEx) refuses below margin and stops children if headroom falls under it; Linux oom_score_adj 1000 on each child; Windows job object with a per-process memory limit; slow mode = Idle priority (nice 19) and affinity to N cores.
8. Progress from child logs every 30 s, failures reported with the last log lines; outputs joined into one CSV with a # header (input, seed, gen version, location versions, game build, tool version, zones, processes, time).
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Built (2026-11-22): plugin moved to patches-tools/zonegen/plugin (seed-created worlds, prefab list, vegcut auto, probe mode, re-plan detection, @info/@progress/@done lines in its own log); CLI in patches-tools/zonegen/cli (.NET 8, self-contained single file, about 12 MB per platform, plugin embedded); compare.py for accuracy.
Against the TASK-27 snapshot over every zone the base save had not generated: 99.2% (copper 99.9, tin 99.9, scrap 99.9, silver 97.3, obsidian 98.1), as the spike (99.3%).
Bugs found by the first runs: a folder path with a trailing slash gave an empty world name and the server silently created a new random world; fixed, and the tool now stops if a process loads a world whose seed or generated-zone count differs from the save. Bare Debian 12: .NET aborted on missing libssl; the start-up check now lists libssl, CA certificates, 32-bit libgcc (steamcmd) and libatomic with the install lines.

Linux and Windows outputs match each other 99.4% (generation order near zone edges differs between parallel runs).

The machine sat at about 60% CPU and 62% memory: the flat 3.5 GB per process estimate limited it. Memory grows with the zones a process generates (about 1.5 GB base plus a share of about 8 GB of world objects in these runs), so the estimate is to be replaced by a measured model (criterion #6).

So this world was not re-planned since it was created, and a seed-only run reproduces it apart from the unique sites already settled.

Added (user request 2026-11-22): incremental writing, resume and object dropping. A zone is written (rows flushed, then its line in <tag>-zones.txt) once it and its 8 neighbours are done in that process; with ZONEGEN_DROP its objects are then released (ZDOMan sector list and m_objectsByID, ZDOPool.Release: what HandleDestroyedZDO does without the network part). The run folder keeps its own copy of the input and run.txt; `zonegen run --resume DIR` folds the written zones into prev- files (rows of a zone missing from the zones file are dropped), recopies the world and skips those zones.
Installs (user request): by default the tool now uses a server already on the machine (Steam's Valheim dedicated server from the registry and libraryfolders.vdf, or /opt/valheim/bepinex or /opt/valheim/server in a server container, or the usual Linux Steam paths) and that server's BepInEx if it has one; else its own install, reused as it is. --update (opt-in, on run and setup) updates only its own install; --own-server forces its own.
Game updates: the plugin binds to game members by name at run time, so it survives patches that don't change what it uses; one that does now fails loudly: Harmony patching and the first-frame code are wrapped and write an @error line, and the tool stops a process that hasn't reported in 10 minutes.

Object dropping measured (2026-11-22): whole world, 5 processes: 99.2% against the snapshot (same as without), peak 3.02 GB per process (3.09 without), 13.65 GB summed (14.9). One process, 12,000 central zones: managed heap 320-340 MB with dropping, 460 MB without; process total 1.5 to 1.9 GB either way. So a process's memory is mostly the game assets it has loaded for the biomes and location types it has met, which stay referenced: about 3.0-3.4 GB per process for a whole-world run, whatever the process count. The earlier "base plus a share of the objects" model was wrong; the flat estimate per process is the right shape. Dropping stays on by default (no accuracy cost, about 0.1 GB less).

Peak per process is 2.6-2.9 GB on Windows whatever the seed or count (3.35 GB in the Linux run), so the memory check keeps 3.5 GB per process.
Linux: the plugin now quits when its zonegen tool is gone (ZONEGEN_PARENT_PID, checked every 5 s through /proc), since on Linux nothing ends a child when the tool is killed or its session drops, and the memory guard lives in the tool.

(3) zonegen killed with SIGKILL after the world loaded: its server logged "the zonegen tool is gone" 3 s later and was gone within 7 s. (A first run of (3) hung for 19 min in the test script, which waited for a world that the tool had correctly refused to load for lack of memory.)
<!-- SECTION:NOTES:END -->
