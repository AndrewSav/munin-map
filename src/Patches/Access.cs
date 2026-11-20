// patches: TASK-20. Who may change things from the web page (fork only, see PATCHES.md).
//
// enable_sweep_api: off, POST api/sweep and /structures/refresh need the admin token.
// require_auth: on, the actions that change something need a claimed player (part 2).
// Both off/on at their defaults, every request goes through untouched, as upstream.
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace MuninMap.Patches
{
    internal static class Access
    {
        public static bool EnableSweepApi = true;
        public static bool RequireAuth = false;

        public static void Bind(ConfigFile config)
        {
            RequireAuth = config.Bind("User", "require_auth", RequireAuth,
                "Only claimed players may place or remove pins, ping, rename or hide bases, or ask for a world sweep from the web page. "
                + "Each player claims their character once in their browser with the player token (player.token beside announce.token). "
                + "The admin token is accepted as always. Off: anyone who can reach the map may, as before.").Value;
            EnableSweepApi = config.Bind("Sweep", "enable_sweep_api", EnableSweepApi,
                "Let anyone ask for a world sweep now (POST /api/sweep, /structures/refresh). Off: only with the admin token.").Value;
        }

        public static void AddTo(Dictionary<string, object> config) { config["require_auth"] = RequireAuth; }

        // the admin token (announce.token), as MapDataServer.Authorized checks it
        public static bool Admin(HttpListenerRequest req)
        {
            string want = Announce.Token;
            string got = req.Headers["X-Announce-Token"] ?? req.Headers["X-Munin-Token"] ?? "";
            return want != null && got == want;
        }

        // Before upstream's routing: true when this request was answered here.
        public static bool Handle(HttpRequestEventArgs e, string path, bool post)
        {
            var req = e.Request;
            bool sweep = (path == "/api/sweep" && post) || path == "/structures/refresh";
            if (sweep && !EnableSweepApi && !Admin(req)) return Json(e, 403, "{\"error\":\"the sweep API is off\"}");
            return false;
        }

        internal static bool Json(HttpRequestEventArgs e, int status, string json)
        {
            var res = e.Response;
            byte[] data = Encoding.UTF8.GetBytes(json);
            res.Headers.Add("Cache-Control", "no-cache");
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.ContentType = "application/json";
            res.StatusCode = status;
            res.ContentLength64 = data.Length;
            res.Close(data, true);
            return true;
        }
    }
}
