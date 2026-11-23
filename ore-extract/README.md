# ore-extract

Predicts where a Valheim world's ore deposits are, including in zones nobody has visited yet. It
generates a copy of the world on a headless dedicated server (no game client, no players), lets
the game place everything with its own rules, and writes the ore deposits it placed to a CSV file.
It comes with each release of this fork of Munin Map, separately from the plugin.

## Get it

From the fork's release page, next to `MuninMap.zip`:
- Windows: `ore-extract-win-x64.zip`, holding `ore-extract.exe`;
- Linux: `ore-extract-linux-x64.tar.gz`, holding `ore-extract`.

Each is one self-contained file (no .NET, Docker or install needed) plus this README.
`SHA256SUMS` on the release lists their checksums. `releases/latest/download/<file name>` always
gives the newest. `ore-extract version` prints the release it belongs to (for example
`3.4.0+patches.5`).

## Use

```
ore-extract run --save <world folder>        # fast: uses most of the machine
ore-extract run --save <world folder> --slow # one low-priority process, next to a game server
ore-extract run --seed <seed name>           # a world known to be fresh, without its save
ore-extract run --resume <run folder>        # continue a stopped run
ore-extract info --save <world folder>       # seed and versions of a save
ore-extract setup                            # only download and install
ore-extract help
```

The result is `<world>-deposits.csv` in the current folder (or `--out FILE`): a `#` header
(input, seed, versions, game build, zones, processes, time), then `prefab,x,y,z,zx,zy` per
deposit. Only zones the save had not generated yet are in it: the save already holds the real
deposits of the others.

### Stopping and resuming

Each process writes a zone's deposits as soon as the zone and its neighbours are generated, so a
run that is stopped (the memory guard, Ctrl+C, a crash, a reboot) keeps what it had. The tool prints
the run folder; `ore-extract run --resume <run folder>` continues with the same options, from the run's
own copy of the save, skipping the zones already written. Deposits next to the skipped zones can
differ slightly from an uninterrupted run (99.8% the same in a test), the same kind of difference
as between two parallel runs.

### Run by another program

Munin doesn't need the tool for `predict_ore = server`: it runs the same code (`core/`) in its
own process. For another program that runs the tool, three options help: `--run-dir DIR` puts the run in a fixed folder (so it can be
found and resumed after a restart), `--status FILE` keeps a small `key=value` file saying how the
run is going (state, zones done and in all, zones per minute, seconds left, the reason for a
failure), and `--parent-pid PID` stops the run (it can be resumed) when that process is gone. The
output file is written beside its name and moved into place, so a watcher never reads half of it.

### The input

- **A copy of the world save (recommended).** Give the world's folder, `worlds_local/<world>/`
  with `_main.N.fwl2`, `.db2`, `.chunks`, `.ok` and the `.chunk` files. The tool copies the newest
  complete save (the one with an `.ok` file) and checks it wasn't replaced during the copy, so it
  can be pointed at the save folder of a running server. The files given are only read. A world
  still in the older `.fwl`/`.db` format isn't taken: the game only ever saves in the folder format
  and converts an older world when it loads it, so start the current server on it once.
- **A seed** (`--seed NAME`, optionally `--gen-version N`, default 2) for a world you know is
  fresh. It is exact only if the world's location plan was never redone: a game update that raises
  the game's location version makes a server plan the world's unplaced locations again, around the
  placed ones, the next time it loads it, and a world built from the seed alone can't know where
  those are. The save doesn't record whether that ever happened; with the save, use the save.

**Run it with the same game version and world-generation mods as the world's server.** The game's
rules decide the result, so a different version or a mod that changes generation (Expand World,
Better Continents and the like) gives different deposits. `ore-extract info` shows the save's location
version next to the installed game's; when they differ, the server re-plans unplaced locations on
its next start, and a copy run here does the same.

What a copy can't predict exactly: the facing of randomly rotated locations and which candidate
site the unique traders get are not fixed by the seed, so ore close to those can differ (about 1%
of deposits in the measurements against a fully generated world).

### Fast and slow

- **Fast (default)** starts as many server processes as fit in 80% of the CPU threads and in free
  memory (1 GB kept free), each generating a share of the zones. It uses most of the machine's CPU
  and memory for the run: don't run it on a machine that is doing something that matters.
  `--processes N` sets the count. A process keeps about 2 CPU threads busy, 1.6 when the CPU is
  shared out, so the default is about one process per two threads. Most of the speed comes with
  fewer: 8 processes were only about 10% faster than 5.
- **Memory per process:** about 2.6-3.4 GB at its peak for a whole world, whatever the number of
  processes: most of it is the game's assets for the biomes and locations a process has met,
  which every process meets. It starts near 1.6 GB and climbs most in the outer biomes, near the
  end of the run. So the memory, not the CPU, often sets how many processes fit.
- **Slow (`--slow`)** runs one process at the lowest CPU priority, held to one core (`--cores N`
  for more). It is meant to run beside a game server for hours without getting in its way.

### Memory protection

The tool gives way before anything else does when memory runs short:
- it checks the memory actually available to it before starting (in a container with a memory
  limit, the container's limit and use, not the host's free memory) and refuses to start without
  room for its processes plus the margin;
- during the run it stops its processes if free memory falls under the margin (`--margin GB`,
  default 1);
- on Linux each server process it starts marks itself as the first one to kill if memory runs out
  anyway (`oom_score_adj` 1000, no privileges needed); on Windows each runs under a memory cap.

### Platforms and what it downloads

Windows x64 and Linux x64, one self-contained file each (no .NET or Docker needed). On first use
it downloads into its work folder (`%LOCALAPPDATA%\ore-extract` or `~/.local/share/ore-extract`, or
`--work-dir`):
- steamcmd, and through it the Valheim dedicated server (Steam app 896660, anonymous login,
  about 1.5 GB);
- BepInExPack_Valheim 5.4.2360 from Thunderstore, checked against a fixed SHA-256.

**Installs already on the machine are reused.** By default the tool uses a dedicated server it
finds (Steam's "Valheim dedicated server", or `/opt/valheim` inside a Valheim server container),
and that server's own BepInEx if it has one; it downloads nothing it finds there. Otherwise it uses
its own install, downloaded once and then reused as it is. `--server-dir DIR` names an install,
`--own-server` always uses the tool's own. Nothing is updated unless asked: `--update` (on `run` or
`setup`) updates the tool's own server with steamcmd; an install found elsewhere is never changed,
so update it the way it is normally updated.

A server install is only read: each process gets its own BepInEx folder holding only this tool's
plugin, its own save folder and its own ports, so a game server running from the same install or
in the same container is not touched. (On Windows an existing install must already have BepInEx's
`winhttp.dll`; one without it is skipped.)

**After a game update.** The plugin uses the game's code by name, so it keeps working across game
updates that don't change what it uses. If one does, the run stops with an error saying so (or,
if the plugin never starts, after 10 minutes) and the tool needs rebuilding against the new game.

Linux needs a few system libraries; the tool checks for them and prints the install line. On
Debian/Ubuntu: `libssl3 ca-certificates libatomic1`, plus `lib32gcc-s1` for steamcmd.

Disk: the server install plus, per process, a copy of the save and its BepInEx folder; the run's
working files are deleted at the end unless `--keep`.

## Build

```
./build.sh     # dist/ore-extract-win-x64.zip and dist/ore-extract-linux-x64.tar.gz
```

Each bundle holds the one executable and this README. `OREEXTRACT_VERSION` sets the version it
reports (the release workflow passes the release stamp); without it, `0.0.0-local`.

Needs the .NET 8 SDK and the game's and BepInEx's assemblies in `../libs` (Munin's `build.sh` puts
them there), and zip or PowerShell for the Windows bundle. The code:
- `plugin/`: the OreExtract BepInEx plugin that drives the generation inside the server; built
  first and embedded in `core`.
- `core/`: the part that copies the save, starts the servers, watches them, guards memory and
  joins their output; built for .NET 8 (the tool) and .NET Framework 4.8 (Munin, which merges it
  into `MuninMap.dll` and runs it in-process for `predict_ore = server`).
- `cli/`: the `ore-extract` command-line tool: options, and finding or downloading the server and
  BepInEx.

The accuracy checks (`compare.py`, `compare_locations.py`) and the spike's test-image scripts are
in `patches-tools/ore-extract/`.
