---
id: DRAFT-6
title: 'Spike: put Munin''s web pins on cartography tables so players get them in game'
status: To Do
assignee: []
created_date: '2026-11-21T07:53:00Z'
updated_date: '2026-11-21T08:01:00Z'
labels:
  - plugin
dependencies: []
references:
  - src/World/Markers.cs
  - src/World/Fog.cs
documentation:
  - >-
    backlog/docs/doc-1 -
    Cartography-table-pins-how-the-game-reads-and-writes-them.md
type: spike
ordinal: 23000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Pins placed on the web map live only in Munin (`pins.csv`) and never reach the game. Players see them only on the web page; web pings are temporary. A player's own map pins are stored in their character file on their PC, which a server plugin can't reach. The one place the server can leave pins for vanilla players is a cartography table: its map data is a ZDO field the server can write, and players get the table's pins when they Read it.

doc-1 records how the game reads and writes table pins, from the decompiled game code (1.0.21). In short:
- Tables are not synced with each other.
- Reading a table decides which of other players' pins a player has, matched by position within 1 m.
- A table pin under the reader's own ID is skipped.
- A write replaces the table's pin list with the writer's map, after a silent read of that table.

For web pins that means: every table, one fixed owner ID that no character has, merging into each table's existing data, and never editing a pin in place. Most of this was read in the code, and none of it has been tried in game.

This spike checks the parts that matter on the local test server and decides whether the feature is worth building and how. Decide before building: which tables (every table, per doc-1), how a pin's placer is shown, what happens on web removal and edit, whether it needs a setting, how it fits require_auth and claimed players, and how Munin's own map avoids showing these pins twice.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 On the local test server, a pin Munin writes into a cartography table appears on a vanilla player's map when they Read that table, and the table's explored area still reads correctly
- [ ] #2 A web pin Munin removes from a table disappears from a player's map on their next Read of that table
- [ ] #3 Checked in game with two characters: a player's Write at a table keeps Munin's pin on it, and other players' pins don't spread between tables through a writer (doc-1, 'What follows')
- [ ] #4 Found and recorded what the game shows from a table pin's author field, and so whether it can carry who placed a web pin
- [ ] #5 The write path is tried both with a player at the table (its owner) and with nobody near, and the one that sticks is recorded; a write racing a player's own Write is described
- [ ] #6 doc-1 updated with what was tested, and every claim marked as tested in game or not
- [ ] #7 A recommendation is recorded in the task: build or not, and if build, the design choices listed in the description, as a proposed feature task (not created without approval)
- [ ] #8 Spike code is not merged into the product
- [ ] #9 The recommendation addresses abuse: with `require_auth` off anyone who can open the map can place pins, and on the tables those pins would reach every player who Reads one, and no player can delete them for good (only Munin can, from every table). It decides who may ever reach the tables (for example only claimed players with `require_auth` on, only admin-approved pins, or an admin switch) and how a bad pin is removed everywhere
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Comments

<!-- COMMENTS:BEGIN -->
author: @claude
created: 2026-11-21T08:01:00Z

---

Concern raised in review: web pins on tables could be used for spam. Players cannot permanently delete a pin someone else owns (it comes back on their next Read), so with require_auth off anyone who can open the public map could fill every player's in-game map with junk that sticks until Munin removes it from every table. Consider making table pins limited to claimed players' pins or admin-approved ones. Added as AC #9.

---
<!-- COMMENTS:END -->
