---
id: TASK-10
title: Investigate tooltips for colour-coded map objects (plants, mushrooms, ore)
status: To Do
assignee: []
created_date: '2026-11-18T09:40:00Z'
updated_date: '2026-11-19T10:31:00Z'
labels:
  - web
dependencies: []
references:
  - patches-tools/local-test/README.md
type: spike
ordinal: 9000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Many small objects on the map are shown only as colour-coded dots: plants, mushrooms, berries, ore and similar. There is no way to hover over one and see what it is. The idea may be that you learn the colours and stop needing names, but until then the map is hard to read, and the colours are easy to forget.

These objects are dense, often touching or overlapping, so a tooltip has to work when one object covers another. For example, it could list everything under the pointer in one tooltip, not only the top one.

This is an investigation first; it may or may not end in a change. Find out how these objects are drawn and what the page knows about each one (its kind and name, per map view), what tooltips the map already has and how they pick an object, and what hovering would cost on a dense map. Record the findings and a proposal in this task, and agree it with the user before implementing anything. The local test server (`patches-tools/local-test/`) with the copied live world is the place to try it.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 The findings are recorded in the task notes: how these objects are drawn in each map view, what the page knows about each one, and how existing tooltips pick what to show
- [ ] #2 A proposal is recorded and agreed with the user (or the user decides not to go ahead) before any implementation starts
- [ ] #3 If implemented: hovering over any colour-coded object shows what it is, in the views where those objects are drawn
- [ ] #4 If implemented: when objects overlap under the pointer, the tooltip shows all of them, not just the top one
- [ ] #5 If implemented: hovering stays smooth on the densest parts of the copied live world
- [ ] #6 If implemented: `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->
