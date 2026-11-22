// zonegen: generate the ungenerated zones of a copy of a Valheim world on a headless dedicated
// server, and write out the ore deposits (or other prefabs) that generation placed. TASK-4.2.
using System.Reflection;

namespace ZoneGenCli;

internal static class Program
{
    internal static readonly string ToolVersion =
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "?";

    private const string Usage = """
zonegen - predict a Valheim world's ore deposits by generating a copy of it headless

Usage:
  zonegen run  (--save PATH | --seed NAME [--gen-version N]) [options]
  zonegen run  --resume RUN_DIR
  zonegen info --save PATH [--no-probe]
  zonegen setup [--update] [--own-server]

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

Common:
  --work-dir DIR       where downloads and runs live (default: %LOCALAPPDATA%\zonegen or
                       ~/.local/share/zonegen)
  --server-dir DIR     use this Valheim dedicated server install; it is only read (on Windows it
                       must already have BepInEx's winhttp.dll)
  (default)            a server already on this machine (Steam's "Valheim dedicated server", or
                       /opt/valheim in a server container) and its BepInEx if it has one; else
                       zonegen's own install, downloaded on first use and then reused as it is
  --own-server         always use zonegen's own install
  --update             update zonegen's own server install with steamcmd first (opt-in; an
                       install found elsewhere is never changed)
""";

    private static int Main(string[] argv)
    {
        try
        {
            var a = new Args(argv);
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
            return 1;
        }
    }
}

internal sealed class ToolError(string message) : Exception(message);

internal sealed class Args
{
    public readonly string Command = "";
    private readonly Dictionary<string, string> values = new();
    private readonly HashSet<string> flags = new();
    private static readonly HashSet<string> FlagNames = ["--slow", "--keep", "--no-guard", "--update", "--no-probe", "--no-drop", "--own-server"];

    public readonly string[] Raw;

    public Args(string[] argv)
    {
        Raw = argv;
        int i = 0;
        if (argv.Length > 0 && !argv[0].StartsWith("-")) Command = argv[i++];
        else if (argv.Length > 0 && (argv[0] == "--help" || argv[0] == "-h")) { Command = "help"; return; }
        for (; i < argv.Length; i++)
        {
            string k = argv[i];
            if (k == "--help" || k == "-h") { Command = "help"; return; }
            if (!k.StartsWith("--")) throw new ToolError($"unexpected argument '{k}'");
            if (FlagNames.Contains(k)) { flags.Add(k); continue; }
            if (i + 1 >= argv.Length) throw new ToolError($"{k} needs a value");
            values[k] = argv[++i];
        }
    }

    public string Get(string k, string d = null) => values.TryGetValue(k, out var v) ? v : d;
    public bool Has(string k) => flags.Contains(k) || values.ContainsKey(k);

    public int Int(string k, int d)
    {
        if (!values.TryGetValue(k, out var v)) return d;
        if (!int.TryParse(v, out int r) || r < 0) throw new ToolError($"{k} needs a whole number, not '{v}'");
        return r;
    }

    public double Num(string k, double d)
    {
        if (!values.TryGetValue(k, out var v)) return d;
        if (!double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double r) || r < 0)
            throw new ToolError($"{k} needs a number, not '{v}'");
        return r;
    }
}
