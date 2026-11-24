---
id: TASK-35
title: Ore stays behind when the map glides to a marker
status: Done
assignee:
  - '@claude'
created_date: '2026-11-24T11:12:57Z'
updated_date: '2026-11-24T19:14:51Z'
labels:
  - web
dependencies: []
type: bug
ordinal: 42000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Clicking a marker in a list (the markers page, search, a player card) glides the map there (smoothzoom.js glideTo). On the WebGL map that glide moves the view by moving Leaflet's pixel origin, not the map pane, and fires 'move' but 'zoom' only when the zoom changes. The ore layer (web/js/patches/ore-layer.js, TASK-4) is a Leaflet GridLayer, which places its tiles again only on 'viewreset' and 'zoom': a glide at one zoom (already zoomed in to 6 or closer) left the ore where it was, in the wrong places after the map stopped.

Fix: the ore layer places its tiles again on every 'move' as well (GridLayer._setZoomTransforms), in the fork's own file.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 After clicking a marker in a list at a zoom that doesn't change, the ore is in the right places during and after the glide
- [x] #2 Gliding with a zoom change, wheel zoom, dragging and the 3D view are unaffected
- [x] #3 Removing and adding the ore layer leaves no move handler behind
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Checked by the user on the test server (WebGL map, predicted ore on): glide to a marker at one zoom and with a zoom change, wheel zoom, drag, ore layer off and on. No PATCHES.md change: the TASK-4 entry describes the feature, which works as described again. Release-notes line added after trimming the lines released in patches-3.4.0-9.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
The ore layer (a Leaflet GridLayer) now places its tiles again on every 'move', not only on 'zoom', so the page's own glide to a marker at one zoom (which moves the pixel origin, not the map pane) no longer leaves the ore behind. One handler in web/js/patches/ore-layer.js, the same function across add and remove so Leaflet unbinds it. Checked by the user on the test server.
<!-- SECTION:FINAL_SUMMARY:END -->
