---
id: TASK-34
title: 'Telegram: choose which kinds of post are sent, as finely as which are silent'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-24T07:08:22Z'
updated_date: '2026-11-24T07:35:54Z'
labels:
  - plugin
dependencies: []
type: feature
ordinal: 41000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Telegram (TASK-18) lets an admin pick silence per kind of post: telegram_post_silent takes any of online, offline, join, leave, raid, chat. What is posted at all is coarser: online, offline, joins and leaves are always posted; raids and invasions are one switch (telegram_world_events); chat is telegram_post_chat (off, shouts, all). So an admin can't, for example, post raids but not joins and leaves, or joins but not leaves.

Add the same per-kind choice for what is posted, with the same kind names as telegram_post_silent, so the two settings read alike. Settled with the user:
- The new setting replaces telegram_world_events, and the on/off part of telegram_post_chat: whether chat is posted becomes the chat kind.
- Which chat is posted stays a separate setting, renamed to fit (suggested: telegram_chat_scope), with the values shouts and all, default shouts. Normal chat only reaches players nearby in the game, so all publishes it to the whole group. Whispers are never posted.
- No backward compatibility code: an existing config's telegram_world_events and telegram_post_chat values are not migrated.
- online off, offline on: the offline post is sent, and there is no online post to strike through.
- online on, offline off: the online post is not struck through, and no offline post is sent.
- The default posts what is posted today, so a server that sets nothing new sees no change in its group.

Ref: TASK-18 (src/Patches/Telegram.cs Bind and Queue, the kinds list), docs/ADVANCED.md (the Telegram section and the telegram_* settings rows).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 An admin can choose, per kind (online, offline, join, leave, raid, chat), whether it is posted, with the same kind names as telegram_post_silent; unknown kinds are logged and ignored, as telegram_post_silent does
- [x] #2 docs/ADVANCED.md: the telegram_* settings rows and the Telegram section describe the new choice (and any setting replaced or migrated)
- [x] #3 PATCHES.md: the existing TASK-18 Telegram entry is updated; no new heading
- [x] #4 patches-tools/release-notes.md gets a line for this task when it is Done
- [x] #5 With default settings the group gets exactly the posts it gets today
- [x] #6 online off, offline on: the offline post is sent, nothing is struck through; online on, offline off: the online post is not struck through and no offline post is sent
- [x] #7 telegram_world_events and telegram_post_chat are gone, with no migration code; whether chat is posted is the chat kind, and a renamed setting (shouts or all, default shouts) chooses which chat
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Telegram.cs: telegram_post (kinds posted, default online, offline, join, leave, raid) and telegram_chat_scope (shouts/all, default shouts) replace telegram_world_events and telegram_post_chat, no migration; one Kinds() parser for telegram_post and telegram_post_silent; each hook checks its kind; Offline() sends nothing without offline, and edits the online card only when online is posted too.
2. docs/ADVANCED.md settings rows and Telegram section; TASK-18 PATCHES.md entry.
3. Test server with a test bot, chaining restarts (a shutdown uses the previous run's settings): defaults, offline only, online only, join/leave/chat, an unknown kind.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Tested on the test server with a test bot (user watched the chat): defaults posted online, then at shutdown struck it through and posted offline; offline only: no online post, offline post alone at shutdown; online only: online post, nothing at shutdown and the card not struck; join, leave, chat: join and leave posted, no online post; 'raids' logged as an unknown kind and ignored. Not exercised live: the raid and chat kinds (chat needs a second player online; a raid needs one to happen); both go through the same per-kind check as the others. BepInEx keeps an existing config's old telegram_world_events and telegram_post_chat lines as orphans; they do nothing.

Release-notes line added (after trimming the lines released in patches-3.4.0-8). Accepted by the user with raid and chat not exercised live.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Telegram's posts are now chosen per kind, like silence: telegram_post lists the kinds sent (online, offline, join, leave, raid, chat; default all but chat, i.e. what was posted before) and telegram_chat_scope picks shouts (default) or all chat. They replace telegram_world_events and telegram_post_chat, with no migration. Without offline, nothing goes out at shutdown and the online post stays as it is; without online, the offline post goes alone. Unknown kinds are logged and ignored. Docs: ADVANCED.md settings rows and Telegram section, the TASK-18 PATCHES.md entry. Tested with a test bot for defaults, offline only, online only, join/leave, and an unknown kind; raid and chat not exercised live.
<!-- SECTION:FINAL_SUMMARY:END -->
