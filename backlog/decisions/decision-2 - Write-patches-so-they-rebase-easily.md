---
id: decision-2
title: Write patches so they rebase easily
date: '2026-11-19T09:41:00Z'
status: accepted
---
## Context

`patches` carries our features on top of upstream and is rebased onto every upstream release (decision-1). Every line we change in an upstream file can conflict at each of those rebases, and upstream releases often. Each conflict costs time and risks a wrong resolution, and the cost keeps coming back for as long as the change lives on the branch.

## Decision

How easily a change will rebase is the first consideration when writing it, ahead of how neat it looks in the codebase.

- **New behaviour goes in new files we own:** new classes in `src/`, new modules in `web/js/`, new stylesheets, new scripts in `patches-tools/`. Upstream never touches those files, so they never conflict.
- **Edits to upstream files stay small and surgical:** a call into our code, a registration line, an import, a hook. Prefer one line in one place to logic spread through an upstream file.
- **Mark edits in upstream files with a comment where the format allows a harmless one,** naming the task: `// patches: TASK-1` in C# and JS, `/* patches: TASK-1 */` in CSS, `<!-- patches: TASK-1 -->` in HTML and in Markdown outside tables. Skip it where there is no safe way: JSON, Markdown table rows, generated files. The comments are a convenience inside a conflicted file, not the record.
- **Start every commit subject with the task ID** (`TASK-1: ...`), one commit per feature. Git is then the complete record for every file type: `git log` and `git blame` on an upstream file name the task behind each changed line, and a diff against the release `patches` is based on lists every upstream file the fork touches.
- **Don't touch upstream lines we don't need to:** no reformatting, reordering, renaming, whitespace or comment tidying. Leave `CHANGELOG.md` and the version numbers alone.
- **When a change has to alter how upstream code works,** accept the edit, but confine it to as few places as possible, and keep the rest of the change in our own files.
- **Git is the record of what the fork touches; no hand-kept lists.** Before a rebase, or when reviewing a task, list every upstream file `patches` changes compared with the release it is based on, and the tasks behind a file:
  - `git diff --stat <release>..patches -- . ':!backlog' ':!patches-tools' ':!CLAUDE.md'`
  - `git log --format=%s <release>..patches -- <file>`
  where `<release>` is the upstream tag `patches` sits on (`git describe --tags --abbrev=0 upstream/main` after fetching).
- **When a rebase does conflict,** resolve it, then reshape our code around that spot so the next rebase is less likely to conflict there: move the logic into our own files, or move the hook to a quieter place. Record what happened in the task's notes.

## Consequences

- Most of a feature lives in files upstream never sees. Conflicts, when they happen, are limited to a few marked lines.
- Some features end up less integrated than an upstream author would write them, for example a hook and a separate file instead of an inline change. That is the accepted price.
- A feature that can only be done by changing upstream code widely is expensive to carry. It is worth offering upstream as a pull request instead.
