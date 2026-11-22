---
id: TASK-18
title: Post Munin's server notices, and optionally chat, to a Telegram group
status: In Progress
assignee:
  - '@claude'
created_date: '2026-11-20T05:18:00Z'
updated_date: '2026-11-22T19:47:58Z'
labels:
  - plugin
dependencies: []
references:
  - src/Server/DiscordWebHook.cs
  - src/MuninMap.cs
  - src/Live/WorldEvents.cs
  - src/Server/MapDataServer.cs
  - https://core.telegram.org/bots/api
type: feature
ordinal: 17000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Some communities run on Telegram, not Discord. The user wants Munin connected to a Telegram group: the server notices Munin already posts to Discord should also go to that group, and, as an option, the in-game chat too. This direction (game to Telegram) is decided. The other direction, Telegram messages shown in the game, is not decided and is tracked as DRAFT-1 ("Telegram to game").

New settings, in the `[Server]` section next to the `discord_*` ones (names proposed; the plan agreed under criterion #1 may change them, and then this list changes too):
- `telegram_bot_token` (empty): the bot's token from @BotFather. A secret: whoever holds it is the bot. Empty means the Telegram integration is off.
- `telegram_chat_id` (empty): the group to post to, a negative number such as `-1001234567890`.
- `telegram_topic_id` (empty): optional, for groups with topics: post into that one topic (the Bot API's `message_thread_id`).
- `telegram_world_events` (true): also post raids and invasions starting and ending, with the map link; the twin of `discord_world_events`.
- `telegram_post_chat` (`off`): relay in-game chat to the group: `off`, `shouts` (shouts only) or `all` (normal chat and shouts).
- `telegram_post_address` (empty): the address shown on the online card and copied by its Address button (added at the user's request 2026-11-23). Empty: detected, the game's public IP and the server's `-port`. A host name alone (`valheim.example.com`) gets the detected port added; with a port (`valheim.example.com:24560`) it is shown as given, for a DNS name or an external port forwarded to a different one.
- `telegram_post_password` (false): include the server password in the online post.
- `telegram_post_user_key` (false): include the user key (the player-claim secret from TASK-20, read from the `user.token` file beside `announce.token`, not a setting) in the online post. Posted only when this is on and the file exists and is not empty; the post reads the file when it is sent, so a changed key is picked up.
- `telegram_post_silent` (empty): kinds of post sent silently, comma separated, from `online`, `offline`, `join`, `leave`, `raid` (raids and invasions) and `chat`. Silent is Telegram's `disable_notification`: the post appears in the group as usual but phones make no sound. Empty: none are silent, every post behaves like a normal message and members' own notification settings apply.

`map_url` and `hidden_players` are existing settings this also uses.

Naming: settings for what Munin sends to the group start `telegram_post_`, except `telegram_world_events`, which keeps the name of its Discord twin. If DRAFT-1 (Telegram to game) goes ahead, its settings get their own direction-specific names (proposed: `telegram_show_in_game` for the mode), so no setting here needs renaming.

Message look (agreed in outline with the user 2026-11-22; exact layout and wording settled under criterion #1). Telegram can do more than Discord's markdown, and the posts should use it where it helps:
- Short notices (join, leave, raid and invasion, chat): ordinary `sendMessage` with HTML formatting, one line each, an emoji per kind. Raid and invasion starts get an inline `url` button "🗺 Open map" to the spot when `map_url` is set.
- Quiet posts: an admin can send chosen kinds silently with `telegram_post_silent` (none by default), e.g. `join,leave,chat` for a busy group, so routine posts don't buzz phones and members don't mute the whole group.
- Times: no post shows a time of its own; Telegram's timestamp on each post is enough (the user dropped "online since", 2026-11-22). If a later post needs one, Telegram's date-time entity (`<tg-time>`, Bot API 9.5) shows it in each reader's time zone.
- The online post as a small card: the server name in bold, the address in monospace, and the buttons below. Plain HTML `sendMessage` like every other post: `sendRichMessage` (Bot API 10.1, June 2026) was tried and works in groups, but the user preferred the plain card (step 0, 2026-11-23).
- Easy copying: inline keyboard `copy_text` buttons (Bot API 7.11; tap copies, Telegram shows "Copied", up to 256 characters) for the address, and for the password and user key when their settings are on. The address also appears as monospace text (`<code>`), which Telegram copies on a tap. Whether the password and user key also show in the text (plainly, in monospace or as a spoiler) is a presentation choice settled with the wording. It is not a security measure: whatever the bot posts, buttons included, everyone in the group can read. `telegram_post_password` and `telegram_post_user_key` decide whether they reach the group at all.
- Offline: edit the online card in place (`editMessageText`) to say the server is offline, instead of a new post, if that proves reliable; otherwise a separate post as Discord does.

How the Discord integration works today, which this mirrors (read from the code 2026-11-22; details in the notes):
- One-way webhook: `discord_webhook` holds the URL, `src/Server/DiscordWebHook.cs` posts the form field `content` with `WebClient.UploadValues` and reads nothing back.
- Posts, each starting with `🎮 **<server name>**`: online 🟢 (with IP:port, the server password and `map_url`), offline 🔴, `player _<name>_ joined`, `player _<name>_ left`, and with `discord_world_events` (default on) raids (⚔️) and invasions (🔥) starting and ending. A start post carries a `[map](url#x,z,6)` link when `map_url` is set.
- Call sites: `NotifyOnline`/`NotifyOffline`/`NotifyJoin`/`NotifyLeave`/`PostWorldEvent` in `src/MuninMap.cs`, world events from `src/Live/WorldEvents.cs`. Settings are bound in `src/Config.cs`.

Telegram's side: a bot made with @BotFather and added to the group. Its token lives in the server's own config, never in the repo. Posting uses the Bot API's `sendMessage`, outgoing HTTPS only.

Finding the group's chat ID (tried by the user 2026-11-22; these steps go into the docs):
1. Add the bot to the group.
2. In the group, send `/start@<bot's username>`. A plain `@<bot> hello` is not enough: with privacy mode on (the default), a bot in a group receives only commands addressed to it, replies to its own messages and service events, so a mention never reaches it and the next step returns `{"ok":true,"result":[]}`. Removing the bot from the group and adding it back also works (it produces a `my_chat_member` update).
3. Open `https://api.telegram.org/bot<token>/getUpdates` in a browser (or curl it). The reply holds `"chat":{"id":-100...,"title":"..."}`; that number is `telegram_chat_id`.
4. Still empty: something else is reading the bot's updates (another program using the same token), or the bot has a webhook set (`getWebhookInfo` shows its `url`).

Munin needs no code for this. A possible extra, not in scope: with the token set and `telegram_chat_id` empty, Munin calls `getUpdates` and writes the chat IDs it finds to the server log (console and BepInEx `LogOutput.log`, also shown in the page's Events > Server log).

In-game chat is already read on the server: the `RouteRPC`/`HandleRoutedRPC` patch in `src/MuninMap.cs` decodes sender, text, type and position for every chat message and hands it to `MapDataServer.AddMessage` (the web page's Events panel). Whispers and Munin's own chat commands are already filtered there. Normal chat is proximity-only in game, so `telegram_post_chat = all` publishes local talk to the whole group.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A plan is agreed with the user before implementation, settling: all chat or shouts only for the chat relay, whether the password is ever posted, topic support, and the exact message wording and emoji
- [x] #2 With a bot token and chat ID set, Munin posts to the group the notices the Discord webhook posts: online, offline, player joined, player left, and raid and invasion start and end (with the map link when map_url is set)
- [x] #3 Each kind of notice carries its own emoji, so Munin's posts stand apart from people's messages in the group
- [ ] #4 An option, off by default, relays in-game chat to the group with the player's name and message; whispers and Munin's chat commands are never relayed
- [x] #5 Text from players is escaped for Telegram's formatting, so names or messages with markup characters post as typed
- [x] #6 Players in hidden_players are left out of Telegram posts
- [x] #7 Neither the server password nor the user key is posted to Telegram unless an admin turns that on, each with its own setting; the user key only when the user.token file exists and is not empty
- [x] #8 Sending never stalls the server: posts go out off the main thread, and a slow, failing or unreachable Telegram only delays or drops posts
- [x] #9 With the Telegram settings empty nothing is sent to Telegram, and the Discord path is untouched: the diff leaves src/Server/DiscordWebHook.cs and the Discord message text unchanged, adding only Telegram calls beside the existing Discord calls (checked by reading the diff; no live Discord test)
- [x] #10 The settings are documented in docs/ADVANCED.md only (its settings table, plus a Telegram section with making the bot in @BotFather and finding the chat ID step by step as in the description: /start@bot or re-adding the bot, since a plain @mention is not delivered in privacy mode); README.md is not changed
- [x] #11 PATCHES.md has an entry for this change: what changes for players and admins, and how it differs from upstream
- [x] #12 Written per decision-2: new behaviour in new files, upstream edits marked where the format allows, every commit subject starts with the task ID
- [x] #13 The online card's format is chosen by a test before it is built: sendRichMessage tried by hand in a basic group, a supergroup and a supergroup topic (and an older app if one is to hand); rich messages if they work and look right in all of them, plain HTML otherwise, with no runtime fallback; the test result is recorded in the task notes
- [x] #14 Posts of the kinds listed in telegram_post_silent are sent silently (Telegram's disable_notification: shown in the group as usual, no sound on phones); with the setting empty, the default, no post is silent; unknown kinds in the list are logged and ignored
- [x] #15 When posted, the server address, the password and the user key can each be copied with one tap (copy buttons)
- [x] #16 telegram_post_address, when set, replaces the detected address on the online card and its Address button: a host name alone gets the detected port added, a host:port is used as given
- [x] #17 With no server password, the online card has no Password button, and with telegram_post_password off it has none either
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
Researched 2026-11-22 against `patches` at 0d6649f. Approved by the user 2026-11-22 (criterion #1); the choices are recorded at the end.

0. Before building the online card, test `sendRichMessage` by hand (curl) with the user's bot: in a basic group, in a supergroup, in a topic of a supergroup with topics, and how the card looks in an older Telegram app if one is to hand. It works in all of them and looks right: the card uses rich messages. It fails anywhere or looks wrong: the card uses plain HTML. The result and the choice go in the task notes. Everything else in the plan does not depend on it.
1. New files we own, in `src/Patches/` beside Access.cs, Seed.cs and Claims.cs (decision-2):
   - `Telegram.cs`: binds the eight settings in `[Server]` (`Bind(config)`, the Seed.cs pattern), and the hooks the upstream code calls: `Online()`, `Offline()`, `Join(name)`, `Leave(name)`, `WorldEvent(message, x, z)`, `Chat(type, name, text)`. Each hook does nothing when `telegram_bot_token` or `telegram_chat_id` is empty, never throws (catches everything), and only formats the post and queues it.
   - `TelegramFormat.cs`: message text and buttons, as pure functions (HTML escaping, the online card, short notices, chat lines, the silent check), so they can be read and checked on their own.
   - `TelegramSender.cs`: one background thread, posts paced by a bucket (5 at once, then one every 3 s), (`IsBackground`, the UpdateCheck.cs pattern) and a queue. Posts with `HttpWebRequest`, TLS 1.2, 10 s timeout, JSON bodies via `Util/JsonWriter`, replies read with `Util/JsonParser`.
2. Upstream edits, one marked line each (`// patches: TASK-18`), the Telegram call placed first so a failing Discord post cannot skip it, Discord lines untouched (criterion #9):
   - `src/Config.cs`: `Patches.Telegram.Bind(config);` next to the Access and Seed binds.
   - `src/MuninMap.cs`: `NotifyOnline`, `NotifyOffline`, `NotifyJoin`, `NotifyLeave`, and `PostWorldEvent` (before its Discord-only early return).
   - `src/Server/MapDataServer.cs` `AddMessage`: `Patches.Telegram.Chat(type, name, message);` after the "I have arrived!" filter. This is the one place all chat already passes, after Munin's chat commands are taken out. Telegram drops whispers there itself, plus anything not allowed by `telegram_post_chat`.
3. Sending (criterion #8):
   - The game thread only queues. The sender thread posts one at a time, at most one post every 3 s: Telegram allows a bot about 20 posts a minute in one group.
   - When chat lines pile up behind that limit, consecutive ones merge into a single post.
   - HTTP 429: wait `retry_after`, then retry. Network errors: retry twice with a short wait, then drop. The queue is capped at 100 posts; the oldest is dropped first.
   - Failures go to the server log at most once a minute. The token never goes in a log line or exception message (it is part of the request URL).
4. Shutdown: `Offline()` runs inside `ZNet.Shutdown`, and the process can exit right after. So it waits up to 3 s for the queue to empty, the offline post included, then gives up. That is the only place the game thread waits, bounded, and only at shutdown.
5. Online card (criteria #13, #14):
   - Sent with `sendMessage` and `parse_mode: HTML`, like every other post (step 0 result, user's choice 2026-11-23). A rejected post is an ordinary failed post, logged like any other.
   - Content: the server name as heading; the address in `<code>`; the map link. No "online since": Telegram already timestamps the post, and it goes out when the server comes online (user, 2026-11-22).
   - Inline keyboard: `copy_text` buttons "📋 Address", plus "🔑 Password" when `telegram_post_password` is on and the server has one, and "🗝 User key" when `telegram_post_user_key` is on and `Claims.Token` is set (read when sending). Then a `url` button "🗺 Open map" when `map_url` is set.
   - Whether password and key also appear in the text is settled with the wording (choice A); it is presentation only, the buttons and the text are equally readable by the group.
   - The address: `telegram_post_address` when set (a bare host gets the game port added), otherwise `ZNet.GetServerIP` plus `SteamManager.m_serverPort` (the `-port` argument). Not `ZNet.GetHostPort()`, which answers 1 on the server.
   - The card's `message_id` is kept for step 6.
6. Offline: a short "🔴 offline" post (notifies, like Discord), and the online card edited (`editMessageText`) to its first line struck through, with its buttons removed, so the old password or key can't be copied from it. If the edit fails, it's skipped.
7. Short notices (criteria #2, #3, #5):
   - Join, leave, raid and invasion: `sendMessage`, HTML, names and text escaped.
   - Raid and invasion starts get an "🗺 Open map" `url` button to `map_url#x,z,6` when `map_url` is set, the same link Discord posts.
   - `telegram_world_events` off: no raid or invasion posts.
   - `telegram_topic_id` set: every post and edit carries `message_thread_id`.
   - Kinds listed in `telegram_post_silent` get `disable_notification`. An unknown kind is logged once at startup and ignored (criterion #15).
8. hidden_players (criterion #6):
   - Join, leave and chat from a hidden player are not posted.
   - Raid and invasion posts arrive as finished text ending "(near A, B and C)". Telegram rebuilds that list without hidden names. When every player near is hidden (one or several), the whole " (near ...)" suffix goes, so the post reads as if nobody was near. No upstream change (choice B).
9. Docs: `docs/ADVANCED.md` gets the settings rows and a "Telegram" section at the end of the file: making the bot in @BotFather, the chat ID steps from the description, topics, and the password and key settings. README.md is not changed. PATCHES.md gets a TASK-18 entry.
10. Test on a test server with a test bot and group:
    - Server start: card, buttons and copy on phone and desktop.
    - The online card as built from the step 0 result.
    - Join, leave and chat: the user connects.
    - A raid: forced with the `event` command, or another way to start one on the test server.
    - Shutdown: the offline post and the card edit.
    - `telegram_post_silent`, `telegram_topic_id`, hidden_players.
    - Stall test: an environment variable `MUNIN_TELEGRAM_API` (not a setting; only for testing) points the sender at an address that never answers. Then check the game is not delayed and shutdown takes at most about 3 s.
    - Discord: by reading the diff only (criterion #9).
11. One commit, subject starting with "TASK-18:", with the backlog changes folded in.

Choices, settled by the user 2026-11-22:
A. Wording: as proposed in the notes, without "online since" (on the card, and on the card when edited to offline). Password and key in the buttons only.
B. hidden_players in raid and invasion "near" lists: filtered in Telegram only (step 8). All near players hidden: no "(near ...)" suffix at all.
C. Offline: build the recommended post plus card edit (step 6); the user decides after seeing it and may change it.
D. Chat lines piling up behind the rate limit merge into one post (step 3).

12. Shutdown (raised by the user 2026-11-22: the Events panel never shows "server offline"). That is because nothing writes one: the `ZNet.Shutdown` patch posts to Discord but never calls `Events.Add`, and it stops the web server first. It says nothing about how much time shutdown gets.
    - Supported: graceful shutdown only, i.e. whenever the game itself runs `ZNet.Shutdown`, however the admin stops it (Linux or Windows, with or without Docker, a console, a script or a panel). A forced kill or crash never runs the patch, so no offline post can go out (Discord included); the docs say so in one line. No stop method gets special handling.
    - The wait stays short (3 s at most) because grace periods differ between setups and some are only a few seconds.
    - `Offline()` writes a log line when it starts and another when the queue is empty or the wait runs out, with the time taken.
    - Test: one graceful stop of the test server: both log lines appear and the offline post reaches the group.
    - Out of scope, possible follow-up: an "offline" line in the Events panel too.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-11-22, findings from reading the Discord code (no code changed):
- `discord_invite_url` does nothing on the page. `src/Config.cs:430` sends it in the page config, but nothing in `web/` reads it, though docs/ADVANCED.md says "shown on the page".
- Every Discord post runs synchronously on the game's main thread with no timeout of our own (.NET's 100 s default). Online, offline and world-event posts swallow errors; `NotifyJoin` and `NotifyLeave` do not, so a failing webhook throws inside the `ZRoutedRpc.AddPeer` and `ZNet.Disconnect` patches and likely skips the event feed, stats and `Players.Forget` calls after it. A bug in the existing Discord path; possibly its own task, not decided.
- The online post includes the server password in plain text.
- `hidden_players` are still named in Discord join and leave posts (documented in ADVANCED.md).
- README.md says Discord gets "joins, deaths, raids and more"; deaths, chat and boss kills are not posted, they only reach Munin's Events panel.
- The form values sit in one static collection shared by all senders: not thread-safe, harmless while everything sends from the main thread, but a Telegram sender on another thread must not copy the pattern.
- One bot token can be read by only one program (getUpdates returns HTTP 409 to a second reader); this matters only if the Telegram-to-game draft goes ahead, but it argues for one bot per server from the start.

The Telegram-to-game direction is DRAFT-1.

For the implementer: `NotifyJoin`/`NotifyLeave` do not catch a failing Discord post, so a Telegram call placed after the Discord one would be skipped whenever Discord throws. Put the Telegram call first, or guard it, so the two paths cannot break each other, without changing the Discord lines themselves (criterion #9).

Proposed wording, for choice A in the plan (HTML; names and text escaped; the server name in bold leads the online, offline, raid and invasion posts, as on Discord):
- Online card: heading "🟢 <b>My Server</b> is online"; "Online since <tg-time>"; "💻 <code>203.0.113.5:2456</code>"; buttons [📋 Address] [🔑 Password] [🗝 User key] / [🗺 Open map]
- Offline post: "🔴 <b>My Server</b> is offline". The card becomes "🔴 <b>My Server</b> is offline (was online since <tg-time>)", with no buttons.
- Join: "➡️ <b>Anna</b> joined". Leave: "⬅️ <b>Anna</b> left".
- Raid start: "⚔️ <b>My Server</b> Raid: The ground is shaking (near Anna and Bob)" [🗺 Open map]. Raid end: "⚔️ <b>My Server</b> Raid: is over" (or the game's own end text).
- Invasion start: "🔥 <b>My Server</b> Ashlands army has begun (near Anna)" [🗺 Open map]. Invasion end: "🔥 <b>My Server</b> Ashlands army is over".
- Chat: "💬 <b>Anna</b>: text". Shout: "📢 <b>Anna</b>: text". Several merged: one post, one line each.
Join, leave and chat drop the server name: in a group for one server it is noise.

Plan approved by the user 2026-11-22 (criterion #1). Changes to the proposed wording above:
- No "Online since" on the online card, and no "(was online since ...)" when the card is edited to offline: Telegram already timestamps the post, and it goes out when the server comes online. The offline card reads "🔴 <b>My Server</b> is offline", with no buttons.
- Password and user key appear in the copy buttons only, not in the card text.
- Raid or invasion near only hidden players (one or several): the post has no "(near ...)" suffix at all.
- Offline handling (post plus card edit) is built as proposed; the user will decide after seeing it.

Step 0 result (2026-11-23): `sendRichMessage` works in a supergroup and in a basic group (a test card with a heading, a bordered table and copy buttons, sent with the user's bot). Not tried: a topic in a supergroup, an older Telegram app. The user found the rich card "not particularly impressive" compared with the plain HTML card (bold name, monospace address, copy buttons, map button), so the online card is plain HTML via `sendMessage`, like every other post. No rich-message code.
Also found while testing: a token pasted with the `bot` prefix from an API address (`bot123:ABC`) makes every call 404 "Not Found"; Munin now strips that prefix. A basic group that Telegram upgrades to a supergroup gets a new `-100...` id; Munin's log line passes on Telegram's `migrate_to_chat_id`.

Test results, 2026-11-23, on a test server with a test bot and group:
- Online card, join, leave: posted as designed.
- The address on the card first showed port `:1`. `ZNet.GetHostPort()` asks the Steam socket, which always answers 1 on the host; the real port is `SteamManager.m_serverPort` (the `-port` argument), which the Telegram card now uses. The Discord online post uses `GetHostPort()` too, so upstream posts `:1`: an upstream bug, left alone here (criterion #9).
- Offline handling (choice C): the edited card and the new offline post first read the same, so it looked like a duplicate. The user chose to strike the old card through instead ("<s>🟢 <b>name</b> is online</s>", buttons removed), and keep the offline post. Confirmed: the struck card and one offline post. The offline post and card edit took 0.8-1.9 s.
- Online card with `telegram_post_address` set to a host name: shown with the game port added; copy buttons for the address and the user key (`user.token` present), and the map button. With no server password: no Password button. Telegram accepted both a public `https://` address and a local `http://127.0.0.1:<port>` address as the map button URL.
- Join and leave of a player in `hidden_players`: neither posted.
- Raid, started at the player with a throwaway plugin on the test server (the vanilla `event` command can't be sent by a remote admin): "⚔️ **<server>** Raid: Eikthyr rallies the creatures of the forest (near <player>)", and the end post "... Raid: The creatures are calming down". With that player hidden: no "(near ...)" at all.
- Invasions: not triggered live; same hook and formatting as raids, checked by the format test below.
- Telegram unreachable: a refused connection dropped the online post with one log line, and the server came online as fast as usual. A stand-in that accepts connections and never answers: the server came online as usual; a graceful stop during the hang gave up after 2999 ms; shutdown while posts were refused and retried also gave up at 2999 ms.
- Format test (the built DLL's TelegramFormat called by reflection): hidden names removed from 2-4 name lists in every position, suffix gone when all are hidden, "is over" untouched, invasion text; names and messages with `<`, `>`, `&` escaped; an empty password leaves the Password button out.
- Chat relay: not testable with one player. In Valheim 1.0 chat is addressed to the other players and only passes through the server on its way, so with nobody else online nothing reaches it (Munin's own Events log had no chat either). The Telegram hook sits in `MapDataServer.AddMessage`, where the Events feed gets chat, so chat in the Events panel means the hook ran. To verify with two players: shouts and normal chat posted per `telegram_post_chat`, whispers never.
- The game's own public-IP lookup failed once on a fresh start (`ZNet.GetPublicIP` reuses its HttpClient on the retry and throws). A game bug, not Munin's; the detected address can be wrong or empty because of it, one more reason for `telegram_post_address`.
- Upstream diff: seven added lines, each marked `// patches: TASK-18`, in Config.cs, MuninMap.cs and MapDataServer.cs; DiscordWebHook.cs and the Discord message text unchanged.

Silent posts (2026-11-23): with `telegram_post_silent = online,join,leave,raid` the online card arrived with no sound; after a restart with `telegram_post_silent = leave` the online card made one (confirmed by the user). Hidden-name safety net added: `TelegramFormat.SafeWithoutHidden` checks the finished raid text for any hidden name as a whole word; if one survives (upstream reworded the "(near ...)" list), the text is cut back before it and a warning is logged once. Format check: 7 cases incl. "near: Bob & Anna", translated wording, square brackets, and "Bobcats" not taken for "Bob".

Settings off (2026-11-23): server with a password and `user.token` present, `telegram_post_password` and `telegram_post_user_key` false: the card had only the Address and Open map buttons (confirmed by the user).

decision-2 (2026-11-23): behaviour in three new files under src/Patches/; upstream files only gain seven marked one-line calls (Config.cs, MuninMap.cs, MapDataServer.cs) plus a docs section at the end of docs/ADVANCED.md with a marker on its heading (table rows cannot carry one); commits 4491518 and 91eac44 on `telegram-notes`, both "TASK-18:", to be squashed into one when merged into `patches`.
<!-- SECTION:NOTES:END -->
