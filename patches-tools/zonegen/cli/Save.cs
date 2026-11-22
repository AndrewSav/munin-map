// A world save given to the tool: found, read (seed, versions) and copied. The original files are
// only ever read.
using System.IO.Compression;
using System.Text;

namespace ZoneGenCli;

internal sealed class SaveInput
{
    public string WorldName, Dir;
    public bool Chunked;
    public uint SaveNumber;          // chunked: the newest complete save (its .ok exists)
    public string MetaPath, DbPath;

    // PATH: a world folder of the current (chunked) format, any file in it, or an older .fwl file
    public static SaveInput Find(string path)
    {
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (File.Exists(path))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".fwl" || ext == ".db")
            {
                string dir = Path.GetDirectoryName(path), name = Path.GetFileNameWithoutExtension(path);
                return Old(dir, name);
            }
            path = Path.GetDirectoryName(path);
        }
        if (!Directory.Exists(path)) throw new ToolError($"no save at {path}");
        var nums = Directory.GetFiles(path, "_main.*.ok").Select(f => Path.GetFileName(f).Split('.'))
            .Where(p => p.Length == 3 && uint.TryParse(p[1], out _)).Select(p => uint.Parse(p[1])).ToList();
        if (nums.Count > 0)
        {
            var s = new SaveInput { Dir = path, Chunked = true, WorldName = Path.GetFileName(path), SaveNumber = nums.Max() };
            s.MetaPath = Path.Combine(path, $"_main.{s.SaveNumber}.fwl2");
            s.DbPath = Path.Combine(path, $"_main.{s.SaveNumber}.db2");
            if (!File.Exists(s.MetaPath)) throw new ToolError($"{s.MetaPath} missing");
            return s;
        }
        var fwls = Directory.GetFiles(path, "*.fwl");
        if (fwls.Length == 1) return Old(path, Path.GetFileNameWithoutExtension(fwls[0]));
        if (fwls.Length > 1) throw new ToolError($"{path} holds several worlds; give the .fwl of the one you want");
        throw new ToolError($"{path} is not a world save (no _main.N.ok of a completed save, and no .fwl)");
    }

    private static SaveInput Old(string dir, string name)
    {
        var s = new SaveInput { Dir = dir, Chunked = false, WorldName = name, MetaPath = Path.Combine(dir, name + ".fwl"), DbPath = Path.Combine(dir, name + ".db") };
        if (!File.Exists(s.MetaPath)) throw new ToolError($"{s.MetaPath} missing");
        if (!File.Exists(s.DbPath)) throw new ToolError($"{s.DbPath} missing");
        return s;
    }

    // copy into <worldsLocal>: a running server saves every few minutes and then deletes the
    // previous save's files, so copy the newest complete save, check its .ok is still there, and
    // start over if a file vanished or a newer save finished meanwhile
    public void CopyTo(string worldsLocal)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (!Chunked)
                {
                    Directory.CreateDirectory(worldsLocal);
                    File.Copy(MetaPath, Path.Combine(worldsLocal, WorldName + ".fwl"), true);
                    File.Copy(DbPath, Path.Combine(worldsLocal, WorldName + ".db"), true);
                    return;
                }
                var fresh = Find(Dir);
                SaveNumber = fresh.SaveNumber; MetaPath = fresh.MetaPath; DbPath = fresh.DbPath;
                string dst = Path.Combine(worldsLocal, WorldName);
                if (Directory.Exists(dst)) Directory.Delete(dst, true);
                Directory.CreateDirectory(dst);
                foreach (var ext in new[] { "fwl2", "db2", "chunks", "ok" })
                    File.Copy(Path.Combine(Dir, $"_main.{SaveNumber}.{ext}"), Path.Combine(dst, $"_main.{SaveNumber}.{ext}"));
                foreach (var f in Directory.GetFiles(Dir, "*.chunk"))
                    File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
                if (!File.Exists(Path.Combine(Dir, $"_main.{SaveNumber}.ok"))) throw new IOException("a newer save replaced it during the copy");
                return;
            }
            catch (IOException e) when (attempt < 5)
            {
                Install.Say($"save copy attempt {attempt} failed ({e.Message}); trying again");
                Thread.Sleep(2000);
            }
        }
    }

    // ---- reading ----
    public sealed class Meta { public string Name, SeedName; public int Seed, FormatVersion, GenVersion = -1; public long Uid; }

    // .fwl / .fwl2: int length, then a package: int format version, string name, string seed name,
    // int seed, long uid, int world-generation version (format 26+), ... (World.SaveWorldFWLData)
    public Meta ReadMeta()
    {
        using var br = new BinaryReader(File.OpenRead(MetaPath), Encoding.UTF8);
        int len = br.ReadInt32();
        using var pr = new BinaryReader(new MemoryStream(br.ReadBytes(len)), Encoding.UTF8);
        var m = new Meta { FormatVersion = pr.ReadInt32() };
        m.Name = pr.ReadString(); m.SeedName = pr.ReadString(); m.Seed = pr.ReadInt32(); m.Uid = pr.ReadInt64();
        if (m.FormatVersion >= 26) m.GenVersion = pr.ReadInt32();
        return m;
    }

    // .db2 (current format): int format version, double game time, then ZoneSystem's block: int size,
    // gzip package of: int zone count, zone ids (2 x int16), int location version, ...
    // (ZNet.SaveWorldThread, ZoneSystem.Save). Older .db files keep it after every object: not read.
    public (int locationVersion, int generatedZones)? ReadLocationVersion()
    {
        if (!Chunked) return null;
        using var br = new BinaryReader(File.OpenRead(DbPath));
        br.ReadInt32(); br.ReadDouble();
        int n = br.ReadInt32();
        using var gz = new GZipStream(new MemoryStream(br.ReadBytes(n)), CompressionMode.Decompress);
        using var pr = new BinaryReader(gz);
        int zones = pr.ReadInt32();
        for (int i = 0; i < zones; i++) pr.ReadInt32();
        return (pr.ReadInt32(), zones);
    }
}

internal static class InfoCommand
{
    public static int Run(Args a)
    {
        string path = a.Get("--save") ?? throw new ToolError("info needs --save PATH");
        var s = SaveInput.Find(path);
        var m = s.ReadMeta();
        Console.WriteLine($"world:                     {m.Name}");
        Console.WriteLine($"save:                      {(s.Chunked ? $"current format, save {s.SaveNumber}" : "older format (.fwl/.db)")} in {s.Dir}");
        Console.WriteLine($"seed:                      {m.SeedName}");
        Console.WriteLine($"world-generation version:  {(m.GenVersion < 0 ? "0 (format before the field)" : m.GenVersion.ToString())}");
        var lv = s.ReadLocationVersion();
        Console.WriteLine($"location version (save):   {(lv.HasValue ? lv.Value.locationVersion.ToString() : "not read from the older format")}");
        if (lv.HasValue) Console.WriteLine($"generated zones:           {lv.Value.generatedZones}");
        if (a.Has("--no-probe")) return 0;
        var inst = Install.From(a);
        if (inst.ManagedServer && !File.Exists(inst.ServerExe))
        {
            Console.WriteLine("location version (game):   (no server installed yet: run 'zonegen setup', or give --server-dir)");
            return 0;
        }
        inst.EnsureAll();
        var probe = Probe.GameInfo(inst);
        Console.WriteLine($"location version (game):   {probe.locationVersion} (Valheim {probe.build}, server build {inst.BuildId() ?? "?"})");
        if (lv.HasValue && lv.Value.locationVersion != probe.locationVersion)
            Console.WriteLine("note: they differ. On its next start this game version plans the world's unplaced locations again, around the placed ones; a copy run here does the same.");
        return 0;
    }
}
