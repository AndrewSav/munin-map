---
id: TASK-6
title: Add a fork CI workflow that builds patches and publishes releases
status: Done
assignee:
  - '@claude'
created_date: '2026-11-18T03:46:00Z'
updated_date: '2026-11-20T00:46:00Z'
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
- [x] #1 The workflow is a new file that exists only on `patches`; no upstream file is changed
- [x] #2 Every push to `patches` builds the plugin, so a rebase that breaks the build shows up as a failed run
- [x] #3 Pushing a `patches-X.Y.Z-N` tag builds the plugin and publishes a GitHub release on the fork, named after the tag, with the annotated tag message as its notes
- [x] #4 A `patches-*` tag whose `X.Y.Z` does not match `version_number` in `package/manifest.json` fails before building
- [x] #5 Release assets have fixed names (`MuninMap.zip` and `SHA256SUMS`) and the release is not a pre-release, so https://github.com/AndrewSav/munin-map/releases/latest/download/MuninMap.zip fetches the newest build without a token
- [x] #6 The plugin built from a tag identifies itself as `X.Y.Z+patches.N` in its version information, while `package/manifest.json` and the `<Version>` in `src/MuninMap.csproj` stay untouched
- [x] #7 The workflow checks out submodules, so it keeps working once TASK-5 lands
- [x] #8 Upstream's Build workflow is disabled in the fork's Actions settings, so syncing `main` never makes it build or publish in the fork
- [x] #9 Any scripts the workflow runs live in `patches-tools/`. Only the workflow file itself goes in `.github/workflows/`, because GitHub only looks for workflows there
- [x] #10 A release can also be started with the workflow's Run workflow button (workflow_dispatch) on `patches`: the workflow works out the next `patches-X.Y.Z-N` tag (X.Y.Z from `package/manifest.json`, N one more than the highest existing tag for that X.Y.Z, else 1), creates it as an annotated tag whose message lists the task IDs from the commit subjects since the previous `patches-*` tag, pushes it, and publishes the release exactly as a pushed tag would
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. New .github/workflows/patches.yml: on push to patches (build only), on patches-* tags (check, build, publish) and workflow_dispatch on patches (next tag, build, push the tag, publish in the same run, because a tag pushed with the run's own token starts no new run).
2. patches-tools/ci/release-tag.sh: check <tag> (format and X.Y.Z against package/manifest.json) and next (the next N; an annotated tag listing the task IDs since the previous patches-* tag, or all of them when that tag is not on the branch). Prints tag, version and stamp.
3. patches-tools/ci/fetch-server.sh: SteamCMD fetch of the dedicated server's managed assemblies (as in upstream's release.yml), with the same cache.
4. The build runs upstream's build.sh with InformationalVersion=X.Y.Z+patches.N in the environment; MSBuild picks it up, so no upstream file changes. The zip is renamed MuninMap.zip, with SHA256SUMS. gh release create publishes it as latest, not a pre-release.
5. Test the scripts in a Linux container, then on GitHub: a push build, a Run workflow release, and a mismatched tag that must fail before building.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-11-20: upstream's Build workflow (release.yml, id 424321336) disabled in the fork with gh workflow disable Build; gh workflow list now shows it disabled_manually. It had never run in the fork. The fork's default branch is now patches.

Tested in mcr.microsoft.com/dotnet/sdk:8.0, 2026-11-20: check patches-3.4.0-1 gives stamp 3.4.0+patches.1; check patches-9.9.9-1 and check v3.4.0 fail with ::error before anything builds; next creates patches-3.4.0-1 with the notes "Tasks: TASK-1 TASK-2 TASK-5" plus their commit subjects, and then patches-3.4.0-2 with plain notes (the empty task list no longer trips set -e). build.sh with InformationalVersion=3.4.0+patches.1 in the environment gives a MuninMap.dll carrying 3.4.0+patches.1 in both the metadata and the version resource. Not yet run on GitHub.

On GitHub, 2026-11-20 (the user pushed and clicked):
- push to patches, run 44647107915: success (build only).
- Run workflow on patches, run 44647544420: success. It created and pushed patches-3.4.0-1 and published that release: not a pre-release, Latest, assets MuninMap.zip and SHA256SUMS, notes "Tasks: TASK-1 TASK-2 TASK-5 TASK-6" with their commit subjects.
- releases/latest/download/MuninMap.zip downloads without a token (HTTP 200); SHA256SUMS checks out; the zip holds one MuninMap.dll, which carries 3.4.0+patches.1 in its metadata and version resource.
- Pushed tag patches-9.9.9-1, run 44647743531: failed at "Which release" with "tag patches-9.9.9-1 names 9.9.9, but package/manifest.json says 3.4.0"; setup, fetch, build and publish were all skipped. The tag was then deleted.
- Not yet exercised: a correct tag pushed by hand publishing a release (criterion 3). It shares the build and publish steps with the Run workflow path, but its tag check and tag-message fetch have only run on the failure path.

Pushed tag patches-3.4.0-2 (made by the user, message "Test of a hand-made release tag"), run 44648287557: success. Which release passed, the build and the stamp check (3.4.0+patches.2) passed, and Push the new tag was skipped as it should be. The release patches-3.4.0-2 was published as Latest, with the tag message as its notes and assets MuninMap.zip and SHA256SUMS.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
The fork now builds and releases itself on GitHub Actions, in .github/workflows/patches.yml (a new file, the only one outside patches-tools/), with patches-tools/ci/release-tag.sh and fetch-server.sh. No upstream file changed. Upstream's own Build workflow is disabled in the fork's settings.

- A push to patches builds, without releasing.
- A pushed patches-X.Y.Z-N tag is checked against package/manifest.json, built and published, with the tag message as the notes.
- Run workflow on patches works out the next tag, writes notes from the task IDs since the previous release, builds, pushes the tag and publishes in the same run.
- Releases are Latest, not pre-releases, with fixed asset names, so https://github.com/AndrewSav/munin-map/releases/latest/download/MuninMap.zip is always the newest build. The plugin carries X.Y.Z+patches.N in its version information, set through the environment when calling upstream's build.sh.

Verified on GitHub: a push build; a Run workflow release (patches-3.4.0-1); a hand-made tag release (patches-3.4.0-2); a mismatched tag (patches-9.9.9-1) failing before anything was built; the latest URL downloading without a token, its checksum, and the stamp inside the DLL.
<!-- SECTION:FINAL_SUMMARY:END -->
