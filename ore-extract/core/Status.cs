// How a run is going (TASK-4.5): kept in memory for Munin, which runs the supervisor in-process, and
// written whole at each change to --status FILE (key=value lines) for a program that runs the CLI.
// The file is an option of the invocation, not of the run: a resume takes it from its own command line.
using System.Globalization;
using System.Text;

namespace OreExtract.Core;

public static class Status
{
    private static string path;
    private static readonly Dictionary<string, string> values = new();
    private static readonly object gate = new();

    public static void Init(string file)
    {
        lock (gate) { values.Clear(); path = file != null ? Path.GetFullPath(file) : null; }
        Set(("state", "starting"), ("pid", Compat.ProcessId), ("tool", Install.ToolVersion));
    }

    // a copy of the current values
    public static Dictionary<string, string> Snapshot()
    {
        lock (gate) return new Dictionary<string, string>(values);
    }

    // state: starting (installing, probing, copying the save), loading (the servers are loading the
    // world), running, done, failed, stopped
    public static void Set(params (string key, object value)[] kv)
    {
        lock (gate)
        {
            foreach (var (k, v) in kv)
            {
                if (v == null) { values.Remove(k); continue; }
                string s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
                values[k] = s.Replace("\r", "").Replace("\n", " / ");
            }
            values["updated_utc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var sb = new StringBuilder();
            foreach (var e in values) sb.Append(e.Key).Append('=').Append(e.Value).Append('\n');
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString());
                Compat.MoveOver(tmp, path);
            }
            catch { }   // the status is a courtesy: never what stops a run
        }
    }

    public static void Failed(string reason) => Set(("state", "failed"), ("reason", reason));
}
