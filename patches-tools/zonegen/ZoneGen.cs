// TASK-4.1 spike: generate a world's ungenerated zones on a dedicated server without a game client,
// write out the ore deposits created, and quit. Fork tooling for the test server, not shipped.
//
// Off unless ZONEGEN_MODE is set. Settings come from environment variables:
//   ZONEGEN_MODE      generate | export   (export: only write the deposits already in the world)
//   ZONEGEN_OUT       output folder (default: BepInEx/zonegen)
//   ZONEGEN_TAG       name for this run's files (default: run)
//   ZONEGEN_LIMIT     generate at most this many zones (0 = all)
//   ZONEGEN_SHARE     k/N: only zones whose index in the list is k mod N (for parallel processes)
//   ZONEGEN_LAND      1 = skip zones that are ocean at the centre and all four corners
//   ZONEGEN_RADIUS    world radius in metres for the zone list (default 10500)
//   ZONEGEN_PLAIN     1 = like Upgrade World: one zone per frame, wait on a refused zone (10 s limit
//                     unless ZONEGEN_STALL_S), no other speed-ups
//   ZONEGEN_BUDGET_MS per frame, keep starting zones until this much time is used (default 0 = one)
//   ZONEGEN_AHEAD     n = ask the heightmap builder for the next n zones ahead of time
//   ZONEGEN_UNCAP     1 = lift the frame-rate cap
//   ZONEGEN_NOSAVE    1 = no world saves while running (and none on quit)
//   ZONEGEN_NODUNGEON 1 = don't build dungeon interiors
//   ZONEGEN_VEGCUT    n = keep only the first n vegetation entries (the ores end at index 93)
//   ZONEGEN_WINDOW    zones in progress at a time outside plain mode (default 8)
//   ZONEGEN_STALL_S   seconds a refused zone is retried before it counts as failed (default 120)
//   ZONEGEN_DRY       1 = only count the zones in the list (and how many are land), then quit
//   ZONEGEN_QUIT      0 = stay running at the end (default: quit)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ZoneGen
{
    [BepInPlugin("patches.zonegen", "ZoneGen (TASK-4.1 spike)", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static string Env(string k, string d = "") => Environment.GetEnvironmentVariable(k) is string v && v.Length > 0 ? v : d;
        private static int EnvInt(string k, int d) => int.TryParse(Env(k), out int v) ? v : d;
        private static bool EnvOn(string k) => Env(k) == "1";

        internal static bool Active, NoSave, NoDungeon;
        private static readonly string[] OrePrefabs = { "rock4_copper", "MineRock_Tin", "silvervein", "MineRock_Obsidian", "mudpile_beacon" };
        private string mode, outDir, runTag;
        private bool started;

        private void Awake()
        {
            mode = Env("ZONEGEN_MODE");
            if (mode != "generate" && mode != "export") return;
            Active = true;
            NoSave = EnvOn("ZONEGEN_NOSAVE");
            NoDungeon = EnvOn("ZONEGEN_NODUNGEON");
            outDir = Env("ZONEGEN_OUT", Path.Combine(Paths.BepInExRootPath, "zonegen"));
            runTag = Env("ZONEGEN_TAG", "run");
            Directory.CreateDirectory(outDir);
            new Harmony("patches.zonegen").PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"ZoneGen: mode {mode}, tag {runTag}, output {outDir}");
        }

        private void Update()
        {
            if (!Active || started) return;
            var zs = ZoneSystem.instance;
            if (zs == null || ZNet.instance == null || WorldGenerator.instance == null || !zs.LocationsGenerated) return;
            started = true;
            StartCoroutine(WhenLoaded(mode == "export" ? ExportOnly() : Generate()));
        }

        // the world's objects load from their chunks after the world is up (about 40 s for a fully
        // generated world): wait until their count has stopped growing for 5 s
        private IEnumerator WhenLoaded(IEnumerator then)
        {
            int last = -1; float stableSince = Time.realtimeSinceStartup;
            while (true)
            {
                int n = ZDOMan.instance.m_objectsByID.Count;
                if (n != last) { last = n; stableSince = Time.realtimeSinceStartup; }
                else if (n > 0 && Time.realtimeSinceStartup - stableSince >= 5f) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Log($"world loaded: {last} objects");
            yield return StartCoroutine(then);
        }

        private IEnumerator ExportOnly()
        {
            yield return null;
            var sw = Stopwatch.StartNew();
            int n = WriteDeposits(null);
            Log($"export: {n} deposits written in {sw.Elapsed.TotalSeconds:F1} s");
            Finish();
        }

        private IEnumerator Generate()
        {
            var zs = ZoneSystem.instance;
            bool plain = EnvOn("ZONEGEN_PLAIN");
            int limit = EnvInt("ZONEGEN_LIMIT", 0), radius = EnvInt("ZONEGEN_RADIUS", 10500);
            int budgetMs = plain ? 0 : EnvInt("ZONEGEN_BUDGET_MS", 0), ahead = plain ? 0 : EnvInt("ZONEGEN_AHEAD", 0);
            int stallS = EnvInt("ZONEGEN_STALL_S", plain ? 10 : 120), vegCut = EnvInt("ZONEGEN_VEGCUT", 0);
            bool land = EnvOn("ZONEGEN_LAND");
            int shareK = 0, shareN = 1;
            var share = Env("ZONEGEN_SHARE").Split('/');
            if (share.Length == 2) { int.TryParse(share[0], out shareK); int.TryParse(share[1], out shareN); if (shareN < 1) shareN = 1; }

            if (EnvOn("ZONEGEN_UNCAP")) { Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; }
            if (vegCut > 0 && vegCut < zs.m_vegetation.Count) zs.m_vegetation.RemoveRange(vegCut, zs.m_vegetation.Count - vegCut);

            // the zone list: every ungenerated zone within the radius, nearest the centre first
            var list = new List<Vector2s>();
            int rz = radius / 64 + 1;
            for (int y = -rz; y <= rz; y++)
                for (int x = -rz; x <= rz; x++)
                {
                    var id = new Vector2s(x, y);
                    var c = ZoneSystem.GetZonePos(id);
                    if (c.x * c.x + c.z * c.z > (float)radius * radius) continue;
                    if (zs.IsZoneGenerated(id)) continue;
                    if (land && IsOcean(c)) continue;
                    list.Add(id);
                }
            list.Sort((a, b) => (a.x * a.x + a.y * a.y).CompareTo(b.x * b.x + b.y * b.y));
            if (shareN > 1) list = list.Where((z, i) => i % shareN == shareK).ToList();
            if (limit > 0 && list.Count > limit) list = list.GetRange(0, limit);
            if (EnvOn("ZONEGEN_DRY")) { int landN = list.Count(z => !IsOcean(ZoneSystem.GetZonePos(z))); Log($"dry: {list.Count} zones in the list, {landN} of them land"); Finish(); yield break; }
            var runZones = new HashSet<Vector2s>(list);
            Log($"generate: {list.Count} zones (plain {plain}, budget {budgetMs} ms, ahead {ahead}, uncap {EnvOn("ZONEGEN_UNCAP")}, nosave {NoSave}, nodungeon {NoDungeon}, vegcut {vegCut}, land {land}, share {shareK}/{shareN}, stall {stallS} s, window {(plain ? 1 : Math.Max(1, EnvInt("ZONEGEN_WINDOW", 8)))})");

            var hm = zs.m_zonePrefab.GetComponentInChildren<Heightmap>();
            var total = Stopwatch.StartNew();
            var since = Stopwatch.StartNew();
            int done = 0, failed = 0, frames = 0, doneAtReport = 0;
            var pending = new Queue<Vector2s>(list);
            var refusedSince = new Dictionary<Vector2s, float>();
            int aheadIndex = 0;
            var listArr = list.ToArray();

            int window = plain ? 1 : Math.Max(1, EnvInt("ZONEGEN_WINDOW", 8));
            var active = new List<Vector2s>();
            while (pending.Count > 0 || active.Count > 0)
            {
                frames++;
                var frame = Stopwatch.StartNew();
                if (ahead > 0)
                {
                    // queue terrain for the next zones (the builder keeps up to 16 ready)
                    int target = Math.Min(listArr.Length, done + failed + ahead);
                    for (; aheadIndex < target; aheadIndex++)
                        HeightmapBuilder.instance.IsTerrainReady(ZoneSystem.GetZonePos(listArr[aheadIndex]), hm.m_width, hm.m_scale, hm.IsDistantLod, WorldGenerator.instance);
                }
                // zones in progress: at most `window` at a time (plain: 1), so the heightmap builder,
                // which keeps only 16 finished heightmaps, isn't asked for hundreds at once (that
                // thrashed: finished heightmaps were dropped before their zone came round again)
                while (active.Count < window && pending.Count > 0) active.Add(pending.Dequeue());
                for (int i = 0; i < active.Count; )
                {
                    var z = active[i];
                    bool ok = zs.IsZoneGenerated(z) || zs.SpawnZone(z, ZoneSystem.SpawnMode.Ghost, out GameObject _);
                    if (ok)
                    {
                        active.RemoveAt(i); refusedSince.Remove(z); done++;
                        if (pending.Count > 0) active.Add(pending.Dequeue());
                    }
                    else
                    {
                        if (!refusedSince.TryGetValue(z, out float t0)) refusedSince[z] = t0 = Time.realtimeSinceStartup;
                        if (Time.realtimeSinceStartup - t0 > stallS) { active.RemoveAt(i); refusedSince.Remove(z); failed++; Log($"failed zone {z} after {Time.realtimeSinceStartup - t0:F0} s"); if (pending.Count > 0) active.Add(pending.Dequeue()); }
                        else i++;
                    }
                    if (budgetMs <= 0 || frame.ElapsedMilliseconds >= budgetMs) break;
                }

                if (since.Elapsed.TotalSeconds >= 30)
                {
                    double rate = (done - doneAtReport) / since.Elapsed.TotalMinutes;
                    Log($"progress: {done}/{list.Count} done, {failed} failed, {rate:F0} zones/min recent, {done / total.Elapsed.TotalMinutes:F0} avg, {frames / since.Elapsed.TotalSeconds:F0} fps, {Mem()}");
                    doneAtReport = done; frames = 0; since.Restart();
                }
                yield return null;
            }
            double mins = total.Elapsed.TotalMinutes;
            Log($"generate done: {done} zones, {failed} failed, in {total.Elapsed.TotalSeconds:F0} s = {(done / Math.Max(mins, 1e-6)):F0} zones/min, {Mem()}");
            var sw = Stopwatch.StartNew();
            int n = WriteDeposits(runZones);
            Log($"export: {n} deposits written in {sw.Elapsed.TotalSeconds:F1} s");
            File.WriteAllText(Path.Combine(outDir, runTag + "-summary.txt"),
                $"tag={runTag}\nzones={list.Count}\ndone={done}\nfailed={failed}\nseconds={total.Elapsed.TotalSeconds:F0}\nzones_per_min={(done / Math.Max(mins, 1e-6)):F0}\ndeposits={n}\n{Mem()}\n");
            Finish();
        }

        private static bool IsOcean(Vector3 c)
        {
            var wg = WorldGenerator.instance;
            float h = 32f;
            return wg.GetBiome(c.x, c.z) == Heightmap.Biome.Ocean && wg.GetBiome(c.x - h, c.z - h) == Heightmap.Biome.Ocean
                && wg.GetBiome(c.x + h, c.z - h) == Heightmap.Biome.Ocean && wg.GetBiome(c.x - h, c.z + h) == Heightmap.Biome.Ocean
                && wg.GetBiome(c.x + h, c.z + h) == Heightmap.Biome.Ocean;
        }

        // ore deposits as CSV: prefab, x, y, z, zone x, zone y (zones: null = all)
        private int WriteDeposits(HashSet<Vector2s> zones)
        {
            var hashes = OrePrefabs.ToDictionary(p => p.GetStableHashCode(), p => p);
            var sb = new StringBuilder("prefab,x,y,z,zx,zy\n");
            int n = 0;
            foreach (var zdo in ZDOMan.instance.m_objectsByID.Values)
            {
                if (!hashes.TryGetValue(zdo.GetPrefab(), out string name)) continue;
                var p = zdo.GetPosition();
                var zone = ZoneSystem.GetZone(p);
                if (zones != null && !zones.Contains(zone)) continue;
                sb.Append(name).Append(',').Append(p.x.ToString("F2")).Append(',').Append(p.y.ToString("F2")).Append(',').Append(p.z.ToString("F2"))
                  .Append(',').Append(zone.x).Append(',').Append(zone.y).Append('\n');
                n++;
            }
            File.WriteAllText(Path.Combine(outDir, runTag + "-deposits.csv"), sb.ToString());
            return n;
        }

        private static string Mem()
        {
            var p = Process.GetCurrentProcess();
            p.Refresh();
            return $"working set {p.WorkingSet64 / (1024 * 1024)} MB, managed {GC.GetTotalMemory(false) / (1024 * 1024)} MB";
        }

        private void Log(string s) => Logger.LogInfo("ZoneGen: " + s);

        private void Finish()
        {
            if (Env("ZONEGEN_QUIT", "1") == "1") { Log("quitting"); Application.Quit(); }
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Save))]
    internal static class NoSavePatch
    {
        private static bool Prefix() => !(Plugin.Active && Plugin.NoSave);
    }

    [HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.Generate), new[] { typeof(ZoneSystem.SpawnMode) })]
    internal static class NoDungeonPatch
    {
        private static bool Prefix() => !(Plugin.Active && Plugin.NoDungeon);
    }
}
