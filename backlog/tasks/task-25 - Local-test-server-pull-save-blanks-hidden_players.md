---
id: TASK-25
title: 'Local test server: pull-save blanks hidden_players'
status: Done
assignee:
  - '@claude'
created_date: '2026-11-21T08:03:00Z'
updated_date: '2026-11-21T08:11:00Z'
labels:
  - tooling
dependencies: []
type: chore
ordinal: 24000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
pull-save.ps1 copies live's Munin settings to the test server. Live hides some names from the map with `hidden_players` (for example an admin character), and the test server then hides the same names. Test characters are often the very ones live hides, so after a pull the tester can't see themselves on the map, and their trails, heat and builder names aren't recorded either. The test server is local only, so there is nothing to hide there. Fork tooling only (patches-tools/local-test), no upstream files.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 pull-save.ps1 writes the test server's Munin settings with `hidden_players` blank, as it does for `discord_webhook`
- [x] #2 After a pull, a character whose name is in live's `hidden_players` shows on the test map
- [x] #3 README.md in patches-tools/local-test lists the blanked setting with the others
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [x] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Live's hidden_players = admin hid the test character AdMiN (case-insensitive) after a pull. pull-save.ps1 now blanks it alongside discord_webhook. First edit put a comment after a line-continuation backtick (breaks the continuation); moved it above. Full pull run: test cfg has hidden_players and discord_webhook blank, data_path the test volume. User rejoined and sees their character on the map. decision-2: fork tooling only, no PATCHES.md entry.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
pull-save.ps1 blanks hidden_players in the Munin settings it copies from live, so test characters that live hides show on the test map (and their trails, heat and builder names are recorded). README lists it with the other changed settings. Verified by the user after a pull and restart.
<!-- SECTION:FINAL_SUMMARY:END -->
