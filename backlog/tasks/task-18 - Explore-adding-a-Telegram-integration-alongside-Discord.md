---
id: TASK-18
title: Explore adding a Telegram integration alongside Discord
status: To Do
assignee: []
created_date: '2026-11-20T05:18:00Z'
labels:
  - plugin
dependencies: []
references:
  - src/Server/DiscordWebHook.cs
  - src/MuninMap.cs
type: spike
ordinal: 17000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Explore adding a Telegram integration alongside the existing Discord one, for communities that use Telegram. Before deciding anything, the Discord integration needs to be fully understood, because a Telegram version would most likely mirror it.

Starting facts (from src/MuninMap.cs, src/Server/DiscordWebHook.cs, src/Config.cs):
- It is a one-way Discord webhook. `discord_webhook` holds the URL; Munin posts with .NET's WebClient (form field `content`) and never reads anything back from Discord.
- It posts:
  - "<server> is online" with the server's address and port, its password and `map_url`;
  - "<server> is offline";
  - "player <name> joined" and "player <name> left";
  - with `discord_world_events` on (default), raids and invasions starting and ending, with a link to the spot on the map when `map_url` is set.
- `discord_invite_url` adds a Discord invite link to the page (sent to it in the page config). This is separate from the webhook.
- Posts are sent synchronously from the game's own code paths (the join and leave handlers, startup, shutdown, world events). A slow or unreachable webhook URL may stall the server for the length of the request; to check.

Telegram's equivalent would be a bot: a bot token and a chat ID, posting with the Bot API's sendMessage. That is also one-way, but it needs two settings and different message formatting (Telegram's MarkdownV2 or HTML, not Discord's markdown).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 What the Discord integration does is written down in full: every message it posts and when, the settings, the page's invite link, how sending works (thread, timeouts, failures) and anything it sends that an admin should know about (the server password in the online message)
- [ ] #2 How a Telegram integration would match it is written down: the settings it needs, message formatting, which messages it would send, and what can be shared with the Discord path rather than duplicated
- [ ] #3 A proposal (or a decision not to go ahead) is recorded and agreed with the user before any implementation starts
- [ ] #4 If implemented: Telegram posts the agreed messages to the configured chat, sending never stalls the server, and the settings are documented
- [ ] #5 If implemented: `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [ ] #6 If implemented: written per decision-2: new behaviour in new files, upstream edits marked where the format allows, and every commit subject starts with the task ID
<!-- AC:END -->
