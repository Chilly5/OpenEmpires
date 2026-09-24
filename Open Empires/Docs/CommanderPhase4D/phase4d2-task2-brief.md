# Phase 4D.2 Task 2 brief

Read this with the approved design spec `Docs/superpowers/specs/2026-09-24-commander-phase4d2-design.md`. Do not implement Task 3.

## Global Constraints

- Start production work only after the root confirms the recorded Phase 4D.1 Gates 1–5 PASS against final 4D.1 source; reread current APIs and preserve the dirty, uncommitted user tree. `Docs/CommanderPhase4D/phase4d-source-baseline.json` remains the immutable Phase 4C reference.
- Only one production writer and exactly one Unity runner owner at a time. Finish each test job to terminal before starting another; do not overlap full suites or redo a completed run because a handle was lost.
- This phase is observation only. Preserve `StrategicApprovalLayer → StrategicDecisionPolicy → StrategicPlanner → CommanderGoalManager → existing deterministic RTS execution`; no provider/planner shortcut, control, intent, goal, reservation, command, simulation mutation, autonomous adaptation, or advisory emission from health.
- Do not add `TechnologyRush`, `SiegePreparation`, `NavalExpansion`, network messages, packages, credentials, cloud memory, wall-clock/frame/random decisions, or hidden enemy information. Do not alter the Phase 4C House reservation policy without an actual RED correctness failure and authority review.
- Preserve public and reflection-sensitive signatures. New snapshots contain copied primitive/value data only; explicitly serialized fields are bounded, deterministic, culture-invariant, and stable-order; unknown enum values fail closed.
- Before a protected-file edit, record frozen before-hash, exact diff, reason, authority impact, focused regression, after-hash, and independent review. Use `Docs/CommanderPhase4D/phase4d-protected-boundary-frozen.json`; do not silently rebaseline.
- Use meaningful behavioral RED before each change where practical. Record agent, task, expected output, RED, implementation, GREEN, review, and residuals in `Docs/CommanderPhase4D/phase4d2-task-report.md`; update `progress.md` and `requirements-matrix.md` only during execution, not as plan preparation.
- At the 4D.2 source freeze, run fresh complete EditMode **and** PlayMode suites: zero failed, skipped, or inconclusive and all new test IDs discovered. Do not infer current-source success from Phase 4C/4D.1 totals or historical jobs.

### Task 2: Offline host health view, explanations, and stale reset

**Files:** Modify `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs` and `CommanderChatUI.Explanations.cs`; modify `CommanderChatUI.cs` only for a necessary narrow hook; test `Assets/Tests/PlayMode/CommanderPhase4D2HostPlayModeTests.cs` with `.meta` and extend `CommanderPhase4D2Tests.cs` for pure rendering. The reviewer can reject host UX while preserving Task 1's core projection.

**Interfaces:** The host calls `strategicPipeline.StrategicPlanner.CapturePlanHealth(Conversation.PlayerId, selectedPlanId)` on Unity's owning thread immediately before every health answer and visible refresh. The planner creates its own fresh owned-state context; the UI never accesses `GameSimulation` for health. Revision is an identity/staleness check, **not** a cache freshness key: resource/population/queue state may change without a plan revision. A host-local validity check compares current pipeline reference, `runtimeGeneration`, trusted owner, plan ID/creation tick/revision, and nonterminal current selection before rendering as current. Terminal responses are explicit plan-ID historical observations only while that exact plan remains retained; no “current strategy” alias silently selects an archived plan.

- [ ] **Step 1: Write RED host tests.** Start the real initialized `CommanderChatUI` with a counting provider double. Test exact whole-form `why is the strategy paused?`, `why is the plan waiting?`, `what is blocking the current strategy?`, `did the strategy recover?`, and `why did the plan stop?`; mixed/hostile suffixes must not match. Assert zero provider calls, zero plan/goal/reservation/command changes, bounded truthful primary/secondary evidence, and explicit “evidence unavailable” when no matching current evidence exists. Add `MatchingNumericIdentity_NewPipelineRejectsOldHealth`, `UnchangedRevision_WorldChangeRefreshesHealth` for resource/population/queue changes, owner mismatch, plan revision change, reset/reinitialize, and destruction tests. Retain existing 4D.1 button and cancel-confirmation behavior.

- [ ] **Step 2: Run focused RED** for the verified `CommanderPhase4D2HostPlayModeTests` group; preserve the terminal job and specific assertion, not only a count.
- [ ] **Step 3: Implement minimal rendering/routing.** Reuse the exact whole-form normalization style in `CommanderChatUI.Explanations.cs`; dispatch before the provider in `SubmitMessageAsync`. Recapture on each answer/render even if plan revision did not change, then render copied typed primary and bounded secondary health evidence. Never parse `OutcomeMessage` or `StatusReason` or diagnose a hidden enemy, a worker shortage, or a unit-specific producer from generic evidence. Show a short health line next to existing selected-plan status. Do not create recommendation/advisory events or mutate memory with live DTO graphs; if recording text, use the existing bounded informational memory path only.

```csharp
// Read-only routing belongs before the existing provider branch; use actual host-private names.
if (TryHandleStrategicLifecycle(trimmed)) return null;
if (TryHandleExplanationQuery(trimmed)) return null;
if (TryHandlePlanHealthQuery(trimmed)) return null;
```
- [ ] **Step 4: Implement host invalidation.** Keep no core cache. If the host retains the most recent snapshot, clear it on `ResetConversation`, `Initialize`/`InitializeStrategic`, pipeline replacement, owner change, and destruction; revalidate pipeline/generation/token at render time. An old numeric identity from another match must never be considered current. Preserve existing public/reflection-sensitive signatures; add an overload rather than alter one.
- [ ] **Step 5: Run focused GREEN** and affected Phase 4C.2 explanation plus Phase 4D.1 host groups under the single runner. Record discovered IDs, no-provider counters, exact jobs, source hashes, compiler zero, and XML/payload evidence. Obtain independent review of stale, ownership, and authority boundaries before Task 3.

