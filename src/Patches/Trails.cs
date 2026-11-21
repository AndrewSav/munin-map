// patches: TASK-3. Player trails kept across server restarts (fork only, see PATCHES.md).
//
// Part of Activity (a partial class) so it can read the trails as they are, with no copy of their
// shape. Activity.Load and Activity.Save each make one call in here. The file is trails.txt beside
// activity.txt, one block per player:
//   p<TAB>key<TAB>name
//   unix-seconds x z        (one line per point, oldest first)
// Saves ride on stats_save_interval, through Activity.Save. On the game's thread only the trails are
// copied; the text is made and written on a worker thread, to a temp file renamed over the old one,
// so a crash mid-write leaves the last good file. The save at shutdown is written in place, so the
// process cannot exit before it is on disk.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using HarmonyLib;
using MuninMap.Util;

namespace MuninMap.Live
{
    internal static partial class Activity
    {
        private sealed class TrailCopy { public string key, name; public int[] t; public float[] x, z; }

        private static string trailsPath;
        private static long trailsSig;            // the trails as last saved (point count and newest time)
        private static int trailsSeq, trailsWritten;
        private static readonly object trailsWrite = new object();
        private static volatile bool shuttingDown;

        // Load, inside its lock, right after it emptied the trails
        private static void LoadTrails(string worldDir)
        {
            trailsPath = Path.Combine(worldDir, "trails.txt");
            shuttingDown = false;
            int players = 0, points = 0;
            lock (sync)
            {
                trailsSig = TrailsSig();   // nothing to save until a trail changes
                try
                {
                    if (!File.Exists(trailsPath)) { ZLog.LogWarning("MuninMap: no trails.txt yet, starting with no trails"); return; }
                    int from = Now() - TrailSeconds;
                    Trail tr = null;
                    foreach (var line in File.ReadAllLines(trailsPath))
                    {
                        if (line.Length == 0) continue;
                        if (line[0] == 'p')
                        {
                            var h = line.Split('\t');
                            tr = null;
                            if (h.Length < 3 || h[0] != "p" || h[1].Length == 0) continue;
                            // a player hidden since (hidden_players) is not brought back
                            if (MuninConfig.IsHidden(h[2]) || trails.ContainsKey(h[1])) continue;
                            trails[h[1]] = tr = new Trail { name = h[2] };
                            continue;
                        }
                        if (tr == null) continue;
                        var p = line.Split(' ');
                        if (p.Length != 3 || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int t)
                            || !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                            || !float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) continue;
                        // aged out, or out of order (the page's search needs them oldest first)
                        if (t < from || (tr.t.Count > 0 && t < tr.t[tr.t.Count - 1])) continue;
                        tr.t.Add(t); tr.x.Add(x); tr.z.Add(z);
                    }
                    var empty = new List<string>();
                    foreach (var kv in trails)
                    {
                        var r = kv.Value;
                        if (r.t.Count == 0) { empty.Add(kv.Key); continue; }
                        if (r.t.Count > MaxPoints) { int cut = r.t.Count - MaxPoints; r.t.RemoveRange(0, cut); r.x.RemoveRange(0, cut); r.z.RemoveRange(0, cut); }
                        players++; points += r.t.Count;
                    }
                    foreach (var k in empty) trails.Remove(k);
                    trailsSig = TrailsSig();
                    if (players == 0) ZLog.LogWarning("MuninMap: trails.txt holds no trails from the last 24 hours, starting with none");
                    else ZLog.Log("MuninMap: trails restored, " + players + " players, " + points + " points");
                }
                catch (Exception e)
                {
                    trails.Clear();
                    trailsSig = TrailsSig();   // the unreadable file stays until there is a trail to save
                    ZLog.LogWarning("MuninMap: trails.txt not readable, starting with no trails: " + e.Message);
                }
            }
        }

        // what has to change for the trails to be worth saving again: a new point, or the last one moved up
        private static long TrailsSig()
        {
            long n = 0, newest = 0;
            foreach (var tr in trails.Values) { n += tr.t.Count; if (tr.t.Count > 0) newest = Math.Max(newest, tr.t[tr.t.Count - 1]); }
            return (n << 32) ^ newest;
        }

        // top of Save (every stats_save_interval, and at shutdown)
        private static void SaveTrails()
        {
            if (trailsPath == null) return;
            long t0 = Perf.Start();
            List<TrailCopy> copy;
            int seq;
            lock (sync)
            {
                long sig = TrailsSig();
                if (sig == trailsSig) { Perf.Stop("trails save", t0); return; }
                copy = new List<TrailCopy>(trails.Count);
                foreach (var kv in trails)
                    copy.Add(new TrailCopy { key = kv.Key, name = kv.Value.name, t = kv.Value.t.ToArray(), x = kv.Value.x.ToArray(), z = kv.Value.z.ToArray() });
                trailsSig = sig;
                seq = ++trailsSeq;
            }
            string path = trailsPath;
            Perf.Stop("trails save", t0);
            if (shuttingDown) WriteTrails(path, copy, seq);
            else ThreadPool.QueueUserWorkItem(_ => WriteTrails(path, copy, seq));
        }

        private static void WriteTrails(string path, List<TrailCopy> copy, int seq)
        {
            lock (trailsWrite)
            {
                if (seq <= trailsWritten) return;   // a newer copy is already on disk
                try
                {
                    var sb = new StringBuilder(64 + copy.Count * 64);
                    foreach (var tr in copy)
                    {
                        sb.Append("p\t").Append(Clean(tr.key)).Append('\t').Append(Clean(tr.name)).Append('\n');
                        for (int i = 0; i < tr.t.Length; i++)
                            sb.Append(tr.t[i].ToString(CultureInfo.InvariantCulture)).Append(' ')
                              .Append(tr.x[i].ToString("0.#", CultureInfo.InvariantCulture)).Append(' ')
                              .Append(tr.z[i].ToString("0.#", CultureInfo.InvariantCulture)).Append('\n');
                    }
                    string tmp = path + ".tmp";
                    File.WriteAllText(tmp, sb.ToString());
                    if (File.Exists(path)) File.Replace(tmp, path, null);
                    else File.Move(tmp, path);
                    trailsWritten = seq;
                }
                catch (Exception e) { ZLog.LogWarning("MuninMap: could not write trails.txt: " + e.Message); }
            }
        }

        private static string Clean(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        // before the game's own shutdown saves (Activity.Save among them): write the last one in place
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
        private static class TrailsShutdown
        {
            private static void Prefix() { shuttingDown = true; }
        }
    }
}
