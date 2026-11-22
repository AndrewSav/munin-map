---
id: DRAFT-1
title: 'Telegram to game: show Telegram group messages to players in game'
status: Draft
assignee: []
created_date: '2026-11-22T09:32:37Z'
updated_date: '2026-11-22T10:11:46Z'
labels:
  - plugin
dependencies: []
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Not decided. If TASK-18 connects Munin to a Telegram group (game to Telegram), the group could also talk back: messages written in Telegram shown to players in game. The user has not decided whether to do this; two things need confirming first, listed at the end.

How it would work (from the Bot API and the mod's existing plumbing, nothing tested):
- Receiving: a background thread long-polls `getUpdates` (an open request Telegram answers as soon as a message arrives). Outgoing HTTPS only, so no open port; works behind NAT or Docker. Each update carries the sender's Telegram user ID, first and last name and @username, filled in by Telegram (the sender cannot fake the account, but display names are self-chosen).
- One reader per bot token: a second program reading the same bot gets HTTP 409, so the bot must not be used elsewhere.
- Messages sent while the server was down are kept by Telegram for up to 24 hours; Munin should drop that backlog on startup, not replay it.
- Showing in game: a server-only mod cannot put a line in an unmodded client's chat window (the client's permission check drops a server-made sender in silence; see `src/Server/Announce.cs`). What works is the `ShowMessage` RPC that restart announcements use (`Announce.Enqueue`, thread-safe, pumped on the main thread). `Center` is the big mid-screen text announcements use; `TopLeft` is the small corner notice and the likely choice, e.g. `[TG] Anna: on my way`. Seen by everyone online at once, for a few seconds, not in the chat log. Would need length cuts and rate limiting; stickers and photos skipped or shown as `[photo]`.
- No echo loop: these notices are not chat messages, so the TASK-18 chat relay will not send them back to the group, and bots never receive their own posts.
- The messages could also go into the web page's Events panel as their own kind, so they can be scrolled back through.
- Attribution: lines are labelled with the Telegram display name and a `[TG]` prefix. Possible extras: a setting mapping Telegram user IDs to character names, and an allowlist of who may post into the game. Without an allowlist, anyone in the group can post to the game.

Settings: named for this direction, so they never clash with TASK-18's `telegram_post_*` ones. Proposed: `telegram_show_in_game` for the mode (`off`, `command`, `reply`, `all`; default `off`); any later extras (name mapping, allowlist) follow the same direction-specific pattern. It reuses TASK-18's `telegram_bot_token` and `telegram_chat_id`.

To confirm before this becomes a task:
1. Notice usability: whether `TopLeft` notices are usable as chat in practice (readable, long enough on screen, what happens with several in a row), and whether Valheim's font draws emoji or shows boxes. Needs a test on the test server with a player connected.
2. Which of three modes to support, and the default:
   - Command: only messages starting with a command, e.g. `/v on my way`. Works with the bot's default privacy mode.
   - Reply: only replies to the bot's own posts. Works with the default privacy mode.
   - Everything: every text message in the group. Needs privacy mode off in @BotFather, or the bot made a group admin. Could flood players in a busy group.
   - Not a mode: a plain @mention of the bot. In privacy mode it is never delivered to the bot (seen by the user 2026-11-22: `getUpdates` came back empty after an @mention), so mentions only count in Everything mode.
<!-- SECTION:DESCRIPTION:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
