# Phase 4D.1 core review package

Base Phase 4C live source is pinned by Docs/CommanderPhase4D/phase4d-source-baseline.json. No commits are made because the Phase 4C fix is uncommitted.

## Tracked core diff

warning: in the working copy of 'Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs', LF will be replaced by CRLF the next time Git touches it
diff --git a/Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs b/Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs
index d71c083..e24ae81 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs	
@@ -18,6 +18,7 @@ namespace OpenEmpires
         private readonly List<CommanderGoal> goals = new List<CommanderGoal>();
         private readonly List<CommanderGoal> activeGoals = new List<CommanderGoal>();
         private readonly List<CommanderGoal> archivedGoals = new List<CommanderGoal>();
+        private readonly HashSet<int> suspendedGoalIds = new HashSet<int>();
         private int nextGoalId = 1;
         private int lastEvaluatedTick = -1;
         private bool isTicking;
@@ -41,6 +42,54 @@ namespace OpenEmpires
             return null;
         }
 
+        // Called only with child IDs selected by StrategicPlanner, never from player text.
+        internal void SuspendGoal(int goalId)
+        {
+            CommanderGoal goal = GetGoal(goalId);
+            if (goal == null || goal.IsTerminal) return;
+            suspendedGoalIds.Add(goalId);
+            if (ActiveGoal == goal) ActiveGoal = null;
+        }
+
+        internal bool CanResumeGoal(int goalId, int pausedTicks)
+        {
+            CommanderGoal goal = GetGoal(goalId);
+            if (pausedTicks < 0 || goal == null || goal.IsTerminal || !suspendedGoalIds.Contains(goalId))
+                return pausedTicks >= 0;
+            return CanShift(goal.CreatedTick, pausedTicks)
+                && (goal.BlockedSinceTick < 0 || CanShift(goal.BlockedSinceTick, pausedTicks))
+                && (goal.BlockedSinceTick < 0 || CanShift(goal.NextBlockedRetryTick, pausedTicks))
+                && (goal.LastEconomyCommandTick == int.MinValue / 2
+                    || CanShift(goal.LastEconomyCommandTick, pausedTicks))
+                && (goal.ObservedConstructionBuildingId < 0
+                    || CanShift(goal.LastConstructionProgressTick, pausedTicks))
+                && (goal.LastConstructionRecoveryTick == int.MinValue / 2
+                    || CanShift(goal.LastConstructionRecoveryTick, pausedTicks));
+        }
+
+        internal void ResumeGoal(int goalId, int pausedTicks)
+        {
+            CommanderGoal goal = GetGoal(goalId);
+            if (goal == null || goal.IsTerminal || !suspendedGoalIds.Contains(goalId)) return;
+            if (!CanResumeGoal(goalId, pausedTicks))
+                throw new InvalidOperationException("A suspended goal tick anchor cannot be shifted safely.");
+            goal.CreatedTick = checked(goal.CreatedTick + pausedTicks);
+            if (goal.BlockedSinceTick >= 0)
+            {
+                goal.BlockedSinceTick = checked(goal.BlockedSinceTick + pausedTicks);
+                goal.NextBlockedRetryTick = checked(goal.NextBlockedRetryTick + pausedTicks);
+            }
+            if (goal.LastEconomyCommandTick != int.MinValue / 2)
+                goal.LastEconomyCommandTick = checked(goal.LastEconomyCommandTick + pausedTicks);
+            if (goal.ObservedConstructionBuildingId >= 0)
+                goal.LastConstructionProgressTick = checked(goal.LastConstructionProgressTick + pausedTicks);
+            if (goal.LastConstructionRecoveryTick != int.MinValue / 2)
+                goal.LastConstructionRecoveryTick = checked(goal.LastConstructionRecoveryTick + pausedTicks);
+            suspendedGoalIds.Remove(goalId);
+        }
+
+        private static bool CanShift(int anchor, int delta) => (long)anchor + delta <= int.MaxValue;
+
         public CommanderWorkerReservation? GetWorkerReservation(int workerId) => workerAuthority.GetReservation(workerId);
 
         public bool TryReserveWorker(int goalId, int workerId, CommanderWorkerReservationType reservationType)
@@ -145,6 +194,7 @@ namespace OpenEmpires
             {
                 CommanderGoal goal = goals[i];
                 if (goal.GoalId != goalId || goal.IsTerminal) continue;
+                suspendedGoalIds.Remove(goalId);
                 goal.SetStatus(CommanderGoalStatus.Cancelled, "Cancelled by the owning player.");
                 workerAuthority.ReleaseGoal(goal.GoalId);
                 ArchiveGoal(goal);
@@ -174,7 +224,8 @@ namespace OpenEmpires
                 for (int i = 0; i < activeGoals.Count; i++)
                 {
                     CommanderGoal goal = activeGoals[i];
-                    if (!goal.IsTerminal && goal.MaxDurationTicks > 0
+                    if (!goal.IsTerminal && !suspendedGoalIds.Contains(goal.GoalId)
+                        && goal.MaxDurationTicks > 0
                         && currentTick - goal.CreatedTick >= goal.MaxDurationTicks)
                         FailGoal(goal, $"Goal exceeded its {goal.MaxDurationTicks}-tick duration limit.", currentTick);
                 }
@@ -185,7 +236,8 @@ namespace OpenEmpires
                 for (int i = 0; i < activeGoals.Count; i++)
                 {
                     CommanderGoal goal = activeGoals[i];
-                    if (goal.IsTerminal || (goal.Status == CommanderGoalStatus.Blocked
+                    if (goal.IsTerminal || suspendedGoalIds.Contains(goal.GoalId)
+                        || (goal.Status == CommanderGoalStatus.Blocked
                         && currentTick < goal.NextBlockedRetryTick)) continue;
                     CommanderPlan plan = planner.Plan(goal, currentTick);
                     if (plan.Command != null && !workerAuthority.TryReserveCommand(goal, plan.Command, currentTick))
@@ -286,6 +338,7 @@ namespace OpenEmpires
             simulation.CommandBuffer.CommandEnqueued -= HandleCommandEnqueued;
             for (int i = 0; i < activeGoals.Count; i++)
                 workerAuthority.ReleaseGoal(activeGoals[i].GoalId);
+            suspendedGoalIds.Clear();
             GoalStatusChanged = null;
             GoalEventPublished = null;
         }
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs b/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs
index 7940ec2..4ad92df 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs	
@@ -51,6 +51,8 @@ namespace OpenEmpires
             && currentMilestoneIndex < milestones.Count ? milestones[currentMilestoneIndex] : null;
         public StrategicPlanStatus Status { get; internal set; }
         public int CreatedTick { get; internal set; }
+        public int Revision { get; private set; }
+        internal int PausedAtTick { get; set; } = -1;
         public IReadOnlyList<int> ChildGoalIds => childGoalIds;
         public IReadOnlyList<StrategicMilestone> Milestones => milestones;
         public IReadOnlyList<StrategicResourceRequirement> RequiredResources => requiredResources;
@@ -63,6 +65,17 @@ namespace OpenEmpires
             || Status == StrategicPlanStatus.Failed
             || Status == StrategicPlanStatus.Cancelled;
 
+        internal void InitializeRevision()
+        {
+            if (Revision != 0) throw new InvalidOperationException("A plan revision can be initialized only once.");
+            Revision = 1;
+        }
+
+        internal void AdvanceRevision()
+        {
+            Revision = checked(Revision + 1);
+        }
+
         protected StrategicPlan(int ownerPlayerId, StrategicPlanType planType, int sourceIntentId,
             string completionResponse, string cancellationResponse)
         {
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs b/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs
index 31836c8..caad962 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs	
@@ -221,6 +221,7 @@ namespace OpenEmpires
             FitEconomyToAvailableWorkers(plan);
             plan.StrategicPlanId = nextPlanId++;
             plan.CreatedTick = goalManager.CurrentTick;
+            plan.InitializeRevision();
             plans.Add(plan);
             activePlans.Add(plan);
             intentsByPlanId.Add(plan.StrategicPlanId, intent);
@@ -279,7 +280,9 @@ namespace OpenEmpires
             ThrowIfDisposed();
             StrategicPlan plan = GetPlan(strategicPlanId);
             if (plan == null || plan.IsTerminal) return false;
+            if (plan.Revision == int.MaxValue) return false;
             plan.Status = StrategicPlanStatus.Cancelled;
+            plan.AdvanceRevision();
             plan.OutcomeMessage = plan.CancellationMessage;
             activePlans.Remove(plan);
             ArchivePlan(plan);
@@ -335,7 +338,8 @@ namespace OpenEmpires
             for (int i = 0; i < waiting.Count; i++)
             {
                 StrategicPlan plan = waiting[i];
-                if (!plan.IsTerminal && plan.CurrentMilestone != null)
+                if (!plan.IsTerminal && plan.Status != StrategicPlanStatus.Paused
+                    && plan.CurrentMilestone != null)
                     StartOrWaitForMilestone(plan, plan.CurrentMilestone);
             }
         }
@@ -389,20 +393,38 @@ namespace OpenEmpires
         public bool UpdateReservationAmount(int reservationId, int newAmount)
         {
             ThrowIfDisposed();
-            return reservationManager.UpdateReservationAmount(reservationId, newAmount);
+            for (int i = 0; i < reservationManager.Reservations.Count; i++)
+            {
+                StrategicResourceReservation reservation = reservationManager.Reservations[i];
+                if (reservation.ReservationId != reservationId) continue;
+                StrategicPlan plan = GetPlan(reservation.PlanId);
+                if (plan == null || plan.Status == StrategicPlanStatus.Paused) return false;
+                bool updated = reservationManager.UpdateReservationAmount(reservationId, newAmount);
+                if (updated && newAmount > 0) plan.AdvanceRevision();
+                return updated;
+            }
+            return false;
         }
 
         public bool ReleaseReservation(int reservationId, bool cancelled = false)
         {
             ThrowIfDisposed();
-            return reservationManager.ReleaseReservation(reservationId, cancelled);
+            for (int i = 0; i < reservationManager.Reservations.Count; i++)
+            {
+                StrategicResourceReservation reservation = reservationManager.Reservations[i];
+                if (reservation.ReservationId != reservationId) continue;
+                StrategicPlan plan = GetPlan(reservation.PlanId);
+                return plan != null && plan.Status != StrategicPlanStatus.Paused
+                    && reservationManager.ReleaseReservation(reservationId, cancelled);
+            }
+            return false;
         }
 
         public bool CompleteMilestoneAndAdvance(int strategicPlanId)
         {
             ThrowIfDisposed();
             StrategicPlan plan = GetPlan(strategicPlanId);
-            if (plan == null || plan.IsTerminal)
+            if (plan == null || plan.IsTerminal || plan.Status == StrategicPlanStatus.Paused)
                 return false;
             if (plan.CurrentMilestone == null) return false;
             CompleteMilestoneAndAdvance(plan, plan.CurrentMilestone);
@@ -412,6 +434,7 @@ namespace OpenEmpires
         private void CreateGoalsForMilestone(StrategicPlan plan, StrategicMilestone milestone,
             bool economyOnly = false)
         {
+            if (plan.Status == StrategicPlanStatus.Paused) return;
             if (milestone.TacticalGoalsStarted) return;
             if (!economyOnly) milestone.MarkTacticalGoalsStarted();
             if (milestone.TacticalGoals.Count == 0)
@@ -443,7 +466,7 @@ namespace OpenEmpires
         private bool SubmitTrackedGoal(StrategicPlan plan, StrategicMilestone milestone,
             Func<CommanderGoal> submit)
         {
-            if (plan.IsTerminal) return false;
+            if (plan.IsTerminal || plan.Status == StrategicPlanStatus.Paused) return false;
             submittingPlan = plan;
             submittingMilestone = milestone;
             try
@@ -473,16 +496,19 @@ namespace OpenEmpires
             if (!childGoalLinks.TryGetValue(goalId, out ChildGoalLink link)) return;
 
             ChildGoalEventObserved?.Invoke(link.Plan, goalEvent);
-            if (link.Plan.IsTerminal) return;
+            if (link.Plan.IsTerminal || link.Plan.Status == StrategicPlanStatus.Paused) return;
             switch (goalEvent.EventType)
             {
                 case CommanderGoalEventType.GoalStarted:
+                    break;
                 case CommanderGoalEventType.GoalProgressChanged:
                 case CommanderGoalEventType.GoalBlocked:
                     // These events are observable but do not advance a milestone.
+                    link.Plan.AdvanceRevision();
                     break;
                 case CommanderGoalEventType.GoalCompleted:
                     link.Milestone.MarkChildGoalCompleted(goalId);
+                    link.Plan.AdvanceRevision();
                     if (link.Milestone.Status == StrategicMilestoneStatus.Active
                         && link.Milestone.IsSatisfied)
                         CompleteMilestoneAndAdvance(link.Plan, link.Milestone);
@@ -501,8 +527,10 @@ namespace OpenEmpires
 
         private void CompleteMilestoneAndAdvance(StrategicPlan plan, StrategicMilestone milestone)
         {
-            if (plan.IsTerminal || milestone.Status != StrategicMilestoneStatus.Active) return;
+            if (plan.IsTerminal || plan.Status == StrategicPlanStatus.Paused
+                || milestone.Status != StrategicMilestoneStatus.Active) return;
             milestone.SetStatus(StrategicMilestoneStatus.Completed);
+            plan.AdvanceRevision();
             Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} milestone completed: {milestone.Name}.");
             MilestoneStatusChanged?.Invoke(plan, milestone);
 
@@ -524,12 +552,14 @@ namespace OpenEmpires
 
         private void StartOrWaitForMilestone(StrategicPlan plan, StrategicMilestone milestone)
         {
-            if (plan == null || milestone == null || plan.IsTerminal) return;
+            if (plan == null || milestone == null || plan.IsTerminal
+                || plan.Status == StrategicPlanStatus.Paused) return;
             if (plan is EconomicExpansionPlan && goalManager.Simulation.GetPlayerAge(PlayerId)
                 < LandmarkDefinitions.GetBuildingRequiredAge(BuildingType.TownCenter))
             {
                 bool changed = milestone.Status != StrategicMilestoneStatus.WaitingForPrerequisite;
                 milestone.SetStatus(StrategicMilestoneStatus.WaitingForPrerequisite);
+                if (changed) plan.AdvanceRevision();
                 plan.OutcomeMessage = $"TownCenter requires age {LandmarkDefinitions.GetBuildingRequiredAge(BuildingType.TownCenter)}. Advance age to resume economic expansion.";
                 if (changed) MilestoneStatusChanged?.Invoke(plan, milestone);
                 return;
@@ -546,6 +576,7 @@ namespace OpenEmpires
                     bool changed = milestone.Status
                         != StrategicMilestoneStatus.WaitingForResources;
                     milestone.SetStatus(StrategicMilestoneStatus.WaitingForResources);
+                    if (changed) plan.AdvanceRevision();
                     plan.OutcomeMessage = conflict.ToString();
                     if (changed)
                     {
@@ -567,7 +598,9 @@ namespace OpenEmpires
                     milestone.AddResourceReservation(created[r].ReservationId);
             }
 
+            bool activated = milestone.Status != StrategicMilestoneStatus.Active;
             milestone.SetStatus(StrategicMilestoneStatus.Active);
+            if (activated) plan.AdvanceRevision();
             plan.OutcomeMessage = string.Empty;
             Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} milestone active: {milestone.Name}.");
             MilestoneStatusChanged?.Invoke(plan, milestone);
@@ -730,6 +763,7 @@ namespace OpenEmpires
         private void CompletePlan(StrategicPlan plan)
         {
             plan.Status = StrategicPlanStatus.Completed;
+            plan.AdvanceRevision();
             plan.OutcomeMessage = plan.CompletionMessage;
             activePlans.Remove(plan);
             ArchivePlan(plan);
@@ -748,6 +782,7 @@ namespace OpenEmpires
                 MilestoneStatusChanged?.Invoke(plan, milestone);
             }
             plan.Status = StrategicPlanStatus.Failed;
+            plan.AdvanceRevision();
             plan.OutcomeMessage = reason ?? "The strategic plan failed.";
             activePlans.Remove(plan);
             ArchivePlan(plan);
@@ -764,6 +799,7 @@ namespace OpenEmpires
             if (plan == null || milestone == null || childGoalLinks.ContainsKey(goalId)) return;
             childGoalLinks.Add(goalId, new ChildGoalLink(plan, milestone));
             plan.AddChildGoal(goalId);
+            plan.AdvanceRevision();
             milestone.AddRequiredChildGoal(goalId);
         }
 
@@ -771,6 +807,7 @@ namespace OpenEmpires
         {
             StrategicPlan plan = GetPlan(reservation.PlanId);
             plan?.AddResourceReservation(reservation.ReservationId);
+            plan?.AdvanceRevision();
             Debug.Log($"[StrategicPlanner] Reservation #{reservation.ReservationId} created for "
                 + $"plan #{reservation.PlanId}: {reservation.Amount} {reservation.ResourceType}.");
             ReservationCreated?.Invoke(reservation);
@@ -778,6 +815,7 @@ namespace OpenEmpires
 
         private void HandleReservationReleased(StrategicResourceReservation reservation)
         {
+            GetPlan(reservation.PlanId)?.AdvanceRevision();
             Debug.Log($"[StrategicPlanner] Reservation #{reservation.ReservationId} "
                 + $"{reservation.Status.ToString().ToLowerInvariant()} for plan #{reservation.PlanId}.");
             ReservationReleased?.Invoke(reservation);


## Added file Assets/Scripts/AI/Commander/Strategic/StrategicPlanControl.cs

using System;

namespace OpenEmpires
{
    public enum StrategicPlanControlType { Pause, Resume, Cancel, Status }

    public enum StrategicPlanControlStatus
    {
        Applied,
        NoActivePlan,
        AmbiguousPlan,
        AlreadyPaused,
        AlreadyRunning,
        AlreadyCompleted,
        AlreadyCancelled,
        StalePlan,
        Unauthorized,
        Rejected
    }

    // A copied observation, not a reference to a plan or an authority to submit goals.
    public readonly struct StrategicPlanControlRequest
    {
        public int PlayerId { get; }
        public int PlanId { get; }
        public int CreatedTick { get; }
        public int ObservedRevision { get; }
        public StrategicPlanControlType ControlType { get; }

        internal StrategicPlanControlRequest(int playerId, int planId, int createdTick,
            int observedRevision, StrategicPlanControlType controlType)
        {
            PlayerId = playerId;
            PlanId = planId;
            CreatedTick = createdTick;
            ObservedRevision = observedRevision;
            ControlType = controlType;
        }
    }

    public readonly struct StrategicPlanControlResult
    {
        public StrategicPlanControlStatus Status { get; }
        public int PlayerId { get; }
        public int PlanId { get; }
        public int CreatedTick { get; }
        public int Revision { get; }
        public StrategicPlanStatus PlanStatus { get; }
        public string Message { get; }

        internal StrategicPlanControlResult(StrategicPlanControlStatus status,
            StrategicPlanControlRequest request, StrategicPlan plan, string message)
        {
            Status = status;
            PlayerId = request.PlayerId;
            PlanId = request.PlanId;
            CreatedTick = request.CreatedTick;
            Revision = plan?.Revision ?? request.ObservedRevision;
            PlanStatus = plan?.Status ?? StrategicPlanStatus.Created;
            Message = message ?? string.Empty;
        }
    }
}



## Added file Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.Controls.cs

using System;

namespace OpenEmpires
{
    public sealed partial class StrategicPlanner
    {
        public bool CaptureControlRequest(int trustedPlayerId, int planId,
            StrategicPlanControlType action, out StrategicPlanControlRequest request)
        {
            request = default;
            if (disposed || trustedPlayerId != PlayerId || !Enum.IsDefined(typeof(StrategicPlanControlType), action))
                return false;
            StrategicPlan plan = GetPlan(planId);
            if (plan == null || plan.IsTerminal || plan.OwnerPlayerId != trustedPlayerId)
                return false;
            request = new StrategicPlanControlRequest(trustedPlayerId, plan.StrategicPlanId,
                plan.CreatedTick, plan.Revision, action);
            return true;
        }

        public bool CaptureCurrentControlRequest(int trustedPlayerId,
            StrategicPlanControlType action, out StrategicPlanControlRequest request)
        {
            request = default;
            if (disposed || trustedPlayerId != PlayerId || !Enum.IsDefined(typeof(StrategicPlanControlType), action))
                return false;
            StrategicPlan selected = null;
            for (int i = 0; i < activePlans.Count; i++)
            {
                StrategicPlan plan = activePlans[i];
                if (plan.IsTerminal || plan.OwnerPlayerId != trustedPlayerId) continue;
                if (selected != null) return false;
                selected = plan;
            }
            return selected != null && CaptureControlRequest(trustedPlayerId,
                selected.StrategicPlanId, action, out request);
        }

        public StrategicPlanControlResult ApplyControl(StrategicPlanControlRequest request)
        {
            if (disposed) return Result(StrategicPlanControlStatus.StalePlan, request, null,
                "The plan runtime is no longer active.");
            if (!Enum.IsDefined(typeof(StrategicPlanControlType), request.ControlType))
                return Result(StrategicPlanControlStatus.Rejected, request, null,
                    "Unknown plan control action.");
            if (request.PlayerId != PlayerId)
                return Result(StrategicPlanControlStatus.Unauthorized, request, null,
                    "Plan owner does not match the Commander host.");
            StrategicPlan plan = GetPlan(request.PlanId);
            if (plan == null || plan.OwnerPlayerId != request.PlayerId
                || plan.CreatedTick != request.CreatedTick || plan.Revision != request.ObservedRevision)
                return Result(StrategicPlanControlStatus.StalePlan, request, null,
                    "The captured plan state has changed.");
            if (plan.Status == StrategicPlanStatus.Completed || plan.Status == StrategicPlanStatus.Failed)
                return Result(StrategicPlanControlStatus.AlreadyCompleted, request, plan,
                    "The plan has already ended.");
            if (plan.Status == StrategicPlanStatus.Cancelled)
                return Result(StrategicPlanControlStatus.AlreadyCancelled, request, plan,
                    "The plan has already been cancelled.");

            switch (request.ControlType)
            {
                case StrategicPlanControlType.Status:
                    return Result(StrategicPlanControlStatus.Applied, request, plan,
                        "Plan status observed.");
                case StrategicPlanControlType.Pause:
                    if (plan.Status == StrategicPlanStatus.Paused)
                        return Result(StrategicPlanControlStatus.AlreadyPaused, request, plan,
                            "The plan is already paused.");
                    if (plan.Revision == int.MaxValue)
                        return Result(StrategicPlanControlStatus.Rejected, request, plan,
                            "Plan revision exhausted.");
                    for (int i = 0; i < plan.ChildGoalIds.Count; i++)
                        goalManager.SuspendGoal(plan.ChildGoalIds[i]);
                    plan.PausedAtTick = goalManager.CurrentTick;
                    plan.Status = StrategicPlanStatus.Paused;
                    plan.AdvanceRevision();
                    PublishPlanStatus(plan);
                    return Result(StrategicPlanControlStatus.Applied, request, plan,
                        "Plan paused; already-dispatched actions may finish.");
                case StrategicPlanControlType.Resume:
                    if (plan.Status != StrategicPlanStatus.Paused)
                        return Result(StrategicPlanControlStatus.AlreadyRunning, request, plan,
                            "The plan is already running.");
                    long elapsed = (long)goalManager.CurrentTick - plan.PausedAtTick;
                    if (elapsed < 0 || elapsed > int.MaxValue || plan.Revision == int.MaxValue)
                        return Result(StrategicPlanControlStatus.Rejected, request, plan,
                            "The pause interval cannot be resumed safely.");
                    for (int i = 0; i < plan.ChildGoalIds.Count; i++)
                        if (!goalManager.CanResumeGoal(plan.ChildGoalIds[i], (int)elapsed))
                            return Result(StrategicPlanControlStatus.Rejected, request, plan,
                                "A goal tick anchor cannot be resumed safely.");
                    for (int i = 0; i < plan.ChildGoalIds.Count; i++)
                        goalManager.ResumeGoal(plan.ChildGoalIds[i], (int)elapsed);
                    plan.PausedAtTick = -1;
                    plan.Status = StrategicPlanStatus.Active;
                    plan.AdvanceRevision();
                    PublishPlanStatus(plan);
                    if (plan.CurrentMilestone != null
                        && plan.CurrentMilestone.Status == StrategicMilestoneStatus.Active
                        && plan.CurrentMilestone.IsSatisfied)
                        CompleteMilestoneAndAdvance(plan, plan.CurrentMilestone);
                    return Result(StrategicPlanControlStatus.Applied, request, plan,
                        "The same plan resumed.");
                case StrategicPlanControlType.Cancel:
                    if (plan.Revision == int.MaxValue)
                        return Result(StrategicPlanControlStatus.Rejected, request, plan,
                            "Plan revision exhausted.");
                    CancelPlan(plan.StrategicPlanId);
                    return Result(StrategicPlanControlStatus.Applied, request, plan,
                        "The plan was cancelled.");
                default:
                    return Result(StrategicPlanControlStatus.Rejected, request, plan,
                        "Unknown plan control action.");
            }
        }

        private static StrategicPlanControlResult Result(StrategicPlanControlStatus status,
            StrategicPlanControlRequest request, StrategicPlan plan, string message)
        {
            return new StrategicPlanControlResult(status, request, plan, message);
        }
    }
}



## Added file Assets/Tests/EditMode/CommanderPhase4D1Tests.cs

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4D1")]
    public sealed class CommanderPhase4D1Tests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
        }

        [TearDown]
        public void TearDown()
        {
            planner?.Dispose();
            goals?.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void PauseStrategy_StopsFutureStrategicProgress()
        {
            StrategicPlan plan = StartPlan();
            int milestone = plan.CurrentMilestone.MilestoneId;
            int children = plan.ChildGoalIds.Count;
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            for (int tick = 30; tick <= 300; tick += 30)
            {
                planner.Tick(tick);
                goals.Tick(tick);
            }
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            Assert.That(plan.CurrentMilestone.MilestoneId, Is.EqualTo(milestone));
            Assert.That(plan.ChildGoalIds.Count, Is.EqualTo(children));
        }

        [Test]
        public void ResumeStrategy_ContinuesSamePlan()
        {
            StrategicPlan plan = StartPlan();
            int id = plan.StrategicPlanId;
            int children = plan.ChildGoalIds.Count;
            Apply(Capture(0, plan, "Pause"));
            Assert.That(Apply(Capture(0, plan, "Resume")), Is.EqualTo("Applied"));
            Assert.That(plan.StrategicPlanId, Is.EqualTo(id));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(plan.ChildGoalIds.Count, Is.EqualTo(children));
        }

        [Test]
        public void CancelStrategy_ReleasesReservations()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId), Is.Not.Empty);
            Assert.That(Apply(Capture(0, plan, "Cancel")), Is.EqualTo("Applied"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(value => value.Status == StrategicResourceReservationStatus.Cancelled), Is.True);
            Assert.That(planner.GetReservedAmountForPlan(plan.StrategicPlanId, ResourceType.Food), Is.Zero);
            Assert.That(planner.GetReservedAmountForPlan(plan.StrategicPlanId, ResourceType.Gold), Is.Zero);
        }

        [Test]
        public void CancelStrategy_DoesNotDeleteCompletedAssets()
        {
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            var building = simulation.CreateBuilding(0, BuildingType.House, x + 15, z, false);
            StrategicPlan plan = StartPlan();
            Apply(Capture(0, plan, "Cancel"));
            Assert.That(simulation.BuildingRegistry.GetBuilding(building.Id), Is.SameAs(building));
        }

        [Test]
        public void CancelStrategy_DoesNotAffectUnrelatedPlayerCommands()
        {
            StrategicPlan plan = StartPlan();
            var manual = goals.SubmitResourceAllocation(ResourceType.Gold, 1);
            Apply(Capture(0, plan, "Cancel"));
            Assert.That(manual.IsTerminal, Is.False);
            goals.Tick(30);
            Assert.That(manual.Status, Is.Not.EqualTo(CommanderGoalStatus.Cancelled));
        }

        [Test]
        public void StalePauseRequest_CannotPauseReplacementPlan()
        {
            StrategicPlan old = StartPlan();
            StrategicPlanControlRequest token = Capture(0, old, "Pause");
            planner.CancelPlan(old.StrategicPlanId);
            StrategicPlan replacement = StartPlan();
            Assert.That(Apply(token), Is.EqualTo("StalePlan"));
            Assert.That(replacement.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void StaleCancelRequest_CannotCancelReplacementPlan()
        {
            StrategicPlan old = StartPlan();
            StrategicPlanControlRequest token = Capture(0, old, "Cancel");
            planner.CancelPlan(old.StrategicPlanId);
            StrategicPlan replacement = StartPlan();
            Assert.That(Apply(token), Is.EqualTo("StalePlan"));
            Assert.That(replacement.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [TestCase("Pause")]
        [TestCase("Cancel")]
        [TestCase("Resume")]
        public void CrossPlayerControl_IsRejected(string action)
        {
            StrategicPlan plan = StartPlan();
            StrategicPlanControlRequest token = Capture(1, plan, action);
            Assert.That(Apply(token), Is.EqualTo("Unauthorized"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void LifecycleCommands_CannotBypassOwnership()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(Apply(Capture(1, plan, "Cancel")), Is.EqualTo("Unauthorized"));
            Assert.That(plan.IsTerminal, Is.False);
        }

        [Test]
        public void SameLifecycleStateProducesSameResult()
        {
            StrategicPlan plan = StartPlan();
            string first = Apply(Capture(0, plan, "Status"));
            string second = Apply(Capture(0, plan, "Status"));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.EqualTo("Applied"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void UnknownAction_IsRejectedWithoutMutation()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(Apply(Capture(0, plan, "99")), Is.EqualTo("Rejected"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void MultipleActivePlans_CurrentCaptureIsAmbiguous()
        {
            StrategicPlan first = StartPlan();
            StrategicPlan second = StartPlan();
            Assert.That(first.IsTerminal || second.IsTerminal, Is.False);
            Assert.That(CaptureCurrent(0, "Pause"), Is.EqualTo("AmbiguousPlan"));
            Assert.That(first.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(second.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void Reset_ClearsLifecycleState()
        {
            StrategicPlan plan = StartPlan();
            StrategicPlanControlRequest old = Capture(0, plan, "Pause");
            StrategicPlanner oldPlanner = planner;
            planner.Dispose();
            goals.Dispose();
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            StrategicPlan newPlan = StartPlan();
            Assert.That(ApplyOn(oldPlanner, old), Is.EqualTo("StalePlan"));
            Assert.That(newPlan.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void LongPause_PreservesGoalDurationAndRetryBudget()
        {
            StrategicPlan plan = StartPlan();
            int[] ids = plan.ChildGoalIds.ToArray();
            Apply(Capture(0, plan, "Pause"));
            goals.Tick(36030);
            planner.Tick(36030);
            Assert.That(ids.Select(goals.GetGoal).All(goal => goal == null || !goal.IsTerminal), Is.True);
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
        }

        [Test]
        public void LongPause_ResumeShiftsGoalDurationAnchor()
        {
            StrategicPlan plan = StartPlan();
            CommanderGoal child = goals.GetGoal(plan.ChildGoalIds[0]);
            int initialAnchor = child.CreatedTick - child.MaxDurationTicks + 60;
            typeof(CommanderGoal).GetProperty("CreatedTick").SetValue(child, initialAnchor);
            Apply(Capture(0, plan, "Pause"));
            for (int i = 0; i < 120; i++) simulation.Tick();
            goals.Tick(simulation.CurrentTick);
            Assert.That(child.IsTerminal, Is.False);
            Assert.That(Apply(Capture(0, plan, "Resume")), Is.EqualTo("Applied"));
            Assert.That(child.CreatedTick, Is.EqualTo(initialAnchor + 120));
            goals.Tick(simulation.CurrentTick + 15);
            Assert.That(child.Status, Is.Not.EqualTo(CommanderGoalStatus.Failed));
        }

        [Test]
        public void PauseStrategy_ManualGoalStillTicks()
        {
            StrategicPlan plan = StartPlan();
            CommanderGoal manual = goals.SubmitResourceAllocation(ResourceType.Stone, 1);
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            goals.Tick(15);
            Assert.That(manual.Status, Is.Not.EqualTo(CommanderGoalStatus.Pending));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
        }

        [Test]
        public void PauseStrategy_RetainsAndProtectsExistingReservations()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            var reservation = planner.GetReservationsForPlan(plan.StrategicPlanId).First();
            int amount = reservation.Amount;
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            Assert.That(planner.UpdateReservationAmount(reservation.ReservationId, amount + 1), Is.False);
            Assert.That(planner.ReleaseReservation(reservation.ReservationId), Is.False);
            Assert.That(reservation.Amount, Is.EqualTo(amount));
            Assert.That(reservation.Status, Is.EqualTo(StrategicResourceReservationStatus.Active));
        }

        [Test]
        public void CancelPausedPlan_ReleasesItsReservations()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId), Is.Not.Empty);
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            Assert.That(Apply(Capture(0, plan, "Cancel")), Is.EqualTo("Applied"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(value => value.Status == StrategicResourceReservationStatus.Cancelled), Is.True);
        }

        [Test]
        public void StaleRevisionRequest_CannotPauseChangedPlan()
        {
            StrategicPlan plan = StartPlan();
            StrategicPlanControlRequest stale = Capture(0, plan, "Pause");
            Assert.That(Apply(Capture(0, plan, "Status")), Is.EqualTo("Applied"));
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            Assert.That(Apply(stale), Is.EqualTo("StalePlan"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
        }

        [Test]
        public void ResumeStrategy_ShiftsBlockedAndConstructionAnchors()
        {
            StrategicPlan plan = StartPlan();
            CommanderGoal child = goals.GetGoal(plan.ChildGoalIds[0]);
            child.BlockedSinceTick = 10;
            child.NextBlockedRetryTick = 160;
            child.LastEconomyCommandTick = 5;
            child.ObservedConstructionBuildingId = 1;
            child.LastConstructionProgressTick = 12;
            child.LastConstructionRecoveryTick = 14;
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            for (int i = 0; i < 120; i++) simulation.Tick();
            Assert.That(Apply(Capture(0, plan, "Resume")), Is.EqualTo("Applied"));
            Assert.That(child.BlockedSinceTick, Is.EqualTo(130));
            Assert.That(child.NextBlockedRetryTick, Is.EqualTo(280));
            Assert.That(child.LastEconomyCommandTick, Is.EqualTo(125));
            Assert.That(child.LastConstructionProgressTick, Is.EqualTo(132));
            Assert.That(child.LastConstructionRecoveryTick, Is.EqualTo(134));
        }

        private StrategicPlan StartPlan()
        {
            RichWorld();
            StrategicPlan plan = planner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
            Assert.That(plan, Is.Not.Null);
            return plan;
        }

        private int Resource(ResourceType type)
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        private void RichWorld()
        {
            var ages = (int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation);
            ages[0] = 3;
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            for (int i = 0; i < 8; i++)
            {
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x - 10 + i, z), Fixed32.One,
                    Fixed32.One, Fixed32.One);
                worker.UnitType = 0;
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
            }
        }

        private StrategicPlanControlRequest Capture(int playerId, StrategicPlan plan, string action)
        {
            StrategicPlanControlType control = action == "99" ? (StrategicPlanControlType)99
                : (StrategicPlanControlType)Enum.Parse(typeof(StrategicPlanControlType), action);
            if (playerId == planner.PlayerId && Enum.IsDefined(typeof(StrategicPlanControlType), control))
            {
                Assert.That(planner.CaptureControlRequest(playerId, plan.StrategicPlanId,
                    control, out StrategicPlanControlRequest captured), Is.True);
                return captured;
            }
            // Forge only malformed/foreign detached values to exercise ApplyControl's second check.
            ConstructorInfo constructor = typeof(StrategicPlanControlRequest).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic).Single();
            return (StrategicPlanControlRequest)constructor.Invoke(new object[] { playerId,
                plan.StrategicPlanId, plan.CreatedTick, plan.Revision, control });
        }

        private string CaptureCurrent(int playerId, string action)
        {
            bool result = planner.CaptureCurrentControlRequest(playerId,
                (StrategicPlanControlType)Enum.Parse(typeof(StrategicPlanControlType), action),
                out _);
            return result ? "Applied" : "AmbiguousPlan";
        }

        private string Apply(StrategicPlanControlRequest token) => ApplyOn(planner, token);

        private string ApplyOn(StrategicPlanner target, StrategicPlanControlRequest token)
        {
            return target.ApplyControl(token).Status.ToString();
        }
    }
}



