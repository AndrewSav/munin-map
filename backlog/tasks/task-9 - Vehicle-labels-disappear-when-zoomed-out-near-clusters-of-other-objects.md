---
id: TASK-9
title: Vehicle labels disappear when zoomed out near clusters of other objects
status: To Do
assignee: []
created_date: '2026-11-18T08:27:00Z'
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
<!-- AC:END -->
