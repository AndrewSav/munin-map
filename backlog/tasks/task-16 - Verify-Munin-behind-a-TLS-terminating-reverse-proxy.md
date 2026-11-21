---
id: TASK-16
title: Verify Munin behind a TLS-terminating reverse proxy
status: Done
assignee:
  - '@claude'
created_date: '2026-11-20T01:17:00Z'
updated_date: '2026-11-21T05:41:00Z'
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
- [x] #1 Verified against a real TLS-terminating reverse proxy deployment reachable from the internet; deployment details (address, proxy product, hosts) are kept out of the repo and the backlog
- [x] #2 Through the proxy over HTTPS, the page, data, map tiles, 3D models and icons all load, with no mixed-content warnings in the browser
- [x] #3 The live connection works over wss through the proxy: players, events and the clock update, and the page reconnects after a server restart
- [x] #4 The link preview tags carry the public https address, either from the forwarded headers or from map_url, and which one to rely on is recorded
- [x] #5 Whatever the server uses X-Forwarded-For for works correctly behind the proxy, and whether a visitor could spoof it is recorded in this task
- [x] #6 The proxy settings Munin needs (websocket upgrade, forwarded headers, timeouts for long-lived connections, request size limits if any) are written down in `docs/ADVANCED.md` (section "Behind a reverse proxy")
- [x] #7 Anything that breaks is fixed per decision-2, or recorded with a reason when it is left
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

Ping limit behind a proxy: fixed for require_auth in TASK-22 (per claimed player). Options weighed (2026-11-21): leave it; per claimed player (chosen); per X-Forwarded-For. Correction to the earlier discussion: X-Forwarded-For is trustworthy when Munin reads the entry the proxy added (the last one, if the proxy appends to a client-sent header) and Munin is only reachable through the proxy; it was not chosen because it depends on that setup. With require_auth off, the limit stays shared by all visitors behind a proxy (to be noted in the docs).

Criteria 1, 2, 4, 5 and 7 checked (2026-11-21) from the findings above; 7: the one problem found (ping limit behind a proxy) is fixed for require_auth in TASK-22, and with require_auth off is recorded, left as upstream. Open: 3 (reconnect after a server restart) and 6 (proxy settings written down).

Reconnect after a server restart (2026-11-21): headless Chrome kept the public page open over wss while the user restarted the live server. The connection closed when the server went down; the page retried by itself with growing gaps (1, 1, 3, 5, 9, 14, then about every 15 s), each try answered 502 by the proxy while Munin was down; it reconnected about 1 min 48 s after the close, and players updates resumed at the normal rate, with no page reload. Criterion 3 checked.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Munin verified behind a real TLS-terminating reverse proxy (deployment details kept out of the repo). Over HTTPS the page, data, tiles, models, icons and scripts load with no mixed content or console errors; the live connection runs over wss, survives long idle periods (an update every second) and reconnects by itself after a server restart (about 1 min 48 s, the proxy answering 502 meanwhile). The link preview carries the public https address; map_url is the setting to rely on. X-Forwarded-For is only used in a debug log line. One problem found: the web ping limit is keyed by remote address, so behind a proxy it is shared by all visitors; fixed for require_auth in TASK-22, recorded for require_auth off. The proxy settings Munin needs are written in docs/ADVANCED.md, 'Behind a reverse proxy' (commit a35527b; the ping note comes with TASK-22). No product code changed, so no PATCHES.md entry.
<!-- SECTION:FINAL_SUMMARY:END -->
