// patches: TASK-18. The text and buttons of Munin's Telegram posts (fork only, see PATCHES.md).
//
// Pure functions: everything here builds strings from what it is given, so it can be read and
// checked on its own. Telegram's HTML formatting is used throughout (parse_mode HTML): only
// <, > and & need escaping in it, which keeps player names and chat safe however they're typed.
using System.Collections.Generic;
using System.Text;
using MuninMap.Util;

namespace MuninMap.Patches
{
    internal static class TelegramFormat
    {
        // Telegram's limit for one message's text, in characters
        public const int MaxText = 4096;

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '&': sb.Append("&amp;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ---- the online card

        public static string OnlineCard(string server, string address)
        {
            var sb = new StringBuilder();
            sb.Append("🟢 <b>").Append(Escape(server)).Append("</b> is online");
            if (!string.IsNullOrEmpty(address)) sb.Append("\n💻 <code>").Append(Escape(address)).Append("</code>");
            return sb.ToString();
        }

        // What the card turns into when the server stops: its first line struck through, with no buttons,
        // so nothing stale can be copied. The offline post that goes with it says the rest.
        public static string OfflineCard(string server) => "<s>🟢 <b>" + Escape(server) + "</b> is online</s>";

        public static string OfflinePost(string server) => "🔴 <b>" + Escape(server) + "</b> is offline";

        // The card's buttons as reply_markup JSON: copy buttons on the first row, the map on the second.
        // Copy buttons hold at most 256 characters; anything longer is left off rather than cut.
        public static string CardButtons(string address, string password, string userKey, string mapUrl)
        {
            var copies = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrEmpty(address)) copies.Add(new KeyValuePair<string, string>("📋 Address", address));
            if (!string.IsNullOrEmpty(password)) copies.Add(new KeyValuePair<string, string>("🔑 Password", password));
            if (!string.IsNullOrEmpty(userKey)) copies.Add(new KeyValuePair<string, string>("🗝 User key", userKey));
            copies.RemoveAll(kv => kv.Value.Length > 256);
            bool map = IsWebUrl(mapUrl);
            if (copies.Count == 0 && !map) return null;

            var j = new JsonWriter(256).BeginObject().Key("inline_keyboard").BeginArray();
            if (copies.Count > 0)
            {
                j.BeginArray();
                foreach (var kv in copies)
                    j.BeginObject().Prop("text", kv.Key).Key("copy_text").BeginObject().Prop("text", kv.Value).End().End();
                j.End();
            }
            if (map) j.BeginArray().BeginObject().Prop("text", "🗺 Open map").Prop("url", mapUrl).End().End();
            return j.End().End().ToString();
        }

        // a single "Open map" button, for raids and invasions
        public static string MapButton(string url)
        {
            if (!IsWebUrl(url)) return null;
            return new JsonWriter(128).BeginObject().Key("inline_keyboard").BeginArray()
                .BeginArray().BeginObject().Prop("text", "🗺 Open map").Prop("url", url).End().End()
                .End().End().ToString();
        }

        public static bool IsWebUrl(string url) =>
            !string.IsNullOrEmpty(url) && (url.StartsWith("http://") || url.StartsWith("https://"));

        // the map's address pointed at a spot, as the Discord posts link it
        public static string MapLink(string mapUrl, float x, float z)
        {
            if (string.IsNullOrEmpty(mapUrl)) return null;
            return mapUrl.TrimEnd('#') + "#" + UnityEngine.Mathf.RoundToInt(x) + "," + UnityEngine.Mathf.RoundToInt(z) + ",6";
        }

        // ---- short notices

        public static string Join(string name) => "➡️ <b>" + Escape(name) + "</b> joined";
        public static string Leave(string name) => "⬅️ <b>" + Escape(name) + "</b> left";

        // A raid or invasion, from the text the world-event code builds for Discord:
        // "<emoji> <text>", e.g. "⚔️ Raid: The ground is shaking (near Anna and Bob)".
        // The emoji stays in front, then the server's name in bold, then the text.
        public static string WorldEvent(string server, string message)
        {
            message = message ?? "";
            string emoji = "", text = message;
            int sp = message.IndexOf(' ');
            if (sp > 0 && sp <= 4 && !char.IsLetterOrDigit(message[0])) { emoji = message.Substring(0, sp) + " "; text = message.Substring(sp + 1); }
            return emoji + "<b>" + Escape(server) + "</b> " + Escape(text);
        }

        // Takes hidden players out of a world event's "(near A, B and C)" ending. With nobody left the
        // ending goes entirely, so the post reads as if no one was near.
        public static string WithoutHidden(string message, System.Func<string, bool> isHidden)
        {
            if (string.IsNullOrEmpty(message) || !message.EndsWith(")")) return message;
            const string marker = " (near ";
            int at = message.LastIndexOf(marker, System.StringComparison.Ordinal);
            if (at < 0) return message;
            string list = message.Substring(at + marker.Length, message.Length - at - marker.Length - 1);
            var names = new List<string>();
            int and = list.LastIndexOf(" and ", System.StringComparison.Ordinal);
            if (and >= 0)
            {
                names.AddRange(list.Substring(0, and).Split(new[] { ", " }, System.StringSplitOptions.None));
                names.Add(list.Substring(and + 5));
            }
            else names.Add(list);
            names.RemoveAll(n => isHidden(n));
            string head = message.Substring(0, at);
            if (names.Count == 0) return head;
            return head + marker + NameList(names) + ")";
        }

        // The safety net behind WithoutHidden, which relies on upstream's wording: if a hidden name is
        // still in the text afterwards, the wording has changed, so everything from the first such name
        // on goes (back to its opening bracket, and a dangling "near"), and wordingChanged is set for the
        // log. A hidden player is never named because of a reworded message, the post only loses detail.
        public static string SafeWithoutHidden(string message, IEnumerable<string> hidden, System.Func<string, bool> isHidden, out bool wordingChanged)
        {
            wordingChanged = false;
            string text = WithoutHidden(message, isHidden);
            if (string.IsNullOrEmpty(text) || hidden == null) return text;
            int first = -1;
            foreach (var name in hidden)
            {
                if (string.IsNullOrEmpty(name)) continue;
                var m = System.Text.RegularExpressions.Regex.Match(text,
                    @"(?<![\p{L}\p{N}_])" + System.Text.RegularExpressions.Regex.Escape(name) + @"(?![\p{L}\p{N}_])",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success && (first < 0 || m.Index < first)) first = m.Index;
            }
            if (first < 0) return text;
            wordingChanged = true;
            string cut = text.Substring(0, first);
            int open = cut.LastIndexOfAny(new[] { '(', '[' });
            if (open >= 0) cut = cut.Substring(0, open);
            cut = System.Text.RegularExpressions.Regex.Replace(cut, @"\s*\bnear\b[\W_]*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return cut.TrimEnd(' ', ',', ':', ';', '-', '&', '(');
        }

        // "A", "A and B", "A, B and C": as the world-event code joins names
        public static string NameList(List<string> names)
        {
            if (names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }

        // ---- chat

        public static string Chat(bool shout, string name, string text) =>
            (shout ? "📢 <b>" : "💬 <b>") + Escape(name) + "</b>: " + Escape(text);
    }
}
