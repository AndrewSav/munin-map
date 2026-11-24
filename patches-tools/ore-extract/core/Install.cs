// The dedicated server install and BepInEx a run uses, and the plugin it gives them. Finding an
// install on the machine and downloading one are the CLI's (cli/Setup.cs, through the hooks below);
// Munin gives the install it runs in (--server-dir), which has BepInEx, so it downloads nothing.
using System.Reflection;

namespace OreExtract.Core;

public sealed class Install
{
    // BepInExPack_Valheim as the live and test servers run it (TASK-4.2, 2026-11-22)
    public const string BepInExVersion = "5.4.2360";
    public const string ServerAppId = "896660";

    // set by the program that runs a run: the version it reports, and where its messages go
    public static string ToolVersion = "?";
    public static Action<string> Output = Console.WriteLine;
    // the CLI's: an install already on this machine (dir, how it was found), and getting what's missing
    public static Func<(string dir, string source)> FindExisting = () => (null, null);
    public static Action<Install, bool> Ensure = (i, update) => i.CheckExistingServer();

    public string WorkDir, ServerDir, BepInExDir;
    public bool ManagedServer, BepInExReused;
    public string ServerSource;   // how the server was chosen, for the log
    public static bool IsWindows => Compat.IsWindows;
    public string ServerExe => ExeIn(ServerDir);
    public static string ExeIn(string dir) => Path.Combine(dir, IsWindows ? "valheim_server.exe" : "valheim_server.x86_64");

    // baseDir: where relative paths in the options are from (a resume: where the run was started)
    public static Install From(Args a, string baseDir = null)
    {
        baseDir ??= Environment.CurrentDirectory;
        string Full(string p) => p == null ? null : Path.GetFullPath(Path.Combine(baseDir, p));
        var i = new Install { WorkDir = Full(a.Get("--work-dir")) ?? Path.GetFullPath(DefaultWorkDir()) };
        Directory.CreateDirectory(i.WorkDir);
        i.BepInExDir = Path.Combine(i.WorkDir, "bepinex-" + BepInExVersion);
        // which server: --server-dir; else one already on this machine (Steam's, or a server
        // container's), which matches what that machine runs; else this tool's own (--own-server
        // forces it), downloaded on first use
        string sd = Full(a.Get("--server-dir")), found = null;
        if (sd == null && !a.Has("--own-server")) (found, i.ServerSource) = FindExisting();
        i.ManagedServer = sd == null && found == null;
        i.ServerDir = Path.GetFullPath(sd ?? found ?? Path.Combine(i.WorkDir, "server"));
        if (sd != null) i.ServerSource = "--server-dir";
        else if (found == null) i.ServerSource = "ore-extract's own install";
        // a server that has BepInEx brings its own copy: use it rather than download one
        string core = Path.Combine(i.ServerDir, "BepInEx", "core", "BepInEx.Preloader.dll");
        string loader = IsWindows ? Path.Combine(i.ServerDir, "winhttp.dll") : Path.Combine(i.ServerDir, "doorstop_libs", "libdoorstop_x64.so");
        if (!i.ManagedServer && File.Exists(core) && File.Exists(loader)) { i.BepInExDir = i.ServerDir; i.BepInExReused = true; }
        return i;
    }

    public static string DefaultWorkDir()
    {
        if (IsWindows) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ore-extract");
        string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        string home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
        return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".local", "share") : xdg, "ore-extract");
    }

    public void EnsureAll(bool update = false) => Ensure(this, update);

    // an install this tool doesn't manage: only checked, never changed
    public void CheckExistingServer()
    {
        if (!File.Exists(ServerExe)) throw new ToolError($"no Valheim dedicated server in {ServerDir} ({Path.GetFileName(ServerExe)} not found)");
        if (IsWindows && !File.Exists(Path.Combine(ServerDir, "winhttp.dll")))
            throw new ToolError($"{ServerDir} has no BepInEx (winhttp.dll); this tool doesn't change a server it doesn't manage. Leave out --server-dir to let it install its own.");
        if (!BepInExReused && !File.Exists(Path.Combine(BepInExDir, "BepInEx", "core", "BepInEx.Preloader.dll")))
            throw new ToolError($"no BepInEx in {ServerDir}");
    }

    public string BuildId()
    {
        string acf = Path.Combine(ServerDir, "steamapps", $"appmanifest_{ServerAppId}.acf");
        if (!File.Exists(acf)) return null;
        foreach (var line in File.ReadLines(acf))
        {
            var t = line.Trim();
            if (t.StartsWith("\"buildid\"")) return t.Split(['"'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
        }
        return null;
    }

    // the plugin, embedded in this library
    public static byte[] PluginBytes()
    {
        using var s = typeof(Install).Assembly.GetManifestResourceStream("OreExtract.dll") ?? throw new ToolError("plugin missing from this build");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static void Say(string s) => Output($"[{DateTime.Now:HH:mm:ss}] {s}");
}
