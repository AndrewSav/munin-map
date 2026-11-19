---
id: TASK-1
title: 'Spoiler toggle: the whole world and planned locations, when reveal_all is on'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-18T03:09:00Z'
updated_date: '2026-11-19T21:15:00Z'
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
- [x] #1 With `reveal_all = false` nothing changes: the page offers no toggle, and the server sends exactly what upstream sends
- [x] #2 With `reveal_all = true` the page offers a single toggle, clearly labelled as showing spoilers, off by default
- [x] #3 With `reveal_all = true` and the toggle off, the map (fog, tiles, layers, markers) looks the same as with `reveal_all = false`
- [x] #4 With `reveal_all = true` and the toggle on, there is no fog and every layer shows the whole generated world: terrain, biomes, mist, region labels, vegetation, ruins, player builds, vehicles and every marker set
- [x] #5 In the spoiler view, planned locations of the six kinds (boss altars, mini-boss lairs, dungeons, traders, camps & resources, landmarks) in zones not generated yet also appear, read from the world location registry, drawn like built ones
- [x] #6 The "boss altar found" and "trader found" world events behave as before: only built locations on explored ground, never planned ones, and never because someone turned the toggle on
- [x] #7 Modded locations appear only when Munin's existing name classification recognises them, as for built ones; planned locations are read fresh from the registry, so after `genloc` or a game update that re-rolls them the markers follow on the next marker rebuild
- [x] #8 No new server setting: `reveal_all` alone decides whether the toggle exists
- [x] #9 The `reveal_all` entry in `docs/ADVANCED.md` and the setting's description in `src/Config.cs` describe the toggle and its spoilers, drop "Testing only" (nothing technical limits it to testing), and keep the note that tiles for unexplored ground render as people browse, so map data grows with use
- [x] #10 The left bar's per-layer counts, its click-to-cycle ("go to the next one") and the Markers panel's lists and search use exactly the markers currently shown: with the toggle off they match the normal view, so no count, jump or search result gives a spoiler away; with it on they include planned locations, and flipping the toggle starts the cycling again from the first marker
- [x] #11 Written per decision-2: new behaviour in new files, upstream edits marked where the format allows, and every commit subject starts with TASK-1
- [x] #12 `PATCHES.md` has an entry for this change: what changes for players and admins, and how it differs from upstream
- [x] #13 The popup of a planned location says it has not been generated yet; for a candidate site of a unique location (the game builds only one of them and deletes the rest: the traders, the Forge of Potential), it also says it is one of N possible sites
- [x] #14 The Spoilers switch (only when reveal_all is on) sits in a new "Settings" group in the left bar, between "3D map" and "Map key"; its description says flipping it reloads the page, which comes back at the same view, zoom, 3D camera and layer choices
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
- Found while testing: Fog.Save writes Fog.Png() to disk, and with reveal_all upstream Fog.Png() is the all-clear image. So running with reveal_all overwrites the saved explored record with "whole world explored", and turning reveal_all off later leaves the map fully revealed. Harmless for a testing mode, and a real technical reason behind "Testing only" (the earlier note saying there was none was wrong); it matters once reveal_all is an everyday setting. The Fog.Png hook fixes it as a side effect: saving runs outside any spoiler request, so it writes the real mask. Recovery for a world already affected: stop the server and delete the world's fog.png and fog.grid in Munin's data folder. Munin rebuilds the explored record from the zones players have visited, map tables and traces (coarser than before). Seen on the test server: exploredCells was 4194304 (all), and 43358 (about 2%) after the reset.

Page half, 2026-11-20: web/js/spoilers.js plus marked hooks in net.js (getJSON), data.js (vegetation chunks, 2D and 3D), biomehover.js, layers/markers.js (popup) and ui.js (the Settings group). Checked against the server: with reveal_all on and the toggle off, the markers and chunk indexes the page keeps were compared with what a reveal_all = false server sends for the same world. All 17 fogged marker sets and the structures, ruins and objects indexes were identical, apart from one label: the server names the highest peak it shows "Highest peak". It sees every peak with reveal_all on, so the filter now relabels the highest one left.
Unique locations (checked against the game's code and location data): five locations have 10 candidate sites and keep only the first one built: Vendor_BlackForest, Hildir_camp, BogWitch_Camp, AncientUpgradeStation (the Forge of Potential, always present once built) and BigRockClearing (not shown by Munin). The unique flag is the only thing that removes the other candidates, so keying on it covers them all. PlaceofMystery1-3 are unique with one candidate each, so no "one of N" line (sites > 1 only). Hildir's three dungeons have 3 candidates each but are not unique: all can be built.

Browser testing with the user, 2026-11-20:
- Sidebar scroll: flipping the switch reset the sidebar to the top. Fixed in spoilers.js (keepPlace/restorePlace: the Settings group's offset is saved in sessionStorage and held for up to 4 s after the reload).
- Forge of Potential labels show only on hover: upstream label rules (only spawn, altars, traders and mini-boss lairs are always labelled), so moved to TASK-9.
- Trees and rocks on black ground in 3D with the toggle off: same as plain upstream v3.4.0 with reveal_all = false, built and run on the same world to compare. Upstream 3D only darkens the ground, and the server sends whole 256 m chunks wherever any part is explored, so objects up to a chunk deep stand on unexplored ground once the 3D view has loaded them. Not a TASK-1 regression.
- 3D coordinate readout stays at the last 2D position: upstream (only the 2D mousemove writes it).
- 3D zoom sometimes changes after the reload: intermittent and not reproduced on demand. Restoring the 3D camera from the URL is upstream code; a plain F5 in 3D will show whether upstream does it too.

Final checks, 2026-11-20. With reveal_all = false (our build): config reveal_all false, so no Settings group; fog.png the same with and without the cookie; no placed or sites fields once the first sweep had run (30 s). Before that sweep the server serves its last markers from disk (upstream behaviour when reveal_all changes), so the page now drops planned locations whenever the spoiler view is not on. Spoilers row: the description shortened to fit, and the icon drawn like ui.js's SVG() icons.
Upstream files touched (git is the record): src/Server/MapDataServer.cs, src/World/Fog.cs, src/World/Markers.cs, src/Config.cs, docs/ADVANCED.md, web/js/net.js, web/js/data.js, web/js/biomehover.js, web/js/layers/markers.js, web/js/ui.js. New: src/Patches/Spoilers.cs, web/js/spoilers.js.

Settings group made shared (2026-11-20): web/js/patches-settings.js holds the fork's Settings group; features register rows with addSetting(), and ui.js calls buildSettings() from its one marked line. The group appears when any row has something to show. The group subtitle "Changing these reloads the page" is gone, because TASK-2's clock switch will not reload; the Spoilers row now says "Whole world, all locations (reloads)".

Correction: the Settings group is always built (the user's call): TASK-2's clock switch will always be in it on this branch, so the group needs no condition. Until TASK-2 lands, it is empty with reveal_all off.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
With reveal_all on, each viewer now chooses between the normal map and a spoiler view, through a Spoilers switch in a new Settings group in the left bar (off by default; flipping it reloads the page at the same view). With reveal_all off nothing changes.

How it works: with reveal_all on, the server keeps sending the whole generated world, as upstream does, plus the locations the game has planned but not built yet. The fog image depends on a cookie. Without it, the page gets the real explored mask and draws it as the fog of war in 2D and 3D, and web/js/spoilers.js filters what would otherwise show past it: markers on unexplored ground, planned locations, and building, ruin, tree and object chunks. It uses the server's own explored rule. Planned locations carry placed:false. Candidate sites of unique locations (the traders, the Forge of Potential) also carry the number of sites, and their popups say "not generated yet" and "one of N possible sites".

Verified: with the toggle off, the 17 fogged marker sets and the structures, ruins and objects indexes the page keeps were identical to what a reveal_all = false server sends for the same world. With reveal_all off, our build sends no planned locations and serves the same fog with or without the cookie. The user tested the switch, the sidebar position across the reload, the spoiler view, popups and 3D in the browser. Trees on black ground in 3D were compared against plain upstream v3.4.0 and match it.

Also keeps the record of explored ground: upstream's reveal_all, meant for testing, saves "the whole world explored" as the fog record while it is on, so turning it off later leaves the map fully revealed. With reveal_all now an everyday setting, the real record is always saved. PATCHES.md describes the repair for an affected world.

Follow-ups: Forge and other non-always-labelled markers lose their labels in crowded views (TASK-9). The 3D zoom sometimes changes after the reload: intermittent, in upstream's camera restore, not reproduced on demand.
<!-- SECTION:FINAL_SUMMARY:END -->
