---
id: TASK-12
title: >-
  Review authentication: exposed Steam IDs, unauthenticated pin editing, the
  admin token
status: To Do
assignee: []
created_date: '2026-11-18T09:50:00Z'
updated_date: '2026-11-19T22:00:00Z'
labels:
  - plugin
  - web
  - security
dependencies: []
type: spike
ordinal: 11000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Review what the map exposes to anyone who can open it, and what it lets them change. The map has no login: anyone who can reach it sees and can do everything it offers.

The user's current questions:
1. **Steam IDs are visible to everyone.** Players' Steam IDs appear on the map page or in its data. Is that necessary for anything the page does, or could the page work without them (or with an opaque ID)?
2. **Pin editing has no authentication.** With pin editing enabled (`web_pins`), anyone who can open the page can add, move or delete pins, and there is no setting to require a login or a token for it.
3. **What does the admin token do?** The plugin has a token (`announce.token` in the map's data folder appears to be one). Find out what it unlocks, how it is passed (an API call, a page setting), where it is documented, and whether it could also protect pin editing.

This is a review first. Record the findings and a proposal in this task and agree it with the user before changing anything. Changes to how authentication works would touch upstream code, so keep them rebase-friendly per decision-2.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Every place the page or its data shows or sends a Steam ID (or another platform ID) is listed, with what each use needs it for, and whether the page could work without it
- [ ] #2 Everything a visitor can change without credentials is listed (pins, base names, anything else), with the settings that turn each on or off
- [ ] #3 What the admin token does is written down: what it unlocks, how it is created and passed, where it is documented, and whether it could protect pin editing too
- [ ] #4 The findings and a proposal are recorded in the task notes and agreed with the user (or the user decides to leave things as they are) before any change starts
- [ ] #5 If implemented: `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->
