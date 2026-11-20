---
id: TASK-21
title: Document the settings missing from docs/ADVANCED.md's settings table
status: To Do
assignee: []
created_date: '2026-11-20T22:04:00Z'
labels:
  - docs
dependencies: []
references:
  - docs/ADVANCED.md
  - src/Config.cs
  - README.md
type: docs
ordinal: 20000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
docs/ADVANCED.md has a settings table meant as the one place to look a setting up, but 16 of upstream 3.4.0's 59 settings (src/Config.cs) are not in it, so an admin can miss them; some are mentioned elsewhere, in README prose or the README's Discord table, where they are easy to overlook. Found 2026-11-21 by matching every `config.Bind` key against the table's Key column (upstream/main ee49d16):

- not documented anywhere: Texture `texture_size`, `pixel_size`; Interval `save_fog_texture_interval`, `stats_save_interval`; User `web_pings`, `hidden_players`; Debug `perf_report`; Server `test`, `show_github_link`, `check_updates`; Render `main_thread_rows_per_frame`; Sweep `first_sweep_delay`, `second_sweep_delay`;
- only in README prose: Server `data_path`;
- only in the README's Discord table: Server `preview_image`, `preview_description`.

The new rows go together as one block at the end of the table, not interleaved by section, so the edit to this upstream file stays one hunk that rebases easily (decision-2). Each row's text must say what the code actually does, not just repeat the config description: some descriptions are vague ("Enable test features (bugs).") or may be out of date. Where code and description disagree, the table says what the code does and the disagreement goes in this task's notes; fixing the code or the config description is out of scope.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Every setting bound in src/Config.cs (fork branch, at the time of the change) has a row in docs/ADVANCED.md's settings table, checked by a script that compares the `config.Bind` keys with the table's Key column
- [ ] #2 The new rows are one contiguous block at the end of the table, with no change to existing rows, and use the table's columns (Section, Key, Default, What)
- [ ] #3 Each new row's default matches the code, and its "What" is checked against the code that reads the setting, with the place in the code recorded in the task notes for each
- [ ] #4 Any setting whose config description does not match what the code does is listed in the task notes, with what the code actually does
- [ ] #5 Committed with a subject starting with the task ID
<!-- AC:END -->
