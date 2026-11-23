// ore-extract: generate the ungenerated zones of a copy of a Valheim world on a headless dedicated
// server, and write out the ore deposits (or other prefabs) that generation placed. TASK-4.2.
using System.Reflection;
using OreExtract.Core;

namespace OreExtractCli;

internal static class Program
{
    internal static readonly string ToolVersion =
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";   // e.g. 3.4.0+patches.5 (the release stamp)

    private const string Usage = """
ore-extract - predict a Valheim world's ore deposits by generating a copy of it headless

Usage:
  ore-extract run  (--save PATH | --seed NAME [--gen-version N]) [options]
  ore-extract run  --resume RUN_DIR
  ore-extract info --save PATH [--no-probe]
  ore-extract setup [--update] [--own-server]

Input (run):
  --save PATH          a copy of the world save: the world's folder (worlds_local/<world>, with
                       _main.N.fwl2, .db2, .chunks, .ok and .chunk files); an older .fwl/.db world is
                       converted by starting the current server on it once. The tool copies it; the
                       files given are only read.
  --seed NAME          instead of a save, for a world known to be fresh: its seed name
  --gen-version N      world-generation version for --seed (default 2: every world since it existed)

Speed (run):
  (default)            fast: as many processes as fit in 80% of the CPU threads and in memory
  --processes N        exactly N processes
  --slow               one process at the lowest CPU priority, to run next to a game server
  --cores N            hold the processes to N CPU cores (with --slow: default 1)

Other (run):
  --out FILE           output CSV (default: <world>-deposits.csv in the current folder)
  --prefabs A,B,...    prefabs to export (default: rock4_copper,MineRock_Tin,silvervein,
                       MineRock_Obsidian,mudpile_beacon)
  --memory-per-process GB  expected peak per process (default 3.5) for the memory checks
  --margin GB          memory to leave free (default 1)
  --no-guard           don't stop the run when free memory falls under the margin (testing only)
  --locations FILE     also write the world's location plan as loaded (prefab, position, placed)
  --limit N            generate at most N zones per process (testing)
  --keep               keep the run's working files
  --no-drop            keep every generated object in memory to the end (default: a zone's objects
                       are released once it is written, so memory stays near each process's base)
  --resume RUN_DIR     continue a run that was stopped (memory guard, Ctrl+C, a crash): its written
                       zones are kept and skipped; the other options are the stopped run's
  --run-dir DIR        the run's working folder (default: a new one under the work folder); it
                       must not hold a run already (resume that one instead)

For a program that runs the tool (run):
  --status FILE        keep a key=value file saying how the run is going (state, zones done and in
                       all, zones per minute, seconds left, the run folder, the reason for a failure)
  --parent-pid PID     stop (the run can be resumed) when that process is gone

Common:
  --work-dir DIR       where downloads and runs live (default: %LOCALAPPDATA%\ore-extract or
                       ~/.local/share/ore-extract)
  --server-dir DIR     use this Valheim dedicated server install; it is only read (on Windows it
                       must already have BepInEx's winhttp.dll)
  (default)            a server already on this machine (Steam's "Valheim dedicated server", or
                       /opt/valheim in a server container) and its BepInEx if it has one; else
                       ore-extract's own install, downloaded on first use and then reused as it is
  --own-server         always use ore-extract's own install
  --update             update ore-extract's own server install with steamcmd first (opt-in; an
                       install found elsewhere is never changed)
""";

    private static int Main(string[] argv)
    {
        try
        {
            Install.ToolVersion = ToolVersion;
            InstallSetup.Hook();
            var a = new Args(argv);
            if (a.Command == "run")
            {
                Status.Init(a.Get("--status"));
                if (a.Has("--parent-pid")) ParentWatch.Start(a.Int("--parent-pid", 0));
            }
            switch (a.Command)
            {
                case "run": return Runner.Run(a);
                case "info": return InfoCommand.Run(a);
                case "setup": return SetupCommand.Run(a);
                case "version": Console.WriteLine(ToolVersion); return 0;
                case "help": case "": Console.WriteLine(Usage); return a.Command == "" ? 2 : 0;
                default: Console.Error.WriteLine($"unknown command '{a.Command}'\n"); Console.WriteLine(Usage); return 2;
            }
        }
        catch (ToolError e)
        {
            Console.Error.WriteLine("error: " + e.Message);
            Status.Failed(e.Message);
            return 1;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("error: " + e);
            Status.Failed(e.GetType().Name + ": " + e.Message);
            return 1;
        }
    }
}
