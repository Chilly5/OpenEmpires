# Phase 4D.3 read-only preflight notes

Preparation only. Do not start 4D.3 production until the 4D.2 runtime, full-suite, independent-review, and boundary gates pass.

- `StrategicPlanHealthSnapshot` is a detached current-state observation; it does not prove historical recovery or grant authority. A proposal must copy bounded scalar evidence and be revalidated against fresh owner/plan ID/creation tick/revision/status and the blocker assumption before application.
- The existing approval path is `StrategicApprovalLayer.Evaluate` → `StrategicPipeline.EvaluateApprovedIntentNow` → `IStrategicApprovedDecisionPolicy.DecideApproved` → `StrategicPlanner.SubmitIntent`. The pipeline rebuilds context, verifies intent ownership, and rechecks approval; proposals must not call planner directly.
- Merely displaying a recommendation must leave the old active plan untouched. `StrategicPlanner.SubmitIntent` cancels incompatible plans only for an approved player override or qualifying emergency before starting the new plan (`StrategicPlanner.cs` around lines 183–243). `CancelPlan` owns child/reservation cleanup.
- Review focus: submission currently cancels conflicting plans before checking the active-plan limit and computing the new plan budget. Test approval-time failure ordering before claiming an atomic replacement. Do not silently add rollback or a second authority path.
- The user's Spearman screenshot is separate tactical evidence. `CommanderPlanner.PlanGather` maps null from node, worker, or reachability selection to the same worker-unavailable message; 4D.2 health must not infer a specific worker cause from that string.
