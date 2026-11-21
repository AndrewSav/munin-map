---
id: doc-2
title: 'Ore placement: how the game places vegetation in a zone'
type: specification
created_date: '2026-11-21T08:53:00Z'
updated_date: '2026-11-21T08:54:00Z'
---
How Valheim places vegetation, ore deposits included, when it generates a zone, and how far that could be reproduced for zones nobody has visited (TASK-4). Source: the decompiled game code (`assembly_valheim`, Valheim 1.0.21) and the game's asset data (`_ZoneSystem`, `_LocationList_*`), read on 2026-11-21. Each point says whether it was read in the code or data, or inferred. Nothing here has been tested.

## Order inside a zone (read in the code)

- `ZoneSystem.SpawnZone` (`ZoneSystem.cs:1316`) runs `PlaceLocations`, then `PlaceVegetation`, then `PlaceZoneCtrl` (`:1336-1338`), and only for a zone that isn't generated yet.
- The vegetation list (`m_vegetation`) is built in `SetupLocations` (`:866-900`): the location lists sorted by `m_sortOrder`, with `_ZoneSystem`'s own list first, then each alt-biome's `m_addVegetation`. Placement goes through it in that order (`PlaceVegetation`, `:1384`).
- A zone holds at most one location. It is placed before any vegetation, and its position is planned and saved in the world file.

## Seeding and random draws (read in the code, `PlaceVegetation` `:1379-1580`)

- The Unity random state is saved at the start of the pass and restored at the end (`:1381`, `:1578`), so nothing outside the pass disturbs it.
- Each vegetation entry is reseeded on its own (`:1391`): `Random.InitState(seed + zoneID.x*4271 + zoneID.y*9187 + veg.m_prefab.name.GetStableHashCode())`, with `seed = WorldGenerator.GetSeed()`. Int arithmetic, wrapping. It uses the prefab name, so two entries for the same prefab share a stream.
- An entry is skipped before seeding if it is disabled or the zone's heightmap doesn't have its biome.
- **Target count:**
  - If `m_max < 1`: one `Random.value`, and the entry is skipped when it's above `m_max`.
  - Otherwise: `Range((int)m_min, (int)m_max + 1)`.
- **Tries** (`:1407`): `m_forcePlacement ? count*50 : count`. Only `silvervein` is forced. Every other entry gets exactly `count` candidates, and a candidate that fails is not replaced.
- **Draws per candidate, in order:**
  1. x and z: 2 floats within ±(32 − group radius) of the zone centre.
  2. The alt-biome check. On a reject, only those 2 draws are used.
  3. Group size: 1 int.
  4. For each group member: `GetRandomPointInRadius` (2 draws; members after the first only), then rotation, scale, tilt x and tilt z. All drawn before any check.
- A rejected candidate uses the same draws as an accepted one, with two exceptions. Either one shifts every later candidate of that entry in that zone when the accept or reject is predicted wrongly:
  1. `GetTerrainDelta` (`:2699`): 10 `Random.insideUnitCircle` samples, taken only if the candidate gets that far and the entry has a terrain-delta radius. Only `silvervein` has one.
  2. One `Random.value` for `m_chanceToUseGroundTilt > 0` (`:1530`), taken only for an accepted candidate. `MineRock_Tin` and `MineRock_Obsidian` have one.

## The checks, in code order (read in the code)

1. **Block check** (`IsBlocked`, `:2726`): a physics raycast from y+2000 down 10 000 m against the Default, static_solid, Default_small and piece layers (`:671`). Needs real colliders (see Blocking).
2. **Ground data** (`GetGroundData`, `:2897`): a raycast against the zone heightmap's collider, which gives the height and the triangle normal.
   - **Biome:** `Heightmap.GetBiome(point)` (`Heightmap.cs:477`), not `WorldGenerator.GetBiome`. It's the biome with the largest distance-weighted sum over the zone's 4 corner biomes, unless all four match.
   - **Biome area:** one value per zone: Edge if the corners differ, else Median (`:511`). Both can be computed from `WorldGenerator` at the zone corners.
3. **Altitude:** `y - 30` against min and max.
4. **Vegetation mask** (only when min ≠ max): `Heightmap.GetVegetationMask` (`Heightmap.cs:925`). Of the ores, only `giant_ribs` and `giant_skull` use it.
5. **Ocean depth** (only when min ≠ max): a lerp of the 4 corner values. No ore uses it.
6. **Tilt:** the normal's y against cos(max tilt) and cos(min tilt).
7. **Terrain delta:** max minus min of 10 ground-height raycasts at random points within the radius. Only `silvervein` (radius 9, delta 0-3).
8. **Distance from the world centre:** pure maths.
9. **In forest:** `WorldGenerator.GetForestFactor`. No ore uses it.
10. **Clear areas** (`InsideClearArea`, `:1585`): a square test (|dx| and |dz| < radius) against the clear areas of the location in this zone only, using `m_exteriorRadius`, and only where `m_clearArea` is set.

Also:
- `m_snapToStaticSolid` and the surround check are used by no ore.
- **Heights:** the zone's terrain is `HeightmapBuilder`'s corner-biome blend of `GetBiomeHeight` (`HeightmapBuilder.cs:174-208`), not `WorldGenerator.GetHeight(x, z)`. The two differ near biome borders, so a reproduction has to do the same blend and interpolate the triangles.
- **Location terrain changes:** flattening and paint change the heightmap (`Heightmap.cs:531-545`). Inferred: not confirmed that the collider is rebuilt between `PlaceLocations` and `PlaceVegetation` within the same frame.

## Blocking (mechanism read in the code, timing inferred)

- `IsBlocked` is a plain physics raycast. Nothing checks a list of placed positions, so it needs instantiated colliders.
- Every accepted object is instantiated straight away inside the loop (`:1543-1572`), so it blocks later candidates and later entries. The location placed just before blocks too; on a dedicated server that is only its networked children (see Ghost mode).
- Inferred: Unity registers a new collider at its spawn pose without a sync step. Objects from neighbouring zones that are already loaded could also block near zone edges.
- Without colliders, each earlier object's footprint has to be approximated from the prefab's colliders × scale × rotation.

## Which ores are vegetation, and what comes before them (read in the data, enabled entries only)

| Ore (prefab) | Where, how many | Rules | Placed before it in its biome |
|---|---|---|---|
| Silver (`silvervein`, #8) | Mountain, 1, forced (50 tries) | block check, altitude 120-1000, tilt 0-30, terrain delta radius 9 / max 3 | #6 `rock3_mountain` |
| Buried iron scrap (`mudpile_beacon`, #12; the Wishbone finds it) | Swamp, 0-5 | altitude ≥ 0.5, no ground tilt, no terrain delta | #11 `rock_4_swamp` |
| Copper (`rock4_copper`, entry #17 `rock4_copper_blackforest`) | Black Forest, 0-1 (about half the zones have no candidate) | block check, altitude ≥ 4, tilt 5-45, no ground tilt, no terrain delta | #16 `rock4_blackforest` |
| Tin (`MineRock_Tin`, #18) | Black Forest, 20 | shoreline, altitude −0.6 to 1.5, ground tilt 1.0 | #16, #17 |
| Obsidian (`MineRock_Obsidian`, #93) | Mountain, 10-15, group radius 1 | altitude ≥ 100, ground tilt 1.0 | almost every Mountain rock and tree (#6, #9, #10, #13, #15, #50, #87 …) |

**Also vegetation:**
- Mistlands: `giant_helmet1/2` and `giant_sword1/2` (copper and iron scrap), and `giant_ribs` (black marble); 1-2 per zone, terrain-delta radius 5.
- Ashlands: Grausten from the `cliff_ashlands*` entries, `Ashlands_rock2`, `Pickable_Ashstone` and others.

**Disabled as vegetation:** `rock3_silver`, `Pickable_Tin`, `Pickable_BogIronOre`, `LeviathanLava`.

**Not vegetation, so out of scope for this method:**
- Iron inside Sunken Crypts (dungeon rooms).
- Flametal: the `LeviathanLava` location and Charred fortress chests and ruins.
- `MineRock_Meteorite`, which isn't placed anywhere.

## Other differences between a re-run and the real pass

- **Instantiating doesn't touch the random stream** for these prefabs (inferred, partly checked): their spawn components draw nothing, and `MineRock5`'s draw is in `Start`.
- **Game version:** zones generated under an older game version used that version's lists and world-generator version (`World.m_worldGenVersion`). Measure accuracy against zones known to be generated under 1.0.21.
- **Float differences** between PhysX mesh raycasts and triangle maths only matter at a threshold.
- **Not involved:** grass and clutter (`ClutterSystem`), dungeons (their own seeds), and player terrain edits (`TerrainComp`, which only exists after a zone has been edited).

## What a reproduction would get right and wrong (inferred from the above)

- **Copper and buried scrap** (fixed draws, no replacement): every predicted deposit is at a spot the game really tried, and positions are never shifted.
  - A wrong call gives an **extra** deposit (a missed block or failed check) or a **missing** one (a candidate rejected that the game kept).
- **Tin and obsidian:** the same up to the first wrong call in a zone. After it, the later positions are shifted.
- **Silver** (forced, first pass wins): the right zone, but a wrong call moves the deposit to another real candidate spot. The Wishbone finds it within 25 m, so a few metres off may still be useful.
- **Overall:** mostly extra deposits at real candidate spots, from blocks not modelled. Modelling the one blocker before copper (#16) and before buried scrap (#11) should remove most of them.
- **Suggested order:** copper and buried scrap first, measured against generated zones; then tin.

## Munin runs inside the game

Sites that predict outside the game have to reproduce Unity's random functions, which are not open source. Munin doesn't: it is a BepInEx plugin inside the dedicated server's Unity process. It can call `UnityEngine.Random` (`InitState`, `value`, `Range`, `insideUnitCircle`) and `WorldGenerator` directly, the same code the game uses.

What that requires:
- **Save and restore Unity's random state** around its own draws, as `PlaceVegetation` does, so the game's sequence isn't disturbed.
- **Run on the game's thread,** in small slices, as the world sweep already does.

The remaining gaps are blocking and the zone's real terrain, not random numbers.

## Ghost mode: the server's own generation (read in the code unless marked)

**What it is.** Ghost mode is not a preview: it is how a dedicated server really generates zones.
- `ZoneSystem.Update` (`:1174-1184`) runs `CreateGhostZones` around the server's reference position and every peer's position.
- `CreateGhostZones` (`:1216-1236`) calls `SpawnZone(zone, SpawnMode.Ghost)` for at most one ungenerated zone per update.
- Players are peers, so on a dedicated server nearly every zone is generated in Ghost mode. Inferred: only zones around the server's own reference position get Full.

**What it saves:**
- Each object still gets a ZDO (`ZNetView.Awake`, `ZNetView.cs:~90-105`), but its GameObject is not added to the scene. `SpawnZone` destroys the GameObjects afterwards (`:1339-1347`).
- The ZDOs are saved and sent to clients.
- The zone is marked generated (`SetZoneGenerated`, `:1349`).
- The location instance is marked placed (`:2226-2227`).
- For a unique location (the traders), the other candidate sites are **deleted** (`RemoveUnplacedLocations`, `:2231`).
- A location with an icon has the icon sent to every client (`SendLocationIcons`, `:2235`).
- Dungeons get their ZDOs (`DungeonGenerator.Generate`).

**Ghost and Full differ in zones with a location.** This corrects an earlier answer that said they place things identically.
- In Ghost mode only the location's networked children are spawned during generation (`SpawnLocation` `:~2440-2525`, `CreateLocationProxy` `:2577-2591`). Its static body is spawned later, when someone loads the zone: meshes, colliders, and the terrain modifiers it carries.
- So in a Ghost-generated zone, vegetation is blocked only by the location's networked children and its clear-area square. Inferred: the location's terrain changes don't shape the ground the vegetation pass sees.
- Zones without a location are the same in both modes.

**Location rotation isn't seeded.** A randomly rotated location draws its rotation (`Random.Range(0, 16)*22.5`, `:2222`) from whatever Unity's random state is at that moment, before any reseed.
- So in such a zone, which spots the location's children block isn't fixed by the seed, and a prediction can't be exact even in theory (inferred).
- Zones without a location are fully determined.

**Running it on the live world and undoing it:** possible for zones without a location, but not recommended.
- Every new ZDO has to be destroyed (`ZDOMan.DestroyZDO`, `:988`), and the zone removed from the generated list.
- For zones with a location, the location has to be set back to not placed, and a unique location's deleted candidate sites restored.
- A location icon already sent to clients can't be called back: their maps keep it until they reconnect.

**Running it on a copy of the world:** the recommended exact method.
1. Run a second server process on a copy of the save, with the same plugins.
2. Call `SpawnZone(zone, Ghost)` for the target zones, each frame until it returns true. It first waits for the zone's terrain to be built (`HeightmapBuilder.IsTerrainReady`, `:1320`) and for a location's prefab to load (`:1325`).
3. Read off the ZDOs with the ore prefabs (`rock4_copper`, `MineRock_Tin`, `silvervein`, `MineRock_Obsidian`, `mudpile_beacon` …).
4. Throw the copy away.

That gives the game's own result with the server's own mods applied, except for location rotation, and the real world is never touched.

**Cost:**
- Each zone instantiates every vegetation entry for its biomes up to the last one needed. Inferred: a few to some tens of milliseconds per zone; the game itself does one zone per update.
- Stopping after the last entry needed is exact, because each entry reseeds itself and only earlier entries' colliders affect it. That's index 18 for copper and tin, 12 for buried scrap, 8 for silver.
- Obsidian (#93) needs nearly the whole Mountain list.
- Earlier entries can't be skipped, even ones that aren't ore: their colliders are what block.

**Traps:**
- Mods that patch zone generation or `WorldGenerator` (Expand World, Better Continents and the like) must be the same in both processes.
- Inferred: physics sees colliders instantiated earlier in the same call, which the game's own blocking relies on, so going through the game's path behaves the same.
- Objects overhanging from neighbouring zones block only if those zones are loaded, the same as for the real server.
- **Never run it on the live save:** pruned trader sites and sent icons can't be undone.
