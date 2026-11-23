---
id: TASK-4.5
title: >-
  Server extract: Munin makes the prediction on the server (predict_ore =
  server)
status: Done
assignee:
  - '@claude'
created_date: '2026-11-22T22:22:25Z'
updated_date: '2026-11-23T15:17:41Z'
labels:
  - plugin
  - web
  - docs
dependencies:
  - TASK-4.4
  - TASK-4.3
parent_task_id: TASK-4
type: feature
ordinal: 34000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
TASK-4's `predict_ore = server`: Munin makes the extract itself, on the server, so the admin needs no second machine and no manual steps. TASK-4.2 showed this is practical: one process at the lowest priority on one core generated a whole world in about an hour and a half inside a live server's container, with no measurable effect on the game server, at about 1.5 GB of memory rising to about 3.5 GB in the outer biomes. It runs for a long time, so players need to see how far along it is, and a server restart must not throw the work away.

Ref: TASK-4 (approach in its notes), TASK-4.2 (slow mode, resume, memory guard, oom_score_adj, the generator quitting when its tool is gone), TASK-4.4 (the file Munin reads), TASK-4.3 (ore-extract shipped).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 With `predict_ore = server` and `reveal_all = true`, Munin starts the extract by itself when there is no current prediction for the world, on a copy of the save, with no admin action beyond the setting; the live world is never written
- [x] #2 The extract runs as one process at the lowest CPU priority on one core, with the memory guard and the out-of-memory preference of TASK-4.2, using the server's own install
- [x] #3 Progress (zones done, percent, time left) and the result (done, failed with the reason) are shown to everyone in the page's server details
- [x] #4 A server restart doesn't lose the work: after it, Munin resumes the extract where it stopped
- [x] #5 When the extract finishes, its deposits are shown as TASK-4.4 shows a file extract, without a restart
- [x] #6 After a game update that changes the game version, Munin makes the prediction again by itself
- [x] #7 The extract never outlives the server: it stops when the server stops or Munin is unloaded
- [x] #8 The game server's fps and longest frames during an extract, against a settled baseline, are measured on the test server and show no noticeable effect
- [x] #9 docs/ADVANCED.md describes the server extract (what it does, its time and memory, where to see progress) and adds it to the comparison of the methods
- [x] #10 PATCHES.md's single TASK-4 entry, which presents predicted ore as one feature with its modes, covers the server mode: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. ore-extract (fork-owned) gains what a supervisor needs, with no behaviour change for people running it by hand:
   - `--parent-pid PID`: the tool stops its servers and quits when that process is gone (the server crashed or was killed, so the extract can't outlive it, #7);
   - `--status FILE`: a key=value file rewritten on each progress line and at the end (state loading|running|done|failed, zones done/total, rate, time left, the reason for a failure, the run folder), so Munin reads it instead of parsing console text;
   - `--run-dir DIR`: a fixed run folder (Munin's: <world data>/ore-extract-run), so a restart finds it and passes `--resume DIR`;
   - the output CSV is written to a temporary name and moved into place, so Munin never reads half a file.
2. Shipping (with the user: bundled, not downloaded): the release workflow adds both platforms' ore-extract to MuninMap.zip under plugins/MuninMap/ore-extract/ (Linux mode 755) after it builds ore-extract; build.sh (upstream) is not touched. Munin looks for ore-extract(.exe) next to its DLL or in ore-extract/ beside it, and on Linux makes it executable if unzipping lost the bit. The local test server's deploy.ps1 copies the Linux one too.
3. Munin, new src/Patches/ServerExtract.cs; PredictedOre gains the value `server` (setting text, AcceptableValueList) and reads the same predicted-ore.csv as file mode, so a finished extract shows without a restart (#5). With predict_ore = server and reveal_all on, once the world has loaded:
   - no current prediction (no file, another seed, or another game version than the server's: #1, #6) and no run folder: start `ore-extract run --save <the world's save folder> --slow --server-dir <this server's install> --run-dir ... --status ... --parent-pid <server pid> --out <world data>/predicted-ore.csv`; ore-extract copies the last complete save itself and loads only its own plugin, so the live world is never written (#1); --slow is one lowest-priority process on one core, with its memory guard and oom_score_adj / job object (#2);
   - a run folder left by an earlier start: resume it (#4);
   - a failure (memory guard, not enough memory to start, an error) is shown with its reason and not retried until the next server start, which resumes it;
   - stopping: Munin kills ore-extract when it is unloaded or the game quits; --parent-pid covers a crash (#7).
   The process is started and watched off the game thread (the status file is polled every few seconds); its console output goes to a log file in the run folder, and a few lines go to the server log (start, finish, failure).
4. Page: the Predicted ore line in Server details shows the extract's state to everyone: waiting for the world, running (zones done/total, percent, time left), done, or failed with the reason (#3). The state goes out in data/predicted-ore.json (reveal_all is on in this mode) and is polled by the page while a run is going.
5. Docs: ADVANCED.md settings row, a server extract part in Predicted ore (what it does, about 1.5 h and 1.5-3.5 GB, the copy of the save, where progress shows, what a failure looks like) and a row in the methods comparison (#9); PATCHES.md's TASK-4 entry gains the server mode (#10).
6. Tests on the test server: a whole extract from the start with the server running; a restart halfway through (it resumes); a killed server (ore-extract stops); predict_ore = server with an extract for another game version (it runs again); progress and the result in Server details; fps and longest frames during the run against a settled baseline with perf_report (#8).

Revised step 2 (with the user, 2026-11-23): ore-extract travels inside MuninMap.dll, not as a folder in MuninMap.zip, so predict_ore = server works however the plugin is deployed (Windows or Linux, container or host; some installs copy only *.dll). ore-extract/build.sh gzips both executables into dist/embed/; a fork-owned src/Directory.Build.targets embeds them (no edit to upstream's csproj); the release workflow builds the plugin again after ore-extract. Munin unpacks the one for its platform into <data>/ore-extract/bin/<hash>/ on first use (chmod +x on Linux) and removes older ones; one placed next to the DLL is the fallback for builds without it. The test image's folder-copy change is reverted.

Revised again (with the user, 2026-11-23): no executable in Munin. ore-extract's supervisor moves into a shared library, ore-extract/core (net48 and net8.0; it embeds ore-extract's plugin), used by the CLI and merged into MuninMap.dll by ILRepack through src/Directory.Build.targets (a ProjectReference plus one ILRepack input; upstream's project files unchanged). Munin runs Runner.Run on a background thread (Runner.Hosted: no working-directory change, no Ctrl+C or process-exit hooks) and reads the run's state in memory; Runner.Stop kills the generation server at once. Linux priority and affinity go through libc (setpriority, sched_setaffinity), Windows through kernel32, not Process properties. The plugin's parent check takes the parent's name from OREEXTRACT_PARENT_COMM, so it works under the game server too. The release workflow is back to its TASK-4.3 form, since Munin's own build builds core and the plugin.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-11-23, first slice on the test server (copy of live, 3056 zones generated, 81,570 to go):
- ore-extract gained --status, --parent-pid and --run-dir, and writes its output to a temporary name before moving it into place. MuninMap.zip carries both platforms' tools in plugins/MuninMap/ore-extract/ (release workflow). The test image's start script now copies plugin folders too, not only *.dll.
- Found while testing: the live server's doorstop leaves DOORSTOP_INITIALIZED=TRUE in its environment, ore-extract inherited it, and doorstop then skipped ore-extract's servers, so BepInEx never loaded in them (TASK-4.2 ran from a clean docker exec shell, which hid it). Both sides now start clean: Munin drops DOORSTOP_* and LD_PRELOAD, and ore-extract clears DOORSTOP_* before setting its own.
- Started by itself with predict_ore = server and no predicted-ore.csv: probe 17 s, then one process at nice 19 on one core with oom_score_adj 1000. Progress reaches data/ore-extract.json.
- Restart (docker restart) mid-run: Munin stopped ore-extract on quit, nothing outlived the server, and the next start resumed it with 2006 zones kept.
- --parent-pid: a separate ore-extract under a dummy parent stopped itself and its server about 4 s after the parent was killed (status stopped, with the reason).
- Settled baseline for #8 (test server up 3 h, nobody connected, no extract): 30 sweeps, 30 fps, longest frame 4.5 ms on average, 6.5 ms at most.

Embedded tool tested on the test server with only MuninMap.dll in plugins (19.7 MB, was 6.7 MB): Munin unpacked ore-extract to munin-data/ore-extract/bin/a914a1309033/, made it executable and resumed the run. Not yet tried on a Windows host.

Whole-world run finished (executable version): 81,570 zones in 24 min of generation across the resume, 0 failed, 17,787 deposits, peak 3.63 GB, 1.0 thread busy. Munin read the new file 4 s later and showed it with no restart (#5). Frame times during the run: 10 sweeps, 30 fps, longest frame 4.2 ms on average, 4.3 ms at most, against the settled baseline of 4.5 / 6.5 ms (#8, to measure again after the change below).
Change of approach (with the user): ore-extract's supervisor (copying the save, setting up and starting the generation server, watching progress and memory, joining the output) moves into code shared by the CLI and Munin, and Munin runs it in-process on a background thread. Only the game's own dedicated server is started as a separate process, with ore-extract's small plugin DLL, which is platform-independent. No platform executables in MuninMap.dll.

In-process version on the test server: MuninMap.dll 6.8 MB (about +80 KB; no reference to OreExtract.Core is left after the merge, and it carries the plugin as a resource). Started by itself; the generation server is a child of the game server at nice 19, Cpus_allowed_list 15, oom_score_adj 1000, OREEXTRACT_PARENT_COMM=valheim_server., doorstop variables cleared. docker restart mid-run: stopped on quit, then resumed with 1886 zones kept.

In-process whole-world run (after a restart and resume): 81,570 zones in 31 min, 0 failed, 17,787 deposits, peak 3.77 GB. Shown 4 s after it finished, with no restart (#5). Against the executable version's run, 17,729 rows are the same and 59 differ each way (99.7%), the expected effect of different resume points.
Frame times during the run (13 sweeps, 07:48-08:13): 30 fps throughout, longest frame 5.3 ms on average, 4.1-5.2 ms in 11 sweeps, 7.4 and 13.0 ms in two (both in Munin's markers job, with no garbage collection in a sweep frame), and 3 collections in all. The earlier baseline came from another window, so a fresh 13-sweep baseline with no extract follows (#8).
Review fixes: the CLI's relative --work-dir and --server-dir resolve from where the run was started (resume); deleting an old run folder moved off the game's thread; the failure reason shown to everyone loses ore-extract's resume hint and the server's paths; child CPU time read from /proc on Linux (Mono reports 0); the dead "unavailable" page state removed; the setting text says half an hour to an hour and a half.

Comparison run unattended with the PC idle (2026-11-23/24; each window skips the first 6 min; same build apart from the interval):
- watch every 1 s: 13 sweeps, 30 fps throughout, longest frame 4.3 ms on average, 6.9 ms at most (one sweep over 6.6), 2 garbage collections; whole run 31 min 4 s.
- watch every 5 s: 13 sweeps, 30 fps throughout, longest frame 4.3 ms on average, 6.7 ms at most (one sweep over 6.6), 2 garbage collections; whole run 31 min 0 s.
- baseline, no extract: 15 sweeps, 30 fps throughout, longest frame 4.2 ms on average, 4.6 ms at most, 2 garbage collections.
The interval makes no measurable difference. During an extract one sweep in about 13 has a longest frame near 7 ms, in Munin's walk, against a 33 ms frame at 30 fps; fps never drops. The 13 ms markers frame of the earlier run did not recur. Munin keeps 5 s (less work in the game server's process; the wait now wakes at once on a stop request, so stopping is not slower).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
predict_ore = server: with reveal_all on and no current prediction (no predicted-ore.csv, or one for another world or game version), Munin makes the extract itself. ore-extract's supervisor moved into a shared library, ore-extract/core (net48 and net8.0, carrying ore-extract's game-server plugin). The CLI uses it, and Munin merges it into MuninMap.dll (about +80 KB, through a fork-owned src/Directory.Build.targets; upstream's project files unchanged) and runs it on a background thread. It copies the world's last complete save and starts one headless game server from the server's own install, with only ore-extract's plugin, at the lowest priority on one CPU, with the memory guard and the out-of-memory preference. The result is the predicted-ore.csv that file mode reads, so it shows without a restart. A restart resumes the run. The generation server stops with the game server: Munin stops it on quit or unload, and it quits by itself if its parent dies. Server details show name/value rows: server version, predicted deposits, prediction version when it differs, prediction update % (zones as a tooltip) and ETA, and a short note for a version mismatch or a failure (server paths stripped). ore-extract gained --status, --parent-pid and --run-dir and atomic output. Fixed on the way: doorstop's DOORSTOP_INITIALIZED, inherited from a game server, stopped BepInEx loading in the generation servers.
Verified on the test server (Linux container, the game's Mono): automatic start, nice 19 / one CPU / oom_score_adj 1000; restart and resume; deposits shown 4 s after finishing; rerun after a game-version mismatch; a killed parent stops the generation server within a second; the rows checked in the browser. The Windows code paths were verified through the CLI on Windows (Idle priority, one CPU, parent death, paths with spaces). Frame times with the PC idle: 30 fps throughout with or without an extract; during an extract about one sweep in 13 has a longest frame near 7 ms (baseline at most 4.6), and polling every 1 s or 5 s makes no difference (Munin uses 5 s). Whole world in about 31 min on the test machine, 17,787 deposits, peak 3.8 GB.
Not tested: Munin itself on a Windows host (no Windows dedicated server with Munin available); unloading Munin without a server stop (same stop code as quitting).
<!-- SECTION:FINAL_SUMMARY:END -->
