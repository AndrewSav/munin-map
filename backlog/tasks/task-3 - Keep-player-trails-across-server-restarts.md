---
id: TASK-3
title: Keep player trails across server restarts
status: To Do
assignee: []
created_date: '2026-11-18T03:12:00Z'
labels:
  - plugin
  - docs
dependencies: []
type: feature
ordinal: 3000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Player trails on the activity layer (the last 3 or 24 hours of where each player went) are kept only in the server's memory. A server that restarts on a schedule (a daily restart is common) wipes them every time, so the 24-hour view is rarely actually 24 hours.

Trails should survive a restart. A clean shutdown should keep everything recorded up to that point. A crash or kill can lose a little, about the last 10 minutes at most. Points older than the trail window (24 hours) are not worth restoring.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 After a clean server restart, the activity layer shows trail points recorded before the restart, up to the 24-hour trail window
- [ ] #2 After a crash or a killed process, at most about the last 10 minutes of trail points are lost
- [ ] #3 Points older than 24 hours are dropped on load, and players whose whole trail has aged out are not restored
- [ ] #4 Trails are kept per world, alongside that world's other map data, so two worlds never share trails
- [ ] #5 A missing, empty or unreadable trails file means starting with no trails and a warning in the log, never a failed start
- [ ] #6 Saving trails causes no noticeable server hitch
- [ ] #7 The "Where things are kept" table in `docs/ADVANCED.md` lists the new file
<!-- AC:END -->
