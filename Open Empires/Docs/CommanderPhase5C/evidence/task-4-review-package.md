# Task4 frozen incremental review package
HEAD unchanged: 4c9de96adbf6342fbe9e1490fa9b898a0f99caf6. No commits. Task4 delta against per-file first-touch snapshots; excludes earlier dirty-phase edits.

## Assets/Scripts/AI/Commander/CommanderGoal.cs
Before: 6C7EC7A7A36A2C26EFAC5C1A9FDE805F79B3A955DA2A11246D7F6D5B8B18F916; current: F6229C4DA01C9ADF5377B3C24873A33F2AF2F58AA3E54A4ACC035ADB0B4C9524
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs"
index 68971fc..d911256 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs"	
@@ -316,19 +316,37 @@ namespace OpenEmpires
         internal bool[] NativeMovementProcessed { get; set; }
         internal NativeMovePathResult[] NativeMovementResults { get; set; }
         internal bool NativeMovementReplaced { get; set; }
         internal long NativeRemainingDistance { get; set; } = long.MaxValue;
         internal int NativeLastProgressSimulationTick { get; set; } = -1;
         // Runtime-only typed result source. This prevents provider node indices from
         // becoming entity references and prevents cross-manager/stale-result reuse.
         internal CommanderGoal ResultSourceGoal { get; set; }
         internal bool RequiresResultBinding { get; set; }
         internal bool ResultHumanOverride { get; set; }
+        internal BuildingData GarrisonShelter { get; set; }
+        internal int[] GarrisonCandidateIds { get; set; }
+        internal int[] GarrisonIssuedIds { get; set; }
+        internal int GarrisonProtectedCount { get; set; }
+        internal int GarrisonUnaccommodatedCount { get; set; }
+        internal long GarrisonRemainingDistance { get; set; } = long.MaxValue;
+        internal int GarrisonLastProgressSimulationTick { get; set; } = -1;
+        internal int[] UngarrisonInitialIds { get; set; }
+        internal CommanderBoundLocation DynamicMoveLocation { get; set; }
+        internal int DynamicMoveResourceId { get; set; } = -1;
+        internal int DiscoveryScoutId { get; set; } = -1;
+        internal HashSet<int> DiscoveryVisitedWaypoints { get; } = new HashSet<int>();
+        internal int DiscoveryMovesIssued { get; set; }
+        internal int DiscoveryScanRadius { get; set; }
+        internal int DiscoveryScanOffset { get; set; }
+        internal int DiscoveryScanCenterX { get; set; } = -1;
+        internal int DiscoveryScanCenterZ { get; set; } = -1;
+        internal int DiscoveryCandidatesChecked { get; set; }
 
         public CommanderCapabilityGoal(CapabilityActionIntent action, int maxDurationTicks = 36000)
             : base(action?.PlayerId ?? throw new ArgumentNullException(nameof(action)),
                 CommanderGoalType.CapabilityAction, maxDurationTicks)
         {
             Action = action;
         }
     }
 }

## Assets/Scripts/AI/Commander/CommanderGoalManager.cs
Before: 8B8DA6FB77EDC27023A1FFC961897C839871C0CAC03B35039A9E840D95D5C21A; current: 3E4EA9E5D6FE86B34A57E519C52BC580E39552587CE268A099E6DFE5270CFE64
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderGoalManager.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoalManager.cs"
index 662fb09..10babb4 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderGoalManager.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoalManager.cs"	
@@ -573,20 +573,30 @@ namespace OpenEmpires
                         && (plan.Command != null || plan.Status == CommanderGoalStatus.WaitingForResources))
                     {
                         if (workerAuthority.TryReserveWorkers(workers.SelectedWorkerIds, goal.GoalId, currentTick))
                             workers.ReservationsAcquired = true;
                         else plan = new CommanderPlan(CommanderGoalStatus.Blocked,
                             "The full selected worker set could not be reserved; no assignments were issued.", 0, 0);
                     }
                     if (plan.Command != null && !workerAuthority.TryReserveCommand(goal, plan.Command, currentTick))
                         plan = new CommanderPlan(CommanderGoalStatus.Blocked,
                             "Worker is protected or reserved by another goal.", plan.OwnedCount, plan.QueuedCount);
+                    if (plan.Command is GarrisonCommand
+                        && goal is CommanderCapabilityGoal shelterGoal
+                        && shelterGoal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits
+                        && !simulation.RegisterExactGarrisonCommand(plan.Command))
+                    {
+                        workerAuthority.ReleaseGoal(goal.GoalId);
+                        plan = new CommanderPlan(CommanderGoalStatus.Blocked,
+                            "The exact native garrison order could not be bound safely; no order was issued.",
+                            plan.OwnedCount, plan.QueuedCount);
+                    }
                     goal.LastObservedOwnedCount = plan.OwnedCount;
                     goal.LastPlannerObservationTick=currentTick;
                     goal.LastObservedQueuedCount = plan.QueuedCount;
                     if (plan.Status == CommanderGoalStatus.Blocked)
                     {
                         if (goal.BlockedSinceTick < 0) goal.BlockedSinceTick = currentTick;
                         // Re-plan before failing, so a condition resolved at the deadline can recover.
                         if (currentTick - goal.BlockedSinceTick >= BlockedTimeoutTicks)
                         {
                             FailGoal(goal, $"Blocked for {BlockedTimeoutTicks} ticks. {plan.Reason}", currentTick);

## Assets/Scripts/AI/Commander/CommanderIntentValidator.cs
Before: 445DFD14DE91064ACEBB7BE17A1199887A796F4C434D61E4FA3107516C9A7F83; current: EFDAC9B706A61583558547FD2C0933605B60E702CCB38C244EE51A1B9F335E6B
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderIntentValidator.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderIntentValidator.cs"
index 318d5c3..6e5a31d 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderIntentValidator.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderIntentValidator.cs"	
@@ -209,28 +209,54 @@ namespace OpenEmpires
 
         private static CommanderIntentValidationResult ValidateCapabilityAction(CapabilityActionIntent intent)
         {
             if(intent.LocationSelector.RadiusTiles.HasValue)
                 return Invalid(CommanderIntentErrorCode.UnsupportedConstraint,
                     "This action supports a point target, not a radius or perimeter. Request a point route or revise the spatial constraint.");
             if (!Enum.IsDefined(typeof(CommanderCapabilityActionType), intent.ActionType))
                 return Invalid(CommanderIntentErrorCode.UnknownCommand, "Unknown Commander capability action.");
             if (intent.UnitSelector.Count < 1 || intent.UnitSelector.Count > 50)
                 return Invalid(CommanderIntentErrorCode.AmountOutOfRange, "Unit selector count is outside the bounded range.");
+            if (intent.UnitSelector.AllMatching && (intent.ActionType != CommanderCapabilityActionType.GarrisonUnits
+                || intent.UnitSelector.Kind != CommanderUnitSelectorKind.Villagers))
+                return Invalid(CommanderIntentErrorCode.UnknownCommand,
+                    "AllMatching units are supported only for bounded villager garrisoning.");
             if (!Enum.IsDefined(typeof(CommanderUnitSelectorKind), intent.UnitSelector.Kind)
                 || !Enum.IsDefined(typeof(CommanderLocationSelectorKind), intent.LocationSelector.Kind))
                 return Invalid(CommanderIntentErrorCode.UnknownCommand, "Unknown Commander selector.");
             if (intent.UnitSelector.Kind == CommanderUnitSelectorKind.UnitType
                 && !CommanderIntentCatalog.IsSupportedUnit(intent.UnitSelector.UnitType))
                 return Invalid(CommanderIntentErrorCode.UnknownUnit, "The requested unit selector is not supported.");
             if (intent.TargetSelector.HasValue && !intent.TargetSelector.Value.IsCompatible(intent.ActionType, intent.LocationSelector.Kind))
                 return Invalid(CommanderIntentErrorCode.UnknownCommand, "The requested target type is incompatible with this action.");
+            if (intent.ActionType == CommanderCapabilityActionType.DiscoverEnemy
+                && (intent.UnitSelector.Kind != CommanderUnitSelectorKind.Scout
+                    || intent.UnitSelector.Count != 1 || intent.UnitSelector.AllMatching
+                    || intent.LocationSelector.Kind != CommanderLocationSelectorKind.VisibleEnemy
+                    || intent.LocationSelector.RadiusTiles.HasValue
+                    || intent.LocationSelector.ResourceType.HasValue || !intent.TargetSelector.HasValue
+                    || intent.StructureType.HasValue || intent.Technology.HasValue))
+                return Invalid(CommanderIntentErrorCode.UnknownCommand,
+                    "Discovery requires one owned Scout and an explicit enemy target type.");
+            if (intent.ActionType == CommanderCapabilityActionType.GarrisonUnits
+                || intent.ActionType == CommanderCapabilityActionType.UngarrisonBuilding)
+            {
+                if (intent.LocationSelector.Kind != CommanderLocationSelectorKind.PlayerBase
+                    || !intent.StructureType.HasValue || intent.TargetSelector.HasValue
+                    || intent.Technology.HasValue || intent.LocationSelector.ResourceType.HasValue)
+                    return Invalid(CommanderIntentErrorCode.UnknownCommand,
+                        "A garrison action requires one owned shelter type at the player base.");
+                if (intent.ActionType == CommanderCapabilityActionType.UngarrisonBuilding
+                    && intent.UnitSelector.Count != 1)
+                    return Invalid(CommanderIntentErrorCode.UnknownCommand,
+                        "Ungarrison is building-wide; selective egress is unsupported.");
+            }
             if (intent.ActionType == CommanderCapabilityActionType.RepairTarget
                 && intent.UnitSelector.Kind != CommanderUnitSelectorKind.Villagers)
                 return Invalid(CommanderIntentErrorCode.UnknownCommand,
                     "RepairTarget requires an owned-villager selector.");
             if (intent.ActionType == CommanderCapabilityActionType.AttackTarget
                 && intent.UnitSelector.Kind == CommanderUnitSelectorKind.Villagers)
                 return Invalid(CommanderIntentErrorCode.UnknownCommand,
                     "AttackTarget cannot commandeer villagers.");
             if ((intent.LocationSelector.Kind == CommanderLocationSelectorKind.WorkedResource
                 || intent.LocationSelector.Kind == CommanderLocationSelectorKind.VisibleResource)

## Assets/Scripts/AI/Commander/CommanderPlanner.cs
Before: 4EA6E18CF79C19DCCCD0AE263744C2D111B72BCEB46FA25D168F662925064B4A; current: DAA11C65CE2B3B315A27CE7B8CED9E195F5F059C301D82E4B342AE0F13EE43E1
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.cs"
index 2b590fc..c485984 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.cs"	
@@ -66,34 +66,51 @@ namespace OpenEmpires
             if (goal is EnsureUnitCountGoal units) return PlanUnits(units, currentTick);
             if (goal is BuildStructureGoal building) return PlanStructure(building, currentTick);
             if (goal is ResourceAllocationGoal resource) return PlanAllocation(resource, currentTick);
             if (goal is ReachAgeGoal reachAge) return PlanReachAge(reachAge, currentTick);
             if (goal is CommanderCapabilityGoal capability) return PlanCapability(capability, currentTick);
             return new CommanderPlan(CommanderGoalStatus.Failed, "Unknown goal type.", 0, 0);
         }
 
         private CommanderPlan PlanCapability(CommanderCapabilityGoal goal, int currentTick)
         {
+            if (goal.Action.ActionType == CommanderCapabilityActionType.DiscoverEnemy)
+                return PlanDiscovery(goal, currentTick);
+            if (goal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits
+                || goal.Action.ActionType == CommanderCapabilityActionType.UngarrisonBuilding)
+                return PlanShelterCapability(goal, currentTick);
             if (goal.ResultHumanOverride)
                 return new CommanderPlan(CommanderGoalStatus.Blocked,
                     "The player took control of a required producer result; this dependent action will not reclaim it.", 0, 0);
             if (goal.RequiresResultBinding && goal.ResultSourceGoal == null)
                 return new CommanderPlan(CommanderGoalStatus.Blocked,
                     "The required producer result is missing; no selector fallback is allowed.", 0, 0);
             if (goal.ResultSourceGoal != null && (goal.RuntimeOwner == null
                 || goal.RuntimeOwner.IsDisposed
                 || !ReferenceEquals(goal.ResultSourceGoal.RuntimeOwner, goal.RuntimeOwner)
                 || !ReferenceEquals(goal.RuntimeOwner.Simulation, simulation)
                 || !goal.Dependencies.Contains(goal.ResultSourceGoal)
                 || goal.ResultSourceGoal.Status != CommanderGoalStatus.Completed))
                 return new CommanderPlan(CommanderGoalStatus.Blocked,
                     "The referenced producer is unavailable in this Commander runtime; no fallback is allowed.", 0, 0);
+            if (goal.DynamicMoveLocation != null)
+            {
+                if (goal.CommandIssued)
+                {
+                    ResourceNodeData source = simulation.MapData.GetResourceNode(goal.DynamicMoveResourceId);
+                    if (!goal.DynamicMoveLocation.AllowsResource(simulation, goal.PlayerId, source))
+                        return new CommanderPlan(CommanderGoalStatus.Blocked,
+                            "The exact selected woodline source or owned Town Center was lost; no substitute move is authorized.", 0, 0);
+                    return ObserveCapability(goal, currentTick);
+                }
+                return PlanDynamicWorkerMove(goal, currentTick);
+            }
             if (goal.CommandIssued)
                 return ObserveCapability(goal, currentTick);
             var executor = new CommanderCapabilityExecutor(simulation);
             // Bind before actor/resource reservation succeeds. A blocked retry may
             // not reinterpret the named type as a replacement runtime identity.
             if (goal.Action.TargetSelector.HasValue && goal.TargetBinding == null)
             {
                 if (!executor.TryBindTarget(goal.Action, out var selectedTarget))
                     return new CommanderPlan(CommanderGoalStatus.Blocked,
                         "No eligible requested target is available; no other type will substitute.", 0, 0);
@@ -272,28 +289,32 @@ namespace OpenEmpires
                 if (unit == null || unit.PlayerId != goal.PlayerId || unit.CurrentHealth <= 0
                     || unit.State == UnitState.Dead || workerAuthority.IsHumanProtected(unit.Id, currentTick)) return false;
                 bool found = false;
                 for (int radius = 0; radius <= 10 && !found; radius++)
                     for (int dx = -radius; dx <= radius && !found; dx++)
                         for (int dz = -radius; dz <= radius && !found; dz++)
                         {
                             if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius) continue;
                             int x = anchor.x + dx, z = anchor.y + dz;
                             var destination = new Vector2Int(x, z);
+                            if (goal.DynamicMoveLocation != null
+                                && !goal.DynamicMoveLocation.AllowsMoveTile(simulation, goal.PlayerId,
+                                    goal.DynamicMoveResourceId, x, z)) continue;
                             if (!simulation.MapData.IsWalkable(x, z)
                                 || !simulation.MapData.IsWalkable(x, z, goal.PlayerId, simulation.BuildingRegistry)
                                 || !IsKnownTile(goal.PlayerId, x, z)) continue;
                             FixedVector3 proposed = simulation.MapData.TileToWorldFixed(x, z);
                             FixedVector3 commandAnchor = i == 0 ? proposed : slots[0];
                             if (!TryKnownNativeMoveEnvelope(goal.PlayerId, anchor, unit,
                                 commandAnchor, proposed, occupied, out FixedVector3 nativeCenter,
-                                out Fixed32 scatter, out HashSet<Vector2Int> covered)) continue;
+                                out Fixed32 scatter, out HashSet<Vector2Int> covered,
+                                goal.DynamicMoveLocation, goal.DynamicMoveResourceId)) continue;
                             occupied.UnionWith(covered);
                             slots[i] = proposed;
                             nativeCenters[i] = nativeCenter;
                             scatterLimits[i] = scatter;
                             found = true;
                         }
                 if (!found) return false;
             }
             move.TargetPosition = slots[0];
             move.FormationPositions = slots;
@@ -308,37 +329,40 @@ namespace OpenEmpires
             goal.NativeMovementProcessed = new bool[slots.Length];
             goal.NativeMovementResults = new NativeMovePathResult[slots.Length];
             goal.NativeMovementReplaced = false;
             reason = "Moving exact selected units to known reachable native formation positions.";
             return true;
         }
 
         private bool TryKnownNativeMoveEnvelope(int playerId, Vector2Int semanticAnchor,
             UnitData unit, FixedVector3 commandAnchor, FixedVector3 slot,
             HashSet<Vector2Int> occupied, out FixedVector3 center,
-            out Fixed32 scatter, out HashSet<Vector2Int> covered)
+            out Fixed32 scatter, out HashSet<Vector2Int> covered,
+            CommanderBoundLocation dynamicMoveLocation = null, int dynamicMoveResourceId = -1)
         {
             center = simulation.NativeMoveFormationCenter(commandAnchor, slot, unit);
             scatter = simulation.MaximumNativeMoveScatter(unit);
             covered = new HashSet<Vector2Int>();
             if (scatter > Fixed32.FromInt(10)) return false;
             Vector2Int low = simulation.MapData.WorldToTile(new FixedVector3(center.x - scatter,
                 center.y, center.z - scatter));
             Vector2Int high = simulation.MapData.WorldToTile(new FixedVector3(center.x + scatter,
                 center.y, center.z + scatter));
             if (high.x - low.x > 20 || high.y - low.y > 20) return false;
             Vector2Int start = simulation.MapData.WorldToTile(unit.SimPosition);
             for (int x = low.x; x <= high.x; x++)
                 for (int z = low.y; z <= high.y; z++)
                 {
                     var tile = new Vector2Int(x, z);
                     if (Mathf.Max(Mathf.Abs(x - semanticAnchor.x), Mathf.Abs(z - semanticAnchor.y)) > 10
+                        || dynamicMoveLocation != null && !dynamicMoveLocation.AllowsMoveTile(
+                            simulation, playerId, dynamicMoveResourceId, x, z)
                         || occupied.Contains(tile)
                         || !simulation.MapData.IsWalkable(x, z, playerId, simulation.BuildingRegistry)
                         || !IsKnownTile(playerId, x, z)
                         || !GridPathfinder.TryFindCompletePath(simulation.MapData, start, tile,
                             out List<Vector2Int> path, playerId, simulation.BuildingRegistry)
                         || !IsKnownPath(playerId, start, path)) return false;
                     covered.Add(tile);
                 }
             return covered.Count > 0;
         }

## Assets/Scripts/AI/Commander/CommanderWorkerAuthority.cs
Before: D1568F25171E243B02F1C9CAAE0447EEE47666D6B854DC80A93DE36E8DFC7EEF; current: 53C8E63EFE8EB6EF3EB87794009BFBBC63B05B2E6BD65B540BD1128BAE53E61B
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderWorkerAuthority.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderWorkerAuthority.cs"
index 7cb79ea..7882bda 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderWorkerAuthority.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderWorkerAuthority.cs"	
@@ -68,21 +68,21 @@ namespace OpenEmpires
             var role = command is GatherCommand || command is SlaughterSheepCommand ? CommanderWorkerReservationType.Gatherer : CommanderWorkerReservationType.Builder;
             for (int i = 0; i < workers.Length; i++)
             {
                 UnitData unit = simulation.UnitRegistry.GetUnit(workers[i]);
                 if (unit.IsVillager) TryReserve(workers[i], goal.GoalId, role, currentTick);
                 else commanderUnitReservations[workers[i]] = goal.GoalId;
             }
             return true;
         }
 
-        private bool CanUseUnit(int unitId, int goalId, int currentTick)
+        internal bool CanUseUnit(int unitId, int goalId, int currentTick)
         {
             UnitData unit = simulation.UnitRegistry.GetUnit(unitId);
             if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
                 || unit.State == UnitState.Dead || IsHumanProtected(unitId, currentTick)) return false;
             if (unit.IsVillager) return CanUseWorker(unitId, goalId, currentTick);
             return !commanderUnitReservations.TryGetValue(unitId, out int owner) || owner == goalId;
         }
 
         public void ReleaseGoal(int goalId)
         {
@@ -185,33 +185,14 @@ namespace OpenEmpires
             return commanderControlledWorkers.Contains(unitId);
         }
 
         public bool IsRecentGatherAssignment(int unitId, int currentTick)
         {
             return commanderGatherUntilTick.TryGetValue(unitId, out int until) && currentTick < until;
         }
 
         internal static int[] GetSubjectUnitIds(ICommand command)
         {
-            switch (command)
-            {
-                case MoveCommand value: return value.UnitIds;
-                case GatherCommand value: return value.UnitIds;
-                case StopCommand value: return value.UnitIds;
-                case AttackBuildingCommand value: return value.UnitIds;
-                case AttackUnitCommand value: return value.UnitIds;
-                case ConstructBuildingCommand value: return value.UnitIds;
-                case DropOffCommand value: return value.UnitIds;
-                case GarrisonCommand value: return value.UnitIds;
-                case PatrolCommand value: return value.UnitIds;
-                case DeleteUnitsCommand value: return value.UnitIds;
-                case FollowUnitCommand value: return value.UnitIds;
-                case HealUnitCommand value: return value.UnitIds;
-                case RepairBuildingCommand value: return value.UnitIds;
-                case PlaceBuildingCommand value: return value.VillagerUnitIds;
-                case PlaceWallCommand value: return value.VillagerUnitIds;
-                case SlaughterSheepCommand value: return value.VillagerIds;
-                default: return null;
-            }
+            return CommandBuffer.GetSubjectUnitIds(command);
         }
     }
 }

## Assets/Scripts/AI/Commander/Economy/AllocateWorkersGoal.cs
Before: 26A0A8557803A0235EAAC52B0CC0AD41AE4B00E46DFBD75167A0F7A76161AE47; current: 953EF57BF9F412C7D48B6BCF55B02521AEFD15BBF27829E0B10023CF32517CDF
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Economy\\AllocateWorkersGoal.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Economy\\AllocateWorkersGoal.cs"
index fbb6714..29bcbb6 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Economy\\AllocateWorkersGoal.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Economy\\AllocateWorkersGoal.cs"	
@@ -7,20 +7,21 @@ namespace OpenEmpires
     {
         internal readonly int WorkerId, TargetId;
         internal readonly CommanderWorkerTargetKind Kind;
         internal CommanderWorkerAssignment(int worker, int target, CommanderWorkerTargetKind kind)
         { WorkerId = worker; TargetId = target; Kind = kind; }
     }
 
     public sealed class AllocateWorkersGoal : CommanderGoal
     {
         public CommanderWorkerAllocation Allocation { get; }
+        internal CommanderBoundLocation DynamicResourceLocation { get; set; }
         public long ResourceProgress { get; internal set; }
         internal EnsureUnitCountGoal ResultSourceGoal;
         internal long GatheredIncomeAtActivation;
         internal bool Prepared, ReservationsAcquired, SnapshotCaptured, BaselineCaptured, HumanInterrupted;
         internal int BaselineCount, NextCommandGroup, LastIssuedSimulationTick = -1;
         internal readonly List<int> SnapshotWorkerIds = new List<int>();
         internal readonly List<int> SelectedWorkerIds = new List<int>();
         internal readonly List<CommanderWorkerAssignment> Assignments = new List<CommanderWorkerAssignment>();
         internal readonly List<ICommand> CommandGroups = new List<ICommand>();
         // Recovery only: O(original workers) state, never an unbounded rejected-source cache.

## Assets/Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs
Before: 2C8EA0D3B6B8F8C96DAB792A6EAAE25481DDBEA3FBC1FCE85724567ACEA13718; current: 5A1647D009AD8EAE023562C6CF42F86FF98DE49BDE7913005352FF0718E75DEE
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Economy\\CommanderPlanner.Economy.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Economy\\CommanderPlanner.Economy.cs"
index ef16773..1e94874 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Economy\\CommanderPlanner.Economy.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Economy\\CommanderPlanner.Economy.cs"	
@@ -275,21 +275,22 @@ namespace OpenEmpires
             if (!node.IsFarmNode) return true;
             BuildingData farm = simulation.BuildingRegistry.GetBuilding(node.LinkedBuildingId);
             return farm != null && farm.Type == BuildingType.Farm && farm.PlayerId == player && !farm.IsDestroyed && !farm.IsUnderConstruction;
         }
 
         private List<EconomyTarget> VisibleEconomyTargets(AllocateWorkersGoal goal)
         {
             var result = new List<EconomyTarget>(); var destination = goal.Allocation.Destination;
             foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                 if (!node.IsDepleted && node.Type == destination.Resource && ResourceSourceRules.Matches(node, destination.SourceKind)
-                    && LegalFarm(node, goal.PlayerId) && simulation.FogOfWar.GetVisibility(goal.PlayerId, node.TileX, node.TileZ) == TileVisibility.Visible)
+                    && LegalFarm(node, goal.PlayerId) && simulation.FogOfWar.GetVisibility(goal.PlayerId, node.TileX, node.TileZ) == TileVisibility.Visible
+                    && (goal.DynamicResourceLocation == null || goal.DynamicResourceLocation.AllowsResource(simulation, goal.PlayerId, node)))
                     result.Add(new EconomyTarget(node.Id, node.TileX, node.TileZ, node.FootprintWidth, node.FootprintHeight,
                         node.IsFarmNode ? 1 : (node.FootprintWidth + 2) * (node.FootprintHeight + 2) - node.FootprintWidth * node.FootprintHeight,
                         CommanderWorkerTargetKind.ResourceNode));
             if (destination.Resource == ResourceType.Food && (destination.SourceKind == ResourceSourceKind.Any || destination.SourceKind == ResourceSourceKind.Sheep))
                 foreach (UnitData sheep in simulation.UnitRegistry.GetAllUnits())
                     if (sheep.IsSheep && sheep.PlayerId == goal.PlayerId && sheep.CurrentHealth > 0 && sheep.State != UnitState.Dead)
                     {
                         Vector2Int tile = simulation.MapData.WorldToTile(sheep.SimPosition);
                         if (simulation.FogOfWar.GetVisibility(goal.PlayerId, tile.x, tile.y) == TileVisibility.Visible)
                             result.Add(new EconomyTarget(sheep.Id, tile.x, tile.y, 1, 1, 8, CommanderWorkerTargetKind.OwnedSheep));

## Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticAdmission.cs
Before: 2BA68DD20FC4A74CDA445C00BBDEC090D43C31033E5A0903E1E0D2AF5A5DA244; current: 61C915B62B05B1DB79A051DAA521BC3C48E3DD63B3DD96CBB5275D29EA12919A
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticAdmission.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticAdmission.cs"
index cfbe033..e775c9c 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticAdmission.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticAdmission.cs"	
@@ -80,35 +80,39 @@ namespace OpenEmpires
                 case CommanderSemanticNodeType.ReachAge:
                     if (!node.AgeTarget.HasValue || node.UnitType.HasValue || node.BuildingType.HasValue
                         || node.ResourceType.HasValue || node.Count.HasValue
                         || node.StrategicObjectiveType.HasValue) return false;
                     dto.intentType = nameof(CommanderIntentType.ReachAge);
                     dto.targetAge = node.AgeTarget.Value.ToString();
                     break;
 
                 case CommanderSemanticNodeType.MoveUnits:
                 case CommanderSemanticNodeType.ScoutArea:
+                case CommanderSemanticNodeType.DiscoverEnemy:
                 case CommanderSemanticNodeType.PatrolArea:
                 case CommanderSemanticNodeType.SetRallyPoint:
                 case CommanderSemanticNodeType.AttackTarget:
                 case CommanderSemanticNodeType.DefendArea:
                 case CommanderSemanticNodeType.RetreatUnits:
                 case CommanderSemanticNodeType.RepairTarget:
                 case CommanderSemanticNodeType.ResearchTechnology:
+                case CommanderSemanticNodeType.GarrisonUnits:
+                case CommanderSemanticNodeType.UngarrisonBuilding:
                     if (!node.UnitSelector.HasValue || !node.LocationSelector.HasValue)
                         return false;
                     if (node.Type == CommanderSemanticNodeType.ResearchTechnology && !node.Technology.HasValue)
                         return false;
                     if (node.TargetSelector.HasValue && !node.TargetSelector.Value.IsCompatible(ToCapabilityAction(node.Type),
                         ToLocationSelector(node.LocationSelector.Value))) return false;
                     intent = new CapabilityActionIntent(context.PlayerId,
-                        ToCapabilityAction(node.Type), ToUnitSelector(node.UnitSelector.Value, node.UnitType, node.Count),
+                        ToCapabilityAction(node.Type), ToUnitSelector(node.UnitSelector.Value, node.UnitType, node.Count,
+                            node.AllMatchingUnits),
                         new CommanderLocationSelector(ToLocationSelector(node.LocationSelector.Value),
                             node.ResourceType), node.Technology, node.BuildingType,
                         node.Constraints, node.TargetSelector);
                     safeReason = string.Empty;
                     return true;
 
                 default:
                     return false;
             }
 
@@ -125,32 +129,33 @@ namespace OpenEmpires
             safeReason = string.Empty;
             return true;
         }
 
         private static CommanderCapabilityActionType ToCapabilityAction(CommanderSemanticNodeType type)
         {
             return (CommanderCapabilityActionType)Enum.Parse(typeof(CommanderCapabilityActionType), type.ToString());
         }
 
         private static CommanderUnitSelector ToUnitSelector(CommanderSemanticUnitSelector selector,
-            int? unitType, int? count)
+            int? unitType, int? count, bool allMatching = false)
         {
             CommanderUnitSelectorKind kind;
             switch (selector)
             {
                 case CommanderSemanticUnitSelector.Scout: kind = CommanderUnitSelectorKind.Scout; break;
                 case CommanderSemanticUnitSelector.Villagers: kind = CommanderUnitSelectorKind.Villagers; break;
                 case CommanderSemanticUnitSelector.Spearman: kind = CommanderUnitSelectorKind.UnitType; unitType = 1; break;
                 case CommanderSemanticUnitSelector.Archer: kind = CommanderUnitSelectorKind.UnitType; unitType = 2; break;
                 case CommanderSemanticUnitSelector.Knight: kind = CommanderUnitSelectorKind.UnitType; unitType = 7; break;
                 case CommanderSemanticUnitSelector.DamagedMilitary: kind = CommanderUnitSelectorKind.DamagedMilitary; break;
                 default: kind = CommanderUnitSelectorKind.Military; break;
             }
-            return new CommanderUnitSelector(kind, Math.Max(1, count ?? 1), unitType ?? -1);
+            return new CommanderUnitSelector(kind, Math.Max(1, count ?? 1), unitType ?? -1,
+                allMatching);
         }
 
         private static CommanderLocationSelectorKind ToLocationSelector(CommanderSemanticLocationSelector selector)
         {
             return (CommanderLocationSelectorKind)Enum.Parse(typeof(CommanderLocationSelectorKind), selector.ToString());
         }
     }
 }

## Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs
Before: 349C47BA3F78C94161326B932F5BF064E1C858CFE76D3D4BA99C70CA5B6A79D2; current: 9E9D3A36CDA716046276C0040ABAF005D67C162401AC408D4C529FE4CA982BB6
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticJson.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticJson.cs"
index c05ca4e..d5e01f6 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticJson.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticJson.cs"	
@@ -266,70 +266,92 @@ namespace OpenEmpires
                         dependsOn: dependsOn, constraints: ParseConstraints(node));
 
                 case "MoveUnits":
                 case "ScoutArea":
                 case "PatrolArea":
                 case "SetRallyPoint":
                 case "AttackTarget":
                 case "DefendArea":
                 case "RetreatUnits":
                 case "RepairTarget":
+                case "GarrisonUnits":
+                case "DiscoverEnemy":
                     return ParseCapabilityNode(node, RequiredString(node, "type"), dependsOn);
 
+                case "UngarrisonBuilding":
+                    CheckFields(node, "type", "location", "structure", "dependsOn");
+                    if (RequiredString(node, "location") != "PlayerBase") throw new JsonException();
+                    return new CommanderSemanticNode(CommanderSemanticNodeType.UngarrisonBuilding,
+                        buildingType: ParseBuilding(RequiredString(node, "structure")),
+                        unitSelector: CommanderSemanticUnitSelector.Villagers,
+                        locationSelector: CommanderSemanticLocationSelector.PlayerBase,
+                        count: 1, dependsOn: dependsOn);
+
                 case "ResearchTechnology":
                     CheckFields(node, "type", "technology", "dependsOn");
                     return new CommanderSemanticNode(CommanderSemanticNodeType.ResearchTechnology,
                         technology: ParseTechnology(RequiredString(node, "technology")),
                         unitSelector: CommanderSemanticUnitSelector.Military,
                         locationSelector: CommanderSemanticLocationSelector.PlayerBase,
                         dependsOn: dependsOn);
 
                 case "StrategicObjective":
                     CheckFields(node, "type", "objective", "dependsOn");
                     return new CommanderSemanticNode(CommanderSemanticNodeType.StrategicObjective,
                         strategicObjectiveType: ParseObjective(RequiredString(node, "objective")), dependsOn: dependsOn);
 
                 default: throw new JsonException();
             }
         }
 
         private static CommanderSemanticNode ParseCapabilityNode(JObject node, string type,
             IReadOnlyList<int> dependsOn)
         {
-            CheckFields(node, "type", "unitSelector", "count", "location", "resource", "structure", "dependsOn", "resultFromNode", "target");
+            CheckFields(node, "type", "unitSelector", "count", "countMode", "location", "resource", "structure", "dependsOn", "resultFromNode", "target");
             CommanderSemanticUnitSelector unit = ParseUnitSelector(RequiredString(node, "unitSelector"));
-            int count = RequiredCount(node, 1, 50);
+            bool allMatching = node.Property("countMode") != null;
+            if (allMatching && (type != "GarrisonUnits" || unit != CommanderSemanticUnitSelector.Villagers
+                || RequiredString(node, "countMode") != "AllMatching" || node.Property("count") != null))
+                throw new JsonException();
+            int? count = allMatching ? (int?)null : RequiredCount(node, 1, 50);
             CommanderSemanticLocationSelector location = ParseLocationSelector(RequiredString(node, "location"));
             ResourceType? resource = null;
             if (node.Property("resource") != null)
                 resource = ParseResource(RequiredString(node, "resource"));
             if ((location == CommanderSemanticLocationSelector.WorkedResource
                 || location == CommanderSemanticLocationSelector.VisibleResource) && !resource.HasValue)
                 throw new JsonException();
             BuildingType? structure = null;
             if (node.Property("structure") != null)
             {
                 structure = ParseBuilding(RequiredString(node, "structure"));
-                if (type != "SetRallyPoint") throw new JsonException();
+                if (type != "SetRallyPoint" && type != "GarrisonUnits") throw new JsonException();
             }
+            if (type == "GarrisonUnits" && (!structure.HasValue
+                || location != CommanderSemanticLocationSelector.PlayerBase)) throw new JsonException();
+            if (type == "DiscoverEnemy" && (unit != CommanderSemanticUnitSelector.Scout
+                || count != 1 || location != CommanderSemanticLocationSelector.VisibleEnemy
+                || resource.HasValue || structure.HasValue || node.Property("target") == null
+                || node.Property("resultFromNode") != null)) throw new JsonException();
             CommanderSemanticNodeType action;
             if (!Enum.TryParse(type, false, out action)) throw new JsonException();
             CommanderTargetSelector? target = node.Property("target") == null ? (CommanderTargetSelector?)null
                 : ParseTargetSelector(node["target"]);
             if (target.HasValue && !target.Value.IsCompatible(
                 (CommanderCapabilityActionType)Enum.Parse(typeof(CommanderCapabilityActionType), type),
                 (CommanderLocationSelectorKind)Enum.Parse(typeof(CommanderLocationSelectorKind), location.ToString())))
                 throw new JsonException();
             int? resultFromNode = ParseOptionalNodeIndex(node, "resultFromNode");
             return new CommanderSemanticNode(action, count: count, resourceType: resource,
                 buildingType: structure, unitSelector: unit, locationSelector: location,
-                dependsOn: dependsOn, resultFromNode: resultFromNode, targetSelector: target);
+                dependsOn: dependsOn, resultFromNode: resultFromNode, targetSelector: target,
+                allMatchingUnits: allMatching);
         }
 
         // Shared strict target vocabulary for both semantic requests and legacy DTOs.
         internal static CommanderTargetSelector ParseTargetSelector(JToken token)
         {
             if (!(token is JObject target)) throw new JsonException();
             string kind = RequiredString(target, "kind");
             if (kind == "UnitType")
             {
                 CheckFields(target, "kind", "unit");

## Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs
Before: E57DDA95C59B22F8BF01381536A05F1243763F3A13C07DB90BE143E19C7CE9CF; current: AC9888C06C43246F2D5DF8D70EB0C24CAF4349B4061B103DC34D2F75C3EE58B5
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticProvider.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticProvider.cs"
index ef388e9..d2a0365 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticProvider.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticProvider.cs"	
@@ -105,21 +105,21 @@ namespace OpenEmpires
                     .Select(id => GameKnowledgeCatalog.BuildCanonicalIdentityCatalog().FindUnit(id)?.DisplayName)
                     .Where(name => name != null).OrderBy(name => name, StringComparer.Ordinal)),
                 ["structureCapabilities"] = structureNames.Length > 0 ? new JArray(structureNames)
                     : new JArray("House", "Barracks", "ArcheryRange", "Stables", "Mill", "Tower", "TownCenter"),
                 ["resourceCapabilities"] = new JArray("Food", "Wood", "Gold", "Stone"),
                 ["strategyCapabilities"] = new JArray("AttackPreparation", "DefensivePreparation",
                     "EconomicExpansion", "MilitaryReinforcement", "RangedReinforcement",
                     "DefensiveTurtle"),
                 ["actionCapabilities"] = new JArray("AllocateWorkers", "MoveUnits", "ScoutArea", "PatrolArea",
                     "SetRallyPoint", "AttackTarget", "DefendArea", "RetreatUnits",
-                    "RepairTarget", "ResearchTechnology"),
+                    "RepairTarget", "ResearchTechnology", "GarrisonUnits", "UngarrisonBuilding", "DiscoverEnemy"),
                 ["selectorCapabilities"] = new JObject
                 {
                     ["units"] = new JArray("Military", "Scout", "Villagers", "Spearman", "Archer", "Knight", "DamagedMilitary"),
                     ["locations"] = new JArray("PlayerBase", "WorkedResource", "VisibleResource", "VisibleEnemy", "RelativeToSelectedUnits"),
                     ["constructionAnchors"] = new JArray("MyTownCenter", "MyBarracks", "WorkedResource", "VisibleResource"),
                     ["targets"] = new JObject {
                         ["UnitType"] = new JArray("Villager", "Spearman", "Archer", "Scout", "Knight"),
                         ["BuildingType"] = new JArray(Enum.GetNames(typeof(BuildingType))) }
                 },
                 ["knowledge"] = ParseKnowledge(context.KnowledgeContext)

## Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs
Before: FA90990081F6CF38C891699675B147522D958C703B73E2EADF444986AA89A72B; current: A9FA141E24C76ABB512BFBFC7E1E8BA6500427AB06E2DC6A4AC76DAFF1CA574C
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticRequest.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticRequest.cs"
index 4de1650..078c8f0 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticRequest.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4E\\CommanderSemanticRequest.cs"	
@@ -22,21 +22,24 @@ namespace OpenEmpires
         ScoutArea,
         PatrolArea,
         SetRallyPoint,
         AttackTarget,
         DefendArea,
         RetreatUnits,
         RepairTarget,
         ResearchTechnology,
         StrategicObjective,
         AllocateWorkers,
-        WatchFutureUnits
+        WatchFutureUnits,
+        GarrisonUnits,
+        UngarrisonBuilding,
+        DiscoverEnemy
     }
 
     // Provider-facing age vocabulary. Numeric game ages remain game-side data.
     public enum CommanderSemanticAgeTarget
     {
         Next,
         Feudal = 2,
         Castle = 3,
         Imperial = 4
     }
@@ -86,20 +89,21 @@ namespace OpenEmpires
         public BuildingType? BuildingType { get; }
         public ResourceType? ResourceType { get; }
         public int? Count { get; }
         public StrategicObjectiveType? StrategicObjectiveType { get; }
         public CommanderSemanticAgeTarget? AgeTarget { get; }
         public CommanderSemanticAnchorSelector? PlacementAnchorSelector { get; }
         public int? PlacementAnchorOrdinal { get; }
         public CommanderSemanticPlacementRelation? PlacementRelation { get; }
         public int? ClearGapTiles { get; }
         public CommanderSemanticUnitSelector? UnitSelector { get; }
+        public bool AllMatchingUnits { get; }
         public CommanderSemanticLocationSelector? LocationSelector { get; }
         public CommanderTargetSelector? TargetSelector { get; }
         public CommanderProductionQuantityMode QuantityMode { get; }
         public TechnologyType? Technology { get; }
         // References are bounded semantic node indices, never game/entity IDs.
         public IReadOnlyList<int> DependsOn { get; }
         public int? ProducerFromNode { get; }
         public int? ResultFromNode { get; }
         public CommanderWorkerAllocation WorkerAllocation { get; }
         public int? ProducerOrdinal { get; }
@@ -120,34 +124,36 @@ namespace OpenEmpires
             TechnologyType? technology = null,
             IReadOnlyList<int> dependsOn = null,
             int? producerFromNode = null,
             int? resultFromNode = null,
             CommanderWorkerAllocation workerAllocation = null,
             IReadOnlyList<CommanderConstraint> constraints = null,
             CommanderTargetSelector? targetSelector = null,
             CommanderProductionQuantityMode quantityMode = CommanderProductionQuantityMode.TargetTotal,
             int? producerOrdinal = null,
             CommanderFutureUnitAction? futureAction = null,
-            ResourceSourceKind? sourceKind = null)
+            ResourceSourceKind? sourceKind = null,
+            bool allMatchingUnits = false)
         {
             Type = type;
             UnitType = unitType;
             BuildingType = buildingType;
             ResourceType = resourceType;
             Count = count;
             StrategicObjectiveType = strategicObjectiveType;
             PlacementAnchorSelector = placementAnchorSelector;
             PlacementAnchorOrdinal = placementAnchorOrdinal;
             PlacementRelation = placementRelation;
             ClearGapTiles = clearGapTiles;
             AgeTarget = ageTarget;
             UnitSelector = unitSelector;
+            AllMatchingUnits = allMatchingUnits;
             LocationSelector = locationSelector;
             TargetSelector = targetSelector;
             QuantityMode = quantityMode;
             ProducerOrdinal = producerOrdinal;
             FutureAction = futureAction;
             SourceKind = sourceKind;
             Technology = technology;
             ProducerFromNode = producerFromNode;
             ResultFromNode = resultFromNode;
             WorkerAllocation = workerAllocation;

## Assets/Scripts/AI/Commander/Phase4G/CommanderCapability.cs
Before: C3B2083BB7E556575FC8C3B36D4AE9D5D68298C07A173AA436B58212A01D43C5; current: 8DDC88F7950A9F19BEAD5C07DD6E7D577B12546F026C5249B9C21084FC88FECE
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapability.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapability.cs"
index d3eee0e..06b810a 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapability.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapability.cs"	
@@ -6,48 +6,54 @@ namespace OpenEmpires
     public enum CommanderCapabilityActionType
     {
         MoveUnits,
         ScoutArea,
         PatrolArea,
         SetRallyPoint,
         AttackTarget,
         DefendArea,
         RetreatUnits,
         RepairTarget,
-        ResearchTechnology
+        ResearchTechnology,
+        GarrisonUnits,
+        UngarrisonBuilding,
+        DiscoverEnemy
     }
 
     public enum CommanderUnitSelectorKind
     {
         Military,
         Scout,
         Villagers,
         UnitType,
         DamagedMilitary
     }
 
     public readonly struct CommanderUnitSelector
     {
         public CommanderUnitSelectorKind Kind { get; }
         public int UnitType { get; }
         public int Count { get; }
+        public bool AllMatching { get; }
 
-        public CommanderUnitSelector(CommanderUnitSelectorKind kind, int count = 1, int unitType = -1)
+        public CommanderUnitSelector(CommanderUnitSelectorKind kind, int count = 1, int unitType = -1,
+            bool allMatching = false)
         {
             if (!Enum.IsDefined(typeof(CommanderUnitSelectorKind), kind))
                 throw new ArgumentOutOfRangeException(nameof(kind));
             if (count < 1 || count > 50) throw new ArgumentOutOfRangeException(nameof(count));
             if (kind == CommanderUnitSelectorKind.UnitType && unitType < 0)
                 throw new ArgumentOutOfRangeException(nameof(unitType));
             Kind = kind;
             Count = count;
             UnitType = unitType;
+            AllMatching = allMatching;
         }
     }
 
     public enum CommanderLocationSelectorKind
     {
         PlayerBase,
         WorkedResource,
         VisibleResource,
         VisibleEnemy,
         RelativeToSelectedUnits
@@ -75,21 +81,23 @@ namespace OpenEmpires
                 throw new ArgumentOutOfRangeException(nameof(structureType));
             Kind = CommanderTargetSelectorKind.BuildingType; UnitType = -1; StructureType = structureType;
         }
 
         internal bool IsCompatible(CommanderCapabilityActionType action, CommanderLocationSelectorKind location)
         {
             bool valid = Kind == CommanderTargetSelectorKind.UnitType
                 ? CommanderIntentCatalog.IsSupportedUnit(UnitType) && !StructureType.HasValue
                 : Kind == CommanderTargetSelectorKind.BuildingType && UnitType == -1
                     && StructureType.HasValue && Enum.IsDefined(typeof(BuildingType), StructureType.Value);
-            return valid && (action == CommanderCapabilityActionType.AttackTarget
+            return valid && (action == CommanderCapabilityActionType.DiscoverEnemy
+                && location == CommanderLocationSelectorKind.VisibleEnemy
+                || action == CommanderCapabilityActionType.AttackTarget
                 && location == CommanderLocationSelectorKind.VisibleEnemy
                 || action == CommanderCapabilityActionType.RepairTarget
                 && location == CommanderLocationSelectorKind.PlayerBase && Kind == CommanderTargetSelectorKind.BuildingType);
         }
     }
 
     public readonly struct CommanderLocationSelector
     {
         public CommanderLocationSelectorKind Kind { get; }
         public ResourceType? ResourceType { get; }

## Assets/Scripts/AI/Commander/Phase4G/CommanderCapabilityExecutor.cs
Before: 27B12811A91A5DAA7015AFB2D1E362075035CB8654A29895FE8F0514440B21B5; current: F3D3EB16466F8370D0683EA1F21629371841AE8E6EBFDD4F80AE1176E0C2D90C
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapabilityExecutor.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapabilityExecutor.cs"
index e4ad574..e94bc13 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapabilityExecutor.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapabilityExecutor.cs"	
@@ -180,21 +180,21 @@ namespace OpenEmpires
                     default: match = !unit.IsVillager && !unit.IsSheep && unit.UnitType != 4; break;
                 }
                 if (match) candidates.Add(unit);
             }
             candidates.Sort((a, b) => a.Id.CompareTo(b.Id));
             if (candidates.Count < selector.Count) { ids = null; return false; }
             ids = candidates.Take(selector.Count).Select(unit => unit.Id).ToArray();
             return true;
         }
 
-        private bool MatchesSelector(UnitData unit, CommanderUnitSelector selector)
+        internal bool MatchesSelector(UnitData unit, CommanderUnitSelector selector)
         {
             switch (selector.Kind)
             {
                 case CommanderUnitSelectorKind.Scout: return unit.UnitType == 4;
                 case CommanderUnitSelectorKind.Villagers: return unit.IsVillager;
                 case CommanderUnitSelectorKind.UnitType:
                     return unit.UnitType == simulation.ResolveCivUnitType(unit.PlayerId, selector.UnitType);
                 case CommanderUnitSelectorKind.DamagedMilitary:
                     return !unit.IsVillager && !unit.IsSheep && unit.UnitType != 4
                         && unit.CurrentHealth < unit.MaxHealth;

## Assets/Scripts/AI/Commander/Phase5A/CommanderActionPlanCandidate.cs
Before: FA6DC92F498B80CBE568E544C5B2445D8F45E3495E6EFD146130DDA6C06BDF76; current: 80834F0C5E78485097FE422C958DB61D2B01CF9661FEE4E94A705FB09299A139
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderActionPlanCandidate.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderActionPlanCandidate.cs"
index a1811e6..894c3be 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderActionPlanCandidate.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderActionPlanCandidate.cs"	
@@ -146,21 +146,24 @@ namespace OpenEmpires
                 canonicalUnitNames?.TryGetValue(node.Index, out canonicalUnitName);
                 var dynamicSource = graph.DynamicProgram?.Nodes.FirstOrDefault(n => n.Id == node.DynamicNodeId);
                 if (dynamicSource?.Primitive.Mechanic == CommanderDynamicMechanic.Produce)
                 {
                     string id = dynamicSource.Parameter<string>("unit");
                     canonicalUnitName = KeybindManager.GetUnitTypeDisplayName(int.Parse(
                         id.Substring("unit:".Length), System.Globalization.CultureInfo.InvariantCulture));
                 }
                 string resultBuildingName=node.ResultFromNode.HasValue&&graph.Nodes[node.ResultFromNode.Value].Intent is BuildStructureIntent boundBuilding
                     ?Words(CommanderIntentCatalog.GetStructureDisplayName(boundBuilding.StructureType)):null;
-                text.Append(node.Index + 1).Append(". ").Append(Describe(node.Intent, canonicalUnitName,resultBuildingName));
+                text.Append(node.Index + 1).Append(". ").Append(
+                    dynamicSource?.Primitive.Mechanic == CommanderDynamicMechanic.MoveWorkers
+                        ? "Move the full frozen selected villager set to the visible resource-side woodline using native formation travel; no gather order"
+                        : Describe(node.Intent, canonicalUnitName,resultBuildingName));
                 if (node.Intent is EnsureUnitCountIntent units && !units.NewProductionCount.HasValue)
                     foreach (var quote in graph.ProductionExpectations)
                         if (quote.NodeIndex == node.Index)
                             text.Append("; expected ").Append(quote.NewCount).Append(" newly produced (existing and queued count toward the total)");
                 if (node.DependsOn.Count > 0)
                 {
                     text.Append("; after ").Append(string.Join(", ",node.DependsOn.Select(d=>"step "+(d+1))));
                 }
                 if (node.ProducerFromNode.HasValue)
                     text.Append("; ").Append(ProducerDescription(graph,node.ProducerFromNode.Value));

## Assets/Scripts/AI/Commander/Phase5A/CommanderBoundLocation.cs
Before: 5E6E7EC61F2679BCF4AFA6ADCCEC697D45D97F3462B15B200538AA15ED8DBADC; current: 209C985413ED89558D6CEE66CD2D6C02546A124D35E78DA3767CF2E390E57408
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderBoundLocation.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderBoundLocation.cs"
index 615ac04..a1993c6 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderBoundLocation.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderBoundLocation.cs"	
@@ -14,30 +14,32 @@ namespace OpenEmpires
         private readonly int anchorId;
         private readonly int originX;
         private readonly int originZ;
         private readonly int footprintWidth;
         private readonly int footprintHeight;
         private readonly BuildingType buildingType;
         private readonly ResourceType resourceType;
         private readonly ResourceSourceKind sourceKind;
         private readonly bool worked;
         private readonly BuildStructureGoal futureSource;
+        private readonly IReadOnlyList<int> resourceSideIds;
 
         internal CommanderSemanticPlacementRelation Relation { get; }
         internal int ClearGapTiles { get; }
+        internal bool IsResourceSide => resourceSideIds != null;
 
         private CommanderBoundLocation(AnchorKind kind, GameSimulation runtime,
             CommanderGoalManager owner, int playerId, int anchorId,
             int x, int z, int width, int height, BuildingType buildingType,
             ResourceType resourceType, ResourceSourceKind sourceKind, bool worked,
             BuildStructureGoal futureSource, CommanderSemanticPlacementRelation relation,
-            int gap)
+            int gap, IReadOnlyList<int> resourceSideIds = null)
         {
             if (runtime == null || owner == null || playerId != owner.PlayerId
                 || !ReferenceEquals(owner.Simulation, runtime)
                 || gap < CommanderSemanticJson.MinimumClearGapTiles
                 || gap > CommanderSemanticJson.MaximumClearGapTiles
                 || (relation == CommanderSemanticPlacementRelation.Near && gap != 1)
                 || (relation != CommanderSemanticPlacementRelation.Near
                     && relation != CommanderSemanticPlacementRelation.MapWest
                     && relation != CommanderSemanticPlacementRelation.MapEast))
                 throw new ArgumentException("Invalid semantic location binding.");
@@ -45,20 +47,21 @@ namespace OpenEmpires
             this.runtime = runtime;
             this.owner = owner;
             this.playerId = playerId;
             this.anchorId = anchorId;
             originX = x; originZ = z; footprintWidth = width; footprintHeight = height;
             this.buildingType = buildingType;
             this.resourceType = resourceType;
             this.sourceKind = sourceKind;
             this.worked = worked;
             this.futureSource = futureSource;
+            this.resourceSideIds = resourceSideIds;
             Relation = relation;
             ClearGapTiles = gap;
         }
 
         internal static CommanderBoundLocation FromBuilding(GameSimulation runtime,
             CommanderGoalManager owner, int playerId, BuildingData building,
             CommanderSemanticPlacementRelation relation, int gap)
         {
             if (!ValidBuilding(runtime, playerId, building, null))
                 throw new ArgumentException("The requested owned building anchor is unavailable.");
@@ -75,20 +78,84 @@ namespace OpenEmpires
             CommanderSemanticPlacementRelation relation, int gap)
         {
             if (!ValidResource(runtime, playerId, resource, type, sourceKind, worked))
                 throw new ArgumentException("The requested visible resource anchor is unavailable.");
             return new CommanderBoundLocation(AnchorKind.Resource, runtime, owner, playerId,
                 resource.Id, resource.TileX, resource.TileZ,
                 resource.FootprintWidth, resource.FootprintHeight, default,
                 type, sourceKind, worked, null, relation, gap);
         }
 
+        // A bounded, admission-time visible source set tied to one frozen owned anchor.
+        // Provider supplies neither runtime node IDs nor world coordinates.
+        internal static CommanderBoundLocation FromResourceSide(GameSimulation runtime,
+            CommanderGoalManager owner, int playerId, BuildingData anchor,
+            ResourceType type, ResourceSourceKind sourceKind, int gap)
+        {
+            if (!ValidBuilding(runtime, playerId, anchor, BuildingType.TownCenter)
+                || !ResourceSourceRules.IsCompatible(type, sourceKind))
+                throw new ArgumentException("The owned Town Center or resource/source pair is unavailable.");
+            var ids = new List<int>();
+            foreach (ResourceNodeData resource in runtime.MapData.GetAllResourceNodes())
+                if (ValidResource(runtime, playerId, resource, type, sourceKind, false)
+                    && (long)resource.TileX + resource.FootprintWidth + gap <= anchor.OriginTileX)
+                    ids.Add(resource.Id);
+            ids.Sort();
+            if (ids.Count < 1 || ids.Count > 200)
+                throw new ArgumentException("No bounded visible source set lies west of the owned Town Center.");
+            return new CommanderBoundLocation(AnchorKind.Building, runtime, owner, playerId,
+                anchor.Id, anchor.OriginTileX, anchor.OriginTileZ,
+                anchor.TileFootprintWidth, anchor.TileFootprintHeight,
+                BuildingType.TownCenter, type, sourceKind, false, null,
+                CommanderSemanticPlacementRelation.MapWest, gap, Array.AsReadOnly(ids.ToArray()));
+        }
+
+        internal bool AllowsResource(GameSimulation currentRuntime, int currentPlayerId, ResourceNodeData resource)
+        {
+            if (resourceSideIds == null || resource == null
+                || !TryResolve(currentRuntime, currentPlayerId, out int x, out _, out _, out _)
+                || (long)resource.TileX + resource.FootprintWidth + ClearGapTiles > x
+                || !ValidResource(runtime, playerId, resource, resourceType, sourceKind, false)) return false;
+            foreach (int id in resourceSideIds)
+                if (id == resource.Id) return true;
+            return false;
+        }
+
+        internal bool TryResolveResourceSidePoint(GameSimulation currentRuntime, int currentPlayerId,
+            out FixedVector3 position, out int resourceId)
+        {
+            position = default;
+            resourceId = -1;
+            if (resourceSideIds == null) return false;
+            foreach (int id in resourceSideIds)
+            {
+                ResourceNodeData candidate = runtime.MapData.GetResourceNode(id);
+                if (!AllowsResource(currentRuntime, currentPlayerId, candidate)) continue;
+                position = runtime.MapData.TileToWorldFixed(candidate.TileX, candidate.TileZ);
+                resourceId = id;
+                return true;
+            }
+            return false;
+        }
+
+        internal bool AllowsMoveTile(GameSimulation currentRuntime, int currentPlayerId,
+            int resourceId, int x, int z)
+        {
+            if (resourceSideIds == null || !TryResolve(currentRuntime, currentPlayerId,
+                    out int anchorX, out _, out _, out _)
+                || (long)x + ClearGapTiles >= anchorX) return false;
+            ResourceNodeData resource = runtime.MapData.GetResourceNode(resourceId);
+            return AllowsResource(currentRuntime, currentPlayerId, resource)
+                && Math.Abs(x - resource.TileX) <= 10
+                && Math.Abs(z - resource.TileZ) <= 10;
+        }
+
         internal static CommanderBoundLocation FromFutureBuild(GameSimulation runtime,
             CommanderGoalManager owner, int playerId, BuildStructureGoal source,
             CommanderSemanticPlacementRelation relation, int gap)
         {
             if (source == null || source.PlayerId != playerId || source.Count != 1)
                 throw new ArgumentException("A future placement anchor must have one exact building result.");
             return new CommanderBoundLocation(AnchorKind.FutureBuild, runtime, owner, playerId,
                 -1, -1, -1, 0, 0, source.StructureType, default, default, false,
                 source, relation, gap);
         }

## Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicCompiler.cs
Before: 949C929D7332E2ABC0E353DE91313E7F263B7083FC25F4D01C75FDA76A0A1899; current: DEC1FCBE7B17807B32BFA67116BEC27DF30321B28A1FDA280F28D6D8EF55C026
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicCompiler.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicCompiler.cs"
index e9276fb..fee0919 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicCompiler.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicCompiler.cs"	
@@ -139,51 +139,76 @@ namespace OpenEmpires
                 case CommanderDynamicMechanic.Build:
                     if (!TryBuilding(node.Parameter<string>("building"), knowledge,
                         civilization, out BuildingKnowledge building)
                         || !CommanderIntentCatalog.IsSupportedStructure(building.BuildingType))
                         return false;
                     if (node.Inputs.TryGetValue("workers", out string builders)
                         && (!byId.TryGetValue(builders, out var workers)
                             || !TryWorkerSelection(workers, byId, simulation.Config.MaxPopulation,
                                 out _, out _, out _))) return false;
                     if (node.Inputs.TryGetValue("location", out string location)
-                        && (!byId.TryGetValue(location, out var site) || !TryLocation(site, byId)))
+                        && (!byId.TryGetValue(location, out var site) || !TryLocation(site, byId)
+                            || site.Parameters.ContainsKey("resource")))
                         return false;
                     intent = new BuildStructureIntent(playerId, building.BuildingType,
                         node.Parameter<int>("count"), constraints);
                     break;
                 case CommanderDynamicMechanic.AllocateWorkers:
                     if (!CompatibleResource(node.Parameter<string>("resource"),
                         node.Parameter<string>("sourceKind"))
                         || !byId.TryGetValue(node.Inputs["workers"], out var source)
                         || !TryWorkerSelection(source, byId, simulation.Config.MaxPopulation,
                             out int selectedCount, out CommanderWorkerState state,
                             out ResourceType? currentResource)) return false;
                     var resource = (ResourceType)Enum.Parse(typeof(ResourceType),
                         node.Parameter<string>("resource"));
                     var sourceKind = (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
                         node.Parameter<string>("sourceKind"));
+                    if (node.Inputs.TryGetValue("location", out string resourceLocation)
+                        && (!byId.TryGetValue(resourceLocation, out var region)
+                            || !TryLocation(region, byId)
+                            || !region.Parameters.TryGetValue("resource", out object regionResource)
+                            || !region.Parameters.TryGetValue("sourceKind", out object regionSource)
+                            || (string)regionResource != resource.ToString()
+                            || (string)regionSource != sourceKind.ToString())) return false;
                     foreach (var restriction in globalConstraints)
                         if (restriction is ResourceSourceConstraint sourceRestriction
                             && sourceRestriction.Resource == resource && sourceRestriction.SourceKind != ResourceSourceKind.Any)
                         {
                             if (sourceKind != ResourceSourceKind.Any && sourceKind != sourceRestriction.SourceKind)
                             { reason = "Worker destination contradicts the shared resource-source restriction."; return false; }
                             sourceKind = sourceRestriction.SourceKind;
                         }
                     var allocation = new CommanderWorkerAllocation(
                         CommanderWorkerAllocationMode.SelectedCount,
-                        CommanderWorkerCountMode.Exact, selectedCount,
+                        source.Parameters.ContainsKey("countMode")
+                            ? CommanderWorkerCountMode.AllMatching : CommanderWorkerCountMode.Exact,
+                        source.Parameters.ContainsKey("countMode") ? (int?)null : selectedCount,
                         new CommanderWorkerSelector(state, currentResource),
                         new CommanderResourceDestination(resource, sourceKind));
                     intent = new AllocateWorkersIntent(playerId, allocation, constraints);
                     break;
+                case CommanderDynamicMechanic.MoveWorkers:
+                    if (!byId.TryGetValue(node.Inputs["workers"], out var movingWorkers)
+                        || !TryWorkerSelection(movingWorkers, byId, simulation.Config.MaxPopulation,
+                            out _, out _, out _)
+                        || !byId.TryGetValue(node.Inputs["location"], out var moveLocation)
+                        || !TryLocation(moveLocation, byId)
+                        || !moveLocation.Parameters.ContainsKey("resource")
+                        || !moveLocation.Parameters.ContainsKey("sourceKind")
+                        || moveLocation.Parameter<string>("anchor") != "MyTownCenter"
+                        || moveLocation.Parameter<string>("relation") != "MapWest") return false;
+                    intent = new CapabilityActionIntent(playerId, CommanderCapabilityActionType.MoveUnits,
+                        new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers),
+                        new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase),
+                        constraints: constraints);
+                    break;
                 case CommanderDynamicMechanic.Produce:
                     if (!TryUnit(node.Parameter<string>("unit"), knowledge, civilization,
                         out UnitKnowledge unit)
                         || !TryResolveTrainingRequest(unit, knowledge, simulation, playerId, out int requestedType)
                         || !simulation.TryGetProductionBuildingType(playerId, requestedType,
                             out BuildingType requiredProducer)) return false;
                     if (node.Inputs.TryGetValue("producers", out string producerId))
                     {
                         if (!byId.TryGetValue(producerId, out var producerNode)
                             || (producerNode.Primitive.Mechanic != CommanderDynamicMechanic.Build
@@ -283,20 +308,24 @@ namespace OpenEmpires
         {
             if (node.Primitive.Mechanic != CommanderDynamicMechanic.ResolveLocation)
                 return false;
             string relation = node.Parameter<string>("relation");
             int gap = node.Parameter<int>("clearGapTiles");
             if (relation != "Near" && relation != "MapWest" && relation != "MapEast")
                 return false;
             if (gap < CommanderSemanticJson.MinimumClearGapTiles
                 || gap > CommanderSemanticJson.MaximumClearGapTiles
                 || (relation == "Near" && gap != 1)) return false;
+            if (node.Parameters.ContainsKey("resource")
+                && (!node.Parameters.ContainsKey("sourceKind") || relation != "MapWest"
+                    || !node.Parameters.TryGetValue("anchor", out object composedAnchor)
+                    || (string)composedAnchor != "MyTownCenter")) return false;
             if (node.Parameters.TryGetValue("anchor", out object anchor))
                 return (string)anchor == "MyTownCenter" || (string)anchor == "MyBarracks";
             if (node.Inputs.TryGetValue("structures", out string structures))
                 return byId.TryGetValue(structures, out var source)
                     && source.Primitive.Result == CommanderDynamicResultKind.StructureSet;
             if (node.Inputs.TryGetValue("resources", out string resources))
                 return byId.TryGetValue(resources, out var resourceSource)
                     && resourceSource.Primitive.Result == CommanderDynamicResultKind.ResourceSet;
             return false;
         }
@@ -313,32 +342,34 @@ namespace OpenEmpires
                     || !TryWorkerSelection(source, byId, maximumPopulation,
                         out int sourceCount, out state, out currentResource))
                     return false;
                 int offset = node.Parameter<int>("offset");
                 count = node.Parameter<int>("count");
                 return offset >= 0 && count >= 1 && offset <= sourceCount
                     && count <= sourceCount - offset;
             }
             if (node.Primitive.Mechanic != CommanderDynamicMechanic.SelectWorkers)
                 return false;
-            count = node.Parameter<int>("count");
-            if (count < 1 || count > maximumPopulation) return false;
+            bool all = node.Parameters.ContainsKey("countMode");
+            count = all ? 0 : node.Parameter<int>("count");
+            if (!all && (count < 1 || count > maximumPopulation)) return false;
             if (node.Parameters.TryGetValue("state", out object stateName)
                 && (!Enum.TryParse((string)stateName, false, out state)
                     || state.ToString() != (string)stateName)) return false;
             if (node.Parameters.TryGetValue("currentResource", out object resourceName))
             {
                 if (state != CommanderWorkerState.Gathering
                     || !Enum.TryParse((string)resourceName, false, out ResourceType current)
                     || current.ToString() != (string)resourceName) return false;
                 currentResource = current;
             }
+            if (all && state == CommanderWorkerState.Any) return false;
             return true;
         }
 
         private static bool CollectNearestEffects(string id,
             Dictionary<string, CommanderDynamicNode> byId,
             Dictionary<string, int> effectIndices, HashSet<int> found)
         {
             if (effectIndices.TryGetValue(id, out int effect)) { found.Add(effect); return true; }
             if (!byId.TryGetValue(id, out var source)) return false;
             foreach (string dependency in source.DependsOn)

## Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlan.cs
Before: 639005446D8347F620C0616617C9EC4A31F423EA27CCC19CE40E93085C71A2A9; current: 8E8A7C611042607C297CE11C8CA9A7D60408CEE46EDCF49B3B79902728EEF53E
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicPlan.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicPlan.cs"
index 58db8e6..22e418f 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicPlan.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicPlan.cs"	
@@ -1,37 +1,37 @@
 using System;
 using System.Collections.Generic;
 using System.Collections.ObjectModel;
 
 namespace OpenEmpires
 {
     public enum CommanderDynamicMechanic
     {
         SelectWorkers, PartitionWorkers, SelectUnits, SelectStructures,
-        SelectResources, ResolveLocation, Build, AllocateWorkers, Produce
+        SelectResources, ResolveLocation, Build, AllocateWorkers, MoveWorkers, Produce
     }
 
     public enum CommanderDynamicResultKind
     {
         WorkerSet, UnitSet, StructureSet, ResourceSet, LocationIntent,
         AssignmentEffect, UnitResult
     }
 
     public enum CommanderDynamicEffectClass
     {
-        Selection, Location, Construction, Allocation, Production
+        Selection, Location, Construction, Allocation, Movement, Production
     }
 
     public enum CommanderDynamicFieldKind
     {
         Count, Offset, ClearGapTiles, UnitId, BuildingId, Resource,
-        SourceKind, WorkerState, ResourceMode, Anchor, Relation,
+        SourceKind, WorkerState, ResourceMode, Anchor, Relation, WorkerCountMode,
         QuantityMode, UnitKind
     }
 
     public sealed class CommanderDynamicField
     {
         public string Name { get; }
         public CommanderDynamicFieldKind Kind { get; }
         public bool Required { get; }
         internal CommanderDynamicField(string name, CommanderDynamicFieldKind kind, bool required)
         { Name = name; Kind = kind; Required = required; }
@@ -71,21 +71,22 @@ namespace OpenEmpires
         private static CommanderDynamicField P(string name, CommanderDynamicFieldKind kind,
             bool required = true) => new CommanderDynamicField(name, kind, required);
         private static CommanderDynamicInput I(string role, CommanderDynamicResultKind kind,
             bool required = true) => new CommanderDynamicInput(role, kind, required);
 
         public static readonly IReadOnlyList<CommanderDynamicPrimitive> All =
             Array.AsReadOnly(new[]
             {
                 new CommanderDynamicPrimitive("select-workers", CommanderDynamicMechanic.SelectWorkers,
                     CommanderDynamicResultKind.WorkerSet, CommanderDynamicEffectClass.Selection,
-                    new[] { P("count", CommanderDynamicFieldKind.Count),
+                    new[] { P("count", CommanderDynamicFieldKind.Count, false),
+                        P("countMode", CommanderDynamicFieldKind.WorkerCountMode, false),
                         P("state", CommanderDynamicFieldKind.WorkerState, false),
                         P("currentResource", CommanderDynamicFieldKind.Resource, false) },
                     Array.Empty<CommanderDynamicInput>()),
                 new CommanderDynamicPrimitive("partition-workers", CommanderDynamicMechanic.PartitionWorkers,
                     CommanderDynamicResultKind.WorkerSet, CommanderDynamicEffectClass.Selection,
                     new[] { P("offset", CommanderDynamicFieldKind.Offset),
                         P("count", CommanderDynamicFieldKind.Count) },
                     new[] { I("workers", CommanderDynamicResultKind.WorkerSet) }),
                 new CommanderDynamicPrimitive("select-units", CommanderDynamicMechanic.SelectUnits,
                     CommanderDynamicResultKind.UnitSet, CommanderDynamicEffectClass.Selection,
@@ -102,34 +103,42 @@ namespace OpenEmpires
                     CommanderDynamicResultKind.ResourceSet, CommanderDynamicEffectClass.Selection,
                     new[] { P("resource", CommanderDynamicFieldKind.Resource),
                         P("sourceKind", CommanderDynamicFieldKind.SourceKind),
                         P("mode", CommanderDynamicFieldKind.ResourceMode),
                         P("count", CommanderDynamicFieldKind.Count) },
                     Array.Empty<CommanderDynamicInput>()),
                 new CommanderDynamicPrimitive("resolve-location", CommanderDynamicMechanic.ResolveLocation,
                     CommanderDynamicResultKind.LocationIntent, CommanderDynamicEffectClass.Location,
                     new[] { P("anchor", CommanderDynamicFieldKind.Anchor, false),
                         P("relation", CommanderDynamicFieldKind.Relation),
-                        P("clearGapTiles", CommanderDynamicFieldKind.ClearGapTiles) },
+                        P("clearGapTiles", CommanderDynamicFieldKind.ClearGapTiles),
+                        P("resource", CommanderDynamicFieldKind.Resource, false),
+                        P("sourceKind", CommanderDynamicFieldKind.SourceKind, false) },
                     new[] { I("structures", CommanderDynamicResultKind.StructureSet, false),
                         I("resources", CommanderDynamicResultKind.ResourceSet, false) }),
                 new CommanderDynamicPrimitive("build", CommanderDynamicMechanic.Build,
                     CommanderDynamicResultKind.StructureSet, CommanderDynamicEffectClass.Construction,
                     new[] { P("building", CommanderDynamicFieldKind.BuildingId),
                         P("count", CommanderDynamicFieldKind.Count) },
                     new[] { I("workers", CommanderDynamicResultKind.WorkerSet, false),
                         I("location", CommanderDynamicResultKind.LocationIntent, false) }),
                 new CommanderDynamicPrimitive("allocate-workers", CommanderDynamicMechanic.AllocateWorkers,
                     CommanderDynamicResultKind.AssignmentEffect, CommanderDynamicEffectClass.Allocation,
                     new[] { P("resource", CommanderDynamicFieldKind.Resource),
                         P("sourceKind", CommanderDynamicFieldKind.SourceKind) },
-                    new[] { I("workers", CommanderDynamicResultKind.WorkerSet) }),
+                    new[] { I("workers", CommanderDynamicResultKind.WorkerSet),
+                        I("location", CommanderDynamicResultKind.LocationIntent, false) }),
+                new CommanderDynamicPrimitive("move-workers", CommanderDynamicMechanic.MoveWorkers,
+                    CommanderDynamicResultKind.AssignmentEffect, CommanderDynamicEffectClass.Movement,
+                    Array.Empty<CommanderDynamicField>(),
+                    new[] { I("workers", CommanderDynamicResultKind.WorkerSet),
+                        I("location", CommanderDynamicResultKind.LocationIntent) }),
                 new CommanderDynamicPrimitive("produce", CommanderDynamicMechanic.Produce,
                     CommanderDynamicResultKind.UnitResult, CommanderDynamicEffectClass.Production,
                     new[] { P("unit", CommanderDynamicFieldKind.UnitId),
                         P("count", CommanderDynamicFieldKind.Count),
                         P("quantityMode", CommanderDynamicFieldKind.QuantityMode) },
                     new[] { I("producers", CommanderDynamicResultKind.StructureSet, false) })
             });
 
         public static CommanderDynamicPrimitive Find(string id)
         {

## Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlanParser.cs
Before: FD283726DD078F2E61793FC2D192D17F50B916B0446DF7002EC43009F50F0054; current: E7618E45B5235DBB99FE945DC78DDA0F05157E04AB1C1D43F93D04C8B149EDFF
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicPlanParser.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicPlanParser.cs"
index 213df11..3863b47 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicPlanParser.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderDynamicPlanParser.cs"	
@@ -152,20 +152,23 @@ namespace OpenEmpires
                     break;
                 case CommanderDynamicFieldKind.Resource:
                     if (!NamedEnum<ResourceType>(s)) throw new JsonException();
                     break;
                 case CommanderDynamicFieldKind.SourceKind:
                     if (!NamedEnum<ResourceSourceKind>(s)) throw new JsonException();
                     break;
                 case CommanderDynamicFieldKind.WorkerState:
                     if (!NamedEnum<CommanderWorkerState>(s)) throw new JsonException();
                     break;
+                case CommanderDynamicFieldKind.WorkerCountMode:
+                    if (s != "AllMatching") throw new JsonException();
+                    break;
                 case CommanderDynamicFieldKind.ResourceMode:
                     if (s != "Visible" && s != "Worked") throw new JsonException();
                     break;
                 case CommanderDynamicFieldKind.Anchor:
                     if (s != "MyTownCenter" && s != "MyBarracks") throw new JsonException();
                     break;
                 case CommanderDynamicFieldKind.Relation:
                     if (s != "Near" && s != "MapWest" && s != "MapEast")
                         throw new JsonException();
                     break;
@@ -195,25 +198,40 @@ namespace OpenEmpires
                 throw new JsonException();
             if (mechanic == CommanderDynamicMechanic.SelectUnits && (int)parameters["count"] > 50)
                 throw new JsonException();
             if (mechanic == CommanderDynamicMechanic.SelectUnits &&
                 parameters.ContainsKey("unit") == parameters.ContainsKey("kind"))
                 throw new JsonException();
             if (mechanic == CommanderDynamicMechanic.SelectWorkers &&
                 parameters.ContainsKey("currentResource") &&
                 (!parameters.TryGetValue("state", out object state) ||
                     (string)state != "Gathering")) throw new JsonException();
+            if (mechanic == CommanderDynamicMechanic.SelectWorkers &&
+                (parameters.ContainsKey("count") == parameters.ContainsKey("countMode")
+                    || parameters.ContainsKey("countMode") &&
+                        (!parameters.TryGetValue("state", out object selectedState)
+                            || (string)selectedState == "Any"))) throw new JsonException();
             if (mechanic == CommanderDynamicMechanic.ResolveLocation &&
                 (Convert.ToInt32(parameters.ContainsKey("anchor")) +
                  Convert.ToInt32(inputs.ContainsKey("structures")) +
                  Convert.ToInt32(inputs.ContainsKey("resources"))) != 1)
                 throw new JsonException();
+            if (mechanic == CommanderDynamicMechanic.ResolveLocation &&
+                (parameters.ContainsKey("resource") != parameters.ContainsKey("sourceKind")
+                    || parameters.ContainsKey("resource") &&
+                        (!parameters.TryGetValue("anchor", out object sourceAnchor)
+                            || (string)sourceAnchor != "MyTownCenter"
+                            || (string)parameters["relation"] != "MapWest"
+                            || !ResourceSourceRules.IsCompatible(
+                                (ResourceType)Enum.Parse(typeof(ResourceType), (string)parameters["resource"]),
+                                (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind), (string)parameters["sourceKind"])))))
+                throw new JsonException();
             if (mechanic == CommanderDynamicMechanic.SelectResources ||
                 mechanic == CommanderDynamicMechanic.AllocateWorkers)
             {
                 var resource = (ResourceType)Enum.Parse(typeof(ResourceType),
                     (string)parameters["resource"]);
                 var source = (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
                     (string)parameters["sourceKind"]);
                 if (!ResourceSourceRules.IsCompatible(resource, source)) throw new JsonException();
             }
         }
@@ -253,21 +271,22 @@ namespace OpenEmpires
 
             // Selection is symbolic here. Only source-relative ranges are validated;
             // no worker runtime IDs are invented or exposed.
             var workerUses = new List<(int consumer, string root, int start, int end)>();
             for (int i = 0; i < nodes.Count; i++)
             {
                 if (nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.PartitionWorkers)
                     DynamicWorkerSpan(i, nodes, byId);
                 if (nodes[i].Inputs.TryGetValue("workers", out string source) &&
                     (nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.Build ||
-                     nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.AllocateWorkers))
+                     nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.AllocateWorkers ||
+                     nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.MoveWorkers))
                 {
                     var span = DynamicWorkerSpan(byId[source], nodes, byId);
                     workerUses.Add((i, span.root, span.start, span.end));
                 }
             }
             for (int i = 0; i < workerUses.Count; i++)
                 for (int j = i + 1; j < workerUses.Count; j++)
                 {
                     var a = workerUses[i]; var b = workerUses[j];
                     if (a.root == b.root && a.start < b.end && b.start < a.end &&
@@ -295,23 +314,26 @@ namespace OpenEmpires
             state[i] = 2;
             state[nodes.Count + i] = depth;
             return depth;
         }
 
         private static (string root, int start, int end) DynamicWorkerSpan(int i,
             List<CommanderDynamicNode> nodes, Dictionary<string, int> byId)
         {
             var node = nodes[i];
             if (node.Primitive.Mechanic == CommanderDynamicMechanic.SelectWorkers)
-                return (node.Id, 0, node.Parameter<int>("count"));
+                return (node.Id, 0, node.Parameters.ContainsKey("countMode")
+                    ? 200 : node.Parameter<int>("count"));
             if (node.Primitive.Mechanic != CommanderDynamicMechanic.PartitionWorkers)
                 throw new JsonException();
+            if (nodes[byId[node.Inputs["workers"]]].Parameters.ContainsKey("countMode"))
+                throw new JsonException("An ALL worker snapshot cannot be partitioned by an unprovided count.");
             var source = DynamicWorkerSpan(byId[node.Inputs["workers"]], nodes, byId);
             int offset = node.Parameter<int>("offset"), count = node.Parameter<int>("count");
             if (offset > source.end - source.start ||
                 count > source.end - source.start - offset) throw new JsonException();
             return (source.root, source.start + offset, source.start + offset + count);
         }
 
         private static bool DynamicDependsOn(int node, int ancestor,
             List<CommanderDynamicNode> nodes, Dictionary<string, int> byId)
         {

## Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicLocations.cs
Before: F2A83930F391DFEC3A6304A55BA1F36FB057EB7486E06DE60CCD7D88C4E468B9; current: 43F91A0D78851AF4A39AC3E952C9D0D7610EFFFC80D21BB74AED6FB1765A3E13
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderGoalManager.DynamicLocations.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderGoalManager.DynamicLocations.cs"
index 3d81dfa..63954a6 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderGoalManager.DynamicLocations.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderGoalManager.DynamicLocations.cs"	
@@ -55,21 +55,26 @@ namespace OpenEmpires
                 var relation = ParseRelation(node.Parameter<string>("relation"));
                 int gap = node.Parameter<int>("clearGapTiles");
                 CommanderBoundLocation bound;
                 if (node.Parameters.TryGetValue("anchor", out object semanticAnchor))
                 {
                     var selector = (CommanderSemanticAnchorSelector)Enum.Parse(
                         typeof(CommanderSemanticAnchorSelector), (string)semanticAnchor);
                     if (!resolver.TryResolveOwnedAnchor(playerId, selector, null,
                         out BuildingData anchor))
                         throw new ArgumentException("The owned semantic anchor is unavailable.");
-                    bound = CommanderBoundLocation.FromBuilding(simulation, this, playerId,
+                    if (node.Parameters.TryGetValue("resource", out object resourceValue))
+                        bound = CommanderBoundLocation.FromResourceSide(simulation, this, playerId,
+                            anchor, (ResourceType)Enum.Parse(typeof(ResourceType), (string)resourceValue),
+                            (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
+                                node.Parameter<string>("sourceKind")), gap);
+                    else bound = CommanderBoundLocation.FromBuilding(simulation, this, playerId,
                         anchor, relation, gap);
                 }
                 else if (node.Inputs.TryGetValue("structures", out string structureNodeId))
                 {
                     CommanderDynamicNode source = byId[structureNodeId];
                     if (source.Primitive.Mechanic == CommanderDynamicMechanic.SelectStructures)
                     {
                         IReadOnlyList<int> selected = structures[structureNodeId];
                         if (selected.Count != 1)
                             throw new ArgumentException("The placement needs one exact structure anchor.");
@@ -100,48 +105,71 @@ namespace OpenEmpires
                     bool worked = source.Parameter<string>("mode") == "Worked";
                     bound = CommanderBoundLocation.FromResource(simulation, this, playerId,
                         simulation.MapData.GetResourceNode(selected[0]), type, sourceKind,
                         worked, relation, gap);
                 }
                 else throw new ArgumentException("The semantic placement has no anchor.");
                 locations.Add(node.Id, bound);
             }
 
             var buildBindings = new List<(BuildStructureGoal goal, CommanderBoundLocation location)>();
+            var allocationBindings = new List<(AllocateWorkersGoal goal, CommanderBoundLocation location)>();
+            var moveBindings = new List<(CommanderCapabilityGoal goal, CommanderBoundLocation location)>();
             var producerBindings = new List<(EnsureUnitCountGoal goal, IReadOnlyList<int> ids)>();
             foreach (var node in program.Nodes)
             {
                 if (node.Primitive.Mechanic == CommanderDynamicMechanic.Build
                     && node.Inputs.TryGetValue("location", out string locationId))
                 {
                     if (!goalsById.TryGetValue(node.Id, out CommanderGoal effect)
                         || !(effect is BuildStructureGoal build)
                         || !locations.TryGetValue(locationId, out CommanderBoundLocation bound))
                         throw new ArgumentException("The building location is unavailable.");
                     buildBindings.Add((build, bound));
                 }
+                if (node.Primitive.Mechanic == CommanderDynamicMechanic.AllocateWorkers
+                    && node.Inputs.TryGetValue("location", out string resourceLocationId))
+                {
+                    if (!goalsById.TryGetValue(node.Id, out CommanderGoal effect)
+                        || !(effect is AllocateWorkersGoal allocation)
+                        || !locations.TryGetValue(resourceLocationId, out CommanderBoundLocation bound))
+                        throw new ArgumentException("The resource-side location is unavailable.");
+                    allocationBindings.Add((allocation, bound));
+                }
+                if (node.Primitive.Mechanic == CommanderDynamicMechanic.MoveWorkers)
+                {
+                    if (!goalsById.TryGetValue(node.Id, out CommanderGoal effect)
+                        || !(effect is CommanderCapabilityGoal move)
+                        || !node.Inputs.TryGetValue("location", out string moveLocationId)
+                        || !locations.TryGetValue(moveLocationId, out CommanderBoundLocation bound)
+                        || !bound.IsResourceSide)
+                        throw new ArgumentException("The exact resource-side move location is unavailable.");
+                    moveBindings.Add((move, bound));
+                }
                 if (node.Primitive.Mechanic == CommanderDynamicMechanic.Produce
                     && node.Inputs.TryGetValue("producers", out string producerNodeId)
                     && byId[producerNodeId].Primitive.Mechanic ==
                         CommanderDynamicMechanic.SelectStructures)
                 {
                     if (!goalsById.TryGetValue(node.Id, out CommanderGoal effect)
                         || !(effect is EnsureUnitCountGoal produce)
                         || !structures.TryGetValue(producerNodeId, out IReadOnlyList<int> selected)
                         || selected.Count < 1)
                         throw new ArgumentException("The exact producer set is unavailable.");
                     producerBindings.Add((produce, Array.AsReadOnly(Copy(selected))));
                 }
             }
 
             foreach (BuildStructureGoal source in futureAnchors) source.HasResultConsumer = true;
             foreach (var binding in buildBindings) binding.goal.DynamicLocation = binding.location;
+            foreach (var binding in allocationBindings) binding.goal.DynamicResourceLocation = binding.location;
+            foreach (var binding in moveBindings) binding.goal.DynamicMoveLocation = binding.location;
             foreach (var binding in producerBindings) binding.goal.BoundProducerBuildingIds = binding.ids;
         }
 
         private IReadOnlyList<int> FreezeResources(CommanderDynamicNode node)
         {
             var type = (ResourceType)Enum.Parse(typeof(ResourceType),
                 node.Parameter<string>("resource"));
             var source = (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
                 node.Parameter<string>("sourceKind"));
             bool worked = node.Parameter<string>("mode") == "Worked";

## Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicWorkers.cs
Before: 9F33ED484A0161AE782AC04553DBFA385EBEE8BE93A2AD1D30122C1A49275D86; current: 7341B2F2B544EEC2D12045DB3DB6B52D9F5E833CF99A0A5E1D2AA5D547807D39
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderGoalManager.DynamicWorkers.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderGoalManager.DynamicWorkers.cs"
index 1115eb4..d2e750c 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderGoalManager.DynamicWorkers.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderGoalManager.DynamicWorkers.cs"	
@@ -14,21 +14,22 @@ namespace OpenEmpires
             var nodes = program.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
             var selections = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
             var sourceClaims = new HashSet<int>();
             var effectClaims = new HashSet<int>();
             var effectOwners = new Dictionary<int, CommanderGoal>();
 
             IReadOnlyList<int> ResolveWorkers(string id)
             {
                 if (selections.TryGetValue(id, out var captured)) return captured;
                 var node = nodes[id];
-                int count = node.Parameter<int>("count");
+                bool allMatching = node.Parameters.ContainsKey("countMode");
+                int count = allMatching ? 0 : node.Parameter<int>("count");
                 int[] ids;
                 if (node.Primitive.Mechanic == CommanderDynamicMechanic.PartitionWorkers)
                 {
                     var source = ResolveWorkers(node.Inputs["workers"]);
                     int offset = node.Parameter<int>("offset");
                     if (offset < 0 || count < 1 || count > source.Count - offset)
                         throw new InvalidOperationException("A shared worker partition is outside its frozen source.");
                     ids = source.Skip(offset).Take(count).ToArray();
                 }
                 else if (node.Primitive.Mechanic == CommanderDynamicMechanic.SelectWorkers)
@@ -47,24 +48,30 @@ namespace OpenEmpires
                         if (state == "Idle" && worker.State != UnitState.Idle) return false;
                         if (program.Constraints.Any(c => c is PreferredWorkersConstraint preference
                             && preference.WorkerSource == CommanderPreferredWorkerSource.IdleOnly)
                             && worker.State != UnitState.Idle) return false;
                         if (state == "Gathering" && !gathering) return false;
                         if (state == "Any" && worker.State != UnitState.Idle && !gathering) return false;
                         var current = simulation.MapData.GetResourceNode(worker.TargetResourceNodeId);
                         return !resource.HasValue || (gathering && current != null && !current.IsDepleted
                             && current.Type == resource.Value);
                     }
-                    ids = simulation.UnitRegistry.GetAllUnits().Where(Matches).OrderBy(w => w.Id)
-                        .Take(count).Select(w => w.Id).ToArray();
-                    if (ids.Length != count)
+                    int bound = Math.Min(200, simulation.Config.MaxPopulation);
+                    ids = allMatching
+                        ? simulation.UnitRegistry.GetAllUnits().Where(Matches).Take(bound + 1)
+                            .Select(w => w.Id).ToArray()
+                        : simulation.UnitRegistry.GetAllUnits().Where(Matches).OrderBy(w => w.Id)
+                            .Take(count).Select(w => w.Id).ToArray();
+                    if (allMatching ? ids.Length < 1 || ids.Length > bound
+                        : ids.Length != count)
                         throw new InvalidOperationException("The full shared worker selection is unavailable; no goals or reservations were admitted.");
+                    if (allMatching) Array.Sort(ids);
                     foreach (int worker in ids) sourceClaims.Add(worker);
                 }
                 else throw new InvalidOperationException("The worker reference is incompatible.");
                 var frozen = Array.AsReadOnly(ids);
                 selections.Add(id, frozen);
                 return frozen;
             }
 
             // Freeze source queries once, in documented AST order, before partitioning.
             foreach (var source in program.Nodes)

## Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs
Before: 41F46C84120C151B94BBD129088D6499A3F8F869D84FED83EE17A20A27407A24; current: E56883BBF3391DEA699BE9DC61E301B21CC7F787AE262E88ACFA54FEB58B4CD8
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanner.DynamicPlacement.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanner.DynamicPlacement.cs"
index 5e60fc2..6b634e4 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanner.DynamicPlacement.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanner.DynamicPlacement.cs"	
@@ -28,32 +28,69 @@ namespace OpenEmpires
                 return new CommanderPlan(CommanderGoalStatus.Blocked,
                     $"No eligible owned villager is available to build {type}.", owned, queued);
             }
 
             GetFootprint(type, out int width, out int height);
             IReadOnlyList<Vector2Int> candidates = CommanderSemanticPlacementCandidates.Generate(
                 anchorX, anchorZ, anchorWidth, anchorHeight, width, height,
                 simulation.MapData.Width, simulation.MapData.Height,
                 location.Relation, location.ClearGapTiles);
             int border = type == BuildingType.Farm ? 0 : 1;
+            // Before placing the first building, require enough mutually legal sites
+            // for the entire repeated request. Later external occupancy/authority
+            // changes may still block honestly; a known impossible prefix is never issued.
+            if (goal.Count > 1 && goal.AttributedBuildingIds.Count == 0
+                && !HasFullDynamicSiteSet(goal, type, currentTick, candidates,
+                    width, height, border))
+            {
+                goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
+                return new CommanderPlan(CommanderGoalStatus.Blocked,
+                    $"Only a partial set of distinct legal {type} sites is available near the exact anchor; "
+                    + $"none of the requested {goal.Count} placements was started.", owned, queued);
+            }
             for (int i = 0; i < candidates.Count; i++)
             {
                 Vector2Int tile = candidates[i];
                 if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y, width, height,
                     border, type)) continue;
                 UnitData builder = FindReachableSemanticBuilder(goal, tile, width,
                     height, currentTick);
                 if (builder == null) continue;
                 goal.PlacementBlocker = CommanderPlacementBlocker.None;
                 return new CommanderPlan(CommanderGoalStatus.Executing,
                     $"Placing {type} at ({tile.x},{tile.y}) from its exact semantic anchor "
                     + $"with villager #{builder.Id}.", owned, queued,
                     new PlaceBuildingCommand(goal.PlayerId, type, tile.x, tile.y,
                         new[] { builder.Id }));
             }
             goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
             return new CommanderPlan(CommanderGoalStatus.Blocked,
                 $"No visible, buildable, reachable location satisfies the exact {type} "
                 + "anchor and bounded relation.", owned, queued);
         }
+
+        private bool HasFullDynamicSiteSet(BuildStructureGoal goal, BuildingType type,
+            int currentTick, IReadOnlyList<Vector2Int> candidates,
+            int width, int height, int border)
+        {
+            var selected = new List<Vector2Int>(goal.Count);
+            for (int i = 0; i < candidates.Count && selected.Count < goal.Count; i++)
+            {
+                Vector2Int tile = candidates[i];
+                if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y,
+                    width, height, border, type)) continue;
+                bool conflicts = false;
+                for (int j = 0; j < selected.Count; j++)
+                {
+                    Vector2Int prior = selected[j];
+                    if (tile.x - border < prior.x + width && tile.x + width + border > prior.x
+                        && tile.y - border < prior.y + height && tile.y + height + border > prior.y)
+                    { conflicts = true; break; }
+                }
+                if (conflicts || FindReachableSemanticBuilder(goal, tile,
+                    width, height, currentTick) == null) continue;
+                selected.Add(tile);
+            }
+            return selected.Count == goal.Count;
+        }
     }
 }

## Assets/Scripts/AI/Commander/Phase5A/CommanderPlanPreview.Descriptions.cs
Before: 760DFD7F5BC570EC04D6365F992D95E1B76B4E33583EEFE6C7F14F8F2AED9FC1; current: A6B5ECBE0A23B56B97C6B7BC0B50177F162C58D234AE4B4C5B70A026F5913D76
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanPreview.Descriptions.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanPreview.Descriptions.cs"
index 4d71220..7b5a91e 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanPreview.Descriptions.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanPreview.Descriptions.cs"	
@@ -56,43 +56,46 @@ namespace OpenEmpires
                 case CommanderLocationSelectorKind.PlayerBase:return "a point at your base";
                 case CommanderLocationSelectorKind.WorkedResource:return "a point at your worked "+selector.ResourceType+" resource";
                 case CommanderLocationSelectorKind.VisibleResource:return "a visible "+selector.ResourceType+" resource point";
                 case CommanderLocationSelectorKind.VisibleEnemy:return "a currently visible enemy target";
                 case CommanderLocationSelectorKind.RelativeToSelectedUnits:return "the center of the selected units";
                 default:throw new ArgumentException("Unsupported action location.");
             }
         }
         private static string DescribeAction(CapabilityActionIntent action,string resultBuildingName)
         {
-            string actors=action.UnitSelector.Count+" owned "+(action.UnitSelector.Kind==CommanderUnitSelectorKind.UnitType
+            string actors=(action.UnitSelector.AllMatching?"all currently eligible owned ":action.UnitSelector.Count+" owned ")+(action.UnitSelector.Kind==CommanderUnitSelectorKind.UnitType
                 ?CommanderIntentCatalog.GetUnitDisplayName(action.UnitSelector.UnitType):UnitKind(action.UnitSelector.Kind.ToString()));
             string location=Location(action.LocationSelector);
             string target=action.TargetSelector.HasValue
                 ?action.TargetSelector.Value.Kind==CommanderTargetSelectorKind.UnitType
                     ?"a visible enemy "+CommanderIntentCatalog.GetUnitDisplayName(action.TargetSelector.Value.UnitType)
                     :(action.ActionType==CommanderCapabilityActionType.RepairTarget?"your ":"a visible enemy ")+Words(CommanderIntentCatalog.GetStructureDisplayName(action.TargetSelector.Value.StructureType.Value))
                 :action.ActionType==CommanderCapabilityActionType.RepairTarget?"an eligible damaged owned building":location;
             string effect;
             switch(action.ActionType)
             {
                 case CommanderCapabilityActionType.MoveUnits:effect="Move "+actors+" to "+location;break;
                 case CommanderCapabilityActionType.ScoutArea:effect="Scout by moving "+actors+" to "+location;break;
+                case CommanderCapabilityActionType.DiscoverEnemy:effect="Discover "+target+" with one owned Scout over a finite route of known frontier waypoints; complete only when the requested enemy becomes visibly observed; stop on route exhaustion or scout loss";break;
                 case CommanderCapabilityActionType.PatrolArea:effect="Patrol with "+actors+"; point patrol from each starting position to "+location;break;
                 case CommanderCapabilityActionType.DefendArea:effect="Move "+actors+" to "+location+" to help defend";break;
                 case CommanderCapabilityActionType.RetreatUnits:effect="Retreat "+actors+" to "+location;break;
                 case CommanderCapabilityActionType.AttackTarget:effect="Attack "+target+" with "+actors;break;
                 case CommanderCapabilityActionType.RepairTarget:effect="Repair "+target+" with "+actors;break;
                 case CommanderCapabilityActionType.SetRallyPoint:effect="Set the rally point of "+(resultBuildingName!=null?"the exact "+resultBuildingName:"your "+Words(CommanderIntentCatalog.GetStructureDisplayName(action.StructureType??BuildingType.Barracks)))+" at "+location;break;
                 case CommanderCapabilityActionType.ResearchTechnology:effect="Research "+Words(action.Technology?.ToString())+" using an eligible completed research building";break;
+                case CommanderCapabilityActionType.GarrisonUnits:effect="Garrison up to "+actors+" in one exact owned "+Words(CommanderIntentCatalog.GetStructureDisplayName(action.StructureType.Value))+"; report protected and unaccommodated remainder; no alternate shelter fallback";break;
+                case CommanderCapabilityActionType.UngarrisonBuilding:effect="Ungarrison every housed unit from one exact owned "+Words(CommanderIntentCatalog.GetStructureDisplayName(action.StructureType.Value))+" (building-wide; selective egress is unsupported)";break;
                 default:throw new ArgumentException("Unsupported preview effect.");
             }
-            if(action.TargetSelector.HasValue)effect+="; no unrelated target substitutes (normal gameplay may retarget after dispatch)";
+            if(action.TargetSelector.HasValue && action.ActionType!=CommanderCapabilityActionType.DiscoverEnemy)effect+="; no unrelated target substitutes (normal gameplay may retarget after dispatch)";
             if(action.LocationSelector.RadiusTiles.HasValue)effect+="; unsupported radius constraint "+action.LocationSelector.RadiusTiles.Value;
             return effect;
         }
         private static string ProducerDescription(CommanderSemanticGraphPlan graph,int index)
         {
             if(!(graph.Nodes[index].Intent is BuildStructureIntent build))throw new ArgumentException("Unsupported producer preview binding.");
             return "only the "+Words(CommanderIntentCatalog.GetStructureDisplayName(build.StructureType))+" built in step "+(index+1)
                 +(build.Count>1?" (all "+build.Count+" buildings from that step)":"")+"; no other producer substitutes";
         }
         private static string ResultDescription(CommanderSemanticGraphPlan graph,int index)
@@ -133,37 +136,44 @@ namespace OpenEmpires
         {
             text.Append("Selections and placement (fixed for this plan):\n");
             foreach(var node in graph.DynamicProgram.Nodes)
             {
                 string description;
                 switch(node.Primitive.Mechanic)
                 {
                     case CommanderDynamicMechanic.SelectWorkers:
                         string state=node.Parameters.TryGetValue("state",out object s)?(string)s:"Any";
                         string resource=node.Parameters.TryGetValue("currentResource",out object r)?(string)r:null;
-                        description="Choose "+node.Parameter<int>("count")+" "+WorkerDescription(state,resource)+" once";break;
+                        description=node.Parameters.ContainsKey("countMode")
+                            ? "Choose all currently "+WorkerDescription(state,resource)+" once as a bounded frozen snapshot"
+                            : "Choose "+node.Parameter<int>("count")+" "+WorkerDescription(state,resource)+" once";break;
                     case CommanderDynamicMechanic.PartitionWorkers:
                         int first=node.Parameter<int>("offset")+1,last=first+node.Parameter<int>("count")-1;
                         description="Use "+(first==last?"villager "+first:"villagers "+first+"–"+last)+" from "+Label(graph,node.Inputs["workers"])+" as a fixed group";break;
                     case CommanderDynamicMechanic.SelectUnits:
                         description="Choose "+node.Parameter<int>("count")+" visible owned "+(node.Parameters.TryGetValue("unit",out object unit)?UnitName((string)unit):UnitKind(node.Parameter<string>("kind")))+" once";break;
                     case CommanderDynamicMechanic.SelectStructures:
                         description="Choose "+node.Parameter<int>("count")+" visible completed owned "+BuildingName(node.Parameter<string>("building"))+" once";break;
                     case CommanderDynamicMechanic.SelectResources:
                         description="Choose "+node.Parameter<int>("count")+" "+(node.Parameter<string>("mode")=="Worked"?"currently worked and visible":"visible")+" "+Destination((ResourceType)Enum.Parse(typeof(ResourceType),node.Parameter<string>("resource")),(ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),node.Parameter<string>("sourceKind")))+" resource anchor(s) once";break;
                     case CommanderDynamicMechanic.ResolveLocation:
                         string anchor=node.Parameters.TryGetValue("anchor",out object a)?Anchor((string)a)
                             :node.Inputs.TryGetValue("structures",out string structure)?"the exact building from "+Label(graph,structure)
                             :"the exact resource from "+Label(graph,node.Inputs["resources"]);
-                        description=Relation(node.Parameter<string>("relation"))+" "+anchor+"; "+node.Parameter<int>("clearGapTiles")+"-tile clear gap between footprints within bounded tolerance; report a blocker if no valid site exists";break;
+                        description=node.Parameters.ContainsKey("resource")
+                            ? "Only currently visible "+node.Parameter<string>("sourceKind")+"/"+node.Parameter<string>("resource")
+                                +" sources "+Relation(node.Parameter<string>("relation"))+" "+anchor
+                                +"; at least "+node.Parameter<int>("clearGapTiles")+" clear tile(s); freeze eligible source identities at admission"
+                            : Relation(node.Parameter<string>("relation"))+" "+anchor+"; "+node.Parameter<int>("clearGapTiles")+"-tile clear gap between footprints within bounded tolerance; report a blocker if no valid site exists";break;
                     case CommanderDynamicMechanic.Build:
                     case CommanderDynamicMechanic.AllocateWorkers:
+                    case CommanderDynamicMechanic.MoveWorkers:
                     case CommanderDynamicMechanic.Produce:continue; // Fully disclosed by effect lines and input bindings.
                     default:throw new ArgumentException("Unsupported preview selection.");
                 }
                 text.Append("  ").Append(Label(graph,node.Id)).Append(": ").Append(description);
                 if(node.DependsOn.Count>0)text.Append("; requires ").Append(string.Join(", ",node.DependsOn.Select(id=>Label(graph,id))));
                 text.Append('\n');
             }
         }
     }
 }

## Assets/Scripts/AI/Commander/Phase5A/CommanderScopeEquivalence.cs
Before: DDCC31CCED59A47348FE24DD70C2E4C47858B5A2880109FE25F4AB6AE0A39225; current: 6C3880FF2719517FCB42152C780A4B744955DF762D9877962FE9D91A4A9C5085
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderScopeEquivalence.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderScopeEquivalence.cs"
index 49cd37f..b2ddbf1 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderScopeEquivalence.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderScopeEquivalence.cs"	
@@ -27,21 +27,23 @@ namespace OpenEmpires
             if (a is ReachAgeIntent age && b is ReachAgeIntent next)
                 return age.RequestedTarget == next.RequestedTarget && age.TargetAge == next.TargetAge;
             if (a is AllocateWorkersIntent w && b is AllocateWorkersIntent z) return SameAllocation(w.Allocation, z.Allocation);
             if (a is WatchFutureUnitsIntent f && b is WatchFutureUnitsIntent g)
                 return f.UnitType == g.UnitType && f.Count == g.Count
                     && f.ProducerType == g.ProducerType && f.ProducerOrdinal == g.ProducerOrdinal
                     && f.Action == g.Action && f.Resource == g.Resource && f.SourceKind == g.SourceKind;
             if (a is CapabilityActionIntent c && b is CapabilityActionIntent d)
                 return c.ActionType == d.ActionType && c.Technology == d.Technology && c.StructureType == d.StructureType
                     && c.UnitSelector.Kind == d.UnitSelector.Kind && c.UnitSelector.UnitType == d.UnitSelector.UnitType
-                    && c.UnitSelector.Count == d.UnitSelector.Count && c.LocationSelector.Kind == d.LocationSelector.Kind
+                    && c.UnitSelector.Count == d.UnitSelector.Count
+                    && c.UnitSelector.AllMatching == d.UnitSelector.AllMatching
+                    && c.LocationSelector.Kind == d.LocationSelector.Kind
                     && c.LocationSelector.ResourceType == d.LocationSelector.ResourceType
                     && c.LocationSelector.RadiusTiles == d.LocationSelector.RadiusTiles
                     && c.TargetSelector.HasValue == d.TargetSelector.HasValue
                     && (!c.TargetSelector.HasValue || c.TargetSelector.Value.Kind == d.TargetSelector.Value.Kind
                         && c.TargetSelector.Value.UnitType == d.TargetSelector.Value.UnitType
                         && c.TargetSelector.Value.StructureType == d.TargetSelector.Value.StructureType);
             return false; // Unknown runtime type fails closed, even when enum/display happens to match.
         }
 
         private static bool SameAllocation(CommanderWorkerAllocation a, CommanderWorkerAllocation b)

## Assets/Scripts/AI/Commander/Phase5C/CommanderSemanticRequestPack.cs
Before: 2F57C17FA503AAEB7AF2ED019A146D187217FC15C96CCC55021787F24ACFD677; current: B6B7B2FE26DF1D0328DC6A59416475D1079B2CE9F9226D243689B95FB24ECC45
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5C\\CommanderSemanticRequestPack.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5C\\CommanderSemanticRequestPack.cs"
index 7193bbc..6d8cb58 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5C\\CommanderSemanticRequestPack.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5C\\CommanderSemanticRequestPack.cs"	
@@ -85,21 +85,24 @@ namespace OpenEmpires
                 foreach (CommanderCapabilityActionType capability in Enum.GetValues(typeof(CommanderCapabilityActionType)))
                     if (capability != CommanderCapabilityActionType.ResearchTechnology
                         && (fallbackAll || MatchesAction(capability, words))
                         && Enum.TryParse(capability.ToString(), out CommanderSemanticNodeType type)) selected.Add(type);
 
             string instruction = request.IsReadOnlyQuestion ? ReadOnlyRules : CommonRules + Vocabulary(selected);
             // Contract retrieval only: quantities on future units must not imply repeated buildings.
             bool repeated = Has("split", "partition") || construction && (Has("third") || Regex.IsMatch(search,
                 @"\b(?:build|construct|make)\s+(?:two|three|four|five|six|seven|eight|nine|ten|[2-9]|1[0-9]|20)\b",
                 RegexOptions.IgnoreCase));
+            bool composedWoodline = Has("all") && Has("wood", "woodline", "trees", "tree")
+                && Has("west", "western", "left") && Has("town", "center", "tc");
             if (repeated && !request.IsReadOnlyQuestion) instruction += DynamicVocabulary();
+            else if (composedWoodline && !request.IsReadOnlyQuestion) instruction += ComposedWoodlineVocabulary;
             bool optionalStripped = false;
             while (true)
             {
             string packedInstruction = instruction;
             if (knowledge?["nameInterpretations"] is JArray possibleHints
                 && possibleHints.OfType<JObject>().Any(hint => (bool?)hint["ambiguous"] == false))
                 packedInstruction += " Similar-name hints are interpretation only; Clarify before Request, never substitute a suggested name as authority.";
             JObject compact = CompactContext(context, strategy);
             if (knowledge?["requiredCanonicalIds"] is JArray requiredIds
                 && !RequiredRowsPresent(requiredIds, compact["knowledge"] as JObject))
@@ -174,21 +177,24 @@ namespace OpenEmpires
 
         private const string ReadOnlyRules = "Return one JSON object, no markdown: {\"outcome\":\"Answer\",\"message\":\"brief grounded answer\"}, "
             + "{\"outcome\":\"Clarify\",\"message\":\"specific missing fact\"} or {\"outcome\":\"Unsupported\",\"message\":\"reason\"}. "
             + "Message<=180 chars. INFORMATION ONLY: no nodes/plans/approval. Use owned task facts; unknown facts remain unknown. No hidden state, IDs or commands.";
         private static bool MatchesAction(CommanderCapabilityActionType type, HashSet<string> words)
         {
             bool Has(params string[] terms) => terms.Any(words.Contains);
             switch (type)
             {
                 case CommanderCapabilityActionType.MoveUnits: return Has("move", "send", "march", "go");
-                case CommanderCapabilityActionType.ScoutArea: return Has("scout", "explore", "find", "discover");
+                case CommanderCapabilityActionType.ScoutArea: return Has("scout", "explore") && !Has("find", "discover");
+                case CommanderCapabilityActionType.DiscoverEnemy: return Has("find", "discover") && Has("enemy", "base", "town", "center");
+                case CommanderCapabilityActionType.GarrisonUnits: return Has("garrison");
+                case CommanderCapabilityActionType.UngarrisonBuilding: return Has("ungarrison", "evacuate");
                 case CommanderCapabilityActionType.PatrolArea: return Has("patrol");
                 case CommanderCapabilityActionType.SetRallyPoint: return Has("rally");
                 case CommanderCapabilityActionType.AttackTarget: return Has("attack", "strike");
                 case CommanderCapabilityActionType.DefendArea: return Has("defend", "defense", "guard", "protect");
                 case CommanderCapabilityActionType.RetreatUnits: return Has("retreat", "withdraw");
                 case CommanderCapabilityActionType.RepairTarget: return Has("repair", "fix");
                 case CommanderCapabilityActionType.ResearchTechnology: return false;
                 default: throw new InvalidOperationException("No capability retrieval mapping: " + type);
             }
         }
@@ -227,44 +233,53 @@ namespace OpenEmpires
                             throw new InvalidOperationException("Semantic family has no compact contract: " + type);
                         break; // Registered capability actions share one field contract below.
                 }
             }
             var actions = selected.Where(t => t != CommanderSemanticNodeType.ResearchTechnology
                 && Enum.TryParse(t.ToString(), out CommanderCapabilityActionType _)).OrderBy(t => t).ToArray();
             if (actions.Length > 0)
             {
                 text.Append("Action type:").Append(string.Join("|", actions))
                     .Append("; unitSelector,count:1..50,location,resource/structure?,resultFromNode/dependsOn?. ")
-                    .Append("unitSelector:Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary; location:PlayerBase|WorkedResource|VisibleResource|VisibleEnemy|RelativeToSelectedUnits. ALL/current-resource workers/composed side+resource+TC location unsupported: Clarify/Unsupported,never drop criteria. VisibleEnemy cannot discover unseen base;point only,no radius. ");
+                    .Append("unitSelector:Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary; location:PlayerBase|WorkedResource|VisibleResource|VisibleEnemy|RelativeToSelectedUnits. Ordinary ScoutArea is one visible point, not enemy discovery; no radius. Keep explicit source/side/current-resource criteria; use the typed DynamicPlan composition when advertised, otherwise Clarify/Unsupported, never drop criteria. ");
                 if (actions.Contains(CommanderSemanticNodeType.AttackTarget) || actions.Contains(CommanderSemanticNodeType.RepairTarget))
                     text.Append("Attack/Repair target?{kind:UnitType,unit}|{kind:BuildingType,structure}. ");
                 if (actions.Contains(CommanderSemanticNodeType.SetRallyPoint)) text.Append("Rally uses owned producer. ");
+                if (actions.Contains(CommanderSemanticNodeType.GarrisonUnits))
+                    text.Append("GarrisonUnits{unitSelector:Villagers,countMode:AllMatching omit count OR count:1..50,location:PlayerBase,structure:owned shelter type}; exact shelter, actual native entry/capacity and protected/unaccommodated remainder; no other shelter fallback. ");
+                if (actions.Contains(CommanderSemanticNodeType.UngarrisonBuilding))
+                    text.Append("UngarrisonBuilding{location:PlayerBase,structure:owned shelter type}; building-wide egress, not selective. ");
+                if (actions.Contains(CommanderSemanticNodeType.DiscoverEnemy))
+                    text.Append("DiscoverEnemy{unitSelector:Scout,count:1,location:VisibleEnemy,target:{kind:UnitType,unit}|{kind:BuildingType,structure}}; finite known-frontier Scout+Move route, complete only on legitimately visible requested enemy type; no hidden coordinates or resultFromNode. ");
             }
             text.Append("resource:Food|Wood|Gold|Stone; sourceKind:Any or Food=Sheep|Berries|Farm,Wood=Tree,Gold=GoldMine,Stone=StoneMine. "
                 + "constraints?<=4 objects type:NoConstruction;PreferredWorkers mode:IdleOnly;ProtectedResource resource,amount?:0..200;MaximumQueue amount1..8;ResourceSource resource,sourceKind. ");
             return text.ToString();
         }
 
         private static string DynamicVocabulary()
         {
             var text = new StringBuilder("DynamicPlan for repeated placed construction/shared workers: {outcome:DynamicPlan,version:1,nodes:[{id:symbol,mechanic,parameters:{},inputs:{},dependsOn:[]}]}. One shared worker selection, partition disjointly; exact producer set for explicitly new buildings. Registry fields below use name:type; ? optional; input refs also in dependsOn. ");
             foreach (var primitive in CommanderDynamicPrimitiveRegistry.All)
             {
                 text.Append(primitive.Id).Append(" p[");
                 foreach (var field in primitive.Parameters) text.Append(field.Name).Append(':').Append(field.Kind).Append(field.Required ? ";" : "?;");
                 text.Append("] in[");
                 foreach (var input in primitive.Inputs) text.Append(input.Role).Append(':').Append(input.Accepts).Append(input.Required ? ";" : "?;");
                 text.Append("] ->").Append(primitive.Result).Append('.');
             }
             return text.Append(" Max12 nodes,4 refs/node,depth5,total counts200. Canonical IDs from context only; relation Near|MapWest|MapEast, Near gap1; resource mode Visible|Worked. Simple known requests keep Request fast path. ").ToString();
         }
 
+        private const string ComposedWoodlineVocabulary =
+            "For all current-Wood villagers and west-of-owned-TC Wood/Tree: DynamicPlan version1 with select-workers{state:Gathering,currentResource:Wood,countMode:AllMatching,omit count} -> WorkerSet; resolve-location{anchor:MyTownCenter,relation:MapWest,clearGapTiles:1,resource:Wood,sourceKind:Tree} -> LocationIntent. For explicit move, move-workers inputs workers:select-workers,location:resolve-location -> native Move (not Gather); for explicit gather, allocate-workers{resource:Wood,sourceKind:Tree} with same inputs -> native Gather. Include dependsOn refs; never replace ALL with count50 or silently change Move to Gather. Game binds owned workers/source/anchor and refuses overflow. No coordinates/IDs. ";
+
         private static JObject CompactContext(JObject source, bool includeStrategy)
         {
             var result = new JObject();
             foreach (string key in new[] { "age", "civilization", "resources", "population", "populationCap", "workerCounts", "tacticalStatus" })
                 if (source[key] != null) result[key] = source[key].DeepClone();
             if (source["knowledge"] is JObject knowledge)
             {
                 var facts = new JObject();
                 var supportedIds = new HashSet<string>(StringComparer.Ordinal);
                 foreach (string field in new[] { "canonicalUnitIds", "canonicalBuildingIds" })

## Assets/Scripts/Commands/CommandBuffer.cs
Before: D4E865F98E6E2C4A29A85EF92D8508604823A5D3AC0CA1D5E798849E1585E5A1; current: B76262F4E32938E2A8C6B899493BC7216A10AD16710AC9880D080771364CC445
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\Commands\\CommandBuffer.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Commands\\CommandBuffer.cs"
index 5390049..01b3960 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\Commands\\CommandBuffer.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Commands\\CommandBuffer.cs"	
@@ -40,12 +40,38 @@ namespace OpenEmpires
         internal bool RemovePendingExact(ICommand command)
         {
             for (int i = 0; i < pendingCommands.Count; i++)
                 if (ReferenceEquals(pendingCommands[i], command))
                 {
                     pendingCommands.RemoveAt(i);
                     return true;
                 }
             return false;
         }
+
+        // Shared command-subject extraction for human takeover and local native
+        // order-affinity checks. This does not change command serialization.
+        internal static int[] GetSubjectUnitIds(ICommand command)
+        {
+            switch (command)
+            {
+                case MoveCommand value: return value.UnitIds;
+                case GatherCommand value: return value.UnitIds;
+                case StopCommand value: return value.UnitIds;
+                case AttackBuildingCommand value: return value.UnitIds;
+                case AttackUnitCommand value: return value.UnitIds;
+                case ConstructBuildingCommand value: return value.UnitIds;
+                case DropOffCommand value: return value.UnitIds;
+                case GarrisonCommand value: return value.UnitIds;
+                case PatrolCommand value: return value.UnitIds;
+                case DeleteUnitsCommand value: return value.UnitIds;
+                case FollowUnitCommand value: return value.UnitIds;
+                case HealUnitCommand value: return value.UnitIds;
+                case RepairBuildingCommand value: return value.UnitIds;
+                case PlaceBuildingCommand value: return value.VillagerUnitIds;
+                case PlaceWallCommand value: return value.VillagerUnitIds;
+                case SlaughterSheepCommand value: return value.VillagerIds;
+                default: return null;
+            }
+        }
     }
 }

## Assets/Scripts/Core/CommanderCommandOriginLedger.cs
Before: 55F65DC6897EF9B3ABBAF3B9783F4C16A252527D4D4D564F760D2A493EC45B24; current: 3387D36D246BB43B8F59F8CC5874EF38929E2FB8FC6D96C07FD76C47D4BB782E
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\Core\\CommanderCommandOriginLedger.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Core\\CommanderCommandOriginLedger.cs"
index 4410445..9249279 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\Core\\CommanderCommandOriginLedger.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Core\\CommanderCommandOriginLedger.cs"	
@@ -96,54 +96,65 @@ namespace OpenEmpires
                     || !batch.Stamped[i].Equals(replayed))
                 {
                     Discard(batch, simulation);
                     return verified;
                 }
             }
 
             // Without an explicit relay FIFO contract, equal payloads from distinct
             // original boxes cannot be assigned to their true issuers by ordinal.
             var ambiguousMoves = new HashSet<int>();
+            var ambiguousGarrisons = new HashSet<int>();
             for (int i = 0; i < batch.Stamped.Length; i++)
             {
                 for (int j = i + 1; j < batch.Stamped.Length; j++)
                 {
                     if (batch.Stamped[i].Equals(batch.Stamped[j])
                         && !ReferenceEquals(batch.Originals[i], batch.Originals[j]))
                     {
                         if (batch.Originals[i] is MoveCommand)
                         {
                             // Identical relay payloads have no provable individual
                             // origin; leave only these Move receipts unattributed.
                             ambiguousMoves.Add(i);
                             ambiguousMoves.Add(j);
                             continue;
                         }
+                        if (batch.Originals[i] is GarrisonCommand)
+                        {
+                            // An opt-in exact order cannot inherit a different
+                            // identical box's origin; native rejects unmatched
+                            // replay candidates while its marker is pending.
+                            ambiguousGarrisons.Add(i);
+                            ambiguousGarrisons.Add(j);
+                            continue;
+                        }
                         if (batch.Originals[i] is PlaceBuildingCommand
                             || batch.Originals[j] is PlaceBuildingCommand
                             || simulation.HasPendingTrainingOrigin(batch.Originals[i])
                             || simulation.HasPendingTrainingOrigin(batch.Originals[j]))
                         {
                             Discard(batch, simulation);
                             return verified;
                         }
                     }
                 }
             }
 
             for (int i = 0; i < batch.Originals.Length; i++)
             {
                 ICommand original = batch.Originals[i];
                 if (!(original is PlaceBuildingCommand) && !(original is MoveCommand)
+                    && !(original is GarrisonCommand)
                     && (!(original is TrainUnitCommand) || !simulation.HasPendingTrainingOrigin(original)))
                     continue;
-                if (ambiguousMoves.Contains(i)) continue;
+                if (ambiguousMoves.Contains(i) || ambiguousGarrisons.Contains(i)) continue;
                 // A replay box must identify only one original box in this tick.
                 if (verified.ContainsKey(local[i]))
                 {
                     verified.Clear();
                     Discard(batch, simulation);
                     return verified;
                 }
                 verified.Add(local[i], original);
             }
             return verified;

## Assets/Scripts/Core/GameSimulation.cs
Before: 8BB4822858164CAA48D1A2868D2409D16CE5DC63AFC4B4E5305B8E7118D821D0; current: 5EA5528B8B062424CAEC3F7C9013D3050744EBC485108C9BF4FAF13E6E6898CA
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\Core\\GameSimulation.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Core\\GameSimulation.cs"
index ec031ad..df3f360 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Scripts\\Core\\GameSimulation.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Core\\GameSimulation.cs"	
@@ -1710,20 +1710,23 @@ namespace OpenEmpires
                         unit.CombatTargetBuildingId = -1;
                     }
                     return;
                 }
             }
         }
 
         private void ProcessCommand(ICommand command, ICommand verifiedOriginal = null)
         {
             if (ConsumeSuppressedLocalAction(command)) return;
+            if (RejectUnattributedExactGarrison(command, verifiedOriginal)) return;
+            if (!ObserveExactGarrisonCommand(command, verifiedOriginal ?? command,
+                out bool exactGarrison)) return;
             switch (command)
             {
                 case MoveCommand move:
                     ProcessMoveCommand(move, verifiedOriginal ?? command);
                     break;
                 case GatherCommand gather:
                     ProcessGatherCommand(gather);
                     break;
                 case StopCommand stop:
                     ProcessStopCommand(stop);
@@ -1764,21 +1767,21 @@ namespace OpenEmpires
                 case ToggleAutoProduceCommand toggleAuto:
                     ProcessToggleAutoProduceCommand(toggleAuto);
                     break;
                 case UpgradeTowerCommand upgradeTower:
                     ProcessUpgradeTowerCommand(upgradeTower);
                     break;
                 case CancelUpgradeCommand cancelUpgrade:
                     ProcessCancelUpgradeCommand(cancelUpgrade);
                     break;
                 case GarrisonCommand garrison:
-                    ProcessGarrisonCommand(garrison);
+                    ProcessGarrisonCommand(garrison, exactGarrison);
                     break;
                 case UngarrisonCommand ungarrison:
                     ProcessUngarrisonCommand(ungarrison);
                     break;
                 case PatrolCommand patrol:
                     ProcessPatrolCommand(patrol);
                     break;
                 case CheatResourceCommand cheatRes:
                     ProcessCheatResourceCommand(cheatRes);
                     break;
@@ -6387,20 +6390,21 @@ namespace OpenEmpires
                 {
                     unit.ClearPatrol();
                 }
             }
         }
 
         // ---- Garrison support ----
 
         private void ProcessArrivedGarrisonUnits()
         {
+            PruneExactGarrisonSubjects();
             // Collect units to process first to avoid collection modification during iteration
             garrisonUnitsBuffer.Clear();
             foreach (var unit in UnitRegistry.GetAllUnits())
             {
                 if (unit.TargetGarrisonBuildingId < 0) continue;
                 if (unit.State == UnitState.Idle || unit.State == UnitState.MovingToGarrison)
                 {
                     garrisonUnitsBuffer.Add(unit);
                 }
             }
@@ -6473,20 +6477,21 @@ namespace OpenEmpires
                 else
                 {
                     // Can't path to building, find alternative
                     RedirectToAlternativeGarrison(unit);
                 }
             }
         }
 
         private void RedirectToAlternativeGarrison(UnitData unit)
         {
+            if (RejectExactGarrisonFallback(unit)) return;
             var alternativeBuilding = FindNearestAvailableGarrisonBuilding(unit.PlayerId, unit.SimPosition, unit.TargetGarrisonBuildingId);
             
             if (alternativeBuilding == null)
             {
                 // No valid buildings available, cancel garrison
                 unit.TargetGarrisonBuildingId = -1;
                 unit.ClearPatrol();
                 unit.State = UnitState.Idle;
                 return;
             }
@@ -6546,36 +6551,38 @@ namespace OpenEmpires
                 if (b.IsDestroyed || b.IsUnderConstruction) continue;
                 if (b.PlayerId != playerId) continue;
                 // Check footprint + 1-tile border (since sheep path ends adjacent to building)
                 if (tile.x >= b.OriginTileX - 1 && tile.x < b.OriginTileX + b.TileFootprintWidth + 1
                     && tile.y >= b.OriginTileZ - 1 && tile.y < b.OriginTileZ + b.TileFootprintHeight + 1)
                     return b.Id;
             }
             return -1;
         }
 
-        private void ProcessGarrisonCommand(GarrisonCommand cmd)
+        private void ProcessGarrisonCommand(GarrisonCommand cmd, bool exactTarget = false)
         {
             // First, collect all valid units for garrison
             var validUnits = new List<UnitData>();
             foreach (int unitId in cmd.UnitIds)
             {
                 var unit = UnitRegistry.GetUnit(unitId);
-                if (unit == null || unit.State == UnitState.Dead) continue;
+                if (unit == null || unit.State == UnitState.Dead || unit.CurrentHealth <= 0) continue;
                 if (unit.PlayerId != cmd.PlayerId) continue;
                 validUnits.Add(unit);
             }
 
             if (validUnits.Count == 0) return;
 
             // Get all available garrison buildings for this player, sorted by distance from the first unit
             var availableBuildings = GetAvailableGarrisonBuildings(cmd.PlayerId, validUnits[0].SimPosition);
+            if (exactTarget)
+                availableBuildings.RemoveAll(building => building.Id != cmd.TargetBuildingId);
             if (availableBuildings.Count == 0) return;
 
             // Try to prioritize the specified building if it's available
             var specifiedBuilding = BuildingRegistry.GetBuilding(cmd.TargetBuildingId);
             if (specifiedBuilding != null && specifiedBuilding.PlayerId == cmd.PlayerId && 
                 !specifiedBuilding.IsDestroyed && !specifiedBuilding.IsUnderConstruction && 
                 specifiedBuilding.GarrisonCapacity > 0 && specifiedBuilding.CanGarrison)
             {
                 // Move specified building to front if it exists in available list
                 for (int i = 0; i < availableBuildings.Count; i++)
@@ -6681,21 +6688,22 @@ namespace OpenEmpires
                     unit.CombatTargetBuildingId = -1;
                     unit.DropOffBuildingId = -1;
                     unit.ClearPatrol();
                 }
             }
         }
 
         private bool GarrisonUnit(UnitData unit, BuildingData building)
         {
             // Check if building can actually garrison this unit
-            if (!building.CanGarrison)
+            if (unit.State == UnitState.Dead || unit.CurrentHealth <= 0
+                || !building.CanGarrison || !IsExactGarrisonTarget(unit, building.Id))
             {
                 return false;
             }
 
             unit.ClearPath();
             unit.ClearFormation();
             unit.ClearCommandQueue();
             unit.ClearSavedPath();
             unit.State = UnitState.Idle;
             unit.TargetResourceNodeId = -1;
@@ -6710,20 +6718,22 @@ namespace OpenEmpires
 
             // Move unit to building center (hidden from view)
             unit.SimPosition = building.SimPosition;
             unit.PreviousSimPosition = building.SimPosition;
 
             building.GarrisonedUnitIds.Add(unit.Id);
 
             // Move to garrison storage (won't be moved/attacked/etc)
             UnitRegistry.GarrisonUnit(unit.Id);
 
+            activeExactGarrison.Remove(unit.Id);
+
             OnUnitGarrisoned?.Invoke(unit.Id, building.Id);
             return true;
         }
 
         private void ProcessUngarrisonCommand(UngarrisonCommand cmd)
         {
             var building = BuildingRegistry.GetBuilding(cmd.BuildingId);
             if (building == null || building.IsDestroyed) return;
             if (building.PlayerId != cmd.PlayerId) return;
             if (building.GarrisonCount == 0) return;

## Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs
Before: A811C130753D42BA82D565170BA1A2CFC7194645F2F2DA94DCF9FDFCE785F974; current: 8AF860030765A4876F52F85AADA38FCC2D3C15C7F47AFDDF41344FC79FD622A0
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Tests\\EditMode\\CommanderPhase5ADynamicRuntimeTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase5ADynamicRuntimeTests.cs"
index 98329ac..4becfca 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Tests\\EditMode\\CommanderPhase5ADynamicRuntimeTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase5ADynamicRuntimeTests.cs"	
@@ -1,20 +1,714 @@
 using System;
 using System.Linq;
 using NUnit.Framework;
 using UnityEngine;
 
 namespace OpenEmpires.Tests
 {
     [Category("CommanderPhase5A")]
     public sealed class CommanderPhase5ADynamicRuntimeTests
     {
+        [Test]
+        public void AllCurrentWoodWorkers_GatherOnlyVisibleTreesWestOfOwnedTownCenter()
+        {
+            using var f = new Fixture(4);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var oldWood = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
+                f.Sim.MapData.TileToWorldFixed(x + 12, z), 1000);
+            var westernTree = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
+                f.Sim.MapData.TileToWorldFixed(x - 14, z + 4), 1000);
+            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
+                f.Sim.MapData.TileToWorldFixed(x + 14, z + 4), 1000);
+            for (int i = 0; i < 3; i++)
+            {
+                f.Workers[i].State = UnitState.Gathering;
+                f.Workers[i].TargetResourceNodeId = oldWood.Id;
+            }
+            var parsed = CommanderSemanticJson.Parse(AllWoodWestPlan);
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "gather with all current Wood workers at trees west of my TC", 1);
+            var goal = (AllocateWorkersGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            Assert.That(goal.FrozenWorkerIds, Is.EquivalentTo(f.Workers.Take(3).Select(w => w.Id)));
+            f.Manager.Tick(0);
+            var commands = f.Sim.CommandBuffer.FlushCommands().OfType<GatherCommand>().ToArray();
+            Assert.That(commands.SelectMany(c => c.UnitIds), Is.EquivalentTo(f.Workers.Take(3).Select(w => w.Id)));
+            Assert.That(commands.All(c => c.ResourceNodeId == westernTree.Id && c.SourceKind == ResourceSourceKind.Tree), Is.True);
+            f.Manager.Tick(15); // Issued commands have not yet entered native simulation.
+            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed),
+                "Their pre-existing east-side Wood assignments cannot satisfy the west-side request.");
+            f.Sim.Tick(commands.Cast<ICommand>().ToList());
+            Assert.That(f.Workers.Take(3).All(w => w.TargetResourceNodeId == westernTree.Id), Is.True,
+                "Normal native Gather processing must retain the selected western Tree.");
+        }
+
+        private const string AllWoodWestPlan = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
+            + "{\"id\":\"all\",\"mechanic\":\"select-workers\",\"parameters\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\",\"countMode\":\"AllMatching\"},\"inputs\":{},\"dependsOn\":[]},"
+            + "{\"id\":\"woodline\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":1,\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},\"inputs\":{},\"dependsOn\":[]},"
+            + "{\"id\":\"gather\",\"mechanic\":\"allocate-workers\",\"parameters\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},\"inputs\":{\"workers\":\"all\",\"location\":\"woodline\"},\"dependsOn\":[\"all\",\"woodline\"]}]}";
+
+        [Test]
+        public void AllCurrentWoodWorkers_ExplicitMoveToWestTownCenterWoodlineUsesNativeMove()
+        {
+            using var f = new Fixture(4);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var oldWood = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
+                f.Sim.MapData.TileToWorldFixed(x + 12, z), 1000);
+            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
+                f.Sim.MapData.TileToWorldFixed(x - 14, z + 4), 1000);
+            for (int i = 0; i < 3; i++)
+            {
+                f.Workers[i].State = UnitState.Gathering;
+                f.Workers[i].TargetResourceNodeId = oldWood.Id;
+            }
+            var parsed = CommanderSemanticJson.Parse(AllWoodWestMovePlan);
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "move all current Wood villagers to the woodline west of my Town Center", 1);
+            Assert.That(candidate.Preview, Does.Contain("Move"));
+            Assert.That(candidate.Preview, Does.Contain("all").IgnoreCase);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            Assert.That(goal.FrozenWorkerIds, Is.EquivalentTo(f.Workers.Take(3).Select(w => w.Id)));
+            f.Manager.Tick(0);
+            var issued = f.Sim.CommandBuffer.FlushCommands();
+            Assert.That(issued.OfType<GatherCommand>(), Is.Empty);
+            var move = issued.OfType<MoveCommand>().Single();
+            Assert.That(move.UnitIds,
+                Is.EquivalentTo(f.Workers.Take(3).Select(w => w.Id)));
+            Assert.That(move.FormationPositions.Length, Is.EqualTo(3));
+            Assert.That(move.FormationPositions.All(p => f.Sim.MapData.WorldToTile(p).x < x), Is.True);
+            f.Manager.Tick(15);
+            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed),
+                "A dispatched Move is not native arrival proof.");
+            for (int tick = 1; tick < 1500 && goal.Status != CommanderGoalStatus.Completed
+                && goal.Status != CommanderGoalStatus.Failed; tick++)
+            {
+                f.Sim.Tick(tick == 1 ? issued.ToList() : new System.Collections.Generic.List<ICommand>());
+                f.Manager.Tick((tick + 1) * 15);
+            }
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+            Assert.That(f.Workers.Take(3).All(w => f.Sim.MapData.WorldToTile(w.SimPosition).x < x), Is.True);
+            Assert.That(goal.NativeMovementProcessed.All(done => done), Is.True,
+                "The existing native movement receipt must process all frozen subjects.");
+        }
+
+        private const string AllWoodWestMovePlan = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
+            + "{\"id\":\"all\",\"mechanic\":\"select-workers\",\"parameters\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\",\"countMode\":\"AllMatching\"},\"inputs\":{},\"dependsOn\":[]},"
+            + "{\"id\":\"woodline\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":1,\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},\"inputs\":{},\"dependsOn\":[]},"
+            + "{\"id\":\"move\",\"mechanic\":\"move-workers\",\"parameters\":{},\"inputs\":{\"workers\":\"all\",\"location\":\"woodline\"},\"dependsOn\":[\"all\",\"woodline\"]}]}";
+
+        [Test]
+        public void CurrentWoodMove_AllOverBoundSnapshotRejectsBeforeAnyGoalOrCommand()
+        {
+            using var f = new Fixture(1);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var oldWood = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
+                f.Sim.MapData.TileToWorldFixed(x + 12, z), 1000);
+            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
+                f.Sim.MapData.TileToWorldFixed(x - 14, z + 4), 1000);
+            f.Workers[0].State = UnitState.Gathering;
+            f.Workers[0].TargetResourceNodeId = oldWood.Id;
+            for (int i = 0; i < 200; i++)
+            {
+                var worker = f.Sim.UnitRegistry.CreateUnit(0, f.Workers[0].SimPosition,
+                    Fixed32.One, Fixed32.One, Fixed32.One);
+                worker.IsVillager = true;
+                worker.CurrentHealth = worker.MaxHealth = 100;
+                worker.State = UnitState.Gathering;
+                worker.TargetResourceNodeId = oldWood.Id;
+            }
+            var parsed = CommanderSemanticJson.Parse(AllWoodWestMovePlan);
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "move all current Wood villagers west of my TC", 1);
+            Assert.Throws<InvalidOperationException>(() => f.Manager.SubmitSemanticGraph(
+                f.Manager.ApproveActionPlan(candidate, 1)));
+            Assert.That(f.Manager.Goals, Is.Empty);
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        [Test]
+        public void DiscoveryEnemyTownCenter_IssuesOnlyKnownFrontierScoutMove()
+        {
+            using var f = new Fixture(1, twoPlayers: true);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
+                Fixed32.One, Fixed32.One, Fixed32.One);
+            scout.UnitType = 4;
+            scout.CurrentHealth = scout.MaxHealth = 100;
+            scout.State = UnitState.Idle;
+            var hidden = f.Sim.CreateBuilding(1, BuildingType.TownCenter, x + 40, z, false, true);
+            Assert.That(f.Sim.FogOfWar.GetVisibility(0, hidden.OriginTileX, hidden.OriginTileZ),
+                Is.EqualTo(TileVisibility.Unexplored));
+            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center with my scout", 1);
+            Assert.That(candidate.Preview, Does.Contain("finite").IgnoreCase);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing), goal.StatusReason);
+            var move = f.Sim.CommandBuffer.FlushCommands().OfType<MoveCommand>().Single();
+            Assert.That(move.UnitIds, Is.EqualTo(new[] { scout.Id }));
+            var waypoint = f.Sim.MapData.WorldToTile(move.TargetPosition);
+            Assert.That(f.Sim.FogOfWar.GetVisibility(0, waypoint.x, waypoint.y),
+                Is.Not.EqualTo(TileVisibility.Unexplored));
+            Assert.That(waypoint.x, Is.LessThan(hidden.OriginTileX));
+        }
+
+        private const string DiscoverEnemyTownCenter = "{\"outcome\":\"Request\",\"nodes\":["
+            + "{\"type\":\"DiscoverEnemy\",\"unitSelector\":\"Scout\",\"count\":1,"
+            + "\"location\":\"VisibleEnemy\",\"target\":{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}}]}";
+
+        [Test]
+        public void Discovery_VisibleEnemyCompletesOnlyOnLegitimateVisibleTarget()
+        {
+            using var f = new Fixture(1, twoPlayers: true);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
+                Fixed32.One, Fixed32.One, Fixed32.One);
+            scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100;
+            var enemy = f.Sim.CreateBuilding(1, BuildingType.TownCenter, x + 40, z, false, true);
+            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
+            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<MoveCommand>().Count(), Is.EqualTo(1));
+            f.Sim.FogOfWar.SetVisible(0, enemy.OriginTileX, enemy.OriginTileZ);
+            f.Manager.Tick(15);
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        [Test]
+        public void Discovery_NoFrontierFailsWithoutInventingEnemyOrCommand()
+        {
+            using var f = new Fixture(1, twoPlayers: true);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
+                Fixed32.One, Fixed32.One, Fixed32.One);
+            scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100;
+            for (int tx = 0; tx < f.Sim.MapData.Width; tx++)
+                for (int tz = 0; tz < f.Sim.MapData.Height; tz++)
+                    if (!f.Sim.MapData.IsOutsideCircle(tx, tz)) f.Sim.FogOfWar.SetVisible(0, tx, tz);
+            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing),
+                "One observation may scan only a bounded portion, not the whole frontier map.");
+            Assert.That(goal.DiscoveryScanRadius, Is.LessThan(64));
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+            for (int tick = 1; tick < 8 && !goal.IsTerminal; tick++)
+            {
+                f.Manager.Tick(tick * 15);
+                Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty,
+                    "An exhausted known-map scan cannot invent a target or issue a point-scout order.");
+            }
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Failed), goal.StatusReason);
+            Assert.That(goal.DiscoveryCandidatesChecked, Is.EqualTo(0));
+            Assert.That(goal.DiscoveryMovesIssued, Is.EqualTo(0));
+        }
+
+        [TestCase(false)]
+        [TestCase(true)]
+        public void Discovery_UnsupportedResultOrStaleRuntimeCannotSelectUnrelatedScout(bool staleRuntime)
+        {
+            using var f = new Fixture(1, twoPlayers: true);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
+                Fixed32.One, Fixed32.One, Fixed32.One);
+            scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100;
+            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            if (staleRuntime) goal.RuntimeOwner = null;
+            else goal.RequiresResultBinding = true; // Internal stale state, not a provider-supported composition.
+            f.Manager.Tick(0);
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
+            Assert.That(goal.DiscoveryScoutId, Is.EqualTo(-1));
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        [TestCase("Lost")]
+        [TestCase("Human")]
+        [TestCase("Cancel")]
+        public void Discovery_ExactScoutLossTakeoverOrCancelStopsFurtherOrders(string outcome)
+        {
+            using var f = new Fixture(1, twoPlayers: true);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
+                Fixed32.One, Fixed32.One, Fixed32.One);
+            scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100;
+            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<MoveCommand>().Count(), Is.EqualTo(1));
+            if (outcome == "Lost") scout.CurrentHealth = 0;
+            else if (outcome == "Human") f.Sim.CommandBuffer.EnqueueCommand(
+                new MoveCommand(0, new[] { scout.Id }, scout.SimPosition), CommandEnqueueSource.Human);
+            else Assert.That(f.Manager.CancelGoal(goal.GoalId), Is.True);
+            f.Sim.CommandBuffer.FlushCommands();
+            f.Manager.Tick(15);
+            Assert.That(goal.Status, Is.EqualTo(outcome == "Cancel"
+                ? CommanderGoalStatus.Cancelled : CommanderGoalStatus.Failed), goal.StatusReason);
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        [Test]
+        public void ComposedWoodline_RejectsWrongSourceAndRelativeDirectionBeforeAdmission()
+        {
+            using var f = new Fixture(1);
+            Assert.That(CommanderSemanticJson.Parse(AllWoodWestPlan.Replace(
+                "\"sourceKind\":\"Tree\"},\"inputs\":{\"workers\"",
+                "\"sourceKind\":\"GoldMine\"},\"inputs\":{\"workers\"")).IsValid, Is.False);
+            Assert.That(CommanderSemanticJson.Parse(AllWoodWestPlan.Replace(
+                "\"relation\":\"MapWest\"", "\"relation\":\"Near\"")).IsValid, Is.False);
+            Assert.That(f.Manager.Goals, Is.Empty);
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        [TestCase(false)]
+        [TestCase(true)]
+        public void ComposedWoodline_RejectsWrongOwnerOrFoggedSource(bool foreignAnchor)
+        {
+            using var f = new Fixture(1);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var wood = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
+                f.Sim.MapData.TileToWorldFixed(foreignAnchor ? x - 15 : x - 30, z + 3), 1000);
+            f.Workers[0].State = UnitState.Gathering;
+            f.Workers[0].TargetResourceNodeId = wood.Id;
+            if (foreignAnchor)
+                f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter).PlayerId = 1;
+            var parsed = CommanderSemanticJson.Parse(AllWoodWestPlan);
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "gather at my western woodline", 1);
+            Assert.Throws<ArgumentException>(() => f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1)));
+            Assert.That(f.Manager.Goals, Is.Empty);
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        [Test]
+        public void TwoHousesNearOwnedTownCenter_CompleteAtDistinctNativeSites()
+        {
+            using var f = new Fixture(3);
+            string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
+                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
+                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
+            var parsed = CommanderSemanticJson.Parse(json);
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "build two Houses near my Town Center", 1);
+            var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            int initialWood = f.Sim.ResourceManager.GetPlayerResources(0).Wood;
+            for (int tick = 0; tick < 6000 && goal.Status != CommanderGoalStatus.Completed; tick++)
+            {
+                if (tick % 15 == 0) f.Manager.Tick(tick);
+                f.Sim.Tick(); // Accelerated native fixture ticks; no construction-time shortcut.
+            }
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+            Assert.That(goal.ResultBuildingIds.Count, Is.EqualTo(2));
+            var houses = goal.ResultBuildingIds.Select(id => f.Sim.BuildingRegistry.GetBuilding(id)).ToArray();
+            Assert.That(houses.All(h => h != null && h.PlayerId == 0 && h.Type == BuildingType.House
+                && !h.IsUnderConstruction && !h.IsDestroyed), Is.True);
+            Assert.That(houses.Select(h => new Vector2Int(h.OriginTileX, h.OriginTileZ)).Distinct().Count(), Is.EqualTo(2));
+            Assert.That(f.Sim.ResourceManager.GetPlayerResources(0).Wood, Is.LessThan(initialWood));
+        }
+
+        [Test]
+        public void TwentyAnchoredHouses_InsufficientDistinctSitesRejectsBeforeFirstPlacement()
+        {
+            using var f = new Fixture(3);
+            string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
+                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
+                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":20},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
+            var parsed = CommanderSemanticJson.Parse(json);
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "build twenty Houses near my TC", 1);
+            var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
+            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<PlaceBuildingCommand>(), Is.Empty,
+                "A statically impossible full site count must not execute a legal prefix.");
+        }
+
+        [Test]
+        public void ExactTargetGarrison_FullRequestedShelterMustNotDispatchToAnotherShelter()
+        {
+            using var f = new Fixture(2);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
+            requested.GarrisonCapacity = 1;
+            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
+            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
+            Assert.That(requested.GarrisonedUnitIds, Does.Contain(f.Workers[0].Id));
+            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.Not.EqualTo(other.Id),
+                "An exact-target request must not dispatch to an unrelated shelter when the requested one fills.");
+            Assert.That(other.GarrisonedUnitIds.Contains(f.Workers[1].Id), Is.False);
+        }
+
+        [Test]
+        public void ExactTargetGarrison_RelayOriginRemainsExactAndAmbiguousReplayFailsClosed()
+        {
+            using var f = new Fixture(2);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
+            requested.GarrisonCapacity = 1;
+            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
+            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
+            ICommand original = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            Assert.That(f.Sim.RegisterExactGarrisonCommand(original), Is.True);
+            var ledger = new CommanderCommandOriginLedger();
+            ICommand stamped = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            ledger.Record(0, 0, new[] { original }, new[] { stamped }, f.Sim);
+            ICommand replay = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            var commands = new System.Collections.Generic.List<ICommand> { replay, new NoopCommand(0) };
+            var origins = ledger.Consume(0, 0, commands, f.Sim);
+            Assert.That(origins.Count, Is.EqualTo(1));
+            Assert.That(origins[replay], Is.SameAs(original));
+            f.Sim.Tick(commands, origins);
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.Not.EqualTo(other.Id));
+
+            ICommand guarded = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            ICommand duplicate = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            Assert.That(f.Sim.RegisterExactGarrisonCommand(guarded), Is.True);
+            var ambiguous = new CommanderCommandOriginLedger();
+            ambiguous.Record(1, 0, new[] { guarded, duplicate }, new[] { guarded, duplicate }, f.Sim);
+            var replays = new System.Collections.Generic.List<ICommand> {
+                new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id),
+                new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id), new NoopCommand(0) };
+            var unknown = ambiguous.Consume(1, 0, replays, f.Sim);
+            Assert.That(unknown, Is.Empty);
+            f.Sim.Tick(replays, unknown);
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.Not.EqualTo(other.Id));
+            Assert.That(other.GarrisonedUnitIds.Contains(f.Workers[1].Id), Is.False);
+        }
+
+        [TestCase(false)]
+        [TestCase(true)]
+        public void ExactTargetGarrison_LateFullOrDestroyedTargetCancelsWithoutFallback(bool destroyed)
+        {
+            using var f = new Fixture(2);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
+            requested.GarrisonCapacity = 1;
+            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(requested.Id));
+            if (destroyed) requested.CurrentHealth = 0;
+            else
+            {
+                f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
+                requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
+                // Model the path reaching the target; native arrival handles the full shelter.
+                f.Workers[1].State = UnitState.Idle;
+                f.Workers[1].SimPosition = f.Sim.MapData.TileToWorldFixed(x - 1, z);
+            }
+            f.Sim.Tick();
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(-1));
+            Assert.That(other.GarrisonedUnitIds.Contains(f.Workers[1].Id), Is.False);
+        }
+
+        [Test]
+        public void OrdinaryGarrisonAndHumanReplacementKeepNativeFallback()
+        {
+            using var f = new Fixture(2);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
+            requested.GarrisonCapacity = 1;
+            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(requested.Id));
+            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
+            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
+            // A new human Garrison is a replacement order with ordinary fallback semantics.
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> {
+                new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id) });
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(other.Id));
+        }
+
+        [Test]
+        public void ForeignInvalidStopCannotDisarmAnOwnedExactGarrisonOrder()
+        {
+            using var f = new Fixture(2);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
+            requested.GarrisonCapacity = 1;
+            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(requested.Id));
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> {
+                new StopCommand(1, new[] { f.Workers[1].Id }) });
+            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
+            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
+            f.Workers[1].State = UnitState.Idle;
+            f.Workers[1].SimPosition = f.Sim.MapData.TileToWorldFixed(x - 1, z);
+            f.Sim.Tick();
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(-1));
+            Assert.That(other.GarrisonedUnitIds.Contains(f.Workers[1].Id), Is.False);
+        }
+
+        [Test]
+        public void ChangedOwnerReleasesGuardWithoutMutatingForeignActor()
+        {
+            using var f = new Fixture(1);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            var newOwnersShelter = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
+            newOwnersShelter.PlayerId = 1;
+            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[0].Id }, requested.Id);
+            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
+            Assert.That(f.Workers[0].TargetGarrisonBuildingId, Is.EqualTo(requested.Id));
+            f.Workers[0].PlayerId = 1;
+            f.Sim.Tick();
+            Assert.That(f.Workers[0].TargetGarrisonBuildingId, Is.EqualTo(newOwnersShelter.Id),
+                "The old owner's marker must not cancel the changed-owner actor's normal redirect.");
+        }
+
+        [Test]
+        public void ExactGarrison_HealthZeroBeforeNativeProcessingCannotEnter()
+        {
+            using var f = new Fixture(1);
+            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[0].Id }, requested.Id);
+            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
+            f.Workers[0].CurrentHealth = 0;
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
+            Assert.That(f.Workers[0].TargetGarrisonBuildingId, Is.EqualTo(-1));
+            Assert.That(requested.GarrisonedUnitIds.Contains(f.Workers[0].Id), Is.False);
+        }
+
+        [Test]
+        public void SemanticGarrison_UsesOwnedShelterAndReportsActualHousedCount()
+        {
+            using var f = new Fixture(2);
+            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
+                + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"count\":2,"
+                + "\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}");
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison two villagers in my Town Center", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            var shelter = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            shelter.GarrisonCapacity = 1;
+            f.Manager.Tick(0);
+            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
+            var command = (GarrisonCommand)original;
+            Assert.That(command.TargetBuildingId, Is.EqualTo(shelter.Id));
+            Assert.That(command.UnitIds.Length, Is.EqualTo(1));
+            for (int tick = 1; tick < 500 && goal.Status != CommanderGoalStatus.Completed; tick++)
+            {
+                f.Sim.Tick(tick == 1 ? new System.Collections.Generic.List<ICommand> { original }
+                    : new System.Collections.Generic.List<ICommand>());
+                f.Manager.Tick(tick * 15);
+            }
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+            Assert.That(shelter.GarrisonCount, Is.EqualTo(1));
+            Assert.That(goal.StatusReason, Does.Contain("1/2"));
+            Assert.That(goal.StatusReason, Does.Contain("unaccommodated"));
+        }
+
+        [Test]
+        public void SemanticGarrison_AllVillagersFreezesActualSetAndReportsRemainder()
+        {
+            using var f = new Fixture(3);
+            var shelter = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            shelter.GarrisonCapacity = 1;
+            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
+                + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"countMode\":\"AllMatching\","
+                + "\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}");
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison all villagers", 1);
+            Assert.That(candidate.Preview, Does.Contain("all").IgnoreCase);
+            Assert.That(candidate.Preview, Does.Not.Contain("1 owned"));
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
+            Assert.That(((GarrisonCommand)original).UnitIds.Length, Is.EqualTo(1));
+            for (int tick = 1; tick < 500 && goal.Status != CommanderGoalStatus.Completed; tick++)
+            {
+                f.Sim.Tick(tick == 1 ? new System.Collections.Generic.List<ICommand> { original }
+                    : new System.Collections.Generic.List<ICommand>());
+                f.Manager.Tick(tick * 15);
+            }
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+            Assert.That(goal.StatusReason, Does.Contain("1/3"));
+            Assert.That(goal.StatusReason, Does.Contain("unaccommodated 2"));
+        }
+
+        [Test]
+        public void SemanticGarrison_StalledNativeApproachTerminatesWithoutShelterFallback()
+        {
+            using var f = new Fixture(1);
+            f.Workers[0].MoveSpeed = Fixed32.Zero;
+            var parsed = CommanderSemanticJson.Parse(GarrisonTwoVillagers.Replace("\"count\":2", "\"count\":1"));
+            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison one villager", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
+            for (int tick = 1; tick <= 165 && goal.Status == CommanderGoalStatus.Executing; tick++)
+            {
+                f.Sim.Tick(tick == 1 ? new System.Collections.Generic.List<ICommand> { original }
+                    : new System.Collections.Generic.List<ICommand>());
+                f.Manager.Tick(tick * 15);
+            }
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
+            Assert.That(goal.StatusReason, Does.Contain("progress").IgnoreCase);
+        }
+
+        [Test]
+        public void SemanticGarrison_AllMatchingOverBoundRefusesWholeSnapshotWithoutPrefix()
+        {
+            using var f = new Fixture(1);
+            for (int i = 0; i < 200; i++)
+            {
+                var worker = f.Sim.UnitRegistry.CreateUnit(0, f.Workers[0].SimPosition,
+                    Fixed32.One, Fixed32.One, Fixed32.One);
+                worker.IsVillager = true;
+                worker.CurrentHealth = worker.MaxHealth = 100;
+                worker.State = UnitState.Idle;
+            }
+            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
+                + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"countMode\":\"AllMatching\","
+                + "\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}");
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(
+                f.Manager.PrepareActionPlan(parsed, "garrison all villagers", 1), 1))[0];
+            f.Manager.Tick(0);
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
+            Assert.That(goal.StatusReason, Does.Contain("200-subject safety bound"));
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        [Test]
+        public void SemanticUngarrison_ExplicitBuildingWideCommandRestoresAllHousedUnits()
+        {
+            using var f = new Fixture(2);
+            var shelter = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            foreach (var worker in f.Workers)
+            {
+                f.Sim.UnitRegistry.GarrisonUnit(worker.Id);
+                shelter.GarrisonedUnitIds.Add(worker.Id);
+            }
+            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
+                + "{\"type\":\"UngarrisonBuilding\",\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}");
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "ungarrison everyone from my Town Center", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            var command = f.Sim.CommandBuffer.FlushCommands().OfType<UngarrisonCommand>().Single();
+            Assert.That(command.BuildingId, Is.EqualTo(shelter.Id));
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { command });
+            f.Manager.Tick(15);
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+            Assert.That(shelter.GarrisonCount, Is.Zero);
+            Assert.That(f.Workers.All(w => f.Sim.UnitRegistry.GetUnit(w.Id) != null), Is.True);
+            Assert.That(goal.StatusReason, Does.Contain("Building-wide"));
+        }
+
+        [Test]
+        public void SemanticGarrison_ProtectedWorkerIsNotReclaimedOrSilentlyReplaced()
+        {
+            using var f = new Fixture(2);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            f.Workers[1].SimPosition = f.Sim.MapData.TileToWorldFixed(x - 1, z);
+            f.Sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { f.Workers[0].Id }));
+            f.Sim.CommandBuffer.FlushCommands(); // Human enqueue creates a protection lease.
+            var parsed = CommanderSemanticJson.Parse(GarrisonTwoVillagers);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison two villagers in my TC", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
+            var command = (GarrisonCommand)original;
+            Assert.That(command.UnitIds, Is.EqualTo(new[] { f.Workers[1].Id }));
+            for (int tick = 1; tick < 500 && goal.Status != CommanderGoalStatus.Completed; tick++)
+            {
+                f.Sim.Tick(tick == 1 ? new System.Collections.Generic.List<ICommand> { original }
+                    : new System.Collections.Generic.List<ICommand>());
+                f.Manager.Tick(tick * 15);
+            }
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+            Assert.That(goal.StatusReason, Does.Contain("protected 1"));
+            Assert.That(f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter)
+                .GarrisonedUnitIds.Contains(f.Workers[0].Id), Is.False);
+        }
+
+        [Test]
+        public void SemanticGarrison_FullShelterBlocksWithoutAnyCommand()
+        {
+            using var f = new Fixture(2);
+            var shelter = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            shelter.GarrisonCapacity = 1;
+            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
+            shelter.GarrisonedUnitIds.Add(f.Workers[0].Id);
+            var parsed = CommanderSemanticJson.Parse(GarrisonTwoVillagers);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison two villagers in my TC", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+            Assert.That(goal.StatusReason, Does.Contain("unaccommodated"));
+        }
+
+        [TestCase(false)]
+        [TestCase(true)]
+        public void SemanticGarrison_CancelOrResetRetainsExactGuardUntilNativeOrderEnds(bool reset)
+        {
+            using var f = new Fixture(2);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
+            var parsed = CommanderSemanticJson.Parse(GarrisonTwoVillagers);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison two villagers in my TC", 1);
+            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            f.Manager.Tick(0);
+            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
+            var command = (GarrisonCommand)original;
+            Assert.That(command.TargetBuildingId, Is.EqualTo(requested.Id));
+            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { original });
+            if (reset) f.Manager.Dispose();
+            else
+            {
+                Assert.That(f.Manager.CancelActionPlan(candidate), Is.True);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
+            }
+            requested.GarrisonCapacity = 0;
+            f.Sim.Tick();
+            Assert.That(f.Workers.All(w => w.TargetGarrisonBuildingId != other.Id), Is.True);
+            Assert.That(other.GarrisonedUnitIds, Is.Empty);
+        }
+
+        [Test]
+        public void SelectiveUngarrisonDeclarationIsRejectedBeforeAdmission()
+        {
+            using var f = new Fixture(1);
+            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
+                + "{\"type\":\"UngarrisonBuilding\",\"location\":\"PlayerBase\","
+                + "\"structure\":\"TownCenter\",\"count\":1}]}");
+            Assert.That(parsed.IsValid, Is.False);
+            Assert.That(f.Manager.Goals, Is.Empty);
+            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        private const string GarrisonTwoVillagers = "{\"outcome\":\"Request\",\"nodes\":["
+            + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"count\":2,"
+            + "\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}";
+
         [Test]
         public void OptInRequestTrace_CorrelatesApprovalAdmissionAndDispatchWithoutOriginalText()
         {
             using var f = new Fixture(1);
             var logs = new System.Collections.Generic.List<string>();
             void Capture(string message, string stack, LogType type)
             { if (message.StartsWith("[Commander] request=", StringComparison.Ordinal)) logs.Add(message); }
             Application.logMessageReceived += Capture;
             try
             {
@@ -421,25 +1115,26 @@ namespace OpenEmpires.Tests
             public int BarracksCost;
             public override int BarracksWoodCost => BarracksCost;
         }
 
         private sealed class Fixture : IDisposable
         {
             internal readonly SimulationConfig Config;
             internal readonly GameSimulation Sim;
             internal readonly CommanderGoalManager Manager;
             internal readonly UnitData[] Workers;
-            internal Fixture(int count, bool mutableCosts = false)
+            internal Fixture(int count, bool mutableCosts = false, bool twoPlayers = false)
             {
                 Config = mutableCosts ? ScriptableObject.CreateInstance<MutableCostConfig>()
                     : ScriptableObject.CreateInstance<SimulationConfig>();
-                Sim = new GameSimulation(Config, 1, new[] { 0 }, Array.Empty<int>());
+                Sim = new GameSimulation(Config, twoPlayers ? 2 : 1,
+                    twoPlayers ? new[] { 0, 1 } : new[] { 0 }, Array.Empty<int>());
                 int x = Sim.MapData.Width / 2, z = Sim.MapData.Height / 2;
                 for (int tx = x - 25; tx <= x + 25; tx++)
                     for (int tz = z - 20; tz <= z + 20; tz++)
                     {
                         Sim.MapData.Tiles[tx, tz] = TileType.Grass;
                         Sim.MapData.ForestDensity[tx, tz] = 0;
                         Sim.MapData.FoundationCount[tx, tz] = 0;
                         Sim.FogOfWar.SetVisible(0, tx, tz);
                     }
                 Sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;

## Assets/Tests/EditMode/CommanderPhase5CProviderBudgetTests.cs
Before: A5C683329D11CB66569BC77C37FBDC9D8EA54F879C85882E6DB358E026FFF307; current: D9A30BE457C9191B581EC05B49216FC74191226EC08C7E9E4C38F3DB64B0AB11
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Tests\\EditMode\\CommanderPhase5CProviderBudgetTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase5CProviderBudgetTests.cs"
index b759289..964f9d7 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-baseline\\Assets\\Tests\\EditMode\\CommanderPhase5CProviderBudgetTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase5CProviderBudgetTests.cs"	
@@ -7,20 +7,52 @@ using System.Threading;
 using System.Threading.Tasks;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 using NUnit.Framework;
 
 namespace OpenEmpires.Tests
 {
     [Category("CommanderPhase5C")]
     public sealed class CommanderPhase5CProviderBudgetTests
     {
+        [TestCase("Garrison all villagers in my Town Center.", "GarrisonUnits")]
+        [TestCase("Ungarrison my Town Center.", "UngarrisonBuilding")]
+        [TestCase("Find the enemy Town Center with my Scout.", "DiscoverEnemy")]
+        public void NewNativeAdapters_ReceiveAccurateCompactContract(string utterance, string action)
+        {
+            var request = new CommanderSemanticProviderRequest(utterance,
+                CommanderEconomyClarificationTests.Context());
+            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
+                out var pack, out string reason), Is.True, reason);
+            string instruction = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
+            Assert.That(instruction, Does.Contain(action));
+            if (action == "GarrisonUnits") Assert.That(instruction, Does.Contain("AllMatching"));
+            if (action == "UngarrisonBuilding") Assert.That(instruction, Does.Contain("building-wide"));
+            if (action == "DiscoverEnemy") Assert.That(instruction, Does.Contain("finite"));
+            Assert.That(pack.EstimatedInputTokens, Is.LessThan(4000));
+        }
+
+        [Test]
+        public void ExplicitCurrentWoodMove_ReceivesTypedComposedDynamicContractWithoutCount50Downgrade()
+        {
+            var request = new CommanderSemanticProviderRequest(
+                "Move all of my Wood villagers to the woodline west of my Town Center.",
+                CommanderEconomyClarificationTests.Context());
+            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
+                out var pack, out string reason), Is.True, reason);
+            string instruction = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
+            Assert.That(instruction, Does.Contain("move-workers"));
+            Assert.That(instruction, Does.Contain("countMode:AllMatching"));
+            Assert.That(instruction, Does.Contain("sourceKind:Tree"));
+            Assert.That(instruction, Does.Not.Contain("ALL/current-resource workers/composed side+resource+TC location unsupported"));
+            Assert.That(pack.EstimatedInputTokens, Is.LessThan(4000));
+        }
         [TestCase("Gather 200 Gold with three villagers from Wood.")]
         [TestCase("Build a House, then send the next five villagers from my first Town Center to gather Wood from trees.")]
         public async Task OrdinaryRequests_KeepCompleteModelPayloadBelowConservativeInputBudget(string utterance)
         {
             var transport = new CaptureTransport();
             var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
             await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(utterance,
                 CommanderEconomyClarificationTests.Context()), CancellationToken.None);
             Assert.That(transport.Calls, Is.EqualTo(1));
             var body = JObject.Parse(transport.Body);

## Assets/Scripts/AI/Commander/CommanderPlanner.DynamicMove.cs
Before: NEW; current: F941731972B1A5C3C5792CAB2D6870ABB8ACC7D422E989F30D9B0015B53C3994
using System;
using System.Linq;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private CommanderPlan PlanDynamicWorkerMove(CommanderCapabilityGoal goal, int currentTick)
        {
            if (goal.Action.ActionType != CommanderCapabilityActionType.MoveUnits
                || goal.FrozenWorkerIds == null || goal.FrozenWorkerIds.Count < 1
                || goal.FrozenWorkerIds.Count > 200
                || !goal.DynamicMoveLocation.TryResolveResourceSidePoint(simulation, goal.PlayerId,
                    out FixedVector3 target, out int resourceId))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The complete frozen worker set or visible owned resource-side destination is unavailable.", 0, 0);
            goal.DynamicMoveResourceId = resourceId;
            var command = new MoveCommand(goal.PlayerId, goal.FrozenWorkerIds.ToArray(), target);
            if (!TryResolveNativeMovementSlots(goal, command, currentTick,
                    out MoveCommand formation, out string reason))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "No complete west-side native formation exists for the frozen worker set; no prefix moved. " + reason,
                    0, 0);
            return new CommanderPlan(CommanderGoalStatus.Executing,
                "Moving the full frozen worker set to the exact visible western woodline using native Move.",
                0, 0, formation);
        }
    }
}

Meta SHA256: 3E10D4274D50036EE37636F3C3101C1CE3CF5E3A75F2C5E161CD77B4DDC26BDE
fileFormatVersion: 2
guid: 6a3aa6a611e60674e85f3142b4bbf744

## Assets/Scripts/AI/Commander/CommanderPlanner.Garrison.cs
Before: NEW; current: 142056AAFE49F0346EB2231BCE3533CB099D6C0244B0E88FBE53DD3D2CACA6A4
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private const int MaxGarrisonAllMatching = 200;
        private const int GarrisonNoProgressTicks = 150;

        private CommanderPlan PlanShelterCapability(CommanderCapabilityGoal goal, int currentTick)
        {
            bool enter = goal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits;
            BuildingData shelter = goal.GarrisonShelter;
            if (shelter == null)
            {
                shelter = simulation.BuildingRegistry.GetAllBuildings()
                    .Where(b => b.PlayerId == goal.PlayerId && !b.IsDestroyed
                        && !b.IsUnderConstruction && b.Type == goal.Action.StructureType
                        && b.GarrisonCapacity > 0)
                    .OrderByDescending(b => b.IsMainTownCenter).ThenBy(b => b.Id).FirstOrDefault();
                if (shelter == null)
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        "No completed owned shelter of the requested type is available.", 0, 0);
                goal.GarrisonShelter = shelter;
            }
            if (!ReferenceEquals(simulation.BuildingRegistry.GetBuilding(shelter.Id), shelter)
                || shelter.IsDestroyed || shelter.IsUnderConstruction || shelter.PlayerId != goal.PlayerId)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The exact selected shelter was lost or changed owner; no substitute is authorized.", 0, 0);

            if (!enter) return PlanBuildingWideUngarrison(goal, shelter);
            if (goal.CommandIssued) return ObserveExactGarrison(goal, shelter, currentTick);

            if (goal.GarrisonCandidateIds == null)
            {
                var executor = new CommanderCapabilityExecutor(simulation);
                var eligible = simulation.UnitRegistry.GetAllUnits()
                    .Where(unit => unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
                        && unit.State != UnitState.Dead && !unit.IsSheep
                    && executor.MatchesSelector(unit, goal.Action.UnitSelector))
                    .OrderBy(unit => unit.Id);
                goal.GarrisonCandidateIds = eligible
                    .Take(goal.Action.UnitSelector.AllMatching ? MaxGarrisonAllMatching + 1
                        : goal.Action.UnitSelector.Count).Select(unit => unit.Id).ToArray();
            }
            if (goal.Action.UnitSelector.AllMatching
                && goal.GarrisonCandidateIds.Length > MaxGarrisonAllMatching)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "AllMatching villager snapshot exceeds the 200-subject safety bound; no prefix was ordered.", 0, 0);
            int requested = goal.Action.UnitSelector.AllMatching
                ? goal.GarrisonCandidateIds.Length : goal.Action.UnitSelector.Count;
            if (requested == 0)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "No owned eligible villagers matched the AllMatching snapshot.", 0, 0);

            int protectedCount = 0;
            int unaccommodated = requested - goal.GarrisonCandidateIds.Length;
            int free = Math.Max(0, shelter.GarrisonCapacity - shelter.GarrisonCount);
            var selected = new List<int>();
            foreach (int id in goal.GarrisonCandidateIds)
            {
                UnitData unit = simulation.UnitRegistry.GetUnit(id);
                if (unit == null || unit.PlayerId != goal.PlayerId || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead)
                { unaccommodated++; continue; }
                if (!workerAuthority.CanUseUnit(id, goal.GoalId, currentTick)
                    || unit.CommandQueue.Count > 0)
                { protectedCount++; continue; }
                if (selected.Count >= free || !CanReachBuilding(unit, shelter))
                { unaccommodated++; continue; }
                selected.Add(id);
            }
            goal.GarrisonProtectedCount = protectedCount;
            goal.GarrisonUnaccommodatedCount = unaccommodated;
            if (selected.Count == 0)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"Garrisoned 0/{requested}; protected {protectedCount}; unaccommodated {unaccommodated}. No exact owned, reachable capacity can be used.",
                    0, 0);
            goal.GarrisonIssuedIds = selected.ToArray();
            return new CommanderPlan(CommanderGoalStatus.Executing,
                $"Sending {selected.Count}/{requested} owned eligible unit(s) to exact shelter; protected {protectedCount}; unaccommodated {unaccommodated}.",
                0, 0, new GarrisonCommand(goal.PlayerId, goal.GarrisonIssuedIds, shelter.Id));
        }

        private CommanderPlan ObserveExactGarrison(CommanderCapabilityGoal goal, BuildingData shelter,
            int currentTick)
        {
            int requested = goal.Action.UnitSelector.AllMatching
                ? goal.GarrisonCandidateIds.Length : goal.Action.UnitSelector.Count;
            int housed = 0;
            bool moving = false;
            long remainingDistance = 0;
            foreach (int id in goal.GarrisonIssuedIds ?? Array.Empty<int>())
            {
                UnitData occupant = simulation.UnitRegistry.GetGarrisonedUnit(id);
                if (occupant != null && occupant.PlayerId == goal.PlayerId && occupant.CurrentHealth > 0
                    && shelter.GarrisonedUnitIds.Contains(id))
                { housed++; continue; }
                UnitData unit = simulation.UnitRegistry.GetUnit(id);
                if (unit != null && unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
                    && unit.State != UnitState.Dead
                    && workerAuthority.IsHumanProtected(id, currentTick))
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        $"Garrisoned {housed}/{requested}; the player replaced an exact subject's order; no reclaim or shelter fallback is authorized.",
                        housed, 0);
                if (unit != null && unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
                    && unit.State != UnitState.Dead && unit.TargetGarrisonBuildingId == shelter.Id)
                {
                    moving = true;
                    remainingDistance += Math.Abs((long)unit.SimPosition.x.Raw - shelter.SimPosition.x.Raw)
                        + Math.Abs((long)unit.SimPosition.z.Raw - shelter.SimPosition.z.Raw);
                }
            }
            int remainder = requested - housed - goal.GarrisonProtectedCount;
            string result = $"Garrisoned {housed}/{requested} in the exact owned {shelter.Type}; protected {goal.GarrisonProtectedCount}; unaccommodated {Math.Max(0, remainder)}.";
            if (housed == (goal.GarrisonIssuedIds?.Length ?? 0)
                && simulation.CurrentTick > goal.CommandIssuedSimulationTick)
                return new CommanderPlan(CommanderGoalStatus.Completed, result, housed, 0);
            if (moving && remainingDistance < goal.GarrisonRemainingDistance)
            {
                goal.GarrisonRemainingDistance = remainingDistance;
                goal.GarrisonLastProgressSimulationTick = simulation.CurrentTick;
            }
            if (moving && goal.GarrisonLastProgressSimulationTick >= 0
                && simulation.CurrentTick - goal.GarrisonLastProgressSimulationTick >= GarrisonNoProgressTicks)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    result + " Exact native entry made no progress for 150 simulation ticks; no alternate shelter was authorized.",
                    housed, 0);
            if (moving || simulation.CurrentTick <= goal.CommandIssuedSimulationTick)
                return new CommanderPlan(CommanderGoalStatus.Executing, result + " Native entry is pending.", housed, 0);
            return new CommanderPlan(CommanderGoalStatus.Blocked,
                result + " An exact subject did not enter; no alternate shelter was authorized.", housed, 0);
        }

        private CommanderPlan PlanBuildingWideUngarrison(CommanderCapabilityGoal goal, BuildingData shelter)
        {
            if (!goal.CommandIssued)
            {
                if (shelter.GarrisonCount == 0)
                    return new CommanderPlan(CommanderGoalStatus.Completed,
                        "The exact owned shelter is already empty; building-wide ungarrison released 0 units.", 0, 0);
                goal.UngarrisonInitialIds = shelter.GarrisonedUnitIds.ToArray();
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    $"Building-wide ungarrison will release all {goal.UngarrisonInitialIds.Length} housed unit(s).",
                    0, 0, new UngarrisonCommand(goal.PlayerId, shelter.Id));
            }
            if (simulation.CurrentTick <= goal.CommandIssuedSimulationTick)
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    "Building-wide native ungarrison is pending.", 0, 0);
            int released = 0;
            foreach (int id in goal.UngarrisonInitialIds ?? Array.Empty<int>())
                if (simulation.UnitRegistry.GetUnit(id) != null
                    && !shelter.GarrisonedUnitIds.Contains(id)) released++;
            if (released == (goal.UngarrisonInitialIds?.Length ?? 0))
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    $"Building-wide ungarrison released {released}/{goal.UngarrisonInitialIds.Length} from the exact owned shelter.",
                    released, 0);
            return new CommanderPlan(CommanderGoalStatus.Blocked,
                $"Building-wide ungarrison released {released}/{goal.UngarrisonInitialIds?.Length ?? 0}; native egress is incomplete.",
                released, 0);
        }
    }
}

Meta SHA256: 658DD9E9A63D0078E4F1D3644DC62779035EDBDB3F3C0CE92E107BB0FA9ABA04
fileFormatVersion: 2
guid: d2168c454b067f3478cf9fa69685a851

## Assets/Scripts/AI/Commander/CommanderPlanner.Discovery.cs
Before: NEW; current: 0E88F09263A2935F29CB68FD2A42841FEF6C2392EC6EB330DA0D777522D08C6C
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private const int MaximumDiscoveryMoves = 8;
        private const int MaximumFrontierCandidatesPerMove = 64;
        private const int MaximumFrontierCellsPerObservation = 4096;
        private const int MaximumDiscoveryCandidates = 256;
        private const int MaximumDiscoveryRadius = 64;

        private CommanderPlan PlanDiscovery(CommanderCapabilityGoal goal, int currentTick)
        {
            if (goal.RuntimeOwner == null || goal.RuntimeOwner.IsDisposed
                || !ReferenceEquals(goal.RuntimeOwner.Simulation, simulation))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The discovery goal is detached from its original Commander runtime.", 0, 0);
            if (goal.ResultHumanOverride || goal.RequiresResultBinding || goal.ResultSourceGoal != null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "Result-bound discovery is unsupported; no unrelated Scout may substitute.", 0, 0);
            if (goal.DiscoveryScoutId < 0)
            {
                UnitData selected = simulation.UnitRegistry.GetAllUnits()
                    .Where(unit => unit != null && unit.PlayerId == goal.PlayerId && unit.UnitType == 4
                        && unit.CurrentHealth > 0 && unit.State != UnitState.Dead
                        && workerAuthority.CanUseUnit(unit.Id, goal.GoalId, currentTick))
                    .OrderBy(unit => unit.Id).FirstOrDefault();
                if (selected == null)
                    return new CommanderPlan(CommanderGoalStatus.Failed,
                        "No eligible owned Scout is available for this finite discovery request.", 0, 0);
                goal.DiscoveryScoutId = selected.Id;
            }
            UnitData scout = simulation.UnitRegistry.GetUnit(goal.DiscoveryScoutId);
            if (scout == null)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    simulation.UnitRegistry.GetGarrisonedUnit(goal.DiscoveryScoutId) != null
                        ? "The exact discovery Scout is garrisoned; no substitute is authorized."
                        : "The exact discovery Scout is missing; no substitute is authorized.", 0, 0);
            if (scout.PlayerId != goal.PlayerId)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The exact discovery Scout changed ownership; no substitute is authorized.", 0, 0);
            if (scout.CurrentHealth <= 0 || scout.State == UnitState.Dead)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The exact discovery Scout died; no substitute is authorized.", 0, 0);
            if (scout.UnitType != 4)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The exact discovery actor is no longer a Scout; no substitute is authorized.", 0, 0);
            if (workerAuthority.IsHumanProtected(scout.Id, currentTick))
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The player took control of the exact discovery Scout; Commander will not reclaim it.", 0, 0);
            if (!workerAuthority.CanUseUnit(scout.Id, goal.GoalId, currentTick))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The exact discovery Scout is temporarily reserved; no substitute is authorized.", 0, 0);

            if (DiscoveryTargetVisible(goal))
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    "The requested enemy target is now visible to the player.", 1, 0);

            if (goal.CommandIssued)
            {
                string reason = "Waiting for the exact Scout's native move to reach its known frontier waypoint.";
                if (goal.IssuedCommand is MoveCommand move
                    && ObserveNativeMovement(goal, move, new[] { scout.Id }, currentTick, out reason))
                {
                    goal.CommandIssued = false;
                    goal.IssuedCommand = null;
                    goal.DiscoveryScanCenterX = -1;
                    goal.DiscoveryScanCenterZ = -1;
                    goal.DiscoveryScanRadius = 0;
                    goal.DiscoveryScanOffset = 0;
                    return new CommanderPlan(CommanderGoalStatus.Executing,
                        "The Scout reached a known frontier waypoint; the requested enemy is not yet visible.", 1, 0);
                }
                if (reason.StartsWith("The exact native formation made no progress", StringComparison.Ordinal)
                    || reason.StartsWith("A native route stopped short", StringComparison.Ordinal)
                    || reason.StartsWith("An exact subject has a replacement", StringComparison.Ordinal)
                    || reason.StartsWith("An exact movement subject is temporarily unavailable", StringComparison.Ordinal))
                    return new CommanderPlan(CommanderGoalStatus.Failed, reason, 1, 0);
                return new CommanderPlan(CommanderGoalStatus.Executing, reason, 1, 0);
            }
            if (goal.DiscoveryMovesIssued >= MaximumDiscoveryMoves)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The finite discovery route was exhausted without a visible requested enemy target.", 1, 0);

            Vector2Int current = simulation.MapData.WorldToTile(scout.SimPosition);
            if (goal.DiscoveryScanCenterX < 0)
            {
                goal.DiscoveryScanCenterX = current.x;
                goal.DiscoveryScanCenterZ = current.y;
            }
            int width = simulation.MapData.Width, height = simulation.MapData.Height;
            int checkedCells = 0, checkedCandidates = 0;
            while (goal.DiscoveryScanRadius <= MaximumDiscoveryRadius
                && checkedCells < MaximumFrontierCellsPerObservation
                && checkedCandidates < MaximumFrontierCandidatesPerMove
                && goal.DiscoveryCandidatesChecked < MaximumDiscoveryCandidates)
            {
                int radius = goal.DiscoveryScanRadius;
                int ringLength = radius == 0 ? 1 : 8 * radius;
                if (goal.DiscoveryScanOffset >= ringLength)
                { goal.DiscoveryScanRadius++; goal.DiscoveryScanOffset = 0; continue; }
                Vector2Int tile = DiscoveryRingTile(goal.DiscoveryScanCenterX,
                    goal.DiscoveryScanCenterZ, radius, goal.DiscoveryScanOffset++);
                checkedCells++;
                if (!simulation.MapData.IsInBounds(tile.x, tile.y)
                    || !IsKnownTile(goal.PlayerId, tile.x, tile.y)
                    || !simulation.MapData.IsWalkable(tile.x, tile.y, goal.PlayerId, simulation.BuildingRegistry)
                    || goal.DiscoveryVisitedWaypoints.Contains(tile.x * height + tile.y)
                    || !BordersUnknownPlayableTile(goal.PlayerId, tile.x, tile.y)) continue;
                goal.DiscoveryVisitedWaypoints.Add(tile.x * height + tile.y);
                checkedCandidates++;
                goal.DiscoveryCandidatesChecked++;
                var requested = new MoveCommand(goal.PlayerId, new[] { scout.Id },
                    simulation.MapData.TileToWorldFixed(tile.x, tile.y));
                if (!TryResolveNativeMovementSlots(goal, requested, currentTick, out MoveCommand resolved, out _))
                    continue;
                goal.DiscoveryMovesIssued++;
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    $"Moving the exact Scout to known frontier waypoint {goal.DiscoveryMovesIssued}/{MaximumDiscoveryMoves}.",
                    1, 0, resolved);
            }
            if (goal.DiscoveryScanRadius <= MaximumDiscoveryRadius
                && goal.DiscoveryCandidatesChecked < MaximumDiscoveryCandidates)
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    "Scanning only known frontier tiles within the finite per-observation budget.", 1, 0);
            return new CommanderPlan(CommanderGoalStatus.Failed,
                "No reachable known frontier waypoint remains; the requested enemy was not visibly discovered.", 1, 0);
        }

        private static Vector2Int DiscoveryRingTile(int centerX, int centerZ, int radius, int offset)
        {
            if (radius == 0) return new Vector2Int(centerX, centerZ);
            int side = 2 * radius;
            if (offset < side) return new Vector2Int(centerX - radius + offset, centerZ - radius);
            offset -= side;
            if (offset < side) return new Vector2Int(centerX + radius, centerZ - radius + offset);
            offset -= side;
            if (offset < side) return new Vector2Int(centerX + radius - offset, centerZ + radius);
            offset -= side;
            return new Vector2Int(centerX - radius, centerZ + radius - offset);
        }

        private bool BordersUnknownPlayableTile(int playerId, int x, int z)
        {
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dz) > 3) continue;
                    int neighborX = x + dx, neighborZ = z + dz;
                    if (simulation.MapData.IsInBounds(neighborX, neighborZ)
                        && !simulation.MapData.IsOutsideCircle(neighborX, neighborZ)
                        && simulation.FogOfWar.GetVisibility(playerId, neighborX, neighborZ)
                            == TileVisibility.Unexplored) return true;
                }
            return false;
        }

        private bool DiscoveryTargetVisible(CommanderCapabilityGoal goal)
        {
            if (!goal.Action.TargetSelector.HasValue) return false;
            CommanderTargetSelector target = goal.Action.TargetSelector.Value;
            int player = goal.PlayerId;
            if (target.Kind == CommanderTargetSelectorKind.UnitType)
                return simulation.UnitRegistry.GetAllUnits().Any(unit =>
                {
                    if (unit == null || unit.PlayerId < 0 || unit.PlayerId == player
                        || simulation.AreAllies(player, unit.PlayerId) || unit.CurrentHealth <= 0
                        || unit.State == UnitState.Dead
                        || unit.UnitType != simulation.ResolveCivUnitType(unit.PlayerId, target.UnitType)) return false;
                    Vector2Int tile = simulation.MapData.WorldToTile(unit.SimPosition);
                    return simulation.FogOfWar.GetVisibility(player, tile.x, tile.y) == TileVisibility.Visible;
                });
            return simulation.BuildingRegistry.GetAllBuildings().Any(building =>
                building != null && building.PlayerId >= 0 && building.PlayerId != player
                && !simulation.AreAllies(player, building.PlayerId) && !building.IsDestroyed
                && simulation.GetEffectiveBuildingType(building) == target.StructureType
                && simulation.FogOfWar.GetVisibility(player, building.OriginTileX, building.OriginTileZ)
                    == TileVisibility.Visible);
        }
    }
}

Meta SHA256: 331069D769C17A0BF4C944665AD80B8DAE0A8E5A0D4349738E4B2B55935EAA58
fileFormatVersion: 2
guid: 96ae2ef5b6b154c46bcb986ebb2461c0

## Assets/Scripts/Core/GameSimulation.ExactGarrison.cs
Before: NEW; current: 9034145F4DB30E853FBAB68DAF333FD52AF0EDDC803E309E83A0F299CF99E5DF
using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public partial class GameSimulation
    {
        // Local-only correlation: neither the marker nor the boxed command identity
        // is serialized. Unmarked/native ordinary Garrison keeps its usual fallback.
        private sealed class ExactGarrisonOrder
        {
            internal ICommand Original;
            internal int PlayerId;
            internal int BuildingId;
            internal int[] UnitIds;
            internal int ExpiresTick;
        }

        private const int MaxExactGarrisonSubjects = 200;
        private readonly List<ExactGarrisonOrder> pendingExactGarrison = new List<ExactGarrisonOrder>();
        private readonly Dictionary<int, ExactGarrisonOrder> activeExactGarrison =
            new Dictionary<int, ExactGarrisonOrder>();

        internal bool RegisterExactGarrisonCommand(ICommand original)
        {
            if (!(original is GarrisonCommand command) || command.UnitIds == null
                || command.UnitIds.Length == 0 || command.UnitIds.Length > MaxExactGarrisonSubjects
                || command.TargetBuildingId < 0)
                return false;
            PruneExpiredPendingExactGarrison();
            int pendingSubjects = 0;
            foreach (var pending in pendingExactGarrison) pendingSubjects += pending.UnitIds.Length;
            if (pendingSubjects + command.UnitIds.Length > MaxExactGarrisonSubjects) return false;
            var distinct = new HashSet<int>();
            foreach (int id in command.UnitIds)
            {
                var unit = UnitRegistry.GetUnit(id);
                if (!distinct.Add(id) || unit == null || unit.PlayerId != command.PlayerId
                    || unit.State == UnitState.Dead || unit.CurrentHealth <= 0) return false;
            }
            pendingExactGarrison.Add(new ExactGarrisonOrder {
                Original = original, PlayerId = command.PlayerId,
                BuildingId = command.TargetBuildingId,
                UnitIds = (int[])command.UnitIds.Clone(), ExpiresTick = currentTick + 256
            });
            return true;
        }

        private void PruneExpiredPendingExactGarrison()
        {
            pendingExactGarrison.RemoveAll(order => currentTick > order.ExpiresTick);
        }

        // A reconstructed relay order with no unique verified original cannot
        // inherit the opt-in guard. Suppress every identical candidate in this
        // tick: permitting one would allow an unauthorized shelter fallback.
        private bool RejectUnattributedExactGarrison(ICommand command, ICommand verifiedOriginal)
        {
            if (verifiedOriginal != null || !(command is GarrisonCommand garrison)) return false;
            PruneExpiredPendingExactGarrison();
            foreach (var pending in pendingExactGarrison)
            {
                if (ReferenceEquals(pending.Original, command)
                    || garrison.PlayerId != pending.PlayerId
                    || garrison.TargetBuildingId != pending.BuildingId
                    || garrison.UnitIds == null || garrison.UnitIds.Length != pending.UnitIds.Length)
                    continue;
                bool identical = true;
                for (int i = 0; i < pending.UnitIds.Length; i++)
                    if (garrison.UnitIds[i] != pending.UnitIds[i]) { identical = false; break; }
                if (!identical) continue;
                pending.ExpiresTick = currentTick;
                return true;
            }
            return false;
        }

        // This runs at the native command boundary after local suppression. A later
        // human or Commander unit order replaces the old marker before it executes.
        private bool ObserveExactGarrisonCommand(ICommand command, ICommand original,
            out bool exactGarrison)
        {
            exactGarrison = false;
            PruneExpiredPendingExactGarrison();
            int[] subjects = CommandBuffer.GetSubjectUnitIds(command);
            if (subjects == null) return true;
            ExactGarrisonOrder exact = null;
            for (int i = 0; i < pendingExactGarrison.Count; i++)
                if (ReferenceEquals(pendingExactGarrison[i].Original, original))
                {
                    exact = pendingExactGarrison[i];
                    pendingExactGarrison.RemoveAt(i);
                    break;
                }
            if (exact != null)
            {
                if (!(command is GarrisonCommand garrison)
                    || garrison.PlayerId != exact.PlayerId
                    || garrison.TargetBuildingId != exact.BuildingId
                    || garrison.UnitIds == null || garrison.UnitIds.Length != exact.UnitIds.Length)
                    return false;
                for (int i = 0; i < exact.UnitIds.Length; i++)
                {
                    if (garrison.UnitIds[i] != exact.UnitIds[i]) return false;
                    var unit = UnitRegistry.GetUnit(exact.UnitIds[i]);
                    if (unit == null || unit.PlayerId != exact.PlayerId
                        || unit.State == UnitState.Dead || unit.CurrentHealth <= 0)
                        return false;
                }
            }
            foreach (int id in subjects)
            {
                var unit = UnitRegistry.GetUnit(id);
                if (unit != null && unit.PlayerId == command.PlayerId)
                    activeExactGarrison.Remove(id);
            }
            if (exact == null) return true;
            foreach (int id in exact.UnitIds) activeExactGarrison[id] = exact;
            exactGarrison = true;
            return true;
        }

        private bool IsExactGarrisonTarget(UnitData unit, int buildingId)
        {
            return !activeExactGarrison.TryGetValue(unit.Id, out var exact)
                || (exact.PlayerId == unit.PlayerId && exact.BuildingId == buildingId);
        }

        private void CancelExactGarrisonOrder(UnitData unit)
        {
            activeExactGarrison.Remove(unit.Id);
            unit.TargetGarrisonBuildingId = -1;
            unit.ClearPath();
            unit.ClearPatrol();
            unit.State = UnitState.Idle;
            unit.PlayerCommanded = false;
        }

        private bool RejectExactGarrisonFallback(UnitData unit)
        {
            if (!activeExactGarrison.ContainsKey(unit.Id)) return false;
            CancelExactGarrisonOrder(unit);
            return true;
        }

        private void PruneExactGarrisonSubjects()
        {
            if (activeExactGarrison.Count == 0) return;
            var ids = new List<int>(activeExactGarrison.Keys);
            foreach (int id in ids)
            {
                var unit = UnitRegistry.GetUnit(id);
                if (unit == null || unit.PlayerId != activeExactGarrison[id].PlayerId
                    || unit.State == UnitState.Dead)
                {
                    activeExactGarrison.Remove(id);
                    continue;
                }
                if (unit.CurrentHealth <= 0)
                {
                    CancelExactGarrisonOrder(unit);
                    continue;
                }
                var exact = activeExactGarrison[id];
                if (unit.TargetGarrisonBuildingId < 0)
                {
                    activeExactGarrison.Remove(id);
                    continue;
                }
                if (unit.TargetGarrisonBuildingId != exact.BuildingId)
                    CancelExactGarrisonOrder(unit);
            }
        }
    }
}

Meta SHA256: 78E39227F0E4844F74383492ECE3BB7C9AD27BC56E3805D4850659733E8493E3
fileFormatVersion: 2
guid: 37b29fc10dfcbdc4aae6c4649f317dd6

