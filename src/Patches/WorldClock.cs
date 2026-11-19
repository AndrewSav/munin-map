// patches: TASK-2. The in-game day and time for the map's clock chip (fork only, see PATCHES.md).
//
// Added to the players update the server already sends about once a second (Live/Players.cs,
// the websocket "players" frame and data/players.json), as one extra field:
//   "clock": {"worldTime", "dayLength", "day", "dayFraction", "night"}
// worldTime and dayLength let the page keep the clock going between updates. Updated every
// second, it also follows the game's fast-forward to morning while everyone sleeps.
// The same numbers as the stats panel's day tile (Live/Stats.cs WorldTime): the day is
// worldTime / dayLength, and the time of day is the game's rescaled clock (EnvMan.RescaleDayFraction),
// with the daylight part running from 0.25 to 0.75.
using MuninMap.Util;

namespace MuninMap.Patches
{
    internal static class WorldClock
    {
        public static void Write(JsonWriter j)
        {
            try
            {
                if (ZNet.instance == null) return;
                double t = ZNet.instance.GetTimeSeconds();
                float dayLen = 1800f;
                try { if (EnvMan.instance != null && EnvMan.instance.m_dayLengthSec > 0) dayLen = EnvMan.instance.m_dayLengthSec; } catch { }
                double frac = Live.Stats.DayFraction((t % dayLen) / dayLen);
                j.Key("clock").BeginObject()
                 .Prop("worldTime", t, 1).Prop("dayLength", dayLen, 0).Prop("day", (int)(t / dayLen))
                 .Prop("dayFraction", frac, 5).Prop("night", frac <= 0.25 || frac >= 0.75)
                 .End();
            }
            catch { }
        }
    }
}
