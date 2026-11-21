---
id: TASK-16
title: Verify Munin behind a TLS-terminating reverse proxy
status: In Progress
assignee:
  - '@claude'
created_date: '2026-11-20T01:17:00Z'
updated_date: '2026-11-21T05:08:00Z'
labels:
  - plugin
  - web
  - testing
milestone: m-1
dependencies: []
references:
  - src/Server/Preview.cs
  - web/js/net.js
type: chore
ordinal: 15000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The map is to be reachable from outside the local network through a reverse proxy that terminates TLS. Visitors use HTTPS, and the proxy talks plain HTTP to Munin. The proxy, its configuration and the DNS record are outside this repo. This task makes sure Munin itself works correctly behind such a proxy.

Upstream already has the pieces:
- the page derives its websocket address from its own protocol (`wss:` when served over HTTPS, `web/js/net.js`);
- the link preview builds absolute URLs from `X-Forwarded-Proto` and `X-Forwarded-Host`, or from the `map_url` setting (`src/Server/Preview.cs`);
- the server reads `X-Forwarded-For` (`src/Server/MapDataServer.cs`).
None of this has been tried behind a real TLS-terminating proxy. Security of what an outside visitor can see and change is TASK-12's subject.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Verified against a real TLS-terminating reverse proxy deployment reachable from the internet; deployment details (address, proxy product, hosts) are kept out of the repo and the backlog
- [ ] #2 Through the proxy over HTTPS, the page, data, map tiles, 3D models and icons all load, with no mixed-content warnings in the browser
- [ ] #3 The live connection works over wss through the proxy: players, events and the clock update, and the page reconnects after a server restart
- [ ] #4 The link preview tags carry the public https address, either from the forwarded headers or from map_url, and which one to rely on is recorded
- [ ] #5 Whatever the server uses X-Forwarded-For for works correctly behind the proxy, and whether a visitor could spoof it is recorded in this task
- [ ] #6 The proxy settings Munin needs (websocket upgrade, forwarded headers, timeouts for long-lived connections, request size limits if any) are written down in the README
- [ ] #7 Anything that breaks is fixed per decision-2, or recorded with a reason when it is left
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Decided 2026-11-21: test against the real deployment instead of building a local proxy (criterion 1 replaced). No deployment details go into the repo or backlog: findings are written generically.

Findings against the real deployment (2026-11-21; HTTPS at the proxy, plain HTTP to Munin; build with TASK-19 and TASK-20, require_auth on):
- Page, data (markers, stats, players, events, fog), map and height tiles, a 3D model (.glb), game icons and scripts all load over HTTPS (200, right content types). The page has no absolute http:// URLs. Headless Chrome on the public URL: no console messages, no mixed-content warnings, no failed requests; the live connection opens on wss://<host>/ws.
- wss held 75 s: hello, events, world and a players frame every second throughout; the proxy did not drop the connection. (Reconnect after a server restart not yet tried: needs a live restart.)
- Link preview: og:url is https://<public host>/. Client-sent X-Forwarded-Host / X-Forwarded-Proto do not change it, so in this deployment either the proxy overwrites them or map_url is set (/config does not show map_url). Rely on map_url: it does not depend on the proxy's header handling.
- X-Forwarded-For: used only for one debug log line when a websocket opens (MapDataServer WebSocketHandler.OnOpen). Spoofing it changes nothing.
- Found: the web ping limit (WebPing.Try, 10 s per visitor, 2 s between any two) is keyed by the connection's remote address (MapDataServer /api/ping). Behind a proxy every visitor has the proxy's address, so it becomes one ping per 10 s for everyone. Upstream behaviour; options under discussion with the user.
- Note: TASK-19 and TASK-20 fixes confirmed on the deployment: no key field or Steam IDs in data/stats.json; an unclaimed pin is refused 403.
<!-- SECTION:NOTES:END -->
