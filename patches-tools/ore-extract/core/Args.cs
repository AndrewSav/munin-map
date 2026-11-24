// Command-line options of a run: the CLI's own, and the ones Munin passes when it runs the supervisor
// in-process (TASK-4.5). A run folder records them (run.txt), so a resume takes the same.
namespace OreExtract.Core;

public sealed class ToolError(string message) : Exception(message);

public sealed class Args
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
