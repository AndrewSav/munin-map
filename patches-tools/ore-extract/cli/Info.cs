// ore-extract info: a save's seed and versions, and the game's location version.
using OreExtract.Core;

namespace OreExtractCli;

internal static class InfoCommand
{
    public static int Run(Args a)
    {
        string path = a.Get("--save") ?? throw new ToolError("info needs --save PATH");
        var s = SaveInput.Find(path);
        var m = s.ReadMeta();
        Console.WriteLine($"world:                     {m.Name}");
        Console.WriteLine($"save:                      save {s.SaveNumber} in {s.Dir}");
        Console.WriteLine($"seed:                      {m.SeedName}");
        Console.WriteLine($"world-generation version:  {(m.GenVersion < 0 ? "0 (format before the field)" : m.GenVersion.ToString())}");
        var lv = s.ReadLocationVersion();
        Console.WriteLine($"location version (save):   {lv.Value.locationVersion}");
        Console.WriteLine($"generated zones:           {lv.Value.generatedZones}");
        if (a.Has("--no-probe")) return 0;
        var inst = Install.From(a);
        if (inst.ManagedServer && !File.Exists(inst.ServerExe))
        {
            Console.WriteLine("location version (game):   (no server installed yet: run 'ore-extract setup', or give --server-dir)");
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
