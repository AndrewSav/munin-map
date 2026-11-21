// patches: TASK-15. The world's seed in the stats panel's Server details (fork only, see PATCHES.md).
//
// The seed lets anyone see the whole world on the seed-map websites, so it is a spoiler: shown only
// when the admin turns show_seed on. With it off the seed is not sent at all, not just hidden.
using System.Collections.Generic;
using BepInEx.Configuration;

namespace MuninMap.Patches
{
    internal static class Seed
    {
        public static bool Show = false;

        public static void Bind(ConfigFile config)
        {
            Show = config.Bind("Server", "show_seed", Show,
                "Show the world's seed in the stats panel's Server details, so players can look the world up on seed-map websites. "
                + "The seed reveals the whole world there (a spoiler). Off: the seed is not sent to the page at all.").Value;
        }

        // the world's generation seed (the text shown when the world was created), or null
        private static string Current()
        {
            try { return WorldGenerator.instance?.m_world?.m_seedName ?? ZNet.m_world?.m_seedName; } catch { return null; }
        }

        public static void AddTo(Dictionary<string, object> config)
        {
            if (!Show) return;
            string s = Current();
            if (!string.IsNullOrEmpty(s)) config["seed"] = s;
        }
    }
}
