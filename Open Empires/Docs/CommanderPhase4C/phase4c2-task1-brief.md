# Commander Phase 4C.2 Task 1 brief

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans task-by-task. Implementation is gated on recorded Phase4C.1 completion.

**Goal:** Explain actual strategic outcomes and observed plan progress without making decisions or changing gameplay.

**Architecture:** The game-owned chat host projects recorded outcomes and current detached plan snapshots into immutable explanation values. A deterministic service renders those values offline. Explanation questions enter a read-only conversation route before pending-state clearing.

**Tech Stack:** Existing Unity C#, NUnit/Unity Test Framework, Unity MCP; no new packages or remote requests.

**Spec:** `Docs/superpowers/specs/2026-09-15-commander-phase4c2-design.md` and the overall Phase4C spec. Authoritative user brief: attachment `691a2ccc-7545-452f-934d-6f5278f532ce/pasted-text-1.txt`.

## Global Constraints

- No bypass of StrategicApprovalLayer, StrategicDecisionPolicy, or StrategicPlanner.
- No direct commands, simulation access from providers, or autonomous uncontrolled behavior.
- Explanation service and value types receive no simulation, pipeline, planner, intent, decision-record graph, command, or delegate references.
- No hidden information, future prediction, fabricated reasons, or resource stock labelled income.
- Preserve original public APIs, existing tactical/strategic behavior, memory reset, and exact pending approval identity.
- No commits, pushes, merges, branch switches, packages, credentials, or unrelated changes.
- Sol owns production integration/nontrivial tests; Luna runs routine regression/audit/artifact tasks; Astra owns architecture/security/review. One production writer and one Unity runner owner; no worker-spawned agents.

## Task 1: Integrated deterministic explanations

**Agent:** Sol 5.6 high. **Output:** explanation values/service and working chat queries, focused RED/GREEN, report. **Gate:** independent scoped review plus full regression and boundary audit from Task2.

**Files:**

- Create `Assets/Scripts/AI/Commander/Phase4C/ExplanationContext.cs`: enums and immutable copied outcome/plan values.
- Create `Assets/Scripts/AI/Commander/Phase4C/ExplanationResult.cs`: bounded immutable output.
- Create `Assets/Scripts/AI/Commander/Phase4C/CommanderExplanationService.cs`: deterministic rendering only.
- Create `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Explanations.cs`: game-owned projection, query matching, and explanation lifecycle.
- Modify `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`: make class partial; hook query before pending-state mutation, event projection, reset, and synchronous submission provenance. Do not refactor other UI code.
- Create `Assets/Tests/EditMode/CommanderPhase4C2Tests.cs` and `Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs`.
- Evidence: `Docs/CommanderPhase4C/phase4c2-task1-report.md`, `phase4c2-task1-before/`, phase-specific detailed result files.

**Interfaces and exact behavior:**

- Consume existing `ConversationState.PlayerId`, `ConversationState.Snapshot()`, `CommanderMemory.RecordExplanation(string)`, and chat `ResetConversation()` lifecycle. Memory stores only the bounded rendered summary.
- `ExplanationOutcome`: `NoDecision`, `Rejected`, `TransitionRefused`, `PlannerRejected`, `PlanCreated`, `SelectionNotSubmitted`.
- `CommanderExplanationQuery`: `LastDecision`, `LastRejection`, `AttackReason`, `CurrentPlan`.
- `ExplanationPlanState`: immutable primitive plan ID, plan type, status, current milestone, milestone status and reason strings; no plan reference. Bound each string to 512 characters.
- `ExplanationContext(int playerId, int? decisionId = null, int? decisionTick = null, ExplanationOutcome outcome = ExplanationOutcome.NoDecision, string reason = null, string requestedObjective = null, int? acceptedPlanId = null, string acceptedPlanType = null, int? currentSnapshotTick = null, IReadOnlyList<ExplanationPlanState> currentPlans = null)`: immutable properties corresponding to each constructor argument. Maximum 32 current plans; each string max512. Nonnegative owner, no negative IDs/ticks. Unknown objective stays empty. Constructor copies collections; caller mutation cannot change an instance. `new ExplanationContext(playerId)` provides empty evidence.
- `ExplanationResult`: immutable display text (max8192) and outcome; return explicit unavailable text for absent evidence. Output uses invariant formatting and deterministic plan-ID ordering.
- `CommanderExplanationService.Explain(ExplanationContext context, CommanderExplanationQuery query = CommanderExplanationQuery.LastDecision)`: pure value-to-value method. Null context rejected with ArgumentNullException; invalid query enum rejected. No callbacks or external I/O.
- Chat exposes read-only `LatestExplanation` for validation; its stored explanation source is separate from the existing mutable diagnostic `LatestStrategicDecision` property.

### Steps

- [ ] Read the spec, these requirements, TDD guidance, and Unity skill. Confirm Phase4C.1 gate and fresh idle editor before production changes. Preserve full current before-content of each existing touched file; use the final 4C.1 suite pair as the pre-edit baseline without rerunning it.
- [ ] Add minimal compilable value API skeletons and focused failing tests. Demonstrate assertion-level RED, not only missing-type/compiler failure. Use this concrete reason-fidelity case with the final constructor's named arguments:

```csharp
var context = new ExplanationContext(playerId: 0, decisionId: 17,
    decisionTick: 450, outcome: ExplanationOutcome.Rejected,
    reason: "Insufficient available gold.", requestedObjective: "AttackPreparation");
var result = new CommanderExplanationService().Explain(context);
Assert.That(result.DisplayText, Does.Contain("Insufficient available gold."));
Assert.That(result.DisplayText, Does.Contain("450"));
Assert.That(result.DisplayText, Does.Not.Contain("gold income"));
```

- [ ] Implement immutable constructors and rendering. Required named tests: `Explanation_MatchesDecisionReason`, `ExplanationCannotModifyIntent`, `RejectedPlanHasReason`. The mutation test must exercise actual host projection and prove original intent/status, decision history, plan/milestone state, reservation and goal/command counts unchanged; type inspection is supplementary.
- [ ] Cover all six outcomes with hand-specified expected meaning. `Rejected` uses the recorded decision/outcome reason, `TransitionRefused` only selected-but-blocked, `PlannerRejected` only actual failed submission, `PlanCreated` only CreatedPlan. Selection alone never becomes approval. Empty reasons explicitly say no recorded reason is available. Label decision tick as historical.
- [ ] Wire `OnStrategicEvaluationCompleted` to a host-only projection method. Copy primitives immediately. Determine outcomes in order: CreatedPlan; actual failed submission; explicit Rejected; NoDecision; selected+notallowed; selected not submitted. Do not keep source-record/intent/plan references in explanation values. A NoDecision event must not erase a previously meaningful decision; an initially empty source may describe NoDecision.
- [ ] Capture requested objective/identity immediately before the chat's synchronous approval/pipeline call and clear provenance in `finally`. The event handler may use that exact active submission's values; otherwise use the selected/submitted intent if available. A rejection with no such provenance has unknown objective. Never infer the rejected objective from `ActivePlanType` or prose.

```csharp
// These two nullable fields belong only to the game-owned chat partial.
pendingExplanationIntentId = intent.IntentId;
pendingExplanationObjective = intent.ObjectiveType.ToString();
try
{
    var approval = new StrategicApprovalLayer().Evaluate(
        strategicPipeline.CaptureContext(), intent, intent.Source);
    LatestStrategicDecision = strategicPipeline.EvaluateApprovedIntentNow(approval);
}
finally
{
    pendingExplanationIntentId = null;
    pendingExplanationObjective = null;
}
```

- [ ] Add whole-form query matching in the new chat partial before any clear-pending/latest-decision/submitting path. Normalize invariant lowercase and whitespace; permit one optional trailing `?` or `.`. Match only `why are we not attacking`, `why was that rejected`, `explain last decision`, `what is the plan doing`. Appended instructions are not explanation queries and remain subject to existing rejection routing.
- [ ] Render `LastRejection` honestly when latest meaningful outcome is not rejection. Render `AttackReason` as an attack rejection only for known AttackPreparation with a rejection/refusal/planner-failure outcome; otherwise state no attributable attack rejection and optionally identify the known latest outcome. Never invent emergency/gold/income reasons.
- [ ] For CurrentPlan, game-owned host calls only existing `strategicPipeline.CaptureContext()` and copies `ActivePlans` primitive properties into fresh ExplanationPlanState values. Service sees no pipeline/context-builder. Distinguish current snapshot tick from historical decision tick. A null current snapshot tick means current state is unavailable, not no active plans; an empty list with a valid snapshot says no active plan observed. Neither asserts a historical plan completed. Current milestone/status/reason are observed, not predicted. When the displayed list reaches 32, label it as showing up to 32 plans rather than claiming it is exhaustive.
- [ ] Explanation queries append local transcript plus one bounded explanation memory summary. They neither clear nor replace pending intent, start a provider request, invoke approval/policy/planner execution, nor overwrite latest meaningful decision. Offline/provider-unavailable use remains functional.
- [ ] Reset copied source/LatestExplanation during clear memory, Initialize, different strategic pipeline/session replacement, owner change and destruction. Do not rehydrate from old game decision history. Reuse the established lifecycle hooks; preserve the game history itself.
- [ ] Add PlayMode tests through real CommanderChatUI: actual rejected attack approval followed by question matches reason; rejected attack while unrelated defense exists is attributed to attack; pending preview identity survives all four questions; actual plan advances via existing execution and CurrentPlan matches a fresh detached snapshot without query-side advancement. Clear memory then ask again yields unavailable explanation despite retained game decision history. Track all gameplay counts before/after questions.
- [ ] Cover copied input mutation, bounded strings/collections, invariant culture, unknown objective, empty history, hostile appended text, and queries with unconfigured/failing remote provider (no remote call). Use existing runtime fixture construction patterns rather than modifying production execution to make tests pass.
- [ ] Run focused EditMode and PlayMode category `CommanderPhase4C2`, save full per-test results and source hashes, check compiler errors. Write report with final interfaces, RED/GREEN evidence, exact changed files, runtime proof, and any concerns; freeze source and hand runner ownership back. Do not run full suites repeatedly during edits.
