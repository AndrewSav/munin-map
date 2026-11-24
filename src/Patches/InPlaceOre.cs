// patches: TASK-4 (TASK-4.6). In-place ore prediction (fork only, see PATCHES.md).
//
// With predict_ore = true and reveal_all on, Munin works out the ore deposits of zones the game
// has not generated yet by re-running the game's vegetation pass (ZoneSystem.PlaceVegetation, backlog
// doc-2) for them. Nothing is spawned in the world and no zone is generated. The work runs on a few
// lowest-priority worker threads; the game thread only reads the game's data once at the start and
// collects results. Off the game thread it uses copies of what Unity allows only on its main thread,
// each checked against the game's own code before it is trusted:
//   - Unity's random number generator (URandom; every function used, many seeds),
//   - the zone's terrain, worked out per vertex with HeightmapBuilder's formula (ZoneTerrain; against
//     HeightmapBuilder.Build for a few zones),
//   - the block check, as ray tests against the prefabs' own colliders (Blockers).
// The result goes to the world's predicted-ore.csv (PredictedOre.FileName), which PredictedOre reads and
// serves. A restart resumes from the file's done set.
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MuninMap.Tiles;
using UnityEngine;

namespace MuninMap.Patches
{
    internal static class InPlaceOre
    {
        public const string FileName = PredictedOre.FileName;
        private const string OldFileName = "predicted-ore-inplace.csv";   // its name before TASK-4.7
        // The Valheim version in-place was verified on (the game-update routine, DRAFT-10): the parts
        // copied from the game's code (placement, locations, Awake draws) were compared with it and the
        // result measured against a generated copy. Raised in a Munin release when the routine has been run
        // for a new game version; on any other version it still runs, and the page says it may be less exact.
        public static string CheckedAgainst
        {
            get { var v = Environment.GetEnvironmentVariable("MUNIN_INPLACE_CHECKED"); return string.IsNullOrEmpty(v) ? "1.0.21" : v; }   // the variable: testing only
        }
        private const int FormatVersion = 10;   // 5: off the game thread; 6: convex colliders as their hull; 7: out to 10,500 m; 8: location parts; 9: unplaced unique locations left out; 10: an entry blocks itself

        // the ore entries' prefabs (doc-2: the ores that are vegetation)
        private static readonly HashSet<string> OrePrefabs = new HashSet<string> {
            "silvervein", "mudpile_beacon", "rock4_copper", "MineRock_Tin", "MineRock_Obsidian" };

        // testing knobs (environment, not settings): MUNIN_INPLACE_ALL=1 predicts generated zones too (to
        // compare with a generated copy), MUNIN_INPLACE_BLOCK=none|shapes, MUNIN_INPLACE_THREADS,
        // MUNIN_INPLACE_ZONE_DELAY_MS=N (a pause after each zone, to watch the order), MUNIN_INPLACE_LOCATIONS=off (leave locations' parts out), MUNIN_INPLACE_FRESH=1 (start again rather than resume), MUNIN_INPLACE_STOP_AFTER=N (stop after N
        // zones, as an interrupted run would)
        private static bool allZones;
        private static string blockMode = "shapes";
        private static int threads = 2;
        private static int zoneDelayMs;   // testing: slow the workers down so the order can be watched

        public static bool On => PredictedOre.On;

        private static string worldDir;
        private static int generation;   // bumped per world, so an old run stops
        private static volatile bool stopping;
        private static string status = "waiting for the world to load";
        private static int zonesDone, zonesTotal;
        public static string Status => status;
        public static int ZonesDone => zonesDone;
        public static int ZonesTotal => zonesTotal;

        // per world, from PredictedOre.Load (main thread)
        public static void Load(string worldDataPath)
        {
            worldDir = worldDataPath;
            generation++;
            stopping = true;   // an earlier world's threads stop at their next zone
            active = false;
            zonesDone = 0; zonesTotal = 0;
            status = "waiting for the world to load";
            if (!On) return;
            if (!quitHooked) { quitHooked = true; Application.quitting += SaveOnQuit; }
            allZones = Environment.GetEnvironmentVariable("MUNIN_INPLACE_ALL") == "1";
            string b = Environment.GetEnvironmentVariable("MUNIN_INPLACE_BLOCK");
            if (b == "none" || b == "shapes") blockMode = b;
            if (int.TryParse(Environment.GetEnvironmentVariable("MUNIN_INPLACE_THREADS"), out int th) && th > 0) threads = th;
            int.TryParse(Environment.GetEnvironmentVariable("MUNIN_INPLACE_ZONE_DELAY_MS"), out zoneDelayMs);
            locations = Environment.GetEnvironmentVariable("MUNIN_INPLACE_LOCATIONS") != "off";
            FrameWatch.Start();   // testing only: MUNIN_FRAME_LOG
            StaticCoroutine.Start(Run(generation));
        }

        // ---- the game's data, read once per world (game thread) ----

        private sealed class Entry
        {
            public int index;
            public ZoneSystem.ZoneVegetation veg;
            public int hash;            // prefab name's stable hash, for the seed
            public bool ore, networked;
            public string prefab;
            public ShapeSet shapes;     // colliders that can block or be snapped to (null: none)
            public int awakeDraws;      // random draws its components' Awake makes when the game instantiates it
            public int lodFades;        // LodFadeInOut with a LODGroup: one more draw when a camera is over 20 m away
        }

        private static List<Entry> entries;
        private static Heightmap.Biome oreBiomes;
        private static int hmWidth;
        private static float hmScale;
        private static int blockMask, staticSolidMask;
        private static int seedHash;
        private static bool haveCamera;
        private static Vector3 cameraPos;
        private static Dictionary<Vector2s, (Vector3 pos, float r)> clearAreas;
        // the planned location in each zone, and the copies of the location prefabs that can stand in an ore
        // biome (a name maps to null when its parts can't block); a zone whose location isn't copied yet waits
        private static Dictionary<Vector2s, (ZoneSystem.ZoneLocation loc, Vector3 pos)> locationAt;
        private static readonly ConcurrentDictionary<string, LocModel> locModels = new ConcurrentDictionary<string, LocModel>();
        private static HashSet<string> locNeeded;
        private static bool locations = true;
        private static int locPlaced, locSkippedRandom;

        private static void Prepare()
        {
            var zs = ZoneSystem.instance;
            var hm = zs.m_zonePrefab.GetComponentInChildren<Heightmap>();
            hmWidth = hm.m_width; hmScale = hm.m_scale;
            blockMask = zs.m_blockRayMask;
            staticSolidMask = LayerMask.GetMask("static_solid");
            seedHash = WorldGenerator.instance.GetSeed();
            Blockers.hitTriggers = Physics.queriesHitTriggers;
            Blockers.hitBackfaces = Physics.queriesHitBackfaces;
            var cam = Utils.GetMainCamera();
            haveCamera = cam != null;
            cameraPos = haveCamera ? cam.transform.position : Vector3.zero;

            // PlaceLocations' clear area for each planned location (only zones not generated yet use it)
            clearAreas = new Dictionary<Vector2s, (Vector3, float)>();
            locationAt = new Dictionary<Vector2s, (ZoneSystem.ZoneLocation, Vector3)>();
            foreach (var kv in zs.m_locationInstances)
            {
                var loc = kv.Value.m_location;
                if (loc == null) continue;
                // a unique location (the traders, the Forge of Potential...) has several candidate sites: the
                // first one generated is built and the others are deleted (RemoveUnplacedLocations), so an
                // unplaced candidate most likely never exists; it is left out, clear area and parts
                if (loc.m_unique && !kv.Value.m_placed) continue;
                if (loc.m_clearArea) clearAreas[kv.Key] = (kv.Value.m_position, loc.m_exteriorRadius);
                locationAt[kv.Key] = (loc, kv.Value.m_position);
            }

            var sets = new Dictionary<string, ShapeSet>();
            entries = new List<Entry>();
            oreBiomes = Heightmap.Biome.None;
            int i = 0, meshes = 0, convex = 0;
            foreach (var veg in zs.m_vegetation)
            {
                i++;
                if (!veg.m_enable || veg.m_prefab == null) continue;
                var e = new Entry { index = i, veg = veg, prefab = veg.m_prefab.name, hash = veg.m_prefab.name.GetStableHashCode() };
                e.ore = OrePrefabs.Contains(e.prefab);
                e.networked = veg.m_prefab.GetComponent<ZNetView>() != null;
                if (e.ore) oreBiomes |= veg.m_biome;
                AwakeDraws(veg.m_prefab, out e.awakeDraws, out e.lodFades);
                if (!sets.TryGetValue(e.prefab, out var set)) sets[e.prefab] = set = Shapes(veg.m_prefab, ref meshes, ref convex);
                e.shapes = set;
                entries.Add(e);
            }
            ZLog.Log($"MuninMap: in-place ore: {entries.Count} vegetation entries, {entries.Count(x => x.ore)} ore entries, "
                + $"{sets.Values.Count(s => s != null)} prefabs with colliders that can block ({meshes} mesh colliders, {convex} of them convex); "
                + $"blocking: {blockMode}; main camera " + (haveCamera ? "at " + cameraPos : "none")
                + $"; queries hit triggers {Blockers.hitTriggers}, backfaces {Blockers.hitBackfaces}; "
                + "ore entries: " + string.Join(", ", entries.Where(x => x.ore).Select(x => $"#{x.index} {x.prefab} (awake draws {x.awakeDraws}+{x.lodFades} lod)")));
        }

        // The game instantiates every accepted object inside the placement loop, and some components draw
        // from Unity's random state in Awake (which runs during Instantiate, for active objects, enabled
        // or not), shifting every later candidate of that entry (doc-2). Counted from the loaded prefab;
        // the conditions are those in the components' Awake (1.0.21), with the ZDO present as in Ghost mode.
        private static void AwakeDraws(GameObject prefab, out int fixedDraws, out int lodFades)
        {
            fixedDraws = 0; lodFades = 0;
            if (!prefab.activeSelf) return;
            foreach (var b in prefab.GetComponentsInChildren<MonoBehaviour>(false))
            {
                switch (b)
                {
                    case LodFadeInOut l: if (l.GetComponent<LODGroup>() != null) lodFades++; break;
                    case MineRock5 _: fixedDraws++; break;
                    case Pickable pk: if (pk.m_respawnTimeMinutes > 0f) fixedDraws++; break;
                    case ResourceRoot _: fixedDraws++; break;
                    case ZSFX _: fixedDraws++; break;
                    case Floating _: fixedDraws++; break;
                }
            }
        }

        // a collider as a shape, `local` mapping its space to the space it is placed in; null for a kind
        // the block check can't use
        public static ShapeDef ShapeOf(Collider c, Matrix4x4 local)
        {
            var d = new ShapeDef { layer = c.gameObject.layer, trigger = c.isTrigger, rigidbody = c.attachedRigidbody != null, local = local };
            switch (c)
            {
                case BoxCollider bx: d.kind = 0; d.center = bx.center; d.size = bx.size; d.bounds = new Bounds(bx.center, bx.size); return d;
                case SphereCollider sp: d.kind = 1; d.center = sp.center; d.radius = sp.radius; d.bounds = new Bounds(sp.center, Vector3.one * sp.radius * 2f); return d;
                case CapsuleCollider cp:
                    d.kind = 2; d.center = cp.center; d.radius = cp.radius; d.height = cp.height; d.direction = cp.direction;
                    d.bounds = new Bounds(cp.center, Vector3.one * Mathf.Max(cp.height, cp.radius * 2f)); return d;
                case MeshCollider mc:
                    if (mc.sharedMesh == null || !mc.sharedMesh.isReadable) return null;
                    d.kind = 3; d.verts = mc.sharedMesh.vertices; d.tris = mc.sharedMesh.triangles; d.bounds = mc.sharedMesh.bounds; d.convex = mc.convex;
                    return d;
                default: return null;
            }
        }

        // the prefab's enabled colliders on the layers the block check or static-solid snapping sees,
        // relative to the prefab's root; null when it has none
        private static ShapeSet Shapes(GameObject prefab, ref int meshes, ref int convex)
        {
            var pt = prefab.transform;
            var toRoot = pt.worldToLocalMatrix;
            var list = new List<ShapeDef>();
            foreach (var c in prefab.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled) continue;
                int layer = c.gameObject.layer;
                if (((1 << layer) & (blockMask | staticSolidMask)) == 0) continue;
                var d = ShapeOf(c, toRoot * c.transform.localToWorldMatrix);
                if (d == null) continue;
                if (d.kind == 3) { meshes++; if (d.convex) convex++; }
                list.Add(d);
            }
            if (list.Count == 0) return null;
            return new ShapeSet { shapes = list.ToArray(), prefabScale = pt.localScale };
        }

        // ---- checks of the copies against the game's own code ----

        // game thread: URandom against UnityEngine.Random for every function used; picks the formulas
        // that match (Unity doesn't document them) and returns false when none does
        private static bool CheckRandom(out string why)
        {
            why = null;
            var saved = UnityEngine.Random.state;
            try
            {
                var r = new URandom();
                var seeds = new List<int> { 0, 1, -1, 12345, int.MaxValue, int.MinValue };
                var sr = new System.Random(4271);
                for (int i = 0; i < 200; i++) seeds.Add(sr.Next(int.MinValue, int.MaxValue));
                // value and int Range
                foreach (int s in seeds)
                {
                    UnityEngine.Random.InitState(s); r.InitState(s);
                    for (int k = 0; k < 50; k++)
                    {
                        float a = UnityEngine.Random.value, b = r.value;
                        if (a != b) { why = $"value differs (seed {s}, draw {k}: {a:R} vs {b:R})"; return false; }
                        int lo = sr.Next(-500, 500), hi = lo + sr.Next(-50, 400);
                        int ia = UnityEngine.Random.Range(lo, hi), ib = r.Range(lo, hi);
                        if (ia != ib) { why = $"Range({lo}, {hi}) differs (seed {s}: {ia} vs {ib})"; return false; }
                    }
                }
                // float Range: the formula that matches everywhere
                bool found = false;
                for (int form = 0; form < 3 && !found; form++)
                {
                    URandom.RangeFloatForm = form;
                    bool ok = true;
                    var sr2 = new System.Random(9187);
                    foreach (int s in seeds)
                    {
                        UnityEngine.Random.InitState(s); r.InitState(s);
                        for (int k = 0; k < 50 && ok; k++)
                        {
                            float lo = (float)(sr2.NextDouble() * 200 - 100), hi = lo + (float)(sr2.NextDouble() * 400 - 20);
                            if (UnityEngine.Random.Range(lo, hi) != r.Range(lo, hi)) ok = false;
                        }
                        if (!ok) break;
                    }
                    found = ok;
                }
                if (!found)
                {
                    // what Unity gives, to work the formula out from
                    var sb = new StringBuilder();
                    foreach (int s in new[] { 1, 12345, -7 })
                    {
                        r.InitState(s); uint u = r.NextUInt();
                        UnityEngine.Random.InitState(s); float v = UnityEngine.Random.value;
                        UnityEngine.Random.InitState(s); float a = UnityEngine.Random.Range(0f, 1f);
                        UnityEngine.Random.InitState(s); float b = UnityEngine.Random.Range(10f, 20f);
                        UnityEngine.Random.InitState(s); float c = UnityEngine.Random.Range(-3.5f, 7.25f);
                        sb.Append($" [seed {s}: u {u} v {v:R} r01 {a:R} r10_20 {b:R} r-3.5_7.25 {c:R}]");
                    }
                    why = "Range(float, float) matches none of the known formulas;" + sb;
                    return false;
                }
                found = false;
                for (int form = 0; form < 4 && !found; form++)
                {
                    URandom.CircleForm = form;
                    bool ok = true;
                    foreach (int s in seeds)
                    {
                        UnityEngine.Random.InitState(s); r.InitState(s);
                        for (int k = 0; k < 30 && ok; k++) if (UnityEngine.Random.insideUnitCircle != r.insideUnitCircle) ok = false;
                        if (!ok) break;
                    }
                    found = ok;
                }
                if (!found)
                {
                    var sb = new StringBuilder();
                    foreach (int s in new[] { 1, 12345, -7 })
                    {
                        r.InitState(s); uint u1 = r.NextUInt(), u2 = r.NextUInt();
                        UnityEngine.Random.InitState(s); var c = UnityEngine.Random.insideUnitCircle;
                        sb.Append($" [seed {s}: u {u1} {u2} circle {c.x:R} {c.y:R}]");
                    }
                    why = "insideUnitCircle matches none of the known formulas;" + sb;
                    return false;
                }
                return true;
            }
            finally { UnityEngine.Random.state = saved; }
        }

        // worker thread: ZoneTerrain against HeightmapBuilder.Build, every vertex, for a few land zones
        private static bool CheckTerrain(List<Vector2s> order, out string why)
        {
            why = null;
            var wg = WorldGenerator.instance;
            int checkedZones = 0;
            foreach (var z in order)
            {
                if (checkedZones >= 6) break;
                var t = new ZoneTerrain(z, hmWidth, hmScale);
                t.Corners(wg, oreBiomes);
                if (!t.active) continue;
                var data = new HeightmapBuilder.HMBuildData(t.center, hmWidth, hmScale, false, wg);
                HeightmapBuilder.instance.Build(data);
                int n = hmWidth + 1;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float a = data.m_baseHeights[y * n + x], b = t.H(x, y);
                        if (a != b) { why = $"zone {z.x},{z.y} vertex {x},{y}: height {a:R} vs {b:R}"; return false; }
                        if (data.m_baseMask[y * n + x].a != t.MaskA(x, y)) { why = $"zone {z.x},{z.y} vertex {x},{y}: vegetation mask differs"; return false; }
                    }
                for (int c = 0; c < 4; c++)
                    if (data.m_cornerBiomes[c].Biome != t.corners[c].Biome) { why = $"zone {z.x},{z.y}: corner {c} biome differs"; return false; }
                checkedZones++;
            }
            return true;
        }

        // ---- zones ----

        // Zones nearest the centre first (the order fixes each zone's index in the done set), out to the
        // world's edge at 10,500 m (ore lies beyond 10 km; a zone counts when any part of it is inside)
        private static List<Vector2s> ZoneOrder()
        {
            const float radius = 10500f + 46f;
            int r = Mathf.CeilToInt(radius / 64f);
            var list = new List<Vector2s>();
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (new Vector2(x * 64f, y * 64f).magnitude <= radius) list.Add(new Vector2s(x, y));
            list.Sort((a, b) => { int d = (a.x * a.x + a.y * a.y).CompareTo(b.x * b.x + b.y * b.y); if (d != 0) return d; d = a.y.CompareTo(b.y); return d != 0 ? d : a.x.CompareTo(b.x); });
            return list;
        }

        private struct Found { public string prefab; public Vector3 p; public Vector2s zone; }

        // ---- zones a browser is looking at go first (criterion #7) ----

        private static volatile bool active;
        private static bool quitHooked;
        // the run in progress, for SaveOnQuit
        private static (string path, string seed, string game, List<Found> found, bool[] done, ConcurrentQueue<(int, Found[])> results, Func<int> running)? current;

        // The server is stopping: the workers stop at their next zone, and what they finished is written,
        // so the next start redoes nothing (game thread, Application.quitting)
        private static void SaveOnQuit()
        {
            var c = current;
            if (c == null || !active) return;
            stopping = true; active = false;
            try
            {
                var run = c.Value;
                var wait = System.Diagnostics.Stopwatch.StartNew();
                while (run.running() > 0 && wait.ElapsedMilliseconds < 2000) Thread.Sleep(10);
                while (run.results.TryDequeue(out var res)) { run.found.AddRange(res.Item2); if (!run.done[res.Item1]) { run.done[res.Item1] = true; zonesDone++; } }
                writing?.Wait(2000);
                Write(run.path, run.seed, run.game, run.found.ToArray(), run.done, zonesDone, zonesDone >= zonesTotal);
                ZLog.Log($"MuninMap: in-place ore: the server is stopping; {zonesDone} of {zonesTotal} zones written, the next start resumes");
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: in-place ore: saving on quit: " + e.Message); }
        }
        private static Dictionary<Vector2s, int> indexOf;   // zone -> index in the order; read-only while active
        private static int[] claimed;                        // 1: a worker has taken the zone (or it was done)
        private static readonly ConcurrentStack<int> hot = new ConcurrentStack<int>();
        private static int hotDone;
        // location prefabs that viewed zones wait for: the loader takes these first
        private static readonly ConcurrentQueue<string> hotLocations = new ConcurrentQueue<string>();

        // HTTP threads (ViewedZones): a zone a browser is looking at goes first, and so does its location's
        // prefab if it isn't copied yet. Cheap, and nothing when no run is going.
        private static void OnViewed(int zx, int zz)
        {
            if (!active) return;
            var ix = indexOf; var cl = claimed;
            if (ix == null || cl == null) return;
            var z = new Vector2s(zx, zz);
            if (!ix.TryGetValue(z, out int k) || Volatile.Read(ref cl[k]) != 0) return;
            hot.Push(k);
            if (locNeeded != null && locationAt != null && locationAt.TryGetValue(z, out var la) && la.loc.m_prefabName != null
                && locNeeded.Contains(la.loc.m_prefabName) && !locModels.ContainsKey(la.loc.m_prefabName))
                hotLocations.Enqueue(la.loc.m_prefabName);
        }

        // worker threads: the next zone, a viewed one first; -1 when none is left
        private static int stopAfter, taken;   // testing: stop as if interrupted after this many zones (0: never)

        private static int NextZone(ConcurrentQueue<int> queue, out bool viewed)
        {
            viewed = true;
            if (stopAfter > 0 && Interlocked.Increment(ref taken) > stopAfter) return -1;
            while (hot.TryPop(out int k)) if (Interlocked.CompareExchange(ref claimed[k], 1, 0) == 0) return k;
            viewed = false;
            while (queue.TryDequeue(out int k)) if (Interlocked.CompareExchange(ref claimed[k], 1, 0) == 0) return k;
            return -1;
        }

        private static readonly object statLock = new object();
        private static int niceLogged;
        private static double workMs;    // the worker threads' time, and the zones that had work
        private static int landZones;
        private static long touchedSum; private static int touchedMax, maxZoneMs;

        private static IEnumerator Run(int gen)
        {
            while (gen == generation && !Ready()) yield return new WaitForSecondsRealtime(2f);
            if (gen != generation) yield break;
            // testing only: start later, for a settled baseline before the run (MUNIN_INPLACE_DELAY_S)
            if (int.TryParse(Environment.GetEnvironmentVariable("MUNIN_INPLACE_DELAY_S"), out int delay) && delay > 0)
            {
                status = "waiting (test delay)";
                ZLog.Log($"MuninMap: in-place ore: starting in {delay} s (MUNIN_INPLACE_DELAY_S)");
                yield return new WaitForSecondsRealtime(delay);
                if (gen != generation) yield break;
            }
            stopping = false;
            try
            {
                Prepare();
                if (!CheckRandom(out string why))
                {
                    status = "failed: this game's random numbers don't match Munin's copy (" + why + ")";
                    ZLog.LogWarning("MuninMap: in-place ore: " + status + "; nothing worked out");
                    yield break;
                }
            }
            catch (Exception e) { status = "failed (" + e.Message + ")"; ZLog.LogWarning("MuninMap: in-place ore: " + e); yield break; }
            if (Environment.GetEnvironmentVariable("MUNIN_INPLACE_MEASURE_LOCATIONS") == "1")
            {
                var m = MeasureLocations();
                while (m.MoveNext()) yield return m.Current;
            }

            string seed = WorldGenerator.instance.m_world.m_seedName;
            string game = global::Version.CurrentVersion.ToString();
            string path = Path.Combine(worldDir, FileName);
            AdoptOldFile(path, Path.Combine(worldDir, OldFileName));
            var order = ZoneOrder();
            zonesTotal = order.Count;
            var found = new List<Found>();
            var done = new bool[order.Count];
            if (Environment.GetEnvironmentVariable("MUNIN_INPLACE_FRESH") != "1") Resume(path, seed, game, found, done);
            int.TryParse(Environment.GetEnvironmentVariable("MUNIN_INPLACE_STOP_AFTER"), out stopAfter); taken = 0;
            zonesDone = done.Count(d => d);
            var generated = allZones ? new HashSet<Vector2s>() : new HashSet<Vector2s>(ZoneSystem.instance.m_generatedZones);

            string terrainWhy = null; bool terrainChecked = false, terrainOk = false;
            new Thread(() =>
            {
                try { terrainOk = CheckTerrain(order, out terrainWhy); }
                catch (Exception e) { terrainWhy = e.Message; }
                terrainChecked = true;
            }) { IsBackground = true, Name = "MuninInPlaceOre check", Priority = System.Threading.ThreadPriority.Lowest }.Start();
            while (!terrainChecked) yield return null;
            if (gen != generation) yield break;
            if (!terrainOk)
            {
                status = "failed: the zone terrain doesn't match the game's (" + terrainWhy + ")";
                ZLog.LogWarning("MuninMap: in-place ore: " + status + "; nothing worked out");
                yield break;
            }
            ZLog.Log($"MuninMap: in-place ore: random numbers and terrain match the game's (Range form {URandom.RangeFloatForm}, circle form {URandom.CircleForm}); "
                + (zonesDone > 0 ? $"resuming with {zonesDone} of {order.Count} zones done" : $"working out {order.Count} zones")
                + (allZones ? " (MUNIN_INPLACE_ALL: generated zones too)" : $" ({generated.Count} generated, skipped)")
                + $" on {threads} thread(s)");
            status = "running";

            var queue = new ConcurrentQueue<int>();
            for (int k = 0; k < order.Count; k++) if (!done[k]) queue.Enqueue(k);
            hot.Clear(); hotDone = 0;
            claimed = new int[order.Count];
            for (int k = 0; k < order.Count; k++) if (done[k]) claimed[k] = 1;
            var ixMap = new Dictionary<Vector2s, int>(order.Count);
            for (int k = 0; k < order.Count; k++) ixMap[order[k]] = k;
            indexOf = ixMap;
            active = true;
            while (hotLocations.TryDequeue(out _)) { }
            ViewedZones.Zone -= OnViewed;
            ViewedZones.Zone += OnViewed;
            var results = new ConcurrentQueue<(int, Found[])>();
            workMs = 0; landZones = 0; touchedSum = 0; touchedMax = 0; maxZoneMs = 0; locPlaced = 0; locSkippedRandom = 0;
            var later = new ConcurrentQueue<int>();   // zones waiting for their location's copy
            locModels.Clear();
            // only the location prefabs that a zone still to be worked out has, so a finished prediction (most
            // restarts) loads none, and a partly finished one only what is left (each costs memory that stays)
            locNeeded = new HashSet<string>();
            if (locations && blockMode == "shapes")
                for (int k = 0; k < order.Count; k++)
                {
                    if (done[k] || generated.Contains(order[k]) || !locationAt.TryGetValue(order[k], out var la)) continue;
                    var l = la.loc;
                    if (l.m_enable && l.m_prefabName != null && (l.m_biome & oreBiomes) != 0) locNeeded.Add(l.m_prefabName);
                }
            if (locNeeded.Count > 0)
                StaticCoroutine.Start(LoadLocations(gen, ZoneSystem.instance.m_locations.FindAll(l => l.m_prefabName != null && locNeeded.Contains(l.m_prefabName))));
            int running = threads;
            current = (path, seed, game, found, done, results, () => Volatile.Read(ref running));
            for (int w = 0; w < threads; w++)
            {
                new Thread(() =>
                {
                    string nice = ThreadNice.Lower();
                    if (Interlocked.Exchange(ref niceLogged, 1) == 0) ZLog.Log("MuninMap: in-place ore: worker threads at the lowest priority (" + nice + ")");
                    var r = new URandom();
                    var blockers = new Blockers();
                    var zf = new List<Found>();
                    var wg = WorldGenerator.instance;
                    try
                    {
                        while (!stopping)
                        {
                            // a zone that waited for its location's copy goes as soon as the copy is there
                            int k = -1; bool viewed = false;
                            if (later.TryDequeue(out int lk)) { if (LocationReady(order[lk])) k = lk; else later.Enqueue(lk); }
                            if (k < 0)
                            {
                                k = NextZone(queue, out viewed);
                                if (k < 0)
                                {
                                    // only zones waiting for their location's copy are left
                                    if (!later.TryDequeue(out k)) break;
                                    if (!LocationReady(order[k])) { later.Enqueue(k); Thread.Sleep(50); continue; }
                                }
                                else if (!LocationReady(order[k])) { later.Enqueue(k); continue; }
                            }
                            if (viewed) Interlocked.Increment(ref hotDone);
                            var z = order[k];
                            zf.Clear();
                            if (!generated.Contains(z))
                            {
                                var t = new ZoneTerrain(z, hmWidth, hmScale);
                                t.Corners(wg, oreBiomes);
                                if (t.active)
                                {
                                    var sw = System.Diagnostics.Stopwatch.StartNew();
                                    blockers.Clear();
                                    PlaceLocation(t, r, blockers);
                                    var plan = Plan(t);
                                    for (int i = 0; i < plan.Count; i++) PlaceEntry(plan[i], t, r, blockers, zf, i < plan.Count - 1);
                                    double ms = sw.Elapsed.TotalMilliseconds;
                                    int touched = t.Touched;
                                    lock (statLock)
                                    {
                                        workMs += ms; landZones++; touchedSum += touched;
                                        if (touched > touchedMax) touchedMax = touched;
                                        if (ms > maxZoneMs) maxZoneMs = (int)ms;
                                    }
                                }
                            }
                            results.Enqueue((k, zf.ToArray()));
                            if (zoneDelayMs > 0) Thread.Sleep(zoneDelayMs);
                        }
                    }
                    catch (Exception e) { ZLog.LogWarning("MuninMap: in-place ore: worker: " + e); }
                    finally { Interlocked.Decrement(ref running); }
                }) { IsBackground = true, Name = "MuninInPlaceOre " + w, Priority = System.Threading.ThreadPriority.Lowest }.Start();
            }

            // the game thread only collects, logs and writes (the file on a worker)
            var runSw = System.Diagnostics.Stopwatch.StartNew();
            float lastWrite = Time.realtimeSinceStartup, lastLog = lastWrite;
            var wait = new WaitForSecondsRealtime(0.5f);
            int hotWritten = 0;
            while (gen == generation)
            {
                bool over = Volatile.Read(ref running) == 0;
                while (results.TryDequeue(out var res)) { found.AddRange(res.Item2); if (!done[res.Item1]) { done[res.Item1] = true; zonesDone++; } }
                if (over) break;
                float now = Time.realtimeSinceStartup;
                if (now - lastLog >= 30f)
                {
                    lastLog = now;
                    ZLog.Log($"MuninMap: in-place ore: {zonesDone}/{zonesTotal} zones, {found.Count} deposits, {runSw.Elapsed.TotalMinutes:F1} min");
                }
                // every minute, and within a few seconds once zones someone is looking at are done
                int hd = Volatile.Read(ref hotDone);
                if ((now - lastWrite >= 60f || (hd != hotWritten && now - lastWrite >= 3f)) && (writing == null || writing.IsCompleted))
                {
                    lastWrite = now; hotWritten = hd;
                    var copy = found.ToArray(); var mask = (bool[])done.Clone(); int n = zonesDone;
                    writing = System.Threading.Tasks.Task.Run(() => Write(path, seed, game, copy, mask, n, false));
                }
                yield return wait;
            }
            active = false;
            ViewedZones.Zone -= OnViewed;
            current = null;
            if (gen != generation) yield break;
            while (writing != null && !writing.IsCompleted) yield return null;
            var all = found.ToArray(); var fin = (bool[])done.Clone(); int count = zonesDone; bool complete = zonesDone >= zonesTotal;
            writing = System.Threading.Tasks.Task.Run(() => Write(path, seed, game, all, fin, count, complete));
            while (!writing.IsCompleted) yield return null;
            status = complete ? "done" : "stopped";
            ZLog.Log($"MuninMap: in-place ore: {status}, {found.Count} deposits in {runSw.Elapsed.TotalMinutes:F2} min on {threads} thread(s), {hotDone} zones first because a browser was looking "
                + $"({workMs / 1000:F1} s of work for {landZones} zones with ore biomes, longest zone {maxZoneMs} ms; "
                + $"location parts placed in {locPlaced} zones, left out in {locSkippedRandom} (randomly rotated); "
                + $"terrain vertices worked out: {(landZones > 0 ? touchedSum / landZones : 0)} per zone on average, {touchedMax} at most, of {(hmWidth + 1) * (hmWidth + 1)})");
        }

        // Measuring (TASK-4.6 plan step 5): what loading the location prefabs that can stand in an ore
        // biome costs, done as the game does it when it generates a zone: SoftReference.LoadAsync, then
        // the colliders under networked children (what a Ghost-mode zone gets before its vegetation) copied
        // in pieces of about 1 ms a frame, then Release. Logs the longest frame seen
        // meanwhile, against the frames before it started.
        private static IEnumerator MeasureLocations()
        {
            var zs = ZoneSystem.instance;
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            long Rss() { proc.Refresh(); return proc.WorkingSet64; }
            var list = zs.m_locations.Where(l => l.m_enable && l.m_quantity > 0 && (l.m_biome & oreBiomes) != 0).ToList();
            var frame = System.Diagnostics.Stopwatch.StartNew();
            double baseMax = 0;
            for (int i = 0; i < 150; i++) { yield return null; baseMax = Math.Max(baseMax, frame.Elapsed.TotalMilliseconds); frame.Restart(); }
            ZLog.Log($"MuninMap: in-place ore: measuring {list.Count} location prefabs in ore biomes (asynchronous loads); longest of the 150 frames before: {baseMax:F1} ms; memory {Rss() / 1048576} MB");
            long start = Rss(), peak = start;
            double maxFrame = 0, maxCopy = 0; string maxFrameAt = null;
            int colliders = 0, meshVerts = 0;
            var all = System.Diagnostics.Stopwatch.StartNew();
            frame.Restart();
            foreach (var loc in list)
            {
                bool done = false, ok = false;
                loc.m_prefab.LoadAsync((id, result) => { done = true; ok = result == SoftReferenceableAssets.LoadResult.Succeeded; });
                while (!done)
                {
                    yield return null;
                    double f = frame.Elapsed.TotalMilliseconds; frame.Restart();
                    if (f > maxFrame) { maxFrame = f; maxFrameAt = loc.m_prefabName + " (loading)"; }
                }
                if (!ok) { ZLog.LogWarning("MuninMap: in-place ore: location " + loc.m_prefabName + " did not load"); continue; }
                long m = Rss(); if (m > peak) peak = m;
                // copy the shapes in pieces
                var asset = loc.m_prefab.Asset;
                var views = Utils.GetEnabledComponentsInChildren<ZNetView>(asset);
                var slice = System.Diagnostics.Stopwatch.StartNew();
                foreach (var nv in views)
                {
                    if (nv.gameObject == asset) continue;
                    foreach (var c in nv.GetComponentsInChildren<Collider>())
                    {
                        if (!c.enabled || ((1 << c.gameObject.layer) & blockMask) == 0) continue;
                        colliders++;
                        var local = c.transform.localToWorldMatrix;
                        if (c is MeshCollider mc && mc.sharedMesh != null && mc.sharedMesh.isReadable) { meshVerts += mc.sharedMesh.vertices.Length; var tris = mc.sharedMesh.triangles; }
                    }
                    if (slice.Elapsed.TotalMilliseconds >= 1.0)
                    {
                        maxCopy = Math.Max(maxCopy, slice.Elapsed.TotalMilliseconds);
                        yield return null;
                        double f = frame.Elapsed.TotalMilliseconds; frame.Restart();
                        if (f > maxFrame) { maxFrame = f; maxFrameAt = loc.m_prefabName + " (copying)"; }
                        slice.Restart();
                    }
                }
                maxCopy = Math.Max(maxCopy, slice.Elapsed.TotalMilliseconds);
                loc.m_prefab.Release();
                yield return null;
                double g = frame.Elapsed.TotalMilliseconds; frame.Restart();
                if (g > maxFrame) { maxFrame = g; maxFrameAt = loc.m_prefabName + " (after release)"; }
            }
            ZLog.Log($"MuninMap: in-place ore: measured {list.Count} location prefabs in {all.Elapsed.TotalSeconds:F1} s: longest frame meanwhile {maxFrame:F1} ms ({maxFrameAt}), "
                + $"longest copying piece {maxCopy:F2} ms; {colliders} blocking colliders, {meshVerts} mesh vertices copied; "
                + $"memory {start / 1048576} MB before, {peak / 1048576} MB at most, {Rss() / 1048576} MB after");
        }

        // a zone can be worked out once its location (if it has one that matters) is copied
        private static bool LocationReady(Vector2s z) =>
            locNeeded == null || !locationAt.TryGetValue(z, out var la) || la.loc.m_prefabName == null
            || !locNeeded.Contains(la.loc.m_prefabName) || locModels.ContainsKey(la.loc.m_prefabName);

        // the zone's location parts, placed as SpawnLocation places them in Ghost mode (worker thread)
        private static void PlaceLocation(ZoneTerrain t, URandom r, Blockers blockers)
        {
            if (locNeeded == null || !locationAt.TryGetValue(t.zone, out var la) || la.loc.m_prefabName == null) return;
            if (!locModels.TryGetValue(la.loc.m_prefabName, out var model) || model == null) return;
            var loc = la.loc;
            Vector3 p = la.pos;
            if (Ground(t, p, out float gy, out _)) p.y = gy;   // PlaceLocations' GetGroundData
            if (loc.m_snapToWater) p.y = 30f;
            int seed = seedHash + t.zone.x * 4271 + t.zone.y * 9187;
            Quaternion rot = Quaternion.identity;
            if (loc.m_slopeRotation)
                rot = InPlaceOreLocations.LikelySlopeRotation(p, loc.m_exteriorRadius, q => GroundHeight(t, q), r, seed ^ 0x5a17);
            else if (loc.m_randomRotation) { Interlocked.Increment(ref locSkippedRandom); return; }   // its facing can't be known
            InPlaceOreLocations.Place(model, seed, p, rot, r, blockers, q => WorldGenerator.instance.GetBiome(q));
            Interlocked.Increment(ref locPlaced);
        }

        // game thread: each location prefab that can stand in an ore biome, loaded asynchronously as the
        // game loads it, copied in pieces of about a millisecond a frame, then released
        private static IEnumerator LoadLocations(int gen, List<ZoneSystem.ZoneLocation> list)
        {
            var seen = new HashSet<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int copied = 0, parts = 0, colliders = 0, first = 0;
            var byName = new Dictionary<string, ZoneSystem.ZoneLocation>();
            foreach (var l in list) if (!byName.ContainsKey(l.m_prefabName)) byName[l.m_prefabName] = l;
            var pending = new List<string>(byName.Keys);
            while (pending.Count > 0)
            {
                if (gen != generation) yield break;
                // a location a viewed zone waits for comes first
                string name = null;
                while (hotLocations.TryDequeue(out var h)) if (pending.Contains(h)) { name = h; first++; break; }
                if (name == null) name = pending[0];
                pending.Remove(name);
                var loc = byName[name];
                if (!seen.Add(loc.m_prefabName)) continue;
                bool done = false, ok = false;
                try { loc.m_prefab.LoadAsync((id, result) => { done = true; ok = result == SoftReferenceableAssets.LoadResult.Succeeded; }); }
                catch (Exception e) { ZLog.LogWarning("MuninMap: in-place ore: location " + loc.m_prefabName + ": " + e.Message); locModels[loc.m_prefabName] = null; continue; }
                while (!done) yield return null;
                LocModel model = null;
                if (ok)
                {
                    var copy = InPlaceOreLocations.Copy(loc.m_prefabName, loc.m_prefab.Asset, blockMask, 1.0, mm => model = mm);
                    while (true)
                    {
                        bool more;
                        try { more = copy.MoveNext(); }
                        catch (Exception e) { ZLog.LogWarning("MuninMap: in-place ore: copying location " + loc.m_prefabName + ": " + e.Message); model = null; break; }
                        if (!more) break;
                        yield return null;
                    }
                    try { loc.m_prefab.Release(); } catch { }
                }
                else ZLog.LogWarning("MuninMap: in-place ore: location " + loc.m_prefabName + " did not load; its parts are left out");
                if (model != null && model.parts.Length == 0) model = null;
                if (model != null) { copied++; parts += model.parts.Length; colliders += model.colliders; }
                locModels[loc.m_prefabName] = model;
            }
            ZLog.Log($"MuninMap: in-place ore: {seen.Count} location prefabs read in {sw.Elapsed.TotalSeconds:F1} s ({copied} with parts that can block: {parts} parts, {colliders} colliders; {first} first because a browser was looking)");
        }

        private static bool Ready()
        {
            try { return ZoneSystem.instance != null && ZoneSystem.instance.LocationsGenerated && WorldGenerator.instance != null && HeightmapBuilder.instance != null; }
            catch { return false; }
        }

        // the entries the zone needs, in the game's order: every ore entry for its biomes, and (with
        // blocking) every earlier entry for its biomes whose objects can block, up to the last ore entry
        private static List<Entry> Plan(ZoneTerrain t)
        {
            var cb = t.corners;
            bool Have(Heightmap.Biome b) => (cb[0].Biome & b) != 0 || (cb[1].Biome & b) != 0 || (cb[2].Biome & b) != 0 || (cb[3].Biome & b) != 0;
            int last = -1;
            for (int k = 0; k < entries.Count; k++) if (entries[k].ore && Have(entries[k].veg.m_biome)) last = k;
            var plan = new List<Entry>();
            for (int k = 0; k <= last; k++)
            {
                var e = entries[k];
                if (!Have(e.veg.m_biome)) continue;
                if (e.ore || (blockMode == "shapes" && e.shapes != null)) plan.Add(e);
            }
            return plan;
        }

        // ---- one vegetation entry in one zone: ZoneSystem.PlaceVegetation's loop body ----

        [ThreadStatic] private static List<float> surround;

        private static void PlaceEntry(Entry e, ZoneTerrain t, URandom r, Blockers blockers, List<Found> found, bool later)
        {
            var veg = e.veg;
            var wg = WorldGenerator.instance;
            Vector2s zoneID = t.zone;
            Vector3 zoneCenterPos = t.center;
            r.InitState(seedHash + zoneID.x * 4271 + zoneID.y * 9187 + e.hash);
            int count = 1;
            if (veg.m_max < 1f) { if (r.value > veg.m_max) return; }
            else count = r.Range((int)veg.m_min, (int)veg.m_max + 1);
            float maxTiltCos = Mathf.Cos(Mathf.PI / 180f * veg.m_maxTilt);
            float minTiltCos = Mathf.Cos(Mathf.PI / 180f * veg.m_minTilt);
            float margin = 32f - veg.m_groupRadius;
            var sur = surround ?? (surround = new List<float>());
            sur.Clear();
            int tries = veg.m_forcePlacement ? count * 50 : count;
            int placed = 0;
            // an accepted object blocks the rest of its own entry (if that entry has the block check) as well
            // as later entries
            bool spawn = e.shapes != null && (later || veg.m_blockCheck) && blockMode == "shapes";
            for (int i = 0; i < tries; i++)
            {
                Vector3 vector = new Vector3(r.Range(zoneCenterPos.x - margin, zoneCenterPos.x + margin), 0f,
                    r.Range(zoneCenterPos.z - margin, zoneCenterPos.z + margin));
                BiomeSector sector = wg.GetBiomeSector(vector);
                if ((veg.AltBiomeParent != null && !sector.AltBiomes.Any(a => a.m_name == veg.AltBiomeParent))
                    || sector.AltBiomes.Any(a => a.m_blockVegetationNames.Contains(veg.m_name))) continue;
                int groupSize = r.Range(veg.m_groupSizeMin, veg.m_groupSizeMax + 1);
                bool any = false;
                for (int g = 0; g < groupSize; g++)
                {
                    Vector3 p = g == 0 ? vector : RandomPointInRadius(r, vector, veg.m_groupRadius);
                    float rotY = r.Range(0, 360);
                    float scale = r.Range(veg.m_scaleMin, veg.m_scaleMax);
                    float tiltX = r.Range(0f - veg.m_randTilt, veg.m_randTilt);
                    float tiltZ = r.Range(0f - veg.m_randTilt, veg.m_randTilt);
                    if (veg.m_blockCheck && IsBlocked(blockers, p)) continue;
                    if (!Ground(t, p, out float gy, out Vector3 normal)) continue;   // the game's ray misses: biome None
                    p.y = gy;
                    Heightmap.Biome biome = GetBiome(t, p);
                    Heightmap.BiomeArea area = BiomeArea(t);
                    if ((veg.m_biome & biome) == 0 || (veg.m_biomeArea & area) == 0) continue;
                    if (veg.m_snapToStaticSolid && StaticSolid(blockers, t, p, out float sh, out Vector3 sn)) { p.y = sh; normal = sn; }
                    float alt = p.y - 30f;
                    if (alt < veg.m_minAltitude || alt > veg.m_maxAltitude) continue;
                    if (!veg.m_minVegetation.Equals(veg.m_maxVegetation))
                    {
                        float m = VegetationMask(t, p);
                        if (m > veg.m_maxVegetation || m < veg.m_minVegetation) continue;
                    }
                    if (!veg.m_minOceanDepth.Equals(veg.m_maxOceanDepth))
                    {
                        float d = OceanDepth(t, p);
                        if (d < veg.m_minOceanDepth || d > veg.m_maxOceanDepth) continue;
                    }
                    if (normal.y < maxTiltCos || normal.y > minTiltCos) continue;
                    if (veg.m_terrainDeltaRadius > 0f)
                    {
                        float delta = TerrainDelta(r, t, p, veg.m_terrainDeltaRadius);
                        if (delta > veg.m_maxTerrainDelta || delta < veg.m_minTerrainDelta) continue;
                    }
                    if (veg.m_minDistanceFromCenter > 0f || veg.m_maxDistanceFromCenter > 0f)
                    {
                        float dc = Utils.LengthXZ(p);
                        if ((veg.m_minDistanceFromCenter > 0f && dc < veg.m_minDistanceFromCenter) || (veg.m_maxDistanceFromCenter > 0f && dc > veg.m_maxDistanceFromCenter)) continue;
                    }
                    if (veg.m_inForest)
                    {
                        float f = WorldGenerator.GetForestFactor(p);
                        if (f < veg.m_forestTresholdMin || f > veg.m_forestTresholdMax) continue;
                    }
                    if (veg.m_surroundCheckVegetation)
                    {
                        float sum = 0f;
                        for (int l = 0; l < veg.m_surroundCheckLayers; l++)
                        {
                            float dist = (float)(l + 1) / veg.m_surroundCheckLayers * veg.m_surroundCheckDistance;
                            for (int s = 0; s < 6; s++)
                            {
                                float a = s / 6f * Mathf.PI * 2f;
                                float vm = VegetationMask(t, p + new Vector3(Mathf.Sin(a) * dist, 0f, Mathf.Cos(a) * dist));
                                float w = (1f - dist) / (veg.m_surroundCheckDistance * 2f);
                                sum += vm * w;
                            }
                        }
                        sur.Add(sum);
                        if (sur.Count < 10) continue;
                        float max = sur.Max(), avg = sur.Average();
                        if (sum < avg + (max - avg) * veg.m_surroundBetterThanAverage) continue;
                    }
                    if (InsideClearArea(t.zone, p)) continue;
                    if (veg.m_snapToWater) p.y = 30f;
                    p.y += veg.m_groundOffset;
                    Quaternion rot;
                    if (veg.m_chanceToUseGroundTilt > 0f && r.value <= veg.m_chanceToUseGroundTilt)
                    {
                        Quaternion q = Quaternion.Euler(0f, rotY, 0f);
                        rot = Quaternion.LookRotation(Vector3.Cross(normal, q * Vector3.forward), normal);
                    }
                    else rot = Quaternion.Euler(tiltX, rotY, tiltZ);
                    if (e.ore) found.Add(new Found { prefab = e.prefab, p = p, zone = zoneID });
                    // the draws the instantiated object's Awake makes (LodFadeInOut: per child, its distance
                    // to the camera; the object's own position stands in for its children's)
                    int draws = e.awakeDraws;
                    if (e.lodFades > 0 && haveCamera && Vector3.Distance(cameraPos, p) > 20f) draws += e.lodFades;
                    for (int dr = 0; dr < draws; dr++) r.NextUInt();
                    // as the game spawns it: the prefab's own scale unless the drawn scale differs
                    // (networked: ZNetView.SetLocalScale), always the drawn scale for other objects
                    if (spawn)
                    {
                        var ps = e.shapes.prefabScale;
                        blockers.Add(e.shapes, p, rot, e.networked && scale.Equals(ps.x) ? ps : new Vector3(scale, scale, scale));
                    }
                    any = true;
                }
                if (any) placed++;
                if (placed >= count) break;
            }
        }

        private static Vector3 RandomPointInRadius(URandom r, Vector3 center, float radius)
        {
            float f = r.value * Mathf.PI * 2f;
            float d = r.Range(0f, radius);
            return center + new Vector3(Mathf.Sin(f) * d, 0f, Mathf.Cos(f) * d);
        }

        // ZoneSystem.IsBlocked: a ray from 2000 m above the point (y = 0 here), 10 km down
        private static bool IsBlocked(Blockers b, Vector3 p) =>
            blockMode == "shapes" && b.Highest(p.x, p.z, p.y + 2000f, p.y - 8000f, blockMask, out _, out _, out _);

        // ZoneSystem.GetStaticSolidHeight: the first static_solid or terrain hit from 1000 m above, 2 km
        // down; false when that first hit has a rigidbody
        private static bool StaticSolid(Blockers b, ZoneTerrain t, Vector3 p, out float height, out Vector3 normal)
        {
            bool ground = Ground(t, p, out height, out normal);
            if (blockMode == "shapes" && b.Highest(p.x, p.z, p.y + 1000f, p.y - 1000f, staticSolidMask, out float y, out Vector3 n, out bool rb)
                && (!ground || y > height))
            {
                if (rb) { height = 0f; normal = Vector3.zero; return false; }
                height = y; normal = n; return true;
            }
            return ground;
        }

        // ---- the zone's terrain: Heightmap's collision mesh and its lookups ----

        private static Vector3 Vertex(ZoneTerrain t, int x, int y) =>
            t.center + new Vector3((float)((double)hmWidth * hmScale * -0.5), 0f, (float)((double)hmWidth * hmScale * -0.5))
                + new Vector3((float)((double)x * hmScale), t.H(x, y), (float)((double)y * hmScale));

        // the ray from above against the zone's collision mesh (quads split from (x+1,y) to (x,y+1), as
        // Heightmap.RebuildCollisionMesh builds them); false outside the zone (no neighbour is loaded)
        private static bool Ground(ZoneTerrain t, Vector3 p, out float y, out Vector3 normal)
        {
            float half = hmWidth * hmScale * 0.5f;
            float lx = (p.x - (t.center.x - half)) / hmScale, lz = (p.z - (t.center.z - half)) / hmScale;
            y = p.y; normal = Vector3.up;
            if (lx < 0f || lz < 0f || lx > hmWidth || lz > hmWidth) return false;
            int ix = Mathf.Min((int)lx, hmWidth - 1), iz = Mathf.Min((int)lz, hmWidth - 1);
            float fx = lx - ix, fz = lz - iz;
            Vector3 v00 = Vertex(t, ix, iz), v10 = Vertex(t, ix + 1, iz), v01 = Vertex(t, ix, iz + 1);
            Vector3 n;
            if (fx + fz <= 1f)
            {
                y = v00.y + (v10.y - v00.y) * fx + (v01.y - v00.y) * fz;
                n = Vector3.Cross(v01 - v00, v10 - v00);
            }
            else
            {
                Vector3 v11 = Vertex(t, ix + 1, iz + 1);
                y = v11.y + (v01.y - v11.y) * (1f - fx) + (v10.y - v11.y) * (1f - fz);
                n = Vector3.Cross(v10 - v11, v01 - v11);
            }
            if (n.y < 0f) n = -n;
            normal = n.normalized;
            return true;
        }

        // ZoneSystem.GetGroundHeight: the point's own height when the ray misses
        private static float GroundHeight(ZoneTerrain t, Vector3 p) => Ground(t, p, out float y, out _) ? y : p.y;

        // ZoneSystem.GetTerrainDelta (10 random samples within the radius)
        private static float TerrainDelta(URandom r, ZoneTerrain t, Vector3 center, float radius)
        {
            float hi = -999999f, lo = 999999f;
            for (int i = 0; i < 10; i++)
            {
                Vector2 v = r.insideUnitCircle * radius;
                float h = GroundHeight(t, center + new Vector3(v.x, 0f, v.y));
                if (h < lo) lo = h;
                if (h > hi) hi = h;
            }
            return hi - lo;
        }

        private static float Weight(float x, float y, float rx, float ry)
        {
            float dx = (float)((double)x - rx), dy = (float)((double)y - ry);
            float d = Mathf.Sqrt((float)((double)dx * dx + (double)dy * dy));
            float w = (float)(1.4140000343322754 - d);
            return (float)((double)w * w * w);
        }

        [ThreadStatic] private static float[] weights;

        // Heightmap.GetBiome
        private static Heightmap.Biome GetBiome(ZoneTerrain t, Vector3 point)
        {
            var cb = t.corners;
            if (cb[0].Biome == cb[1].Biome && cb[0].Biome == cb[2].Biome && cb[0].Biome == cb[3].Biome) return cb[0].Biome;
            float size = hmWidth * hmScale;
            float x = (point.x - t.center.x) / size + 0.5f, y = (point.z - t.center.z) / size + 0.5f;
            var w = weights ?? (weights = new float[10]);
            for (int i = 1; i < w.Length; i++) w[i] = 0f;
            w[cb[0].Biome.ToIndex()] += Weight(x, y, 0f, 0f);
            w[cb[1].Biome.ToIndex()] += Weight(x, y, 1f, 0f);
            w[cb[2].Biome.ToIndex()] += Weight(x, y, 0f, 1f);
            w[cb[3].Biome.ToIndex()] += Weight(x, y, 1f, 1f);
            int best = 0; float bw = -99999f;
            for (int i = 1; i < w.Length; i++) if (w[i] > bw) { best = i; bw = w[i]; }
            return ((Heightmap.BiomeIndex)best).ToBiome();
        }

        // Heightmap.GetBiomeArea
        private static Heightmap.BiomeArea BiomeArea(ZoneTerrain t)
        {
            var cb = t.corners;
            bool edge = !(cb[0].Biome == cb[1].Biome && cb[0].Biome == cb[2].Biome && cb[0].Biome == cb[3].Biome);
            return edge ? Heightmap.BiomeArea.Edge : Heightmap.BiomeArea.Median;
        }

        // Heightmap.GetVegetationMask: the paint mask's alpha (the base mask: no location modifiers in Ghost mode)
        private static float VegetationMask(ZoneTerrain t, Vector3 p)
        {
            int n = hmWidth + 1, half = n / 2;
            float vx = p.x - 0.5f - t.center.x, vz = p.z - 0.5f - t.center.z;
            int x = Mathf.Clamp(Mathf.FloorToInt(vx / hmScale + 0.5f + half), 0, n - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(vz / hmScale + 0.5f + half), 0, n - 1);
            return t.MaskA(x, y);
        }

        // Heightmap.GetOceanDepth (UpdateCornerDepths, WorldToVertex)
        private static float OceanDepth(ZoneTerrain t, Vector3 p)
        {
            float d0 = Mathf.Max(0f, 30f - t.H(0, hmWidth)), d1 = Mathf.Max(0f, 30f - t.H(hmWidth, hmWidth));
            float d2 = Mathf.Max(0f, 30f - t.H(hmWidth, 0)), d3 = Mathf.Max(0f, 30f - t.H(0, 0));
            int half = hmWidth / 2;
            int x = Mathf.FloorToInt((p.x - t.center.x) / hmScale + 0.5f) + half;
            int y = Mathf.FloorToInt((p.z - t.center.z) / hmScale + 0.5f) + half;
            float tx = (float)((double)x / (double)(float)hmWidth), ty = (float)y / (float)hmWidth;
            float a = DUtils.Lerp(d3, d2, tx), b = DUtils.Lerp(d0, d1, tx);
            return DUtils.Lerp(a, b, ty);
        }

        // PlaceLocations' clear area for the zone's location (from the world's location plan)
        private static bool InsideClearArea(Vector2s zone, Vector3 p)
        {
            if (!clearAreas.TryGetValue(zone, out var ca)) return false;
            Vector3 c = ca.pos; float r = ca.r;
            return p.x > c.x - r && p.x < c.x + r && p.z > c.z - r && p.z < c.z + r;
        }

        // ---- the file ----

        // the deposits already worked out and the zones done (by index in the zone order, a bitmap in the
        // header), when the file was made by this version for this world, game version and settings;
        // nothing otherwise
        private static void Resume(string path, string seed, string game, List<Found> found, bool[] done)
        {
            if (!File.Exists(path)) return;
            try
            {
                var head = new Dictionary<string, string>();
                var rows = new List<Found>();
                foreach (var raw in File.ReadLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == '#')
                    {
                        int c = line.IndexOf(':');
                        if (c > 0) head[line.Substring(1, c - 1).Trim()] = line.Substring(c + 1).Trim();
                        continue;
                    }
                    if (line.StartsWith("prefab,", StringComparison.Ordinal)) continue;
                    var p = line.Split(',');
                    if (p.Length < 6) continue;
                    rows.Add(new Found
                    {
                        prefab = p[0],
                        p = new Vector3(float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture), float.Parse(p[3], CultureInfo.InvariantCulture)),
                        zone = new Vector2s(int.Parse(p[4]), int.Parse(p[5]))
                    });
                }
                if (Get(head, "seed") != seed || Get(head, "game_version") != game || Get(head, "inplace_format") != FormatVersion.ToString()
                    || Get(head, "blocking") != blockMode || Get(head, "all_zones") != (allZones ? "1" : "0") || Get(head, "locations") != (locations ? "1" : "0")
                    || Get(head, "done") == null) return;
                var bits = Convert.FromBase64String(Get(head, "done"));
                if (bits.Length != (done.Length + 7) / 8) return;
                for (int i = 0; i < done.Length; i++) done[i] = (bits[i >> 3] & (1 << (i & 7))) != 0;
                found.AddRange(rows);
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: in-place ore: " + path + " unreadable, starting again: " + e.Message); Array.Clear(done, 0, done.Length); }
        }

        private static string Get(Dictionary<string, string> d, string k) => d.TryGetValue(k, out var v) ? v : null;

        // TASK-4.7 renamed the file. An in-place file under the old name takes the new name (so a run in
        // progress resumes), unless an in-place file is already there; a file under the new name that
        // in-place didn't write (an extract from the removed file and server modes) is replaced by it,
        // and otherwise by the run, since Resume ignores a file without in-place's header.
        private static void AdoptOldFile(string path, string oldPath)
        {
            try
            {
                if (!File.Exists(oldPath)) return;
                bool current = File.Exists(path) && File.ReadLines(path).Take(8).Any(l => l.Trim() == "# method: in-place");
                if (current) { File.Delete(oldPath); return; }
                if (File.Exists(path)) File.Delete(path);
                File.Move(oldPath, path);
                ZLog.Log("MuninMap: in-place ore: " + OldFileName + " renamed to " + FileName);
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: in-place ore: renaming " + oldPath + ": " + e.Message); }
        }

        // written to a temporary name and moved into place, so PredictedOre never reads half a file
        private static System.Threading.Tasks.Task writing;

        // worker thread
        private static void Write(string path, string seed, string game, Found[] found, bool[] doneSet, int done, bool complete)
        {
            try
            {
                var sb = new StringBuilder(64 * found.Length + 256);
                sb.Append("# seed: ").Append(seed).Append('\n');
                sb.Append("# game_version: ").Append(game).Append('\n');
                sb.Append("# method: in-place\n");
                sb.Append("# inplace_format: ").Append(FormatVersion).Append('\n');
                sb.Append("# blocking: ").Append(blockMode).Append('\n');
                sb.Append("# all_zones: ").Append(allZones ? "1" : "0").Append('\n');
                sb.Append("# locations: ").Append(locations ? "1" : "0").Append('\n');
                sb.Append("# zones_done: ").Append(done).Append('\n');
                sb.Append("# complete: ").Append(complete ? "1" : "0").Append('\n');
                var bits = new byte[(doneSet.Length + 7) / 8];
                for (int i = 0; i < doneSet.Length; i++) if (doneSet[i]) bits[i >> 3] |= (byte)(1 << (i & 7));
                sb.Append("# done: ").Append(Convert.ToBase64String(bits)).Append('\n');
                sb.Append("prefab,x,y,z,zx,zy\n");
                foreach (var f in found)
                    sb.Append(f.prefab).Append(',').Append(f.p.x.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                      .Append(f.p.y.ToString("F2", CultureInfo.InvariantCulture)).Append(',').Append(f.p.z.ToString("F2", CultureInfo.InvariantCulture))
                      .Append(',').Append(f.zone.x).Append(',').Append(f.zone.y).Append('\n');
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString());
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: in-place ore: writing " + path + ": " + e.Message); }
        }
    }
}
