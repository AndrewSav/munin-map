// patches: TASK-20. Who renamed or hid a base, and when (fork only, see PATCHES.md).
//
// Upstream keeps base overrides (renamed, hidden) in bases.json and its saver writes only x, z,
// label and hidden, so who and when live here instead, per world in bases-edits.json: one record
// per override, not a history, matched to it by position the way upstream matches an override to
// a base (World/Markers.cs MATCH_RADIUS). Who: the claimed player, "admin" for the admin token,
// "web" otherwise (require_auth off: nobody is known).
//
// Upstream drops a base's custom name when it is hidden (the page sends only hidden), so the record
// keeps the last custom name and who chose it: GET api/bases shows it as previousLabel, and the
// admin can put it back with POST /api/base {x, z, label}.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MuninMap.Util;

namespace MuninMap.Patches
{
    internal static class BaseEdits
    {
        private const float MatchRadius = 60f;   // World/Markers.cs MATCH_RADIUS

        private sealed class Rec { public float x, z; public string label, renamedBy, renamedAt, hiddenBy, hiddenAt; }

        private static readonly object gate = new object();
        private static string loadedFrom;
        private static List<Rec> recs = new List<Rec>();

        private static string PathNow => MuninMap.worldDataPath == null ? null : Path.Combine(MuninMap.worldDataPath, "bases-edits.json");
        private static string Now => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        private static void Load()
        {
            string path = PathNow;
            if (path == loadedFrom) return;
            loadedFrom = path; recs = new List<Rec>();
            try
            {
                if (path == null || !File.Exists(path)) return;
                var list = JsonParser.Arr(JsonParser.ParseObject(File.ReadAllText(path)), "bases");
                if (list != null)
                    foreach (var o in list)
                        if (o is Dictionary<string, object> d)
                            recs.Add(new Rec { x = (float)JsonParser.Num(d, "x"), z = (float)JsonParser.Num(d, "z"), label = JsonParser.Str(d, "label", null),
                                renamedBy = JsonParser.Str(d, "renamedBy", null), renamedAt = JsonParser.Str(d, "renamedAt", null),
                                hiddenBy = JsonParser.Str(d, "hiddenBy", null), hiddenAt = JsonParser.Str(d, "hiddenAt", null) });
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: bases-edits.json not readable: " + e.Message); }
        }

        private static void Save()
        {
            string path = PathNow;
            if (path == null) return;
            try
            {
                var j = new JsonWriter(512);
                j.BeginObject().Key("bases").BeginArray();
                foreach (var r in recs)
                {
                    j.BeginObject().Prop("x", r.x, 1).Prop("z", r.z, 1);
                    if (r.label != null) j.Prop("label", r.label);
                    if (r.renamedBy != null) j.Prop("renamedBy", r.renamedBy).Prop("renamedAt", r.renamedAt);
                    if (r.hiddenBy != null) j.Prop("hiddenBy", r.hiddenBy).Prop("hiddenAt", r.hiddenAt);
                    j.End();
                }
                j.End().End();
                File.WriteAllText(path, j.ToString());
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: could not save bases-edits.json: " + e.Message); }
        }

        private static Rec Find(float x, float z)
        {
            Rec best = null; float bd = MatchRadius * MatchRadius;
            foreach (var r in recs)
            {
                float d = (r.x - x) * (r.x - x) + (r.z - z) * (r.z - z);
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        // after Markers.SetBase(x, z, label, hidden), with the same values: label null and hidden false
        // put the base back to its automatic name and upstream dropped the override, so the record goes too
        public static void Record(string who, float x, float z, string label, bool hidden)
        {
            lock (gate)
            {
                Load();
                var r = Find(x, z);
                if (label == null && !hidden) { if (r != null) { recs.Remove(r); Save(); } return; }
                if (r == null) { r = new Rec(); recs.Add(r); }
                r.x = x; r.z = z;
                if (label != null) { r.label = label; r.renamedBy = who; r.renamedAt = Now; }   // (hidden with no label: the last name and who chose it are kept)
                if (hidden) { if (r.hiddenBy == null) { r.hiddenBy = who; r.hiddenAt = Now; } } else { r.hiddenBy = r.hiddenAt = null; }
                Save();
            }
        }

        // a renamed base's marker: who renamed it, when known (Markers' base list, game thread)
        public static void Write(JsonWriter j, float x, float z)
        {
            string by;
            lock (gate) { Load(); by = Find(x, z)?.renamedBy; }
            if (by != null) j.Prop("renamedBy", by);
        }

        // GET api/bases: upstream's overrides as bases.json has them, each with its who and when
        public static string ListJson()
        {
            var j = new JsonWriter(1024);
            j.BeginArray();
            try
            {
                string path = MuninMap.worldDataPath == null ? null : Path.Combine(MuninMap.worldDataPath, "bases.json");
                var list = path != null && File.Exists(path) ? JsonParser.Arr(JsonParser.ParseObject(File.ReadAllText(path)), "bases") : null;
                lock (gate)
                {
                    Load();
                    if (list != null)
                        foreach (var o in list)
                        {
                            if (!(o is Dictionary<string, object> d)) continue;
                            float x = (float)JsonParser.Num(d, "x"), z = (float)JsonParser.Num(d, "z");
                            string label = JsonParser.Str(d, "label", null);
                            bool hidden = JsonParser.Bool(d, "hidden");
                            var r = Find(x, z);
                            j.BeginObject().Prop("x", x, 1).Prop("z", z, 1);
                            if (label != null) j.Prop("label", label);
                            j.Prop("hidden", hidden);
                            if (label == null && r?.label != null) j.Prop("previousLabel", r.label);
                            if (label != null || r?.label != null) j.Prop("renamedBy", r?.renamedBy ?? "unknown").Prop("renamedAt", r?.renamedAt ?? "unknown");
                            if (hidden) j.Prop("hiddenBy", r?.hiddenBy ?? "unknown").Prop("hiddenAt", r?.hiddenAt ?? "unknown");
                            j.End();
                        }
                }
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: bases list: " + e.Message); }
            j.End();
            return j.ToString();
        }
    }
}
