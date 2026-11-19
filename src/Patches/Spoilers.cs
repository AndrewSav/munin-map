// patches: TASK-1. The spoiler view (fork only, see PATCHES.md).
//
// With reveal_all on, the server keeps sending the whole generated world, as upstream's
// reveal_all does, plus the locations the game has planned but not built yet. The page decides
// what to show: a viewer with the spoiler toggle off gets the real explored mask as data/fog.png
// and filters the rest itself (web/js/spoilers-core.js), so the map looks as with reveal_all off.
// With reveal_all off, none of this does anything.
using System;
using System.Collections.Generic;
using MuninMap.Util;

namespace MuninMap.Patches
{
    internal static class Spoilers
    {
        public const string Cookie = "munin_spoilers";

        // whether the request being handled asked for the spoiler view (set per request; the
        // HTTP server handles a request on one thread from start to end)
        [ThreadStatic] private static bool requested;

        public static void BeginRequest(WebSocketSharp.Net.HttpListenerRequest req)
        {
            requested = false;
            try { requested = req?.Cookies?[Cookie]?.Value == "1"; } catch { }
        }

        // the all-clear fog image only for a viewer who turned spoilers on; everyone else gets the
        // real explored mask, which the page then draws as the fog of war
        public static bool Requested => MuninConfig.REVEAL_ALL && requested;

        // planned (not yet built) locations are listed with the rest when reveal_all is on; the
        // page leaves them out unless its spoiler toggle is on
        public static bool ShowPlanned => MuninConfig.REVEAL_ALL;

        // a location marker's extra fields: placed:false when the game has not built it yet, and,
        // for a unique location (a trader), how many candidate sites it still has. Main thread.
        public static void WriteLocation(JsonWriter j, ZoneSystem.LocationInstance li)
        {
            if (!MuninConfig.REVEAL_ALL || li.m_placed) return;
            j.Prop("placed", false);
            var loc = li.m_location;
            if (loc != null && loc.m_unique) j.Prop("sites", Candidates(loc.m_prefabName));
        }

        // unbuilt instances per location prefab, counted at most every few seconds (a marker build
        // asks once per location)
        private static Dictionary<string, int> unbuilt;
        private static DateTime counted;
        private static int Candidates(string prefab)
        {
            if (unbuilt == null || (DateTime.UtcNow - counted).TotalSeconds > 5)
            {
                var n = new Dictionary<string, int>();
                var zs = ZoneSystem.instance;
                if (zs != null && zs.m_locationInstances != null)
                    foreach (var li in zs.m_locationInstances.Values)
                    {
                        string p = li.m_location?.m_prefabName;
                        if (p == null || li.m_placed) continue;
                        n.TryGetValue(p, out int c);
                        n[p] = c + 1;
                    }
                unbuilt = n;
                counted = DateTime.UtcNow;
            }
            return prefab != null && unbuilt.TryGetValue(prefab, out int k) ? k : 1;
        }
    }
}
