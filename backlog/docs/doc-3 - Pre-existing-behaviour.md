---
id: doc-3
title: Pre-existing behaviour
type: other
created_date: '2026-11-23T23:02:17Z'
updated_date: '2026-11-24T05:41:57Z'
---
Upstream behaviour noticed while working on the fork and left as it is.

## The sidebar jumps to the top when the server restarts

While the server restarts, the statistics panel empties: the sidebar's content shrank from 3,957 to 925 px, so its scroll position went from 3,032 to 0. When Munin answers again (about 25 s later), the panels are rebuilt at full height, but the scroll position isn't restored, so the sidebar stays at the top. Shortly after, Leaderboard grows by another 73 px as its data arrives. Measured 2026-11-24 (Valheim 1.0.21, Munin 3.4.0).

## Web pings share one rate limit behind a reverse proxy

The web ping limit (one per visitor every 10 s) is keyed by the connection's address
(`MapDataServer.cs`, `WebPing.Try(req.RemoteEndPoint...)`), so behind a reverse proxy all visitors
share one limit. See TASK-16. With the fork's `require_auth` on, the limit is per claimed player
instead (TASK-22); without it, this is still the case.

## The Discord online post shows port :1

The address in the Discord online post uses `ZNet.instance.GetHostPort()` (`MuninMap.cs`), which
gives 1 on a dedicated server. See TASK-18.

## discord_invite_url is never shown

Documented as "Discord invite shown on the page" and sent to the page in `/config` (`Config.cs`), but
no page code has ever read it, as far back as the repository's first commit. See TASK-18.

## The biome map of unexplored land is never rebuilt

`BiomeMap.cs` caches the biome picture on the assumption that the seed alone decides it ("it never
changes"); its cache check looks only at Munin's format and size, so a game update that changes world
generation leaves it stale.
