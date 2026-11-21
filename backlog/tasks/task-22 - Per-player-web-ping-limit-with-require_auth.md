---
id: TASK-22
title: Per-player web ping limit with require_auth
status: To Do
assignee: []
created_date: '2026-11-21T05:22:00Z'
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
- [ ] #1 With `require_auth` on, the 10 s web ping limit is per claimed player: one player's ping does not make another claimed player wait (other than the 2 s between any two pings)
- [ ] #2 With `require_auth` off, the web ping limit is exactly as upstream (keyed by the connection's address)
- [ ] #3 Tested on the local test server with two claimed players and with `require_auth` off
- [ ] #4 `PATCHES.md` records the change (in its own entry or TASK-20's), including that behind a proxy with `require_auth` off the limit is shared by all visitors
- [ ] #5 Written per decision-2: new behaviour in our own file, the upstream edit one marked line, commit subject starting with the task ID
<!-- AC:END -->
