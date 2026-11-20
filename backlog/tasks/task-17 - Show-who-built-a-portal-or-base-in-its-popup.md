---
id: TASK-17
title: Show who built a portal or base in its popup
status: To Do
assignee: []
created_date: '2026-11-20T05:06:00Z'
labels:
  - web
dependencies: []
references:
  - web/js/layers/markers.js
type: enhancement
ordinal: 16000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The server already sends who built each portal and each player base: a `by` field on those markers in `data/markers.json`, the builder's character name from the game's own record (Live/Stats.cs NameOfPlayerId). Visitors can't fake it, unlike the free-text name on web pins. But the page only uses it on the player card, which lists the portals and bases a player built. Clicking a portal or a base on the map doesn't say who built it.

Showing it in those popups costs almost nothing: no server change and no new data, just one more line in the popup text (`popupHtml` in `web/js/layers/markers.js`, which the portal and base popups use).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 A portal's popup shows who built it ("Built by <name>") when the server sends `by`
- [ ] #2 A player base's popup shows who built it the same way, including in the editable popup used for renaming or hiding a base
- [ ] #3 When the builder is unknown (no `by`), the popup shows no builder line, and nothing else in it changes
- [ ] #4 No server change: the name comes from the `by` field already in the markers data
- [ ] #5 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [ ] #6 Written per decision-2: upstream edits marked where the format allows, and every commit subject starts with the task ID
<!-- AC:END -->
