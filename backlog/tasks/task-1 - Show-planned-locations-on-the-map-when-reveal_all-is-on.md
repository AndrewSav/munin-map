---
id: TASK-1
title: 'Spoiler toggle: the whole world and planned locations, when reveal_all is on'
status: In Progress
assignee:
  - '@claude'
created_date: '2026-11-18T03:09:00Z'
updated_date: '2026-11-19T10:59:00Z'
labels:
  - plugin
  - docs
  - web
milestone: Initial release
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
- [ ] #14 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Approach: with reveal_all on, the server keeps sending everything, as upstream reveal_all does. The page already applies the fog itself: the veil over the 2D canvas, the patched 3D terrain, and the tiles composed with the mask, all driven by data/fog.png. So the toggle only has to (a) choose which fog image the page gets and (b) filter, in the page, the things that are drawn above the fog or exist apart from it.
2. Server, new file src/Patches/Spoilers.cs:
   - per-request flag from a cookie (munin_spoilers=1), meaningful only when reveal_all is on;
   - planned-location helpers.
   Marked one-line hooks in upstream files:
   - MapDataServer.Route sets the request flag.
   - Fog.Png serves the all-clear mask only when spoilers are requested, else the real explored mask.
   - Markers.Shown also accepts unplaced locations when reveal_all is on.
   - Markers.Location writes placed:false (and, for unique locations such as traders, the number of candidate sites).
   - MixAt adds the placed state to the set signature, so a site that gets built is rewritten.
3. Page, new file web/js/spoilers.js:
   - the toggle (shown only when config.reveal_all; off by default; remembered per browser);
   - the cookie;
   - when off, the real explored mask, decoded once, with explored(x,z) and anyExplored(chunk), the same rule the server uses;
   - filters for marker sets (drop placed:false and anything off explored ground) and for chunk lists (vegetation, structures, ruins, 3D objects).
   Marked one-line hooks:
   - data.js: the markers store keeps the raw sets and emits filtered ones, so counts, cycling, the Markers panel and search follow; plus the chunk index and region pack loads;
   - ruins.js: its index;
   - ui.js: the toggle row;
   - biome hover and region labels: skip unexplored;
   - markers.js popup: "one of N possible sites".
4. Flipping the toggle: set the cookie, refresh the fog (FogLayer.refresh and the 3D refreshFog), re-emit the filtered markers, and reset and reload the chunk stores. No page reload.
5. Docs: the reveal_all entry in docs/ADVANCED.md and in src/Config.cs.
6. Test on the local test server (its pulled settings already have reveal_all = true): toggle off against a reveal_all=false run, toggle on, planned locations and traders, flipping both ways, 2D and 3D.
Trade-off: with the toggle off, the page still downloads tiles and chunks for unexplored ground, hidden under the veil, and the server renders those tiles on demand. That is the same load reveal_all has today, not more.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Checked 2026-11-19: nothing technical makes reveal_all "testing only". Tiles over unexplored ground are rendered only when a browser requests them (TileStore.RequestMissing); the overview zooms are prerendered for the whole world either way; terrain comes from the world generator, so ungenerated zones render fine. The only cost is CPU and disk growing with how much of the world people browse (whole world at max zoom about 6,400 tiles). The phrase is in docs/ADVANCED.md (settings table) and in the setting description in src/Config.cs.

Checked 2026-11-19: web/js/ui.js cycles through s.markers of a set in server order (this.setAt per set id) and shows n = s.markers.length as the count; renderMarkers (Markers panel, search) uses the same sets. Both read the raw set lists, so any client-side spoiler filtering must happen before they see them.

Server half built and tested on the local test server, 2026-11-19.
- New src/Patches/Spoilers.cs. Marked hooks: MapDataServer.Route (per-request flag from the munin_spoilers cookie); Fog.Png (all-clear only when spoilers are requested); Markers.Shown (planned locations when reveal_all is on); Markers.Location (placed:false, sites for unique locations); Markers.MixAt (built state in the set signature).
- Result: fog.png differs with and without the cookie. markers.json has bosses 30 (24 planned), mini-bosses 9 (9), dungeons 1519 (1407), landmarks 25 (25), camps 1285 (1189), traders 21 (20): Haldor built; Hildir and the Bog Witch with 10 candidate sites each, sites=10.
- Upstream bug found: Fog.Save writes Fog.Png() to disk, and with reveal_all upstream Fog.Png() is the all-clear image. So running with reveal_all overwrites the saved explored record with "whole world explored", and turning reveal_all off later leaves the map fully revealed. That is a real technical reason behind "Testing only" (the earlier note saying there was none was wrong). The Fog.Png hook fixes it as a side effect: saving runs outside any spoiler request, so it writes the real mask. Recovery for a world already affected: stop the server and delete the world's fog.png and fog.grid in Munin's data folder. Munin rebuilds the explored record from the zones players have visited, map tables and traces (coarser than before). Seen on the test server: exploredCells was 4194304 (all), and 43358 (about 2%) after the reset.
<!-- SECTION:NOTES:END -->
