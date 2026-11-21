---
id: TASK-26
title: >-
  Show the resource objects Munin doesn't draw yet: leviathans, Bloodgold,
  ancient roots, seed kale, giant remains by yield
status: To Do
assignee: []
created_date: '2026-11-21T10:00:00Z'
labels:
  - plugin
  - web
dependencies: []
type: feature
ordinal: 25000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Other Valheim maps show resource spots that Munin doesn't: Leviathan, Gold, Kale seeds, Ancient root, Giant remains, Black metal. Munin draws a world object only if its prefab name matches a rule in the vegetation classifier (`src/Tiles/Vegetation.cs`, around line 377). Anything else is filed as `None` and not drawn. The test world's `vegetation-kinds.txt` lists `leviathan` (7) and `blackmetalscrap` (4) as `None`, for example.

What each one is (from the 1.0.21 game data and English text, not checked in game):
- **Leviathan** (`leviathan`, Ocean, about 1 in 100 ocean zones): a barnacle-covered sea creature, hover text "Abyssal Barnacle". Mined from a boat for Chitin before it sinks.
- **Lava Leviathan** (`LeviathanLava`, Ashlands): a location, not vegetation; the game plans up to 100. Its back is covered in Flametal ore. The old flametal meteorite (`MineRock_Meteorite`) is no longer placed, so this is the flametal source to show, along with Charred fortress loot.
- **Bloodgold**, the Deep North gold. Two sources:
  - `goldvein` (needs a tier-5 hit: Battering Ram, Catapult or Explosive Payload);
  - frozen troll corpses (`DN_gammeltroll*`, `TrollFrost_Frac`; tier-6, Fader's Ember Charge only).
  - Both give Petrified Tissue (`GoldOre`).
- **Seed kale** (`Pickable_SeedKale`, Deep North): wild seed kale. Deep North village granaries (`northvillage_granary`) give seeds too.
- **Ancient root** (`YggdrasilRoot`, Mistlands, 2-4 per area): a Sap Extractor goes on it for Sap. It can't be broken.
- **Giant remains** (Mistlands, `giant_*`). Munin already draws them, as "Giant bones" under Rocks. Split by what they give:
  - the ribs and skulls give Black Marble;
  - the helmets and swords give Copper and Iron Scrap.
- **Black metal:** on other maps it marks a Fuling village. Munin already marks Fuling camps (`src/World/Markers.cs:447`), so nothing is missing there.

The test world may not have generated Mistlands, Ashlands or Deep North zones yet: no `giant_*` appears in its kinds file. Checking these may need those zones generated on the test copy first. Only objects in generated zones are drawn; predicting ungenerated ones is TASK-4.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Leviathans, ancient roots, Bloodgold sources and seed kale each have their own entry in the map's layer panel, with a name saying what they give, and are drawn where the save has them
- [ ] #2 Lava Leviathans are shown, as a marker or in the layer panel, named as a Flametal source
- [ ] #3 Giant remains are split by yield: bones (Black Marble) and helmets and swords (Copper and Iron Scrap) can be shown and hidden separately
- [ ] #4 Every new rule matches the exact prefab names, checked against a save where those zones are generated, and no object that was drawn before changes kind by accident
- [ ] #5 The new names live in a file of ours, with one marked hook into the upstream classifier (decision-2)
- [ ] #6 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->
