# Phase 4D.4 Task 1 brief — detached transition value and bounded feed

This brief is extracted from `Docs/superpowers/plans/2026-09-25-commander-phase4d4.md` Task 1. Read this first as the single source of task requirements, then consult the Phase 4D.4 design spec only for an ambiguity. The original Phase 4D goal remains authoritative.

**Files:** Create `Assets/Scripts/AI/Commander/Strategic/StrategicAdvisory.cs` and `.meta`; create `Assets/Tests/EditMode/CommanderPhase4D4Tests.cs` and `.meta`. No planner or simulation-authority edits.

**Interfaces:** Consume `StrategicPlanHealthSnapshot` with its copied owner, plan identity, revision/tick, status, typed category, and resource facts. Produce an immutable `StrategicAdvisory` (copied identity, typed transition, bounded display); `StrategicAdvisoryFeed.Observe(StrategicPlanHealthSnapshot snapshot)` returns `IReadOnlyList<StrategicAdvisory>` with at most four values, and `Reset()` clears state. Retain at most 32 plan identities in deterministic insertion order. Do not retain a live snapshot reference.

- Write behavior-first EditMode tests with exact IDs `Advisory_EmitsOnMeaningfulTransition`, `Advisory_DoesNotRepeatEveryTick`, `Advisory_RecoveryEmitsOnce`, `Advisory_CompletionEmitsOnce`, `Advisory_ResetClearsDeduplication`, `Advisory_IsBounded`, `Advisory_IsPlanVersionSafe`, and `Advisory_RemainsFogSafe`. Use real health captures or construction helpers that exercise real plan state; independently assert emitted text/typed transition and detached values. Name the realistic production mutation each test catches.
- Run exact focused IDs through Unity MCP and retain a meaningful RED. A compiler error or zero-discovery is setup failure, not RED.
- Implement the smallest pure feed. Seed first observation silently; normalize status/category and typed resource blocker identity, not changing deficit amounts. Emit only meaningful transitions in stable order with per-observation cap, once per transition, and deterministic oldest-entry eviction. Reject a snapshot if its revision **or** observed tick is lower than the last accepted snapshot for the same plan identity. Render only facts present in copied health; `Unknown` yields no causal claim. The current health model has no `NeedsPlayerDecision` category, so that example trigger is documented as unavailable rather than guessed.
- Run focused GREEN, static type/reflection checks for live references, and relevant Phase 4D.2 health EditMode regression. Freeze source/hash and request independent scoped value/fog/dedup review before Task 2.

## Global constraints

- Advisory is information only: no planner submission, goal-manager mutation, command buffer, lifecycle control, bridge approval, provider call, or new objective from this production code.
- Use only detached owner-scoped health; no hidden enemy data, simulation object retention, wall-clock gameplay logic, cross-match state, unbounded history, or per-tick transcript spam.
- Keep the current dirty workspace and protected baseline intact; do not stage, commit, reset, clean, change packages/settings/credentials, or touch unrelated scenes.
- Save exact test job IDs/counts/artifacts/hashes and compiler state. One production writer and one Unity runner.
