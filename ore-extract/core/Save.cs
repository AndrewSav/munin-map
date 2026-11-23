// A world save given to the tool: found, read (seed, versions) and copied. The original files are
// only ever read.
using System.IO.Compression;
using System.Text;

namespace OreExtract.Core;

public sealed class SaveInput
{
    public string WorldName, Dir;
    public uint SaveNumber;          // the newest complete save (its .ok exists)
    public string MetaPath, DbPath;

    // PATH: a world folder of the current format, or any file in it. The game only ever saves in
    // this format and converts an older .fwl/.db world when it loads it, so the older format isn't
    // taken: one start of the current server converts it.
    public static SaveInput Find(string path)
    {
        path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (path.Length == 0) path = "/";
        if (File.Exists(path))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".fwl" || ext == ".db") throw new ToolError(OldFormat);
            path = Path.GetDirectoryName(path);
        }
        if (!Directory.Exists(path)) throw new ToolError($"no save at {path}");
        var nums = Directory.GetFiles(path, "_main.*.ok").Select(f => Path.GetFileName(f).Split('.'))
            .Where(p => p.Length == 3 && uint.TryParse(p[1], out _)).Select(p => uint.Parse(p[1])).ToList();
        if (nums.Count > 0)
        {
            var s = new SaveInput { Dir = path, WorldName = Path.GetFileName(path), SaveNumber = nums.Max() };
            s.MetaPath = Path.Combine(path, $"_main.{s.SaveNumber}.fwl2");
            s.DbPath = Path.Combine(path, $"_main.{s.SaveNumber}.db2");
            if (!File.Exists(s.MetaPath)) throw new ToolError($"{s.MetaPath} missing");
            return s;
        }
        if (Directory.GetFiles(path, "*.fwl").Length > 0) throw new ToolError(OldFormat);
        throw new ToolError($"{path} is not a world save (no _main.N.ok of a completed save in it; give the world's own folder, worlds_local/<world>)");
    }

    private const string OldFormat = "an older-format save (.fwl/.db): start the current game's dedicated server on it once, which saves it in the current format, then give that world folder";

    // copy into <worldsLocal>: a running server saves every few minutes and then deletes the
    // previous save's files, so copy the newest complete save, check its .ok is still there, and
    // start over if a file vanished or a newer save finished meanwhile
    public void CopyTo(string worldsLocal)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
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

    // .fwl2: int length, then a package: int format version, string name, string seed name,
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
    // (ZNet.SaveWorldThread, ZoneSystem.Save)
    public (int locationVersion, int generatedZones)? ReadLocationVersion()
    {
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
