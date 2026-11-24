---
id: doc-3
title: Pre-existing behaviour
type: other
created_date: '2026-11-23T23:02:17Z'
updated_date: '2026-11-23T23:26:27Z'
---
Upstream behaviour noticed while working on the fork and left as it is.

## The sidebar jumps to the top when the server restarts

While the server restarts, the statistics panel empties: the sidebar's content shrank from 3,957 to 925 px, so its scroll position went from 3,032 to 0. When Munin answers again (about 25 s later), the panels are rebuilt at full height, but the scroll position isn't restored, so the sidebar stays at the top. Shortly after, Leaderboard grows by another 73 px as its data arrives. Measured 2026-11-24 (Valheim 1.0.21, Munin 3.4.0).
