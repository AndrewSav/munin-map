---
id: TASK-22
title: Per-player web ping limit with require_auth
status: Done
assignee:
  - '@claude'
created_date: '2026-11-21T05:22:00Z'
updated_date: '2026-11-21T05:45:00Z'
labels:
  - plugin
milestone: m-1
dependencies:
  - TASK-20
references:
  - src/Server/WebPing.cs
  - src/Server/MapDataServer.cs
  - src/Patches/Access.cs
type: enhancement
ordinal: 21000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Found by TASK-16. The web ping limit (Server/WebPing.cs Try: 10 s per visitor, 2 s between any two pings) tells visitors apart by the connection's remote address (MapDataServer /api/ping passes req.RemoteEndPoint). Behind a reverse proxy every visitor arrives from the proxy's address, so the per-visitor 10 s becomes one ping per 10 s shared by everyone: one player's ping makes all others wait.

With require_auth on (TASK-20), every ping comes from a claimed player, so the limit can be per claimed player instead, independent of the network and of the proxy's header handling. Chosen over keying by X-Forwarded-For (user, 2026-11-21): that works with require_auth off too, but depends on the proxy setting the header and Munin not being reachable directly. With require_auth off, the limit stays as upstream has it.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 With `require_auth` on, the 10 s web ping limit is per claimed player: one player's ping does not make another claimed player wait (other than the 2 s between any two pings)
- [x] #2 With `require_auth` off, the web ping limit is exactly as upstream (keyed by the connection's address)
- [x] #3 Tested on the local test server with two claimed players and with `require_auth` off
- [x] #4 `PATCHES.md` records the change (in its own entry or TASK-20's), including that behind a proxy with `require_auth` off the limit is shared by all visitors
- [x] #5 Written per decision-2: new behaviour in our own file, the upstream edit one marked line, commit subject starting with the task ID
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Access.cs: PingKey(req): with require_auth on and a claimed Browser ID, "claim:" + the claimed character's stats key (two same-name characters stay apart); otherwise the remote address, as upstream.
2. MapDataServer /api/ping: pass Patches.Access.PingKey(req) to WebPing.Try instead of the remote address (one marked line).
3. Docs: the ping paragraph in ADVANCED.md "Behind a reverse proxy". PATCHES.md: a TASK-22 entry.
4. Test on the local test server: require_auth on with two claimed players pinging within 10 s (both pass after the 2 s gap; the same player again within 10 s waits); require_auth off: two Browser IDs from one address share the 10 s.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Tested on the local test server (2026-11-21). require_auth on, Ann (k2) and Bob (k3) claimed: Ann 0 s ok; Bob 2.5 s ok (upstream would refuse: same address); Ann 4.55 s wait 6; Bob 6.58 s wait 6; Ann 11.04 s ok; Bob 13.52 s ok. require_auth off: k2 0 s ok; k3 2.55 s wait 8; no Browser ID 5.04 s wait 5; k3 10.55 s ok (shared by address, as upstream). Test config restored to require_auth false.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
With require_auth on, the web ping limit (WebPing: 10 s per visitor, 2 s between any two) now counts per claimed character (Access.PingKey: "claim:" + its stats key, so same-name characters stay apart) instead of per network address, which behind a reverse proxy made the 10 s shared by every visitor. With require_auth off it is unchanged, keyed by address as upstream. One marked line in MapDataServer /api/ping, the rest in src/Patches/Access.cs (commit 58ef63a). docs/ADVANCED.md 'Behind a reverse proxy' gains the ping note; PATCHES.md has a TASK-22 entry. Tested with two claimed players and with require_auth off.
<!-- SECTION:FINAL_SUMMARY:END -->
