# Commander Phase 4C.3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Implementation is gated on Phase4C.2 completion.

**Goal:** Supply honest, deterministic, detached strategic insights to the existing advisor without expanding gameplay authority.

**Architecture:** Existing game-owned context projection supplies owned counts and copied plan milestones. A value-only insights builder derives sorted integer aggregates; explicit provider serialization includes only the whitelisted values. No time-series, simulation reader in AI, or prediction is added.

**Tech Stack:** Existing Unity C#, Newtonsoft.Json, NUnit/Unity Test Framework, Unity MCP; no new dependencies.

**Spec:** `Docs/superpowers/specs/2026-09-15-commander-phase4c3-design.md` and overall Phase4C spec; authoritative attachment `691a2ccc-7545-452f-934d-6f5278f532ce/pasted-text-1.txt`.

## Global Constraints

- No bypass of StrategicApprovalLayer, StrategicDecisionPolicy, or StrategicPlanner.
- No direct commands, simulation access from providers, or autonomous uncontrolled behavior.
- No hidden information, future prediction, cheating information, or direct simulation references.
- Preserve existing constructor/method arities, existing output DTO allowlists and identity/source/approval rules.
- Income trends are unavailable; worker activity is not throughput, efficiency, or proof other workers are idle.
- No commits, pushes, merges, branch switches, packages, settings, credentials, or unrelated changes.
- Sol implements integration/nontrivial tests; Luna handles full regression/static/evidence; Astra owns architecture/security/review. One writer and one Unity runner owner; no worker-spawned agents.

## Task 1: Owned-state insights and actual provider integration

**Agent:** Sol 5.6 high. **Output:** detached insights with provider payload and focused RED/GREEN. **Gate:** Task2 review/full regression/security.

**Files:**

- Create `Assets/Scripts/AI/Commander/Phase4C/StrategicContextInsights.cs`: immutable insight and entry values.
- Create `Assets/Scripts/AI/Commander/Phase4C/StrategicContextInsightsBuilder.cs`: value-only aggregation.
- Modify `Assets/Scripts/AI/Commander/Strategic/StrategicContext.cs`: optional Insights property and copied milestone counts in StrategicPlanState; preserve old constructor signatures with delegating overloads.
- Modify `Assets/Scripts/AI/Commander/Strategic/StrategicContextBuilder.cs`: compute/project observed values and attach insights.
- Modify `Assets/Scripts/AI/Commander/Phase4B1/StrategicAIContextSerializer.cs`: explicit stable insight payload while preserving old fields.
- Create `Assets/Tests/EditMode/CommanderPhase4C3Tests.cs` and `Assets/Tests/PlayMode/CommanderPhase4C3PlayModeTests.cs`.
- Evidence: `Docs/CommanderPhase4C/phase4c3-task1-report.md`, `phase4c3-task1-before/`, full per-test result artifacts.

**Value contract:**

- `StrategicContext.Insights` is nullable and get-only. Existing constructor delegates to a distinct overload with null insights; normal game-owned builder explicitly supplies a value. No mutable setters or lazy callbacks.
- `StrategicPlanState.CompletedMilestoneCount` and `TotalMilestoneCount` are get-only copies taken in its unchanged constructor. Count only `StrategicMilestoneStatus.Completed`, not current index or failed/cancelled/skipped milestones. Current status/name remain existing copied fields.
- `StrategicContextInsightsBuilder.Build(int totalWorkers, IReadOnlyList<StrategicWorkerAllocationState> workerAllocation, IReadOnlyList<StrategicMilitaryState> military, IReadOnlyList<StrategicProductionState> production, IReadOnlyList<StrategicPlanState> plans)` receives detached values only and returns an immutable `StrategicContextInsights`.
- Insights exposes `IncomeTrendAvailable` false, worker total/gathering and `WorkerActivityAvailable` plus `WorkerActivityBasisPoints`, and copied read-only `ArmyComposition`, `ProductionPressure`, `PlanProgress` lists. Never retain caller lists or old entry references.
- Army entries: integer UnitType, OwnedCount, QueuedCount, OwnedShareBasisPoints. Exclude villagers0 and sheep5, sort by UnitType, combine duplicate types if supplied. Queued units do not enter owned shares.
- Production entries: BuildingType string, CompletedCount, UnderConstructionCount, ActiveQueueCount, QueuedUnitCount, IdleCompletedCount. Sort ordinal building type; preserve queue counts separately. Completed equals total minus construction, idle equals existing AvailableCapacity. No inferred resource-shortfall labels.
- Plan entries: copied plan ID/type/status, CompletedMilestoneCount, TotalMilestoneCount, CurrentMilestone and MilestoneStatus. Sort by plan ID. These are milestone-stage counts, not time-weighted progress or predictions.
- Validate null input collections and negative counts; use long intermediates for sums/ratios, reject values outside int range rather than wrap. Copy strings as values. Stable invariant rendering.

### Steps

- [ ] Confirm Phase4C.2 gate and latest full clean pair; use that as pre-edit baseline. Read spec/TDD/Unity skill and only relevant current files. Snapshot full before-content/hashes for every existing file touched; create incremental report before edits. No repeated baseline suite.
- [ ] Add focused failing tests with minimal compilable new API skeleton. Required names: `ContextRemainsFogSafe`, `ContextSerializationDeterministic`. Demonstrate assertion-level RED and preserve actual job/output.
- [ ] Implement detached immutable values and exact integer arithmetic. The worker activity rule is:

```csharp
bool available = totalWorkers > 0;
int basisPoints = !available ? 0
    : (int)Math.Min(10000L, gatheringWorkers * 10000L / totalWorkers);
```

Hand-checked test expectations: zero workers => unavailable/0; one gathering of three =>3333; all three =>10000. Other workers must not be labelled idle. Three owned archers and one knight =>7500/2500 owned shares even with two queued knights. No owned army => shares0, queued counts still visible.
- [ ] Extend existing plan projection, without storing live plan references:

```csharp
TotalMilestoneCount = plan.Milestones.Count;
int completed = 0;
for (int i = 0; i < plan.Milestones.Count; i++)
    if (plan.Milestones[i].Status == StrategicMilestoneStatus.Completed) completed++;
CompletedMilestoneCount = completed;
```

Use detached active-plan values in the insights builder, not `StrategicPlan`/`StrategicPlanner`. Retain existing terminal-plan exclusion; no active plans does not prove historical plan completion.
- [ ] Attach built insights in StrategicContextBuilder using its already-built worker/military/production/active-plan lists. Do not add a new enemy/simulation reader. Complete producer arithmetic tests: three total, one construction, one active queue, one available =>completed2/construction1/active1/idle1; construction-only =>completed0/idle0; multiple queued units remain distinct from queue count.
- [ ] Extend explicit StrategicAIContextSerializer with optional `insights` object, omitted when Insights is null. Fields: `incomeTrendAvailable`, `workerActivity` (available,totalWorkers,gatheringWorkers,basisPoints), `armyComposition`, `productionPressure`, `planProgress`, with only the contract's primitive entry fields. Never serialize arbitrary objects using reflection for the new payload. Keep all existing fields and output DTO/schema unchanged.
- [ ] Ensure deterministic sorting in the insight builder and explicit serializer; canonicalize any existing emitted array/map whose ordering causes identical input collections to produce different JSON. Do not reorder the semantics of gameplay. Verify full actual GeminiStrategicAIProvider.BuildRequestJson bytes for identical detached input under different collection insertion orders and non-English cultures, with consistent owner/tick/intent identity.
- [ ] `ContextRemainsFogSafe`: actual fixtures differing only in hidden and explored-only enemy units/buildings/resources must emit identical allowed JSON/insights. Visible enemy changes may alter pre-existing threat aggregates only. Use different enemy counts/locations, not only type inspection or absence of one marker.
- [ ] Prove snapshots remain detached after original source lists/plan milestones change. Assert new insight types have no fields of simulation/plan/planner/delegate/command types. Check unavailable income explicitly; never derive it from resource stock differences or spending.
- [ ] Capture an actual provider request through chat with a recording interpreter/HTTP transport; verify non-null insights reach the serialized request and no plan/goal/command occurs before explicit approval. Do not modify objective validation, default objective, source, priority, or identity based on new metrics.
- [ ] PlayMode: allocate own workers, establish actual queued production and advance an actual plan through existing execution. Capture before/after contexts, match arithmetic and completed-milestone counts to observed state. Capture itself leaves goal/command/reservation/decision-history counts and plan/milestone statuses unchanged. No query advances simulation; only explicit test execution steps do.
- [ ] Run focused EditMode/PlayMode category `CommanderPhase4C3`, preserving full per-test evidence/source hashes/compiler state. Self-review owned-only boundary, arithmetic, sorting, copies and compatibility. Append final interfaces/files/RED-GREEN/runtime evidence to report, freeze and hand runner to Luna. No full suite after each edit.

## Task 2: Review and final context gate

**Agents:** Luna artifact/regression/audit; Sol task reviewer; Astra integration/security.

- [ ] Generate actual before/after diff and new-source package with source hashes. Independent reviewer verifies task spec/quality; no rerunning unchanged suites. Fixes return to original implementer with scoped re-review.
- [ ] Run full EditMode and PlayMode once on frozen source, covering all existing phases. Preserve full unique-test results and original job IDs. Recover stale MCP progress with same handle/log/XML evidence rather than starting overlapping jobs.
- [ ] Run boundary/key audit and verify protected command/network/execution/simulation files unchanged. Interpret game-owned projections separately from advisor/service boundaries; no credential values printed.
- [ ] Astra checks both required named tests, actual emitted payload, fog differentials, meaningful runtime changes without capture-side execution, and all review findings. Gate only when all focused/full/static/runtime evidence passes; then admit objective expansion.

## Preflight/self-review

| Pair/task | Producer/consumer check | Resolution |
| --- | --- | --- |
| Context builder -> insights builder | Owned detached aggregates only | No additional simulation reader |
| Plan -> StrategicPlanState -> insights | Completed/total copies | Existing constructor preserved; no live graph retained |
| Insights -> explicit provider serializer | Optional immutable value | Explicit whitelist and stable sorting, null omitted |
| Insight metrics -> gameplay | Advisory values only | No policy/template/executor changes |
| Task1 -> Task2 | Before snapshots, RED/GREEN, source hashes | One frozen set and full regression |
| Task1 self-consistency | Four optional aggregate groups and required tests | Income unavailable; others implemented and actually serialized |
| Task2 self-consistency | Verification only | Luna cannot edit planner/authority/execution |

Risks: false income/idle claims, queued-vs-owned denominator errors, mutable or hidden-state leaks, fake milestone percentages, missing serializer wiring, and arity regressions. Every risk has an explicit check above. Plan covers all spec sections; no unresolved placeholders. Preparation is architecture-only until the Phase4C.2 gate is recorded.
