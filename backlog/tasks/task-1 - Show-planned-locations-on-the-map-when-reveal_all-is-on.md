---
id: TASK-1
title: 'Spoiler toggle: the whole world and planned locations, when reveal_all is on'
status: To Do
assignee: []
created_date: '2026-11-18T03:09:00Z'
updated_date: '2026-11-19T10:23:00Z'
labels:
  - plugin
  - docs
  - web
dependencies: []
type: feature
ordinal: 1000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Some players want a full spoiler map, like the seed-map websites: the whole world, with every boss altar, dungeon and trader, explored or not. Others don't want spoilers at all. The goal is one spoiler toggle in the page, so each viewer chooses for themselves.

How it fits with upstream's `reveal_all` setting (fog of war off for everyone, testing only):
- `reveal_all = false`: this change is inert. No toggle, and the server and page behave exactly as upstream.
- `reveal_all = true`, toggle off: the map looks the same as with `reveal_all = false`. So turning `reveal_all` on no longer spoils the map for anyone who hasn't asked for it.
- `reveal_all = true`, toggle on: the spoiler view. No fog, the whole generated world as upstream's `reveal_all` shows it today, plus the planned locations below.
The toggle flips both ways freely. Upstream treats the fog as a privacy boundary enforced by the server. This fork does not: seed maps already show this information to anyone who wants it, and a clearly labelled toggle keeps it hidden from those who don't.

Planned locations: the game plans every location when the world is created and keeps all of them, placed or not, in the world's location registry in the save. A planned location's position is final; "not placed" only means no player has been near enough for the game to build it yet. Read the unplaced ones from the registry, never recompute them from the seed, so the map matches the real world, including re-planning after a game update or an admin running `genloc`. This applies to the six location kinds Munin already classifies:
- boss altars;
- mini-boss lairs;
- dungeons;
- traders;
- camps & resources;
- landmarks.
Trees, rocks, ore, world-generated ruins and player builds only exist once a zone is generated, so they stay limited to generated zones. TASK-4 is the separate attempt to predict ore.

Traders are already handled by the game (checked against the game's code and location data). Each trader (Haldor, Hildir, the Bog Witch) is a unique location with up to 10 planned candidate sites. Only the first candidate whose zone generates is built, and it always holds its trader. The game then deletes the other candidates from the registry. So an unbuilt trader entry is one of several possible sites, not a trader, and once a trader is built it stands alone. On the map that reads correctly by itself: one icon means the trader is built, several mean it is at one of them. Planned locations are therefore drawn like built ones, with no new marker style, and only their tooltip says more. `genloc` or a game update that changes the location version re-rolls the unbuilt candidates, so they must not be cached across restarts.

Today the server filters by explored ground in about 20 places (fog image, map tiles, biome map, mist, region labels, vegetation, ruins, player builds, demisters, vehicles and every marker set), and its caches assume a single view. With `reveal_all` on, it has to serve both the normal view and the spoiler view.

Write it per decision-2: new behaviour in new files, small marked edits in upstream files, and the upstream files touched recorded in the notes.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 With `reveal_all = false` nothing changes: the page offers no toggle, and the server sends exactly what upstream sends
- [ ] #2 With `reveal_all = true` the page offers a single toggle, clearly labelled as showing spoilers, off by default
- [ ] #3 With `reveal_all = true` and the toggle off, the map (fog, tiles, layers, markers) looks the same as with `reveal_all = false`
- [ ] #4 With `reveal_all = true` and the toggle on, there is no fog and every layer shows the whole generated world: terrain, biomes, mist, region labels, vegetation, ruins, player builds, vehicles and every marker set
- [ ] #5 In the spoiler view, planned locations of the six kinds (boss altars, mini-boss lairs, dungeons, traders, camps & resources, landmarks) in zones not generated yet also appear, read from the world location registry, drawn like built ones
- [ ] #6 The tooltip of a planned location says it has not been generated yet; for an unbuilt trader site it says it is one of N possible sites for that trader
- [ ] #7 The toggle can be flipped either way at any time without reloading the page
- [ ] #8 The "boss altar found" and "trader found" world events behave as before: only built locations on explored ground, never planned ones, and never because someone turned the toggle on
- [ ] #9 Modded locations appear only when Munin's existing name classification recognises them, as for built ones; planned locations are read fresh from the registry, so after `genloc` or a game update that re-rolls them the markers follow on the next marker rebuild
- [ ] #10 No new server setting: `reveal_all` alone decides whether the toggle exists
- [ ] #11 The `reveal_all` entry in `docs/ADVANCED.md` and the setting's description in `src/Config.cs` describe the toggle and its spoilers, drop "Testing only" (nothing technical limits it to testing), and keep the note that tiles for unexplored ground render as people browse, so map data grows with use
- [ ] #12 The left bar's per-layer counts, its click-to-cycle ("go to the next one") and the Markers panel's lists and search use exactly the markers currently shown: with the toggle off they match the normal view, so no count, jump or search result gives a spoiler away; with it on they include planned locations, and flipping the toggle starts the cycling again from the first marker
- [ ] #13 Written per decision-2: new behaviour in new files, upstream edits marked where the format allows, and every commit subject starts with TASK-1
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Checked 2026-11-19: nothing technical makes reveal_all "testing only". Tiles over unexplored ground are rendered only when a browser requests them (TileStore.RequestMissing); the overview zooms are prerendered for the whole world either way; terrain comes from the world generator, so ungenerated zones render fine. The only cost is CPU and disk growing with how much of the world people browse (whole world at max zoom about 6,400 tiles). The phrase is in docs/ADVANCED.md (settings table) and in the setting description in src/Config.cs.

Checked 2026-11-19: web/js/ui.js cycles through s.markers of a set in server order (this.setAt per set id) and shows n = s.markers.length as the count; renderMarkers (Markers panel, search) uses the same sets. Both read the raw set lists, so any client-side spoiler filtering must happen before they see them.
<!-- SECTION:NOTES:END -->
