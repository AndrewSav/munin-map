# Changes from upstream

This fork ([AndrewSav/munin-map](https://github.com/AndrewSav/munin-map), branch `patches`) is
Munin Map ([cocopuff2u-gaming/munin-map](https://github.com/cocopuff2u-gaming/munin-map)) with
the product changes below on top. Each is rebased onto every upstream release.

Only changes to what the plugin and the map do are listed here. The fork's own tooling (the
local test server, CI, the backlog) is not. Each entry names the backlog task with the full
details, in `backlog/tasks/`.

## TASK-1: Spoiler toggle (in progress)

With `reveal_all = true`, the map gets a toggle labelled as showing spoilers, off by default.

- **Toggle off:** the map looks exactly as it does with `reveal_all = false`: fog of war, and
  only explored ground and what stands on it.
- **Toggle on:** no fog, the whole generated world, and also the locations the game has
  planned but not built yet. These are boss altars, mini-boss lairs, dungeons, traders,
  camps & resources and landmarks. A planned location's popup says it hasn't been generated
  yet. For a trader not found yet, the popup says it is one of N possible sites.
- **With `reveal_all = false`,** nothing changes.

How it differs from upstream:
- Upstream `reveal_all` removes the fog for every visitor. Here, turning it on only makes
  the toggle available, and each viewer decides.
- Upstream never shows locations the game hasn't built yet.
- No new settings.
