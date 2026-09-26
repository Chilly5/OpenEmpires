# Commander Phase 4D.4 implementation plan

> Execute task-by-task with superpowers:subagent-driven-development, behavior-first RED/GREEN, and one production writer/Unity runner at a time. Root owns source/authority rulings and the final gate.

**Goal:** Add bounded, offline, fog-safe strategic transition advisories without giving the provider or advisory layer any execution authority.

**Architecture:** A pure `StrategicAdvisoryFeed` observes copied `StrategicPlanHealthSnapshot` values and emits detached typed notices only for meaningful per-plan transitions. A thin `CommanderChatUI` partial presents those notices through the existing bounded transcript, with generation/owner/plan checks and reset handling. Planner, approval, command, and provider paths remain unchanged.

**Tech Stack:** Unity 6000.5, C#, NUnit EditMode and PlayMode, Unity MCP test jobs.

**Spec:** `Docs/superpowers/specs/2026-09-25-commander-phase4d4-design.md`; original Phase 4D objective `C:/Users/RS/.codex/attachments/6ef5fd8d-d949-4b52-9c96-f84d02fb0968/pasted-text-1.txt`.

## Global constraints

- Begin only after the Phase 4D.3 gate passes. Preserve current dirty workspace, source baseline, and protected hashes. Do not stage, commit, reset, clean, change packages/settings/credentials, or touch unrelated scenes.
- Advisory is information only: no `StrategicPlanner.SubmitIntent`, `CommanderGoalManager`, `CommandBuffer`, lifecycle control, bridge approval, provider call, or new strategic objective from advisory production code.
- Use only owner-scoped detached health; no hidden enemy data, simulation object retention, wall-clock/gameplay randomness, cross-match state, unbounded history, or per-tick transcript spam.
- Each task records current-source hashes, exact discovered test IDs/counts, terminal job/native XML bytes and SHA-256 if returned, console state, and independent review. Zero-discovery/import/compile failures are not behavioral RED or pass evidence.

## Review focus

| Input/condition | Expected behavior and owning test |
|---|---|
| Repeated snapshots while deficits change each tick | No repeated advisory until typed state changes; `Advisory_DoesNotRepeatEveryTick`. |
| Resource/population blockers disappear while another blocker remains | Emit one bounded recovery for the resolved typed blocker, not a false all-clear; `Advisory_RecoveryEmitsOnce`. |
| Older revision/tick or replaced plan shares nearby numeric IDs | Reject stale observation and preserve new plan state; `Advisory_IsPlanVersionSafe`. |
| Old provider/event returns after match or pipeline reset | No advisory, memory, pending intent, or execution leaks; Task 2 reset PlayMode test. |
| Unknown/fog-hidden tactical cause | State only copied owned health facts, never invent an enemy cause; `Advisory_RemainsFogSafe`. |
| Non-selected plan changes or terminates | Observe its exact identity, including after removal from `ActivePlans`; Task 2 non-selected/terminal tests. |
| Several event transitions occur within one simulation tick | Periodic scan runs once per tick, but event captures still reach the feed; Task 2 same-tick test. |

---

### Task 1: Detached transition value and bounded feed

**Files:** Create `Assets/Scripts/AI/Commander/Strategic/StrategicAdvisory.cs` and `.meta`; create `Assets/Tests/EditMode/CommanderPhase4D4Tests.cs` and `.meta`. No planner or simulation-authority edits.

**Interfaces:** Consume `StrategicPlanHealthSnapshot` with its copied owner, plan identity, revision/tick, status, typed category, and resource facts. Produce an immutable `StrategicAdvisory` (copied identity, typed transition, bounded display); `StrategicAdvisoryFeed.Observe(StrategicPlanHealthSnapshot snapshot)` returns `IReadOnlyList<StrategicAdvisory>` with at most four values, and `Reset()` clears state. Retain at most 32 plan identities in deterministic insertion order. Do not retain a live snapshot reference.

- [ ] Write behavior-first EditMode tests with exact IDs `Advisory_EmitsOnMeaningfulTransition`, `Advisory_DoesNotRepeatEveryTick`, `Advisory_RecoveryEmitsOnce`, `Advisory_CompletionEmitsOnce`, `Advisory_ResetClearsDeduplication`, `Advisory_IsBounded`, `Advisory_IsPlanVersionSafe`, and `Advisory_RemainsFogSafe`. Use real health captures or construction helpers that exercise real plan state; independently assert emitted text/typed transition and detached values. Name the realistic production mutation each test catches.
- [ ] Run exact focused IDs through Unity MCP and retain a meaningful RED. A compiler error or zero-discovery is setup failure, not RED.
- [ ] Implement the smallest pure feed. Seed first observation silently; normalize status/category and typed resource blocker identity, not changing deficit amounts. Emit only meaningful transitions in stable order with per-observation cap, once per transition, and deterministic oldest-entry eviction. Reject a snapshot if its revision **or** observed tick is lower than the last accepted snapshot for the same plan identity. Render only facts present in copied health; `Unknown` yields no causal claim. The current health model has no `NeedsPlayerDecision` category, so that example trigger is documented as unavailable rather than guessed.
- [ ] Run focused GREEN, static type/reflection checks for live references, and relevant Phase 4D.2 health EditMode regression. Freeze source/hash and request independent scoped value/fog/dedup review before Task 2.

### Task 2: Thin host presentation, lifecycle cleanup, and PlayMode proof

**Files:** Create `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Advisories.cs` and `.meta`; modify `CommanderChatUI.cs` and possibly `.StrategicControls.cs` only at existing event, health refresh, and reset hooks; create `Assets/Tests/PlayMode/CommanderPhase4D4HostPlayModeTests.cs` and `.meta`.

**Interfaces:** Consume Task 1 feed values. The host owns pipeline/generation and owner validation; feed has no host reference. Presentation is transcript-only (`AppendLine` with non-authoritative memory behavior); existing approval/control and provider interfaces do not change.

- [ ] Write real-host PlayMode RED tests with exact IDs `Advisory_DoesNotExecuteAnything` and `Advisory_DoesNotCallProvider`: start an approved plan, induce typed wait/pause/recovery, assert a bounded advisory appears but provider, decision, plan, goal, reservation, and command counts do not change because of the observation. Add named host cases `Advisory_PauseResumeTransition`, `Advisory_PopulationRecovery`, `Advisory_CancelAndComplete`, `Advisory_NonSelectedPlanTransition`, `Advisory_SameTickEventsAreNotLost`, `Advisory_SamePipelineReinitializeClearsState`, and `Advisory_ResetRejectsOldProviderAndEvent`. Each case asserts transcript output and unchanged provider/decision/plan/goal/reservation/command counts except the ordinary deterministic work explicitly induced by its fixture. Include reused numeric plan ID after reset where practical.
- [ ] Run exact focused host IDs to meaningful behavioral RED. Preserve job/evidence.
- [ ] Wire feed to existing planner status/milestone/goal/reservation callbacks and a periodic scan of **every active owned plan once per `simulation.CurrentTick`**. Event callbacks capture fresh health for their exact plan, including terminal plans removed from `ActivePlans`, even if the periodic scan already ran in the same tick; pure-feed dedupe suppresses repeated state, not distinct same-tick transitions. Verify owner, pipeline identity, `ReferenceEquals(planner.GetPlan(id), eventPlan)`, creation identity, and generation before presenting. Render only the copied snapshot, not mutable event-plan text. Never call bridge, approval, lifecycle, planner submission, goals, commands, or provider from this path. Reset feed on `Initialize`, same/different-pipeline `InitializeStrategic`, `ResetConversation`, owner/match replacement, and `OnDestroy`.
- [ ] Run focused GREEN, affected Phase 4C explanation/4D1 controls/4D2 health/4D3 adaptation and runtime PlayMode suites, and zero compiler errors. Independent Sol review must verify no authority path, fog leak, cross-match advisory, or spam before the 4D.4 gate.

### Task 3: 4D.4 gate and final Phase 4D audit

**Files:** Update `Docs/CommanderPhase4D/progress.md`, `requirements-matrix.md`, task reports and ledger; create `phase4d4-gate-report.md`, `phase4d-final-source-hashes.json`, `phase4d-final-boundary-audit.json`, `Docs/CommanderPhase4D.md`, and compact external Antigravity audit package.

- [ ] Freeze current source, verify all ten required advisory IDs discovered/passed, no failures/skips/inconclusive or compiler errors, and run independent scoped review plus relevant Commander regression. Mark 4D.4 gate only if all five subphase gates pass.
- [ ] Run fresh complete EditMode then complete PlayMode sequentially on final source. Retain exact terminal IDs/counts, native XML if available (else full terminal payload), artifact/source hashes, and console state. Do not reuse historical 4D.3 runs to claim final-source proof.
- [ ] Audit final real PlayMode scenarios A–F requirement by requirement; add missing real scenarios before claiming them. Include advisory absence after reset and plan-version safety. Do not convert pure/unit test output into runtime proof.
- [ ] Verify protected baseline/current hashes, intentional authority-file drifts, source-scope and credential/forbidden-reference candidates without displaying secret values, `.env` state, packages/settings, and temporary/recovery-scene provenance. Make manifest machine-verifiable and verify it against live disk.
- [ ] Have an independent Sol whole-Phase4D reviewer check architecture, authority, stale async, ownership, resource safety, fog, test adequacy, manifest, and static audit. Close Critical/Important findings with RED→GREEN+new full suites where shared authority changes.
- [ ] Write `Docs/CommanderPhase4D.md` with sections A–G and exact final line `READY FOR PHASE 4E` only if every gate and scenario is proved; otherwise end `REQUIRES FIX PHASE` with precise open items. Prepare source-verifiable Antigravity package but do not wait for external audit or begin Phase 4E.
