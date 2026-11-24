// Downloads, on first use: steamcmd for this platform, the Valheim dedicated server (app 896660,
// anonymous) through it, and BepInExPack_Valheim at a pinned version with a checked hash. Nothing is
// shipped in the bundle except the tool and its own plugin.
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using OreExtract.Core;

namespace OreExtractCli;

// the CLI's part of Install (core/Install.cs): finding an install on this machine and downloading
// what's missing; Program sets the hooks
internal static class InstallSetup
{
    private const string BepInExVersion = Install.BepInExVersion;
    private const string BepInExUrl = "https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/" + BepInExVersion + "/";
    private const string BepInExSha256 = "6bca77b1016acfe0ffaebb6534c84b12ae8d12e5a96abbf59ec6e4526204fc6f";
    private const string SteamCmdWin = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    private const string SteamCmdLinux = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz";
    private const string ServerAppId = Install.ServerAppId;
    private static bool IsWindows => Install.IsWindows;
    private static string ExeIn(string dir) => Install.ExeIn(dir);
    private static void Say(string s) => Install.Say(s);

    public static void Hook()
    {
        Install.FindExisting = () => { string d = FindExisting(out string src); return (d, src); };
        Install.Ensure = EnsureAll;
    }

    // an existing dedicated server install this tool can use without changing it
    private static string FindExisting(out string source)
    {
        var dirs = new List<string>();
        const string common = "steamapps/common/Valheim dedicated server";
        if (IsWindows)
        {
            var steamRoots = new List<string>();
            try
            {
                if (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string sp) steamRoots.Add(sp);
                if (Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) is string ip) steamRoots.Add(ip);
            }
            catch { }
            steamRoots.Add(@"C:\Program Files (x86)\Steam");
            foreach (var r in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                dirs.Add(Path.Combine(r, common));
                string vdf = Path.Combine(r, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    dirs.Add(Path.Combine(m.Groups[1].Value.Replace("\\\\", "\\"), common));
            }
        }
        else
        {
            string home = Environment.GetEnvironmentVariable("HOME") ?? "/root";
            // the community Valheim server image: /opt/valheim/bepinex is the server merged with
            // BepInEx, /opt/valheim/server the plain one
            dirs.AddRange(["/opt/valheim/bepinex", "/opt/valheim/server",
                Path.Combine(home, ".steam/steam", common), Path.Combine(home, ".local/share/Steam", common)]);
        }
        foreach (var d in dirs.Select(x => Path.GetFullPath(x)).Distinct(IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
        {
            if (!File.Exists(ExeIn(d))) continue;
            if (IsWindows && !File.Exists(Path.Combine(d, "winhttp.dll")))
            {
                Say($"found a server at {d}, but without BepInEx (winhttp.dll); this tool doesn't change it, so it isn't used");
                continue;
            }
            source = "found on this machine";
            return Path.GetFullPath(d);
        }
        source = null;
        return null;
    }

    private static void EnsureAll(Install i, bool update)
    {
        if (!IsWindows && !(OperatingSystem.IsLinux())) throw new ToolError("only Windows and Linux are supported");
        if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture != System.Runtime.InteropServices.Architecture.X64)
            throw new ToolError("the Valheim dedicated server needs an x64 machine");
        if (!IsWindows) LinuxPreflight(steamcmd: i.ManagedServer && (!File.Exists(i.ServerExe) || update));
        if (!i.BepInExReused) EnsureBepInEx(i);
        if (i.ManagedServer) EnsureServer(i, update);
        else
        {
            i.CheckExistingServer();
            if (update) Say($"--update: the server at {i.ServerDir} isn't ore-extract's own install, so it isn't updated here; update it the way it is normally updated (or use --own-server)");
        }
        Say($"server: {i.ServerDir} ({i.ServerSource}, build {i.BuildId() ?? "?"}); BepInEx: {(i.BepInExReused ? "the server's own" : $"{BepInExVersion} downloaded by ore-extract")}");
    }

    // ---- BepInEx ----
    private static void EnsureBepInEx(Install i)
    {
        string BepInExDir = i.BepInExDir, WorkDir = i.WorkDir;
        if (File.Exists(Path.Combine(BepInExDir, "BepInEx", "core", "BepInEx.Preloader.dll"))) return;
        string cache = Path.Combine(WorkDir, "downloads");
        Directory.CreateDirectory(cache);
        string zip = Path.Combine(cache, $"BepInExPack_Valheim-{BepInExVersion}.zip");
        if (!File.Exists(zip) || Sha256(zip) != BepInExSha256)
        {
            Say($"downloading BepInExPack_Valheim {BepInExVersion} from Thunderstore");
            Download(BepInExUrl, zip);
        }
        string got = Sha256(zip);
        if (got != BepInExSha256) { File.Delete(zip); throw new ToolError($"BepInExPack_Valheim download has the wrong checksum ({got}, expected {BepInExSha256})"); }
        string tmp = BepInExDir + ".tmp";
        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        ZipFile.ExtractToDirectory(zip, tmp);
        string inner = Path.Combine(tmp, "BepInExPack_Valheim");
        if (!Directory.Exists(inner)) throw new ToolError("unexpected BepInExPack_Valheim layout");
        if (Directory.Exists(BepInExDir)) Directory.Delete(BepInExDir, true);
        Directory.Move(inner, BepInExDir);
        Directory.Delete(tmp, true);
    }

    // ---- server through steamcmd ----
    private static void EnsureServer(Install i, bool update)
    {
        string WorkDir = i.WorkDir, ServerDir = i.ServerDir, ServerExe = i.ServerExe;
        Func<string> BuildId = i.BuildId;
        void InstallDoorstopWindows() => InstallDoorstop(i);
        if (File.Exists(ServerExe) && !update) { InstallDoorstopWindows(); return; }
        string steamDir = Path.Combine(WorkDir, "steamcmd");
        string steamcmd = Path.Combine(steamDir, IsWindows ? "steamcmd.exe" : "steamcmd.sh");
        if (!File.Exists(steamcmd))
        {
            Directory.CreateDirectory(steamDir);
            string cache = Path.Combine(WorkDir, "downloads");
            Directory.CreateDirectory(cache);
            Say("downloading steamcmd");
            if (IsWindows)
            {
                string zip = Path.Combine(cache, "steamcmd.zip");
                Download(SteamCmdWin, zip);
                ZipFile.ExtractToDirectory(zip, steamDir, true);
            }
            else
            {
                string tgz = Path.Combine(cache, "steamcmd_linux.tar.gz");
                Download(SteamCmdLinux, tgz);
                using var fs = File.OpenRead(tgz);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gz, steamDir, true);
            }
        }
        Directory.CreateDirectory(ServerDir);
        // steamcmd updates itself on its first runs and sometimes exits before installing: retry
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            Say($"installing the Valheim dedicated server with steamcmd (attempt {attempt}; a few minutes the first time)");
            var psi = new ProcessStartInfo(steamcmd) { UseShellExecute = false, WorkingDirectory = steamDir };
            foreach (var x in new[] { "+force_install_dir", ServerDir, "+login", "anonymous", "+app_update", ServerAppId, "validate", "+quit" }) psi.ArgumentList.Add(x);
            using (var p = Process.Start(psi)) p.WaitForExit();
            if (File.Exists(ServerExe) && BuildId() != null) break;
            if (attempt == 4) throw new ToolError("steamcmd did not install the server; see its output above");
        }
        InstallDoorstopWindows();
    }

    // Windows loads BepInEx through doorstop's winhttp.dll beside the server exe; only in the
    // install this tool manages
    private static void InstallDoorstop(Install i)
    {
        if (!IsWindows || !i.ManagedServer) return;
        foreach (var f in new[] { "winhttp.dll", "doorstop_config.ini" })
        {
            string src = Path.Combine(i.BepInExDir, f), dst = Path.Combine(i.ServerDir, f);
            if (!File.Exists(dst) || Sha256(dst) != Sha256(src)) File.Copy(src, dst, true);
        }
    }

    // ---- Linux system libraries ----
    private static readonly string[] LibDirs64 = ["/lib/x86_64-linux-gnu", "/usr/lib/x86_64-linux-gnu", "/lib64", "/usr/lib64", "/usr/lib", "/lib"];
    private static readonly string[] LibDirs32 = ["/lib/i386-linux-gnu", "/usr/lib/i386-linux-gnu", "/lib32", "/usr/lib32"];
    private static bool HasLib(string[] dirs, string name) => dirs.Any(d => File.Exists(Path.Combine(d, name)));

    private static void LinuxPreflight(bool steamcmd)
    {
        var missing = new List<(string lib, string deb, string rpm)>();
        // this tool's own downloads (.NET uses the system's OpenSSL and certificate store)
        if (!HasLib(LibDirs64, "libssl.so.3") && !HasLib(LibDirs64, "libssl.so.1.1")) missing.Add(("libssl (OpenSSL 3 or 1.1, for HTTPS downloads)", "libssl3", "openssl-libs"));
        if (!new[] { "/etc/ssl/certs/ca-certificates.crt", "/etc/pki/tls/certs/ca-bundle.crt", "/etc/ssl/ca-bundle.pem", "/etc/ssl/cert.pem" }.Any(File.Exists))
            missing.Add(("CA certificates (for HTTPS downloads)", "ca-certificates", "ca-certificates"));
        if (steamcmd && !HasLib(LibDirs32, "libgcc_s.so.1")) missing.Add(("32-bit libgcc_s.so.1 (steamcmd)", "lib32gcc-s1", "glibc.i686 libgcc.i686"));
        if (!HasLib(LibDirs64, "libatomic.so.1")) missing.Add(("libatomic.so.1 (Valheim server)", "libatomic1", "libatomic"));
        if (missing.Count == 0) return;
        throw new ToolError("missing system libraries:\n" + string.Join("\n", missing.Select(m => "  " + m.lib)) +
            "\nInstall them, for example:\n  Debian/Ubuntu: sudo apt-get install " + string.Join(" ", missing.Select(m => m.deb)) +
            "\n  Fedora/RHEL:   sudo dnf install " + string.Join(" ", missing.Select(m => m.rpm)));
    }

    // ---- helpers ----
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
    static InstallSetup() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("ore-extract/" + Program.ToolVersion);

    private static void Download(string url, string path)
    {
        string tmp = path + ".part";
        using (var resp = Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
        {
            if (!resp.IsSuccessStatusCode) throw new ToolError($"download failed: {url}: HTTP {(int)resp.StatusCode}");
            using var src = resp.Content.ReadAsStream();
            using var dst = File.Create(tmp);
            src.CopyTo(dst);
        }
        File.Move(tmp, path, true);
    }

    public static string Sha256(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
    }
}

internal static class SetupCommand
{
    public static int Run(Args a)
    {
        var i = Install.From(a);
        i.EnsureAll(update: a.Has("--update"));
        Install.Say($"ready: server {i.ServerDir} (build {i.BuildId() ?? "?"}), BepInEx {Install.BepInExVersion} in {i.BepInExDir}");
        return 0;
    }
}
