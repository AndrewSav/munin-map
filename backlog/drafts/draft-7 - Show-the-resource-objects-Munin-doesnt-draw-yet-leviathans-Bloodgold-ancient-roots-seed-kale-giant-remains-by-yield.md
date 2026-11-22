---
id: DRAFT-7
title: >-
  Show the resource objects Munin doesn't draw yet: leviathans, Bloodgold,
  ancient roots, seed kale, giant remains by yield
status: To Do
assignee: []
created_date: '2026-11-21T10:00:00Z'
updated_date: '2026-11-22T19:42:17Z'
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

The test server has a fully generated copy of the world (TASK-27, `patches-tools/local-test/README.md`, "A fully generated world"), so every biome can be checked there. Only objects in generated zones are drawn; predicting ungenerated ones is TASK-4.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Leviathans, ancient roots, Bloodgold sources and seed kale each have their own entry in the map's layer panel, with a name saying what they give, and are drawn where the save has them
- [ ] #2 Lava Leviathans are shown, as a marker or in the layer panel, named as a Flametal source
- [ ] #3 Giant remains are split by yield: bones (Black Marble) and helmets and swords (Copper and Iron Scrap) can be shown and hidden separately
- [ ] #4 Every new rule matches the exact prefab names, checked against a save where those zones are generated, and no object that was drawn before changes kind by accident
- [ ] #5 The new names live in a file of ours, with one marked hook into the upstream classifier (decision-2)
- [ ] #6 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [ ] #7 Sulfur arches, volture nests, the Places of Mystery and Charred ruins are shown as location markers, named by what they give (sulfur, volture eggs, molten cores, vineberries and fiddleheads)
- [ ] #8 Unstable lava rock (proustite powder) has its own entry in the layer panel
- [ ] #9 Greydwarf camps are shown as location markers, named as a source of ancient seeds
- [ ] #10 Where the save has them, a location's popup says whether its boss-item object is still there: totem poles in Fuling villages and stone towers, the bell fragment altar in Charred fortresses, sealbreaker crystals in infested mines
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Scope check against what Munin already shows (2026-11-22, from `src/World/Markers.cs` location names and `web/js/vegpack.js` vegetation kinds), against the game's own resource objects (Valheim 1.0.21).

**Already marked as locations:** Sunken crypts, burial chambers, troll caves, frost caves, bear caves, infested mines, Charred fortresses, Morgen holes, Fuling camps, tar pits, drake nests, guck trees, and the Deep North places (Mörkhalla, Winding tunnels, The Hole, Ancestral Memorial, Orb of Fimbulvinter, hot springs, Kall Fimbulbringer). The ruins layer covers cabins, towers and houses.

**Already drawn as vegetation kinds:** copper, tin, silver, obsidian, muddy scrap, giant remains (as "Giant bones" under Rocks, not split by yield), Magecap and Jotun puffs, smoke puff, lingonberries, the berries, thistle, dandelion, fiddlehead, wild barley and flax.

**Missing, location markers:**
- Lava Leviathan `LeviathanLava` (flametal)
- sulfur arch `SulfurArch`
- volture nest `VoltureNest`
- Places of Mystery `PlaceofMystery1`-`3` (molten cores)
- Charred ruins `CharredRuins*` (vineberries, fiddleheads)

**Missing, vegetation:**
- Leviathan `leviathan` (chitin)
- ancient root `YggdrasilRoot` (sap)
- gold vein `goldvein`; frozen trolls `DN_gammeltrollFrac01`/`02`, `TrollFrost_Frac*` (Bloodgold)
- seed kale `Pickable_SeedKale`
- unstable lava rock `UnstableLavaRock` (proustite powder)
- giant remains split: bones `giant_ribs` (black marble) vs helmets and swords `giant_helmet1`/`2`, `giant_sword1`/`2` (copper and iron scrap)

**Under check (asked of the game data 2026-11-22):** boss summoning items with fixed sources (ancient seeds from greydwarf nests, Fuling totems, withered bone, sealbreaker fragments, bell fragments, the Deep North boss item) and Hildir's quest locations. Add them here once confirmed.

Boss summoning items and Hildir, confirmed from the game data (1.0.21):
- Ancient seeds (the Elder): greydwarf nest `Spawner_GreydwarfNest`, 1 in every greydwarf camp `Greydwarf_camp1` (Black Forest, up to 300), 1 seed per nest. Not vegetation. Munin does not mark these camps yet.
- Fuling totems (Yagluth): totem pole `goblin_totempole`, a pickable that never respawns. About one per Fuling village (bonfire room, 50%), one in every Plains stone tower `StoneTower1`; also in StoneHenge chests and from berserkers.
- Withered bone (Bonemass): Sunken Crypt and Swamp chests (about 1 in 4) and crypt muck piles (`mudpile2` about 3% per chunk). Mostly chest loot.
- Sealbreaker fragments (Queen door): `blackmarble_altar_crystal` in some infested-mine rooms (15-30% RandomSpawn).
- Bell fragments (Fader): `Charred_altar_bellfragment`, 1 per Charred fortress, always present.
- Deep North boss: its Hateful Blood has no fixed source in 1.0.21 (the only placement is in a disabled location).
- Hildir's dungeons: already marked by Munin (Smouldering Tomb, Howling Cavern, Sealed Tower); her quest chests are mini-boss drops, not containers.
- Poteitr does not grow wild; seeds only from Deep North village granary chests.
Chests and location contents: asked separately (when chests get their items, what each location can hold); answer pending.

Hateful Blood (Deep North boss), confirmed on a 1.0.21 dedicated server: a fresh world's location plan has 181 types, with 3 DN_Bossroom and 0 FimbulLocation01 (the only location with black ice cores). So there is no fixed Hateful Blood source to map (checked at the location-plan level). Munin labels FimbulLocation as "Orb of Fimbulvinter", but a 1.0.21 world has none.
<!-- SECTION:NOTES:END -->
