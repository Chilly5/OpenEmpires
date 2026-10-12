# Approved narrow recovery follow-up — 2026-10-11

## Human decisions

- `AdditionalGathered` Food completion is deliberately player-wide credited income,
  not income attributed to the chosen workers. Explicit Berries restricts orders.
- Human approved only narrow deterministic, per-tick bounded progressive recovery
  search and focused verification. No further full regression is authorized.
- The product currently reports the broader goal PAUSED. This explicit narrow
  approval authorizes this follow-up; do not start Tasks 4–7 or resume the broader
  goal automatically.

## Task

Reproduce recovery after an already-started explicit-Berries allocation loses its
source, where four nearer visible Berries candidates are unreachable and a farther
visible Berries source is reachable. Current production code already has
`progressiveRecovery`, per-worker `RecoveryCandidateOffsets`, and a bounded window;
do not replace or duplicate a working mechanism merely because an older review
described the pre-fix defect.

Verify the actual production manager/planner via the existing economy fixture.
Recovery must retain exact original selected actors, count, source constraint and
income origin, issue no command to unreachable/unauthorized sources, advance the
search over observations, and keep each observation's path-validation window
bounded. Never substitute workers, weaken explicit source, reset progress, or
introduce another executor.

If this case already passes, add focused regression coverage and report that no
production change was needed. If it fails, retain RED and make the smallest local
fix in the existing search. Also cover exhausted search/fail-closed behavior and
player-wide amount completion from another worker with ordinary gather left running
and no new Commander commands after completion. Aim at three small focused cases;
parameterization is acceptable. No broad test category/full assembly.

## Constraints and evidence

One source writer/Unity runner: the assigned implementer. No other agents, commit,
reset, cleanup, build, paid API call, credentials inspection, editor restart/kill,
multiplayer/network change or UI redesign. Read the Unity MCP, debugging and TDD
skill instructions before task action. Use apply_patch for edits. Before editing,
copy every touched source/test file into evidence/recovery-followup-baseline.
Read current editor state; correct project is Open Empires@6d7310c7. Compile and
check console. Use exact discovered NUnit full-name filters and save every terminal
raw result outside MCP. Recover existing job by exact ID; no duplicate launch.
Initialization 120000; at most one unchanged-input retry after terminal zero-start.

Write Docs/CommanderPhase5C/recovery-followup-report.md with current source facts,
root cause or already-fixed conclusion, exact tests/commands/jobs/results, files,
SHA256, RED/GREEN distinction and evidence limits. Label accelerated EditMode
fixtures accurately. Do not certify full Phase5C acceptance or obsolete full-run
claims. Return under 15 lines with status, test summary, concerns and report path.
