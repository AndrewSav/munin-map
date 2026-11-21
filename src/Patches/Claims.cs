// patches: TASK-20. Claimed players (fork only, see PATCHES.md).
//
// With require_auth on, a browser claims one of the players Munin knows (a character: its stats
// key, <SteamID64>/<name>) with the user key, a secret in user.token beside announce.token
// that the admin shares with their players. The claim ties that browser's key (the client ID the
// page sends as X-Munin-Client, shown on the page as the "pin key") to the player. One player per
// key and one key per player; the admin can unclaim.
//
// Kept per world in claims.json beside bases.json: a random salt and the claims. The salt makes the
// ids the page sees (claimable.json) opaque: a hash of salt + stats key, never the key itself,
// which holds the player's Steam ID.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MuninMap.Live;
using MuninMap.Util;

namespace MuninMap.Patches
{
    internal static class Claims
    {
        public sealed class Claim { public string player, client, name, at; }

        private static readonly object gate = new object();
        private static string loadedFrom;
        private static string salt;
        private static List<Claim> claims = new List<Claim>();

        // ---- the user key: user.token beside announce.token, re-read when it changes

        private const string TokenFile = "user.token";
        private static string tokenCache; private static DateTime tokenStamp;

        public static string Token
        {
            get
            {
                try
                {
                    string path = Path.Combine(MuninMap.mapDataPath ?? "", TokenFile);
                    if (!File.Exists(path)) { tokenCache = null; return null; }
                    var stamp = File.GetLastWriteTimeUtc(path);
                    if (tokenCache == null || stamp != tokenStamp) { tokenCache = File.ReadAllText(path).Trim(); tokenStamp = stamp; }
                    return tokenCache.Length > 0 ? tokenCache : null;
                }
                catch { return null; }
            }
        }

        public static bool Open => Token != null;

        // ---- keys: the browser's client id, as MapDataServer.WebOwner reads it ("web:<id>"), or null

        private static readonly Regex clientIdFilter = new Regex("[^A-Za-z0-9_-]", RegexOptions.Compiled);

        public static string KeyOf(string raw)
        {
            string c = clientIdFilter.Replace(raw ?? "", "");
            if (c.Length > 40) c = c.Substring(0, 40);
            return c.Length == 0 ? null : "web:" + c;
        }

        // ---- the claims

        private static string PathNow => MuninMap.worldDataPath == null ? null : Path.Combine(MuninMap.worldDataPath, "claims.json");

        private static void Load()
        {
            string path = PathNow;
            if (path == loadedFrom) return;
            loadedFrom = path; salt = null; claims = new List<Claim>();
            try
            {
                if (path != null && File.Exists(path))
                {
                    var d = JsonParser.ParseObject(File.ReadAllText(path));
                    salt = JsonParser.Str(d, "salt", null);
                    var list = JsonParser.Arr(d, "claims");
                    if (list != null)
                        foreach (var o in list)
                            if (o is Dictionary<string, object> c)
                            {
                                var cl = new Claim { player = JsonParser.Str(c, "player", null), client = JsonParser.Str(c, "client", null), name = JsonParser.Str(c, "name"), at = JsonParser.Str(c, "at") };
                                if (!string.IsNullOrEmpty(cl.player) && !string.IsNullOrEmpty(cl.client)) claims.Add(cl);
                            }
                }
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: claims.json not readable: " + e.Message); }
            if (string.IsNullOrEmpty(salt))
            {
                var b = new byte[16];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
                salt = BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
                Save();
            }
        }

        private static void Save()
        {
            string path = PathNow;
            if (path == null) return;
            try
            {
                var j = new JsonWriter(512);
                j.BeginObject().Prop("salt", salt).Key("claims").BeginArray();
                foreach (var c in claims) j.BeginObject().Prop("player", c.player).Prop("client", c.client).Prop("name", c.name).Prop("at", c.at).End();
                j.End().End();
                File.WriteAllText(path + ".tmp", j.ToString());
                if (File.Exists(path)) File.Delete(path);
                File.Move(path + ".tmp", path);
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: could not save claims.json: " + e.Message); }
        }

        // the stats are the game thread's; a copy of their keys, or none if they were changing just then
        private static List<string> KnownKeys() { try { return Stats.Keys(); } catch { return new List<string>(); } }

        public static string IdOf(string player)
        {
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + "\n" + player));
                return BitConverter.ToString(h, 0, 9).Replace("-", "").ToLowerInvariant();
            }
        }

        public static Claim ByKey(string key)
        {
            if (key == null) return null;
            lock (gate) { Load(); return claims.Find(c => c.client == key); }
        }

        // the players a browser may claim: known to the stats, not hidden, not claimed. Sorted by name,
        // then first seen (two accounts may each have a character of the same name).
        public static List<KeyValuePair<string, Stats.PlayerStat>> Claimable()
        {
            var list = new List<KeyValuePair<string, Stats.PlayerStat>>();
            lock (gate)
            {
                Load();
                foreach (var k in KnownKeys())
                {
                    if (claims.Exists(c => c.player == k)) continue;
                    Stats.PlayerStat s;
                    try { s = Stats.Get(k, null); } catch { continue; }   // (an existing key: found, not made)
                    if (s == null || string.IsNullOrEmpty(s.name) || MuninConfig.IsHidden(s.name)) continue;
                    list.Add(new KeyValuePair<string, Stats.PlayerStat>(IdOf(k), s));
                }
            }
            list.Sort((a, b) => { int n = string.Compare(a.Value.name, b.Value.name, StringComparison.OrdinalIgnoreCase); return n != 0 ? n : string.CompareOrdinal(a.Value.firstSeen, b.Value.firstSeen); });
            return list;
        }

        // null when claimed, else why not
        public static string TryClaim(string id, string token, string key, out string name)
        {
            name = null;
            if (!Open) return "claiming is closed";
            if (token == null || token.Trim() != Token) return "wrong user key";
            if (key == null) return "no key";
            lock (gate)
            {
                Load();
                if (claims.Exists(c => c.client == key)) return "this key has already claimed a player";
                string player = null; Stats.PlayerStat s = null;
                foreach (var k in KnownKeys()) if (IdOf(k) == id) { player = k; break; }
                if (player == null) return "no such player";
                if (claims.Exists(c => c.player == player)) return "that player is already claimed";
                try { s = Stats.Get(player, null); } catch { }
                if (s == null || string.IsNullOrEmpty(s.name) || MuninConfig.IsHidden(s.name)) return "no such player";
                name = s.name;
                claims.Add(new Claim { player = player, client = key, name = name, at = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) });
                Save();
            }
            ZLog.Log("MuninMap: " + name + " claimed from the web page");
            return null;
        }

        // by the claimed player's id (claims.json-backed list for the admin) or name; true when one went
        public static bool Unclaim(string id, string name)
        {
            lock (gate)
            {
                Load();
                var c = claims.Find(x => (id != null && IdOf(x.player) == id) || (id == null && name != null && string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase)));
                if (c == null) return false;
                claims.Remove(c);
                Save();
                ZLog.Log("MuninMap: " + c.name + " unclaimed by the admin");
                return true;
            }
        }

        public static List<Claim> All()
        {
            lock (gate) { Load(); return new List<Claim>(claims); }
        }
    }
}
