# Phase 4D.3 Task 4 — deterministic recovery and source-freeze evidence

Status: **ready for root gate decision**, 2026-09-26. This is not Phase 4D completion or Phase 4E readiness. One Unity runner owned the jobs serially. No production source, package, setting, credential, or scene was edited by Task 4.

## Runtime tests

Added `Assets/Tests/PlayMode/CommanderPhase4D3RuntimePlayModeTests.cs` and `.meta`. The final test SHA-256 is `69040993B008F6BD470C9632A94ABDDAABF945450ADE0B5C4C41A55D6F341EB1`.

- `TemporaryResourceBlocker_RecoversWithoutNewApproval`: the real chat/provider translation and Approve button create a RangedReinforcement plan through the real approval/pipeline/planner/goal manager. A controlled post-approval Food/Wood stockpile depletion yields a sampled typed current-milestone Wood deficit. Existing gatherers and strategic reservation retry, without new input, advance a later milestone and complete the same plan. It asserts fixed Range/ten-Archer targets, exactly 11 target commands, one decision/provider call, and zero terminal active reservations. The stockpile change is a controlled changing-world fixture, not proof of a player-spending cause.
- `PopulationPrerequisite_RecoversWithoutNewApproval`: 16 workers and one House start at population 16/20. The approved ten-Archer force reaches real population-plus-queue pressure with remaining orders below the hard maximum. Ordinary deterministic tactical execution places and finishes exactly one additional House; the cap rises, the same force plan advances and completes. It asserts fixed goals/targets, exactly 12 target/prerequisite commands, one decision/provider call, and zero terminal active reservations. No manual capacity increase or fabricated DTO is used.

The existing 4D.3 host test `ActiveRangedToDefensive_PendingCannotUseOrdinaryApprove_ConfirmReplacesThroughPolicy` already covers actual active-plan pending/ordinary-Approve no-op/explicit Confirm, real reservation release, and new-plan start. Task 4 did not duplicate it with a weaker fixture. These tests do **not** diagnose or fix the separately reported tactical Spearman wood-gathering issue.

## Exact Unity jobs and artifact custody

| Gate | Job ID | Terminal | Complete payload / SHA-256 |
|---|---|---|---|
| Initial focused runtime | `90eeb121a42246c2a1a4402ce240f616` | 2/2 passed, 0 failed/skipped | `phase4d3-task4-focused-90eeb121.json` |
| Final focused runtime after stronger terminal assertions | `03568f54ef0d4582ba3c86c76ebbe54d` | 2/2 passed, 0 failed/skipped | `phase4d3-task4-focused-final-03568f54.json` / `60812DA70029D4EBCE81E99D67A50C330E77494B7C01D3C1AA45C02F52E2A233` |
| Affected 4D.2 runtime + 4D.3 host/runtime PlayMode | `faff8396424d461a8c3bcc9f5e634f60` | 29/29 passed, 0 failed/skipped | `phase4d3-task4-affected-faff8396.json` / `0BB7D96677B06AD3EBDA9901AB1EDE520C05C1602774F930AE3AAAF953F6F9AF` |
| Final-source complete EditMode, after protected nested-admission fix | `e16cfa9309dd42dcbfdb59e4b333cfa4` | **689/689 passed**, 0 failed/skipped/inconclusive, 689 unique names | `phase4d3-full-edit-e16cfa93.json` / `DE04E481DFA6874FCDE4111647D219E8C6EEBFD7E4E9FC633DC9F2CE19F2FA0F` |
| Final-source complete PlayMode, sequential after EditMode | `e5e4a460cdcb4c2e8088d8817cb77f6b` | **145/145 passed**, 0 failed/skipped/inconclusive, 145 unique names | `phase4d3-full-play-e5e4a460.json` / `BFA8AEE8491579DA3DEC61A94473C585CB8B5C956FD14524B55C5714846C1084` |

The full payloads contain every test name, state, duration, message, stack, and output available from Unity MCP; no native XML was returned for these two final jobs. Complete-suite discovery includes 22 `CommanderPhase4D3Tests` EditMode, 26 `CommanderPhase4D3HostPlayModeTests`, and both required new runtime PlayMode IDs. Console `error CS` filter returned zero entries after full suites; `git diff --check` returned no whitespace errors (Git emitted only LF/CRLF working-copy warnings).

The first new-file test attempts (`d9f1e7c8a3c740eeb64d16aad8cffe7e`, `070c3e31769b48eb89de81aeffcfc4b1`) discovered zero before explicit Unity asset import. The first imported compile had a test-only `TargetTotal` versus `Count` property typo; it was corrected before behavioral tests. These are setup/fixture errors, **not** behavioral RED. Existing deterministic recovery worked on the first actual discovered run, so no artificial RED or production patch was made. A historical full EditMode job `dccba5bd147a42baacc9b8e5c89bc59b` lost its MCP handle before terminal and predates the reviewed protected fix; it is not gate evidence.

## Residuals

- Task 1 and the whole-phase fix document a nontransactional exceptional-observer case: if an external cancellation observer throws after preflight, old cleanup completes but no new plan is inserted; the player must retry. This is not a provider authority path or silent partial reservation leak.
- Task 3 independent review left a Minor test gap: explanation-query dismissal while an adaptation is actively pending is not directly asserted, although active new-message dismissal and no-plan explanation behavior are covered. Keep it visible in the 4D.3-only gate review.
