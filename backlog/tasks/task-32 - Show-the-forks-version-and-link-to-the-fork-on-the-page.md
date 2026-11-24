---
id: TASK-32
title: Show the fork's version and link to the fork on the page
status: Done
assignee:
  - '@claude'
created_date: '2026-11-24T05:27:29Z'
updated_date: '2026-11-24T05:49:52Z'
labels:
  - web
  - plugin
dependencies: []
type: feature
ordinal: 39000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The page's GitHub button (top right) links to upstream's repository (web/index.html, the btn-github link), and Server details shows upstream's version ("Munin Map 3.4.0", from MuninMap.VERSION). Someone who comes across a server running this fork and follows the link lands on a repository that doesn't match what they see on the map, which is confusing. The page should name the fork and its release, and link to the fork.

The fork's release stamp is in the assembly's informational version, e.g. 3.4.0+patches.17 for the release tagged patches-3.4.0-17 (decision-1); the wording the user chose shows it as 3.4.0-17. A local build has no stamp, so it needs a sensible fallback.

The same button is also the update notice: UpdateCheck.cs asks GitHub for upstream's latest release, and when that is newer, the button links to upstream's releases (web/js/app.js). On the fork that suggests 'updating' to upstream's build, which would lose the fork's changes, so it checks the fork's releases instead (with the user). Fork releases are tagged patches-X.Y.Z-N, so newer means a higher upstream version, or the same one with a higher N.

Ref: decision-1 (release tags and the version stamp), decision-2.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 The GitHub button links to the fork's repository, and its tooltip reads 'Munin Map Patches <version> on GitHub', e.g. 'Munin Map Patches 3.4.0-17 on GitHub'
- [x] #2 Server details shows 'Munin Map Patches' with the fork's version, e.g. 3.4.0-17, instead of upstream's 'Munin Map 3.4.0'
- [x] #3 The version comes from the build's release stamp; a build without one (a local build) shows a sensible fallback rather than a wrong or empty version
- [x] #4 Upstream files change only by small marked edits (decision-2), and PATCHES.md has an entry for the change
- [x] #5 The update notice checks the fork's releases, not upstream's: it shows when a newer fork release exists (a higher upstream version, or the same one with a higher N) and links to the fork's releases
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. New fork file src/Patches/ForkVersion.cs: the fork's version from the release stamp (3.4.0+patches.17 -> 3.4.0-17; unstamped -> 3.4.0-dev), the fork's repository URL, and the update check against the fork's releases (same schedule and check_updates switch), filling UpdateCheck.Latest/LatestUrl.
2. Marked one-line hooks: UpdateCheck.Start hands over to the fork's check; Config.cs adds the fork's version and repo_url.
3. Page (marked lines): newerVersion reads X.Y.Z-N as X.Y.Z.N; Server details label 'Munin Map Patches'; the GitHub button's link, tooltip and update note use the fork; index.html's static link points at the fork.
4. PATCHES.md entry (ADVANCED.md left as upstream's: its wording is still correct read generically).
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Verified on the test server: a build stamped 3.4.0+patches.6 showed 3.4.0-6, found 3.4.0-7 on the fork's releases a minute after start (logged, and the user saw the dot, tooltip, note and Server details link to patches-3.4.0-7); stamped +patches.7 it reported up to date (user checked); an unstamped build shows 3.4.0-dev. Page version comparison checked for -17 vs -16, equal, 3.5.0-1 vs 3.4.0-17, -1 vs -dev. Test server's check_updates set back to false.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
The page names the fork: the GitHub button links to the fork's repository with the tooltip 'Munin Map Patches <version> on GitHub', Server details show 'Munin Map Patches' with the fork's release (3.4.0-N from the release stamp, 3.4.0-dev without one), and the update check follows the fork's releases (a higher upstream version, or the same with a higher N). New code in src/Patches/ForkVersion.cs; upstream files get marked one-line edits (UpdateCheck.cs, Config.cs, app.js, ui.js, index.html). Verified on the test server with builds stamped as releases 6 and 7 and unstamped.
<!-- SECTION:FINAL_SUMMARY:END -->
