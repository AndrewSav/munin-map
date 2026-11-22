---
id: TASK-4.2
title: 'Standalone ore-export CLI: fast and background generation from a seed'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-22T06:02:37Z'
updated_date: '2026-11-22T19:36:03Z'
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
TASK-4.1 showed that a client-free generator can produce a whole world's ore deposits in minutes, but only as a spike: a BepInEx plugin driven by PowerShell scripts in the test server's Docker image. To judge TASK-4's methods (criterion #8: the copy method against re-running placement in Munin), the generator has to work where admins would run it, without that setup. This task makes it a standalone tool and measures it there. Hooking it into Munin is a later step and not part of this task.

Two modes from one tool. Fast: on a machine nobody else needs, as many processes as the CPU and memory allow, minutes for a whole world. Slow: one low-priority process next to a running game server, where hours are acceptable. The natural home of the slow mode is the game server's own container, where a dedicated server is already installed.

Platforms: game clients are almost always on Windows and dedicated servers often in Linux containers, so both matter. Docker is not required: steamcmd and the dedicated server for the current platform are enough.

Why the input is a copy of the world, not just its seed (game code, 1.0.21): `WorldGenerator` reads only the seed (`World.m_seed`, the stable hash of the seed name) and `World.m_worldGenVersion` (2 for every world created since that field was added; 0 and 1 change mountain distance, Mistlands noise and swamp distance). Alt-biome sectors are derived from the seed too; `m_uid` and the world name play no part. But the location plan lives in the save: every planned location instance with its position and whether it is placed (its zone generated). On load, if the save's location version differs from the game's (`ZoneSystem.m_locationVersion`: 1 in the code, but the scene sets it: 32 in 1.0.21), the unplaced instances are dropped and planned again around the placed ones (`ClearNonPlacedLocations`, `GenerateLocations`); planning is seeded per location type from the world seed. So a copy of the save, loaded by the same game version, gets exactly the plan of the world's own server, while a fresh world from the seed plans everything with nothing placed and can differ on a world that has been re-planned. The seed-only route is therefore exact only for worlds never re-planned, and is kept as an option for a world the admin knows is fresh (just created, or never through a location-version change), where a seed and version are simpler than copying files. The tool must run the same game version (and world-generation mods) as the world's server.

Ref: TASK-4.1 (patches-tools/zonegen: ZoneGen.cs, run.ps1, bench.ps1), doc-2 'Measured'.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 One tool with the same options runs on Windows x64 and Linux x64, from a release bundle, without Docker or build tools
- [x] #2 Dependencies are downloaded on first use, not shipped in the bundle: steamcmd and the dedicated server for the current platform (anonymous login), and BepInEx at a pinned version whose checksum is verified; an option uses an existing server install instead (as in a Valheim server container)
- [x] #3 Output is one file of deposits (prefab, position, zone) for a configurable prefab list whose default is the ore deposits, headed by the input (world copy or seed), seed, world-generation version, game version, tool version and zone count; the output of all processes is joined into it
- [x] #4 Fast mode defaults to a process count that uses 80% of the logical CPU threads (divided by the threads one process uses, as measured), lowered so the processes fit in available memory; a command-line option sets the count
- [x] #5 Peak memory per process is measured across at least three seeds and several process counts, and the memory limit in fast mode is based on that measurement
- [x] #6 Slow mode runs one process at the lowest CPU priority, and can be held to a single core
- [x] #7 The tool works only on its own copy in its own folder: it never writes the world files it was given or any other save, and its server process does not take ports or a server listing that clash with a game server running on the same host or in the same container
- [x] #8 The tool, not a server next to it, is what runs out of memory first: on Linux its server process sets its own OOM score to the maximum (oom_score_adj 1000, no privileges needed), so the kernel picks it over any other process in the same container or host; on Windows it runs under a job object with a memory limit
- [x] #9 A memory guard reads the memory actually available to it (the container cgroup limit and use where there is one, otherwise the system), refuses to start below a margin, and stops its processes cleanly if headroom falls under the margin during a run
- [x] #10 Accuracy, against the TASK-27 snapshot over the zones live had not explored: a run on a copy of the live world and a seed-only run are both compared, and their deposit match rates, and the location match rate of the seed-only run, are recorded
- [x] #11 Tested where nothing was installed beforehand: full fast-mode extract, with total time (including first-time downloads), process count and peak memory recorded
- [x] #12 Before any test on a live server, a Linux test outside it: the tool runs in a memory-capped container next to a process standing in for a game server, and it is shown that the memory guard stops the tool and that, with the guard off, the kernel kills the tool and not the stand-in
- [x] #13 Tested inside a running Valheim server container next to its game server, after the test above and in an agreed maintenance window: the container memory cap and the server use of it, and free cores and memory, are checked first; then a full slow-mode run (one process, lowest priority, one core) is made, and its run time and the game server fps and longest frame, against a settled baseline, are recorded
- [x] #14 A README covers both modes, platforms, downloads and disk use, and warns that fast mode uses most of the CPU and memory
- [x] #15 Results are recorded in TASK-4 notes as input to criterion #8; the tool lives in patches-tools and nothing goes into the product
- [x] #16 The README says the tool must run the same game version and world-generation mods as the world server, and that the seed-only input is exact only for worlds whose location plan was never redone by a location-version change
- [x] #17 Each process writes its deposits as it goes: a zone is written once it and its neighbours are generated, so a stopped or killed run keeps everything written so far
- [x] #18 zonegen run --resume continues a stopped run: zones already written are kept and skipped, the rest are generated, and the output is the same as an uninterrupted run apart from generation-order differences near the skipped zones
- [x] #19 A process drops the objects of zones that are written and have no ungenerated neighbour left; its effect on memory and accuracy is measured against keeping them and recorded
- [x] #20 The main input is a copy of a world save: the world folder (_main.N.fwl2, .db2, .chunks, .ok and .chunk files, one save number); the tool generates the zones that world has not generated yet and keeps its own locations. A world in the older .fwl/.db format is refused with how to convert it (the game only ever saves in the folder format and converts an older world when it loads it). A seed name plus world-generation version (default 2) is accepted instead for a world known to be fresh. A utility command prints, from a save, the seed, world-generation version and location version, next to the location version of the installed game, and warns when they differ (the server re-plans unplaced locations on its next start)
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Layout: patches-tools/zonegen/plugin (the TASK-4.1 BepInEx plugin, evolved) and patches-tools/zonegen/cli (a .NET 8 console app, self-contained single-file for win-x64 and linux-x64, invariant globalization, the plugin DLL embedded as a resource). Spike scripts run.ps1/bench.ps1 stay for the local test image.
2. Plugin: still driven by ZONEGEN_* variables, defaults = the measured best (back to back, 30 ms budget, window 8, uncapped, terrain ahead, no saves, no dungeon interiors). New: ZONEGEN_PREFABS (list, default the five ores); vegetation cut after the last listed entry (none if a prefab is not vegetation); ZONEGEN_SEED + ZONEGEN_GENVER (Harmony prefix on World.GetCreateWorld creates the world from that seed when there is no save); probe mode (postfix on ZoneSystem.Awake logs m_locationVersion and quits); logs the game build, location versions and whether locations were re-planned on load; output CSV per process.
3. CLI commands: `run` (--save PATH | --seed NAME [--gen-version 2]; --out; --processes N | --slow [--cores N]; --prefabs; --server-dir; --work-dir; --keep), `info --save PATH` (seed, gen version, location version from .fwl2/.db2; the game location version via a probe, cached per server build), `setup` (downloads only).
4. Downloads into the work dir (LocalAppData / XDG data): steamcmd for the platform, app 896660 anonymous, BepInExPack_Valheim 5.4.2360 from Thunderstore, sha256 6bca77b1016acfe0ffaebb6534c84b12ae8d12e5a96abbf59ec6e4526204fc6f. Linux preflight: report missing system libraries with the install line.
5. Per process: its own BepInEx root (copy of the pack core + our plugin only, so no other plugins such as Munin load), own -savedir with its own copy of the world, own free UDP port pair, -public 0, random password. Launch: Linux env DOORSTOP_ENABLED/DOORSTOP_TARGET_ASSEMBLY/LD_PRELOAD (nothing written to the server dir, so an existing install such as a container's is used read-only); Windows winhttp.dll in the managed install + --doorstop-target-assembly (an existing Windows install without the doorstop dll is refused rather than changed).
6. Save copy: newest save number with .ok; copy its _main files and chunk files; recheck the .ok and retry if it vanished.
7. Resources: default processes = floor(0.8 x logical CPUs / threads per process, measured), capped by available memory / measured peak; memory guard (Linux cgroup v2/v1 limit and use, else MemAvailable; Windows GlobalMemoryStatusEx) refuses below margin and stops children if headroom falls under it; Linux oom_score_adj 1000 on each child; Windows job object with a per-process memory limit; slow mode = Idle priority (nice 19) and affinity to N cores.
8. Progress from child logs every 30 s, failures reported with the last log lines; outputs joined into one CSV with a # header (input, seed, gen version, location versions, game build, tool version, zones, processes, time).
9. Tests in order: Windows, Linux in a plain Debian container (clean-system check), accuracy against the TASK-27 snapshot (copy and seed-only), memory profile (3 seeds, several process counts) and threads per process, memory-capped container test with a stand-in, a run where nothing was installed beforehand, README; the run inside a live server container last, in a maintenance window, after checking its memory cap.

Added during the work (user requests): 10. Incremental output per zone once it and its neighbours are done, --resume from the run folder's own input copy, object dropping (measured, small effect). 11. Reuse of a server and BepInEx already on the machine (Steam install, /opt/valheim in a server container); --update opt-in for the tool's own install only; --own-server. 12. Failing loudly on an unsupported game version (wrapped patching and first frame, 10-minute report timeout); servers quit when the tool is gone (Linux). 13. Threads per process and memory per process measured, replacing the guesses. 14. Slow mode uses the fast frame settings (found in the live test). 15. build.sh packs the release bundles. 16. The older .fwl/.db save format is refused with how to convert it.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Built: the plugin moved to patches-tools/zonegen/plugin (seed-created worlds, prefab list, vegcut auto, probe mode, re-plan detection, @info/@progress/@done/@error lines in its own log, per-zone output, object dropping, a skip list for resume, quitting when its tool is gone); the CLI in patches-tools/zonegen/cli (.NET 8, one self-contained file per platform, the plugin embedded); compare.py and compare_locations.py check output against a generated snapshot; build.sh packs the bundles (a zip and a tar.gz with the README).

Windows and Linux (a clean Debian container, a normal user, only the libraries the README lists, the save mounted read-only): setup downloads steamcmd, the server and BepInExPack 5.4.2360 (checksum verified); whole-world runs from the copy finish with no failed zones and match the TASK-27 snapshot 99.1-99.3% (copper and tin 99.8-100%, scrap 99.7-99.9%, silver 97-98%, obsidian 98-98.3%); Linux and Windows outputs agree 99.4% (generation order near zone edges differs between parallel runs). A run where nothing was installed beforehand worked the same; steamcmd's first attempt exited after updating itself and the tool's retry installed the server.

Seed-only input (criterion #10): deposits match the snapshot as well as a copy run (99.3%). Location plans (--locations, compare_locations.py reading the plan from the save's .db2): the copy's plan as loaded matches the save 100% (checking the reader); the seed-only plan matches 99.93%, the only difference being Haldor (Vendor_BlackForest): all his candidate sites in a fresh world against the one placed site in the save. So that world had never been re-planned, and a seed-only run reproduces it apart from unique sites already settled.

Memory and CPU (criterion #5, three seeds, several process counts): peak per process 2.6-2.9 GB on Windows and about 3.3 GB on Linux, about the same whatever the process count. It is mostly the game assets for the biomes and location types a process has met, which stay referenced: one process on a whole-world share peaks like each process of a full run; Resources.UnloadUnusedAssets every 2,000 zones changes nothing; dropping objects saves about 0.1 GB, with no change in accuracy. So the memory check keeps 3.5 GB per process (the first idea, a base plus a share of the objects, was wrong). A process keeps about 2 CPU threads busy on its own and about 1.6 when the CPU is shared out; with 1.6 per process the default lands near 80% busy, as asked. Most of the speed comes with fewer processes (memory bandwidth, presumably).

Safety (criteria #8, #9, #12), Linux, a memory-capped container (cgroup v1 there; the tool read the container's limit, not the system's) next to a larger stand-in process holding filled memory: (1) guard on: stopped by the guard when headroom fell under the margin, what was written kept, the resume command printed, the stand-in alive; (2) --no-guard: the generator ran normally until the container ran out, then was the process killed while the larger stand-in survived (oom_score_adj 1000; the kernel log isn't visible in the container, so the OOM kill is inferred from the generator dying without a result at the limit), with most of its zones already written; (3) zonegen killed with SIGKILL: its server logged "the zonegen tool is gone" 3 s later and quit. On Windows, killing the tool ended its servers through the job object.

Incremental output and resume (criteria #17, #18): a zone is written (rows flushed, then its line in <tag>-zones.txt) once it and its eight neighbours are done in that process; --resume folds the written zones into prev- files (rows of a zone missing from the zones file are dropped), copies the world again from the run folder's own copy and skips those zones. A run killed at about half way and resumed matches an uninterrupted run 99.8%, and both match the snapshot 99.5%.

Object dropping (criterion #19): a written zone's objects are released (ZDOMan sector list and m_objectsByID, ZDOPool.Release: what HandleDestroyedZDO does without the network part). Effect measured: accuracy unchanged, about 0.1 GB less memory (managed heap about 120 MB smaller); it stays on by default.

Installs: by default the tool uses a server already on the machine (Steam's Valheim dedicated server, found through the registry and libraryfolders.vdf, or /opt/valheim/bepinex or /opt/valheim/server in a server container, or the usual Linux Steam paths) and that server's BepInEx if it has one; else its own install, reused as it is. --update (opt-in, on run and setup) updates only its own install; --own-server forces it. On Windows a found server without BepInEx's winhttp.dll is skipped with a message. The probe's result is cached per server build, or by the server executable when the install has no Steam manifest.

Game updates: the plugin binds to game members by name when it runs, so it survives patches that don't change what it uses; one that does fails loudly: Harmony patching and the first-frame code are wrapped and write an @error line, and the tool stops a process that hasn't reported in 10 minutes.

Live test (criterion #13), in an agreed maintenance window with nobody connected, inside the running Valheim server container: the tool used the image's server merged with BepInEx (/opt/valheim/bepinex) and its BepInEx as they were, downloaded nothing, ran as the container's server user, detached (docker exec -d), in slow mode on a copy of the live save. The first attempt ran far slower than expected: slow mode kept the server's frame cap and a 10 ms budget, so the generator idled on its own core. Fixed (slow mode now uses the fast frame settings; its priority and core limit hold it back), the tool stopped mid-run (its generator quit by itself) and the run resumed. The whole world took about 1.5 hours, no failed zones, 99.3% against the snapshot, peak about 3.3 GB. The game server held 30 fps throughout; its longest frame per sweep had a median of 4.3 ms during the run, 4.4 ms over the 100 sweeps before and 4.3 ms after, with a lower maximum during the run than before: no measurable effect. Nothing was left running, and the tool and its files were removed from the container afterwards.

Fixed along the way: a folder path with a trailing slash gave an empty world name and the server silently created a random world (now the tool stops if a process loads a world whose seed or generated-zone count differs from the save); on bare Linux .NET aborted on missing libssl (the start-up check now lists libssl, CA certificates, 32-bit libgcc for steamcmd and libatomic, with the install lines); a resume applied --limit after the skip (fixed: limit, then skip); on Windows, slow mode's uncapped main thread at idle priority on one core starved Unity's heightmap thread (Windows schedules strictly by thread priority) and zones failed, so a frame whose zones are all still waiting now sleeps 2 ms (after: no failed zones, fast mode unchanged on both platforms).

Older .fwl/.db input dropped (with the user): the 1.0.21 server has one save path (ZNet.SaveWorldThread), which writes the folder format, and only reads the older pair (LoadOldWorld), so any world the current server has loaded is in the folder format after its first save; the tool now refuses a .fwl/.db, or a folder holding one, with that advice.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
zonegen (patches-tools/zonegen) turns the TASK-4.1 spike into a standalone tool that predicts a world's ore deposits by generating a copy of it headless. One self-contained file per platform (Windows x64, Linux x64; build.sh packs them with the README) with the ZoneGen BepInEx plugin built in. Input: a copy of the world save (or a seed for a world known to be fresh); it reuses a dedicated server and BepInEx already on the machine (Steam install, /opt/valheim in a server container) or downloads steamcmd, the server and a pinned, checksummed BepInExPack once; --update is opt-in. Output: one CSV of deposits (or any prefab list) with a header of seed, versions and counts.

Modes: fast (processes for 80% of the CPU threads, capped by free memory) and slow (one process at the lowest priority on one core, for next to a game server). Each process writes zones as it goes; --resume continues a stopped run. Safety: a container-aware memory guard, oom_score_adj 1000 on Linux and a job object on Windows so the generator gives way first, servers quit when the tool dies, the given save is only read, own ports and BepInEx folder per process.

Verified on Windows and Linux, from nothing installed, and inside a running Valheim server container in slow mode with no measurable effect on the game server's frame times. Accuracy 99.1-99.3% against the TASK-27 snapshot in every run, seed-only included (its location plan matches the save 99.93%). Memory about 3 GB per process at the peak, mostly game assets, whatever the process count. Memory-guard, OOM-preference, kill and resume tests passed in a memory-capped container.

Fixed on the way: an empty world name made the server create a random world (now checked); slow mode idled under the frame cap and starved the terrain thread on Windows; missing libssl on bare Linux is now reported with the install line.

Not done here: Munin reading the file (TASK-4), and the TASK-4 method decision (criterion #8), which the user keeps open (method 1 is still a candidate).
<!-- SECTION:FINAL_SUMMARY:END -->
