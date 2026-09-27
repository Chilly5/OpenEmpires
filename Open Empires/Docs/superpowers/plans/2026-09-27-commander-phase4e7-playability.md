# Commander Phase 4E.7 Playability Plan

## Goal

Prove the Phase 4E semantic path is playable through the normal Commander UI and simulation lifecycle, not only through direct planner tests.

## Runtime scenarios

- Submit ordinary conversational text through `CommanderChatUI` and execute the resulting `EnsureUnitCount` goal to ten live Spearmen.
- Return a genuine `Clarify` result through the same UI and verify no goal is created while the explanation is visible and bounded in memory.
- Keep the existing human-command precedence and stale-reset PlayMode coverage as the authority/lifecycle gates for the combined Phase 4E runtime.

## Constraints

The scenario provider is a deterministic fake for automated tests. Live Luna use remains a separate bounded acceptance activity; automated runs never spend API quota.
