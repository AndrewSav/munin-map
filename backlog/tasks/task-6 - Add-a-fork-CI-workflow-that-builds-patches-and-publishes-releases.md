---
id: TASK-6
title: Add a fork CI workflow that builds patches and publishes releases
status: To Do
assignee: []
created_date: '2026-11-18T03:46:00Z'
updated_date: '2026-11-18T04:41:00Z'
labels:
  - build
  - ci
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
Patched builds need to reach the user's own server quickly: it should be able to grab the newest release from a fixed URL and update. Fork changes stay out of upstream's files where practical. A new workflow file that exists only on `patches` can never conflict on a rebase. Patching upstream's `.github/workflows/release.yml` would conflict whenever upstream edits it. Building and uploading releases by hand was ruled out: it ties every release to one machine and its local Valheim install.

How GitHub Actions treats a workflow that is not on the default branch: `push` and tag triggers run the workflow file from the commit that triggered them, so a workflow that exists only on `patches` works for both. `workflow_dispatch` (the Run workflow button, `gh workflow run`) and `schedule` only work for workflows on the default branch. The fork's default branch is `main`, which mirrors upstream, so neither is available unless the default branch changes.

Upstream's `release.yml` (workflow name "Build") is on the fork's `main`. It runs on pushes to `main` and on `v*` tags, and creates releases. It will also fail once websocket-sharp comes from a submodule (TASK-5), because it checks out without submodules. Disable it in the fork's Actions settings, for example with `gh workflow disable Build`. That is a repo setting, not a file change, so it never conflicts.

Upstream's workflow shows how the build gets the game assemblies: SteamCMD with an anonymous login fetches the Valheim dedicated server (app 896660), and then `build.sh` runs. Like upstream's build, ours compiles against whatever dedicated server version is current on Steam, which may differ from the version the live server runs.

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
- [ ] #8 Upstream's Build workflow is disabled in the fork's Actions settings, so syncing `main` never makes it build or publish in the fork
- [ ] #9 Any scripts the workflow runs live in `patches-tools/`. Only the workflow file itself goes in `.github/workflows/`, because GitHub only looks for workflows there
<!-- AC:END -->
