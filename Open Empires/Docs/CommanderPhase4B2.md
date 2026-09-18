# Commander Phase 4B.2 — Strategic approval and controlled execution

Status: complete, verified 2026-09-15. **READY FOR PHASE 4C**.

## A. Architecture changes

Strategic interpretation now produces a trusted `AIRecommendation` value, never an executable plan. A pure approval layer checks current owner, source, status, parameters, available resources, population and canonical production/build/training capabilities. The shared `StrategicPipeline` revalidates approval immediately before decision and commitment checks, then calls the existing planner.

Source precedence is `PlayerDirect > AIConfirmedPlayerCommand > AIRecommendation`. Direct and explicitly confirmed player commands use the existing `PlayerOverride` authority; plan source preserves their different precedence. Ordinary AI defense remains Normal. Emergency defense remains a separate game-side policy decision.

Planner-owned, synchronized monotonic IDs are shared by player requests, strategic provider requests and materialized rule-based decisions. Lifetime ownership rejects reuse after history eviction, pending-ID theft and cross-owner submissions. Legacy strategic parser placeholders are explicitly marked and materialized once; fixed external and approved IDs are never silently renumbered.

The chat classifies whole supported requests. Tactical requests retain the existing adapter/dispatcher. Strategic requests display a preview with three controls: **Approve strategy** (Normal), **Confirm as command** (explicit elevated source), and **Dismiss**. No plan starts merely from translation. Confirmation consumes the exact displayed intent once and is revalidated against current state.

The bridge has no planner, simulation or command dependency. It verifies response identity/owner/tick/source and rejects timeout, cancellation, stale or mismatched results. Reinitialization prevents old asynchronous results from overwriting the current chat session.

## B. New files

Production, under `Assets/Scripts/AI/Commander/`:

- `Phase4B2/CommanderIntentRouter.cs`
- `Phase4B2/StrategicAIApprovalBridge.cs`
- `Phase4B2/StrategicApprovalLayer.cs`
- `Strategic/StrategicIntentSource.cs`
- `Strategic/StrategicIntentIdProvider.cs`
- `Strategic/StrategicFeasibility.cs`
- `Strategic/StrategicPlanner.Feasibility.cs`

Tests:

- `Assets/Tests/EditMode/CommanderPhase4B2Tests.cs`
- `Assets/Tests/PlayMode/CommanderPhase4B2PlayModeTests.cs`

Unity `.meta` files accompany new assets. Documentation includes the approved specification, implementation plan, source baseline, review ledger and machine-readable result/audit artifacts.

## C. Modified files

Under `Assets/Scripts/AI/Commander/`:

- `Strategic/StrategicIntent.cs`, `StrategicPlanner.cs`, `StrategicPlan.cs`: identity/provenance and guarded admission.
- `Strategic/StrategicContext.cs`, `StrategicContextBuilder.cs`: detached active authority/source and feasibility quotes.
- `Strategic/StrategicPipeline.cs`, `StrategicDecisionPolicy.cs`, `StrategicCommitmentPolicy.cs`: fresh approved admission, source precedence and shared decision history.
- `Strategic/StrategicRecommendation.cs`: recommendation conversion preserves AI provenance.
- `Phase4B1/StrategicAIInterpreter.cs`, `StrategicAIJson.cs`: caller-owned request IDs and safe interpretation provenance; strict JSON schema unchanged.
- `Phase4A/CommanderChatUI.cs`: classifier composition, preview/confirmation controls and session-safe result publication.
- `SimpleTextIntentParser.cs`, `CommanderIntentDto.cs`: strategic branches only, replacing placeholder construction with trusted unallocated-player interpretation. Tactical branches unchanged by this phase.

`Assets/Tests/EditMode/CommanderPhase4A1HardeningTests.cs`: timeout test now locates the Send button by name because the UI has additional strategy buttons; timeout assertions unchanged.

No simulation, command buffer, command implementation, networking, CommanderPlanner execution or goal-manager execution edits are part of this phase. Inherited work remains preserved; HEAD alone is not the pre-phase working baseline.

## D. Approval flow

```text
Player text -> pure route classification
  Tactical -> existing Phase 4A adapter -> existing tactical execution
  Strategic -> interpreter -> Created AIRecommendation -> preview
    explicit normal approval OR exact player confirmation
      -> pure approval -> shared pipeline (fresh revalidation)
      -> decision policy -> commitment policy -> StrategicPlanner
      -> StrategicPlan -> CommanderGoalManager -> existing execution
  Unknown/mixed -> rejection, no provider execution
```

Rejected approval contains no approved intent and cannot trigger fallback selection of another objective. Successful submission preserves the approved identity and source. Replays and stale resource/authority state reject without cancelling the existing plan.

## E. Tests

Environment: Unity 6000.5.9f1, Unity MCP 10.2.0. All 19 required named EditMode tests are present. Focused evidence:

- `CommanderPhase4B2-focused-69-results.json`: 69/69 EditMode, job `b22fa2269226414e8952013a3b2870c6`.
- `CommanderPhase4B2-focused-playmode-results.json`: 3/3 PlayMode, job `427f1ec3d1fa4c139b75a986c01cec43`.

Coverage includes strict metadata rejection, four objective approval/resource gates, age restrictions, max/queued population, unfinished/missing production, housing-cost boundary, identity allocation/reuse/overflow, stale approval, replay, all source precedence paths, pure interpretation/approval, routing, exact confirmation, cancellation/timeouts and stale UI publication.

Independent review findings were reproduced before fixes. The reviewer rechecked all five final findings and reported no remaining concrete defect in its scoped boundary review. See `CommanderPhase4B2-review.md`.

Full regression, verified from every saved per-test result:

| Suite | Passed | Failed / skipped | Duration | Job |
| --- | ---: | --- | ---: | --- |
| EditMode | 503/503 | 0 / 0 | 113.057 s | `7d6c5ab86f1a4ca299987ff305596f08` |
| PlayMode | 52/52 | 0 / 0 | 26.700 s | `a50e77e21dd04cecae3fd4c6415db011` |

Detailed evidence: `CommanderPhase4B2-editmode-results.json` and `CommanderPhase4B2-playmode-results.json`. EditMode includes 258 Phase3, 26 Phase4A/4A1, 60 Phase4B1 and 69 Phase4B2 cases; PlayMode includes 46 Phase3, two Phase4A and three Phase4B2 cases, plus other existing tests. All 19 required names have passing evidence. A final full refresh returned the Editor idle and ready, with no compilation/domain-reload blockers or error-console entries.

A prior full EditMode attempt lost its handle after an Editor restart; it is not counted as completed evidence. Compiler validation also checked editor logs and assembly freshness because an earlier MCP console query missed a compilation error and the runner used a stale assembly.

Final static audit (`CommanderPhase4B2-audit.json`): 203 baseline production files, 189 unchanged, 14 permitted changes, zero missing files and seven new Commander production files. Frozen simulation/command/network/execution files are unchanged. Both parser/DTO changes were reversed in memory and reproduced their original baseline hashes, proving tactical alias changes visible against Git HEAD were inherited, not Phase4B2 edits. Exact forbidden Phase4B dependencies and provider execution-service references: zero. Credential-shaped matches: zero; `.env` remains untracked and ignored.

Routine final test monitoring and static audit were delegated to lightweight sub-agents as requested. The primary agent inspected saved per-test results, owns the implementation decisions and acceptance, and preserved the working branch `unit_models_and_voice_control`. No production commit, push or merge was performed; the earlier approved-spec-only commit remains separate.

## F. Runtime evidence

Offline PlayMode tests use the real chat, strategic planner, goal manager and simulation ticks:

1. Preview has zero plans, goals and queued commands. Clicking **Approve strategy** creates a Normal plan through the shared pipeline; existing execution builds Stables, trains at least six knights, completes the plan and releases reservations. Subsequent tactical chat trains ten spearmen.
2. An emergency defense rejects a normal attack. Explicit confirmation replaces the AI plan once; a later direct-player plan resists replacement by confirmed AI.
3. Destroying the chat cancels pending translation. A late uncooperative provider response produces no plan, goal or command.

These are deterministic offline integration results, not claims of a new live Gemini request or external multiplayer match.

## G. Remaining limitations

- Only the existing four strategic objectives and recognized request forms are supported. Unknown/mixed text rejects safely; no arbitrary strategy generator or autonomous loop was added.
- Approval is snapshot feasibility, not a guarantee of future completion. Placement/path availability and subsequent world changes remain validated by existing execution; plans can wait or fail safely.
- Identity ownership is per Commander session. Concurrent allocation is unique; determinism means the same ordered game-side request sequence, not deterministic thread scheduling.
- Ordinary recommendation approval and explicit command confirmation are separate user actions. Typed LLM text cannot itself confer player authority.
- No live paid-provider or real network-match verification was added in this phase.

Final verdict: **READY FOR PHASE 4C**.
