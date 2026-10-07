# Phase 5A execution authority policy

Status: strategic and request-scoped authority implementation with focused evidence. This is not exhaustive acceptance; see the implementation report for final handoff scope.

## Strategic roots

Trusted computer ownership is the AI controller actually created by `GameSimulation` setup and exposed by `GetAiPlayer`; guard player range. Local/remote human and unknown slots do not inherit autonomy from player0, host/local status, UI absence or recommendation priority. No AIPlayerSystem mutation or new opponent is introduced.

`StrategicPlanner.SubmitIntent` enforces authorization before plan template construction, registration, cancellation/replacement or reservations. A `StrategicIntent.Source` label is never a credential. Trusted `CreateIntent` and materialized legacy player interpretation mint exact runtime identity-owner evidence. `StrategicAIApprovalBridge.TakeRecommendation/Confirm` consume the displayed intent and mint approval/explicit-confirmation evidence on that identity. These game-owned APIs are not provider primitives or wire fields. Pure `StrategicApprovalLayer.Evaluate` checks feasibility/policy; it does not independently prove human consent.

`StrategicPipeline` retains evaluation/recommendations/history but does not materialize an executable human background intent. It records "Suggested strategy — not started". Emergency cannot bypass the committing gate or replace an older approved root just because its descriptive source is AIRecommendation. Authorized root credentials persist into milestones; retry/start paths recheck them and use existing cancellation/reservation release if missing. Existing strategic UI stronger adaptation confirmation and ownership/revision/freshness checks remain.

## Compound action candidates

`CommanderGoalManager.PrepareActionPlan` strictly admits the entire typed graph without goals/reservations/commands. Candidate carries immutable graph/interpretation, original input<=1024, locally minted request sequence, owner/manager/simulation, generation and strategic revision snapshot. Preview is rendered from all normalized intents/selectors/constraints/result edges, never provider prose; maximum8192characters.

Normal UI exposes Approve plan/Dismiss plus whole-form `approve plan`, `confirm plan`, `cancel plan`, `dismiss plan`; an unambiguous currently displayed candidate also accepts yes/no/cancel. Worker clarification routing has precedence and remains separate. New independent turns supersede unapproved action candidates. Reset/Initialize/disposal cancel pending candidates. No second provider call is made for consent.

Early review repair: prepare/approve methods are internal trusted-game-assembly APIs, not an external provider approval surface. Host candidates retain a live generation/cancellation/provider/manager/simulation/dispatcher lease until commit, including when the displayed pointer was detached. Independent trusted typed callers use their owning manager lifetime. Committed graph event listeners are isolated individually so observer faults do not misreport successful initial admission or suppress later notifications. This changes only post-commit graph publication, not the existing strategic/live-tick observer contract. Review repair affected checks passed43/43Edit (job946a0ffb); no broad final Phase5A claim follows.

`ApproveActionPlan` requires exact owner/runtime/generation/strategic-source/revision and identical normalized re-admission; changed Next-age meaning rejects rather than repairing under old approval. `SubmitSemanticGraph` itself refuses unsigned, stale/foreign or consumed graphs. It preflights every pending goal/dependency before consuming approval and registering initial work. Replay creates none. Completion is not gameplay rollback: already-issued commands are not undone.

Single existing KnownIntent tactical requests still use established automatic admission, with game-owned scope captured before first goal publication. Strategic proposals retain their existing approval path. Dynamic execution is enabled only through the validated/compiler/approval path after checkpoint A passed. Independently trusted typed roots may skip the UI confirmation only when exact intent types, quantities, constraints and bindings match; provider-emitted roots/spans/labels never qualify.

## Evidence and remaining boundaries

See execution-progress and retained XML for exact current narrow results/anomalies. Actual bootstrap qualifying state is controlled before its first tick; building creation events can coalesce into the pending startup trigger. This does not certify the ordinary opening state or exact user incident.

Shared NoConstruction, idle-worker preferences, protected resource floors, queue limits and source restrictions are enforced by existing planners and dynamic preflight; derived preparation inherits its root scope. Strategic roots retain exact planner-owned consent evidence, while chat action requests carry local request/candidate provenance. Trusted legacy direct game APIs are not retroactively converted into chat request tickets; diagnostics must not imply that they are. Nine dynamic primitives and six scoped live Luna/native examples are implemented and evidenced. No new network provenance bytes were added; pre-existing economy restricted version1 packets still require updated peers. Independent exhaustive/standalone/multiplayer/hostile audit remains pending.
