# munin-map fork: the `patches` branch

This branch carries our own changes on top of upstream (cocopuff2u-gaming/munin-map) and is
rebased onto each upstream release (decision-1 in `backlog/decisions/`).

**Write every change so it rebases easily (decision-2).** Consider this first, before how the
change fits the codebase:
- Put new behaviour in new files we own. Keep edits to upstream files small and surgical: a
  call, a hook, a registration line.
- Mark edits in upstream files with a comment such as `// patches: TASK-1` where the format
  allows a harmless one (not JSON, not Markdown table rows).
- Start every commit subject with the task ID (`TASK-1: ...`), one commit per feature.
- Never reformat, reorder or tidy upstream lines you don't need to change. Leave
  `CHANGELOG.md` and version numbers alone.
- Git is the record of what the fork touches. Upstream files changed, and the tasks behind one:
  `git diff --stat <release>..patches -- . ':!backlog' ':!patches-tools' ':!CLAUDE.md' ':!PATCHES.md'`
  `git log --format=%s <release>..patches -- <file>`
  (`<release>`: the upstream tag `patches` sits on, e.g. `git describe --tags --abbrev=0 upstream/main`).
- Keep `PATCHES.md` (the register of product changes from upstream) up to date: every product
  change gets an entry before its task is Done, and the entry changes when the change does.
  Tooling (local test server, CI, backlog) stays out of it.
- When a task or subtask goes Done, decide whether it is user-facing (changes what a server admin or
  a map visitor sees or can do). If it is, add its line to `patches-tools/release-notes.md`: first
  remove the lines already released (those in the file at the latest `patches-*` tag, after
  `git fetch --tags`), then add `- TASK-n: <line>`, its PATCHES.md heading where that fits. After a
  rebase onto a new upstream version, add `- Updated to Munin Map X.Y.Z` the same way. Internal and
  tooling tasks get no line.
- Before archiving a backlog task or draft, append its reason to its notes
  (`--append-notes "Archived <date>: <reason>"`, naming the task or commit that made it unnecessary);
  archived items are read-only.
- After resolving a rebase conflict, reshape our code so that spot is less likely to conflict
  again.

Fork-only tooling goes in `patches-tools/`.

<!-- BACKLOG.MD GUIDELINES START -->
<!-- backlog.md-instructions-version: 1.55.3 -->
<CRITICAL_INSTRUCTION>

## Backlog.md Workflow

This project uses Backlog.md for task and project management.

**At the beginning of each conversation in this project, run `backlog instructions overview` before answering or taking action. Re-read it only if you have not read it yet in the current conversation.**

Use the overview to decide whether to search, read, create, or update Backlog tasks.

Before task lifecycle actions, read the matching detailed guide:
- `backlog instructions task-creation` before creating or splitting tasks
- `backlog instructions task-execution` before planning, changing status or assignee, adding a plan or implementation notes, or implementing task work
- `backlog instructions task-finalization` before checking acceptance criteria, writing final summaries, or moving tasks to terminal statuses

Use `backlog <command> --help` before running unfamiliar commands. Help shows options, fields, and examples.

Do not edit Backlog task, draft, document, decision, or milestone markdown files directly. Use the `backlog` CLI so metadata, relationships, and history stay consistent.

</CRITICAL_INSTRUCTION>
<!-- BACKLOG.MD GUIDELINES END -->
