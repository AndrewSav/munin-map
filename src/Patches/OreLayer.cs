// patches: TASK-4 (TASK-4.4). Real ore deposits as one list, for the page's ore layer (fork only,
// see PATCHES.md).
//
// The page draws all ore, real and predicted, in one layer and one style (web/js/patches/ore-layer.js), so
// it needs the real deposits as points: upstream only sends them packed with the rest of the
// vegetation for its GPU renderer. data/ore.json lists the ore kinds of Vegetation's published
// points (read through AccessTools: upstream keeps them private), rebuilt when Vegetation's version
// changes. A viewer gets explored ground only, unless reveal_all is on and the viewer asked for the
// spoiler view: what upstream's vegetation shows, with the page's spoiler filter applied here.
// Ore inside dungeons (Sunken Crypts' muddy scrap: interiors are built 5,000 m up) comes in a list
// of its own, "i": the page draws it in the classic style only (with the user, TASK-4.4 notes).
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MuninMap.Tiles;
using MuninMap.World;
using WebSocketSharp.Server;

namespace MuninMap.Patches
{
    internal static class OreLayer
    {
        private static readonly FieldInfo zonesField = AccessTools.Field(typeof(Vegetation), "zones");
        private static readonly FieldInfo versionField = AccessTools.Field(typeof(Vegetation), "version");
        private static readonly object gate = new object();
        private static int builtFor = int.MinValue;
        private static int[] points = new int[0];   // kind, x, z, kind, x, z, ...
        private static int[] inside = new int[0];   // the same, for ore inside dungeons
        private const float Interior = 1000f;       // dungeon interiors are built at y + 5000

        // Vegetation's revision: it changes when a sweep publishes changed zones (PredictedOre watches it)
        internal static int VegetationVersion => versionField != null ? (int)versionField.GetValue(null) : 0;

        private static bool IsOre(Palette.Veg k) => k == Palette.Veg.Copper || k == Palette.Veg.Tin || k == Palette.Veg.Silver
            || k == Palette.Veg.Obsidian || k == Palette.Veg.MuddyScrap || k == Palette.Veg.Ore;

        private static void Current(out int version, out int[] outside, out int[] indoors)
        {
            version = versionField != null ? (int)versionField.GetValue(null) : 0;
            lock (gate)
            {
                if (version != builtFor)
                {
                    var list = new List<int>(4096);
                    var ins = new List<int>(1024);
                    if (zonesField?.GetValue(null) is ConcurrentDictionary<long, Vegetation.Point[]> zones)
                        foreach (var kv in zones)
                            foreach (var p in kv.Value)
                            {
                                if (!IsOre(p.kind)) continue;
                                var l = p.y < Interior ? list : ins;
                                l.Add((int)p.kind); l.Add(UnityEngine.Mathf.RoundToInt(p.x)); l.Add(UnityEngine.Mathf.RoundToInt(p.z));
                            }
                    points = list.ToArray();
                    inside = ins.ToArray();
                    builtFor = version;
                }
                outside = points; indoors = inside;
            }
        }

        private static void Append(System.Text.StringBuilder sb, int[] pts, bool all)
        {
            int n = 0;
            for (int i = 0; i + 2 < pts.Length; i += 3)
            {
                if (!all && !Fog.IsExplored(pts[i + 1], pts[i + 2])) continue;
                if (n++ > 0) sb.Append(',');
                sb.Append(pts[i]).Append(',').Append(pts[i + 1]).Append(',').Append(pts[i + 2]);
            }
        }

        // Before upstream's routing (from Access.Handle): true when this request was answered here
        public static bool Handle(HttpRequestEventArgs e, string path, bool post)
        {
            if (path != "/data/ore.json") return false;
            if (post) return false;
            Current(out int version, out var pts, out var ins);
            bool all = Spoilers.Requested;   // reveal_all and the viewer's spoiler view
            var sb = new System.Text.StringBuilder(64 + (pts.Length + ins.Length) * 5);
            sb.Append("{\"rev\":").Append(unchecked(version * 1000003 + Fog.ExploredCells) & 0x7fffffff).Append(",\"d\":[");
            Append(sb, pts, all);
            sb.Append("],\"i\":[");
            Append(sb, ins, all);
            sb.Append("]}");
            return Access.Json(e, 200, sb.ToString());
        }
    }
}
