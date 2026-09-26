# OpenEmpires AI Commander Phase 4D

## Adaptive Strategic Coordination & Plan Resilience

You are the primary implementation orchestrator for **OpenEmpires AI Commander Phase 4D**.

Phase 4C has completed its final architecture, security, runtime, regression, and external audit gates.

Phase 4D must extend Commander intelligence WITHOUT weakening any Phase 3, Phase 4A, Phase 4B, or Phase 4C authority or safety boundary.

---

# 0. MANDATORY PRE-FLIGHT — DO NOT CODE YET

Before modifying production source:

1. Inspect the entire CURRENT local working tree.
2. Identify the final Phase 4C source state.
3. Verify whether the final Phase 4C housing fix is committed or still present as working-tree changes.
4. Record the exact baseline:

   * branch;
   * HEAD SHA;
   * git status;
   * modified/untracked files;
   * current Phase 4C source manifest;
   * current protected-boundary hashes.
5. Confirm the final Phase 4C runtime/test evidence:

   * full EditMode: 571/571;
   * full PlayMode: 87/87;
   * focused Phase 4C.4 tests;
   * dynamic housing proof.
6. Do NOT discard, overwrite, revert, clean, stash, or silently absorb user work.
7. Do NOT begin Phase 4D on top of an ambiguous baseline.

If the final Phase 4C fix is uncommitted:

* treat the CURRENT live source as the Phase 4C baseline;
* record hashes before Phase 4D changes;
* do not revert it;
* do not mix Phase 4C cleanup with Phase 4D features.

Create:

`Docs/CommanderPhase4D/phase4d-source-baseline.json`

and:

`Docs/CommanderPhase4D/progress.md`

The baseline must be machine-verifiable.

---

# 1. ORCHESTRATION RULES

The root/highest-reasoning agent is the **architect and gate owner**.

Do NOT have one agent perform the entire project monolithically.

Use agents according to complexity.

## Root / Highest-Reasoning Agent

Own:

* architecture;
* dependency order;
* authority decisions;
* integration design;
* risk analysis;
* reviewing delegated work;
* resolving conflicting findings;
* final release decision.

The root agent SHOULD NOT waste substantial context/time on:

* repetitive source searches;
* formatting documentation;
* mechanical hash generation;
* routine test enumeration.

---

## Sol-class implementation agents

Use for:

* production implementation;
* lifecycle logic;
* planner integration;
* strategic recovery logic;
* concurrency/version safety;
* non-trivial EditMode/PlayMode tests;
* scoped fixes after review.

Only **one production writer at a time**.

Never allow two agents to modify the same subsystem concurrently.

If an implementation agent is interrupted, resume the SAME worker whenever possible rather than spawning a second overlapping implementation.

---

## Luna-class agents

Use for:

* code search;
* static analysis;
* evidence packaging;
* requirements matrices;
* test inventory;
* documentation reconciliation;
* source/hash audits;
* low-risk repetitive verification.

Luna must NOT independently modify:

* planner authority;
* execution paths;
* simulation authority;
* approval policy;
* network authority.

---

# 2. UNITY RUNNER OWNERSHIP

At any moment, exactly ONE agent owns Unity test execution.

Do not launch overlapping full suites.

Do not rerun expensive completed tests merely because an agent context was interrupted.

A lost test handle is not a PASS.

Recover the authoritative job/XML where possible.

Every claimed result must correspond to actual current source.

---

# 3. PHASE 4D PRIMARY OBJECTIVE

Build:

# ADAPTIVE STRATEGIC COORDINATION

The Commander should be able to understand and communicate the lifecycle and health of an approved strategic plan, safely recover from temporary blockers, and allow the PLAYER to control or revise strategy.

Phase 4D should make strategic behavior feel resilient and understandable.

It must NOT turn the Commander into an autonomous executor.

---

# 4. PHASE 4D CORE ARCHITECTURE

Preserve this authority chain:

Player / AI Provider
↓
Validated input / detached recommendation
↓
StrategicApprovalLayer
↓
StrategicDecisionPolicy
↓
StrategicPlanner
↓
CommanderGoalManager
↓
Existing deterministic RTS execution

The LLM may:

* interpret player language;
* explain plan state;
* summarize blockers;
* suggest possible adaptations;
* format detached recommendations.

The LLM may NOT:

* pause a plan by itself;
* resume a plan by itself;
* cancel a plan by itself;
* replace a plan by itself;
* increase a plan budget;
* add goals;
* issue commands;
* mutate simulation;
* choose an objective and execute it automatically;
* bypass player approval;
* bypass StrategicApprovalLayer;
* bypass StrategicDecisionPolicy;
* bypass StrategicPlanner.

---

# 5. STRICT PHASE 4D NON-GOALS

Do NOT introduce:

* autonomous strategy switching;
* automatic approval;
* background LLM decision polling;
* direct LLM-to-planner references;
* direct provider-to-planner references;
* direct LLM-to-GameSimulation access;
* direct LLM-to-CommandBuffer access;
* permanent player profiling;
* cloud memory;
* embeddings;
* cross-match hidden memory;
* hidden enemy information;
* future-state prediction presented as fact;
* non-deterministic gameplay logic;
* wall-clock-dependent simulation decisions;
* network authority redesign;
* package upgrades unrelated to Phase 4D;
* authentication changes;
* credential handling changes;
* API-key movement;
* test log suppression;
* unsupported strategic objectives.

`TechnologyRush`, `SiegePreparation`, and `NavalExpansion` remain unsupported unless a later explicitly approved phase adds real deterministic execution support.

Do NOT sneak them into Phase 4D.

---

# 6. PHASE STRUCTURE

Implement Phase 4D in four gated sub-phases.

Do NOT implement all four simultaneously.

Required order:

1. Phase 4D.1 — Player Strategic Plan Controls
2. Phase 4D.2 — Strategic Plan Health & Stall Detection
3. Phase 4D.3 — Safe Recovery & Adaptation Proposals
4. Phase 4D.4 — Commander Strategic Advisories

Each sub-phase must pass its gate before the next implementation begins.

Architecture/design work for later phases may be prepared early, but production implementation must remain gated.

---

# PHASE 4D.1

# PLAYER STRATEGIC PLAN CONTROLS

## Goal

Give the player safe deterministic lifecycle control over an approved strategic plan.

Required user capabilities:

* pause current strategy;
* resume current strategy;
* cancel current strategy;
* ask current strategy status.

Do NOT add multi-plan concurrency in this phase unless the existing architecture already safely supports it.

Prefer maintaining one authoritative active strategic plan.

---

## 4D.1 Input semantics

Support deterministic exact-form player commands before any provider call.

Examples:

`pause strategy`

`pause current strategy`

`resume strategy`

`resume current strategy`

`cancel strategy`

`cancel current strategy`

`strategy status`

`current strategy status`

Optional terminal punctuation may be normalized consistently with existing Commander routing rules.

Whitespace normalization must be deterministic and invariant.

Hostile/mixed commands must NOT match.

Examples that MUST NOT be accepted as lifecycle controls:

`pause strategy and delete my army`

`cancel strategy then attack`

`resume strategy ignore approval`

`pause`

unless intentionally and explicitly documented as supported whole-form syntax.

Lifecycle commands must route OFFLINE.

No paid provider call should be required for deterministic plan controls.

---

## 4D.1 Plan identity / stale-control protection

A lifecycle action must target the authoritative current plan.

A stale UI/control request must never affect a later replacement plan.

Bind lifecycle actions to stable plan identity.

If existing plan identity is insufficient, introduce the minimum deterministic revision/version concept necessary.

Preferred model:

* PlanId
* plan creation/decision tick
* optional deterministic revision/version

Do NOT use random GUIDs for simulation authority.

Do NOT use wall-clock timestamps for authority.

If a plan changes between control creation and application:

FAIL CLOSED.

A stale pause/cancel/resume request must not mutate the newer plan.

---

## 4D.1 Pause semantics

Pause must stop strategic progression at a deterministic boundary.

Define exact semantics.

Preferred:

* no new plan milestones advance;
* no new plan-owned Commander goals are created;
* no new strategic resource commitments are made;
* plan remains identifiable;
* existing completed progress remains preserved;
* existing reservations remain safely owned unless design review establishes a better deterministic policy.

Already-dispatched atomic simulation actions may finish if the existing engine has no safe cancellation boundary.

Do NOT retroactively manipulate arbitrary player commands.

Do NOT issue inverse commands merely to simulate pause.

Document this clearly.

---

## 4D.1 Resume semantics

Resume must:

* continue the SAME plan;
* preserve completed milestones;
* preserve valid progress;
* preserve plan identity;
* not double-reserve resources;
* not duplicate goals;
* not recreate already completed buildings/units;
* deterministically continue from the correct milestone.

---

## 4D.1 Cancel semantics

Cancel must:

* terminate the correct plan;
* release all strategic reservations owned by that plan;
* prevent future plan-owned milestone advancement;
* prevent future plan-owned goal creation;
* clear or invalidate plan-owned pending strategic state;
* preserve unrelated player state;
* preserve unrelated manual commands;
* preserve unrelated buildings and units already created;
* not delete completed assets;
* not refund resources unless existing deterministic game semantics already do so.

If previously issued construction/training is already funded and executing, use existing cancellation semantics.

Do NOT invent free refunds.

---

## 4D.1 Required value types

Before creating new classes, inspect existing lifecycle/status APIs.

Reuse existing state wherever possible.

Only if no suitable equivalent exists, consider minimal types such as:

`StrategicPlanControlType`

values:

* Pause
* Resume
* Cancel
* Status

`StrategicPlanControlRequest`

detached fields only:

* playerId;
* planId;
* control type;
* observed revision/tick.

`StrategicPlanControlResult`

possible statuses:

* Applied
* NoActivePlan
* AlreadyPaused
* AlreadyRunning
* AlreadyCompleted
* AlreadyCancelled
* StalePlan
* Unauthorized
* Rejected

Do NOT duplicate existing planner/result state unnecessarily.

---

## 4D.1 Tests

At minimum:

`PauseStrategy_StopsFutureStrategicProgress`

`ResumeStrategy_ContinuesSamePlan`

`CancelStrategy_ReleasesReservations`

`CancelStrategy_DoesNotDeleteCompletedAssets`

`CancelStrategy_DoesNotAffectUnrelatedPlayerCommands`

`StalePauseRequest_CannotPauseReplacementPlan`

`StaleCancelRequest_CannotCancelReplacementPlan`

`LifecycleCommands_DoNotCallProvider`

`LifecycleCommands_AreWholeFormOnly`

`LifecycleCommands_CannotBypassOwnership`

`Reset_ClearsLifecycleState`

`SameLifecycleStateProducesSameResult`

Add PlayMode proof using an actual new Phase 4C objective.

Recommended runtime:

RangedReinforcement:

approve
→ partially progress
→ pause
→ tick simulation
→ verify strategic progress does not advance
→ resume
→ verify completion

Then:

DefensiveTurtle:

approve
→ progress
→ cancel
→ verify reservation release and no future plan-driven progress.

---

# PHASE 4D.2

# STRATEGIC PLAN HEALTH & STALL DETECTION

## Goal

Represent WHY an approved strategy is progressing, waiting, blocked, or unable to continue.

This is observation.

NOT authority.

---

## 4D.2 Plan health model

Create or extend an immutable detached plan-health representation.

Suggested conceptual states:

`Healthy`

`Paused`

`WaitingForResources`

`WaitingForPopulation`

`WaitingForPrerequisite`

`WaitingForProductionCapacity`

`WaitingForWorker`

`WaitingForReservation`

`TemporarilyBlocked`

`NeedsPlayerDecision`

`Completed`

`Cancelled`

`Failed`

Do not create redundant states if existing planner status already expresses them safely.

Prefer adapting existing structured status rather than parsing strings.

---

## Required detached value

If no equivalent exists:

`StrategicPlanHealthSnapshot`

May include only copied primitives/value data such as:

* playerId;
* planId;
* plan type;
* current milestone;
* milestone state;
* plan status;
* observed simulation tick;
* structured blocker type;
* bounded blocker reason;
* current reservation summary;
* required remaining resource summary;
* relevant owned production availability;
* population / capacity values;
* paused/running state;
* progress counters.

Must NOT retain:

* StrategicPlan reference;
* StrategicPlanner reference;
* CommanderGoal reference;
* GameSimulation reference;
* BuildingData;
* UnitData;
* MonoBehaviour;
* callbacks;
* delegates;
* command instances.

---

## Stall detection rules

All stall detection must be deterministic.

Do NOT use:

* DateTime;
* real-world seconds;
* frame count;
* coroutine elapsed time;
* random numbers.

Use simulation tick/progress state.

A plan must not be called “stalled” merely because it is legitimately gathering resources.

Differentiate:

* expected waiting;
* lack of progress;
* impossible continuation;
* explicit pause.

If a threshold is needed:

* derive it from existing simulation tick conventions;
* make it a named deterministic constant/config;
* document it;
* test boundary values.

Never infer blocker type by parsing human-readable explanation text.

Use structured source state.

---

## Fog safety

Plan health must use:

* owned state;
* approved plan state;
* known resource state;
* allowed visible information only.

Do not diagnose:

`Enemy cavalry blocking us`

unless that enemy information is actually visible and legitimately available through existing fog-safe context.

Prefer internal plan blockers over tactical speculation.

---

## Required tests

`PlanHealth_IsDeterministic`

`PlanHealth_DoesNotMutateSimulation`

`PlanHealth_DoesNotAdvancePlan`

`PlanHealth_DoesNotCallProvider`

`SameStateProducesSameHealthSnapshot`

`ResourceWaiting_IsNotMisclassifiedAsFailure`

`PopulationWaiting_IsStructured`

`PausedPlan_ReportsPaused`

`CompletedPlan_ReportsCompleted`

`CancelledPlan_ReportsCancelled`

`HealthSnapshot_DoesNotRetainSimulationObjects`

`PlanHealth_RemainsFogSafe`

`Reset_ClearsPlanHealthProjection`

`OldPlanHealth_DoesNotDescribeReplacementPlan`

Add mutation-isolation tests:

capture snapshot
→ mutate original plan/simulation-owned object
→ prove captured detached snapshot does not change.

---

# PHASE 4D.3

# SAFE RECOVERY & ADAPTATION PROPOSALS

## Goal

Allow the deterministic system to distinguish between:

1. temporary blockers it can safely recover from INSIDE the already approved strategy;

and

2. material strategic changes that require PLAYER approval.

---

# CRITICAL RULE: RECOVERY IS NOT REPLANNING AUTHORITY

Automatic recovery may occur only inside the existing approved intent envelope.

Safe automatic recovery MAY include existing deterministic behavior such as:

* wait for resources;
* continue gathering;
* construct ordinary population prerequisites;
* wait for queue capacity;
* resume an already funded foundation;
* continue after production becomes available;
* retry a temporarily blocked milestone;
* reassign workers within already approved template constraints;
* continue after the player resumes a paused plan.

Automatic recovery MUST NOT:

* change objective;
* add a new strategic objective;
* increase target army size;
* increase required building target;
* add attack/move behavior;
* increase strategic spending beyond approved policy/budget rules;
* silently lower player-approved targets;
* switch RangedReinforcement into DefensiveTurtle;
* switch DefensiveTurtle into an attack plan;
* bypass approval.

---

## Material adaptation

If continuing requires a strategic change, create a DETACHED proposal.

Do not apply it.

Suggested value if no equivalent exists:

`StrategicAdaptationProposal`

Fields may include:

* playerId;
* sourcePlanId;
* sourcePlanRevision/tick;
* current objective;
* proposed objective;
* structured reason;
* bounded explanation;
* old canonical budget;
* proposed canonical budget;
* old targets;
* proposed targets;
* requiresPlayerApproval = true.

No simulation references.

No commands.

No live planner references.

---

## Material-change examples

These require new approval:

* change objective type;
* raise unit target;
* raise structure target;
* introduce new required structure not part of approved template;
* exceed the approved strategic budget envelope;
* replace the current strategy;
* materially change strategic priority;
* cancel current plan in order to start a different one.

---

## Approval path

Adaptation proposal:

detached proposal
↓
shown to player
↓
player approves
↓
validated StrategicIntent
↓
StrategicApprovalLayer
↓
StrategicDecisionPolicy
↓
StrategicPlanner

Do NOT add:

proposal
↓
StrategicPlanner

That is forbidden.

---

## Version/staleness safety

A proposal is valid only for the source plan state it was created from.

If the plan:

* completed;
* was cancelled;
* was paused/resumed in a way that invalidates assumptions;
* was replaced;
* changed revision;
* no longer has the blocker;

then old proposal application must FAIL CLOSED.

No resurrection of stale plans.

---

## Player-initiated strategy change

Support a clean flow where:

active RangedReinforcement
↓
player asks for DefensiveTurtle
↓
new recommendation becomes pending
↓
OLD plan remains authoritative until approval
↓
player approves
↓
existing decision/policy authority determines replacement/supersession
↓
old plan releases appropriate reservations
↓
new plan begins

The LLM must not stop the old plan merely because it suggested a replacement.

---

## Required tests

`TemporaryResourceBlocker_RecoversWithoutNewApproval`

`PopulationPrerequisite_RecoversWithoutNewApproval`

`MaterialObjectiveChange_RequiresPlayerApproval`

`AdaptationProposal_DoesNotModifyPlan`

`AdaptationProposal_DoesNotCallPlanner`

`AdaptationProposal_IsDetached`

`AdaptationProposal_IsDeterministic`

`StaleAdaptationProposal_IsRejected`

`CompletedPlanRejectsOldAdaptationProposal`

`CancelledPlanRejectsOldAdaptationProposal`

`ReplacementPlanRejectsOldProposal`

`BudgetIncreaseRequiresApproval`

`TargetIncreaseRequiresApproval`

`AIRecommendationCannotAutoReplaceActivePlan`

`PlayerApprovedReplacementUsesExistingAuthorityPath`

`ReplacementReleasesOldReservationsCorrectly`

---

# PHASE 4D.4

# COMMANDER STRATEGIC ADVISORIES

## Goal

Let Commander proactively INFORM the player about meaningful strategic lifecycle changes.

This is NOT autonomous strategic behavior.

---

## Advisory examples

Allowed:

`Ranged reinforcement is paused.`

`Ranged reinforcement resumed.`

`The current plan is waiting for wood.`

`The population prerequisite was completed; Archer production resumed.`

`Defensive Turtle is blocked because no owned worker is available.`

`The current plan cannot continue without a material change. You can cancel it or request another strategy.`

`Ranged reinforcement completed.`

---

## Default implementation must be OFFLINE

Do NOT call a paid LLM merely to announce deterministic state.

Use structured deterministic rendering.

Existing explanation infrastructure should be reused where sensible.

If later optional LLM phrasing is desired, it must be explicitly user-triggered or separately designed.

Phase 4D should not introduce background API cost.

---

## Advisory triggers

Only emit on meaningful state transition.

Examples:

Healthy → WaitingForResources

WaitingForResources → Healthy

Running → Paused

Paused → Running

Running → NeedsPlayerDecision

Running → Completed

Running → Cancelled

Do NOT spam every tick.

---

## Advisory deduplication

Use deterministic per-plan transition identity.

Do NOT store unbounded advisory history.

Bound retained advisory information.

No cross-match persistence.

Reset on:

* new match;
* owner replacement;
* strategic system replacement;
* Commander reset;
* destruction.

---

## Cooldown / anti-spam

Prefer state-transition dedupe over time-based cooldown.

If a cooldown is still required:

use simulation ticks.

Never wall-clock time.

---

## Advisory authority

An advisory cannot:

* click approval;
* trigger control actions;
* create an intent;
* pause;
* cancel;
* resume;
* execute;
* submit planner work.

It is informational only.

---

## Required tests

`Advisory_DoesNotExecuteAnything`

`Advisory_DoesNotCallProvider`

`Advisory_EmitsOnMeaningfulTransition`

`Advisory_DoesNotRepeatEveryTick`

`Advisory_RecoveryEmitsOnce`

`Advisory_CompletionEmitsOnce`

`Advisory_ResetClearsDeduplication`

`Advisory_IsBounded`

`Advisory_IsPlanVersionSafe`

`Advisory_RemainsFogSafe`

---

# 7. HOUSE-RESERVATION LIMITATION FROM PHASE 4C

Phase 4C ended with an explicitly audited non-blocking behavior:

* feasibility quotes opportunistic House cost;
* strategic static reservation does not reserve that additional tactical House wood;
* tactical deterministic gathering/recovery can wait for and obtain the required wood.

Do NOT silently redesign this in Phase 4D unless Phase 4D runtime work demonstrates a real correctness failure.

If Phase 4D tests expose an actual deadlock or broken lifecycle related to this behavior:

1. record RED evidence;
2. isolate the root cause;
3. propose the smallest correction;
4. review authority implications before modifying reservation architecture.

Do not “clean it up” merely for symmetry.

---

# 8. RESOURCE / RESERVATION INVARIANTS

Phase 4D must protect all existing reservation guarantees.

Verify throughout:

* pause does not duplicate reservation;
* resume does not reserve again;
* cancel releases plan-owned reservations;
* stale controls cannot release a replacement plan's reservation;
* adaptation proposal holds no real resources;
* merely viewing status holds no resources;
* advisories hold no resources;
* replacement uses existing policy;
* completed plan has zero leaked strategic reservation;
* failed/cancelled plan does not leak strategic reservation.

---

# 9. ASYNC / CONCURRENCY SAFETY

Phase 4D must preserve Phase 4C stale-response protections.

Test scenarios:

1. provider request starts;
2. player cancels active plan;
3. old provider returns;
4. response must not resurrect cancelled state.

Also:

1. request A begins;
2. plan revision changes;
3. request A completes;
4. stale result cannot control current plan.

And:

1. match resets;
2. late task completes;
3. no memory, transcript authority, pending intent, adaptation proposal, lifecycle action, or advisory may leak into the new session.

Use cancellation/generation/session tokens where existing architecture already does so.

Do not create a second unrelated concurrency mechanism without need.

---

# 10. MULTIPLAYER / OWNERSHIP SAFETY

Do NOT redesign networking.

Preserve existing authority.

A player must only control plans owned by that player.

Lifecycle requests must validate owner identity.

Never trust owner/player identity from raw LLM text.

Use existing authoritative host/player identity.

Tests:

`PlayerCannotPauseOtherPlayersPlan`

`PlayerCannotCancelOtherPlayersPlan`

`PlayerCannotResumeOtherPlayersPlan`

If current Commander strategic UI is host-only, preserve that model rather than inventing new network messages.

Document limitations honestly.

---

# 11. SERIALIZATION

Any new provider-visible state must be:

* detached;
* immutable;
* bounded;
* explicitly serialized;
* deterministic;
* culture-invariant;
* stable field order where current serializers require it.

Do NOT serialize live objects.

Do NOT serialize:

* Unity references;
* planner references;
* simulation objects;
* commands;
* delegates;
* callbacks.

Unknown enum values must fail closed.

Never serialize hidden state accidentally through a debugging field.

---

# 12. MEMORY INTEGRATION

Phase 4D may store bounded informational entries such as:

* strategy paused;
* strategy resumed;
* strategy cancelled;
* blocker observed;
* blocker recovered;
* adaptation proposed;
* strategy completed.

Memory must remain non-authoritative.

A memory entry may influence conversation interpretation but must never directly mutate plan lifecycle.

Memory strings must remain bounded.

Do not store live `StrategicPlanHealthSnapshot` object graphs unless they are immutable primitive-only values and retention is explicitly justified.

Prefer concise copied summaries.

---

# 13. EXPLANATION INTEGRATION

Extend Phase 4C explanation safely.

Allow questions such as:

`why is the strategy paused?`

`why is the plan waiting?`

`what is blocking the current strategy?`

`did the strategy recover?`

`why did the plan stop?`

Answers must derive from copied structured lifecycle/health evidence.

Do NOT fabricate causal reasoning unavailable in source state.

If no evidence exists:

state that evidence is unavailable.

Never infer motive.

---

# 14. UI REQUIREMENTS

Keep UI minimal.

Provide:

* current plan status;
* pause control;
* resume control;
* cancel control;
* clear confirmation for destructive cancel;
* plan-health summary;
* pending adaptation recommendation if one exists.

Avoid major visual redesign.

Do not block Phase 4D on polish.

Safety and lifecycle correctness come first.

Cancel should require an intentional player action.

Do not make destructive action share the same ambiguous UI target as approval.

---

# 15. NO NEW STRATEGIC OBJECTIVES IN PHASE 4D

Phase 4D is about lifecycle, resilience, adaptation, and communication.

Do not add:

TechnologyRush

SiegePreparation

NavalExpansion

or any other objective.

A later phase may implement them only after deterministic execution support exists.

---

# 16. TEST-FIRST DEVELOPMENT REQUIREMENT

Every behavioral feature must begin with a meaningful RED test when practical.

Do not write fake RED tests that fail only because a method throws `NotImplementedException` if a more behavioral failure can be expressed.

Preserve RED evidence.

For each task record:

Agent:

Task:

Expected output:

RED evidence:

Implementation:

GREEN evidence:

Review:

Remaining concerns:

---

# 17. SUB-PHASE GATES

Before advancing from each sub-phase:

### Gate 1 — focused tests

100% pass.

### Gate 2 — current compiler/import state

No compiler errors.

### Gate 3 — scoped independent review

Another reasoning agent reviews:

* specification;
* lifecycle semantics;
* authority;
* stale state;
* resource ownership;
* test adequacy.

### Gate 4 — relevant regression

Run existing Commander tests affected by the change.

### Gate 5 — static boundary check

No new forbidden references.

Only then admit the next sub-phase.

---

# 18. FULL REGRESSION STRATEGY

Do not run full 500+ test suites after every tiny edit.

Use:

focused RED/GREEN
→ scoped review
→ relevant Commander regression

for iteration.

Run fresh COMPLETE EditMode + PlayMode at major source freezes:

1. after 4D.2;
2. after 4D.4/final source freeze;
3. after any later Critical/Important fix affecting shared planner authority.

Do not claim historical results for current source.

---

# 19. REQUIRED FINAL RUNTIME SCENARIOS

Before READY FOR PHASE 4E, demonstrate real PlayMode behavior.

## Scenario A — Pause / Resume

Player approves RangedReinforcement.

Plan begins.

Pause mid-progress.

Tick substantially.

Verify:

* no new strategic progress;
* no duplicate goals;
* no new reservation;
* existing deterministic world remains stable.

Resume.

Verify:

* same plan continues;
* progress resumes;
* plan completes;
* reservations release.

---

## Scenario B — Cancel

Approve DefensiveTurtle.

Allow partial progress.

Cancel.

Verify:

* plan marked cancelled;
* future milestones stop;
* plan-owned reservations release;
* completed buildings/units remain;
* unrelated player commands remain;
* no stale plan progression later.

---

## Scenario C — Temporary Blocker Recovery

Approve strategy.

Cause legitimate resource depletion / production wait.

Verify plan health reports temporary blocker.

Continue deterministic gathering.

Verify:

* blocker transitions to recovered;
* plan resumes;
* no reapproval required;
* strategy completes.

---

## Scenario D — Material Strategy Change

Approve RangedReinforcement.

While active, player requests DefensiveTurtle.

Verify:

* new recommendation may become pending;
* old active plan does not stop because of recommendation;
* no execution occurs before approval.

Player approves replacement.

Verify existing approval/policy path performs replacement.

Verify:

* old plan cleaned up safely;
* reservations correct;
* new plan begins;
* no stale old-plan work resurrects.

---

## Scenario E — Stale Control Attack

Capture control/adaptation request for Plan A.

Replace/cancel Plan A.

Create Plan B.

Apply old request.

Must reject.

Plan B remains untouched.

---

## Scenario F — Match Reset During Async Work

Start provider interpretation.

Reset/end match.

Allow old provider task to complete.

Verify absolutely no stale:

* pending intent;
* lifecycle state;
* plan-health state;
* memory;
* advisory;
* adaptation proposal;
* gameplay execution

appears in the new session.

---

# 20. STATIC SECURITY REQUIREMENTS

Search actual source, not just audit scripts.

Verify no new:

* API key;
* token;
* password;
* secret;
* tracked `.env`;
* CommandBuffer use in AI modules;
* direct GameSimulation use from provider/value modules;
* provider → StrategicPlanner path;
* provider → CommanderGoalManager path;
* provider → execution delegate;
* reflection escape hatch;
* dynamic command creation from text;
* network authority shortcut;
* unsafe Unity object retention.

Review any existing host integration references manually.

Not every symbol match is a violation.

Classify findings.

---

# 21. PROTECTED BOUNDARY AUDIT

Carry forward the Phase 4C protected-boundary strategy.

Create a Phase 4D frozen protected list before coding.

At final gate verify:

* exact current hashes;
* intended changed files;
* intended new files;
* no unexplained protected changes;
* no missing source;
* no audit gaps.

If Phase 4D legitimately requires changing a previously protected authority file:

do not silently update the baseline.

Document:

1. why change is necessary;
2. before hash;
3. after hash;
4. exact diff;
5. authority impact review;
6. focused regression;
7. independent review.

---

# 22. SOURCE-SCOPE DISCIPLINE

Do not touch unrelated:

* rendering;
* terrain;
* audio;
* unit models;
* wall systems;
* package files;
* credentials;
* build settings;
* input settings;
* scenes

unless genuinely required.

Temporary TestRunner/recovery scenes must not enter final source.

Before final gate:

inspect git status for:

* recovery scenes;
* temporary test scenes;
* logs;
* generated scratch assets;
* backup files.

Verify before deletion.

Do not delete user assets merely because names look temporary.

---

# 23. DOCUMENTATION ARTIFACTS

Maintain:

`Docs/CommanderPhase4D/progress.md`

`Docs/CommanderPhase4D/requirements-matrix.md`

Suggested phase plans:

`Docs/superpowers/plans/<date>-commander-phase4d1.md`

`Docs/superpowers/plans/<date>-commander-phase4d2.md`

`Docs/superpowers/plans/<date>-commander-phase4d3.md`

`Docs/superpowers/plans/<date>-commander-phase4d4.md`

Per-subphase reports:

`phase4d1-task-report.md`

`phase4d2-task-report.md`

`phase4d3-task-report.md`

`phase4d4-task-report.md`

Retain important RED artifacts.

Do not retain meaningless duplicate evidence.

---

# 24. FINAL EXTERNAL REVIEW PACKAGE

Prepare a compact but complete package for the external Antigravity auditor.

Include:

* Phase 4D specification;
* baseline SHA / state;
* exact changed production files;
* exact new production files;
* exact test files;
* relevant diffs;
* current file hashes;
* test artifact paths/hashes;
* boundary audit;
* lifecycle diagrams;
* adaptation authority diagram;
* known limitations;
* any historical failure/fix record.

Do not tell Antigravity merely to trust your report.

Make source verification possible.

---

# 25. FINAL FULL TEST GATE

At final source freeze:

Run:

FULL EditMode

FULL PlayMode

Require:

* zero failures;
* zero skipped;
* zero inconclusive;
* all expected Phase 4D tests discovered;
* unique test names where applicable.

Report actual new totals.

Do NOT assume totals remain:

571 / 87.

New tests should increase them.

---

# 26. FINAL SOURCE MANIFEST

Create:

`Docs/CommanderPhase4D/phase4d-final-source-hashes.json`

Must include every changed/new production and test file relevant to Phase 4D.

Verify 100% against live disk.

Record manifest file SHA-256.

---

# 27. FINAL BOUNDARY AUDIT

Create:

`Docs/CommanderPhase4D/phase4d-final-boundary-audit.json`

Must report:

* protected file count;
* unchanged count;
* intentional changed protected files;
* audit gaps;
* credential pattern count;
* forbidden-reference candidates;
* `.env` tracked/untracked/ignored status;
* package/settings changes;
* unexpected source files.

---

# 28. FINAL DELIVERABLE

Create:

`Docs/CommanderPhase4D.md`

Containing:

## A. Architecture changes

Describe:

* lifecycle control;
* plan identity/version safety;
* plan health;
* recovery;
* adaptation proposals;
* advisories.

## B. Agent delegation report

For every implementation/review/evidence worker:

* role;
* task;
* expected output;
* validation;
* result.

Do not invent missing agent identities.

## C. New systems

List actual implemented types and integration points.

## D. Safety boundaries

Explicitly state:

* LLM authority;
* planner authority;
* player approval;
* stale request safety;
* ownership safety;
* fog safety;
* resource/reservation safety;
* provider isolation.

## E. Tests

Include:

* focused RED/GREEN;
* scoped regression;
* complete final EditMode;
* complete final PlayMode;
* source hash verification;
* boundary audit.

## F. Runtime evidence

Describe exact PlayMode scenarios actually demonstrated.

Do not convert unit-test evidence into runtime claims.

## G. Limitations / deferred work

Document honestly.

Examples:

* unsupported objective types;
* networking limitations;
* behaviors intentionally left tactical;
* non-blocking reservation design limitations;
* features deferred to Phase 4E.

---

# 29. FINAL VERDICT

The report must end with EXACTLY one:

`READY FOR PHASE 4E`

or

`REQUIRES FIX PHASE`

Do not declare READY merely because tests are green.

READY requires:

* architectural review PASS;
* authority boundaries PASS;
* focused tests PASS;
* full tests PASS;
* runtime scenarios PASS;
* stale async safety PASS;
* ownership safety PASS;
* resource/reservation safety PASS;
* source manifest PASS;
* static/security audit PASS;
* no unresolved Critical or Important findings.

A Minor issue may remain only if:

* explicitly documented;
* demonstrably non-blocking;
* does not weaken safety/authority;
* does not falsify a promised Phase 4D behavior.

---

# 30. REVIEW SEVERITY

Use:

## Critical

Examples:

* direct execution bypass;
* LLM obtains planner/simulation authority;
* cross-player control;
* stale request mutates newer plan;
* hidden-state leak;
* reservation corruption causing determinism failure.

## Important

Examples:

* pause/cancel semantics incorrect;
* old plan resurrects;
* replacement leaks reservations;
* material adaptation applies without approval;
* reset leaks Phase 4D state;
* deterministic lifecycle broken.

## Minor

Examples:

* missing edge-case test with otherwise correct implementation;
* bounded UX inconsistency;
* documentation mismatch;
* non-blocking hygiene issue.

Do not downgrade an authority defect because tests happen to pass.

---

# 31. ROOT AGENT STOP CONDITIONS

If any of these occur:

* architecture boundary violation;
* unexplained protected-file change;
* stale control can affect a replacement plan;
* provider can trigger lifecycle mutation;
* cancellation leaks strategic resources;
* multiplayer ownership can be bypassed;
* full regression reveals shared planner regression;

STOP later-phase implementation.

Fix and rereview before continuing.

---

# 32. IMPLEMENTATION STYLE

Prefer:

* small immutable value objects;
* deterministic pure evaluators;
* existing authority paths;
* explicit enum/switch handling;
* bounded collections;
* exact whole-form commands;
* culture-invariant serialization;
* defensive validation;
* copied snapshots;
* clear reset lifecycle.

Avoid:

* god objects;
* hidden global state;
* static mutable singleton state;
* duplicate plan state machines;
* fragile string parsing;
* silent fallback behavior;
* reflection-based authority hacks;
* unnecessary abstractions.

---

# 33. IMPORTANT COMPATIBILITY RULE

Preserve existing public APIs and reflection-sensitive method signatures unless a change is absolutely necessary.

If a signature must change:

1. find all reflection/test callers;
2. add compatibility overload where safe;
3. write regression first;
4. document the change;
5. obtain independent review.

Phase 4C previously exposed reflection compatibility regressions.

Do not repeat them.

---

# 34. EXECUTION INSTRUCTION

Proceed continuously through the gated plan.

Do not ask for routine approval between implementation steps.

Do NOT wait for external Antigravity during normal implementation.

Complete internal reviews and evidence first.

Stop only for:

* a genuine architecture conflict requiring a product decision;
* a safety boundary that cannot be resolved from existing design;
* missing deterministic execution support that would require expanding Phase 4D scope.

When source and evidence are final:

prepare the Antigravity review package and report readiness for external audit.

Do NOT start Phase 4E.

---

# END GOAL

At the end of Phase 4D, the Commander should feel substantially more intelligent because it understands:

* whether a strategy is running;
* whether it is paused;
* what is blocking it;
* whether the blocker is temporary;
* when it has recovered;
* when it needs a player decision;
* how to safely present an alternative;
* how to respect pause/resume/cancel commands;
* how to survive stale async responses and plan replacement;

while still NEVER becoming the authority that executes strategy.

Player decides.

Deterministic systems execute.

The LLM interprets, explains, and advises.

That boundary is non-negotiable.
