---
id: TASK-6
title: Add a fork CI workflow that builds patches and publishes releases
status: To Do
assignee: []
created_date: '2026-11-18T03:46:00Z'
updated_date: '2026-11-19T23:25:00Z'
labels:
  - build
  - ci
milestone: Initial release
dependencies: []
references:
  - >-
    backlog/decisions/decision-1 -
    Fork-release-tags-and-rebase-base-for-the-patches-branch.md
  - .github/workflows/release.yml
type: chore
ordinal: 6000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Patched builds need to reach the user's own server quickly: it should be able to grab the newest release from a fixed URL and update. Fork changes stay out of upstream's files where practical. A new workflow file that exists only on `patches` can never conflict on a rebase. Patching upstream's `.github/workflows/release.yml` would conflict whenever upstream edits it.

How GitHub Actions triggers fit: `push` and tag triggers run the workflow file from the commit that triggered them. `workflow_dispatch` (the Run workflow button, `gh workflow run`) and `schedule` only work for workflows on the default branch. The fork's default branch is `patches` (changed 2026-11-20), so all of them are available to a workflow that lives on `patches`.

Upstream's `release.yml` (workflow name "Build") is on `patches` too, since the branch contains upstream. It reacts to pushes to `main`, `v*` tags, pull requests and its own Run workflow button. It would fail once websocket-sharp comes from a submodule (TASK-5, done), because it checks out without submodules. It has been disabled in the fork's Actions settings (`gh workflow disable Build`, 2026-11-20): a repo setting, not a file change, so it never conflicts.

Upstream's workflow shows how the build gets the game assemblies: SteamCMD with an anonymous login fetches the Valheim dedicated server (app 896660), and then `build.sh` runs. Like upstream's build, ours compiles against the dedicated server version current on Steam. That is also what live servers run in practice: Steam updates game clients automatically, and a client won't join a server on a different version, so servers follow the current build. Older builds can be downloaded (for example with DepotDownloader), but almost nobody does.

The tag names, the rebase rules and the build's version stamp are set by decision-1.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 The workflow is a new file that exists only on `patches`; no upstream file is changed
- [ ] #2 Every push to `patches` builds the plugin, so a rebase that breaks the build shows up as a failed run
- [ ] #3 Pushing a `patches-X.Y.Z-N` tag builds the plugin and publishes a GitHub release on the fork, named after the tag, with the annotated tag message as its notes
- [ ] #4 A `patches-*` tag whose `X.Y.Z` does not match `version_number` in `package/manifest.json` fails before building
- [ ] #5 Release assets have fixed names (`MuninMap.zip` and `SHA256SUMS`) and the release is not a pre-release, so https://github.com/AndrewSav/munin-map/releases/latest/download/MuninMap.zip fetches the newest build without a token
- [ ] #6 The plugin built from a tag identifies itself as `X.Y.Z+patches.N` in its version information, while `package/manifest.json` and the `<Version>` in `src/MuninMap.csproj` stay untouched
- [ ] #7 The workflow checks out submodules, so it keeps working once TASK-5 lands
- [x] #8 Upstream's Build workflow is disabled in the fork's Actions settings, so syncing `main` never makes it build or publish in the fork
- [ ] #9 Any scripts the workflow runs live in `patches-tools/`. Only the workflow file itself goes in `.github/workflows/`, because GitHub only looks for workflows there
- [ ] #10 A release can also be started with the workflow's Run workflow button (workflow_dispatch) on `patches`: the workflow works out the next `patches-X.Y.Z-N` tag (X.Y.Z from `package/manifest.json`, N one more than the highest existing tag for that X.Y.Z, else 1), creates it as an annotated tag whose message lists the task IDs from the commit subjects since the previous `patches-*` tag, pushes it, and publishes the release exactly as a pushed tag would
<!-- AC:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-11-20: upstream's Build workflow (release.yml, id 424321336) disabled in the fork with gh workflow disable Build; gh workflow list now shows it disabled_manually. It had never run in the fork. The fork's default branch is now patches.
<!-- SECTION:NOTES:END -->
