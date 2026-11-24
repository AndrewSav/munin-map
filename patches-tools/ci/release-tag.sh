#!/usr/bin/env bash
# patches: TASK-6. Works out which release this run makes, for .github/workflows/patches.yml.
#
#   release-tag.sh check <tag>   a pushed tag: patches-X.Y.Z-N whose X.Y.Z must be the version in
#                                package/manifest.json (decision-1); fails otherwise
#   release-tag.sh next          the Run workflow button: the next tag for this version
#                                (N one more than the highest patches-X.Y.Z-* tag, else 1), created
#                                here as an annotated tag. Not pushed here.
#   release-tag.sh notes <tag> <repo-url>
#                                the release's notes, made from patches-tools/release-notes.md at that
#                                tag (TASK-33): a header line linking PATCHES.md, then the file's lines,
#                                each with its task linked; links point at the tag
#
# check and next fail when release-notes.md's lines are the same as at the previous patches-* tag
# (TASK-33): a release with nothing new logged means a change was missed there. A release on a new
# upstream version also needs the line "- Updated to Munin Map X.Y.Z".
#
# check and next print tag=, version= and stamp= lines (for $GITHUB_OUTPUT); stamp is X.Y.Z+patches.N,
# the version the build is stamped with.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

NOTES=patches-tools/release-notes.md
version=$(python3 -c "import json;print(json.load(open('package/manifest.json'))['version_number'])")

# the notes file's lines at a revision ("" when it isn't there)
lines_at() { { git show "$1:$NOTES" 2>/dev/null || true; } | { grep -E '^- ' || true; }; }

# fails when the notes at $1 are the same as at the latest patches-* tag other than $2
check_new_notes() {
  local rev="$1" self="${2:-}" prev
  prev=$(git tag -l 'patches-*' --sort=-v:refname | { grep -vxF "${self:-//}" || true; } | head -1)
  [ -z "$prev" ] && return 0
  if [ "$(lines_at "$rev")" = "$(lines_at "$prev")" ]; then
    echo "::error::$NOTES has nothing new since $prev: log the user-facing changes there before releasing"; exit 1
  fi
  # a release on a new upstream version says so
  case "$prev" in "patches-$version-"*) return 0 ;; esac
  if ! lines_at "$rev" | grep -qxF -- "- Updated to Munin Map $version"; then
    echo "::error::$prev was on another upstream version: add '- Updated to Munin Map $version' to $NOTES"; exit 1
  fi
}

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
    check_new_notes "$tag" "$tag"
    ;;
  next)
    last=$(git tag -l "patches-$version-*" | sed -n "s/^patches-$version-\([0-9][0-9]*\)$/\1/p" | sort -n | tail -1)
    n=$(( ${last:-0} + 1 ))
    tag="patches-$version-$n"
    check_new_notes HEAD
    git -c user.name="github-actions[bot]" -c user.email="41898282+github-actions[bot]@users.noreply.github.com" \
      tag -a "$tag" -m "Munin Map $version with this fork's changes (see PATCHES.md)."
    ;;
  notes)
    tag="${2:?usage: release-tag.sh notes <tag> <repo-url>}"
    repo="${3:?usage: release-tag.sh notes <tag> <repo-url>}"
    v=$(git show "$tag:package/manifest.json" | python3 -c "import json,sys;print(json.load(sys.stdin)['version_number'])")
    # the backlog's task files at the tag, for the links
    git ls-tree -r --name-only "$tag" -- backlog > /tmp/release-files.txt
    lines_at "$tag" | python3 -c '
import sys, re, urllib.parse
tag, repo, v, files = sys.argv[1], sys.argv[2], sys.argv[3], open(sys.argv[4], encoding="utf-8").read().split("\n")
blob = lambda p: f"{repo}/blob/{urllib.parse.quote(tag)}/{urllib.parse.quote(p)}"
pm = blob("PATCHES.md")
print(f"Munin Map {v} with this fork'"'"'s changes (see [PATCHES.md]({pm})).\n")
for line in sys.stdin.read().splitlines():
    m = re.match(r"- (TASK-[0-9.]+): (.*)", line)
    if not m:
        if line.startswith("- "): print(line)   # a line without a task, e.g. Updated to Munin Map 3.5.0
        continue
    tid, text = m.groups()
    f = next((p for p in files if re.search(r"(^|/)" + re.escape(tid.lower()) + r" - ", p.lower())), None)
    print(f"- {text} ([{tid}]({blob(f)}))" if f else f"- {text} ({tid})")
' "$tag" "$repo" "$v" /tmp/release-files.txt
    exit 0
    ;;
  *) echo "usage: release-tag.sh check <tag> | next | notes <tag> <repo-url>"; exit 2 ;;
esac

echo "tag=$tag"
echo "version=$version"
echo "stamp=$version+patches.$n"
