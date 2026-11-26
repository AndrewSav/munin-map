# Release notes

The user-facing changes the next fork release announces (TASK-33). Each release's notes are made from
this file as it is at the release's tag (`patches-tools/ci/release-tag.sh notes`), and a release fails
when the list below is the same as at the previous release: nothing new to announce means a change was
not logged here.

Upkeep: when a user-facing task or subtask is finished, add one line for it, `- TASK-n: <what changed>`
(its PATCHES.md heading where that fits). Before adding it, remove the lines that were already released:
the ones in this file at the latest `patches-*` tag. Internal and tooling tasks get no line.

After a rebase onto a new upstream version, add `- Updated to Munin Map X.Y.Z` (trimming the released
lines first, as above); a release on a new upstream version fails without it. Only lines starting with
`- ` are read.
- TASK-39: Predicted ore verified for Valheim 1.0.31
