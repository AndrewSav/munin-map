#!/usr/bin/env bash
# patches: TASK-6. Works out which release this run makes, for .github/workflows/patches.yml.
#
#   release-tag.sh check <tag>   a pushed tag: patches-X.Y.Z-N whose X.Y.Z must be the version in
#                                package/manifest.json (decision-1); fails otherwise
#   release-tag.sh next          the Run workflow button: the next tag for this version
#                                (N one more than the highest patches-X.Y.Z-* tag, else 1), created
#                                here as an annotated tag listing the task IDs in the commit
#                                subjects since the previous patches-* tag (all of them when that
#                                tag isn't on the branch, as after a rebase). Not pushed here.
#
# Both print tag=, version= and stamp= lines (for $GITHUB_OUTPUT); stamp is X.Y.Z+patches.N, the
# version the build is stamped with.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

version=$(python3 -c "import json;print(json.load(open('package/manifest.json'))['version_number'])")

case "${1:-}" in
  check)
    tag="${2:?usage: release-tag.sh check <tag>}"
    if [[ ! "$tag" =~ ^patches-([0-9]+\.[0-9]+\.[0-9]+)-([0-9]+)$ ]]; then
      echo "::error::tag $tag is not patches-X.Y.Z-N (decision-1)"; exit 1
    fi
    if [ "${BASH_REMATCH[1]}" != "$version" ]; then
      echo "::error::tag $tag names ${BASH_REMATCH[1]}, but package/manifest.json says $version"; exit 1
    fi
    n="${BASH_REMATCH[2]}"
    ;;
  next)
    last=$(git tag -l "patches-$version-*" | sed -n "s/^patches-$version-\([0-9][0-9]*\)$/\1/p" | sort -n | tail -1)
    n=$(( ${last:-0} + 1 ))
    tag="patches-$version-$n"
    prev=$(git describe --tags --abbrev=0 --match 'patches-*' HEAD 2>/dev/null || true)
    range="HEAD"; [ -n "$prev" ] && range="$prev..HEAD"
    tasks=$(git log --format=%s "$range" | { grep -oE '^TASK-[0-9]+[^ ]*' || true; } | sed 's/:$//' | sort -t- -k2 -n -u | tr '\n' ' ' | sed 's/ $//')
    subjects=$(git log --reverse --format='- %s' "$range" | grep -E '^- TASK-[0-9]+' || true)
    msg="Munin Map $version with this fork's changes (see PATCHES.md)."
    [ -n "$tasks" ] && msg="$msg

Tasks: $tasks

$subjects"
    git -c user.name="github-actions[bot]" -c user.email="41898282+github-actions[bot]@users.noreply.github.com" \
      tag -a "$tag" -m "$msg"
    ;;
  *) echo "usage: release-tag.sh check <tag> | next"; exit 2 ;;
esac

echo "tag=$tag"
echo "version=$version"
echo "stamp=$version+patches.$n"
