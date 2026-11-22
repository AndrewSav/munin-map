// OreExtract: generate a world's ungenerated zones on a dedicated server without a game client, write
// out the objects of chosen prefabs (ore deposits by default) that generation created, and quit.
// Part of the ore-extract tool (TASK-4.1 spike, TASK-4.2 tool, shipped with releases since TASK-4.3);
// not part of the Munin plugin. The ore-extract CLI (../cli) starts the server with this plugin and
// nothing else; patches-tools/ore-extract/run.ps1 and bench.ps1 drive it in the test image.
//
// Off unless OREEXTRACT_MODE is set. Settings come from environment variables:
//   OREEXTRACT_MODE      generate | export | probe
//                     export: only write the matching objects already in the world
//                     probe: log the game build and location version as soon as ZoneSystem wakes, quit
//   OREEXTRACT_OUT       output folder (default: BepInEx/ore-extract)
//   OREEXTRACT_TAG       name for this run's files (default: run)
//   OREEXTRACT_PREFABS   comma-separated prefab names to export (default: the five ore deposits)
//   OREEXTRACT_SEED      create the world from this seed name if it has no save (no effect on a save)
//   OREEXTRACT_GENVER    world-generation version for a world created from OREEXTRACT_SEED (default 2)
//   OREEXTRACT_LOCATIONS 1 = also write the location plan as loaded (<tag>-locations.csv)
//   OREEXTRACT_SKIP      file of "zx,zy" lines: zones to leave out (written by an earlier run; resume)
//   OREEXTRACT_DROP      1 = release the objects of zones once written (memory stays near the base)
//   OREEXTRACT_UNLOAD    n = call Resources.UnloadUnusedAssets every n zones (0 = never)
//   OREEXTRACT_LIMIT     generate at most this many zones (0 = all)
//   OREEXTRACT_SHARE     k/N: only zones whose index in the list is k mod N (for parallel processes)
//   OREEXTRACT_LAND      1 = skip zones that are ocean at the centre and all four corners
//   OREEXTRACT_RADIUS    world radius in metres for the zone list (default 10500)
//   OREEXTRACT_PLAIN     1 = like Upgrade World: one zone per frame, wait on a refused zone (10 s limit
//                     unless OREEXTRACT_STALL_S), no other speed-ups
//   OREEXTRACT_BUDGET_MS per frame, keep starting zones until this much time is used (default 0 = one)
//   OREEXTRACT_AHEAD     n = ask the heightmap builder for the next n zones ahead of time
//   OREEXTRACT_UNCAP     1 = lift the frame-rate cap
//   OREEXTRACT_NOSAVE    1 = no world saves while running (and none on quit)
//   OREEXTRACT_NODUNGEON 1 = don't build dungeon interiors
//   OREEXTRACT_VEGCUT    n = keep only the first n vegetation entries; "auto" = cut after the last entry
//                     whose prefab is exported (no cut if an exported prefab isn't vegetation)
//   OREEXTRACT_WINDOW    zones in progress at a time outside plain mode (default 8)
//   OREEXTRACT_STALL_S   seconds a refused zone is retried before it counts as failed (default 120)
//   OREEXTRACT_DRY       1 = only count the zones in the list (and how many are land), then quit
//   OREEXTRACT_QUIT      0 = stay running at the end (default: quit)
//
// Besides the BepInEx log, every line is appended to <out>/<tag>.log. Lines starting with "@" are
// for the CLI: "@info k=v ...", "@progress k=v ...", "@done k=v ...", "@error message".
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace OreExtract
{
    [BepInPlugin("patches.oreextract", "OreExtract", "0.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static string Env(string k, string d = "") => Environment.GetEnvironmentVariable(k) is string v && v.Length > 0 ? v : d;
        private static int EnvInt(string k, int d) => int.TryParse(Env(k), out int v) ? v : d;
        private static bool EnvOn(string k) => Env(k) == "1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool Active, NoSave, NoDungeon;
        internal static string Mode;
        internal static bool CreatedFromSeed, Replanned;
        internal static Plugin Instance;
        private static readonly string[] DefaultPrefabs = { "rock4_copper", "MineRock_Tin", "silvervein", "MineRock_Obsidian", "mudpile_beacon" };
        private string[] prefabs;
        private string outDir, runTag, logPath;
        private bool started;

        private void Awake()
        {
            Mode = Env("OREEXTRACT_MODE");
            if (Mode != "generate" && Mode != "export" && Mode != "probe") return;
            Instance = this;
            Active = true;
            NoSave = EnvOn("OREEXTRACT_NOSAVE") || Mode == "probe";
            NoDungeon = EnvOn("OREEXTRACT_NODUNGEON");
            outDir = Env("OREEXTRACT_OUT", Path.Combine(Paths.BepInExRootPath, "ore-extract"));
            runTag = Env("OREEXTRACT_TAG", "run");
            prefabs = Env("OREEXTRACT_PREFABS").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (prefabs.Length == 0) prefabs = DefaultPrefabs;
            Directory.CreateDirectory(outDir);
            logPath = Path.Combine(outDir, runTag + ".log");
            // a game version whose code no longer has what this plugin patches or uses fails here
            // or on the first frame: report it instead of leaving an idle server behind
            try { new Harmony("patches.oreextract").PatchAll(typeof(Plugin).Assembly); }
            catch (Exception e) { Fail("patching the game failed (game version not supported by this build?)", e); return; }
            Log($"mode {Mode}, tag {runTag}, output {outDir}, prefabs {string.Join(",", prefabs)}");
        }

        private void Fail(string what, Exception e)
        {
            Log("@error " + what + ": " + e.GetType().Name + ": " + e.Message.Replace('\n', ' '));
            Logger.LogError(e);
            Active = false;
            Application.Quit();
        }

        private float parentCheckAt;

        private void Update()
        {
            if (Active && Time.realtimeSinceStartup >= parentCheckAt) { parentCheckAt = Time.realtimeSinceStartup + 5f; CheckParent(); }
            if (!Active || started || Mode == "probe") return;
            // in its own method: a member missing in this game version throws when that method is
            // compiled, which happens at the call, inside this try
            try { Tick(); }
            catch (Exception e) { started = true; Fail("starting the run failed (game version not supported by this build?)", e); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void Tick()
        {
            var zs = ZoneSystem.instance;
            if (zs == null || ZNet.instance == null || WorldGenerator.instance == null || !zs.LocationsGenerated) return;
            started = true;
            var w = ZNet.World;
            Log($"@info build={(global::Version.CurrentVersion)} locver={zs.m_locationVersion} seedname={Q(w.m_seedName)} seed={w.m_seed} genver={w.m_worldGenVersion} fromseed={(CreatedFromSeed ? 1 : 0)} replanned={(Replanned ? 1 : 0)} generated={zs.m_generatedZones.Count}");
            if (EnvOn("OREEXTRACT_LOCATIONS")) WriteLocations(zs);
            StartCoroutine(Guard(WhenLoaded(Mode == "export" ? ExportOnly() : Generate())));
        }

        // OREEXTRACT_PARENT_PID: the ore-extract tool that started this server. On Linux nothing ends a child
        // when its parent dies (killed, or its SSH/docker exec session dropped), and the memory guard
        // lives in the tool, so a server whose tool is gone quits by itself; what it wrote stays for a
        // resume. (On Windows the tool's job object ends its servers anyway.)
        private void CheckParent()
        {
            if (!int.TryParse(Env("OREEXTRACT_PARENT_PID"), out int pid) || Application.platform != RuntimePlatform.LinuxServer && Application.platform != RuntimePlatform.LinuxPlayer) return;
            bool alive;
            try { alive = File.Exists($"/proc/{pid}/comm") && File.ReadAllText($"/proc/{pid}/comm").Trim().StartsWith("ore-extract"); }
            catch { alive = false; }
            if (alive) return;
            Log($"@error the ore-extract tool (pid {pid}) is gone; stopping (resume the run to continue)");
            Active = false;
            Application.Quit();
        }

        private static string Q(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";

        // any exception in the run ends it with an @error line instead of a server left idling
        private IEnumerator Guard(IEnumerator inner)
        {
            while (true)
            {
                object cur;
                try { if (!inner.MoveNext()) yield break; cur = inner.Current; }
                catch (Exception e) { Log("@error " + e.GetType().Name + ": " + e.Message.Replace('\n', ' ')); Logger.LogError(e); Finish(); yield break; }
                yield return cur;
            }
        }

        // the world's objects load from their chunks after the world is up (about 40 s for a fully
        // generated world): wait until their count has stopped growing for 5 s. A world just created
        // from a seed has none.
        private IEnumerator WhenLoaded(IEnumerator then)
        {
            int last = -1; float stableSince = Time.realtimeSinceStartup;
            while (true)
            {
                int n = ZDOMan.instance.m_objectsByID.Count;
                if (n != last) { last = n; stableSince = Time.realtimeSinceStartup; }
                else if ((n > 0 || CreatedFromSeed) && Time.realtimeSinceStartup - stableSince >= 5f) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Log($"world loaded: {last} objects");
            yield return then;
        }

        private IEnumerator ExportOnly()
        {
            yield return null;
            var sw = Stopwatch.StartNew();
            int n = WriteDeposits(null);
            Log($"@done zones=0 failed=0 seconds=0 deposits={n} {MemKv()}");
            Finish();
        }

        private IEnumerator Generate()
        {
            var zs = ZoneSystem.instance;
            bool plain = EnvOn("OREEXTRACT_PLAIN");
            int limit = EnvInt("OREEXTRACT_LIMIT", 0), radius = EnvInt("OREEXTRACT_RADIUS", 10500);
            int budgetMs = plain ? 0 : EnvInt("OREEXTRACT_BUDGET_MS", 0), ahead = plain ? 0 : EnvInt("OREEXTRACT_AHEAD", 0);
            int stallS = EnvInt("OREEXTRACT_STALL_S", plain ? 10 : 120);
            bool land = EnvOn("OREEXTRACT_LAND");
            int shareK = 0, shareN = 1;
            var share = Env("OREEXTRACT_SHARE").Split('/');
            if (share.Length == 2) { int.TryParse(share[0], out shareK); int.TryParse(share[1], out shareN); if (shareN < 1) shareN = 1; }

            if (EnvOn("OREEXTRACT_UNCAP")) { Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; }
            int vegCut = VegCut(zs);
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
            // resume: zones an earlier run already wrote are left out (and never generated here)
            string skipPath = Env("OREEXTRACT_SKIP");
            if (skipPath.Length > 0 && File.Exists(skipPath))
            {
                var skip = new HashSet<Vector2s>();
                foreach (var line in File.ReadAllLines(skipPath))
                {
                    var p = line.Split(',');
                    if (p.Length == 2 && int.TryParse(p[0], out int zx) && int.TryParse(p[1], out int zy)) skip.Add(new Vector2s(zx, zy));
                }
                int before = list.Count;
                list = list.Where(z => !skip.Contains(z)).ToList();
                Log($"resume: {before - list.Count} zones already written, skipped");
            }
            if (EnvOn("OREEXTRACT_DRY")) { int landN = list.Count(z => !IsOcean(ZoneSystem.GetZonePos(z))); Log($"dry: {list.Count} zones in the list, {landN} of them land"); Log($"@done zones={list.Count} failed=0 seconds=0 deposits=0 {MemKv()}"); Finish(); yield break; }
            var settler = new Settler(list, prefabs, outDir, runTag, EnvOn("OREEXTRACT_DROP"));
            int window = plain ? 1 : Math.Max(1, EnvInt("OREEXTRACT_WINDOW", 8));
            Log($"generate: {list.Count} zones (plain {plain}, budget {budgetMs} ms, ahead {ahead}, uncap {EnvOn("OREEXTRACT_UNCAP")}, nosave {NoSave}, nodungeon {NoDungeon}, drop {EnvOn("OREEXTRACT_DROP")}, vegcut {vegCut}, land {land}, share {shareK}/{shareN}, stall {stallS} s, window {window})");
            Log($"@progress done=0 total={list.Count} failed=0 rate=0 {MemKv()}");

            var hm = zs.m_zonePrefab.GetComponentInChildren<Heightmap>();
            var total = Stopwatch.StartNew();
            var since = Stopwatch.StartNew();
            int done = 0, failed = 0, frames = 0, doneAtReport = 0;
            var pending = new Queue<Vector2s>(list);
            var refusedSince = new Dictionary<Vector2s, float>();
            int aheadIndex = 0;
            var listArr = list.ToArray();

            int unloadEvery = EnvInt("OREEXTRACT_UNLOAD", 0), unloadAt = unloadEvery;
            var active = new List<Vector2s>();
            while (pending.Count > 0 || active.Count > 0)
            {
                if (unloadEvery > 0 && done + failed >= unloadAt)
                {
                    unloadAt += unloadEvery;
                    var us = Stopwatch.StartNew();
                    yield return Resources.UnloadUnusedAssets();
                    Log($"unload unused assets at {done} zones: {us.ElapsedMilliseconds} ms");
                }
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
                int finishedBefore = done + failed;
                for (int i = 0; i < active.Count; )
                {
                    var z = active[i];
                    bool ok = zs.IsZoneGenerated(z) || zs.SpawnZone(z, ZoneSystem.SpawnMode.Ghost, out GameObject _);
                    if (ok)
                    {
                        active.RemoveAt(i); refusedSince.Remove(z); done++;
                        settler.Done(z);
                        if (pending.Count > 0) active.Add(pending.Dequeue());
                    }
                    else
                    {
                        if (!refusedSince.TryGetValue(z, out float t0)) refusedSince[z] = t0 = Time.realtimeSinceStartup;
                        if (Time.realtimeSinceStartup - t0 > stallS) { active.RemoveAt(i); refusedSince.Remove(z); failed++; settler.Done(z); Log($"failed zone {z} after {Time.realtimeSinceStartup - t0:F0} s"); if (pending.Count > 0) active.Add(pending.Dequeue()); }
                        else i++;
                    }
                    if (budgetMs <= 0 || frame.ElapsedMilliseconds >= budgetMs) break;
                }
                // every zone in progress is waiting for its terrain: let the heightmap builder's
                // thread run instead of spinning. Uncapped on one core at idle priority, the spinning
                // main thread starved it on Windows, which schedules strictly by thread priority
                // (TASK-4.2: 100 zones/min and zones failing after 120 s)
                if (!plain && done + failed == finishedBefore && active.Count > 0) System.Threading.Thread.Sleep(2);

                if (since.Elapsed.TotalSeconds >= 30)
                {
                    double rate = (done - doneAtReport) / since.Elapsed.TotalMinutes;
                    Log($"progress: {done}/{list.Count} done, {failed} failed, {rate:F0} zones/min recent, {done / total.Elapsed.TotalMinutes:F0} avg, {frames / since.Elapsed.TotalSeconds:F0} fps, {Mem()}");
                    Log($"@progress done={done} total={list.Count} failed={failed} rate={rate.ToString("F0", Inv)} written={settler.SettledZones} dropped={settler.Dropped} {MemKv()}");
                    doneAtReport = done; frames = 0; since.Restart();
                }
                yield return null;
            }
            double mins = total.Elapsed.TotalMinutes;
            Log($"generate done: {done} zones, {failed} failed, in {total.Elapsed.TotalSeconds:F0} s = {(done / Math.Max(mins, 1e-6)):F0} zones/min, {Mem()}");
            settler.Finish();
            int n = settler.Written;
            File.WriteAllText(Path.Combine(outDir, runTag + "-summary.txt"),
                $"tag={runTag}\nzones={list.Count}\ndone={done}\nfailed={failed}\nseconds={total.Elapsed.TotalSeconds:F0}\nzones_per_min={(done / Math.Max(mins, 1e-6)):F0}\ndeposits={n}\n{Mem()}\n");
            Log($"@done zones={done} failed={failed} seconds={total.Elapsed.TotalSeconds.ToString("F0", Inv)} deposits={n} {MemKv()}");
            Finish();
        }

        // OREEXTRACT_VEGCUT: a number, or "auto" for just after the last vegetation entry that places an
        // exported prefab. Each entry reseeds itself and only earlier entries' colliders affect it,
        // so dropping the later ones doesn't change the exported objects (doc-2).
        private int VegCut(ZoneSystem zs)
        {
            string v = Env("OREEXTRACT_VEGCUT");
            if (v != "auto") return int.TryParse(v, out int n) ? n : 0;
            var want = new HashSet<string>(prefabs);
            var found = new HashSet<string>();
            int last = -1;
            for (int i = 0; i < zs.m_vegetation.Count; i++)
            {
                var veg = zs.m_vegetation[i];
                if (veg?.m_prefab == null || !want.Contains(veg.m_prefab.name)) continue;
                found.Add(veg.m_prefab.name);
                last = i;
            }
            if (found.Count < want.Count) { Log($"vegcut auto: not all exported prefabs are vegetation ({string.Join(",", want.Except(found))}), no cut"); return 0; }
            Log($"vegcut auto: {last + 1} of {zs.m_vegetation.Count} entries kept");
            return last + 1;
        }

        private static bool IsOcean(Vector3 c)
        {
            var wg = WorldGenerator.instance;
            float h = 32f;
            return wg.GetBiome(c.x, c.z) == Heightmap.Biome.Ocean && wg.GetBiome(c.x - h, c.z - h) == Heightmap.Biome.Ocean
                && wg.GetBiome(c.x + h, c.z - h) == Heightmap.Biome.Ocean && wg.GetBiome(c.x - h, c.z + h) == Heightmap.Biome.Ocean
                && wg.GetBiome(c.x + h, c.z + h) == Heightmap.Biome.Ocean;
        }

        // matching objects as CSV: prefab, x, y, z, zone x, zone y (zones: null = all)
        private int WriteDeposits(HashSet<Vector2s> zones)
        {
            var hashes = new Dictionary<int, string>();
            foreach (var p in prefabs) hashes[p.GetStableHashCode()] = p;
            var sb = new StringBuilder("prefab,x,y,z,zx,zy\n");
            int n = 0;
            foreach (var zdo in ZDOMan.instance.m_objectsByID.Values)
            {
                if (!hashes.TryGetValue(zdo.GetPrefab(), out string name)) continue;
                var p = zdo.GetPosition();
                var zone = ZoneSystem.GetZone(p);
                if (zones != null && !zones.Contains(zone)) continue;
                sb.Append(name).Append(',').Append(p.x.ToString("F2", Inv)).Append(',').Append(p.y.ToString("F2", Inv)).Append(',').Append(p.z.ToString("F2", Inv))
                  .Append(',').Append(zone.x).Append(',').Append(zone.y).Append('\n');
                n++;
            }
            File.WriteAllText(Path.Combine(outDir, runTag + "-deposits.csv"), sb.ToString());
            return n;
        }

        // OREEXTRACT_LOCATIONS=1: the world's location plan as loaded (and re-planned if it was), before
        // any generation: prefab, x, y, z, placed
        private void WriteLocations(ZoneSystem zs)
        {
            var sb = new StringBuilder("prefab,x,y,z,placed\n");
            foreach (var li in zs.m_locationInstances.Values)
                sb.Append(li.m_location.m_prefabName).Append(',').Append(li.m_position.x.ToString("F2", Inv)).Append(',').Append(li.m_position.y.ToString("F2", Inv))
                  .Append(',').Append(li.m_position.z.ToString("F2", Inv)).Append(',').Append(li.m_placed ? 1 : 0).Append('\n');
            File.WriteAllText(Path.Combine(outDir, runTag + "-locations.csv"), sb.ToString());
            Log($"locations: {zs.m_locationInstances.Count} written");
        }

        private static string Mem()
        {
            var p = Process.GetCurrentProcess();
            p.Refresh();
            return $"working set {p.WorkingSet64 / (1024 * 1024)} MB, managed {GC.GetTotalMemory(false) / (1024 * 1024)} MB";
        }

        private static string MemKv()
        {
            var p = Process.GetCurrentProcess();
            p.Refresh();
            return $"ws_mb={p.WorkingSet64 / (1024 * 1024)} managed_mb={GC.GetTotalMemory(false) / (1024 * 1024)}";
        }

        internal void Log(string s)
        {
            Logger.LogInfo("OreExtract: " + s);
            if (logPath == null) return;
            try { File.AppendAllText(logPath, DateTime.UtcNow.ToString("HH:mm:ss", Inv) + " " + s + "\n"); } catch { }
        }

        internal void Finish()
        {
            if (Env("OREEXTRACT_QUIT", "1") == "1") { Log("quitting"); Application.Quit(); }
        }
    }

    // Writes a zone's matching objects once the zone and its eight neighbours are done here (a
    // neighbour that isn't in this process's list is never generated here, so it counts as done):
    // objects a neighbour's generation places across the border are in by then, and nothing still
    // to be generated here is next to it. Rows go to <tag>-deposits.csv, then the zone to
    // <tag>-zones.txt, both flushed, so a stopped run keeps every zone listed there (rows of a zone
    // missing from the list are dropped on resume). With drop on, a written zone's objects are then
    // released from memory.
    internal sealed class Settler
    {
        private readonly HashSet<Vector2s> mine, done = new HashSet<Vector2s>(), settled = new HashSet<Vector2s>();
        private readonly Dictionary<int, string> hashes = new Dictionary<int, string>();
        private readonly StreamWriter rows, zones;
        private readonly bool drop;
        public int Written, SettledZones, Dropped;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public Settler(List<Vector2s> list, string[] prefabs, string outDir, string tag, bool drop)
        {
            this.drop = drop;
            mine = new HashSet<Vector2s>(list);
            foreach (var p in prefabs) hashes[p.GetStableHashCode()] = p;
            string csv = Path.Combine(outDir, tag + "-deposits.csv");
            bool fresh = !File.Exists(csv);
            rows = new StreamWriter(csv, true, new UTF8Encoding(false));
            if (fresh) rows.Write("prefab,x,y,z,zx,zy\n");
            rows.Flush();
            zones = new StreamWriter(Path.Combine(outDir, tag + "-zones.txt"), true, new UTF8Encoding(false));
        }

        private bool IsDone(Vector2s z) => !mine.Contains(z) || done.Contains(z);

        public void Done(Vector2s z)
        {
            done.Add(z);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = new Vector2s(z.x + dx, z.y + dy);
                    if (!mine.Contains(n) || settled.Contains(n) || !done.Contains(n)) continue;
                    bool ready = true;
                    for (int ey = -1; ey <= 1 && ready; ey++)
                        for (int ex = -1; ex <= 1 && ready; ex++)
                            if (!IsDone(new Vector2s(n.x + ex, n.y + ey))) ready = false;
                    if (ready) Settle(n);
                }
        }

        // every zone left (all done or failed by the end)
        public void Finish()
        {
            foreach (var z in mine.ToList()) if (!settled.Contains(z)) Settle(z);
            rows.Dispose(); zones.Dispose();
        }

        private void Settle(Vector2s z)
        {
            settled.Add(z);
            var idx = ZoneSystem.SectorToIndex(z);
            var list = ZDOMan.instance.m_objectsBySector[idx.Sector];
            if (list != null)
                foreach (var zdo in list)
                {
                    if (!hashes.TryGetValue(zdo.GetPrefab(), out string name)) continue;
                    var p = zdo.GetPosition();
                    rows.Write(name); rows.Write(','); rows.Write(p.x.ToString("F2", Inv)); rows.Write(','); rows.Write(p.y.ToString("F2", Inv)); rows.Write(',');
                    rows.Write(p.z.ToString("F2", Inv)); rows.Write(','); rows.Write(z.x); rows.Write(','); rows.Write(z.y); rows.Write('\n');
                    Written++;
                }
            rows.Flush();
            zones.Write(z.x); zones.Write(','); zones.Write(z.y); zones.Write('\n');
            zones.Flush();
            SettledZones++;
            if (drop && list != null)
            {
                // what ZDOMan.HandleDestroyedZDO does, without the network bookkeeping (no peers here)
                var objs = ZDOMan.instance.m_objectsByID;
                foreach (var zdo in list.ToArray())
                {
                    objs.Remove(zdo.m_uid);
                    ZDOPool.Release(zdo);
                    Dropped++;
                }
                list.Clear();
            }
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

    // probe: the location version is set by the game's scene, not the code (1 in the code, 32 in
    // 1.0.21), so read it from the live component as soon as it exists, and quit before any world loads
    [HarmonyPatch(typeof(ZoneSystem), "Awake")]
    internal static class ProbePatch
    {
        private static void Postfix(ZoneSystem __instance)
        {
            if (!Plugin.Active || Plugin.Mode != "probe") return;
            Plugin.Instance.Log($"@info build={(global::Version.CurrentVersion)} locver={__instance.m_locationVersion}");
            Plugin.Instance.Log("@done zones=0 failed=0 seconds=0 deposits=0");
            Application.Quit();
        }
    }

    // OREEXTRACT_SEED: the server has no seed switch, so a world it would create with a random seed is
    // created from the given seed name and world-generation version instead. An existing save wins.
    [HarmonyPatch(typeof(World), nameof(World.GetCreateWorld))]
    internal static class SeedPatch
    {
        private static bool Prefix(string name, FileHelpers.FileSource source, ref World __result)
        {
            string seed = Plugin.Env("OREEXTRACT_SEED");
            if (!Plugin.Active || seed.Length == 0) return true;
            if (SaveSystem.TryGetSaveByName(name, SaveDataType.World, out var save) && !save.IsDeleted) return true;
            var world = new World(name, seed);
            if (int.TryParse(Plugin.Env("OREEXTRACT_GENVER"), out int gv)) world.m_worldGenVersion = gv;
            world.m_fileSource = source;
            SaveSystem.SetSaveNumber(0u);
            world.SaveWorldFWLData(DateTime.Now);
            Plugin.CreatedFromSeed = true;
            Plugin.Instance?.Log($"created world {name} from seed {seed}, generation version {world.m_worldGenVersion}");
            __result = world;
            return false;
        }
    }

    // a save whose location version differs from the game's has its unplaced locations planned again
    // on load (ZoneSystem.Load clears LocationsGenerated); note it so the CLI can report it
    [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Load))]
    internal static class ReplanPatch
    {
        private static void Postfix(ZoneSystem __instance)
        {
            if (Plugin.Active && !__instance.m_locationsGenerated && __instance.m_locationInstances.Count > 0) Plugin.Replanned = true;
        }
    }
}
