---
id: TASK-17
title: Show who built a portal or base in its popup
status: Done
assignee:
  - '@claude'
created_date: '2026-11-20T05:06:00Z'
updated_date: '2026-11-21T07:12:00Z'
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
- [x] #1 A portal's popup shows who built it ("Built by <name>") when the server sends `by`
- [x] #2 A player base's popup shows who built it the same way, including in the editable popup used for renaming or hiding a base
- [x] #3 When the builder is unknown (no `by`), the popup shows no builder line, and nothing else in it changes
- [x] #4 No server change: the name comes from the `by` field already in the markers data
- [x] #5 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [x] #6 Written per decision-2: upstream edits marked where the format allows, and every commit subject starts with the task ID
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. web/js/builtby.js (ours): builtBy(m, extra) adds '<br><small>Built by NAME</small>' for cat portal or base when m.by is set, else returns extra unchanged.
2. web/js/layers/markers.js (upstream): import line, and one call in popupHtml just before renamedBy (built, then renamed), both marked // patches: TASK-17. popupHtml feeds the portal popup, the base popup and the editable base popup.
3. PATCHES.md entry.
4. Test on the local server: a portal built by the test character shows Built by; a base too, in the editable popup; markers without by (none of the pulled world's builders have a playerId in stats) show no line.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Testing needed live's Munin data on the test server (stats.json maps builder IDs to names), done in TASK-23. With it: 16/16 portals and 11/12 bases carry by. User checked in the browser: portal popup, editable base popup and the portal reached by Go to the other end show Built by; the base without by shows no builder line and is otherwise unchanged. builtBy checked in isolation: escapes the name, leaves other marker kinds and markers without by alone. No server change. decision-2: new web/js/builtby.js; markers.js gets an import and one call next to the TASK-20 hook, both marked.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Portal and player base popups (the editable base popup included) now show 'Built by <name>' from the by field the server already sends; no builder line when by is missing. New web/js/builtby.js, two marked lines in web/js/layers/markers.js (import, and one call in popupHtml before renamedBy). PATCHES.md entry added. Verified by the user on the local test server with live's Munin data.
<!-- SECTION:FINAL_SUMMARY:END -->
