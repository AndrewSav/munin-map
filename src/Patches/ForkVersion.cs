// patches: TASK-32. The fork's name, version and repository on the page, and the update check against
// the fork's releases (fork only, see PATCHES.md).
//
// Someone who finds a server running this fork should land on the fork's repository, see its release,
// and be told about the fork's next release, not upstream's (whose build would drop the fork's
// changes). The release workflow stamps the assembly's informational version as <upstream>+patches.<N>
// for the release tagged patches-<upstream>-<N> (decision-1); the page shows it as <upstream>-<N>.
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using MuninMap.Util;

namespace MuninMap.Patches
{
    internal static class ForkVersion
    {
        public const string Repo = "https://github.com/AndrewSav/munin-map";
        private const string Api = "https://api.github.com/repos/AndrewSav/munin-map/releases/latest";

        // "3.4.0-17" for a release build; "3.4.0-dev" for a build without a release stamp
        public static readonly string Version = Read();

        private static string Read()
        {
            try
            {
                string info = typeof(ForkVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
                var m = Regex.Match(info, @"^(\d+\.\d+\.\d+)\+patches\.(\d+)$");
                if (m.Success) return m.Groups[1].Value + "-" + m.Groups[2].Value;
            }
            catch { }
            return global::MuninMap.MuninMap.VERSION + "-dev";
        }

        // Config.cs, after upstream's values: the page shows the fork's version and links to the fork
        public static void AddTo(Dictionary<string, object> config)
        {
            config["version"] = Version;
            config["repo_url"] = Repo;
        }

        private static Thread thread;

        // From UpdateCheck.Start: the same schedule and switch (check_updates) as upstream's check, but
        // against the fork's latest release. It fills UpdateCheck.Latest and LatestUrl, which the
        // config and the stats already send to the page. Returns true: upstream's check doesn't run.
        public static bool StartUpdateCheck()
        {
            if (!MuninConfig.CHECK_UPDATES || thread != null) return true;
            thread = new Thread(Loop) { IsBackground = true, Name = "MuninMap update check" };
            thread.Start();
            return true;
        }

        private static void Loop()
        {
            Thread.Sleep(60 * 1000);
            while (true)
            {
                try { Check(); }
                catch (Exception e) { ZLog.Log("MuninMap: update check: " + e.Message); }
                Thread.Sleep(12 * 3600 * 1000);
            }
        }

        private static void Check()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "MuninMap/" + Version;
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                var d = JsonParser.Parse(wc.DownloadString(Api)) as Dictionary<string, object>;
                // patches-3.4.0-17 -> 3.4.0-17
                var m = Regex.Match(JsonParser.Str(d, "tag_name") ?? "", @"^patches-(\d+\.\d+\.\d+-\d+)$");
                if (!m.Success) return;
                string latest = m.Groups[1].Value;
                UpdateCheck.LatestUrl = JsonParser.Str(d, "html_url") ?? Repo + "/releases";
                UpdateCheck.Latest = latest;
                if (Newer(latest, Version)) ZLog.Log("MuninMap: version " + latest + " is out (this is " + Version + "): " + UpdateCheck.LatestUrl);
            }
        }

        // a newer than b: X.Y.Z-N compared as X.Y.Z.N ("3.4.0-17" > "3.4.0-16", "3.5.0-1" > "3.4.0-17");
        // a "-dev" build counts as release 0 of its upstream version
        public static bool Newer(string a, string b)
        {
            int[] A = Parts(a), B = Parts(b);
            for (int i = 0; i < Math.Max(A.Length, B.Length); i++)
            {
                int x = i < A.Length ? A[i] : 0, y = i < B.Length ? B[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }

        private static int[] Parts(string v)
        {
            var p = (v ?? "").Replace('-', '.').Split('.');
            var n = new int[p.Length];
            for (int i = 0; i < p.Length; i++) int.TryParse(p[i], out n[i]);
            return n;
        }
    }
}
