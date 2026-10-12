# Task3 fix round1 — actual native receipt, formation parity, causal wording

Read task-3-review.md and task-3-report.md. Fix only the three proved source risks:
1. ObserveNativeMovement may use an idle/no-path actor's SimPosition as endpoint
after any later simulation tick, without proof the issued command was processed.
2. Native movement radius scaling/scattering changes FormationPositions after the
planner validates them; mixed radii/larger formations may target unchecked cells
or be falsely blocked by the two-unit check.
3. `What happened to the six units?` bypasses the owned causal projection.

Movement completion must use a detached receipt/event attributable to the actual
ordinary command and each exact actor. Never synthesize completion from idle/no-path
position or unrelated replacement orders. An actual already-at-target result may be
recognized only from native processing/normal preflight truth, not arbitrary near-slot
position. Preserve captured exact IDs, human priority, missing/foreign/garrison/busy
classification, cancellation/reset/runtime affinity and bounded no-progress.

Use the smallest existing/native observation seam. A narrow normal-Move processing
receipt and shared deterministic formation-target resolver are allowed if necessary;
do not duplicate geometry inconsistently or alter default gameplay behavior merely
to satisfy Commander. Preflight validates the destinations native will actually use
(radius/scatter/snap and actor geometry), within the semantic area and known complete
routes. Receipt observes processed command/path result, not an assumed tick. Preserve
normal ICommand/CommandBuffer/GameSimulation flow. No binary/network envelope or
multiplayer/Rust rewrite. Record any future network/replay verification limitation.

Causal: recognize bounded owned-actor/task `what happened` wording through existing
read-only facts path; don't turn generic economic questions into unrelated army facts.
Keep complete-or-explicit-unavailable reasons and no hidden enemy/actionable answer.
Farm fixes/zero-command atomic leases and Task1/2 hint/authority code are unchanged.

Test-first small set: issued but not processed command + idle actor near requested
slot cannot complete; no-path/partial native processing is not invented arrival;
actual native mixed-radius formation uses preflight-validated semantic/legal slots
and completes via its receipt; unrelated endpoint/order cannot satisfy it. Named
`What happened to the six units?` carries owned causal facts and no game admission.
Keep directly affected six-defense/no-progress/human/loss/generic-economic controls;
not another26category/fullsuite/live/native realtime/build/paid/audio/network run.
Use existing real simulation fixtures, accelerated label, raw records after eachrun.

Original implementer owns sole source/MCP only on parent dispatch. Preserve dirty
tree; apply_patch; no subagents/commits/reset/clean/worktrees/credentials/deploy.
Fresh actual compile/console/method checks,120000init, exactjob recovery and one
terminal0-start retry; no forcekill/restart/observer duplicates. Append FIX1 to
task-3-report.md with exactfiles/commands/IDs/hashes/results/limits and handback.
Parent scoped review then Task4; do not implement other adapters or UI in this pass.
