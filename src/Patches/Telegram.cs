// patches: TASK-18. Munin's server notices, and optionally the game's chat, posted to a Telegram
// group (fork only, see PATCHES.md).
//
// The Discord webhook's twin: the same notices (online, offline, joins, leaves, raids and
// invasions) go to the group a bot posts in, formatted for Telegram. The upstream code calls the
// hooks below from where it posts to Discord, one marked line each. Every hook only formats and
// queues (TelegramSender does the sending on its own thread) and never throws.
//
// Settings for what Munin sends to the group start telegram_post_; telegram_world_events keeps the
// name of its Discord twin. If posts from Telegram into the game are added one day (DRAFT-1), they
// get names of their own, so none of these needs renaming.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BepInEx.Configuration;
using HarmonyLib;

namespace MuninMap.Patches
{
    internal static class Telegram
    {
        public static string BotToken = "";
        public static string ChatId = "";
        public static string TopicId = "";
        public static bool WorldEvents = true;
        public static string PostChat = "off";     // off, shouts, all
        public static string PostAddress = "";     // empty: detected
        public static bool PostPassword = false;
        public static bool PostUserKey = false;
        public static string PostSilent = "";

        private static readonly string[] kinds = { "online", "offline", "join", "leave", "raid", "chat" };
        private static HashSet<string> silent = new HashSet<string>();
        private static long cardId;                // the online card's message id, set by the sender thread
        private static bool warnedWording;

        private static bool On => BotToken.Length > 0 && ChatId.Length > 0;

        public static void Bind(ConfigFile config)
        {
            BotToken = (config.Bind("Server", "telegram_bot_token", BotToken,
                "Telegram bot token, from @BotFather, to post Munin's server notices to a Telegram group (the twin of discord_webhook). "
                + "A secret: whoever holds it is the bot. Empty: nothing is sent to Telegram.").Value ?? "").Trim();
            // copied from an API address (".../bot<token>/..."): the "bot" in front is not part of it
            if (System.Text.RegularExpressions.Regex.IsMatch(BotToken, @"^bot\d+:")) BotToken = BotToken.Substring(3);
            ChatId = (config.Bind("Server", "telegram_chat_id", ChatId,
                "The Telegram group to post to, a negative number such as -1001234567890. docs/ADVANCED.md says how to find it.").Value ?? "").Trim();
            TopicId = (config.Bind("Server", "telegram_topic_id", TopicId,
                "Optional, for a group with topics: the topic to post in (its message_thread_id). Empty: the group's main chat.").Value ?? "").Trim();
            WorldEvents = config.Bind("Server", "telegram_world_events", WorldEvents,
                "Also post raids and invasions (starting and ending) to Telegram, with a button to the spot on the map when map_url is set.").Value;
            PostChat = (config.Bind("Server", "telegram_post_chat", PostChat,
                new ConfigDescription("Relay the game's chat to Telegram: off, shouts (shouts only) or all (normal chat and shouts). "
                    + "Normal chat only reaches players nearby in the game, so all publishes it to the whole group. Whispers are never relayed.",
                    new AcceptableValueList<string>("off", "shouts", "all"))).Value ?? "off").Trim().ToLowerInvariant();
            PostAddress = (config.Bind("Server", "telegram_post_address", PostAddress,
                "The server address shown on the online post in Telegram and copied by its Address button. Empty: detected (the public IP and the game port). "
                + "A host name alone (valheim.example.com) gets the game port added; with a port (valheim.example.com:24560) it is shown as given, for a forwarded port.").Value ?? "").Trim();
            PostPassword = config.Bind("Server", "telegram_post_password", PostPassword,
                "Put a button to copy the server's password on the online post in Telegram.").Value;
            PostUserKey = config.Bind("Server", "telegram_post_user_key", PostUserKey,
                "Put a button to copy the user key (user.token beside announce.token, for claiming players) on the online post in Telegram. Only when the file is there and not empty.").Value;
            PostSilent = (config.Bind("Server", "telegram_post_silent", PostSilent,
                "Kinds of Telegram post sent silently (shown in the group as usual, no sound on phones), comma separated: "
                + "online, offline, join, leave, raid (raids and invasions), chat. Empty: none.").Value ?? "").Trim();

            silent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in PostSilent.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                if (kinds.Contains(k.ToLowerInvariant())) silent.Add(k);
                else ZLog.LogWarning("MuninMap: Telegram: telegram_post_silent: unknown kind \"" + k + "\" ignored (known: " + string.Join(", ", kinds) + ")");
            }
            if (PostChat != "off" && PostChat != "shouts" && PostChat != "all") PostChat = "off";

            TelegramSender.Token = BotToken;
            TelegramSender.ChatId = ChatId;
            TelegramSender.TopicId = 0;
            if (TopicId.Length > 0)
            {
                if (int.TryParse(TopicId, out int t) && t > 0) TelegramSender.TopicId = t;
                else ZLog.LogWarning("MuninMap: Telegram: telegram_topic_id \"" + TopicId + "\" is not a topic number; posting to the main chat");
            }
            if (On) ZLog.Log("MuninMap: Telegram: posting to chat " + ChatId + (TelegramSender.TopicId != 0 ? ", topic " + TelegramSender.TopicId : ""));
        }

        private static string Server()
        {
            try { return MuninMap.serverInfo != null && MuninMap.serverInfo.TryGetValue("serverName", out var n) ? n?.ToString() ?? "" : ""; }
            catch { return ""; }
        }

        // telegram_post_address as set (a bare host gets the game port), or the detected public IP and port
        private static string Address()
        {
            // the game port (-port). ZNet.GetHostPort() is no use here: on the server its Steam socket
            // always answers 1
            int port = 0;
            try { port = SteamManager.m_serverPort; } catch { }
            if (PostAddress.Length > 0)
                return System.Text.RegularExpressions.Regex.IsMatch(PostAddress, @":\d+$") || port <= 0 ? PostAddress : PostAddress + ":" + port;
            string ip = "";
            try { ip = AccessTools.Method(typeof(ZNet), "GetServerIP")?.Invoke(ZNet.instance, new object[] { })?.ToString() ?? ""; } catch { }
            return ip.Length > 0 && port > 0 ? ip + ":" + port : ip;
        }

        private static void Queue(string kind, string html, string buttons = null, string fallbackButtons = null, Action<long> onSent = null)
        {
            TelegramSender.Enqueue(new TelegramSender.Post
            {
                Kind = kind, Html = html, Buttons = buttons, FallbackButtons = fallbackButtons,
                Silent = silent.Contains(kind), OnSent = onSent
            });
        }

        // ---- hooks (the game's thread)

        public static void Online()
        {
            try
            {
                if (!On) return;
                string address = Address();
                string password = null;
                if (PostPassword && MuninMap.serverInfo != null && MuninMap.serverInfo.TryGetValue("password", out var pw)) password = pw?.ToString();
                string key = PostUserKey ? Claims.Token : null;
                string map = MuninConfig.URL;
                Interlocked.Exchange(ref cardId, 0);
                Queue("online", TelegramFormat.OnlineCard(Server(), address),
                    TelegramFormat.CardButtons(address, password, key, map),
                    TelegramFormat.CardButtons(address, password, key, null),
                    id => Interlocked.Exchange(ref cardId, id));
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: Telegram: online post: " + e.Message); }
        }

        // At shutdown: the offline post, then the online card edited to say offline (its copy buttons
        // gone). The process can end right after this, so it waits, at most 3 s, for both to go out.
        // A forced kill or a crash never gets here, so then no offline post can go out at all.
        public static void Offline()
        {
            try
            {
                if (!On) return;
                var started = DateTime.UtcNow;
                ZLog.Log("MuninMap: Telegram: sending the offline post");
                string server = Server();
                Queue("offline", TelegramFormat.OfflinePost(server));
                TelegramSender.Enqueue(new TelegramSender.Post
                {
                    Kind = "edit", Method = "editMessageText", Html = TelegramFormat.OfflineCard(server),
                    EditId = () => Interlocked.Read(ref cardId)
                });
                bool done = TelegramSender.Flush(3000);
                ZLog.Log("MuninMap: Telegram: " + (done ? "offline post sent" : "gave up waiting for the offline post")
                    + " after " + (int)(DateTime.UtcNow - started).TotalMilliseconds + " ms");
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: Telegram: offline post: " + e.Message); }
        }

        public static void Join(string name)
        {
            try { if (On && !MuninConfig.IsHidden(name)) Queue("join", TelegramFormat.Join(name)); }
            catch (Exception e) { ZLog.LogWarning("MuninMap: Telegram: join post: " + e.Message); }
        }

        public static void Leave(string name)
        {
            try { if (On && !MuninConfig.IsHidden(name)) Queue("leave", TelegramFormat.Leave(name)); }
            catch (Exception e) { ZLog.LogWarning("MuninMap: Telegram: leave post: " + e.Message); }
        }

        // a raid or invasion, in the text built for Discord ("⚔️ Raid: ... (near A and B)")
        public static void WorldEvent(string message, float? x, float? z)
        {
            try
            {
                if (!On || !WorldEvents || string.IsNullOrEmpty(message)) return;
                string text = TelegramFormat.SafeWithoutHidden(message, MuninConfig.HIDDEN_PLAYERS, MuninConfig.IsHidden, out bool reworded);
                if (reworded && !warnedWording)
                {
                    warnedWording = true;
                    ZLog.LogWarning("MuninMap: Telegram: a raid or invasion message no longer reads \"... (near A, B and C)\", so the names near it were left out of the post "
                        + "to keep hidden_players hidden. Check WorldEvents.cs after the last upstream update.");
                }
                string link = x.HasValue && z.HasValue ? TelegramFormat.MapLink(MuninConfig.URL, x.Value, z.Value) : null;
                Queue("raid", TelegramFormat.WorldEvent(Server(), text), TelegramFormat.MapButton(link));
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: Telegram: world event post: " + e.Message); }
        }

        // every chat message the server sees, after Munin's own chat commands are taken out
        public static void Chat(int type, string name, string text)
        {
            try
            {
                if (!On || PostChat == "off" || string.IsNullOrEmpty(text) || name == "Server") return;
                bool shout = type == (int)Talker.Type.Shout;
                bool normal = type == (int)Talker.Type.Normal;
                if (!(shout || (normal && PostChat == "all"))) return;   // whispers and pings never
                if (MuninConfig.IsHidden(name)) return;
                Queue("chat", TelegramFormat.Chat(shout, name, text));
            }
            catch (Exception e) { ZLog.LogWarning("MuninMap: Telegram: chat post: " + e.Message); }
        }
    }
}
