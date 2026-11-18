---
id: decision-1
title: Fork release tags and rebase base for the patches branch
date: '2026-11-18T03:45:00Z'
status: accepted
---
## Context

`patches` carries our own features on top of upstream (cocopuff2u-gaming/munin-map) and is rebased when upstream moves. Builds released from it are what the user's own server installs.

Upstream tags each release `vX.Y.Z`. These are lightweight tags, each on a separate `X.Y.Z: …` release commit that follows the feature commits. Commits between releases on upstream `main` are untagged. Upstream's `release.yml` builds and publishes a GitHub release for every `v*` tag. It refuses a tag that does not match `version_number` in `package/manifest.json`.

The fork has no tags of its own, but a local clone holds all of upstream's tags after a fetch.

## Decision

- **Rebase onto upstream releases only.** `patches` is rebased onto an upstream release tag (`git rebase vX.Y.Z`), never onto the tip of `upstream/main`. `main` keeps mirroring `upstream/main` exactly.
- **Tag our releases `patches-X.Y.Z-N`.** `X.Y.Z` is the upstream release `patches` is based on. `N` counts our releases on that base, starts at 1, and restarts at 1 after a rebase onto a new upstream release. For example: `patches-3.3.1-1`, `patches-3.3.1-2`, then `patches-3.3.2-1`.
- **Tags are annotated** (`git tag -a`). The message lists the backlog tasks in that build and is used as the release notes.
- **Push our tags one at a time** (`git push origin patches-3.3.1-1`), never with `git push --tags`, so upstream's `v*` tags never reach the fork.
- **A patched build identifies itself as `X.Y.Z+patches.N`**, without editing upstream's version files (`package/manifest.json`, the `<Version>` in `src/MuninMap.csproj`).

## Consequences

- The `X.Y.Z` in a tag always names the same base as upstream's published build of that version, so a patched build can be compared directly with the stock one.
- Upstream fixes that are merged but not yet released don't reach `patches` until upstream tags a release.
- `patches-*` tags don't match upstream's `v*` trigger, so upstream's workflow never builds them, even if it is enabled in the fork.
- After a rebase, older tags point at commits no longer on `patches`. The tags keep those commits alive, so every published release can still be rebuilt.
- The fork's CI checks that a tag's `X.Y.Z` matches `package/manifest.json` before it builds and publishes (see the fork CI workflow task).
