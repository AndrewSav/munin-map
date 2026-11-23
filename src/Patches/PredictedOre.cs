// patches: TASK-4 (TASK-4.4). Predicted ore deposits from an extract file (fork only, see PATCHES.md).
//
// With predict_ore = file and reveal_all on, Munin reads the extract ore-extract wrote for this world
// (<world data folder>/predicted-ore.csv: a '#' header, then prefab,x,y,z,zx,zy per deposit) and
// serves its deposits as data/predicted-ore.json for the page to draw as predicted (web/js/
// ore-layer.js). A zone the game has generated shows its real deposits instead: the ones generated
// before the server started are dropped at load; one generated while it runs keeps its predictions
// until Munin's sweep has its real objects (at most 5 minutes), so the map never shows neither. A viewer without the page's spoiler view gets only
// deposits on explored ground (in practice none: explored ground is generated), like other game
// content; the rest is not sent at all.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using MuninMap.Tiles;
using MuninMap.Util;
using MuninMap.World;
using UnityEngine;
using WebSocketSharp.Server;

namespace MuninMap.Patches
{
    internal static class PredictedOre
    {
        public const string FileName = "predicted-ore.csv";
        public static string Mode = "off";   // off | file (server and in-place come with TASK-4.5 and 4.6)

        private struct Deposit { public byte kind; public int x, z; public Vector2s zone; }

        private static string worldDir;
        private static DateTime fileTime = DateTime.MinValue;
        private static long fileSize = -1;
        private static Deposit[] all = new Deposit[0];      // what the file holds (for this world)
        private static Deposit[] shown = new Deposit[0];    // minus generated zones; replaced whole, read by the HTTP threads
        private static int generatedCount = -1;
        private static int vegVersion = int.MinValue;
        // zones generated while the server runs, and when (realtime): their predictions stay until Munin's
        // sweep has their real objects, so the map never shows neither (the player's game generates a zone
        // and sends its objects over the next minutes; the sweep runs every sweep_interval)
        private static readonly Dictionary<Vector2s, float> genSince = new Dictionary<Vector2s, float>();
        private static HashSet<Vector2s> knownGen;
        private const float Grace = 300f;   // after this, a generated zone's predictions go anyway
        private static int rev;
        private static string status = "no file";            // shown in the page's server details
        private static string madeFor, gameNow;               // the game versions the extract was made for and the server runs
        private static bool started;
        private static bool missingLogged;   // the missing file is logged once, until it appears

        public static bool On => Mode == "file" && MuninConfig.REVEAL_ALL;

        public static void Bind(ConfigFile config)
        {
            Mode = config.Bind("Server", "predict_ore", Mode, new ConfigDescription(
                "Show predicted ore deposits in zones nobody has visited yet (needs reveal_all = true). "
                + "off: none. file: from an extract made with ore-extract and copied into this world's Munin data folder as "
                + FileName + " (see docs/ADVANCED.md, Predicted ore).",
                new AcceptableValueList<string>("off", "file"))).Value;
            if (Mode != "file") Mode = "off";
        }

        public static void AddTo(Dictionary<string, object> config)
        {
            if (On) config["predict_ore"] = Mode;
        }

        // per world, from MuninMap.NewWorld (main thread)
        public static void Load(string worldDataPath)
        {
            worldDir = worldDataPath;
            fileTime = DateTime.MinValue; fileSize = -1; generatedCount = -1; vegVersion = int.MinValue;
            genSince.Clear(); knownGen = null;
            all = new Deposit[0]; shown = new Deposit[0]; status = "no file"; madeFor = null;
            missingLogged = false;
            if (!On) return;   // reveal_all off or predict_ore off: nothing read
            if (!started) { started = true; StaticCoroutine.Start(Watch()); }
        }

        // the file changed (a new extract copied in), the game generated zones, or a sweep published zones:
        // checked every 2 s until the world has loaded, then every 5 s (comparing a few numbers; the work
        // runs only when one changed)
        private static IEnumerator Watch()
        {
            var soon = new WaitForSecondsRealtime(2f);
            var later = new WaitForSecondsRealtime(5f);
            while (true)
            {
                yield return WorldReady() ? later : soon;
                var check = Check();
                while (true)
                {
                    object step;
                    try { if (!check.MoveNext()) break; step = check.Current; }
                    catch (Exception e) { ZLog.LogWarning("MuninMap: predicted ore: " + e.Message); break; }
                    yield return step;
                }
            }
        }

        // runs `work` on a worker thread; the coroutine waits a frame at a time until it is done
        private static IEnumerator OffThread(Action work)
        {
            var t = System.Threading.Tasks.Task.Run(work);
            while (!t.IsCompleted) yield return null;
            if (t.IsFaulted) ZLog.LogWarning("MuninMap: predicted ore: " + t.Exception?.GetBaseException().Message);
        }

        // The save's generated zones are read with its locations: until then the game's list is empty and
        // every prediction would show, generated zones included. LocationsGenerated turns true once the
        // save is loaded (or, for a new world, once its locations are planned).
        private static bool WorldReady()
        {
            try { return ZoneSystem.instance != null && ZoneSystem.instance.LocationsGenerated; } catch { return false; }
        }

        // Main thread, a coroutine: reading the file and checking the deposits against the generated zones
        // run on a worker thread (the game's set is copied first: it isn't thread-safe); nothing else
        // writes these fields meanwhile, as the checks run one after another
        private static IEnumerator Check()
        {
            if (worldDir == null || !On) yield break;
            bool changed = false;
            string path = Path.Combine(worldDir, FileName);
            var fi = new FileInfo(path);
            if (!fi.Exists)
            {
                if (fileSize != -1 || all.Length > 0) { all = new Deposit[0]; status = "no file"; madeFor = null; changed = true; }
                if (!missingLogged) { missingLogged = true; ZLog.Log("MuninMap: predicted ore: no " + FileName + " in " + worldDir + " (predict_ore = file); nothing shown"); }
                fileSize = -1; fileTime = DateTime.MinValue;
            }
            else if (fi.LastWriteTimeUtc != fileTime || fi.Length != fileSize)
            {
                fileTime = fi.LastWriteTimeUtc; fileSize = fi.Length;
                missingLogged = false;
                gameNow = global::Version.CurrentVersion.ToString();
                string seedHere = null;
                try { seedHere = WorldGenerator.instance?.m_world?.m_seedName ?? ZNet.World?.m_seedName; } catch { }
                yield return OffThread(() => Read(path, seedHere));
                changed = true;
            }
            if (!WorldReady())
            {
                if (shown.Length > 0 || status != "waiting for the world to load") { shown = new Deposit[0]; changed = true; }
                if (all.Length > 0) status = "waiting for the world to load";
                generatedCount = -1; knownGen = null; genSince.Clear();
                if (changed) { rev++; MapDataServer.getInstance()?.BroadcastWorldRevision(); }
                yield break;
            }
            if (all.Length > 0 && status == "waiting for the world to load") status = "ok";
            int count = Generated()?.Count ?? -1;
            int veg = OreLayer.VegetationVersion;
            float now = Time.realtimeSinceStartup;
            bool expired = false;
            foreach (var t in genSince.Values) if (now - t >= Grace) { expired = true; break; }
            if (changed || count != generatedCount || veg != vegVersion || expired)
            {
                bool first = changed || generatedCount == -1;
                generatedCount = count; vegVersion = veg;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var gen = Generated();
                var copy = gen != null ? new HashSet<Vector2s>(gen) : null;
                // zones new since the last look start their wait; the ones generated before the first look
                // (the save's) never wait
                if (copy != null)
                {
                    if (knownGen != null) foreach (var z in copy) if (!knownGen.Contains(z) && !genSince.ContainsKey(z)) genSince[z] = now;
                    knownGen = copy;
                }
                var done = new List<Vector2s>();
                foreach (var kv in genSince) if (now - kv.Value >= Grace) done.Add(kv.Key);
                foreach (var z in done) genSince.Remove(z);
                var waiting = new HashSet<Vector2s>(genSince.Keys);
                double copyMs = sw.Elapsed.TotalMilliseconds;
                if (copyMs > 5) ZLog.LogWarning($"MuninMap: predicted ore: copying the {copy?.Count} generated zones took {copyMs:F1} ms");
                int before = shown.Length;
                yield return OffThread(() => Prune(copy, waiting, first, copyMs));
                if (changed || shown.Length != before)
                {
                    rev++;
                    MapDataServer.getInstance()?.BroadcastWorldRevision();
                }
            }
        }

        private static HashSet<Vector2s> Generated()
        {
            try { return ZoneSystem.instance?.m_generatedZones; } catch { return null; }
        }

        // worker thread: drop deposits in zones the game has generated (`gen`: a copy of its set), except
        // zones generated lately (`waiting`) whose real objects Munin's sweep hasn't published yet
        // (Vegetation's tables are concurrent: safe to read here)
        private static void Prune(HashSet<Vector2s> gen, HashSet<Vector2s> waiting, bool afterRead, double copyMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var keep = new List<Deposit>(all.Length);
            foreach (var d in all)
            {
                bool generated = gen != null && gen.Contains(d.zone);
                if (generated && waiting.Contains(d.zone) && Vegetation.Zone(d.zone.x, d.zone.y) == null) generated = false;
                if (!generated) keep.Add(d);
            }
            shown = keep.ToArray();
            if (afterRead && all.Length > 0)
                ZLog.Log($"MuninMap: predicted ore: {shown.Length} of {all.Length} deposits are in zones not generated yet "
                    + $"(game thread {copyMs:F2} ms to copy {gen?.Count ?? 0} generated zones, worker {sw.Elapsed.TotalMilliseconds:F2} ms)");
        }

        // worker thread
        private static void Read(string path, string seedHere)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            all = new Deposit[0]; madeFor = null;
            string seedThere = null;
            var list = new List<Deposit>();
            int bad = 0;
            try
            {
                foreach (var raw in File.ReadLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == '#')
                    {
                        int c = line.IndexOf(':');
                        if (c < 0) continue;
                        string key = line.Substring(1, c - 1).Trim(), val = line.Substring(c + 1).Trim();
                        if (key == "seed") seedThere = val;
                        else if (key == "game_version") madeFor = val;
                        continue;
                    }
                    if (line.StartsWith("prefab,", StringComparison.Ordinal)) continue;
                    var p = line.Split(',');
                    if (p.Length < 6 || !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                        || !float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
                        || !int.TryParse(p[4], out int zx) || !int.TryParse(p[5], out int zy)) { bad++; continue; }
                    byte kind = Kind(p[0]);
                    if (kind == 0) continue;
                    list.Add(new Deposit { kind = kind, x = Mathf.RoundToInt(x), z = Mathf.RoundToInt(z), zone = new Vector2s(zx, zy) });
                }
            }
            catch (Exception e)
            {
                status = "unreadable (" + e.Message + ")";
                ZLog.LogWarning("MuninMap: predicted ore: " + path + " unreadable: " + e.Message);
                return;
            }
            if (seedThere == null || madeFor == null)
            {
                status = "not an ore-extract file (no seed or game version in its header)";
                ZLog.LogWarning("MuninMap: predicted ore: " + path + " is not an ore-extract file (no seed or game version in its header); nothing shown");
                madeFor = null;
                return;
            }
            if (seedHere != null && seedThere != seedHere)
            {
                status = "made for another world";
                ZLog.LogWarning("MuninMap: predicted ore: " + path + " was made for another world (another seed); nothing shown");
                madeFor = null;
                return;
            }
            all = list.ToArray();
            status = "ok";
            ZLog.Log($"MuninMap: predicted ore: {all.Length} deposits read from {FileName} (made for Valheim {madeFor}"
                + (madeFor != gameNow ? $", the server runs {gameNow}" : "") + (bad > 0 ? $"; {bad} unreadable lines skipped" : "")
                + $"; {sw.Elapsed.TotalMilliseconds:F0} ms on a worker thread)");
        }

        // the kinds real ore is drawn with (Tiles/Vegetation.cs ClassifyName, Tiles/Palette.cs Veg)
        private static byte Kind(string prefab)
        {
            string n = prefab.ToLowerInvariant();
            if (n.Contains("silvervein")) return (byte)Palette.Veg.Silver;
            if (n.Contains("mudpile")) return (byte)Palette.Veg.MuddyScrap;
            if (n.Contains("_copper")) return (byte)Palette.Veg.Copper;
            if (n.Contains("_tin")) return (byte)Palette.Veg.Tin;
            if (n.Contains("obsidian")) return (byte)Palette.Veg.Obsidian;
            if (n.Contains("minerock") || n.Contains("meteorite")) return (byte)Palette.Veg.Ore;
            return 0;
        }

        // Before upstream's routing (from Access.Handle): true when this request was answered here
        public static bool Handle(HttpRequestEventArgs e, string path, bool post)
        {
            if (path != "/data/predicted-ore.json") return false;
            if (post) return false;
            if (!On) return Access.Json(e, 200, "{\"on\":false}");
            bool all = Spoilers.Requested;
            var d = shown;
            var j = new JsonWriter(64 + d.Length * 16).BeginObject()
                .Prop("on", true).Prop("rev", rev).Prop("status", status);
            if (madeFor != null) j.Prop("madeFor", madeFor);
            if (gameNow != null) j.Prop("game", gameNow);
            j.Prop("mismatch", madeFor != null && gameNow != null && madeFor != gameNow);
            j.Prop("total", d.Length);
            // [kind, x, z, kind, x, z, ...]: only explored ground for a viewer without spoilers
            var sb = new System.Text.StringBuilder(d.Length * 14);
            sb.Append('[');
            int n = 0;
            foreach (var dep in d)
            {
                if (!all && !Fog.IsExplored(dep.x, dep.z)) continue;
                if (n++ > 0) sb.Append(',');
                sb.Append(dep.kind).Append(',').Append(dep.x).Append(',').Append(dep.z);
            }
            sb.Append(']');
            j.PropRaw("d", sb.ToString());
            return Access.Json(e, 200, j.End().ToString());
        }
    }
}
