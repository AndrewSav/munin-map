// Runs the generation: one or more dedicated server processes, each with its own BepInEx folder
// (holding only the ZoneGen plugin), its own copy of the world, its own ports and a share of the
// zones; watches them, guards memory, and joins their outputs into one file.
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ZoneGenCli;

internal sealed class Worker
{
    public int Index;
    public string Dir, BepRoot, SaveDir, OutDir, Tag;
    public int Port;
    public Process Proc;
    public long LogPos;
    public Dictionary<string, string> Info, Progress, Done;
    public string Error;
    public long PeakBytes;
    public double CpuSeconds;          // CPU time used so far (all its threads)
    public DateTime StartedUtc;
    public bool Exited, Killed;
    public string Protection;
    public readonly List<string> Tail = new();

    public string LogPath => Path.Combine(OutDir, Tag + ".log");
    public string CsvPath => Path.Combine(OutDir, Tag + "-deposits.csv");
    public string ZonesPath => Path.Combine(OutDir, Tag + "-zones.txt");
    public string PrevCsvPath => Path.Combine(OutDir, Tag + "-prev-deposits.csv");
    public string PrevZonesPath => Path.Combine(OutDir, Tag + "-prev-zones.txt");
    public int PrevZones;

    // resume: fold what earlier attempts wrote into the prev- files, keeping only rows of zones whose
    // line made it into the zones file (a zone's rows are flushed before its line)
    public void Consolidate()
    {
        var settled = new HashSet<string>();
        foreach (var f in new[] { PrevZonesPath, ZonesPath })
            if (File.Exists(f)) foreach (var l in File.ReadLines(f)) if (l.Length > 0) settled.Add(l.Trim());
        var rows = new HashSet<string>();
        foreach (var f in new[] { PrevCsvPath, CsvPath })
            if (File.Exists(f))
                foreach (var l in File.ReadLines(f).Skip(1))
                {
                    var p = l.Split(',');
                    if (p.Length == 6 && settled.Contains(p[4] + "," + p[5])) rows.Add(l);
                }
        File.WriteAllLines(PrevZonesPath, settled);
        File.WriteAllText(PrevCsvPath, "prefab,x,y,z,zx,zy\n" + string.Concat(rows.Select(r => r + "\n")));
        foreach (var f in new[] { ZonesPath, CsvPath }) if (File.Exists(f)) File.Delete(f);
        if (File.Exists(LogPath)) File.Move(LogPath, LogPath + ".prev-" + DateTime.Now.ToString("HHmmss"), true);
        PrevZones = settled.Count;
    }

    public static Worker Prepare(string runDir, int index, Install inst, byte[] plugin)
    {
        var w = new Worker { Index = index, Dir = Path.Combine(runDir, "p" + index), Tag = "p" + index };
        w.BepRoot = Path.Combine(w.Dir, "bep");
        w.SaveDir = Path.Combine(w.Dir, "save");
        w.OutDir = Path.Combine(w.Dir, "out");
        // BepInEx takes its folder from where the preloader is: <BepRoot>/BepInEx/core/...
        CopyDir(Path.Combine(inst.BepInExDir, "BepInEx", "core"), Path.Combine(w.BepRoot, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(w.BepRoot, "BepInEx", "plugins"));
        Directory.CreateDirectory(Path.Combine(w.BepRoot, "BepInEx", "config"));
        File.WriteAllBytes(Path.Combine(w.BepRoot, "BepInEx", "plugins", "ZoneGen.dll"), plugin);
        Directory.CreateDirectory(w.SaveDir);
        Directory.CreateDirectory(w.OutDir);
        return w;
    }

    public void Start(Install inst, string world, IDictionary<string, string> env, ProcessPriorityClass priority, IntPtr? affinity, Protection prot)
    {
        Port = Ports.FreePair();
        var psi = new ProcessStartInfo(inst.ServerExe)
        {
            UseShellExecute = false, WorkingDirectory = inst.ServerDir, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
        };
        string preloader = Path.Combine(BepRoot, "BepInEx", "core", "BepInEx.Preloader.dll");
        string pw = "zg" + Guid.NewGuid().ToString("N")[..10];
        foreach (var x in new[] { "-nographics", "-batchmode", "-name", "zonegen-" + Index, "-port", Port.ToString(), "-world", world,
                     "-password", pw, "-public", "0", "-savedir", SaveDir, "-logfile", Path.Combine(Dir, "server.log") })
            psi.ArgumentList.Add(x);
        psi.Environment["SteamAppId"] = "892970";
        if (Install.IsWindows)
        {
            psi.ArgumentList.Add("--doorstop-enabled"); psi.ArgumentList.Add("true");
            psi.ArgumentList.Add("--doorstop-target-assembly"); psi.ArgumentList.Add(preloader);
        }
        else
        {
            // doorstop and BepInEx from this tool's own copy: nothing is read from or written to a
            // BepInEx the server install may have (a container's live server keeps its own)
            psi.Environment["DOORSTOP_ENABLED"] = "1";
            psi.Environment["DOORSTOP_TARGET_ASSEMBLY"] = preloader;
            string dlibs = Path.Combine(inst.BepInExDir, "doorstop_libs");
            psi.Environment["LD_LIBRARY_PATH"] = Path.Combine(inst.ServerDir, "linux64") + ":" + dlibs;
            psi.Environment["LD_PRELOAD"] = Path.Combine(dlibs, "libdoorstop_x64.so");
        }
        psi.Environment["ZONEGEN_PARENT_PID"] = Environment.ProcessId.ToString();   // the server quits if this tool is gone (Linux)
        psi.Environment["ZONEGEN_OUT"] = OutDir;
        psi.Environment["ZONEGEN_TAG"] = Tag;
        foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
        Proc = Process.Start(psi) ?? throw new ToolError("could not start the server");
        Proc.OutputDataReceived += (_, e) => { };
        Proc.ErrorDataReceived += (_, e) => { };
        Proc.BeginOutputReadLine(); Proc.BeginErrorReadLine();
        StartedUtc = DateTime.UtcNow;
        Protection = prot.Apply(Proc);
        try { Proc.PriorityClass = priority; } catch { }
        if (affinity.HasValue) try { Proc.ProcessorAffinity = affinity.Value; } catch (Exception e) { Install.Say($"p{Index}: could not set CPU affinity: {e.Message}"); }
    }

    // new lines of the plugin's own log
    public void Poll()
    {
        if (File.Exists(LogPath))
        {
            using var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length > LogPos)
            {
                fs.Seek(LogPos, SeekOrigin.Begin);
                var bytes = new byte[fs.Length - LogPos];
                int n = fs.Read(bytes, 0, bytes.Length);
                int end = Array.LastIndexOf(bytes, (byte)'\n', n - 1);
                if (end >= 0)
                {
                    LogPos += end + 1;
                    foreach (var raw in Encoding.UTF8.GetString(bytes, 0, end).Split('\n'))
                    {
                        string line = raw.TrimEnd('\r');
                        int sp = line.IndexOf(' ');
                        string msg = sp >= 0 ? line[(sp + 1)..] : line;
                        Tail.Add(msg); if (Tail.Count > 20) Tail.RemoveAt(0);
                        if (msg.StartsWith("@info ")) Info = Kv(msg[6..]);
                        else if (msg.StartsWith("@progress ")) Progress = Kv(msg[10..]);
                        else if (msg.StartsWith("@done ")) Done = Kv(msg[6..]);
                        else if (msg.StartsWith("@error ")) Error = msg[7..];
                    }
                }
            }
        }
        if (Proc != null && !Exited)
        {
            try { Proc.Refresh(); if (!Proc.HasExited) { PeakBytes = Math.Max(PeakBytes, Proc.WorkingSet64); CpuSeconds = Proc.TotalProcessorTime.TotalSeconds; } } catch { }
            if (Proc.HasExited) Exited = true;
        }
    }

    public long CurrentBytes()
    {
        try { Proc.Refresh(); return Proc.HasExited ? 0 : Proc.WorkingSet64; } catch { return 0; }
    }

    public void Kill()
    {
        try { if (Proc != null && !Proc.HasExited) { Proc.Kill(true); Killed = true; } } catch { }
    }

    // k=v pairs, values optionally in double quotes
    public static Dictionary<string, string> Kv(string s)
    {
        var d = new Dictionary<string, string>();
        int i = 0;
        while (i < s.Length)
        {
            while (i < s.Length && s[i] == ' ') i++;
            int eq = s.IndexOf('=', i);
            if (eq < 0) break;
            string k = s[i..eq];
            i = eq + 1;
            string v;
            if (i < s.Length && s[i] == '"') { int q = s.IndexOf('"', i + 1); if (q < 0) q = s.Length; v = s[(i + 1)..q]; i = q + 1; }
            else { int sp = s.IndexOf(' ', i); if (sp < 0) sp = s.Length; v = s[i..sp]; i = sp; }
            d[k] = v;
        }
        return d;
    }

    public static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }
}

internal static class Probe
{
    // the game's location version and build, read from a server started just long enough for its
    // ZoneSystem to wake (cached per server build)
    public static (int locationVersion, string build) GameInfo(Install inst)
    {
        // the Steam build if the install has its manifest, else the server executable's size and time
        var exe = new FileInfo(inst.ServerExe);
        string id = inst.BuildId() ?? $"exe{exe.Length:x}-{exe.LastWriteTimeUtc.Ticks:x}";
        string cache = Path.Combine(inst.WorkDir, $"probe-{(inst.ManagedServer ? "managed" : Hash(inst.ServerDir))}-{id}.txt");
        if (File.Exists(cache))
        {
            var c = Worker.Kv(File.ReadAllText(cache).Trim());
            if (c.TryGetValue("locver", out var lv) && int.TryParse(lv, out int v)) return (v, c.GetValueOrDefault("build", "?"));
        }
        Install.Say("starting the server once to read the game's location version");
        string runDir = Path.Combine(inst.WorkDir, "runs", "probe-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var w = Worker.Prepare(runDir, 0, inst, Install.PluginBytes());
        using var prot = new Protection(8L << 30);
        w.Start(inst, "zonegen_probe", new Dictionary<string, string> { ["ZONEGEN_MODE"] = "probe" }, ProcessPriorityClass.BelowNormal, null, prot);
        var sw = Stopwatch.StartNew();
        while (w.Info == null && !w.Exited && sw.Elapsed.TotalSeconds < 300) { Thread.Sleep(500); w.Poll(); }
        w.Poll();
        w.Kill();
        try { w.Proc.WaitForExit(10000); } catch { }
        if (w.Info == null || !w.Info.TryGetValue("locver", out var s) || !int.TryParse(s, out int ver))
            throw new ToolError("the probe server didn't report the location version; its log: " + Path.Combine(w.Dir, "server.log"));
        string build = w.Info.GetValueOrDefault("build", "?");
        File.WriteAllText(cache, $"locver={ver} build={build}\n");
        try { Directory.Delete(runDir, true); } catch { }
        return (ver, build);
    }

    private static string Hash(string s) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..8];
}

internal static class Runner
{
    // measured (TASK-4.2): a process keeps about 2.0-2.2 threads busy when it has the CPU to itself
    // and about 1.6 when the CPU is 80% busy, so 80% of the threads
    // divided by 1.6 lands near 80% busy
    private const double ThreadsPerProcess = 1.6;

    public static int Run(Args a)
    {
        string resumeDir = a.Get("--resume");
        if (resumeDir == null) return Run(a, null, 0);
        resumeDir = Path.GetFullPath(resumeDir);
        string runFile = Path.Combine(resumeDir, "run.txt");
        if (!File.Exists(runFile)) throw new ToolError($"{resumeDir} is not a zonegen run folder (no run.txt)");
        var lines = File.ReadAllLines(runFile);
        Environment.CurrentDirectory = lines[0];
        int n = int.Parse(lines[1]);
        var orig = new Args(lines.Skip(2).ToArray());
        if (orig.Get("--work-dir") == null && a.Get("--work-dir") != null) throw new ToolError("give --work-dir only if the stopped run had it");
        Install.Say($"resuming {resumeDir}: {n} process(es), options: {string.Join(" ", orig.Raw)}");
        return Run(orig, resumeDir, n);
    }

    private static int Run(Args a, string resumeDir, int resumeN)
    {
        string savePath = a.Get("--save"), seed = a.Get("--seed");
        if ((savePath == null) == (seed == null)) throw new ToolError("give either --save PATH or --seed NAME");
        if (seed != null && seed.Length == 0) throw new ToolError("--seed needs a seed name");
        int genVer = a.Int("--gen-version", 2);
        bool slow = a.Has("--slow");
        if (slow && a.Has("--processes")) throw new ToolError("--slow runs one process; leave out --processes");
        double perProcGb = a.Num("--memory-per-process", 3.5), marginGb = a.Num("--margin", 1);
        bool guard = !a.Has("--no-guard");
        var prefabs = a.Get("--prefabs");

        var inst = Install.From(a);
        inst.EnsureAll(update: a.Has("--update") && resumeDir == null);

        SaveInput save = savePath != null ? SaveInput.Find(resumeDir != null ? Path.Combine(resumeDir, "input", File.ReadAllText(Path.Combine(resumeDir, "input.txt")).Trim()) : savePath) : null;
        SaveInput.Meta meta = save?.ReadMeta();
        var saveLoc = save?.ReadLocationVersion();
        string world = save?.WorldName ?? "zonegen_seed";

        var game = Probe.GameInfo(inst);
        string seedName = meta?.SeedName ?? seed;
        int gv = meta != null ? Math.Max(meta.GenVersion, 0) : genVer;
        Install.Say($"world {world}: seed {seedName}, world-generation version {gv}" + (save != null ? $", save {save.SaveNumber}" : " (from the seed, no save)"));
        Install.Say($"game: Valheim {game.build}, server build {inst.BuildId() ?? "?"}, location version {game.locationVersion}" + (saveLoc.HasValue ? $"; the save's location version {saveLoc.Value.locationVersion}" : ""));
        if (saveLoc.HasValue && saveLoc.Value.locationVersion != game.locationVersion)
            Install.Say("note: the location versions differ, so the unplaced locations are planned again on load, around the placed ones, as the world's own server does on its next start with this game version");
        if (save == null)
            Install.Say("note: a world from its seed alone matches the real one only if its location plan was never redone by a location-version change since it was created");

        // ---- how many processes ----
        long avail = Mem.Available(out string memWhere);
        long perProc = (long)(perProcGb * Mem.GB), margin = (long)(marginGb * Mem.GB);
        int byMem = (int)Math.Max(0, (avail - margin) / perProc);
        int logical = Cpu.Logical;
        int n;
        if (resumeDir != null) n = resumeN;
        else if (slow) n = 1;
        else if (a.Has("--processes")) n = Math.Max(1, a.Int("--processes", 1));
        else
        {
            int byCpu = Math.Max(1, (int)Math.Floor(0.8 * logical / ThreadsPerProcess));
            n = Math.Max(1, Math.Min(byCpu, byMem));
            Install.Say($"fast mode: {logical} CPU threads -> {byCpu} processes by CPU (80%, {ThreadsPerProcess} threads each); {avail / Mem.GB:F1} GB free ({memWhere}) -> {byMem} by memory ({perProcGb} GB each, {marginGb} GB kept free): {n}");
        }
        if (guard && n * perProc + margin > avail)
            throw new ToolError($"not enough memory for {n} process(es): {avail / Mem.GB:F1} GB free ({memWhere}), each needs about {perProcGb} GB plus {marginGb} GB kept free" +
                (n > 1 ? $"; try --processes {Math.Max(1, byMem)}" : ""));

        int cores = a.Int("--cores", slow ? 1 : 0);
        IntPtr? affinity = cores > 0 ? Cpu.AffinityMask(cores) : null;
        var priority = slow ? ProcessPriorityClass.Idle : ProcessPriorityClass.BelowNormal;

        // ---- prepare and start ----
        string runDir = resumeDir ?? Path.Combine(inst.WorkDir, "runs", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        if (resumeDir == null)
        {
            Directory.CreateDirectory(runDir);
            // the run's own copy of the input: every process, and a resume, copies from it, so a
            // live save moving on meanwhile doesn't matter
            if (save != null)
            {
                string inDir = Path.Combine(runDir, "input");
                save.CopyTo(inDir);
                string rel = save.WorldName;
                File.WriteAllText(Path.Combine(runDir, "input.txt"), rel);
                save = SaveInput.Find(Path.Combine(inDir, rel));
            }
            File.WriteAllLines(Path.Combine(runDir, "run.txt"), new[] { Environment.CurrentDirectory, n.ToString() }.Concat(a.Raw));
        }
        var plugin = Install.PluginBytes();
        var env = new Dictionary<string, string>
        {
            // slow mode is held back by its CPU priority and core limit, not by the frame: a frame cap and
            // a small budget only left its own core idle (TASK-4.2 live test: 186-240 zones/min)
            ["ZONEGEN_MODE"] = "generate", ["ZONEGEN_BUDGET_MS"] = "30", ["ZONEGEN_WINDOW"] = "8",
            ["ZONEGEN_UNCAP"] = "1", ["ZONEGEN_AHEAD"] = "16", ["ZONEGEN_NOSAVE"] = "1", ["ZONEGEN_NODUNGEON"] = "1",
            ["ZONEGEN_VEGCUT"] = "auto", ["ZONEGEN_DROP"] = a.Has("--no-drop") ? "0" : "1",
        };
        if (prefabs != null) env["ZONEGEN_PREFABS"] = prefabs;
        if (save == null) { env["ZONEGEN_SEED"] = seed; env["ZONEGEN_GENVER"] = genVer.ToString(); }
        string locOut = a.Get("--locations");
        if (locOut != null) env["ZONEGEN_LOCATIONS"] = "1";
        if (Environment.GetEnvironmentVariable("ZONEGEN_UNLOAD") is string ul) env["ZONEGEN_UNLOAD"] = ul;   // testing
        if (a.Has("--limit")) env["ZONEGEN_LIMIT"] = a.Int("--limit", 0).ToString();

        var workers = new List<Worker>();
        using var prot = new Protection(Math.Max(perProc * 2, 6L << 30));
        void KillAll() { foreach (var w in workers) w.Kill(); }
        ConsoleCancelEventHandler onCancel = (_, e) => { Install.Say("stopping"); KillAll(); };
        Console.CancelKeyPress += onCancel;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAll();
        var wall = Stopwatch.StartNew();
        try
        {
            for (int k = 0; k < n; k++)
            {
                var w = Worker.Prepare(runDir, k, inst, plugin);
                var e = new Dictionary<string, string>(env) { ["ZONEGEN_SHARE"] = n == 1 && Environment.GetEnvironmentVariable("ZONEGEN_TEST_SHARE") is string ts ? ts : $"{k}/{n}" };   // testing: one process on a share
                if (resumeDir != null)
                {
                    w.Consolidate();
                    if (Directory.Exists(w.SaveDir)) { Directory.Delete(w.SaveDir, true); Directory.CreateDirectory(w.SaveDir); }
                    e["ZONEGEN_SKIP"] = w.PrevZonesPath;
                    Install.Say($"p{k}: {w.PrevZones} zones already written");
                }
                if (save != null) save.CopyTo(Path.Combine(w.SaveDir, "worlds_local"));
                w.Start(inst, world, e, priority, affinity, prot);
                workers.Add(w);
                Install.Say($"p{k}: started (pid {w.Proc.Id}, port {w.Port}, {w.Protection}{(slow ? ", lowest priority" : "")}{(cores > 0 ? $", {cores} core(s)" : "")})");
            }
            string stopReason = Watch(workers, guard, margin, seedName, save != null ? saveLoc?.generatedZones : null);
            if (stopReason != null)
            {
                KillAll();
                throw new ToolError(stopReason + Resume(runDir));
            }
        }
        finally { Console.CancelKeyPress -= onCancel; }

        // ---- results ----
        var bad = workers.Where(w => w.Done == null).ToList();
        if (bad.Count > 0)
        {
            var sb = new StringBuilder($"{bad.Count} of {n} process(es) didn't finish:");
            foreach (var w in bad)
                sb.Append($"\n  p{w.Index}: {(w.Error ?? (w.Killed ? "stopped" : "exited without a result"))}; server log {Path.Combine(w.Dir, "server.log")}\n    last lines: " + string.Join("\n    ", w.Tail.TakeLast(5)));
            sb.Append(Resume(runDir));
            throw new ToolError(sb.ToString());
        }
        string outPath = Path.GetFullPath(a.Get("--out") ?? Path.Combine(Environment.CurrentDirectory, world + "-deposits.csv"));
        var rows = new List<string>();
        foreach (var w in workers)
            foreach (var f in new[] { w.PrevCsvPath, w.CsvPath })
                if (File.Exists(f)) rows.AddRange(File.ReadLines(f).Skip(1).Where(l => l.Length > 0));
        rows.Sort(string.CompareOrdinal);
        int zones = workers.Sum(w => int.Parse(w.Done["zones"]) + w.PrevZones), failed = workers.Sum(w => int.Parse(w.Done["failed"]));
        var info = workers[0].Info ?? new Dictionary<string, string>();
        bool replanned = workers.Any(w => w.Info?.GetValueOrDefault("replanned") == "1");
        long peakEach = workers.Max(w => w.PeakBytes);
        var hdr = new StringBuilder();
        void H(string k, object v) => hdr.Append("# ").Append(k).Append(": ").Append(v).Append('\n');
        H("tool", "zonegen " + Program.ToolVersion);
        H("input", save != null ? $"world save copy (save {save.SaveNumber})" : "seed only");
        H("world", world);
        H("seed", seedName);
        H("world_generation_version", gv);
        H("location_version_game", game.locationVersion);
        if (saveLoc.HasValue) H("location_version_save", saveLoc.Value.locationVersion);
        H("locations_replanned_on_load", replanned ? "yes" : "no");
        H("game_version", info.GetValueOrDefault("build", game.build));
        H("server_build", inst.BuildId() ?? "?");
        H("prefabs", prefabs ?? "rock4_copper,MineRock_Tin,silvervein,MineRock_Obsidian,mudpile_beacon");
        H("zones_generated", zones);
        H("zones_failed", failed);
        H("processes", n);
        if (resumeDir != null) H("resumed", "yes (seconds: the last attempt only)");
        H("seconds", ((int)wall.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        H("created_utc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        File.WriteAllText(outPath, hdr + "prefab,x,y,z,zx,zy\n" + string.Join("\n", rows) + (rows.Count > 0 ? "\n" : ""));
        if (locOut != null)
        {
            locOut = Path.GetFullPath(locOut);
            File.Copy(Path.Combine(workers[0].OutDir, workers[0].Tag + "-locations.csv"), locOut, true);
            Install.Say($"location plan as loaded -> {locOut}");
        }
        Install.Say($"done in {Fmt(wall.Elapsed)}: {zones} zones generated, {failed} failed, {rows.Count} deposits -> {outPath}");
        Install.Say($"peak memory: {peakEach / Mem.GB:F2} GB in the largest process, {workers.Sum(w => w.PeakBytes) / Mem.GB:F2} GB summed over {n}");
        // CPU threads a process kept busy on average, over its life (last sample before it exited)
        var thr = workers.Select(w => w.CpuSeconds / Math.Max(1, (DateTime.UtcNow - w.StartedUtc).TotalSeconds)).ToList();
        Install.Say($"CPU: {thr.Average():F1} threads busy per process on average ({string.Join(", ", thr.Select(t => t.ToString("F1", CultureInfo.InvariantCulture)))}), {thr.Sum():F1} of {Cpu.Logical} in all");
        if (!a.Has("--keep")) { try { Directory.Delete(runDir, true); } catch { } }
        else Install.Say($"working files kept in {runDir}");
        return failed > 0 ? 3 : 0;
    }

    // until every process has exited; returns why the run was stopped, or null
    private static string Watch(List<Worker> workers, bool guard, long margin, string seedName, int? generatedZones)
    {
        var report = Stopwatch.StartNew();
        bool announcedInfo = false;
        var checkedInfo = new HashSet<int>();
        while (true)
        {
            Thread.Sleep(1000);
            foreach (var w in workers) w.Poll();
            // the server falls back to creating a new world with a random seed if it doesn't find
            // the one named: make sure each loaded the world it was given
            foreach (var w in workers.Where(w => w.Info != null && checkedInfo.Add(w.Index)))
            {
                string got = w.Info.GetValueOrDefault("seedname");
                if (got != seedName) return $"p{w.Index} loaded a world with seed '{got}', not '{seedName}': the world copy wasn't found";
                if (generatedZones.HasValue && w.Info.TryGetValue("generated", out var gz) && int.TryParse(gz, out int g) && g != generatedZones.Value)
                    return $"p{w.Index} loaded a world with {g} generated zones, but the save has {generatedZones.Value}: the world copy wasn't the one loaded";
            }
            if (!announcedInfo && workers[0].Info != null)
            {
                announcedInfo = true;
                var i = workers[0].Info;
                Install.Say($"world loaded: {i.GetValueOrDefault("generated")} zones already generated{(i.GetValueOrDefault("replanned") == "1" ? ", locations planned again on load" : "")}{(i.GetValueOrDefault("fromseed") == "1" ? ", created from the seed" : "")}");
            }
            // a process that never reports (BepInEx or the plugin didn't load) would idle forever
            var silent = workers.FirstOrDefault(w => w.Info == null && !w.Exited && (DateTime.Now - w.Proc.StartTime).TotalMinutes > 10);
            if (silent != null) return $"p{silent.Index} hasn't reported in 10 minutes: BepInEx or the plugin didn't load (a game version this build doesn't support?); server log: {Path.Combine(silent.Dir, "server.log")}";
            var err = workers.FirstOrDefault(w => w.Error != null);
            if (err != null) return $"p{err.Index} failed: {err.Error}";
            if (guard)
            {
                long avail = Mem.Available(out string where);
                if (avail < margin)
                    return $"stopped by the memory guard: {avail / Mem.GB:F2} GB free ({where}), under the {margin / Mem.GB:F1} GB margin (processes at {string.Join(", ", workers.Select(w => $"{w.CurrentBytes() / Mem.GB:F1}"))} GB)";
            }
            if (workers.All(w => w.Exited)) { foreach (var w in workers) w.Poll(); return null; }
            var dead = workers.FirstOrDefault(w => w.Exited && w.Done == null);
            if (dead != null) { Thread.Sleep(1500); dead.Poll(); if (dead.Done == null) return $"p{dead.Index} exited without a result (exit code {Safe(() => dead.Proc.ExitCode)}); last lines:\n  " + string.Join("\n  ", dead.Tail.TakeLast(5)) + $"\n  server log: {Path.Combine(dead.Dir, "server.log")}"; }
            if (report.Elapsed.TotalSeconds >= 30)
            {
                report.Restart();
                // memory over the run, per process: for working out what a run needs
                try
                {
                    string memLog = Path.Combine(Path.GetDirectoryName(workers[0].Dir), "memory.csv");
                    if (!File.Exists(memLog)) File.WriteAllText(memLog, "utc,process,zones_done,bytes,cpu_seconds\n");
                    File.AppendAllText(memLog, string.Concat(workers.Select(w =>
                        $"{DateTime.UtcNow:HH:mm:ss},{w.Index},{(w.Progress != null ? w.Progress["done"] : "0")},{w.CurrentBytes()},{w.CpuSeconds.ToString("F0", CultureInfo.InvariantCulture)}\n")));
                }
                catch { }
                int done = workers.Sum(w => w.Progress != null ? int.Parse(w.Progress["done"]) : 0);
                int total = workers.Sum(w => w.Progress != null ? int.Parse(w.Progress["total"]) : 0);
                double rate = workers.Sum(w => w.Progress != null ? double.Parse(w.Progress["rate"], CultureInfo.InvariantCulture) : 0);
                long mem = workers.Sum(w => w.CurrentBytes());
                if (total > 0)
                {
                    string eta = rate > 0 ? $", about {Fmt(TimeSpan.FromMinutes((total - done) / rate))} left" : "";
                    Install.Say($"{done}/{total} zones ({100.0 * done / total:F0}%), {rate:F0} zones/min{eta}, memory {mem / Mem.GB:F1} GB");
                }
                else Install.Say($"loading the world, memory {mem / Mem.GB:F1} GB");
            }
        }
    }

    private static string Resume(string runDir) =>
        $"\nwhat was written so far is kept in {runDir}\nto continue: zonegen run --resume \"{runDir}\"";

    private static string Safe(Func<int> f) { try { return f().ToString(); } catch { return "?"; } }

    private static string Fmt(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min" : t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} min {t.Seconds} s" : $"{t.Seconds} s";
}
