# Frozen Task3 native-correctness review package

Baseline: task-3-baseline snapshots; SemanticProvider snapshot refreshed after clean
name-confirmation fix5. No commits; preserve all earlier dirty work.
Requirements: task-3-brief.md; exact source seams task-3-preparation.md.
Report: task-3-report.md, hashes measured after final source/test compile.
Parent inspected saved final8e58f33fdcfb41d28f7ea1f4bd9ecd3e26/26, 0failed/skipped.
Some first job full payloads expired; contemporaneous compact failures retained.
No native real-time/live-model/UI/build/full-regression acceptance claim.

## Incremental ten-file source/test diffs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__CommanderGoal.cs.snapshot" b/Assets/Scripts/AI/Commander/CommanderGoal.cs
index 05a2280..7b7edae 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__CommanderGoal.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/CommanderGoal.cs
@@ -297,24 +297,28 @@ namespace OpenEmpires
             RequestedTarget = requestedTarget;
             TargetAge = targetAge;
         }
     }
 
     public sealed class CommanderCapabilityGoal : CommanderGoal
     {
         internal CommanderTargetBinding TargetBinding { get; set; }
         public CapabilityActionIntent Action { get; }
         internal bool CommandIssued { get; set; }
         internal ICommand IssuedCommand { get; set; }
         internal int CommandIssuedSimulationTick { get; set; } = -1;
+        internal FixedVector3[] NativeMovementSlots { get; set; }
+        internal FixedVector3[] NativeMovementEndpoints { get; set; }
+        internal long NativeRemainingDistance { get; set; } = long.MaxValue;
+        internal int NativeLastProgressSimulationTick { get; set; } = -1;
         // Runtime-only typed result source. This prevents provider node indices from
         // becoming entity references and prevents cross-manager/stale-result reuse.
         internal CommanderGoal ResultSourceGoal { get; set; }
         internal bool RequiresResultBinding { get; set; }
         internal bool ResultHumanOverride { get; set; }
 
         public CommanderCapabilityGoal(CapabilityActionIntent action, int maxDurationTicks = 36000)
             : base(action?.PlayerId ?? throw new ArgumentNullException(nameof(action)),
                 CommanderGoalType.CapabilityAction, maxDurationTicks)
         {
             Action = action;
         }
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__CommanderGoalManager.cs.snapshot" b/Assets/Scripts/AI/Commander/CommanderGoalManager.cs
index 882b891..62480bf 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__CommanderGoalManager.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/CommanderGoalManager.cs
@@ -557,25 +557,27 @@ namespace OpenEmpires
                         }
                         bool waitingChanged = goal.SetStatus(CommanderGoalStatus.WaitingForPrerequisite,
                             dependencyReason);
                         if (waitingChanged)
                         {
                             GoalStatusChanged?.Invoke(goal);
                             PublishEvent(CommanderGoalEventType.GoalProgressChanged, goal, currentTick);
                         }
                         if (ActiveGoal == null) ActiveGoal = goal;
                         continue;
                     }
                     CommanderPlan plan = planner.Plan(goal, currentTick);
-                    if (plan.Command != null && goal is AllocateWorkersGoal workers && !workers.ReservationsAcquired)
+                    if (goal is AllocateWorkersGoal workers && workers.Prepared
+                        && workers.SelectedWorkerIds.Count > 0 && !workers.ReservationsAcquired
+                        && (plan.Command != null || plan.Status == CommanderGoalStatus.WaitingForResources))
                     {
                         if (workerAuthority.TryReserveWorkers(workers.SelectedWorkerIds, goal.GoalId, currentTick))
                             workers.ReservationsAcquired = true;
                         else plan = new CommanderPlan(CommanderGoalStatus.Blocked,
                             "The full selected worker set could not be reserved; no assignments were issued.", 0, 0);
                     }
                     if (plan.Command != null && !workerAuthority.TryReserveCommand(goal, plan.Command, currentTick))
                         plan = new CommanderPlan(CommanderGoalStatus.Blocked,
                             "Worker is protected or reserved by another goal.", plan.OwnedCount, plan.QueuedCount);
                     goal.LastObservedOwnedCount = plan.OwnedCount;
                     goal.LastPlannerObservationTick=currentTick;
                     goal.LastObservedQueuedCount = plan.QueuedCount;
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__CommanderPlanner.cs.snapshot" b/Assets/Scripts/AI/Commander/CommanderPlanner.cs
index 4217867..b1e3d54 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__CommanderPlanner.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/CommanderPlanner.cs
@@ -114,61 +114,88 @@ namespace OpenEmpires
                 if (building.ResultCaptureTick < 0 || building.ResultBuildingIds == null
                     || building.ResultBuildingIds.Count != 1)
                     return new CommanderPlan(CommanderGoalStatus.WaitingForConstruction,
                         "Waiting for the referenced structure result; no unrelated building may substitute.", 0, 0);
                 binding = CommanderResultBinding.ForBuilding(building.ResultBuildingIds[0],
                     building.GoalId, building.CreatedTick, simulation, goal.RuntimeOwner);
             }
             ICommand command;
             string reason;
             bool created = executor.TryCreateCommand(goal.Action, binding, goal.TargetBinding, out command, out reason);
             if (!created)
                 return new CommanderPlan(CommanderGoalStatus.Blocked, reason, 0, 0);
+            if (command is MoveCommand move
+                && (goal.Action.ActionType == CommanderCapabilityActionType.MoveUnits
+                    || goal.Action.ActionType == CommanderCapabilityActionType.DefendArea
+                    || goal.Action.ActionType == CommanderCapabilityActionType.RetreatUnits
+                    || goal.Action.ActionType == CommanderCapabilityActionType.ScoutArea))
+            {
+                if (!TryResolveNativeMovementSlots(goal, move, currentTick, out move, out reason))
+                    return new CommanderPlan(CommanderGoalStatus.Blocked, reason, 0, 0);
+                command = move;
+            }
             return new CommanderPlan(CommanderGoalStatus.Executing, reason, 0, 0, command);
         }
 
         private CommanderPlan ObserveCapability(CommanderCapabilityGoal goal, int currentTick)
         {
             ICommand issued = goal.IssuedCommand;
             if (issued == null)
                 return new CommanderPlan(CommanderGoalStatus.Blocked,
                     "Capability command reservation was not committed; retrying safely.", 0, 0);
 
             int[] subjects = CommanderWorkerAuthority.GetSubjectUnitIds(issued);
             int eligible = 0;
             if (subjects != null)
             {
                 for (int i = 0; i < subjects.Length; i++)
                 {
                     UnitData unit = simulation.UnitRegistry.GetUnit(subjects[i]);
-                    if (unit != null && unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
-                        && unit.State != UnitState.Dead && !workerAuthority.IsHumanProtected(unit.Id, currentTick))
-                        eligible++;
+                    if (unit == null)
+                    {
+                        UnitData sheltered = simulation.UnitRegistry.GetGarrisonedUnit(subjects[i]);
+                        return new CommanderPlan(CommanderGoalStatus.Blocked,
+                            sheltered != null ? "An exact capability subject is garrisoned; no substitute is authorized."
+                                : "An exact capability subject is missing; no substitute is authorized.", eligible, 0);
+                    }
+                    if (unit.CurrentHealth <= 0 || unit.State == UnitState.Dead)
+                        return new CommanderPlan(CommanderGoalStatus.Blocked,
+                            "An exact capability subject is dead; no substitute is authorized.", eligible, 0);
+                    if (unit.PlayerId != goal.PlayerId)
+                        return new CommanderPlan(CommanderGoalStatus.Blocked,
+                            "An exact capability subject is foreign-owned; no substitute is authorized.", eligible, 0);
+                    if (workerAuthority.IsHumanProtected(unit.Id, currentTick))
+                        return new CommanderPlan(CommanderGoalStatus.Blocked,
+                            "The player took control of an exact capability subject; Commander will not reclaim it.", eligible, 0);
+                    eligible++;
                 }
-                if (eligible == 0)
-                    return new CommanderPlan(CommanderGoalStatus.Blocked,
-                        "The player took control of every eligible capability unit; Commander will not reclaim them.", 0, 0);
             }
 
             switch (goal.Action.ActionType)
             {
                 case CommanderCapabilityActionType.MoveUnits:
                 case CommanderCapabilityActionType.DefendArea:
                 case CommanderCapabilityActionType.RetreatUnits:
                 case CommanderCapabilityActionType.ScoutArea:
-                    if (issued is MoveCommand move && AllUnitsAt(subjects, move.TargetPosition, currentTick))
+                    string movementReason = "The exact issued movement command is unavailable.";
+                    if (issued is MoveCommand move && ObserveNativeMovement(goal, move, subjects, currentTick, out movementReason))
                         return new CommanderPlan(CommanderGoalStatus.Completed,
                             "The movement objective is satisfied in simulation state.", eligible, 0);
+                    if (movementReason.StartsWith("The exact native formation made no progress", StringComparison.Ordinal)
+                        || movementReason.StartsWith("A native route stopped short", StringComparison.Ordinal)
+                        || movementReason.StartsWith("An exact subject has a replacement", StringComparison.Ordinal)
+                        || movementReason.StartsWith("An exact movement subject is temporarily unavailable", StringComparison.Ordinal))
+                        return new CommanderPlan(CommanderGoalStatus.Blocked, movementReason, eligible, 0);
                     return new CommanderPlan(CommanderGoalStatus.Executing,
-                        "Waiting for the selected units to reach the resolved location.", eligible, 0);
+                        movementReason, eligible, 0);
 
                 case CommanderCapabilityActionType.PatrolArea:
                     if (issued is PatrolCommand patrol)
                     {
                         int patrolling = CountPatrolling(subjects, currentTick);
                         if (patrolling > 0)
                             return new CommanderPlan(CommanderGoalStatus.Executing,
                                 $"Patrol is active for {patrolling} eligible unit(s).", patrolling, 0);
                     }
                     return new CommanderPlan(CommanderGoalStatus.Executing,
                         "Patrol remains an active persistent Commander objective.", eligible, 0);
 
@@ -218,41 +245,127 @@ namespace OpenEmpires
                 case CommanderCapabilityActionType.ResearchTechnology:
                     if (goal.Action.Technology.HasValue && simulation.HasTechnology(goal.PlayerId, goal.Action.Technology.Value))
                         return new CommanderPlan(CommanderGoalStatus.Completed,
                             "The requested technology is researched in simulation state.", 1, 0);
                     return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                         "Waiting for the requested technology research to complete.", 0, 0);
                 default:
                     return new CommanderPlan(CommanderGoalStatus.Failed,
                         "Unsupported Commander capability state.", 0, 0);
             }
         }
 
-        private bool AllUnitsAt(int[] subjects, FixedVector3 target, int currentTick)
+        private bool TryResolveNativeMovementSlots(CommanderCapabilityGoal goal, MoveCommand original,
+            int currentTick, out MoveCommand move, out string reason)
         {
-            if (subjects == null || subjects.Length == 0) return false;
-            int eligible = 0;
-            Fixed32 tolerance = Fixed32.FromInt(1);
-            Fixed32 toleranceSquared = tolerance * tolerance;
+            move = original;
+            reason = "No complete known reachable formation objective exists near the requested location.";
+            if (original.UnitIds == null || original.UnitIds.Length == 0 || original.UnitIds.Length > 200) return false;
+            Vector2Int anchor = simulation.MapData.WorldToTile(original.TargetPosition);
+            if (!simulation.MapData.IsInBounds(anchor.x, anchor.y)) return false;
+            var occupied = new HashSet<Vector2Int>();
+            var slots = new FixedVector3[original.UnitIds.Length];
+            for (int i = 0; i < original.UnitIds.Length; i++)
+            {
+                UnitData unit = simulation.UnitRegistry.GetUnit(original.UnitIds[i]);
+                if (unit == null || unit.PlayerId != goal.PlayerId || unit.CurrentHealth <= 0
+                    || unit.State == UnitState.Dead || workerAuthority.IsHumanProtected(unit.Id, currentTick)) return false;
+                Vector2Int start = simulation.MapData.WorldToTile(unit.SimPosition);
+                bool found = false;
+                for (int radius = 0; radius <= 10 && !found; radius++)
+                    for (int dx = -radius; dx <= radius && !found; dx++)
+                        for (int dz = -radius; dz <= radius && !found; dz++)
+                        {
+                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius) continue;
+                            int x = anchor.x + dx, z = anchor.y + dz;
+                            var destination = new Vector2Int(x, z);
+                            if (occupied.Contains(destination)
+                                || !simulation.MapData.IsWalkable(x, z, goal.PlayerId, simulation.BuildingRegistry)
+                                || !IsKnownTile(goal.PlayerId, x, z)) continue;
+                            if (!GridPathfinder.TryFindCompletePath(simulation.MapData, start, destination,
+                                out List<Vector2Int> path, goal.PlayerId, simulation.BuildingRegistry)
+                                || !IsKnownPath(goal.PlayerId, start, path)) continue;
+                            occupied.Add(destination);
+                            slots[i] = simulation.MapData.TileToWorldFixed(x, z);
+                            found = true;
+                        }
+                if (!found) return false;
+            }
+            goal.NativeMovementSlots = slots;
+            move.TargetPosition = slots[0];
+            move.FormationPositions = slots;
+            reason = "Moving exact selected units to known reachable native formation positions.";
+            return true;
+        }
+
+        private bool ObserveNativeMovement(CommanderCapabilityGoal goal, MoveCommand move,
+            int[] subjects, int currentTick, out string reason)
+        {
+            reason = "Waiting for the exact selected units to reach their native formation endpoints.";
+            if (subjects == null || subjects.Length == 0 || goal.NativeMovementSlots == null
+                || goal.NativeMovementSlots.Length != subjects.Length) return false;
+            if (simulation.CurrentTick <= goal.CommandIssuedSimulationTick) return false;
+            for (int i = 0; i < subjects.Length; i++)
+            {
+                UnitData current = simulation.UnitRegistry.GetUnit(subjects[i]);
+                if (current == null || current.State != UnitState.Moving && current.State != UnitState.Idle)
+                {
+                    reason = "An exact movement subject is temporarily unavailable; no replacement or takeover is inferred.";
+                    return false;
+                }
+            }
+            if (goal.NativeMovementEndpoints == null)
+            {
+                var endpoints = new FixedVector3[subjects.Length];
+                for (int i = 0; i < subjects.Length; i++)
+                {
+                    UnitData unit = simulation.UnitRegistry.GetUnit(subjects[i]);
+                    if (unit == null) return false;
+                    endpoints[i] = unit.HasPath ? unit.FinalDestination : unit.SimPosition;
+                    if (!Near(endpoints[i], goal.NativeMovementSlots[i], Fixed32.FromInt(2)))
+                    {
+                        reason = "A native route stopped short of its validated formation slot; no distant idle position counts as arrival.";
+                        return false;
+                    }
+                }
+                goal.NativeMovementEndpoints = endpoints;
+                goal.NativeLastProgressSimulationTick = simulation.CurrentTick;
+            }
+            long remaining = 0;
+            bool allArrived = true;
             for (int i = 0; i < subjects.Length; i++)
             {
                 UnitData unit = simulation.UnitRegistry.GetUnit(subjects[i]);
-                if (unit == null || unit.PlayerId < 0 || unit.CurrentHealth <= 0
-                    || unit.State == UnitState.Dead || workerAuthority.IsHumanProtected(unit.Id, currentTick)) continue;
-                eligible++;
-                Fixed32 dx = unit.SimPosition.x - target.x;
-                Fixed32 dz = unit.SimPosition.z - target.z;
-                if (dx * dx + dz * dz > toleranceSquared) return false;
-            }
-            return eligible > 0;
+                if (unit == null || !Near(unit.FinalDestination, goal.NativeMovementEndpoints[i], Fixed32.FromInt(2))
+                    && unit.HasPath)
+                { reason = "An exact subject has a replacement native order; unrelated movement cannot satisfy this request."; return false; }
+                Fixed32 dx = unit.SimPosition.x - goal.NativeMovementEndpoints[i].x;
+                Fixed32 dz = unit.SimPosition.z - goal.NativeMovementEndpoints[i].z;
+                remaining += Math.Abs((long)dx.Raw) + Math.Abs((long)dz.Raw);
+                if (unit.State != UnitState.Idle || !Near(unit.SimPosition, goal.NativeMovementEndpoints[i], Fixed32.FromInt(1)))
+                    allArrived = false;
+            }
+            if (allArrived) return true;
+            if (remaining < goal.NativeRemainingDistance)
+            { goal.NativeRemainingDistance = remaining; goal.NativeLastProgressSimulationTick = simulation.CurrentTick; }
+            if (simulation.CurrentTick - goal.NativeLastProgressSimulationTick >= 150)
+                reason = "The exact native formation made no progress for 150 simulation ticks; distant idle units are not complete.";
+            return false;
+        }
+
+        private static bool Near(FixedVector3 a, FixedVector3 b, Fixed32 tolerance)
+        {
+            long dx = (long)a.x.Raw - b.x.Raw, dz = (long)a.z.Raw - b.z.Raw;
+            long limit = tolerance.Raw;
+            return dx * dx + dz * dz <= limit * limit;
         }
 
         private int CountPatrolling(int[] subjects, int currentTick)
         {
             int count = 0;
             if (subjects == null) return count;
             for (int i = 0; i < subjects.Length; i++)
             {
                 UnitData unit = simulation.UnitRegistry.GetUnit(subjects[i]);
                 if (unit != null && unit.IsPatrolling && !workerAuthority.IsHumanProtected(unit.Id, currentTick)) count++;
             }
             return count;
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__CommanderTacticalStatusSnapshot.cs.snapshot" b/Assets/Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs
index 75defdd..28cd626 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__CommanderTacticalStatusSnapshot.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs
@@ -103,24 +103,33 @@ namespace OpenEmpires
             string counts=step.RequestedNew.HasValue?$"{step.Produced}/{step.RequestedNew.Value} new completed, {step.AttributedQueued+step.Pending} attributable queued or in flight. "
                 : step.Requested>0?$"Owned {step.Owned}; requested {step.Requested} total/count; queued {step.Queued}. ":"";
             string reason=string.IsNullOrEmpty(step.Reason)?"No status reason has been recorded yet.":"Reported status reason: "+step.Reason;
             return request.Label+": "+counts+reason+(step.HumanOverride?" Human control is protected.":"")
                 +(Truncated?" Some steps are omitted.":"");
         }
     }
 
     internal static class CommanderTacticalStatusProjection
     {
         internal static bool IsStatusQuestion(string input) => Regex.IsMatch(input??"",
             @"\b(status|progress|progressing|doing|waiting|finished|ready|blocked|stuck|request|army)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
+        internal static bool IsCausalTaskQuestion(string input,GameSimulation sim,CommanderGoalManager manager)
+        {
+            string query=(input??"").Trim();
+            if(!Regex.IsMatch(query,@"^(why|what happened|how come|what went wrong)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return false;
+            if(Regex.IsMatch(query,@"\b(task|order|goal|arriv(?:e|ed|ing)|stop(?:ped)?|staff(?:ed|ing)?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
+            var catalog=GameKnowledgeCatalog.Build(sim);
+            return catalog.Units.Any(u=>Mention(query,u.DisplayName)||u.Aliases.Any(a=>Mention(query,a)))
+                ||catalog.Buildings.Any(b=>Mention(query,b.DisplayName)||b.Aliases.Any(a=>Mention(query,a)));
+        }
         internal static bool IsInformationQuestion(string input)
         {
             string text=(input??"").Trim();
             if(Regex.IsMatch(text,@"^(why|what|how|when|where|which|who|is|are|did|does|have|has)\b|^(tell me|explain|show me|report|i want to know|can my civilization|status|progress|current strategy status|strategy status)\b",
                 RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
             if(Regex.IsMatch(text,@"^can (i|we) (make|train|build) (?:a |an )?[a-z]+\s*[?.!]*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
             if(Regex.IsMatch(text,@"^(can|could|would|will) you (?:(tell|show|remind) me\b|(?:explain|report)\b)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
             return text.EndsWith("?",StringComparison.Ordinal)
                 && !Regex.IsMatch(text,@"^(can|could|would|will) (you|we|i)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
                 && !SimpleTextIntentParser.IsExplicitOrderForm(text);
         }
 
@@ -176,25 +185,25 @@ namespace OpenEmpires
             }
             int queued=sim.BuildingRegistry.GetAllBuildings().Where(b=>b.PlayerId==manager.PlayerId&&!b.IsDestroyed).Sum(b=>b.TrainingQueue.Count)
                 +sim.CountPendingTrainingOrders(playerId:manager.PlayerId);
             return new CommanderTacticalStatusSnapshot(sim.CurrentTick,sim.GetPopulation(manager.PlayerId),sim.GetPopulationCap(manager.PlayerId),queued,
                 sim.Config.MaxPopulation,groups.Count==0?"None":groups.Count==1?"One":"Ambiguous",requests,truncated);
         }
 
         private static bool Mention(string input,string term)=>!string.IsNullOrWhiteSpace(term)&&Regex.IsMatch(input??"",@"(?<!\w)"+Regex.Escape(term)+@"(?!\w)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
         private static string Reason(string value)
         {
             string text=Regex.Replace(value??"",@"#\d+|\b(?:goal|building|unit|worker|producer)\s+(?:id\s*)?\d+\b","the resolved entity",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
             text=Regex.Replace(text,@"\(\s*-?\d+\s*,\s*-?\d+\s*\)","the resolved location");
-            text=Regex.Replace(text,@"\s+"," ").Trim();return text.Length<=160?text:text.Substring(0,157)+"...";
+            text=Regex.Replace(text,@"\s+"," ").Trim();return text.Length<=160?text:"The full blocker is unavailable within the bounded task-fact projection.";
         }
         private static CommanderTacticalStepSnapshot Step(GameSimulation sim,CommanderGoalManager manager,CommanderGoal goal)
         {
             int owned=goal.LastObservedOwnedCount,produced=0,queued=goal.LastObservedQueuedCount,attributed=0,pending=0,ready=0,unfinished=0,requested=0;
             int? newCount=null;KnowledgeCost deficit=null;bool takeover=goal.FrozenWorkerHumanOverride;bool? target=null;
             string label=goal.GoalType.ToString(),state=goal.Status.ToString(),reason=Reason(goal.StatusReason);
             if(goal is EnsureUnitCountGoal units)
             {
                 requested=units.TargetTotal;newCount=units.IsExplicitNewProduction||units.HasResultConsumer?units.RequiredNewProductionCount:(int?)null;
                 int resolved=sim.ResolveCivUnitType(goal.PlayerId,units.RequestedUnitType);
                 label=(units.IsExplicitNewProduction?units.RequiredNewProductionCount+" new ":units.TargetTotal+" total ")+KeybindManager.GetUnitTypeDisplayName(resolved);
                 owned=CommanderProductionProjection.LivingOwnedIds(sim,goal.PlayerId,resolved).Count;
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderPlanner.Economy.cs.snapshot" b/Assets/Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs
index f1e0c4e..ef16773 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderPlanner.Economy.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs
@@ -62,24 +62,33 @@ namespace OpenEmpires
                     if (requested == 0) return goal.Allocation.ResourceAmount.HasValue
                         ? ResourceObjectiveWaiting(goal, current)
                         : new CommanderPlan(CommanderGoalStatus.Completed, "The requested worker total is already satisfied.", current, 0);
                 }
                 List<EconomyTarget> targets = VisibleEconomyTargets(goal);
                 List<UnitData> eligible = simulation.UnitRegistry.GetAllUnits().Where(u => EligibleAllocationWorker(goal, u, currentTick)).ToList();
                 if (goal.ResultSourceGoal != null)
                 {
                     var exact = new HashSet<int>(goal.SnapshotWorkerIds);
                     eligible.RemoveAll(u => !exact.Contains(u.Id));
                 }
                 eligible.Sort((a, b) => CompareEconomyWorkers(a, b, targets));
+                if (request.Destination.Resource == ResourceType.Food
+                    && (request.Destination.SourceKind == ResourceSourceKind.Farm || request.Destination.SourceKind == ResourceSourceKind.Any)
+                    && (request.Mode == CommanderWorkerAllocationMode.SelectedCount
+                        || request.CountMode == CommanderWorkerCountMode.AllMatching)
+                    && request.Workers.State != CommanderWorkerState.Idle)
+                    eligible = eligible.OrderBy(u => MatchesDestination(u, request.Destination)
+                        && simulation.MapData.GetResourceNode(u.TargetResourceNodeId)?.IsFarmNode == true
+                        && targets.Any(t => t.Kind == CommanderWorkerTargetKind.ResourceNode && t.Id == u.TargetResourceNodeId) ? 0 : 1)
+                        .ThenBy(u => WorkerPriority(u)).ThenBy(u => u.Id).ToList();
                 if (request.CountMode == CommanderWorkerCountMode.AllMatching)
                 {
                     if (!goal.SnapshotCaptured)
                     { goal.SnapshotWorkerIds.AddRange(eligible.Select(u => u.Id)); goal.SnapshotCaptured = true; }
                     requested = goal.SnapshotWorkerIds.Count;
                     if (requested == 0) return AllocationBlocked("No eligible workers matched the one-time snapshot.");
                     if (requested > Math.Min(200, simulation.Config.MaxPopulation)) return AllocationBlocked("The matching worker snapshot exceeds the allowed bound.");
                     var captured = new HashSet<int>(goal.SnapshotWorkerIds);
                     eligible.RemoveAll(u => !captured.Contains(u.Id));
                 }
                 if (eligible.Count < requested) return AllocationBlocked($"Only {eligible.Count} eligible workers are available; {requested} are required.");
                 if (targets.Count == 0) return AllocationBlocked($"No usable visible {request.Destination.SourceKind}/{request.Destination.Resource} source is known.");
@@ -96,25 +105,30 @@ namespace OpenEmpires
                         transfers[previous.Type] = moved + 1;
                     }
                     selected.Add(worker);
                     if (selected.Count == requested) break;
                 }
                 if (selected.Count < requested) return AllocationBlocked("Protected worker floors leave too few eligible workers.");
                 if (!TryResolveWorkerAssignments(goal, selected, targets, out var assignments))
                     return AllocationBlocked("Insufficient legal source capacity or no known reachable route in the bounded candidate search; no workers were assigned.");
                 goal.Assignments.AddRange(assignments);
                 goal.SelectedWorkerIds.AddRange(selected.Select(u => u.Id));
                 foreach (var group in assignments.GroupBy(a => new { a.Kind, a.TargetId }).OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.TargetId))
                 {
-                    int[] ids = group.Select(a => a.WorkerId).OrderBy(id => id).ToArray();
+                    int[] ids = group.Where(a => !selected.Any(u => u.Id == a.WorkerId
+                        && a.Kind == CommanderWorkerTargetKind.ResourceNode
+                        && IsGatheringState(u.State) && u.TargetResourceNodeId == a.TargetId
+                        && simulation.MapData.GetResourceNode(a.TargetId)?.IsFarmNode == true))
+                        .Select(a => a.WorkerId).OrderBy(id => id).ToArray();
+                    if (ids.Length == 0) continue;
                     goal.CommandGroups.Add(group.Key.Kind == CommanderWorkerTargetKind.ResourceNode
                         ? (ICommand)new GatherCommand(goal.PlayerId, ids, group.Key.TargetId, request.Destination.SourceKind)
                         : new SlaughterSheepCommand { PlayerId = goal.PlayerId, VillagerIds = ids, SheepUnitId = group.Key.TargetId, SourceKind = request.Destination.SourceKind });
                 }
                 goal.Prepared = true;
             }
             foreach (int id in goal.SelectedWorkerIds)
             {
                 UnitData worker = simulation.UnitRegistry.GetUnit(id);
                 if (!OwnedLivingWorker(worker, goal.PlayerId) || worker.CommandQueue.Count > 0 || !workerAuthority.CanUseWorker(id, goal.GoalId, currentTick))
                     return AllocationBlocked("A selected worker is unavailable or protected; no substitute is allowed.");
             }
@@ -223,25 +237,28 @@ namespace OpenEmpires
                     && a.Kind == CommanderWorkerTargetKind.OwnedSheep && a.TargetId == worker.CombatTargetId);
         private static bool OwnedLivingWorker(UnitData u, int player) => u != null && u.PlayerId == player && u.IsVillager && !u.IsSheep
             && u.CurrentHealth > 0 && u.State != UnitState.Dead;
 
         private bool EligibleAllocationWorker(AllocateWorkersGoal goal, UnitData worker, int tick)
         {
             if (!OwnedLivingWorker(worker, goal.PlayerId) || !workerAuthority.CanUseWorker(worker.Id, goal.GoalId, tick)
                 || !CanReassignWorker(goal, worker) || (worker.State != UnitState.Idle && !IsGatheringState(worker.State))) return false;
             var request = goal.Allocation;
             if (request.Workers.State == CommanderWorkerState.Idle && worker.State != UnitState.Idle) return false;
             if (request.Workers.State == CommanderWorkerState.Gathering && !IsGatheringState(worker.State)) return false;
             if (request.Workers.CurrentResource.HasValue && !IsGatheringResource(worker, request.Workers.CurrentResource.Value)) return false;
-            return request.Mode == CommanderWorkerAllocationMode.SelectedCount || !MatchesDestination(worker, request.Destination);
+            return request.Mode == CommanderWorkerAllocationMode.SelectedCount
+                || (request.CountMode == CommanderWorkerCountMode.AllMatching
+                    && request.Mode == CommanderWorkerAllocationMode.SelectedCount)
+                || !MatchesDestination(worker, request.Destination);
         }
 
         private bool MatchesDestination(UnitData worker, CommanderResourceDestination destination)
         {
             if (worker == null || worker.CurrentHealth <= 0) return false;
             if (IsGatheringState(worker.State))
             {
                 ResourceNodeData node = simulation.MapData.GetResourceNode(worker.TargetResourceNodeId);
                 return node != null && !node.IsDepleted && node.Type == destination.Resource
                     && ResourceSourceRules.Matches(node, destination.SourceKind) && LegalFarm(node, worker.PlayerId);
             }
             if (destination.Resource == ResourceType.Food && (destination.SourceKind == ResourceSourceKind.Sheep || destination.SourceKind == ResourceSourceKind.Any)
@@ -290,48 +307,64 @@ namespace OpenEmpires
         private int WorkerPriority(UnitData u) => u.State == UnitState.Idle ? 0 : workerAuthority.IsCommanderControlled(u.Id) ? 1 : 2;
         private long TargetDistance(UnitData worker, EconomyTarget target)
         {
             Vector2Int tile = simulation.MapData.WorldToTile(worker.SimPosition);
             long dx = (long)tile.x - target.X, dz = (long)tile.y - target.Z; return dx * dx + dz * dz;
         }
 
         private bool TryResolveWorkerAssignments(AllocateWorkersGoal goal, List<UnitData> workers, List<EconomyTarget> targets,
             out List<CommanderWorkerAssignment> assignments, bool progressiveRecovery = false)
         {
             assignments = new List<CommanderWorkerAssignment>();
             var selected = new HashSet<int>(workers.Select(w => w.Id));
+            var retained = new Dictionary<int, EconomyTarget>();
+            foreach (UnitData worker in workers)
+                if (MatchesDestination(worker, goal.Allocation.Destination)
+                    && simulation.MapData.GetResourceNode(worker.TargetResourceNodeId)?.IsFarmNode == true)
+                {
+                    var target = targets.FirstOrDefault(t => t.Kind == CommanderWorkerTargetKind.ResourceNode
+                        && t.Id == worker.TargetResourceNodeId);
+                    if (targets.Any(t => t.Kind == CommanderWorkerTargetKind.ResourceNode
+                        && t.Id == worker.TargetResourceNodeId)) retained[worker.Id] = target;
+                }
             var used = new Dictionary<long, int>();
             foreach (UnitData worker in simulation.UnitRegistry.GetAllUnits())
             {
-                if (worker.CurrentHealth <= 0 || worker.State == UnitState.Dead || selected.Contains(worker.Id)) continue;
+                if (worker.CurrentHealth <= 0 || worker.State == UnitState.Dead
+                    || (selected.Contains(worker.Id) && !retained.ContainsKey(worker.Id))) continue;
                 long key = worker.State == UnitState.MovingToSlaughter
                     ? ((long)CommanderWorkerTargetKind.OwnedSheep << 32) | (uint)worker.CombatTargetId : (uint)worker.TargetResourceNodeId;
                 if (!IsGatheringState(worker.State) && worker.State != UnitState.MovingToSlaughter) continue;
                 used.TryGetValue(key, out int count); used[key] = count + 1;
             }
             foreach (CommanderGoal other in goal.RuntimeOwner.ActiveGoals)
                 if (other != goal && other is AllocateWorkersGoal pending && pending.Prepared && pending.ReservationsAcquired && !pending.IsTerminal)
                     foreach (var assignment in pending.Assignments)
                     {
                         UnitData worker = simulation.UnitRegistry.GetUnit(assignment.WorkerId);
                         if (worker == null || selected.Contains(worker.Id)) continue;
                         bool alreadyCounted = assignment.Kind == CommanderWorkerTargetKind.ResourceNode
                             ? IsGatheringState(worker.State) && worker.TargetResourceNodeId == assignment.TargetId
                             : worker.State == UnitState.MovingToSlaughter && worker.CombatTargetId == assignment.TargetId;
                         if (alreadyCounted) continue;
                         long key = ((long)assignment.Kind << 32) | (uint)assignment.TargetId;
                         used.TryGetValue(key, out int count); used[key] = count + 1;
                     }
             foreach (UnitData worker in workers)
             {
+                if (retained.TryGetValue(worker.Id, out EconomyTarget currentTarget))
+                {
+                    assignments.Add(new CommanderWorkerAssignment(worker.Id, currentTarget.Id, currentTarget.Kind));
+                    continue;
+                }
                 var candidates = targets.Where(t => !used.TryGetValue(t.Key, out int count) || count < t.Capacity)
                     .OrderBy(t => TargetDistance(worker, t)).ThenBy(t => t.Kind).ThenBy(t => t.Id).ToList();
                 int offset = 0;
                 if (progressiveRecovery) goal.RecoveryCandidateOffsets.TryGetValue(worker.Id, out offset);
                 if (offset >= candidates.Count) offset = 0; // Capacity/visibility may change between observations.
                 int end = Math.Min(candidates.Count, offset + pathValidationCandidates);
                 bool found = false;
                 for (int index = offset; index < end; index++)
                 {
                     EconomyTarget target = candidates[index];
                     Vector2Int start = simulation.MapData.WorldToTile(worker.SimPosition);
                     if (!HasReachableAdjacentTile(start, goal.PlayerId, target.X, target.Z, target.Width, target.Height)) continue;
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.ReadOnlyQuestions.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.ReadOnlyQuestions.cs
index c478223..15e832c 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.ReadOnlyQuestions.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.ReadOnlyQuestions.cs
@@ -6,25 +6,26 @@ namespace OpenEmpires
 {
     public sealed partial class CommanderChatUI
     {
         private async Task<CommanderAIChatSubmission> SubmitReadOnlyQuestionAsync(string message)
         {
             var sim=semanticSimulation;var manager=semanticGoalManager;var provider=semanticProvider;
             if(sim==null||manager==null||manager.IsDisposed||Conversation==null||Conversation.PlayerId!=manager.PlayerId)
             {AppendLine("Commander","Commander observations are not available in this runtime.",false);return null;}
             int generation=runtimeGeneration;
             bool Current()=>this!=null&&runtimeGeneration==generation&&!lifetime.IsCancellationRequested
                 &&ReferenceEquals(semanticSimulation,sim)&&ReferenceEquals(semanticGoalManager,manager)
                 &&ReferenceEquals(semanticProvider,provider)&&!manager.IsDisposed;
-            bool statusQuestion=CommanderTacticalStatusProjection.IsStatusQuestion(message);
+            bool statusQuestion=CommanderTacticalStatusProjection.IsStatusQuestion(message)
+                ||CommanderTacticalStatusProjection.IsCausalTaskQuestion(message,sim,manager);
             var status=statusQuestion?CommanderTacticalStatusProjection.Observe(sim,manager,message):null;
             TryObserveQuestionAnswer(NormalizeExplanationWholeForm(message),out string facts);
             void LocalFallback(string unavailable)
             {
                 if(!Current())return;
                 string answer=(statusQuestion?CommanderTacticalStatusProjection.Observe(sim,manager,message).Answer():facts)??unavailable;
                 AppendLine("Commander",answer,false);Conversation.Memory.RecordExplanation(answer);
             }
             AppendLine("Player",message,false);
             if(submitting||provider==null)
             {
                 string local=status?.Answer()??facts??"Another interpretation is pending; no information request started gameplay.";
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticProvider.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs
index 53db232..ef388e9 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticProvider.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs
@@ -40,25 +40,27 @@ namespace OpenEmpires
             string text = playerMessage ?? string.Empty;
             IsPlayerMessageTooLong = text.Length > MaximumPlayerMessageCharacters;
             PlayerMessage = IsPlayerMessageTooLong ? string.Empty : text;
             var knowledge = ParseKnowledge(context.KnowledgeContext);
             UnresolvedNameInterpretations = new JArray((knowledge["nameInterpretations"] as JArray ?? new JArray())
                 .OfType<JObject>().Where(h => (bool?)h["interpretationOnly"] == true && (bool?)h["ambiguous"] == false))
                 .ToString(Formatting.None);
             SerializedContext = SerializeContext(context,tacticalStatus);
             SerializedPendingClarification = pendingClarification?.Serialize() ?? string.Empty;
             if (SerializedPendingClarification.Length > 4096)
                 throw new InvalidOperationException("Pending clarification exceeds its bound.");
             string facts = questionFacts ?? tacticalStatus?.Answer() ?? string.Empty;
-            QuestionFacts = facts.Length > 512 ? facts.Substring(0, 512) : facts;
+            QuestionFacts = facts.Length > 512
+                ? "The complete answer is unavailable within the bounded question-fact projection."
+                : facts;
             IsReadOnlyQuestion=readOnlyQuestion||tacticalStatus!=null||!string.IsNullOrEmpty(questionFacts);
             SerializedSemanticMemory = CommanderSemanticConversationMemory.Serialize(semanticMemory
                 ?? Array.Empty<CommanderSemanticMemoryEntry>());
             if (SerializedSemanticMemory.Length > MaximumSemanticMemoryCharacters)
                 throw new InvalidOperationException("Semantic conversation memory exceeded its limit.");
         }
 
         private static string SerializeContext(CommanderContext context,CommanderTacticalStatusSnapshot status)
         {
             var unitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal)
             {
                 ["Villager"] = 0, ["Spearman"] = 0, ["Archer"] = 0, ["Knight"] = 0,
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Tests__EditMode__CommanderEconomyClarificationTests.Execution.cs.snapshot" b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs
index 4befa62..0be4acd 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Tests__EditMode__CommanderEconomyClarificationTests.Execution.cs.snapshot"
+++ b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs
@@ -169,24 +169,152 @@ namespace OpenEmpires.Tests
                 f.Manager.Tick(0);
                 var first = f.Commands(); Assert.That(first.Count, Is.EqualTo(1), "One ordinary command per planning tick.");
                 foreach (var worker in workers) Assert.That(f.Manager.GetWorkerReservation(worker.Id), Is.Not.Null, "Reserve complete selection before first command.");
                 f.Simulation.Tick(first);
                 var issuedTargets = first.OfType<GatherCommand>().Select(c => c.ResourceNodeId).ToList();
                 for (int tick = 15; tick <= 45; tick += 15)
                 { f.Manager.Tick(tick); var commands = f.Commands(); issuedTargets.AddRange(commands.OfType<GatherCommand>().Select(c => c.ResourceNodeId)); f.Simulation.Tick(commands); }
                 Assert.That(issuedTargets.Distinct().Count(), Is.EqualTo(4)); Assert.That(issuedTargets, Is.SubsetOf(targets));
                 Assert.That(workers.Select(w => w.TargetResourceNodeId).Distinct().Count(), Is.EqualTo(4), "Actual native commands must bind four separate farms.");
             }
         }
 
+        [Test]
+        public void OccupiedFarm_SelectedCountRetainsEligibleIncumbentWithoutReissuingGather()
+        {
+            using (var f = new EconomyFixture())
+            {
+                var farm = f.Simulation.CreateBuilding(0, BuildingType.Farm, f.Base.x + 16, f.Base.y + 14, false);
+                var node = f.Simulation.MapData.AddFarmResourceNode(ResourceType.Food, farm.SimPosition, 10000);
+                node.LinkedBuildingId = farm.Id; farm.LinkedResourceNodeId = node.Id;
+                f.Simulation.MapData.MarkFarmTiles(farm.OriginTileX, farm.OriginTileZ, farm.TileFootprintWidth, farm.TileFootprintHeight);
+                var incumbent = f.Worker(0); f.Assigned(incumbent, node);
+                var idle = f.Worker(1);
+                var goal = f.Submit("SelectedCount", "Exact", "1", "Any", "", "Food", "Farm");
+                f.Manager.Tick(0);
+                Assert.That(f.Commands(), Is.Empty, "The native Gather command would count its own incumbent as occupying the Farm.");
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+                Assert.That(incumbent.TargetResourceNodeId, Is.EqualTo(node.Id));
+                Assert.That(idle.TargetResourceNodeId, Is.Not.EqualTo(node.Id));
+            }
+        }
+
+        [Test]
+        public void OccupiedFarm_AdditionalCannotClaimIncumbentOrEvictProtectedCapacity()
+        {
+            using (var f = new EconomyFixture())
+            {
+                var farm = f.Simulation.CreateBuilding(0, BuildingType.Farm, f.Base.x + 16, f.Base.y + 14, false);
+                var node = f.Simulation.MapData.AddFarmResourceNode(ResourceType.Food, farm.SimPosition, 10000);
+                node.LinkedBuildingId = farm.Id; farm.LinkedResourceNodeId = node.Id;
+                f.Simulation.MapData.MarkFarmTiles(farm.OriginTileX, farm.OriginTileZ, farm.TileFootprintWidth, farm.TileFootprintHeight);
+                var incumbent = f.Worker(0); f.Assigned(incumbent, node);
+                var idle = f.Worker(1);
+                var goal = f.Submit("Additional", "Exact", "1", "Any", "", "Food", "Farm");
+                f.Manager.Tick(0);
+                Assert.That(f.Commands(), Is.Empty);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
+                Assert.That(f.Manager.GetWorkerReservation(idle.Id), Is.Null);
+                Assert.That(incumbent.TargetResourceNodeId, Is.EqualTo(node.Id));
+            }
+        }
+
+        [TestCase("SelectedCount", "2")]
+        [TestCase("Additional", "1")]
+        [TestCase("TargetTotal", "2")]
+        public void TwoFarms_QuantityModeUsesIncumbentAndIdleWithoutReissuingGather(string mode, string count)
+        {
+            using (var f = new EconomyFixture())
+            {
+                var first = f.Farm(16); var second = f.Farm(22);
+                var incumbent = f.Worker(0); f.Assigned(incumbent, first);
+                var idle = f.Worker(1);
+                var goal = f.Submit(mode, "Exact", count, "Any", "", "Food", "Farm");
+                f.Manager.Tick(0);
+                var commands = f.Commands();
+                Assert.That(Subjects(commands), Is.EquivalentTo(new[] { idle.Id }));
+                Assert.That(commands.OfType<GatherCommand>().Single().ResourceNodeId, Is.EqualTo(second.Id));
+                Assert.That(incumbent.TargetResourceNodeId, Is.EqualTo(first.Id));
+                f.Simulation.Tick(commands);
+                f.Manager.Tick(15);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+            }
+        }
+
+        [Test]
+        public void OccupiedFarm_IdleOnlyCannotReuseGatheringIncumbent()
+        {
+            using (var f = new EconomyFixture())
+            {
+                var farm = f.Farm(16); var incumbent = f.Worker(0); f.Assigned(incumbent, farm);
+                var idle = f.Worker(1);
+                var goal = f.Submit("SelectedCount", "Exact", "1", "Idle", "", "Food", "Farm");
+                f.Manager.Tick(0);
+                Assert.That(f.Commands(), Is.Empty);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
+                Assert.That(f.Manager.GetWorkerReservation(idle.Id), Is.Null);
+                Assert.That(incumbent.TargetResourceNodeId, Is.EqualTo(farm.Id));
+            }
+        }
+
+        [Test]
+        public void AllMatchingGathering_FreezesFarmIncumbentWithoutNewNativeOrder()
+        {
+            using (var f = new EconomyFixture())
+            {
+                var farm = f.Farm(16); var incumbent = f.Worker(0); f.Assigned(incumbent, farm);
+                var goal = f.Submit("SelectedCount", "AllMatching", "", "Gathering", "Food", "Food", "Farm");
+                f.Manager.Tick(0);
+                Assert.That(f.Commands(), Is.Empty);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+            }
+        }
+
+        [Test]
+        public void RetainedFarmIncumbentWithPendingAmount_ReservesWithoutDummyCommand()
+        {
+            using (var f = new EconomyFixture())
+            {
+                var farm = f.Farm(16); var incumbent = f.Worker(0); f.Assigned(incumbent, farm);
+                var first = f.SubmitWithAmount("SelectedCount", "Exact", "1", "Any", "", "Food", "Farm");
+                f.Manager.Tick(0);
+                Assert.That(first.Status, Is.EqualTo(CommanderGoalStatus.WaitingForResources), first.StatusReason);
+                Assert.That(f.Commands(), Is.Empty, "An incumbent must not receive a duplicate Gather.");
+                Assert.That(f.Manager.GetWorkerReservation(incumbent.Id)?.GoalId, Is.EqualTo(first.GoalId));
+                var competing = f.Submit("SelectedCount", "Exact", "1", "Any", "", "Food", "Farm");
+                f.Manager.Tick(15);
+                Assert.That(competing.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
+                Assert.That(f.Commands(), Is.Empty);
+                Assert.That(f.Manager.GetWorkerReservation(incumbent.Id)?.GoalId, Is.EqualTo(first.GoalId));
+            }
+        }
+
+        [Test]
+        public void ProtectedFarmIncumbent_KeepsCapacityAndCannotBeEvicted()
+        {
+            using (var f = new EconomyFixture())
+            {
+                var farm = f.Farm(16); var incumbent = f.Worker(0); f.Assigned(incumbent, farm);
+                var idle = f.Worker(1);
+                f.Simulation.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { incumbent.Id }));
+                f.Commands(); // Human authority observation without processing away the existing gather.
+                var goal = f.Submit("SelectedCount", "Exact", "1", "Idle", "", "Food", "Farm");
+                f.Manager.Tick(0);
+                Assert.That(f.Commands(), Is.Empty);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
+                Assert.That(f.Manager.GetWorkerReservation(idle.Id), Is.Null);
+                Assert.That(incumbent.TargetResourceNodeId, Is.EqualTo(farm.Id));
+            }
+        }
+
         [Test]
         public void HumanTakeover_InterruptsWithoutReclaimAfterLease()
         {
             using (var f = new EconomyFixture())
             {
                 f.Node(ResourceType.Food, 20); var worker = f.Worker(0); f.Worker(1);
                 var goal = f.Submit("SelectedCount", "Exact", "1", "Idle", "", "Food", "Any");
                 f.Manager.Tick(0); int selectedId = Subjects(f.Commands()).Single();
                 f.Simulation.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { selectedId })); f.Commands();
                 Assert.That(f.Manager.GetWorkerReservation(selectedId), Is.Null);
                 f.Manager.Tick(15); Assert.That(f.Commands(), Is.Empty);
                 f.Manager.Tick(915); Assert.That(f.Commands(), Is.Empty); Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
@@ -214,27 +342,47 @@ namespace OpenEmpires.Tests
                 Simulation.FogOfWar.SetVisionCheat(0, true);
                 Manager = new CommanderGoalManager(Simulation, 0);
             }
             internal UnitData Worker(int offset, int owner = 0)
             {
                 var worker = Simulation.UnitRegistry.CreateUnit(owner, Simulation.MapData.TileToWorldFixed(Base.x + 5 + offset, Base.y + 5),
                     Fixed32.One, Fixed32.FromFloat(0.4f), Fixed32.One);
                 worker.IsVillager = true; worker.UnitType = 0; worker.CurrentHealth = worker.MaxHealth = 100;
                 worker.CarryCapacity = config.VillagerCarryCapacity; worker.State = UnitState.Idle; return worker;
             }
             internal ResourceNodeData Node(ResourceType resource, int offset) => Simulation.MapData.AddResourceNode(resource,
                 Simulation.MapData.TileToWorldFixed(Base.x + offset, Base.y + 10), 10000);
+            internal ResourceNodeData Farm(int offset)
+            {
+                var building = Simulation.CreateBuilding(0, BuildingType.Farm, Base.x + offset, Base.y + 14, false);
+                var node = Simulation.MapData.AddFarmResourceNode(ResourceType.Food, building.SimPosition, 10000);
+                node.LinkedBuildingId = building.Id; building.LinkedResourceNodeId = node.Id;
+                Simulation.MapData.MarkFarmTiles(building.OriginTileX, building.OriginTileZ,
+                    building.TileFootprintWidth, building.TileFootprintHeight);
+                return node;
+            }
             internal void Assigned(UnitData worker, ResourceNodeData node)
             { worker.State = UnitState.Gathering; worker.TargetResourceNodeId = node.Id; }
             internal CommanderGoal Submit(params string[] values)
             {
                 var parsed = CommanderSemanticJson.Parse(Request(AllocationNode(values)));
                 Assert.That(parsed.IsValid, Is.True);
                 Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], Context(), out var intent, out _), Is.True);
                 var result = new CommanderIntentResolver().Resolve(intent, Simulation, Manager);
                 Assert.That(result.CreatedGoal, Is.True, result.Reason); return result.Goal;
             }
+            internal CommanderGoal SubmitWithAmount(params string[] values)
+            {
+                string node = AllocationNode(values);
+                node = node.Substring(0, node.Length - 1)
+                    + ",\"resourceAmount\":100,\"resourceAmountMode\":\"AdditionalGathered\"}";
+                var parsed = CommanderSemanticJson.Parse(Request(node));
+                Assert.That(parsed.IsValid, Is.True);
+                Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], Context(), out var intent, out _), Is.True);
+                var result = new CommanderIntentResolver().Resolve(intent, Simulation, Manager);
+                Assert.That(result.CreatedGoal, Is.True, result.Reason); return result.Goal;
+            }
             internal List<ICommand> Commands() => Simulation.CommandBuffer.FlushCommands();
             public void Dispose() { Manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
         }
     }
 }
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Tests__EditMode__CommanderGrandFixStatusTests.cs.snapshot" b/Assets/Tests/EditMode/CommanderGrandFixStatusTests.cs
index 2fbe348..330136d 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Tests__EditMode__CommanderGrandFixStatusTests.cs.snapshot"
+++ b/Assets/Tests/EditMode/CommanderGrandFixStatusTests.cs
@@ -66,24 +66,51 @@ namespace OpenEmpires.Tests
             Assert.That(step.queued,Is.EqualTo(0));
             Assert.That(step.producerReady,Is.EqualTo(1));
             Assert.That(step.deficits.Wood,Is.EqualTo(20));
             Assert.That(step.reason.ToLowerInvariant(),Does.Contain("wood"));
             Assert.That(goal.StatusReason,Is.EqualTo(originalReason));
             string conversation=chat.Conversation.Memory.ToJson().ToLowerInvariant();
             Assert.That(conversation,Does.Contain("wood"));
             Assert.That(conversation,Does.Not.Contain("everything is completed"));
             Assert.That(provider.Last.QuestionFacts.Length,Is.LessThanOrEqualTo(512));
             Assert.That(provider.Last.SerializedContext.Length,Is.LessThanOrEqualTo(8192));
         }
 
+        [TestCase("Why haven't my Spearmen arrived?")]
+        [TestCase("What happened to my Spearmen?")]
+        public async Task CausalNamedQuestionWithoutStatusKeyword_UsesOwnedTaskFacts(string question)
+        {
+            Waiting(); var provider = new FactsProvider(Answer); var chat = Chat(provider);
+            int before = manager.Goals.Count;
+            await chat.SubmitMessageAsync(question);
+            var status = JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext).tacticalStatus;
+            Assert.That(status, Is.Not.Null);
+            Assert.That(status.requests[0].steps[0].reason.ToLowerInvariant(), Does.Contain("wood"));
+            Assert.That(chat.Conversation.Memory.ToJson().ToLowerInvariant(), Does.Contain("wood"));
+            Assert.That(manager.Goals.Count, Is.EqualTo(before));
+            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        [Test] public void LongCausalBlocker_IsExplicitlyUnavailableRatherThanMeaningTruncated()
+        {
+            var goal = Waiting();
+            goal.SetStatus(CommanderGoalStatus.Blocked,
+                new string('a', 160) + " not the six requested archers; no survivor substitution is allowed.");
+            var status = Observe("Why did my Spearmen task stop?", out string json);
+            Assert.That(status, Is.Not.Null);
+            Assert.That(status.requests[0].steps[0].reason, Does.Contain("unavailable"));
+            Assert.That(status.requests[0].steps[0].reason, Does.Not.Contain("..."));
+            Assert.That(json, Does.Not.Contain("no survivor substitution"));
+        }
+
         [Test] public async Task AmbiguousArmyQuestion_ShowsOwnedChoicesRatherThanSelectingFirstGoal()
         {
             Waiting();manager.SubmitEnsureUnitCount(2,3,newProductionCount:3);
             var provider=new FactsProvider(Answer);var chat=Chat(provider);
             await chat.SubmitMessageAsync("Why is my army still not ready?");
             var status=JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext).tacticalStatus;
             Assert.That(status,Is.Not.Null);
             Assert.That(status.selection,Is.EqualTo("Ambiguous"));
             string text=chat.Conversation.Memory.ToJson().ToLowerInvariant();
             Assert.That(text,Does.Contain("which request"));
             Assert.That(text,Does.Contain("spearman"));Assert.That(text,Does.Contain("archer"));
             Assert.That(manager.Goals.Count,Is.EqualTo(2));Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Tests__EditMode__CommanderPhase4GCapabilityTests.cs.snapshot" b/Assets/Tests/EditMode/CommanderPhase4GCapabilityTests.cs
index 9a9280e..58783c0 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-3-baseline\\Assets__Tests__EditMode__CommanderPhase4GCapabilityTests.cs.snapshot"
+++ b/Assets/Tests/EditMode/CommanderPhase4GCapabilityTests.cs
@@ -238,24 +238,272 @@ namespace OpenEmpires.Tests
                 manager.Tick(0);
 
                 Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
                 Assert.That(simulation.CommandBuffer.FlushCommands(), Has.Some.TypeOf<MoveCommand>());
             }
             finally
             {
                 manager.Dispose();
                 Object.DestroyImmediate(config);
             }
         }
 
+        [Test]
+        public void DefenseAtOwnedBase_UsesDistinctReachableNativeSlots_AcceleratedFixture()
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                var townCenter = simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var ids = new List<int>();
+                for (int i = 0; i < 6; i++)
+                {
+                    UnitData unit = simulation.UnitRegistry.CreateUnit(0,
+                        simulation.MapData.TileToWorldFixed(x + 8 + i, z + 8), Fixed32.One,
+                        Fixed32.FromFloat(.4f), Fixed32.One);
+                    unit.UnitType = 1; unit.MaxHealth = unit.CurrentHealth = 100; ids.Add(unit.Id);
+                }
+                for (int ix = x - 20; ix <= x + 20; ix++)
+                    for (int iz = z - 20; iz <= z + 20; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
+                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
+                    CommanderCapabilityActionType.DefendArea,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 6, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
+                manager.Tick(0);
+                var commands = simulation.CommandBuffer.FlushCommands();
+                Assert.That(commands, Has.Count.EqualTo(1));
+                Assert.That(commands[0], Is.TypeOf<MoveCommand>());
+                var move = (MoveCommand)commands[0];
+                Assert.That(move.UnitIds, Is.EquivalentTo(ids));
+                Assert.That(move.FormationPositions, Has.Length.EqualTo(6));
+                var slots = new HashSet<Vector2Int>();
+                foreach (var position in move.FormationPositions)
+                {
+                    var tile = simulation.MapData.WorldToTile(position);
+                    Assert.That(simulation.MapData.IsWalkable(tile.x, tile.y, 0, simulation.BuildingRegistry), Is.True);
+                    Assert.That(slots.Add(tile), Is.True, "Each exact subject needs a distinct slot.");
+                    Assert.That(tile.x >= townCenter.OriginTileX && tile.x < townCenter.OriginTileX + townCenter.TileFootprintWidth
+                        && tile.y >= townCenter.OriginTileZ && tile.y < townCenter.OriginTileZ + townCenter.TileFootprintHeight, Is.False);
+                }
+                simulation.Tick(commands);
+                for (int tick = 15; tick <= 15000 && !goal.IsTerminal; tick += 15)
+                {
+                    simulation.Tick(new List<ICommand>());
+                    manager.Tick(tick);
+                }
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason + " / " +
+                    string.Join("; ", ids.ConvertAll(id =>
+                    {
+                        var u = simulation.UnitRegistry.GetUnit(id);
+                        return id + ":" + u.State + ":" + u.SimPosition + ":" + u.FinalDestination + ":" + u.HasPath;
+                    })));
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [Test]
+        public void MissingExactMovementSubject_IsNotReportedAsHumanTakeover()
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 8, z + 8),
+                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
+                unit.UnitType = 1; unit.MaxHealth = unit.CurrentHealth = 100;
+                for (int ix = x - 15; ix <= x + 15; ix++)
+                    for (int iz = z - 15; iz <= z + 15; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
+                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
+                    CommanderCapabilityActionType.MoveUnits,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
+                manager.Tick(0);
+                var commands = simulation.CommandBuffer.FlushCommands();
+                Assert.That(commands, Has.Count.EqualTo(1));
+                unit.CurrentHealth = 0; unit.State = UnitState.Dead;
+                manager.Tick(15);
+                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
+                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Contain("dead"));
+                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Not.Contain("player took control"));
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [TestCase("missing", "missing")]
+        [TestCase("foreign", "foreign")]
+        [TestCase("garrison", "garrison")]
+        public void ExactMovementSubjectLoss_IsClassifiedWithoutSurvivorSubstitution(string loss, string expected)
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var exact = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 8, z + 8),
+                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
+                exact.UnitType = 1; exact.CurrentHealth = exact.MaxHealth = 100;
+                var survivor = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 9, z + 8),
+                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
+                survivor.UnitType = 1; survivor.CurrentHealth = survivor.MaxHealth = 100;
+                for (int ix = x - 15; ix <= x + 15; ix++)
+                    for (int iz = z - 15; iz <= z + 15; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
+                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
+                    CommanderCapabilityActionType.DefendArea,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 2, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
+                manager.Tick(0);
+                var commands = simulation.CommandBuffer.FlushCommands();
+                Assert.That(((MoveCommand)commands[0]).UnitIds, Is.EquivalentTo(new[] { exact.Id, survivor.Id }));
+                if (loss == "missing") simulation.UnitRegistry.RemoveUnit(exact.Id);
+                else if (loss == "foreign") exact.PlayerId = 1;
+                else simulation.UnitRegistry.GarrisonUnit(exact.Id);
+                manager.Tick(15);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
+                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Contain(expected));
+                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Not.Contain("player took control"));
+                Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [Test]
+        public void DistantIdleSubject_CannotCompleteUnprocessedNativeMovement()
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 12, z + 12),
+                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
+                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
+                for (int ix = x - 18; ix <= x + 18; ix++)
+                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
+                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
+                    CommanderCapabilityActionType.MoveUnits,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
+                manager.Tick(0); simulation.CommandBuffer.FlushCommands();
+                simulation.Tick(new List<ICommand>()); // Issued command intentionally never reached native processing.
+                unit.State = UnitState.Idle;
+                manager.Tick(15);
+                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [Test]
+        public void NativeMovementWithNoProgress_BecomesBoundedBlocker()
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 12, z + 12),
+                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
+                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
+                for (int ix = x - 18; ix <= x + 18; ix++)
+                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
+                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
+                    CommanderCapabilityActionType.MoveUnits,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
+                manager.Tick(0);
+                simulation.Tick(simulation.CommandBuffer.FlushCommands());
+                unit.MoveSpeed = Fixed32.Zero;
+                for (int tick = 15; tick <= 2500 && goal.Status != CommanderGoalStatus.Blocked; tick += 15)
+                { simulation.Tick(new List<ICommand>()); manager.Tick(tick); }
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
+                Assert.That(goal.StatusReason, Does.Contain("no progress"));
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [Test]
+        public void TemporarilyUnavailableExactMover_IsNotDeathOrHumanTakeover()
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 12, z + 12),
+                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
+                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
+                for (int ix = x - 18; ix <= x + 18; ix++)
+                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
+                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
+                    CommanderCapabilityActionType.MoveUnits,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
+                manager.Tick(0); simulation.CommandBuffer.FlushCommands();
+                simulation.Tick(new List<ICommand>());
+                unit.State = UnitState.MovingToGarrison;
+                manager.Tick(15);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
+                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Contain("temporarily unavailable"));
+                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Not.Contain("player took control"));
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [TestCase(false)]
+        [TestCase(true)]
+        public void InvalidDefenseObjective_EnqueuesNoNativeMove(bool outsideBounds)
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                var tc = simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 20, z + 20),
+                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
+                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
+                for (int ix = x - 25; ix <= x + 25; ix++)
+                    for (int iz = z - 25; iz <= z + 25; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
+                if (outsideBounds) tc.SimPosition = simulation.MapData.TileToWorldFixed(-1, z);
+                else
+                {
+                    var anchor = simulation.MapData.WorldToTile(tc.SimPosition);
+                    for (int ix = anchor.x - 10; ix <= anchor.x + 10; ix++)
+                        for (int iz = anchor.y - 10; iz <= anchor.y + 10; iz++) simulation.MapData.Tiles[ix, iz] = TileType.Water;
+                }
+                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
+                    CommanderCapabilityActionType.DefendArea,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
+                manager.Tick(0);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
+                Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
         [Test]
         public void CapabilityGoal_HumanOverrideBlocksWithoutFalseCompletion()
         {
             var config = ScriptableObject.CreateInstance<SimulationConfig>();
             var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
             var manager = new CommanderGoalManager(simulation, 0);
             try
             {
                 int x = simulation.MapData.Width / 2;
                 int z = simulation.MapData.Height / 2;
                 simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                 UnitData military = simulation.UnitRegistry.CreateUnit(0,
