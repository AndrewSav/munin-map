---
id: TASK-11
title: Investigate showing what lies under the top layer of a building in the tooltip
status: To Do
assignee: []
created_date: '2026-11-18T09:40:00Z'
labels:
  - web
dependencies: []
references:
  - patches-tools/local-test/README.md
type: spike
ordinal: 10000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A building on the map is drawn in several layers: floor, furniture, walls, roof. The tooltip shows only the top piece under the pointer, usually the roof. The user wants to find out what furniture (or other pieces) stands under a roof, for example where a particular workbench or chest is in a base, and the tooltip gives no way to see past the roof.

Related to the tooltip investigation for colour-coded objects (TASK-10): both ask what is under the pointer when several things overlap. They may end up sharing an approach.

This is an investigation first; it may or may not end in a change. Find out how building pieces are drawn and layered, what the page knows about each piece, and how the current tooltip picks the one it shows. Then propose how to show what is underneath: for example, every piece under the pointer grouped by layer, or a way to look through roofs. Record the findings and the proposal in this task, and agree it with the user before implementing anything. The local test server (`patches-tools/local-test/`) with the copied live world, which has real bases, is the place to try it.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 The findings are recorded in the task notes: how building pieces are drawn and layered, what the page knows about each piece, and how the current tooltip picks the one it shows
- [ ] #2 A proposal is recorded and agreed with the user (or the user decides not to go ahead) before any implementation starts
- [ ] #3 If implemented: hovering over a building shows the pieces below the top one (furniture under a roof, for example), not just the top piece
- [ ] #4 If implemented: a specific piece of furniture inside a roofed base in the copied live world can be found from the map alone
<!-- AC:END -->
