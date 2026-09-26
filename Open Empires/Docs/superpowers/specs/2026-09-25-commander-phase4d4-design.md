# Commander Phase 4D.4 — offline strategic advisories

Status: Phase 4D.3 gate passed on 2026-09-26; this reviewed design now guides Phase 4D.4 implementation. It implements the already-approved Phase 4D objective and does not create strategic execution authority.

## Intent and boundary

Tell the player when an owned approved strategy meaningfully changes state—waiting, recovering, pausing, resuming, completing, or cancelling—without a provider call, another approval, a new intent, or a gameplay command. The authoritative input is a fresh, owner-scoped, detached `StrategicPlanHealthSnapshot` from Phase 4D.2. An advisory is a copied informational value, not a plan control request. It must not retain a planner, simulation, Unity object, goal, command, delegate, or provider.

## Approaches considered

1. Put transition detection and wording directly in `CommanderChatUI`: smallest file count, but adds another state machine to a large host and makes deterministic value tests depend on Unity UI.
2. Use a small pure advisory tracker driven by copied health snapshots, with a thin host presenter: preferred. It keeps deduplication, bounded state, and rendering independently testable while reusing the existing host event and health paths.
3. Emit advisories from `StrategicPlanner`: close to status events, but would put player-facing presentation in the execution authority layer and make the planner own transcript lifetime. Reject.

## Value and transition model

`StrategicAdvisoryFeed` accepts one `StrategicPlanHealthSnapshot` at a time and returns zero or a bounded number of detached `StrategicAdvisory` values. A value carries owner, plan ID, creation tick, observed tick, revision, typed transition, and a bounded deterministic display string. It contains no mutating method. The feed never reads the world, never calls the planner, and never invokes callbacks.

The per-plan identity is `(PlayerId, PlanId, CreatedTick)`. For that identity, reject a snapshot if either its revision or its observed simulation tick is lower than the last accepted value; both are monotone within one match. The host rejects an old generation before the feed sees it. Suppress repeat observations of the same meaningful state. Seed an initial observation silently because plan acceptance is already presented by the host. Track structured plan status and typed health categories, not explanation strings or changing deficit amounts. A transition may announce a new resource or population wait; a wait's disappearance may announce one recovery even if a different blocker remains. Terminal status is emitted once. Pause/resume is derived from status change, not elapsed time. If overlapping transitions occur in one observation, emit them in a fixed order and cap output per observation; never announce recovery solely because the source plan vanished.

For a resource wait, name a resource only when the snapshot contains a positive typed deficit. Otherwise say only that resources are pending. Do not infer a cause, enemy, or material-change recommendation that the structured snapshot cannot prove. `Unknown` is not a blocker diagnosis. `NeedsPlayerDecision` is not currently a Phase 4D.2 health category; the example `Running → NeedsPlayerDecision` is unavailable in the present structured model and is explicitly deferred rather than synthesized from a long wait. A failed plan may receive a factual terminal notice without inventing a remedy.

Retain at most a fixed small number of per-plan transition states, evicting oldest insertion order deterministically. No wall-clock cooldown, random identity, persistence, or cross-match cache. `Reset()` clears all retained states. An old snapshot cannot affect a newer plan with a different identity.

## Host integration

Keep integration in a focused `CommanderChatUI` partial. Observe fresh owner-scoped health for **every** active owned plan at most once per `simulation.CurrentTick` during periodic host refresh, so a non-selected plan's ordinary world-state recovery is not missed. Event callbacks are a separate path: use the event's exact plan identity to capture fresh health even for a plan that has just become terminal and left `ActivePlans`. Do not discard an event merely because a periodic scan already ran in the same simulation tick; the pure feed deduplicates repeated state, while distinct same-tick transitions remain visible. Event callbacks never render from the mutable event plan itself. The host verifies current pipeline, owner, `ReferenceEquals(planner.GetPlan(id), eventPlan)`, creation identity, and generation before presenting the fresh copied snapshot. The presenter appends bounded text to the existing transcript without recording an authoritative memory entry. It never touches `StrategicAIApprovalBridge`, approval controls, plan lifecycle, or provider methods.

Reset advisory state on `Initialize`, `InitializeStrategic` replacement or reinitialization, `ResetConversation`, owner/match replacement, and `OnDestroy`. Detach old pipeline subscriptions first. A late event or stale snapshot from the old generation must produce no advisory. Multiple active plans are tracked independently by plan identity; a selected-plan UI change is not itself a lifecycle transition.

## Verification

Start with focused RED tests for the required exact names: `Advisory_DoesNotExecuteAnything`, `Advisory_DoesNotCallProvider`, `Advisory_EmitsOnMeaningfulTransition`, `Advisory_DoesNotRepeatEveryTick`, `Advisory_RecoveryEmitsOnce`, `Advisory_CompletionEmitsOnce`, `Advisory_ResetClearsDeduplication`, `Advisory_IsBounded`, `Advisory_IsPlanVersionSafe`, and `Advisory_RemainsFogSafe`. Value tests must use real detached health values or real planner captures; host PlayMode tests must assert transcript behavior and unchanged provider, decision, plan, goal, reservation, and command counts. Cover pause/resume, resource and population recovery, cancellation, same-state repeated ticks, stale older revision, non-selected plan transitions, same-tick event transitions, reused numeric plan IDs after reset, same-pipeline reinitialization, and a held provider reply crossing match reset.

Run focused GREEN, affected Commander regressions, independent authority/fog/reset review, then fresh complete EditMode and PlayMode suites at final source freeze. The final Phase 4D audit additionally verifies all scenarios A–F, protected hashes, static security boundaries, and the external review package before `READY FOR PHASE 4E` can be considered.
