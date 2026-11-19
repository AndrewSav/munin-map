# Changes from upstream

This fork ([AndrewSav/munin-map](https://github.com/AndrewSav/munin-map), branch `patches`) is
Munin Map ([cocopuff2u-gaming/munin-map](https://github.com/cocopuff2u-gaming/munin-map)) with
the product changes below on top. Each is rebased onto every upstream release.

Only changes to what the plugin and the map do are listed here. The fork's own tooling (the
local test server, CI, the backlog) is not. Each entry names the backlog task with the full
details, in `backlog/tasks/`.

## TASK-1: Spoiler toggle

With `reveal_all = true`, the left bar gets a **Settings** group with a **Spoilers** switch,
off by default for each viewer. Flipping it reloads the page, which comes back at the same view.

- **Spoilers off:** the map looks exactly as it does with `reveal_all = false`: fog of war, and
  only explored ground and what stands on it.
- **Spoilers on:** no fog, the whole generated world, and also the locations the game has
  planned but not built yet. These are boss altars, mini-boss lairs, dungeons, traders,
  camps & resources and landmarks.
  - A planned location's popup says it hasn't been generated yet.
  - Some locations get several candidate sites and are only ever built once: the traders and
    the Forge of Potential. For these, the popup says it is one of N possible sites.
- **With `reveal_all = false`,** nothing changes.

How it differs from upstream:
- Upstream `reveal_all` removes the fog for every visitor. Here, turning it on only makes
  the switch available, and each viewer decides.
- Upstream never shows locations the game hasn't built yet.
- No new settings.
- **Keeps the record of explored ground.** Upstream's `reveal_all` is meant for testing, and
  while it is on, the saved record of explored ground becomes "the whole world", so turning
  `reveal_all` off later leaves the map fully revealed. This fork makes `reveal_all` an everyday
  setting, so it always saves the real record. A world that has run with upstream's
  `reveal_all` can be repaired: stop the server, delete the world's `fog.png` and `fog.grid` in
  Munin's data folder, and start it again. Munin rebuilds the record from the zones players have
  visited, cartography tables and traces, with 64 m-square edges instead of exact trails.
