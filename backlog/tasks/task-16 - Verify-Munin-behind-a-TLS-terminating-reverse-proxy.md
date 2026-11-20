---
id: TASK-16
title: Verify Munin behind a TLS-terminating reverse proxy
status: To Do
assignee: []
created_date: '2026-11-20T01:17:00Z'
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
- [ ] #1 The local test setup gains an optional TLS-terminating reverse proxy in front of the test server (fork tooling in `patches-tools/local-test/`, a local certificate is fine), documented in its README
- [ ] #2 Through the proxy over HTTPS, the page, data, map tiles, 3D models and icons all load, with no mixed-content warnings in the browser
- [ ] #3 The live connection works over wss through the proxy: players, events and the clock update, and the page reconnects after a server restart
- [ ] #4 The link preview tags carry the public https address, either from the forwarded headers or from map_url, and which one to rely on is recorded
- [ ] #5 Whatever the server uses X-Forwarded-For for works correctly behind the proxy, and whether a visitor could spoof it is recorded for TASK-12
- [ ] #6 The proxy settings Munin needs (websocket upgrade, forwarded headers, timeouts for long-lived connections, request size limits if any) are written down in the README
- [ ] #7 Anything that breaks is fixed per decision-2, or recorded with a reason when it is left
<!-- AC:END -->
