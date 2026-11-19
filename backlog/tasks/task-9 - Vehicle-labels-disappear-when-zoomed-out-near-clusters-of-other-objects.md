---
id: TASK-9
title: Vehicle labels disappear when zoomed out near clusters of other objects
status: To Do
assignee: []
created_date: '2026-11-18T08:27:00Z'
updated_date: '2026-11-19T11:34:00Z'
labels:
  - web
dependencies: []
references:
  - patches-tools/local-test/README.md
type: bug
ordinal: 8000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
With labels turned on for vehicles (boats and carts), zooming the map out makes some vehicle labels disappear while others stay. From what the user has seen, a label seems to disappear when many other objects are clumped around the vehicle. The user wants vehicle labels to stay visible in every case, because the labels are how they find their vehicles on a zoomed-out map.

The cause is not known yet: possibly label decluttering or collision handling between overlapping markers, a zoom threshold, or something else. Investigate first and record the findings in this task before proposing a fix. The fix is then agreed with the user before it is implemented.

The local test server (`patches-tools/local-test/`) with the copied live world is the place to reproduce it.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 The problem is reproduced on the local test server, with the zoom level and surroundings where a vehicle label disappears recorded in the task notes
- [ ] #2 The cause is found and recorded in the task notes: which code hides the label, under what conditions, and why some vehicle labels stay
- [ ] #3 A fix is proposed in the task notes and agreed with the user before any implementation starts
- [ ] #4 With vehicle labels on, every vehicle label stays visible at every zoom level, however many other objects are near it
- [ ] #5 Labels of other marker kinds behave as before, unless the agreed fix says otherwise
- [ ] #6 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Lead found while testing TASK-1, 2026-11-20 (not yet confirmed for vehicles). The marker canvas (web/js/layers/markercanvas.js) gives labels at every zoom only to the always-labelled kinds: spawn, boss altars, traders, mini-boss lairs (the always flag set in web/js/layers/markers.js). Every other marker, vehicles included, gets a label only from zoom 5 (LABEL_ZOOM), only if it is not grouped with neighbours within 22 px (GROUP_PX, the q.n === 1 checks), and only if its label does not overlap a label already placed (fits()). The always-labelled ones claim their space first. So near a cluster a vehicle loses its label to the collision check or to grouping, which matches the symptom. The same rule hides the Forge of Potential's label in the TASK-1 spoiler view: landmarks are not always-labelled, and many more altars and traders claim space first there. Consider both together when proposing the fix: vehicles, and possibly landmarks, as always-labelled, against the extra clutter that adds.
<!-- SECTION:NOTES:END -->
