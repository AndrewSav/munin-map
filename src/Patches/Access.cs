// patches: TASK-20. Who may change things from the web page (fork only, see PATCHES.md).
//
// enable_sweep_api: off, POST api/sweep and /structures/refresh need the admin token.
// require_auth: on, the actions that change something need a claimed player (Claims.cs), and pins,
// pings and base edits carry the claimed player's name, never the one a request sends.
// Both at their defaults, every action request goes through untouched, as upstream.
//
// Endpoints served here:
//   GET  /data/claim.json            this browser's claim (X-Munin-Client or ?client=): {open, requireAuth, name}
//   GET  /data/claimable.json        the players a browser may claim: [{id, name, firstSeen}]
//   POST /api/claim                  {id, token} with X-Munin-Client: claim that player
//   POST /api/unclaim                {id} or {name}, admin token
//   GET  /api/claims                 every claim (admin token): [{id, name, at}]
//   GET  /api/bases                  every base override, with who and when (admin token)
using System.Collections.Generic;
using System.IO;
using System.Text;
using MuninMap.Util;
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
                "Only claimed players may place or remove pins, ping or rename or hide bases from the web page, or ask for a world sweep (POST /api/sweep). "
                + "Each player claims their character with the user key the admin shares (user.token beside announce.token). "
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

        // the browser's key: X-Munin-Client, or ?client= (the page's pin list asks that way)
        public static string KeyOf(HttpListenerRequest req) => Claims.KeyOf(req.Headers["X-Munin-Client"] ?? req.QueryString["client"]);

        // the claimed player's name for this request, when require_auth is on; else null
        private static string Claimed(HttpListenerRequest req) => RequireAuth ? Claims.ByKey(KeyOf(req))?.name : null;

        // patches: TASK-22. Who a web ping counts against for WebPing's 10 s limit: with require_auth on,
        // the claimed character (its stats key, so two of the same name stay apart); otherwise the
        // connection's address, as upstream. Behind a reverse proxy every visitor has the proxy's
        // address, so without require_auth the limit is shared by all of them.
        public static string PingKey(HttpListenerRequest req)
        {
            var c = RequireAuth ? Claims.ByKey(KeyOf(req)) : null;
            return c != null ? "claim:" + c.player : req.RemoteEndPoint?.Address?.ToString() ?? "?";
        }

        // what a pin's "by" or a ping's name is: with require_auth on, the claimed player (whatever
        // the request sent); otherwise the name as given
        public static string NameFor(HttpListenerRequest req, string name) => Claimed(req) ?? name;

        // who made a base edit, for BaseEdits
        public static string Who(HttpListenerRequest req) => Claimed(req) ?? (Admin(req) ? "admin" : "web");

        private static readonly HashSet<string> gated = new HashSet<string> { "/api/pin", "/api/unpin", "/api/ping", "/api/base", "/api/sweep", "/structures/refresh" };

        // Before upstream's routing: true when this request was answered here.
        public static bool Handle(HttpRequestEventArgs e, string path, bool post)
        {
            var req = e.Request;
            if (PredictedOre.Handle(e, path, post)) return true;   // TASK-4: data/predicted-ore.json
            if (OreLayer.Handle(e, path, post)) return true;   // TASK-4: data/ore.json
            switch (path)
            {
                case "/data/claim.json":
                {
                    var c = Claims.ByKey(KeyOf(req));
                    var j = new JsonWriter(128).BeginObject().Prop("requireAuth", RequireAuth).Prop("open", Claims.Open);
                    if (c != null) j.Prop("name", c.name);
                    return Json(e, 200, j.End().ToString());
                }
                case "/data/claimable.json":
                {
                    var j = new JsonWriter(512).BeginArray();
                    foreach (var kv in Claims.Claimable()) j.BeginObject().Prop("id", kv.Key).Prop("name", kv.Value.name).Prop("firstSeen", kv.Value.firstSeen).End();
                    return Json(e, 200, j.End().ToString());
                }
                case "/api/claim":
                {
                    if (!post) return false;
                    var f = Body(req);
                    string why = Claims.TryClaim(f != null ? JsonParser.Str(f, "id", null) : null, f != null ? JsonParser.Str(f, "token", null) : null,
                        Claims.KeyOf(req.Headers["X-Munin-Client"] ?? (f != null ? JsonParser.Str(f, "client", null) : null)), out string name);
                    if (why != null) return Json(e, why == "wrong user key" || why == "claiming is closed" ? 403 : 409, "{\"error\":" + Quote(why) + "}");
                    return Json(e, 200, "{\"name\":" + Quote(name) + "}");
                }
                case "/api/unclaim":
                {
                    if (!post) return false;
                    if (!Admin(req)) return Json(e, 403, "{\"error\":\"forbidden\"}");
                    var f = Body(req);
                    bool ok = f != null && Claims.Unclaim(JsonParser.Str(f, "id", null), JsonParser.Str(f, "name", null));
                    return Json(e, ok ? 200 : 404, ok ? "{\"unclaimed\":true}" : "{\"error\":\"no such claim\"}");
                }
                case "/api/claims":
                {
                    if (!Admin(req)) return Json(e, 403, "{\"error\":\"forbidden\"}");
                    var j = new JsonWriter(512).BeginArray();
                    foreach (var c in Claims.All()) j.BeginObject().Prop("id", Claims.IdOf(c.player)).Prop("name", c.name).Prop("at", c.at).End();
                    return Json(e, 200, j.End().ToString());
                }
                case "/api/bases":
                    if (post) return false;
                    if (!Admin(req)) return Json(e, 403, "{\"error\":\"forbidden\"}");
                    return Json(e, 200, BaseEdits.ListJson());
            }
            if (!gated.Contains(path)) return false;
            if (path.StartsWith("/api/", System.StringComparison.Ordinal) && !post) return false;
            if (Admin(req)) return false;
            bool sweep = path == "/api/sweep" || path == "/structures/refresh";
            if (sweep && !EnableSweepApi) return Json(e, 403, "{\"error\":\"the sweep API is off\"}");
            if (RequireAuth && Claimed(req) == null) return Json(e, 403, "{\"error\":\"claim your player first\"}");
            return false;
        }

        private static Dictionary<string, object> Body(HttpListenerRequest req)
        {
            try { using (var sr = new StreamReader(req.InputStream, Encoding.UTF8)) return JsonParser.Parse(sr.ReadToEnd()) as Dictionary<string, object>; }
            catch { return null; }
        }

        private static string Quote(string s) => new JsonWriter(32).Value(s).ToString();

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
