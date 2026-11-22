// patches: TASK-18. Sends Munin's Telegram posts on a thread of its own (fork only, see PATCHES.md).
//
// The game's thread only queues a post and returns: a slow, failing or unreachable Telegram can
// delay or drop posts, never the game. The one wait is Flush at shutdown, bounded by its timeout.
//
// Telegram lets a bot post about 20 messages a minute in one group, so posts go out from a small
// bucket: up to 5 at once, then one every 3 s. Chat lines that pile up behind it are merged into
// one post. A "too many requests" reply waits as long as Telegram asks; a network error is tried
// twice more; anything else Telegram refuses is dropped and logged. The bot token is part of every
// request URL, so it is kept out of every log line.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using MuninMap.Util;

namespace MuninMap.Patches
{
    internal static class TelegramSender
    {
        internal sealed class Post
        {
            public string Kind;          // online, offline, join, leave, raid, chat, edit
            public string Method = "sendMessage";
            public string Html;
            public string Buttons;       // reply_markup JSON, or null
            public string FallbackButtons; // used once if Telegram refuses Buttons (a bad map_url)
            public bool Silent;
            public Func<long> EditId;    // editMessageText: the message to edit, read when sending (0: skip)
            public Action<long> OnSent;  // gets the new message's id
        }

        private const int MaxQueue = 100;
        private const int Burst = 5;
        private const double RefillSeconds = 3.0;

        private static readonly LinkedList<Post> queue = new LinkedList<Post>();
        private static readonly object gate = new object();
        private static Thread thread;
        private static bool busy;          // a post is being sent
        private static double tokens = Burst;
        private static DateTime refilled = DateTime.UtcNow;
        private static DateTime lastLog = DateTime.MinValue;

        public static string Token = "";
        public static string ChatId = "";
        public static int TopicId;      // 0: none

        // MUNIN_TELEGRAM_API is for testing only (pointing the sender at a stand-in): not a setting
        private static string Api
        {
            get
            {
                string env = null;
                try { env = Environment.GetEnvironmentVariable("MUNIN_TELEGRAM_API"); } catch { }
                return string.IsNullOrEmpty(env) ? "https://api.telegram.org" : env.TrimEnd('/');
            }
        }

        public static void Enqueue(Post p)
        {
            lock (gate)
            {
                queue.AddLast(p);
                while (queue.Count > MaxQueue) queue.RemoveFirst();
                if (thread == null)
                {
                    thread = new Thread(Loop) { IsBackground = true, Name = "MuninMap telegram" };
                    thread.Start();
                }
                Monitor.PulseAll(gate);
            }
        }

        // Waits until everything queued is sent, at most timeoutMs. True when the queue emptied in time.
        public static bool Flush(int timeoutMs)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            lock (gate)
            {
                while (queue.Count > 0 || busy)
                {
                    int left = (int)(until - DateTime.UtcNow).TotalMilliseconds;
                    if (left <= 0) return false;
                    Monitor.Wait(gate, left);
                }
            }
            return true;
        }

        private static void Loop()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            while (true)
            {
                Post p;
                lock (gate)
                {
                    while (queue.Count == 0) Monitor.Wait(gate);
                    p = queue.First.Value; queue.RemoveFirst();
                    if (p.Kind == "chat") p = MergeChat(p);
                    busy = true;
                }
                try { WaitForToken(); Send(p); }
                catch (Exception e) { Log("post failed: " + Clean(e.Message)); }
                finally { lock (gate) { busy = false; Monitor.PulseAll(gate); } }
            }
        }

        // consecutive chat posts already waiting go out as one, one line each (caller holds the lock)
        private static Post MergeChat(Post p)
        {
            var sb = new StringBuilder(p.Html);
            while (queue.Count > 0)
            {
                var next = queue.First.Value;
                if (next.Kind != "chat" || next.Silent != p.Silent || sb.Length + 1 + next.Html.Length > TelegramFormat.MaxText) break;
                sb.Append('\n').Append(next.Html);
                queue.RemoveFirst();
            }
            p.Html = sb.ToString();
            return p;
        }

        private static void WaitForToken()
        {
            while (true)
            {
                var now = DateTime.UtcNow;
                tokens = Math.Min(Burst, tokens + (now - refilled).TotalSeconds / RefillSeconds);
                refilled = now;
                if (tokens >= 1) { tokens -= 1; return; }
                Thread.Sleep((int)Math.Ceiling((1 - tokens) * RefillSeconds * 1000));
            }
        }

        private static void Send(Post p)
        {
            long editId = 0;
            if (p.EditId != null) { editId = p.EditId(); if (editId == 0) return; }

            string buttons = p.Buttons;
            int networkTries = 0;
            while (true)
            {
                string body = Body(p, buttons, editId);
                var r = Call(p.Method, body);
                if (r.ok)
                {
                    if (p.OnSent != null && r.messageId != 0) p.OnSent(r.messageId);
                    return;
                }
                if (r.code == 429)
                {
                    int wait = Math.Max(1, Math.Min(r.retryAfter > 0 ? r.retryAfter : 5, 60));
                    Thread.Sleep(wait * 1000);
                    continue;
                }
                if (r.code == 0 && ++networkTries <= 2) { Thread.Sleep(2000); continue; }
                // a button Telegram won't take (map_url not a public web address): once more without it
                if (r.code == 400 && buttons != null && buttons != p.FallbackButtons && (r.description ?? "").IndexOf("BUTTON", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Log("Telegram refused a button (" + Clean(r.description) + "); sent without the map button. Check map_url.");
                    buttons = p.FallbackButtons;
                    continue;
                }
                Log(p.Kind + " post not sent: " + (r.code > 0 ? r.code + " " : "") + Clean(r.description));
                return;
            }
        }

        private static string Body(Post p, string buttons, long editId)
        {
            var j = new JsonWriter(256 + (p.Html?.Length ?? 0)).BeginObject();
            j.Prop("chat_id", ChatId);
            if (editId != 0) j.Prop("message_id", editId);
            else if (TopicId != 0) j.Prop("message_thread_id", TopicId);
            j.Prop("text", p.Html ?? "").Prop("parse_mode", "HTML");
            j.Key("link_preview_options").BeginObject().Prop("is_disabled", true).End();
            if (p.Silent && editId == 0) j.Prop("disable_notification", true);
            if (buttons != null) j.Key("reply_markup").Raw(buttons);
            return j.End().ToString();
        }

        private struct Result { public bool ok; public int code; public string description; public long messageId; public int retryAfter; }

        private static Result Call(string method, string json)
        {
            var r = new Result();
            string reply = null;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(Api + "/bot" + Token + "/" + method);
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.UserAgent = "MuninMap/" + MuninMap.VERSION;
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;
                byte[] data = Encoding.UTF8.GetBytes(json);
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    reply = rd.ReadToEnd();
            }
            catch (WebException e)
            {
                var resp = e.Response as HttpWebResponse;
                if (resp == null) { r.code = 0; r.description = e.Status + ": " + e.Message; return r; }
                r.code = (int)resp.StatusCode;
                try { using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) reply = rd.ReadToEnd(); } catch { }
                resp.Close();
            }
            catch (Exception e) { r.code = 0; r.description = e.Message; return r; }

            var d = reply != null ? JsonParser.Parse(reply) as Dictionary<string, object> : null;
            if (d == null) { if (r.description == null) r.description = "unreadable reply"; return r; }
            r.ok = JsonParser.Bool(d, "ok");
            if (!r.ok)
            {
                if (r.code == 0) r.code = (int)JsonParser.Num(d, "error_code");
                r.description = JsonParser.Str(d, "description");
                var prm = JsonParser.Obj(d, "parameters");
                if (prm != null)
                {
                    r.retryAfter = (int)JsonParser.Num(prm, "retry_after");
                    // a basic group that Telegram upgraded to a supergroup has a new id
                    long moved = (long)JsonParser.Num(prm, "migrate_to_chat_id");
                    if (moved != 0) r.description += "; the group has a new id now: set telegram_chat_id = " + moved;
                }
                return r;
            }
            var msg = JsonParser.Obj(d, "result");
            if (msg != null) r.messageId = (long)JsonParser.Num(msg, "message_id");
            return r;
        }

        // the token never reaches the log, even inside a message that quotes the URL
        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return string.IsNullOrEmpty(Token) ? s : s.Replace(Token, "<token>");
        }

        // failures: the first one, then at most one a minute
        private static void Log(string text)
        {
            var now = DateTime.UtcNow;
            if ((now - lastLog).TotalSeconds < 60) return;
            lastLog = now;
            ZLog.LogWarning("MuninMap: Telegram: " + text);
        }
    }
}
