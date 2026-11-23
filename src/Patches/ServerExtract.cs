// patches: TASK-4 (TASK-4.5). predict_ore = server: Munin makes the ore extract itself (fork only, see PATCHES.md).
//
// When the world has no current prediction (no predicted-ore.csv, or one for another world or game
// version), Munin runs ore-extract's supervisor (ore-extract/core, merged into MuninMap.dll) on a
// background thread of this server. It copies the world's last complete save and starts one headless
// game server from this server's own install, with only ore-extract's plugin, to generate the copy's
// zones, so the live world is never written. That server runs at the lowest CPU priority on one core
// (--slow), under the supervisor's memory guard and out-of-memory preference; the result is the same
// file predict_ore = file reads, so PredictedOre shows it when it lands. The run's folder is kept in the
// world's data folder, so after a restart the run is resumed. The generation server stops with this one:
// Munin stops it when it stops or is unloaded, and it quits by itself when this process is gone (a
// crash; Linux) or with its job object (Windows). Players see how far it is in Server details
// (data/ore-extract.json).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using MuninMap.Util;
using OreExtract.Core;
using UnityEngine;
using WebSocketSharp.Server;

namespace MuninMap.Patches
{
    internal static class ServerExtract
    {
        private const string RunDirName = "ore-extract-run";
        private const string StatusName = "ore-extract-status.txt";
        private const string LogName = "ore-extract.log";
        // how often the supervisor looks at its game server (log, memory, exit): it runs inside this
        // game server's process, so less often than the tool's every second
        private const int PollMs = 5000;

        // what the page shows; replaced whole (read by the HTTP threads)
        private sealed class View
        {
            public string state = "idle";   // idle, current, waiting, starting, loading, running, done, failed
            public string reason;           // waiting / failed: why
            public int done, total, secondsLeft = -1, deposits = -1, seconds = -1;
            public bool resumed;
            public string finishedUtc;
        }
        private static volatile View view = new View();

        private static string worldDir, workDir;
        private static Thread run;              // the supervisor, while a run is going
        private static volatile bool resumed;
        private static StreamWriter log;
        private static readonly object logGate = new object();
        private static bool ranThisStart;       // one run (or resume) per server start: a failure waits for the next start
        private static bool quitHooked;

        public static bool On => PredictedOre.Mode == "server" && MuninConfig.REVEAL_ALL;

        // per world, from PredictedOre.Load (main thread)
        public static void Load(string worldDataPath)
        {
            Stop("the world changed");
            worldDir = worldDataPath;
            workDir = Path.Combine(Path.GetDirectoryName(worldDataPath.TrimEnd('/', '\\')) ?? worldDataPath, "ore-extract");
            ranThisStart = false;
            view = new View();
            if (!On) return;
            if (!quitHooked) { quitHooked = true; Application.quitting += () => Stop("the server is stopping"); }
            view = new View { state = "waiting", reason = "the world is loading" };
        }

        // Main thread, every few seconds from PredictedOre's watch once the world has loaded. `current`:
        // the prediction on disk is for this world and this game version. Cheap: the run itself is on
        // its own thread.
        public static void Step(bool current, string gameNow)
        {
            if (!On || worldDir == null) return;
            if (run != null)
            {
                if (run.IsAlive) { Show(Status.Snapshot(), false); return; }
                run = null;
                Finished();
                return;
            }
            if (ranThisStart) return;   // done or failed: nothing more until the next start
            string runDir = Path.Combine(worldDir, RunDirName), statusPath = Path.Combine(worldDir, StatusName);
            if (current)
            {
                if (view.state != "current")
                {
                    view = new View { state = "current" };
                    // a stopped run the prediction no longer needs (a save copy: off the game's thread)
                    if (Directory.Exists(runDir)) ThreadPool.QueueUserWorkItem(_ => TryDelete(runDir));
                }
                return;
            }
            string saveDir = null, seed = null;
            try
            {
                var w = ZNet.World;
                if (w != null) { saveDir = w.GetSaveDirectory(w.m_fileSource); seed = w.m_seedName; }
            }
            catch { }
            if (saveDir == null || !Directory.Exists(saveDir) || Directory.GetFiles(saveDir, "*.ok").Length == 0)
            {
                if (view.state != "waiting" || view.reason != "the world's first save")
                    view = new View { state = "waiting", reason = "the world's first save" };
                return;
            }

            // a run left by an earlier start: resumed if it was for this world and game version
            var old = ReadStatus(statusPath);
            bool resume = File.Exists(Path.Combine(runDir, "run.txt"));
            if (resume && (Get(old, "seed") is string os && os != seed || Get(old, "game_version") is string og && og != gameNow))
            {
                ZLog.Log("MuninMap: ore extract: the stopped run was for another world or game version; starting again");
                resume = false;
            }

            var args = new List<string>();
            if (resume) { args.Add("run"); args.Add("--resume"); args.Add(runDir); }
            else
            {
                args.AddRange(new[] { "run", "--save", saveDir.TrimEnd('/', '\\'), "--slow", "--server-dir", BepInEx.Paths.GameRootPath,
                    "--run-dir", runDir, "--out", Path.Combine(worldDir, PredictedOre.FileName), "--work-dir", workDir });
            }
            ranThisStart = true;
            Start(args.ToArray(), statusPath, resume, resume ? null : runDir);
        }

        // clear: a run folder to delete first (a stopped run for another world or version), on the run's thread
        private static void Start(string[] argv, string statusPath, bool resume, string clear)
        {
            try
            {
                lock (logGate)
                {
                    log = new StreamWriter(Path.Combine(worldDir, LogName), true) { AutoFlush = true };
                    log.WriteLine($"---- {DateTime.Now:yyyy-MM-dd HH:mm:ss} ore-extract {string.Join(" ", argv)}");
                }
                Install.ToolVersion = "Munin " + ((System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    typeof(ServerExtract).Assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute)))?.InformationalVersion ?? global::MuninMap.MuninMap.VERSION;
                Install.Output = Log;
                Runner.Hosted = true;
                Runner.PollMs = PollMs;
                Status.Init(statusPath);
                resumed = resume;
                view = new View { state = "starting", resumed = resume };
                run = new Thread(() =>
                {
                    try
                    {
                        if (clear != null && Directory.Exists(clear)) Directory.Delete(clear, true);
                        Runner.Run(new Args(argv));
                    }
                    catch (ToolError e) { Log("error: " + e.Message); Status.Failed(e.Message); }
                    catch (Exception e) { Log("error: " + e); Status.Failed(e.GetType().Name + ": " + e.Message); }
                })
                { IsBackground = true, Name = "MuninMap ore extract", Priority = System.Threading.ThreadPriority.BelowNormal };
                run.Start();
                ZLog.Log($"MuninMap: ore extract {(resume ? "resumed" : "started")} (one low-priority game server process; progress in Server details, output in {Path.Combine(worldDir, LogName)})");
            }
            catch (Exception e)
            {
                CloseLog();
                run = null;
                view = new View { state = "failed", reason = "could not start: " + e.Message };
                ZLog.LogWarning("MuninMap: ore extract: could not start: " + e.Message);
            }
        }

        // main thread, once the run's thread has ended
        private static void Finished()
        {
            CloseLog();
            var st = Status.Snapshot();
            if (Get(st, "state") == "done")
            {
                Show(st, true);
                ZLog.Log($"MuninMap: ore extract finished: {Get(st, "zones_done")} zones, {Get(st, "deposits")} deposits in {Fmt(Int(st, "seconds", 0))}");
            }
            else
            {
                string why = Public(Get(st, "reason")) ?? "it stopped without a result";
                view = new View { state = "failed", reason = why };
                ZLog.LogWarning($"MuninMap: ore extract failed: {why} (details in {Path.Combine(worldDir, LogName)}; it is resumed at the next server start)");
            }
        }

        // Main thread or the quit handler: stops the run (its game server is killed at once); the run
        // folder stays for a resume
        public static void Stop(string why)
        {
            var t = run;
            run = null;
            if (t == null) return;
            if (t.IsAlive) ZLog.Log($"MuninMap: ore extract: stopping ({why}); it is resumed at the next start");
            Runner.Stop(why);
            try { t.Join(3000); } catch { }
            CloseLog();
            if (On) view = new View { state = "failed", reason = "stopped: " + why };
        }

        private static void Show(Dictionary<string, string> st, bool finished)
        {
            var v = new View { resumed = resumed };
            v.state = finished ? "done" : Get(st, "state") ?? "starting";
            if (v.state == "failed" || v.state == "stopped") v.state = "starting";   // until the thread ends and Finished says
            v.done = Int(st, "zones_done", 0);
            v.total = Int(st, "zones_total", 0);
            v.secondsLeft = Int(st, "seconds_left", -1);
            if (finished)
            {
                v.deposits = Int(st, "deposits", -1);
                v.seconds = Int(st, "seconds", -1);
                v.finishedUtc = Get(st, "updated_utc");
            }
            view = v;
        }

        private static Dictionary<string, string> ReadStatus(string path)
        {
            var d = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(path)) return d;
                foreach (var line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) d[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
            }
            catch { }
            return d;
        }

        // a reason for everyone to read: without the resume hint ore-extract adds (Status keeps each line
        // apart with " / ") and with the server's folders by name instead of their paths
        private static string Public(string reason)
        {
            if (reason == null) return null;
            int cut = reason.IndexOf(" / what was written", StringComparison.Ordinal);
            if (cut >= 0) reason = reason.Substring(0, cut);
            foreach (var (dir, name) in new[] { (worldDir, "<world data>"), (workDir, "<extract work folder>"), (BepInEx.Paths.GameRootPath, "<server>") })
                if (!string.IsNullOrEmpty(dir)) reason = reason.Replace(dir.TrimEnd('/', '\\'), name);
            return reason;
        }

        private static string Get(Dictionary<string, string> d, string k) => d.TryGetValue(k, out var v) && v.Length > 0 ? v : null;
        private static int Int(Dictionary<string, string> d, string k, int def) => int.TryParse(Get(d, k), NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : def;

        private static void Log(string line)
        {
            lock (logGate) { try { log?.WriteLine(line); } catch { } }
        }

        private static void CloseLog()
        {
            lock (logGate) { try { log?.Dispose(); } catch { } log = null; }
        }

        private static void TryDelete(string dir)
        {
            try { Directory.Delete(dir, true); } catch (Exception e) { ZLog.LogWarning("MuninMap: ore extract: could not delete " + dir + ": " + e.Message); }
        }

        private static string Fmt(int seconds) => seconds >= 3600 ? $"{seconds / 3600} h {seconds % 3600 / 60} min" : $"{seconds / 60} min";

        // data/ore-extract.json, from PredictedOre.Handle: everyone may see how far it is
        public static bool Handle(HttpRequestEventArgs e)
        {
            if (!On) return Access.Json(e, 200, "{\"on\":false}");
            var v = view;
            var j = new JsonWriter(256).BeginObject().Prop("on", true).Prop("state", v.state);
            if (v.reason != null) j.Prop("reason", v.reason);
            if (v.total > 0) j.Prop("done", v.done).Prop("total", v.total);
            if (v.secondsLeft >= 0) j.Prop("secondsLeft", v.secondsLeft);
            if (v.deposits >= 0) j.Prop("deposits", v.deposits);
            if (v.seconds >= 0) j.Prop("seconds", v.seconds);
            if (v.finishedUtc != null) j.Prop("finishedUtc", v.finishedUtc);
            if (v.resumed) j.Prop("resumed", true);
            return Access.Json(e, 200, j.End().ToString());
        }
    }
}
