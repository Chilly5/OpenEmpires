# Phase5C full source review package

Baseline: `4c9de96adbf6342fbe9e1490fa9b898a0f99caf6`. HEAD at packaging: `4c9de96adbf6342fbe9e1490fa9b898a0f99caf6`. Source review snapshot only; not test/build/runtime evidence.

Full source portion: 85 files (52 tracked source/config deltas and 33 new source/meta files). Newly added file headings below retain workspace-relative `Assets/...` labels; the identity manifest canonicalizes every path to repository-root-relative form under `D:/unity_projects/OpenEmpires`. The additional changed font asset is included hash-only, not as a content diff; see identity manifest (86 total identity records).
Tracked deltas are represented below as the complete unified diff; new source/meta/config files are included in full.

## Tracked source/config deltas

```diff
diff --git a/Open Empires/Assets/Scripts/AI/Commander/CommanderContextBuilder.cs b/Open Empires/Assets/Scripts/AI/Commander/CommanderContextBuilder.cs
index 6ad8c5e..708d666 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/CommanderContextBuilder.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/CommanderContextBuilder.cs	
@@ -11,6 +11,10 @@ namespace OpenEmpires
             => Build(simulation, manager, string.Empty);
 
         public CommanderContext Build(GameSimulation simulation, CommanderGoalManager manager, string knowledgeQuery)
+            => Build(simulation, manager, knowledgeQuery, null);
+
+        public CommanderContext Build(GameSimulation simulation, CommanderGoalManager manager, string knowledgeQuery,
+            string boundedPending)
         {
             if (simulation == null) throw new ArgumentNullException(nameof(simulation));
             if (manager == null) throw new ArgumentNullException(nameof(manager));
@@ -134,7 +138,7 @@ namespace OpenEmpires
                     entry.Key, entry.Value));
             var catalog = GameKnowledgeCatalog.Build(simulation);
             string knowledgeContext = CommanderKnowledgeRetrieval.Retrieve(catalog, knowledgeQuery,
-                simulation.GetPlayerCivilization(player));
+                simulation.GetPlayerCivilization(player), boundedPending);
             var civilization = catalog.Civilizations.FirstOrDefault(c =>
                 c.Civilization == simulation.GetPlayerCivilization(player));
             // Detached projections over the same canonical data and implemented
diff --git a/Open Empires/Assets/Scripts/AI/Commander/CommanderGoal.cs b/Open Empires/Assets/Scripts/AI/Commander/CommanderGoal.cs
index 05a2280..c27a245 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/CommanderGoal.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/CommanderGoal.cs	
@@ -1,5 +1,6 @@
 using System;
 using System.Collections.Generic;
+using UnityEngine;
 
 namespace OpenEmpires
 {
@@ -158,9 +159,13 @@ namespace OpenEmpires
         internal void ReleaseRuntimeReferences()
         {
             RuntimeOwner=null;RequestAuthority=null;dependencies.Clear();PreparationAllocators.Clear();FrozenWorkerIds=null;
-            if(this is CommanderCapabilityGoal capability){capability.TargetBinding=null;capability.ResultSourceGoal=null;capability.IssuedCommand=null;}
+            if(this is CommanderCapabilityGoal capability)
+            {capability.TargetBinding=null;capability.ResultSourceGoal=null;capability.IssuedCommand=null;
+                capability.NativeMovementSlots=null;capability.NativeMovementEndpoints=null;
+                capability.NativeMovementProcessed=null;capability.NativeMovementResults=null;
+                capability.NativeMovementScatter=null;}
             if(this is EnsureUnitCountGoal units){units.RequiredProducerGoal=null;units.TrackedTrainingOrders.Clear();units.BaselineUnitIds.Clear();}
-            if(this is BuildStructureGoal build)build.PendingPlacementCommand=null;
+            if(this is BuildStructureGoal build){build.PendingPlacementCommand=null;build.DynamicPlannedSiteSet=null;}
             if(this is ReachAgeGoal age)age.PendingAgeUpCommand=null;
             if(this is AllocateWorkersGoal boundAllocation){boundAllocation.ResultSourceGoal=null;boundAllocation.RecoveryCandidateOffsets.Clear();boundAllocation.RecoverySearchExhausted=false;}
             if(this is WatchFutureUnitsGoal future)future.ReleaseObservations();
@@ -189,7 +194,11 @@ namespace OpenEmpires
         internal bool HasResultConsumer { get; set; }
         internal readonly HashSet<int> BaselineBuildingIds = new HashSet<int>();
         internal readonly List<int> AttributedBuildingIds = new List<int>();
+        // Captured only after the ordinary placement command creates this goal's foundation.
+        // These are observed game-side builders, not a provider-selected identity.
+        internal IReadOnlyList<int> LastAcceptedBuilderUnitIds { get; set; } = Array.Empty<int>();
         internal CommanderBoundLocation DynamicLocation { get; set; }
+        internal IReadOnlyList<Vector2Int> DynamicPlannedSiteSet { get; set; }
         internal IReadOnlyList<int> ResultBuildingIds { get; private set; } = Array.Empty<int>();
         internal int ResultCaptureTick { get; private set; } = -1;
         public bool HasSemanticPlacement => PlacementAnchorSelector.HasValue || DynamicLocation != null;
@@ -306,11 +315,37 @@ namespace OpenEmpires
         internal bool CommandIssued { get; set; }
         internal ICommand IssuedCommand { get; set; }
         internal int CommandIssuedSimulationTick { get; set; } = -1;
+        internal FixedVector3[] NativeMovementSlots { get; set; }
+        internal FixedVector3[] NativeMovementEndpoints { get; set; }
+        internal Fixed32[] NativeMovementScatter { get; set; }
+        internal bool[] NativeMovementProcessed { get; set; }
+        internal NativeMovePathResult[] NativeMovementResults { get; set; }
+        internal bool NativeMovementReplaced { get; set; }
+        internal long NativeRemainingDistance { get; set; } = long.MaxValue;
+        internal int NativeLastProgressSimulationTick { get; set; } = -1;
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs b/Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs
index 882b891..0245115 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs	
@@ -117,6 +117,8 @@ namespace OpenEmpires
             simulation.TrainingOriginLost += HandleTrainingOriginLost;
             simulation.ProducerUnitProduced += HandleFutureBirth;
             simulation.LocalActionCommandProcessed += HandleFutureCommandProcessed;
+            simulation.NativeMoveProcessed += HandleNativeMoveProcessed;
+            simulation.NativeUnitCommandProcessed += HandleNativeUnitCommandProcessed;
             planner = new CommanderPlanner(simulation, workerAuthority,
                 pathValidationCandidates);
         }
@@ -566,7 +568,9 @@ namespace OpenEmpires
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
@@ -576,6 +580,16 @@ namespace OpenEmpires
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
@@ -636,6 +650,7 @@ namespace OpenEmpires
                             capability.CommandIssued = true;
                             capability.IssuedCommand = plan.Command;
                             capability.CommandIssuedSimulationTick = simulation.CurrentTick;
+                            capability.NativeLastProgressSimulationTick = simulation.CurrentTick;
                         }
                         if (goal is WatchFutureUnitsGoal future)
                         {
@@ -807,6 +822,8 @@ namespace OpenEmpires
                 {
                     goal.PlacedBuildingId = created.Id;
                     if (!goal.AttributedBuildingIds.Contains(created.Id)) goal.AttributedBuildingIds.Add(created.Id);
+                    if (command is PlaceBuildingCommand placed && placed.VillagerUnitIds != null)
+                        goal.LastAcceptedBuilderUnitIds = Array.AsReadOnly(placed.VillagerUnitIds.ToArray());
                 }
                 return;
             }
@@ -856,6 +873,8 @@ namespace OpenEmpires
             ReleaseTrainingObservations();
             simulation.ProducerUnitProduced -= HandleFutureBirth;
             simulation.LocalActionCommandProcessed -= HandleFutureCommandProcessed;
+            simulation.NativeMoveProcessed -= HandleNativeMoveProcessed;
+            simulation.NativeUnitCommandProcessed -= HandleNativeUnitCommandProcessed;
             simulation.CommandBuffer.CommandEnqueued -= HandleCommandEnqueued;
             simulation.OnBuildingPlacedFromCommand -= HandleBuildingPlacedFromCommand;
             for (int i = 0; i < activeGoals.Count; i++)
diff --git a/Open Empires/Assets/Scripts/AI/Commander/CommanderIntentValidator.cs b/Open Empires/Assets/Scripts/AI/Commander/CommanderIntentValidator.cs
index 318d5c3..6e5a31d 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/CommanderIntentValidator.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/CommanderIntentValidator.cs	
@@ -216,6 +216,10 @@ namespace OpenEmpires
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
@@ -224,6 +228,28 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/CommanderPlanner.cs b/Open Empires/Assets/Scripts/AI/Commander/CommanderPlanner.cs
index 4217867..c06b8c7 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/CommanderPlanner.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/CommanderPlanner.cs	
@@ -73,6 +73,8 @@ namespace OpenEmpires
 
         private CommanderPlan PlanCapability(CommanderCapabilityGoal goal, int currentTick)
         {
+            if (goal.Action.ActionType == CommanderCapabilityActionType.DiscoverEnemy)
+                return PlanDiscovery(goal, currentTick);
             if (goal.ResultHumanOverride)
                 return new CommanderPlan(CommanderGoalStatus.Blocked,
                     "The player took control of a required producer result; this dependent action will not reclaim it.", 0, 0);
@@ -87,6 +89,21 @@ namespace OpenEmpires
                 || goal.ResultSourceGoal.Status != CommanderGoalStatus.Completed))
                 return new CommanderPlan(CommanderGoalStatus.Blocked,
                     "The referenced producer is unavailable in this Commander runtime; no fallback is allowed.", 0, 0);
+            if (goal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits
+                || goal.Action.ActionType == CommanderCapabilityActionType.UngarrisonBuilding)
+                return PlanShelterCapability(goal, currentTick);
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
@@ -123,6 +140,16 @@ namespace OpenEmpires
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
 
@@ -140,13 +167,24 @@ namespace OpenEmpires
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
@@ -155,11 +193,17 @@ namespace OpenEmpires
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
@@ -227,23 +271,163 @@ namespace OpenEmpires
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
+            var nativeCenters = new FixedVector3[original.UnitIds.Length];
+            var scatterLimits = new Fixed32[original.UnitIds.Length];
+            for (int i = 0; i < original.UnitIds.Length; i++)
+            {
+                UnitData unit = simulation.UnitRegistry.GetUnit(original.UnitIds[i]);
+                if (unit == null || unit.PlayerId != goal.PlayerId || unit.CurrentHealth <= 0
+                    || unit.State == UnitState.Dead || workerAuthority.IsHumanProtected(unit.Id, currentTick)) return false;
+                bool found = false;
+                for (int radius = 0; radius <= 10 && !found; radius++)
+                    for (int dx = -radius; dx <= radius && !found; dx++)
+                        for (int dz = -radius; dz <= radius && !found; dz++)
+                        {
+                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius) continue;
+                            int x = anchor.x + dx, z = anchor.y + dz;
+                            var destination = new Vector2Int(x, z);
+                            if (goal.DynamicMoveLocation != null
+                                && !goal.DynamicMoveLocation.AllowsMoveTile(simulation, goal.PlayerId,
+                                    goal.DynamicMoveResourceId, x, z)) continue;
+                            if (!simulation.MapData.IsWalkable(x, z)
+                                || !simulation.MapData.IsWalkable(x, z, goal.PlayerId, simulation.BuildingRegistry)
+                                || !IsKnownTile(goal.PlayerId, x, z)) continue;
+                            FixedVector3 proposed = simulation.MapData.TileToWorldFixed(x, z);
+                            FixedVector3 commandAnchor = i == 0 ? proposed : slots[0];
+                            if (!TryKnownNativeMoveEnvelope(goal.PlayerId, anchor, unit,
+                                commandAnchor, proposed, occupied, out FixedVector3 nativeCenter,
+                                out Fixed32 scatter, out HashSet<Vector2Int> covered,
+                                goal.DynamicMoveLocation, goal.DynamicMoveResourceId)) continue;
+                            occupied.UnionWith(covered);
+                            slots[i] = proposed;
+                            nativeCenters[i] = nativeCenter;
+                            scatterLimits[i] = scatter;
+                            found = true;
+                        }
+                if (!found) return false;
+            }
+            move.TargetPosition = slots[0];
+            move.FormationPositions = slots;
+            var unsnapped = (FixedVector3[])slots.Clone();
+            simulation.SnapNativeMoveFormationPositions(ref move);
+            for (int i = 0; i < slots.Length; i++)
+                if (simulation.MapData.WorldToTile(slots[i]) != simulation.MapData.WorldToTile(unsnapped[i]))
+                    return false;
+            goal.NativeMovementSlots = nativeCenters;
+            goal.NativeMovementScatter = scatterLimits;
+            goal.NativeMovementEndpoints = new FixedVector3[slots.Length];
+            goal.NativeMovementProcessed = new bool[slots.Length];
+            goal.NativeMovementResults = new NativeMovePathResult[slots.Length];
+            goal.NativeMovementReplaced = false;
+            reason = "Moving exact selected units to known reachable native formation positions.";
+            return true;
+        }
+
+        private bool TryKnownNativeMoveEnvelope(int playerId, Vector2Int semanticAnchor,
+            UnitData unit, FixedVector3 commandAnchor, FixedVector3 slot,
+            HashSet<Vector2Int> occupied, out FixedVector3 center,
+            out Fixed32 scatter, out HashSet<Vector2Int> covered,
+            CommanderBoundLocation dynamicMoveLocation = null, int dynamicMoveResourceId = -1)
+        {
+            center = simulation.NativeMoveFormationCenter(commandAnchor, slot, unit);
+            scatter = simulation.MaximumNativeMoveScatter(unit);
+            covered = new HashSet<Vector2Int>();
+            if (scatter > Fixed32.FromInt(10)) return false;
+            Vector2Int low = simulation.MapData.WorldToTile(new FixedVector3(center.x - scatter,
+                center.y, center.z - scatter));
+            Vector2Int high = simulation.MapData.WorldToTile(new FixedVector3(center.x + scatter,
+                center.y, center.z + scatter));
+            if (high.x - low.x > 20 || high.y - low.y > 20) return false;
+            Vector2Int start = simulation.MapData.WorldToTile(unit.SimPosition);
+            for (int x = low.x; x <= high.x; x++)
+                for (int z = low.y; z <= high.y; z++)
+                {
+                    var tile = new Vector2Int(x, z);
+                    if (Mathf.Max(Mathf.Abs(x - semanticAnchor.x), Mathf.Abs(z - semanticAnchor.y)) > 10
+                        || dynamicMoveLocation != null && !dynamicMoveLocation.AllowsMoveTile(
+                            simulation, playerId, dynamicMoveResourceId, x, z)
+                        || occupied.Contains(tile)
+                        || !simulation.MapData.IsWalkable(x, z, playerId, simulation.BuildingRegistry)
+                        || !IsKnownTile(playerId, x, z)
+                        || !GridPathfinder.TryFindCompletePath(simulation.MapData, start, tile,
+                            out List<Vector2Int> path, playerId, simulation.BuildingRegistry)
+                        || !IsKnownPath(playerId, start, path)) return false;
+                    covered.Add(tile);
+                }
+            return covered.Count > 0;
+        }
+
+        private bool ObserveNativeMovement(CommanderCapabilityGoal goal, MoveCommand move,
+            int[] subjects, int currentTick, out string reason)
+        {
+            reason = "Waiting for the exact selected units to reach their native formation endpoints.";
+            if (subjects == null || subjects.Length == 0 || goal.NativeMovementSlots == null
+                || goal.NativeMovementSlots.Length != subjects.Length
+                || goal.NativeMovementProcessed == null || goal.NativeMovementEndpoints == null
+                || goal.NativeMovementScatter == null || goal.NativeMovementResults == null) return false;
+            if (simulation.CurrentTick <= goal.CommandIssuedSimulationTick) return false;
+            if (goal.NativeMovementReplaced)
+            { reason = "An exact subject has a replacement native order; unrelated movement cannot satisfy this request."; return false; }
+            for (int i = 0; i < subjects.Length; i++)
+            {
+                UnitData current = simulation.UnitRegistry.GetUnit(subjects[i]);
+                if (current == null || current.State != UnitState.Moving && current.State != UnitState.Idle)
+                {
+                    reason = "An exact movement subject is temporarily unavailable; no replacement or takeover is inferred.";
+                    return false;
+                }
+            }
+            for (int i = 0; i < subjects.Length; i++)
+            {
+                if (!goal.NativeMovementProcessed[i])
+                {
+                    if (simulation.CurrentTick - goal.CommandIssuedSimulationTick >= 150)
+                        reason = "The exact native formation made no progress for 150 simulation ticks; native processing was not observed.";
+                    return false;
+                }
+                if (goal.NativeMovementResults[i] == NativeMovePathResult.Partial
+                    || goal.NativeMovementResults[i] == NativeMovePathResult.NoPath
+                    || Math.Abs((long)goal.NativeMovementEndpoints[i].x.Raw - goal.NativeMovementSlots[i].x.Raw) > goal.NativeMovementScatter[i].Raw
+                    || Math.Abs((long)goal.NativeMovementEndpoints[i].z.Raw - goal.NativeMovementSlots[i].z.Raw) > goal.NativeMovementScatter[i].Raw)
+                { reason = "A native route stopped short of its validated formation slot; no distant idle position counts as arrival."; return false; }
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
+                if (unit == null || unit.HasPath
+                    && !Near(unit.FinalDestination, goal.NativeMovementEndpoints[i], Fixed32.FromInt(1)))
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs b/Open Empires/Assets/Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs
index 75defdd..008294c 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs	
@@ -112,6 +112,28 @@ namespace OpenEmpires
     {
         internal static bool IsStatusQuestion(string input) => Regex.IsMatch(input??"",
             @"\b(status|progress|progressing|doing|waiting|finished|ready|blocked|stuck|request|army)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
+        internal static bool IsCausalTaskQuestion(string input,GameSimulation sim,CommanderGoalManager manager)
+        {
+            string query=(input??"").Trim();
+            if(!Regex.IsMatch(query,@"^(why|what happened|how come|what went wrong)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return false;
+            if(Regex.IsMatch(query,@"\b(task|order|goal|arriv(?:e|ed|ing)|stop(?:ped)?|staff(?:ed|ing)?|builders?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
+            if(Regex.IsMatch(query,@"^what happened\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
+                &&ReferencedUnitCount(query).HasValue)return true;
+            var catalog=GameKnowledgeCatalog.Build(sim);
+            return catalog.Units.Any(u=>Mention(query,u.DisplayName)||u.Aliases.Any(a=>Mention(query,a)))
+                ||catalog.Buildings.Any(b=>Mention(query,b.DisplayName)||b.Aliases.Any(a=>Mention(query,a)));
+        }
+        private static int? ReferencedUnitCount(string query)
+        {
+            var match=Regex.Match(query??"",@"\b(?:my|the|our)\s+(?<count>\d{1,3}|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty)\s+(?:selected\s+)?units?\b",
+                RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
+            if(!match.Success)return null;
+            string value=match.Groups["count"].Value.ToLowerInvariant();
+            if(int.TryParse(value,out int number))return number;
+            string[] words={"one","two","three","four","five","six","seven","eight","nine","ten","eleven","twelve","thirteen","fourteen","fifteen","sixteen","seventeen","eighteen","nineteen","twenty"};
+            for(int i=0;i<words.Length;i++)if(value==words[i])return i+1;
+            return null;
+        }
         internal static bool IsInformationQuestion(string input)
         {
             string text=(input??"").Trim();
@@ -127,12 +149,13 @@ namespace OpenEmpires
         internal static CommanderTacticalStatusSnapshot Observe(GameSimulation sim,CommanderGoalManager manager,string query)
         {
             var catalog=GameKnowledgeCatalog.Build(sim);
+            bool builderRole=Regex.IsMatch(query??"",@"\bbuilders?\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
             var all=manager.ActiveGoals.Concat(manager.ArchivedGoals.Reverse().Take(CommanderGoalManager.MaxArchivedGoals))
                 .Where(g=>g.PlayerId==manager.PlayerId&&ReferenceEquals(g.RuntimeOwner,manager)).ToList();
             bool HasMention(CommanderGoal goal)
             {
                 if(goal is EnsureUnitCountGoal unit)return UnitMention(unit.RequestedUnitType);
-                if(goal is BuildStructureGoal build)return BuildingMention(build.StructureType);
+                if(goal is BuildStructureGoal build)return builderRole||BuildingMention(build.StructureType);
                 if(goal is CommanderCapabilityGoal action)
                     return action.Action.UnitSelector.Kind==CommanderUnitSelectorKind.UnitType&&UnitMention(action.Action.UnitSelector.UnitType)
                         || action.Action.TargetSelector.HasValue&&(action.Action.TargetSelector.Value.Kind==CommanderTargetSelectorKind.UnitType
@@ -152,6 +175,11 @@ namespace OpenEmpires
             // absence is not permission to answer about a different request.
             bool specific=catalog.Units.Any(u=>UnitMention(u.UnitType))||catalog.Buildings.Any(b=>BuildingMention(b.BuildingType));
             var groups=all.GroupBy(g=>(object)g.RequestAuthority??g).Where(g=>!specific||g.Any(HasMention)).ToList();
+            int? genericUnitCount=!specific?ReferencedUnitCount(query):null;
+            if(genericUnitCount.HasValue)
+                groups=groups.Where(g=>g.Any(s=>s is EnsureUnitCountGoal u
+                    ?(u.IsExplicitNewProduction?u.RequiredNewProductionCount:u.TargetTotal)==genericUnitCount.Value
+                    :s is CommanderCapabilityGoal c&&c.Action.UnitSelector.Count==genericUnitCount.Value)).ToList();
             // A count attached to a named subject disambiguates otherwise identical
             // owned requests. This is selection for observation, never authority.
             var counts=Regex.Matches(query??"",@"\b[0-9]{1,3}\b").Cast<Match>()
@@ -185,7 +213,7 @@ namespace OpenEmpires
         {
             string text=Regex.Replace(value??"",@"#\d+|\b(?:goal|building|unit|worker|producer)\s+(?:id\s*)?\d+\b","the resolved entity",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
             text=Regex.Replace(text,@"\(\s*-?\d+\s*,\s*-?\d+\s*\)","the resolved location");
-            text=Regex.Replace(text,@"\s+"," ").Trim();return text.Length<=160?text:text.Substring(0,157)+"...";
+            text=Regex.Replace(text,@"\s+"," ").Trim();return text.Length<=160?text:"The full blocker is unavailable within the bounded task-fact projection.";
         }
         private static CommanderTacticalStepSnapshot Step(GameSimulation sim,CommanderGoalManager manager,CommanderGoal goal)
         {
@@ -217,7 +245,26 @@ namespace OpenEmpires
             }
             else if(goal is BuildStructureGoal building)
             { requested=building.Count;label="Build "+building.Count+" "+CommanderIntentCatalog.GetStructureDisplayName(building.StructureType);
-                produced=building.AttributedBuildingIds.Count(id=>sim.BuildingRegistry.GetBuilding(id) is BuildingData b&&b.PlayerId==goal.PlayerId&&!b.IsDestroyed&&!b.IsUnderConstruction); }
+                produced=building.AttributedBuildingIds.Count(id=>sim.BuildingRegistry.GetBuilding(id) is BuildingData b&&b.PlayerId==goal.PlayerId&&!b.IsDestroyed&&!b.IsUnderConstruction);
+                BuildingData acceptedFoundation=sim.BuildingRegistry.GetBuilding(building.PlacedBuildingId);
+                if(!building.IsTerminal&&acceptedFoundation!=null&&acceptedFoundation.PlayerId==goal.PlayerId
+                    &&!acceptedFoundation.IsDestroyed&&acceptedFoundation.IsUnderConstruction
+                    &&building.LastAcceptedBuilderUnitIds.Count>0
+                    &&building.AttributedBuildingIds.Contains(building.PlacedBuildingId))
+                {
+                    bool dead=false,foreign=false,human=false;
+                    foreach(int builderId in building.LastAcceptedBuilderUnitIds)
+                    {
+                        UnitData assigned=sim.UnitRegistry.GetUnit(builderId);
+                        if(assigned==null||assigned.CurrentHealth<=0||assigned.State==UnitState.Dead)dead=true;
+                        else if(assigned.PlayerId!=goal.PlayerId)foreign=true;
+                        else if(manager.ObserveHumanProtection(builderId))human=true;
+                    }
+                    if(dead)reason="An accepted construction builder is dead or missing; no unrelated survivor proves that assignment complete.";
+                    else if(foreign)reason="An accepted construction builder is no longer owned; no foreign unit can satisfy the assignment.";
+                    else if(human){takeover=true;reason="The player took control of an accepted construction builder; that worker remains protected from Commander recovery.";}
+                }
+            }
             else if(goal is AllocateWorkersGoal workers){label="Workers to "+workers.Allocation.Destination.Resource;requested=workers.Allocation.Count??workers.SelectedWorkerIds.Count;takeover|=workers.HumanInterrupted;}
             else if(goal is ResourceAllocationGoal allocation){label="Workers to "+allocation.Resource;requested=allocation.TargetWorkers;}
             else if(goal is ReachAgeGoal age){label="Reach age "+age.TargetAge;requested=age.TargetAge;}
diff --git a/Open Empires/Assets/Scripts/AI/Commander/CommanderWorkerAuthority.cs b/Open Empires/Assets/Scripts/AI/Commander/CommanderWorkerAuthority.cs
index 7cb79ea..7882bda 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/CommanderWorkerAuthority.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/CommanderWorkerAuthority.cs	
@@ -75,7 +75,7 @@ namespace OpenEmpires
             return true;
         }
 
-        private bool CanUseUnit(int unitId, int goalId, int currentTick)
+        internal bool CanUseUnit(int unitId, int goalId, int currentTick)
         {
             UnitData unit = simulation.UnitRegistry.GetUnit(unitId);
             if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
@@ -192,26 +192,7 @@ namespace OpenEmpires
 
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Economy/AllocateWorkersGoal.cs b/Open Empires/Assets/Scripts/AI/Commander/Economy/AllocateWorkersGoal.cs
index fbb6714..29bcbb6 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Economy/AllocateWorkersGoal.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Economy/AllocateWorkersGoal.cs	
@@ -14,6 +14,7 @@ namespace OpenEmpires
     public sealed class AllocateWorkersGoal : CommanderGoal
     {
         public CommanderWorkerAllocation Allocation { get; }
+        internal CommanderBoundLocation DynamicResourceLocation { get; set; }
         public long ResourceProgress { get; internal set; }
         internal EnsureUnitCountGoal ResultSourceGoal;
         internal long GatheredIncomeAtActivation;
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs b/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs
index 7161857..7d2d1ef 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs	
@@ -48,6 +48,7 @@ namespace OpenEmpires
     public sealed class CommanderPendingClarification
     {
         public CommanderWorkerAllocationDraft Draft { get; }
+        public CommanderRequestGraphDraft GraphDraft { get; }
         public string OriginalText { get; }
         public string Question { get; }
         public int Turns { get; }
@@ -62,8 +63,20 @@ namespace OpenEmpires
             RuntimeGeneration = generation; Sequence = sequence; Turns = turns;
             RequestTicket = requestTicket;
         }
+        internal CommanderPendingClarification(CommanderRequestGraphDraft graphDraft, string original, string question,
+            int generation, long sequence, int turns = 0, CommanderRequestTicket requestTicket = null)
+        {
+            if (original != null && original.Length > CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters)
+                throw new ArgumentOutOfRangeException(nameof(original));
+            GraphDraft = graphDraft; OriginalText = original ?? string.Empty; Question = Bound(question, 180);
+            RuntimeGeneration = generation; Sequence = sequence; Turns = turns;
+            RequestTicket = requestTicket;
+        }
         internal string Serialize()
         {
+            if (GraphDraft != null) return new JObject { ["draftGraph"] = GraphDraft.ToPendingJson(),
+                ["missingSlots"] = GraphDraft.ToSlotsJson(), ["originalText"] = OriginalText,
+                ["question"] = Question }.ToString(Formatting.None);
             var missing = new JArray(); foreach (var field in Draft.MissingFields) missing.Add(field.ToString());
             return new JObject { ["draft"] = Draft.ToJson(), ["missingFields"] = missing, ["originalText"] = OriginalText, ["question"] = Question }.ToString(Formatting.None);
         }
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs b/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs
index f1e0c4e..1e94874 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs	
@@ -71,6 +71,15 @@ namespace OpenEmpires
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
@@ -105,7 +114,12 @@ namespace OpenEmpires
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
@@ -232,7 +246,10 @@ namespace OpenEmpires
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
@@ -265,7 +282,8 @@ namespace OpenEmpires
             var result = new List<EconomyTarget>(); var destination = goal.Allocation.Destination;
             foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                 if (!node.IsDepleted && node.Type == destination.Resource && ResourceSourceRules.Matches(node, destination.SourceKind)
-                    && LegalFarm(node, goal.PlayerId) && simulation.FogOfWar.GetVisibility(goal.PlayerId, node.TileX, node.TileZ) == TileVisibility.Visible)
+                    && LegalFarm(node, goal.PlayerId) && simulation.FogOfWar.GetVisibility(goal.PlayerId, node.TileX, node.TileZ) == TileVisibility.Visible
+                    && (goal.DynamicResourceLocation == null || goal.DynamicResourceLocation.AllowsResource(simulation, goal.PlayerId, node)))
                     result.Add(new EconomyTarget(node.Id, node.TileX, node.TileZ, node.FootprintWidth, node.FootprintHeight,
                         node.IsFarmNode ? 1 : (node.FootprintWidth + 2) * (node.FootprintHeight + 2) - node.FootprintWidth * node.FootprintHeight,
                         CommanderWorkerTargetKind.ResourceNode));
@@ -299,10 +317,21 @@ namespace OpenEmpires
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
@@ -323,6 +352,11 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs
index 4d09f5d..44e1543 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs	
@@ -1,5 +1,6 @@
 using System;
 using System.Linq;
+using System.Text.RegularExpressions;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
@@ -16,20 +17,41 @@ namespace OpenEmpires
         {
             if (pendingClarification == null) return false;
             if (pendingClarification.RuntimeGeneration != runtimeGeneration) { pendingClarification = null; return false; }
-            string whole = message.Trim().ToLowerInvariant();
+            string whole = NormalizeWholeForm(message);
             if (whole == "cancel" || whole == "never mind" || whole == "nevermind" || whole == "forget it")
             {
-                pendingClarification = null; AppendLine("Player", message); AppendLine("Commander", "Pending worker request cancelled; no goal was created."); return true;
+                pendingClarification = null; AppendLine("Player", message);
+                SetLatestRequestState("Cancelled", "Pending request cancelled; no goal was created.");
+                AppendLine("Commander", "Pending request cancelled; no goal was created."); return true;
+            }
+            bool negativeCancellation = Regex.IsMatch(whole,
+                @"^no,\s*cancel(?:\s+(?:that|this)\s+(?:proposal|plan))?$");
+            if (pendingClarification.GraphDraft != null && (whole == "no" || negativeCancellation))
+            {
+                pendingClarification = null; AppendLine("Player", message);
+                SetLatestRequestState("Cancelled", "Proposal dismissed; existing tasks are unchanged.");
+                AppendLine("Commander", "Proposal dismissed; existing tasks are unchanged."); return true;
             }
             // This only chooses whether a new turn supersedes a pending question.
             // It neither parses nor executes any gameplay action.
-            string[] standalone = { "build ", "train ", "make ", "produce ", "research ", "attack ", "scout ", "patrol ", "retreat ", "repair ", "defend ", "advance ", "put ", "gather ", "send ", "reach ", "assign ", "have ", "keep " };
-            if (standalone.Any(prefix => whole.StartsWith(prefix, StringComparison.Ordinal)))
+            string[] standalone = { "build ", "train ", "make ", "produce ", "research ", "attack ", "scout ", "patrol ", "retreat ", "repair ", "defend ", "advance ", "put ", "gather ", "send ", "reach ", "assign ", "have ", "keep ", "move ", "garrison ", "ungarrison ", "explore ", "find " };
+            if (!(pendingClarification.GraphDraft != null && CommanderRequestGraphDraft.IsCorrectionUtterance(message))
+                && standalone.Any(prefix => whole.StartsWith(prefix, StringComparison.Ordinal)))
             { pendingClarification = null; return false; }
             var previous = pendingClarification;
-            pendingClarification = new CommanderPendingClarification(previous.Draft, previous.OriginalText, previous.Question,
-                previous.RuntimeGeneration, previous.Sequence, previous.Turns + 1, previous.RequestTicket);
-            if (whole == "yes" || whole == "no" || whole == "those" || whole == "there")
+            if (previous.GraphDraft != null && previous.Turns >= 6)
+            {
+                pendingClarification = null;
+                AppendLine("Player", message);
+                AppendLine("Commander", "Clarification expired after six replies; please give a new complete request.");
+                return true;
+            }
+            pendingClarification = previous.GraphDraft != null
+                ? new CommanderPendingClarification(previous.GraphDraft, previous.OriginalText, previous.Question,
+                    previous.RuntimeGeneration, previous.Sequence, previous.Turns + 1, previous.RequestTicket)
+                : new CommanderPendingClarification(previous.Draft, previous.OriginalText, previous.Question,
+                    previous.RuntimeGeneration, previous.Sequence, previous.Turns + 1, previous.RequestTicket);
+            if (previous.GraphDraft == null && (whole == "yes" || whole == "no" || whole == "those" || whole == "there"))
             { AppendLine("Player", message); ReaskPending("Please specify the missing worker criteria."); return true; }
             return false;
         }
@@ -37,14 +59,20 @@ namespace OpenEmpires
         private void ReaskPending(string reason)
         {
             if (pendingClarification == null) return;
-            if (pendingClarification.Turns >= 3)
-            { pendingClarification = null; AppendLine("Commander", "Worker clarification expired after three replies; please give a new complete request."); }
+            if (pendingClarification.Turns >= (pendingClarification.GraphDraft != null ? 6 : 3))
+            {
+                bool graph = pendingClarification.GraphDraft != null;
+                pendingClarification = null;
+                AppendLine("Commander", graph
+                    ? "Clarification expired after six replies; please give a new complete request."
+                    : "Clarification expired after three replies; please give a new complete request.");
+            }
             else AppendLine("Commander", reason + " " + pendingClarification.Question);
         }
 
         private CommanderSemanticResult LocalCountContinuation(CommanderPendingClarification pending, string reply)
         {
-            if (pending == null || pending.Draft.CountMode != CommanderWorkerCountMode.Exact || pending.Draft.Count.HasValue
+            if (pending?.Draft == null || pending.Draft.CountMode != CommanderWorkerCountMode.Exact || pending.Draft.Count.HasValue
                 || !CommanderClarificationReplies.TryParseCount(reply, out int count)) return null;
             var updated = pending.Draft.WithCount(count);
             if (updated.MissingFields.Count == 0) return updated.CompleteResult();
@@ -53,10 +81,37 @@ namespace OpenEmpires
                 ["pending"] = updated.ToJson(), ["missingFields"] = missing }.ToString(Formatting.None));
         }
 
+        private static CommanderSemanticResult LocalGraphContinuation(CommanderPendingClarification pending, string reply)
+            => pending?.GraphDraft?.Resolve(reply);
+
         private bool HandleClarificationResult(CommanderSemanticResult result, CommanderPendingClarification previous,
-            string reply, int generation, CommanderRequestTicket requestTicket)
+            string reply, int generation, CommanderRequestTicket requestTicket, bool localGraphResult = false)
         {
             if (previous != null && !ReferenceEquals(previous, pendingClarification)) return true;
+            if (previous?.GraphDraft != null)
+            {
+                if (localGraphResult && (result.Outcome == CommanderSemanticOutcome.Request
+                    || result.Outcome == CommanderSemanticOutcome.DynamicPlan))
+                { pendingClarification = null; return false; }
+                if (localGraphResult && result.Outcome == CommanderSemanticOutcome.Clarify
+                    && result.PendingGraphDraft != null)
+                {
+                    pendingClarification = new CommanderPendingClarification(result.PendingGraphDraft,
+                        previous.OriginalText, result.SafeExplanation, generation, previous.Sequence,
+                        previous.Turns, previous.RequestTicket);
+                    return false;
+                }
+                ReaskPending("That reply did not safely resolve the retained request.");
+                return true;
+            }
+            if (result.PendingGraphDraft != null)
+            {
+                if (previous != null) { ReaskPending("That reply did not preserve the worker request."); return true; }
+                pendingClarification = new CommanderPendingClarification(result.PendingGraphDraft, reply,
+                    string.IsNullOrWhiteSpace(result.SafeExplanation) ? "Please clarify the proposed request." : result.SafeExplanation,
+                    generation, ++clarificationSequence, 0, requestTicket);
+                return false;
+            }
             if (result.PendingDraft != null)
             {
                 if (previous != null && !PreservesResolvedCriteria(previous.Draft, result.PendingDraft, reply))
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs
index a8d0c37..2eb7471 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs	
@@ -1,4 +1,5 @@
 using TMPro;
+using System.Text;
 using UnityEngine;
 using UnityEngine.EventSystems;
 using UnityEngine.UI;
@@ -12,6 +13,17 @@ namespace OpenEmpires
         private Button compactRecord, compactCancel;
         private TMP_Text compactRecordLabel;
         private TMP_Text compactApproveLabel;
+        private TMP_Text latestRequestText, providerConnectionText, pendingQuestionText, expandedApprovalText, launcherText;
+        private TMP_Text expandedReviewText;
+        private GameObject requestRecovery, pendingQuestion, expandedApproval, expandedReview, settingsMenu;
+        private Button retryRequestButton, editRequestButton, cancelRequestButton;
+        private Button chatTabButton, tasksTabButton;
+        private RectTransform bubbleContent;
+        private string latestRequestState = "Ready", latestRequestDetail = "";
+        private long presentationTurnRevision;
+        private long pendingStrategicPresentationTurn;
+        private string recoveryDraft;
+        private bool recoveryAvailable, tasksVisible, settingsMenuVisible;
         private bool expanded;
         private int inputRevision, voiceDraftRevision;
         private string voiceOwnedDraft;
@@ -65,17 +77,20 @@ namespace OpenEmpires
             bool voiceOperation = instance.VoiceState == CommanderVoiceState.Recording
                 || instance.VoiceState == CommanderVoiceState.Transcribing
                 || instance.VoiceState == CommanderVoiceState.Preview;
-            if (!voiceOperation && !instance.voiceSetupVisible && !instance.semanticSetupVisible && !instance.expanded) return false;
+            if (!voiceOperation && !instance.voiceSetupVisible && !instance.semanticSetupVisible
+                && !instance.settingsMenuVisible && !instance.expanded) return false;
             instance.lastEscapeActionFrame = Time.frameCount;
             if (voiceOperation) instance.voiceController?.Cancel();
             else if (instance.voiceSetupVisible) instance.HideVoiceSetup();
             else if (instance.semanticSetupVisible) instance.HideSemanticSetup();
+            else if (instance.settingsMenuVisible) instance.CloseSettingsMenu();
             else instance.MinimizeCommander();
             return true;
         }
 
         public void OpenCommander()
         {
+            CloseSettingsMenu();
             expanded = true;
             UpdatePresentation();
             inputField?.ActivateInputField();
@@ -105,9 +120,211 @@ namespace OpenEmpires
         {
             voiceOwnedDraft = null;
             lastPresentationActionFrame = -1;
+            recoveryAvailable = false;
+            recoveryDraft = null;
+            tasksVisible = false;
+            settingsMenuVisible = false;
+            SetLatestRequestState("Ready", "Commander is ready.");
             MinimizeCommander();
         }
 
+        private void SetLatestRequestState(string state, string detail)
+        {
+            latestRequestState = state ?? "Ready";
+            latestRequestDetail = detail ?? string.Empty;
+            UpdatePresentation();
+        }
+
+        private long BeginPresentationTurn()
+        {
+            currentRequestGoalId = 0;
+            recoveryAvailable = false;
+            LastSubmittedSemanticRequestId = null;
+            return ++presentationTurnRevision;
+        }
+
+        internal long LastSubmittedUiTurnId => presentationTurnRevision;
+
+        private bool IsLatestPresentationTurn(long turn) => turn == presentationTurnRevision;
+
+        private void PublishLatestRequestState(long turn, string state, string detail)
+        {
+            if (IsLatestPresentationTurn(turn)) SetLatestRequestState(state, detail);
+        }
+
+        private void OpenSettingsMenu()
+        {
+            HideVoiceSetup();
+            HideSemanticSetup();
+            MinimizeCommander();
+            settingsMenuVisible = true;
+            UpdatePresentation();
+        }
+
+        private void CloseSettingsMenu()
+        {
+            settingsMenuVisible = false;
+            if (settingsMenu != null) settingsMenu.SetActive(false);
+        }
+
+        private string ProviderConnectionLabel
+        {
+            get
+            {
+                string status = SemanticSetupStatus;
+                string route = semanticProvider is OpenRouterCommanderProvider router
+                    ? router.UsesGateway ? "Luna gateway" : "OpenRouter Luna"
+                    : "Text AI";
+                if (semanticSetupMode == SemanticSetupMode.Disabled) return "Text AI: disabled";
+                if (status.Contains("setup required")) return route + ": setup required";
+                if (status.Contains("timed out")) return route + ": timed out";
+                if (status.Contains("connection failed") || status.Contains("service unavailable"))
+                    return route + ": connection issue";
+                if (status.Contains("authentication failed") || status.Contains("access forbidden"))
+                    return route + ": access issue";
+                if (status.Contains("validated semantic data")) return route + ": validated response";
+                if (semanticProvider is OpenRouterCommanderProvider) return route + ": configured, unverified";
+                return "Text AI: local translator";
+            }
+        }
+
+        private string StrategicApprovalPreview()
+        {
+            StrategicIntent pending = strategicBridge?.PendingIntent;
+            if (pending == null) return string.Empty;
+            StrategicPlan template = StrategicPlanRegistry.CreateDefault().CreatePlan(pending);
+            StrategicAdaptationProposal adaptation = pendingAdaptationProposal;
+            var preview = new StringBuilder();
+            preview.AppendLine("Strategic proposal — no new plan started.");
+            preview.AppendLine("Source: validated AI recommendation for your Commander.");
+            if (adaptation != null)
+            {
+                preview.Append("Strategy #").Append(adaptation.SourcePlanId).Append(" (")
+                    .Append(adaptation.CurrentObjective).AppendLine(") remains active until you decide.");
+                preview.AppendLine("Proposed change:");
+            }
+            else preview.AppendLine("Proposed preparation:");
+            int count = adaptation?.ProposedTargets.Count ?? template.Milestones.Count;
+            for (int i = 0; i < count; i++)
+            {
+                string name = i < template.Milestones.Count ? template.Milestones[i].Name : "Next stage";
+                preview.Append("Step ").Append(i + 1).Append(" — ").Append(name).Append(": ");
+                bool any = false;
+                if (adaptation != null)
+                {
+                    foreach (StrategicAdaptationRequest request in adaptation.ProposedTargets[i].Requests)
+                    {
+                        if (any) preview.Append("; ");
+                        preview.Append(StrategicEffect(request, i, template));
+                        any = true;
+                    }
+                }
+                else foreach (StrategicTacticalGoalRequest request in template.Milestones[i].TacticalGoals)
+                {
+                    if (any) preview.Append("; ");
+                    preview.Append(StrategicEffect(request, i, template));
+                    any = true;
+                }
+                if (!any) preview.Append("ready when earlier steps finish");
+                preview.AppendLine(".");
+            }
+            if (adaptation != null)
+                preview.AppendLine("Ordinary Approve cannot replace the active strategy. Confirm as command explicitly; existing policy may reject, coexist, or replace only an eligible AI plan. Direct player plans are never replaced.");
+            else
+                preview.AppendLine("Approve strategy uses ordinary recommendation approval. Confirm as command is a separate stronger player decision. Neither happens by opening Details.");
+            return preview.ToString();
+        }
+
+        private string StrategicEffect(StrategicAdaptationRequest request, int step, StrategicPlan template)
+        {
+            switch (request.Kind)
+            {
+                case StrategicAdaptationRequestKind.WorkerAllocation:
+                    return request.WorkerTarget + " " + request.ResourceType.ToString().ToLowerInvariant() + " workers";
+                case StrategicAdaptationRequestKind.BuildStructure:
+                    return (request.EnsureExisting == true ? "ensure " : "add ") + request.Count + " " + StrategicBuildingName(request.StructureType.Value,request.Count.Value)
+                        + (request.EnsureExisting == true ? " exists" : "");
+                case StrategicAdaptationRequestKind.EnsureUnitCount:
+                    return StrategicUnitEffect(request.UnitType.Value, request.TargetTotal.Value, step, template);
+                default: return "validated step";
+            }
+        }
+
+        private string StrategicEffect(StrategicTacticalGoalRequest request, int step, StrategicPlan template)
+        {
+            if (request is StrategicResourceAllocationGoalRequest workers)
+                return workers.WorkerTarget + " " + workers.ResourceType.ToString().ToLowerInvariant() + " workers";
+            if (request is StrategicBuildStructureGoalRequest building)
+                return (building.EnsureExisting ? "ensure " : "add ") + building.Count + " " + StrategicBuildingName(building.StructureType,building.Count)
+                    + (building.EnsureExisting ? " exists" : "");
+            if (request is StrategicEnsureUnitCountGoalRequest units)
+                return StrategicUnitEffect(units.UnitType, units.TargetTotal, step, template);
+            return "validated step";
+        }
+
+        private string StrategicUnitEffect(int unitType, int total, int step, StrategicPlan template)
+        {
+            string units = unitType == CommanderIntentCatalog.ArcherUnitType ? "archers"
+                : unitType == CommanderIntentCatalog.SpearmanUnitType ? "spearmen"
+                : CommanderIntentCatalog.GetUnitDisplayName(unitType).ToLowerInvariant() + " units";
+            string producer = "its producer";
+            if (semanticSimulation != null && semanticSimulation.TryGetProductionBuildingType(
+                pendingStrategicOwner, unitType, out BuildingType building)) producer = building.ToString();
+            int producerStep = 0;
+            for (int i = 0; i < step && i < template.Milestones.Count; i++)
+                foreach (StrategicTacticalGoalRequest request in template.Milestones[i].TacticalGoals)
+                    if (request is StrategicBuildStructureGoalRequest build && build.StructureType.ToString() == producer)
+                        producerStep = i + 1;
+            return "reach " + total + " total " + units + " after "
+                + (producerStep > 0 ? "step " + producerStep + " secures " : "securing ") + producer
+                + " (existing units count; train only the shortfall)";
+        }
+
+        private static string StrategicBuildingName(BuildingType type, int count)
+            => type == BuildingType.Tower && count > 1 ? "towers" : type.ToString();
+
+        private void CancelActiveSemanticRequest()
+        {
+            semanticRequestRevision++;
+            activeSemanticRequest?.Cancel();
+            activeSemanticRequest = null;
+            recoveryAvailable = false;
+        }
+
+        private void OfferRequestRecovery(long turn, string original, string reason)
+        {
+            if (!IsLatestPresentationTurn(turn)) return;
+            recoveryDraft = original;
+            recoveryAvailable = true;
+            SetLatestRequestState("Failed", reason);
+        }
+
+        private void RetryLatestRequest()
+        {
+            if (!recoveryAvailable || submitting || recoveryDraft == null || InputText != recoveryDraft) return;
+            recoveryAvailable = false; // one physical action consumes this recovery offer
+            _ = SubmitCurrentInputAsync();
+        }
+
+        private void EditLatestRequest()
+        {
+            if (!recoveryAvailable) return;
+            recoveryAvailable = false;
+            inputField?.ActivateInputField();
+            SetLatestRequestState("Failed", "Edit the retained draft, then Send when ready.");
+        }
+
+        private void CancelLatestRequest()
+        {
+            if (submitting)
+            {
+                retainSubmittedDraft = true;
+                CancelActiveSemanticRequest();
+            }
+            recoveryAvailable = false;
+            SetLatestRequestState("Cancelled", "No new order will be submitted from that request.");
+        }
+
         private void OnDraftChanged(string value)
         {
             inputRevision++;
@@ -171,28 +388,63 @@ namespace OpenEmpires
 
         private void BuildPresentationUI()
         {
+            var transcriptViewport = transcriptScroll.viewport;
+            var bubbles = UIObject("BubbleContent", transcriptViewport.transform);
+            bubbleContent = bubbles.GetComponent<RectTransform>();
+            bubbleContent.anchorMin = new Vector2(0, 1);
+            bubbleContent.anchorMax = Vector2.one;
+            bubbleContent.pivot = new Vector2(.5f, 1);
+            bubbleContent.sizeDelta = Vector2.zero;
+            var bubbleLayout = bubbles.AddComponent<VerticalLayoutGroup>();
+            bubbleLayout.padding = new RectOffset(8, 8, 8, 8);
+            bubbleLayout.spacing = 8;
+            bubbleLayout.childControlWidth = bubbleLayout.childControlHeight = true;
+            bubbleLayout.childForceExpandWidth = true; bubbleLayout.childForceExpandHeight = false;
+            bubbles.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
+            transcriptScroll.content = bubbleContent;
+            transcriptText.gameObject.SetActive(false); // Retain exact transcript for API and legacy evidence.
+            RefreshChatBubbles();
             compactCommander = UIObject("CompactCommander",canvasRoot.transform);
             var rect = compactCommander.GetComponent<RectTransform>();
             rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
             // Leave the existing right-edge debug/HUD counters accessible.
-            rect.anchoredPosition = new Vector2(-110,-72); rect.sizeDelta = new Vector2(310,0);
-            compactCommander.AddComponent<Image>().color = new Color(.035f,.045f,.06f,.94f);
+            rect.anchoredPosition = new Vector2(-18,-72); rect.sizeDelta = new Vector2(178,0);
+            compactCommander.AddComponent<Image>().color = new Color(.055f,.065f,.075f,1f);
             var layout = compactCommander.AddComponent<VerticalLayoutGroup>();
             layout.padding = new RectOffset(8,8,8,8); layout.spacing = 6;
             layout.childControlWidth = layout.childControlHeight = true;
             layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
             compactCommander.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
             var open = CompactButton(compactCommander.transform,"Open","Commander",OpenCommander);
+            launcherText = open.GetComponentInChildren<TMP_Text>();
             var settings = CompactButton(compactCommander.transform,"VoiceOptions","Voice",()=>{if(voiceSetupVisible)HideVoiceSetup();else ShowVoiceSetup();});
             var textSettings = CompactButton(compactCommander.transform,"TextAIOptions","Text AI",()=>{if(semanticSetupVisible)HideSemanticSetup();else ShowSemanticSetup();});
             compactRecord = CompactButton(compactCommander.transform,"Record","● Record",OnPttButtonClicked);
             compactRecordLabel = compactRecord.GetComponentInChildren<TMP_Text>();
             compactCancel = CompactButton(compactCommander.transform,"CancelVoice","Cancel",OnVoiceCancelClicked);
-            PanelRow("Launcher",compactCommander.transform,open.gameObject,compactRecord.gameObject,compactCancel.gameObject,settings.gameObject);
+            PanelRow("Launcher",compactCommander.transform,open.gameObject);
             compactCommander.transform.Find("Launcher").SetAsFirstSibling();
-            PanelElement(textSettings.gameObject,28);
+            settings.gameObject.SetActive(false);
+            textSettings.gameObject.SetActive(false);
+            settingsMenu = UIObject("SettingsMenu", compactCommander.transform);
+            settingsMenu.AddComponent<Image>().color = new Color(.09f,.11f,.13f,1f);
+            PanelElement(settingsMenu, 192);
+            var settingsLayout = settingsMenu.AddComponent<VerticalLayoutGroup>();
+            settingsLayout.padding = new RectOffset(6,6,6,6); settingsLayout.spacing = 5;
+            settingsLayout.childControlWidth = settingsLayout.childControlHeight = true;
+            settingsLayout.childForceExpandWidth = true; settingsLayout.childForceExpandHeight = false;
+            var settingsHeading = Text("Heading",settingsMenu.transform,"SETTINGS",13,TextAlignmentOptions.Left);
+            settingsHeading.richText = false; PanelElement(settingsHeading.gameObject,18);
+            PanelElement(CompactButton(settingsMenu.transform,"VoiceSettings","Voice settings",()=>
+            {CloseSettingsMenu();ShowVoiceSetup();}).gameObject,32);
+            PanelElement(CompactButton(settingsMenu.transform,"TextAISettings","Text AI settings",()=>
+            {CloseSettingsMenu();ShowSemanticSetup();}).gameObject,32);
+            PanelElement(CompactButton(settingsMenu.transform,"CaptureEvidence","Capture UI evidence",()=>
+            {CloseSettingsMenu();RequestEvidenceScreenshot();}).gameObject,32);
+            PanelElement(CompactButton(settingsMenu.transform,"CloseSettings","Close",CloseSettingsMenu).gameObject,32);
+            settingsMenu.SetActive(false);
             compactStatus = Text("Status",compactCommander.transform,"Preparing Commander…",12,TextAlignmentOptions.Left);
-            compactStatus.richText = false; PanelElement(compactStatus.gameObject,38);
+            compactStatus.richText = false; PanelElement(compactStatus.gameObject,28);
             BuildTaskBoardUI();
             BuildVoiceSetupUI();
             BuildSemanticSetupUI();
@@ -203,33 +455,272 @@ namespace OpenEmpires
                     ()=>{if(pendingActionPlan!=null)HandleActionPlanControl("approve plan");else OpenCommander();},
                     ()=>{if(pendingActionPlan!=null)HandleActionPlanControl("cancel plan");else DismissStrategicRecommendation();}});
             compactApproveLabel = compactApproval.transform.Find("Actions/Action1").GetComponentInChildren<TMP_Text>();
+            var title = commanderPanel.Find("Title")?.GetComponent<TMP_Text>();
+            if (title != null) title.text = "COMMANDER";
+            commanderPanel.GetComponent<Image>().color = new Color(.065f,.075f,.085f,1f);
+            latestRequestText = Text("LatestRequest",commanderPanel,"Ready — Commander is ready.",14,TextAlignmentOptions.Left);
+            latestRequestText.richText = false; PanelElement(latestRequestText.gameObject,36);
+            providerConnectionText = Text("ProviderConnection",commanderPanel,"Text AI: local translator",12,TextAlignmentOptions.Left);
+            providerConnectionText.richText = false; PanelElement(providerConnectionText.gameObject,18);
+            providerConnectionText.transform.SetSiblingIndex(2);
+            latestRequestText.transform.SetSiblingIndex(3);
+            var chat = CompactButton(commanderPanel,"ChatTab","Chat",()=>{tasksVisible=false;UpdatePresentation();});
+            var tasks = CompactButton(commanderPanel,"TasksTab","Tasks",()=>
+            {tasksVisible=true;UpdateStrategicHostControls();UpdatePresentation();});
+            chatTabButton = chat; tasksTabButton = tasks;
+            var views = PanelRow("Views",commanderPanel,chat.gameObject,tasks.gameObject);
+            views.transform.SetSiblingIndex(4);
+            var settingsButton = CompactButton(commanderPanel,"Settings","Settings",OpenSettingsMenu);
+            PanelElement(settingsButton.gameObject,28);
+            settingsButton.GetComponent<LayoutElement>().ignoreLayout = true;
+            SetRect(settingsButton.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-122,-37),new Vector2(-52,-9));
             var minimize = CompactButton(commanderPanel,"Minimize","Minimize Commander",MinimizeCommander);
             PanelElement(minimize.gameObject,32);
             minimize.GetComponent<LayoutElement>().ignoreLayout = true;
-            SetRect(minimize.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-150,-38),new Vector2(-12,-10));
+            minimize.GetComponentInChildren<TMP_Text>().text="−";
+            SetRect(minimize.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-44,-37),new Vector2(-12,-9));
+            pendingQuestion = UIObject("PendingClarification",commanderPanel);
+            pendingQuestion.AddComponent<Image>().color = new Color(.14f,.19f,.23f,1f);
+            pendingQuestionText = Text("Question",pendingQuestion.transform,"",14,TextAlignmentOptions.Left);
+            pendingQuestionText.richText=false;
+            SetRect(pendingQuestionText.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,4),new Vector2(-8,-4));
+            PanelElement(pendingQuestion,64);pendingQuestion.transform.SetSiblingIndex(5);
+            expandedApproval = UIObject("Approval",commanderPanel);
+            expandedApproval.AddComponent<Image>().color = new Color(.14f,.19f,.23f,1f);
+            PanelElement(expandedApproval,168,true);expandedApproval.transform.SetSiblingIndex(6);
+            var approvalScroll = UIObject("PreviewScroll",expandedApproval.transform);
+            SetRect(approvalScroll.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,
+                new Vector2(8,44),new Vector2(-8,-8));
+            var approvalScrollRect = approvalScroll.AddComponent<ScrollRect>();
+            approvalScrollRect.horizontal=false;
+            approvalScrollRect.vertical=true;
+            approvalScrollRect.movementType=ScrollRect.MovementType.Clamped;
+            var approvalViewport=UIObject("Viewport",approvalScroll.transform);
+            SetRect(approvalViewport.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,
+                Vector2.zero,Vector2.zero);
+            approvalViewport.AddComponent<Image>().color=new Color(0,0,0,.1f);
+            approvalViewport.AddComponent<RectMask2D>();
+            approvalScrollRect.viewport=approvalViewport.GetComponent<RectTransform>();
+            expandedApprovalText=Text("Details",approvalViewport.transform,"",14,TextAlignmentOptions.TopLeft);
+            expandedApprovalText.richText=false;
+            expandedApprovalText.textWrappingMode=TextWrappingModes.Normal;
+            expandedApprovalText.rectTransform.anchorMin=new Vector2(0,1);
+            expandedApprovalText.rectTransform.anchorMax=Vector2.one;
+            expandedApprovalText.rectTransform.pivot=new Vector2(.5f,1);
+            expandedApprovalText.rectTransform.sizeDelta=Vector2.zero;
+            expandedApprovalText.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
+            approvalScrollRect.content=expandedApprovalText.rectTransform;
+            var approvePlan=CompactButton(expandedApproval.transform,"ApprovePlan","Approve plan",()=>HandleActionPlanControl("approve plan"));
+            SetRect(approvePlan.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(.5f,0),
+                new Vector2(8,7),new Vector2(-4,37));
+            var rejectPlan=CompactButton(expandedApproval.transform,"RejectPlan","Reject plan",()=>HandleActionPlanControl("cancel plan"));
+            SetRect(rejectPlan.GetComponent<RectTransform>(),new Vector2(.5f,0),new Vector2(1,0),
+                new Vector2(4,7),new Vector2(-8,37));
+            var approveStrategy=CompactButton(expandedApproval.transform,"ApproveStrategy","Approve strategy",()=>
+            {ApproveStrategicRecommendation();UpdatePresentation();});
+            SetRect(approveStrategy.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(1f/3f,0),
+                new Vector2(8,7),new Vector2(-4,37));
+            var confirmStrategy=CompactButton(expandedApproval.transform,"ConfirmStrategy","Confirm as command",()=>
+            {ConfirmStrategicCommand();UpdatePresentation();});
+            SetRect(confirmStrategy.GetComponent<RectTransform>(),new Vector2(1f/3f,0),new Vector2(2f/3f,0),
+                new Vector2(4,7),new Vector2(-4,37));
+            var dismissStrategy=CompactButton(expandedApproval.transform,"DismissStrategy","Dismiss",()=>
+            {DismissStrategicRecommendation();UpdatePresentation();});
+            SetRect(dismissStrategy.GetComponent<RectTransform>(),new Vector2(2f/3f,0),new Vector2(1,0),
+                new Vector2(4,7),new Vector2(-8,37));
+            requestRecovery = UIObject("RequestRecovery",commanderPanel);
+            retryRequestButton=CompactButton(requestRecovery.transform,"Retry","Retry",RetryLatestRequest);
+            editRequestButton=CompactButton(requestRecovery.transform,"Edit","Edit",EditLatestRequest);
+            cancelRequestButton=CompactButton(requestRecovery.transform,"Cancel","Cancel",CancelLatestRequest);
+            var recoveryLayout=requestRecovery.AddComponent<HorizontalLayoutGroup>();
+            recoveryLayout.spacing=8; recoveryLayout.childControlWidth=recoveryLayout.childControlHeight=true;
+            recoveryLayout.childForceExpandWidth=true;recoveryLayout.childForceExpandHeight=true;
+            PanelElement(requestRecovery,36);
+            expandedReview=UIObject("VoiceReview",commanderPanel);
+            expandedReview.AddComponent<Image>().color=new Color(.13f,.19f,.21f,1f);
+            PanelElement(expandedReview,68);
+            expandedReviewText=Text("ReviewText",expandedReview.transform,
+                "Reviewed transcript — not sent",13,TextAlignmentOptions.Left);
+            expandedReviewText.richText=false;
+            SetRect(expandedReviewText.rectTransform,new Vector2(0,1),Vector2.one,
+                new Vector2(8,-25),new Vector2(-8,-4));
+            var sendReviewed=CompactButton(expandedReview.transform,"SendReviewed","Send reviewed",()=>{_ = SubmitCurrentInputAsync();});
+            var editReviewed=CompactButton(expandedReview.transform,"EditReviewed","Edit",()=>inputField?.ActivateInputField());
+            var discardReviewed=CompactButton(expandedReview.transform,"DiscardReviewed","Discard",OnVoiceCancelClicked);
+            SetRect(sendReviewed.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(.5f,0),new Vector2(8,5),new Vector2(-4,35));
+            SetRect(editReviewed.GetComponent<RectTransform>(),new Vector2(.5f,0),new Vector2(.75f,0),new Vector2(4,5),new Vector2(-4,35));
+            SetRect(discardReviewed.GetComponent<RectTransform>(),new Vector2(.75f,0),new Vector2(1,0),new Vector2(4,5),new Vector2(-8,35));
             inputLengthText = Text("RequestLength",commanderPanel,"0 / 1024 characters",11,TextAlignmentOptions.Left);
             inputLengthText.richText = false; PanelElement(inputLengthText.gameObject,18);
+            requestRecovery.transform.SetSiblingIndex(commanderPanel.childCount-4);
+            expandedReview.transform.SetSiblingIndex(commanderPanel.childCount-3);
+            inputLengthText.transform.SetSiblingIndex(commanderPanel.childCount-2);
+            var inputRow=commanderPanel.Find("TextSubmission");
+            if(inputRow!=null)
+            {
+                inputRow.SetAsLastSibling();
+                var composerLayout=inputRow.GetComponent<HorizontalLayoutGroup>();
+                composerLayout.childForceExpandWidth=false;
+                composerLayout.childForceExpandHeight=true;
+                var inputLayout=inputRow.Find("Input")?.GetComponent<LayoutElement>();
+                if(inputLayout!=null)
+                {
+                    inputLayout.minHeight=inputLayout.preferredHeight=64;
+                    inputLayout.flexibleWidth=1;
+                }
+                var sendLayout=inputRow.Find("Send")?.GetComponent<LayoutElement>();
+                if(sendLayout!=null)
+                {
+                    sendLayout.minHeight=sendLayout.preferredHeight=64;
+                    sendLayout.preferredWidth=76;
+                    sendLayout.flexibleWidth=0;
+                }
+            }
+            var inputRect=commanderPanel.Find("TextSubmission/Input")?.GetComponent<RectTransform>();
+            if(inputRect!=null)
+            {
+                inputRect.parent.GetComponent<LayoutElement>().minHeight=68;
+                inputRect.parent.GetComponent<LayoutElement>().preferredHeight=68;
+                inputRect.GetComponent<Image>().color=new Color(.12f,.14f,.16f,1f);
+            }
+            inputField.lineType=TMP_InputField.LineType.MultiLineNewline;
+            inputField.textComponent.fontSize=14;
+            transcriptText.fontSize=15;
+            var transcriptImage=transcriptScroll.GetComponent<Image>();
+            if(transcriptImage!=null)transcriptImage.color=new Color(.09f,.105f,.12f,1f);
             inputField.onValueChanged.AddListener(OnDraftChanged);
             UpdatePresentation();
         }
 
+        private void RefreshChatBubbles()
+        {
+            if (bubbleContent == null) return;
+            for (int i = bubbleContent.childCount - 1; i >= 0; i--)
+            {
+                var old = bubbleContent.GetChild(i);
+                old.SetParent(null, false);
+                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
+            }
+            foreach (string entry in transcriptEntries)
+            {
+                int marker = entry.IndexOf(':');
+                string speaker = marker < 0 ? "Commander" : entry.Substring(0, marker);
+                string body = marker < 0 ? entry : entry.Substring(marker + 1).Trim();
+                bool player = speaker == "Player";
+                var bubble = UIObject(player ? "PlayerBubble" : "CommanderBubble", bubbleContent);
+                bubble.AddComponent<Image>().color = player
+                    ? new Color(.17f, .28f, .33f, 1f) : new Color(.13f, .15f, .17f, 1f);
+                var layout = bubble.AddComponent<VerticalLayoutGroup>();
+                layout.padding = new RectOffset(10, 10, 8, 8);
+                layout.spacing = 4;
+                layout.childControlWidth = layout.childControlHeight = true;
+                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
+                var name = Text("Speaker", bubble.transform, player ? "YOU" : "COMMANDER", 12,
+                    TextAlignmentOptions.Left);
+                name.richText = false; name.color = new Color(.6f, .83f, .87f, 1f);
+                PanelElement(name.gameObject, 16);
+                var message = Text("Message", bubble.transform, body, 15, TextAlignmentOptions.Left);
+                message.richText = false;
+                message.textWrappingMode = TextWrappingModes.Normal;
+                message.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
+                bubble.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
+            }
+            Canvas.ForceUpdateCanvases();
+            transcriptScroll.verticalNormalizedPosition = 0;
+        }
+
         private void UpdatePresentation()
         {
             if (compactCommander == null) return;
             RefreshTaskBoard();
             RefreshSemanticSetupStatus();
+            bool reviewing = VoiceState == CommanderVoiceState.Preview;
+            bool strategicChatApproval = !tasksVisible && strategicBridge?.PendingIntent != null;
             if (commanderPanel != null) commanderPanel.gameObject.SetActive(expanded);
             compactCommander.SetActive(!expanded);
+            // LatestRequest is the visible lifecycle source; keep the legacy text only
+            // as internal state so stale provider/debug status cannot crowd the dock.
+            if (commanderStatusText != null) commanderStatusText.gameObject.SetActive(false);
+            if (currentPlanStatusText != null)
+                currentPlanStatusText.gameObject.SetActive(selectedPlanId != 0 && !strategicChatApproval);
+            if (strategicChatApproval && strategicHostRow != null) strategicHostRow.SetActive(false);
+            if (voiceRootObject != null)
+            {
+                bool voiceNeedsStatus = VoiceState == CommanderVoiceState.Recording
+                    || VoiceState == CommanderVoiceState.Transcribing
+                    || VoiceState == CommanderVoiceState.Error;
+                voiceRootObject.GetComponent<LayoutElement>().minHeight = voiceNeedsStatus ? 56 : 36;
+                voiceRootObject.GetComponent<LayoutElement>().preferredHeight = voiceNeedsStatus ? 56 : 36;
+                if (voiceStatusText != null) voiceStatusText.gameObject.SetActive(voiceNeedsStatus);
+                voiceRootObject.SetActive(!reviewing);
+            }
+            if (latestRequestText != null)
+            {
+                string detail=latestRequestDetail.Length>90?latestRequestDetail.Substring(0,87)+"…":latestRequestDetail;
+                latestRequestText.text=latestRequestState+(detail.Length==0?"":" — "+detail);
+            }
+            if (providerConnectionText != null) providerConnectionText.text = ProviderConnectionLabel;
+            if (settingsMenu != null) settingsMenu.SetActive(settingsMenuVisible && !voiceSetupVisible && !semanticSetupVisible && !expanded);
+            if (launcherText != null) launcherText.text="Commander"+(taskBadgeText!=null&&taskBadgeText.gameObject.activeSelf?" • "+taskBadgeText.text:"")
+                +(pendingClarification!=null||pendingActionPlan!=null||recoveryAvailable?" !":"");
+            if(chatTabButton!=null)chatTabButton.GetComponent<Image>().color=tasksVisible?new Color(.13f,.15f,.17f,1f):new Color(.19f,.34f,.42f,1f);
+            if(tasksTabButton!=null)tasksTabButton.GetComponent<Image>().color=tasksVisible?new Color(.19f,.34f,.42f,1f):new Color(.13f,.15f,.17f,1f);
+            bool hasApproval = pendingActionPlan!=null||strategicBridge?.PendingIntent!=null;
+            // A pending preview replaces Chat's transcript scroll, rather than
+            // nesting an approval scroller alongside the conversation scroller.
+            if(transcriptScroll!=null)transcriptScroll.gameObject.SetActive(!tasksVisible&&!hasApproval);
+            if(taskBoardScroll!=null)taskBoardScroll.gameObject.SetActive(tasksVisible);
+            if(pendingQuestion!=null)
+            {
+                pendingQuestion.SetActive(pendingClarification!=null);
+                if(pendingClarification!=null)pendingQuestionText.text="Needs clarification — "+pendingClarification.Question;
+            }
+            if(expandedApproval!=null)
+            {
+                expandedApproval.SetActive(hasApproval&&!tasksVisible);
+                if(hasApproval)expandedApprovalText.text=pendingActionPlan?.Preview??StrategicApprovalPreview();
+                var approvePlan=expandedApproval.transform.Find("ApprovePlan");
+                var rejectPlan=expandedApproval.transform.Find("RejectPlan");
+                if(approvePlan!=null)approvePlan.gameObject.SetActive(pendingActionPlan!=null);
+                if(rejectPlan!=null)rejectPlan.gameObject.SetActive(pendingActionPlan!=null);
+                bool strategicOnly=pendingActionPlan==null&&strategicBridge?.PendingIntent!=null;
+                var approveStrategy=expandedApproval.transform.Find("ApproveStrategy");
+                var confirmStrategy=expandedApproval.transform.Find("ConfirmStrategy");
+                var dismissStrategy=expandedApproval.transform.Find("DismissStrategy");
+                if(approveStrategy!=null)
+                {
+                    approveStrategy.gameObject.SetActive(strategicOnly);
+                    approveStrategy.GetComponent<Button>().interactable=strategicOnly&&pendingAdaptationProposal==null&&!submitting;
+                    approveStrategy.GetComponent<Image>().color=pendingAdaptationProposal==null
+                        ?new Color(.18f,.29f,.42f,1f):new Color(.12f,.14f,.16f,1f);
+                    approveStrategy.GetComponentInChildren<TMP_Text>().color=pendingAdaptationProposal==null
+                        ?Color.white:new Color(.55f,.59f,.62f,1f);
+                }
+                if(confirmStrategy!=null)
+                {
+                    confirmStrategy.gameObject.SetActive(strategicOnly);
+                    confirmStrategy.GetComponent<Button>().interactable=strategicOnly&&!submitting;
+                }
+                if(dismissStrategy!=null)dismissStrategy.gameObject.SetActive(strategicOnly);
+            }
+            if(requestRecovery!=null)requestRecovery.SetActive(recoveryAvailable||submitting&&semanticProvider!=null);
+            if(expandedReview!=null)
+            {
+                expandedReview.SetActive(reviewing);
+                if(reviewing)expandedReviewText.text="Reviewed transcript — not sent. Edit or Send: "
+                    +(InputText.Length>72?InputText.Substring(0,69)+"…":InputText);
+            }
+            if(retryRequestButton!=null)retryRequestButton.gameObject.SetActive(recoveryAvailable);
+            if(editRequestButton!=null)editRequestButton.gameObject.SetActive(recoveryAvailable);
+            if(cancelRequestButton!=null)cancelRequestButton.gameObject.SetActive(recoveryAvailable||submitting&&semanticProvider!=null);
             BoundSetupCard(voiceSetupCard,532);
             BoundSetupCard(semanticSetupCard,336);
-            bool reviewing = VoiceState == CommanderVoiceState.Preview;
             compactReview.SetActive(reviewing);
             bool approving = pendingActionPlan != null || strategicBridge?.PendingIntent != null;
-            compactApproval.SetActive(approving);
+            compactApproval.SetActive(false);
             compactApproveLabel.text = pendingActionPlan != null ? "Approve plan" : "Review strategy";
             if (reviewing) compactReviewText.text = InputText;
-            if (approving) compactApprovalText.text = pendingActionPlan?.Preview
-                ?? "A strategy is awaiting consent. Open Details to review it and use the existing strategy confirmation controls.";
+            if (approving) compactApprovalText.text = pendingActionPlan?.Preview ?? StrategicApprovalPreview();
             bool busy = VoiceState == CommanderVoiceState.Recording || VoiceState == CommanderVoiceState.Transcribing;
             compactCancel.gameObject.SetActive(busy);
             compactRecord.interactable = voiceController != null && !submitting && CommanderVoiceSettings.VoiceEnabled
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.ReadOnlyQuestions.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.ReadOnlyQuestions.cs
index c478223..424f281 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.ReadOnlyQuestions.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.ReadOnlyQuestions.cs	
@@ -6,7 +6,7 @@ namespace OpenEmpires
 {
     public sealed partial class CommanderChatUI
     {
-        private async Task<CommanderAIChatSubmission> SubmitReadOnlyQuestionAsync(string message)
+        private async Task<CommanderAIChatSubmission> SubmitReadOnlyQuestionAsync(string message, long presentationTurn)
         {
             var sim=semanticSimulation;var manager=semanticGoalManager;var provider=semanticProvider;
             if(sim==null||manager==null||manager.IsDisposed||Conversation==null||Conversation.PlayerId!=manager.PlayerId)
@@ -15,14 +15,17 @@ namespace OpenEmpires
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
-                AppendLine("Commander",answer,false);Conversation.Memory.RecordExplanation(answer);
+                AppendLine("Commander",IsLatestPresentationTurn(presentationTurn)
+                    ?answer:"Earlier question ("+message+"): "+answer,false);
+                Conversation.Memory.RecordExplanation(answer);
             }
             AppendLine("Player",message,false);
             if(submitting||provider==null)
@@ -54,7 +57,9 @@ namespace OpenEmpires
                     if(answer==null)
                         answer=result?.IsValid==true&&(result.Outcome==CommanderSemanticOutcome.Answer||result.Outcome==CommanderSemanticOutcome.Clarify||result.Outcome==CommanderSemanticOutcome.Unsupported)
                             ?result.SafeExplanation:"You asked for information; no gameplay order was submitted.";
-                    AppendLine("Commander",answer,false);Conversation.Memory.RecordExplanation(answer);
+                    AppendLine("Commander",IsLatestPresentationTurn(presentationTurn)
+                        ?answer:"Earlier question ("+message+"): "+answer,false);
+                    Conversation.Memory.RecordExplanation(answer);
                 }
                 catch(OperationCanceledException){LocalFallback("The information request was cancelled; gameplay requests were unchanged.");}
                 catch(Exception){LocalFallback("I could not observe that safely; no gameplay requests changed.");}
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
index ae93489..8e1452a 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs	
@@ -45,6 +45,11 @@ namespace OpenEmpires
         private Button confirmStrategyButton;
         private Button dismissStrategyButton;
         private bool submitting;
+        private CancellationTokenSource activeSemanticRequest;
+        private int semanticRequestRevision;
+        // Local observation only. A read-only question never receives an executable ticket.
+        internal long? LastSubmittedSemanticRequestId { get; private set; }
+        private bool retainSubmittedDraft;
         private int runtimeGeneration;
         private StrategicAdaptationProposal pendingAdaptationProposal;
         private StrategicPipeline pendingStrategicPipeline;
@@ -103,10 +108,14 @@ namespace OpenEmpires
                 yield return null;
 
             GameBootstrapper bootstrap = GameBootstrapper.Instance;
-            Initialize(CommanderAIProviderFactory.CreateDefault(), bootstrap.Simulation,
+            bool offlineEvidenceFixture = ShouldStartCliOfflineFixture();
+            Initialize(offlineEvidenceFixture ? (ICommanderAIProvider)new OfflineEvidenceProvider()
+                : CommanderAIProviderFactory.CreateDefault(), bootstrap.Simulation,
                 bootstrap.Commander, bootstrap.CommanderDispatcher);
             if (bootstrap.CommanderStrategicPipeline != null)
                 InitializeStrategic(StrategicAIInterpreterFactory.Create(), bootstrap.CommanderStrategicPipeline);
+            if (offlineEvidenceFixture) StartCoroutine(CaptureOfflineFixtureOnce());
+            else if (ShouldStartCliEvidenceCapture()) StartCoroutine(CaptureStatusEvidenceOnce());
         }
 
         public void Initialize(ICommanderAIProvider provider, GameSimulation simulation,
@@ -120,6 +129,8 @@ namespace OpenEmpires
             if (semanticGoalManager != null && !semanticGoalManager.IsDisposed)
                 semanticGoalManager.CancelFutureSubscriptions();
             runtimeGeneration++;
+            CancelActiveSemanticRequest();
+            retainSubmittedDraft = false;
             currentRequestGoalId = 0;
             pendingClarification = null;
             ClearActionPlanPreview();
@@ -138,6 +149,7 @@ namespace OpenEmpires
             transcriptEntries.Clear();
             transcript.Clear();
             if (transcriptText != null) transcriptText.text = string.Empty;
+            RefreshChatBubbles();
             submitting = false;
             adapter = new CommanderAIIntentAdapter(provider, simulation, goalManager, dispatcher,
                 null, providerTimeout);
@@ -180,6 +192,7 @@ namespace OpenEmpires
             if (strategicPipeline != null && !ReferenceEquals(strategicPipeline, pipeline))
                 ResetConversation();
             runtimeGeneration++;
+            CancelActiveSemanticRequest();
             ClearStrategicPreview();
             lifetime?.Cancel();
             lifetime?.Dispose();
@@ -218,19 +231,26 @@ namespace OpenEmpires
             if (trimmed.Length == 0) return null;
             if(CommanderTacticalStatusProjection.IsInformationQuestion(trimmed))
             {
-                currentRequestGoalId = 0;
+                long informationTurn = BeginPresentationTurn();
+                int informationGeneration = runtimeGeneration;
+                SetLatestRequestState("Interpreting", trimmed);
                 if(trimmed.Length>CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters)
-                {AppendLine("Commander","That question is too long; please shorten it.",false);return null;}
+                {SetLatestRequestState("Failed", "Question is too long.");AppendLine("Commander","That question is too long; please shorten it.",false);return null;}
                 string informationForm=NormalizeExplanationWholeForm(trimmed);
                 if(informationForm=="strategy status"||informationForm=="current strategy status")
                 {
                     AppendLine("Player",trimmed,false);
                     AppendLine("Commander",strategicPipeline!=null&&Conversation!=null&&strategicPipeline.StrategicPlanner.PlayerId==Conversation.PlayerId
                         ?CurrentPlansStatus(strategicPipeline,Conversation.PlayerId):"Strategic Commander is not ready.",false);
+                    SetLatestRequestState("Completed", "Information answered; no gameplay order submitted.");
                     return null;
                 }
-                if(TryHandleExplanationQuery(trimmed))return null;
-                return await SubmitReadOnlyQuestionAsync(trimmed);
+                if(TryHandleExplanationQuery(trimmed))
+                {SetLatestRequestState("Completed", "Information answered; no gameplay order submitted.");return null;}
+                var informationResult = await SubmitReadOnlyQuestionAsync(trimmed, informationTurn);
+                if(this!=null&&informationGeneration==runtimeGeneration&&IsLatestPresentationTurn(informationTurn))
+                    SetLatestRequestState("Completed", "Information answered; no gameplay order submitted.");
+                return informationResult;
             }
             if (submitting)
             {
@@ -240,9 +260,16 @@ namespace OpenEmpires
                 if (pendingClarification == null) TryHandleStrategicLifecycle(trimmed);
                 return null;
             }
-            currentRequestGoalId = 0;
+            long presentationTurn = BeginPresentationTurn();
             if (HandlePendingControl(trimmed)) return null;
             if (HandleActionPlanControl(trimmed)) return null;
+            if (pendingClarification == null
+                && (NormalizeWholeForm(trimmed) == "yes" || NormalizeWholeForm(trimmed) == "no"))
+            {
+                AppendLine("Player", trimmed);
+                AppendLine("Commander", "No Commander question or action plan is awaiting that answer.");
+                return null;
+            }
             ClearActionPlanPreview(); // An independent player turn supersedes an unapproved candidate.
             // Any new player turn dismisses an adaptation, while no-plan recommendations
             // retain the established preference/explanation query behavior.
@@ -263,7 +290,7 @@ namespace OpenEmpires
             }
 
             string wholeForm = NormalizeWholeForm(trimmed);
-            if (wholeForm == "clear memory")
+            if (wholeForm == "clear memory" || wholeForm == "reset commander")
             {
                 ResetConversation();
                 return null;
@@ -283,6 +310,8 @@ namespace OpenEmpires
             }
             if (submitting) return null;
             submitting = true;
+            retainSubmittedDraft = false;
+            SetLatestRequestState("Interpreting", trimmed);
             SetCommanderStatus("Commander is interpreting...", false);
             int generation = runtimeGeneration;
             IReadOnlyList<MemoryEntry> memoryAtTurnStart = Conversation.Snapshot();
@@ -292,12 +321,13 @@ namespace OpenEmpires
             LatestSubmission = null;
             UpdateStrategicControls();
             AppendLine("Player", trimmed);
+            int submittedDraftRevision = inputRevision;
             inputField.interactable = false;
             sendButton.interactable = false;
             try
             {
                 if (semanticProvider != null)
-                    return await SubmitSemanticTacticalAsync(trimmed, generation);
+                    return await SubmitSemanticTacticalAsync(trimmed, generation, presentationTurn);
 
                 CommanderTextRoute route = intentRouter.Classify(trimmed);
                 if (route == CommanderTextRoute.Rejected)
@@ -365,8 +395,11 @@ namespace OpenEmpires
                         pendingStrategicIntentId = pending.IntentId;
                         pendingStrategicLastPlanId = sourceLastPlanId;
                         pendingStrategicHadNoPlan = sourceCount == 0;
+                        pendingStrategicPresentationTurn = presentationTurn;
                     }
                     LatestStrategicInterpretation = result;
+                    if (result.Success)
+                        PublishLatestRequestState(presentationTurn, "Awaiting approval", trimmed);
                     AppendLine("Commander", result.Success
                         ? StrategicPreview(result.Intent.ObjectiveType)
                             + (pendingAdaptationProposal == null
@@ -383,7 +416,8 @@ namespace OpenEmpires
                 if (this != null && generation == runtimeGeneration)
                 {
                     LatestSubmission = tacticalResult;
-                    TrackCurrentRequest(tacticalResult?.CommanderSubmission);
+                    if (IsLatestPresentationTurn(presentationTurn))
+                        TrackCurrentRequest(tacticalResult?.CommanderSubmission);
                     AppendLine("Commander", tacticalResult?.DisplayText ?? "The order failed safely.");
                 }
                 return tacticalResult;
@@ -395,7 +429,8 @@ namespace OpenEmpires
                     submitting = false;
                     inputField.interactable = true;
                     sendButton.interactable = true;
-                    inputField.text = string.Empty;
+                    if (!retainSubmittedDraft && inputRevision == submittedDraftRevision
+                        && inputField.text == trimmed) inputField.text = string.Empty;
                     UpdateStrategicControls();
                     UpdateVoiceUI();
                     if (commanderStatusText != null && commanderStatusText.text == "Commander is interpreting...")
@@ -405,7 +440,7 @@ namespace OpenEmpires
         }
 
         private async Task<CommanderAIChatSubmission> SubmitSemanticTacticalAsync(
-            string message, int generation)
+            string message, int generation, long presentationTurn)
         {
             ICommanderSemanticProvider provider = semanticProvider;
             CommanderPendingClarification capturedPending = pendingClarification;
@@ -430,21 +465,34 @@ namespace OpenEmpires
                 return null;
             }
 
+            int requestRevision = ++semanticRequestRevision;
             using (var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
             {
+                activeSemanticRequest = request;
                 request.CancelAfter(semanticProviderTimeout);
                 try
                 {
                     bool IsCurrentScope() => this != null && runtimeGeneration == generation
+                        && semanticRequestRevision == requestRevision
                         && !lifetime.IsCancellationRequested
                         && ReferenceEquals(semanticGoalManager, manager)
                         && ReferenceEquals(semanticSimulation, simulation)
                         && ReferenceEquals(semanticDispatcher, dispatcher)
                         && ReferenceEquals(semanticProvider, provider);
                     var requestTicket = capturedPending?.RequestTicket ?? manager.BeginSemanticRequest(message, generation, IsCurrentScope);
+                    LastSubmittedSemanticRequestId = requestTicket?.Id;
+                    if (capturedPending != null && (!ReferenceEquals(pendingClarification, capturedPending)
+                        || request.IsCancellationRequested || requestTicket == null
+                        || capturedPending.OriginalText != requestTicket.OriginalInput
+                        || !requestTicket.BindLocalContinuation(manager, generation, IsCurrentScope)))
+                    {
+                        pendingClarification = null;
+                        AppendLine("Commander", "The retained request is no longer current; please submit a new request.");
+                        return null;
+                    }
                     semanticStage = "context-projection";
                     CommanderContext context = new CommanderContextBuilder().Build(simulation, manager,
-                        capturedPending?.OriginalText ?? message);
+                        message, capturedPending?.GraphDraft?.IsNameConfirmation == true ? null : capturedPending?.Serialize());
                     if (context.PlayerId != owner)
                     {
                         AppendLine("Commander", "Commander ownership changed; ask again.");
@@ -453,14 +501,23 @@ namespace OpenEmpires
                     IReadOnlyList<CommanderSemanticMemoryEntry> semanticMemoryAtTurnStart =
                         Conversation.SemanticMemory.Snapshot();
                     TryObserveQuestionAnswer(NormalizeExplanationWholeForm(message), out string questionFacts);
-                    var providerRequest = new CommanderSemanticProviderRequest(message, context,
-                        semanticMemoryAtTurnStart, questionFacts, capturedPending);
-                    semanticStage = "provider-request";
-                    Debug.Log("[Commander] context=projected;provider-request=started");
                     CommanderSemanticResult localCount = LocalCountContinuation(capturedPending, message);
-                    Task<CommanderSemanticResult> translation = localCount == null
-                        ? RequestSemanticTranslation(provider, providerRequest, request.Token)
-                        : Task.FromResult(localCount);
+                    CommanderSemanticResult localGraph = LocalGraphContinuation(capturedPending, message);
+                    if (capturedPending?.GraphDraft != null && localGraph == null)
+                    {
+                        semanticStage = "graph-local-reask";
+                        ReaskPending(CommanderRequestGraphDraft.IsCorrectionUtterance(message)
+                            ? "Please identify one graph node and field to correct with a supported value."
+                            : "That reply did not identify one safe graph detail.");
+                        return null;
+                    }
+                    semanticStage = localCount != null || localGraph != null ? "local-continuation" : "provider-request";
+                    if (localCount == null && localGraph == null)
+                        Debug.Log("[Commander] context=projected;provider-request=started");
+                    Task<CommanderSemanticResult> translation = localCount == null && localGraph == null
+                        ? RequestSemanticTranslation(provider, new CommanderSemanticProviderRequest(message, context,
+                            semanticMemoryAtTurnStart, questionFacts, capturedPending), request.Token)
+                        : Task.FromResult(localCount ?? localGraph);
                     // A provider may ignore cancellation. Keep the host usable and observe
                     // any late fault without ever admitting its late response.
                     _ = translation.ContinueWith(task => { var ignored = task.Exception; },
@@ -473,13 +530,17 @@ namespace OpenEmpires
                     {
                         request.Cancel();
                         if (this != null && generation == runtimeGeneration
-                            && !lifetime.IsCancellationRequested)
-                            AppendLine("Commander", "Commander AI request timed out. Please try again or use offline commands.");
+                            && semanticRequestRevision == requestRevision && !lifetime.IsCancellationRequested)
+                        {
+                            retainSubmittedDraft = true;
+                            ReportRequestTimeout(presentationTurn, message);
+                        }
                         return null;
                     }
                     CommanderSemanticResult result = await translation;
                     semanticStage = "semantic-response";
                     if (this == null || generation != runtimeGeneration
+                        || semanticRequestRevision != requestRevision
                         || !ReferenceEquals(semanticProvider, provider)
                         || !ReferenceEquals(semanticSimulation, simulation)
                         || !ReferenceEquals(semanticGoalManager, manager)
@@ -490,6 +551,7 @@ namespace OpenEmpires
 
                     if (result == null || !result.IsValid)
                     {
+                        PublishLatestRequestState(presentationTurn, "Failed", "The response could not be safely interpreted.");
                         if (capturedPending != null) { ReaskPending("That reply could not safely fill the missing criteria."); return null; }
                         AppendLine("Commander", provider is OpenRouterCommanderProvider && result != null
                             ? result.SafeExplanation : "I couldn't understand that request safely.");
@@ -506,20 +568,25 @@ namespace OpenEmpires
                     if (result.Outcome != CommanderSemanticOutcome.Request
                         && result.Outcome != CommanderSemanticOutcome.DynamicPlan)
                     {
-                        if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket)) return null;
+                        if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket,
+                            ReferenceEquals(result, localGraph))) return null;
                         if (result.Outcome == CommanderSemanticOutcome.Clarify)
                             Conversation.SemanticMemory.RecordClarification(result.SafeExplanation);
+                        PublishLatestRequestState(presentationTurn, result.Outcome == CommanderSemanticOutcome.Clarify
+                            ? "Needs clarification" : "Completed", result.SafeExplanation);
                         AppendLine("Commander", string.IsNullOrWhiteSpace(result.SafeExplanation)
                             ? "Please clarify or try a supported Commander request."
                             : result.SafeExplanation);
                         return null;
                     }
-                    if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket)) return null;
+                    if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket,
+                        ReferenceEquals(result, localGraph))) return null;
                     if (result.Outcome == CommanderSemanticOutcome.DynamicPlan)
                     {
                         semanticStage = "dynamic-admission";
-                        pendingActionPlan = manager.PrepareActionPlan(result, message, generation,
+                        pendingActionPlan = manager.PrepareActionPlan(result, requestTicket.OriginalInput, generation,
                             strategicPipeline, IsCurrentScope, requestTicket);
+                        PublishLatestRequestState(presentationTurn, "Awaiting approval", requestTicket.OriginalInput);
                         AppendLine("Commander", pendingActionPlan.Preview);
                         UpdateStrategicControls();
                         return null;
@@ -539,8 +606,9 @@ namespace OpenEmpires
                             AppendLine("Commander", graphReason);
                             return null;
                         }
-                        pendingActionPlan = manager.PrepareActionPlan(result, message, generation,
+                        pendingActionPlan = manager.PrepareActionPlan(result, requestTicket.OriginalInput, generation,
                             strategicPipeline, IsCurrentScope, requestTicket);
+                        PublishLatestRequestState(presentationTurn, "Awaiting approval", requestTicket.OriginalInput);
                         AppendLine("Commander", pendingActionPlan.Preview);
                         UpdateStrategicControls();
                         return null;
@@ -596,6 +664,8 @@ namespace OpenEmpires
                         pendingStrategicIntentId = pending.IntentId;
                         pendingStrategicLastPlanId = sourceLastPlanId;
                         pendingStrategicHadNoPlan = sourceCount == 0;
+                        pendingStrategicPresentationTurn = presentationTurn;
+                        PublishLatestRequestState(presentationTurn, "Awaiting approval", message);
                         AppendLine("Commander", StrategicPreview(pending.ObjectiveType)
                             + (pendingAdaptationProposal == null
                                 ? " No plan started. Approve normally, or explicitly confirm as your command (may replace AI plans, never direct-player plans)."
@@ -626,31 +696,54 @@ namespace OpenEmpires
                     var chatSubmission = new CommanderAIChatSubmission(providerResult,
                         interpretation, submission, display);
                     LatestSubmission = chatSubmission;
+                    PublishLatestRequestState(presentationTurn, submission?.CreatedGoal == true ? "Accepted" : "Failed", display);
                     Debug.Log("[Commander] intent=admitted;goal-submitted=" + (submission?.CreatedGoal == true));
                     if (submission?.CreatedGoal == true)
                         RecordAcceptedSemanticNodes(result.Nodes);
-                    TrackCurrentRequest(submission);
+                    if (IsLatestPresentationTurn(presentationTurn)) TrackCurrentRequest(submission);
                     AppendLine("Commander", display);
                     return chatSubmission;
                 }
                 catch (System.OperationCanceledException)
                 {
                     if (this != null && generation == runtimeGeneration
-                        && !lifetime.IsCancellationRequested)
-                        AppendLine("Commander", "Commander AI request timed out. Please try again or use offline commands.");
+                        && semanticRequestRevision == requestRevision && !lifetime.IsCancellationRequested)
+                    {
+                        retainSubmittedDraft = true;
+                        ReportRequestTimeout(presentationTurn, message);
+                    }
                     return null;
                 }
                 catch (System.Exception error)
                 {
                     Debug.Log("[Commander] failed-stage=" + semanticStage + ";exception=" + error.GetType().Name);
                     if (this != null && generation == runtimeGeneration
-                        && !lifetime.IsCancellationRequested)
-                        AppendLine("Commander", "Commander translation failed safely; no order was submitted.");
+                        && semanticRequestRevision == requestRevision
+                        && !request.IsCancellationRequested && !lifetime.IsCancellationRequested)
+                    {
+                        PublishLatestRequestState(presentationTurn, "Failed", "Translation failed safely; no order was submitted.");
+                        AppendLine("Commander", IsLatestPresentationTurn(presentationTurn)
+                            ? "Commander translation failed safely; no order was submitted."
+                            : "Earlier request failed safely; no order was submitted.");
+                    }
                     return null;
                 }
+                finally
+                {
+                    if (ReferenceEquals(activeSemanticRequest, request)) activeSemanticRequest = null;
+                }
             }
         }
 
+        private void ReportRequestTimeout(long presentationTurn, string message)
+        {
+            bool latest = IsLatestPresentationTurn(presentationTurn);
+            OfferRequestRecovery(presentationTurn, message, "Timed out — no order was submitted.");
+            AppendLine("Commander", latest
+                ? "That request timed out. No order was submitted. Choose Retry, Edit, or Cancel."
+                : "An earlier order timed out. No order was submitted. To try it again, enter that order in the composer and press Send.");
+        }
+
         private void RecordAcceptedSemanticNodes(IReadOnlyList<CommanderSemanticNode> nodes)
         {
             if (Conversation == null || nodes == null) return;
@@ -723,6 +816,7 @@ namespace OpenEmpires
 
         private void ClearStrategicPreview()
         {
+            pendingStrategicPresentationTurn = 0;
             pendingAdaptationProposal = null;
             pendingStrategicPipeline = null;
             pendingStrategicGeneration = 0;
@@ -768,6 +862,7 @@ namespace OpenEmpires
             if (pendingAdaptationProposal != null && !confirmed) return null;
             StrategicIntent intent = confirmed ? strategicBridge.Confirm(id) : strategicBridge.TakeRecommendation(id);
             if (intent == null) return null;
+            long strategicPresentationTurn = pendingStrategicPresentationTurn;
             ClearStrategicPreview();
             pendingExplanationIntentId = intent.IntentId;
             pendingExplanationObjective = intent.ObjectiveType.ToString();
@@ -783,19 +878,25 @@ namespace OpenEmpires
                 pendingExplanationObjective = null;
             }
             AppendLine("Commander", LatestStrategicDecision.Outcome);
+            PublishLatestRequestState(strategicPresentationTurn,
+                LatestStrategicDecision.Submission?.CreatedPlan == true ? "Accepted" : "Failed",
+                LatestStrategicDecision.Outcome);
             UpdateStrategicControls();
             return LatestStrategicDecision;
         }
 
         public void DismissStrategicRecommendation()
         {
+            long strategicPresentationTurn = pendingStrategicPresentationTurn;
             strategicBridge?.ClearPending();
             ClearStrategicPreview();
+            PublishLatestRequestState(strategicPresentationTurn,"Cancelled","Strategic proposal dismissed; nothing started.");
             UpdateStrategicControls();
         }
 
         public void ResetConversation()
         {
+            CancelActiveSemanticRequest();
             if (semanticGoalManager != null && !semanticGoalManager.IsDisposed)
                 semanticGoalManager.CancelFutureSubscriptions();
             ClearActionPlanPreview();
@@ -807,10 +908,12 @@ namespace OpenEmpires
             lifetime?.Dispose();
             lifetime = new CancellationTokenSource();
             Conversation.Reset();
+            SetLatestRequestState("Ready", "Commander is ready.");
             adapter?.ResetHistory();
             transcript.Clear();
             transcriptEntries.Clear();
             if (transcriptText != null) transcriptText.text = string.Empty;
+            RefreshChatBubbles();
             strategicBridge?.Reset();
             ResetAdvisories();
             LatestStrategicInterpretation = null;
@@ -843,8 +946,13 @@ namespace OpenEmpires
                 var label = approveStrategyButton.GetComponentInChildren<TMP_Text>();
                 if (label != null) label.text = actionAvailable ? "Approve plan" : "Approve strategy";
             }
-            if (strategicApprovalRow != null) strategicApprovalRow.SetActive(actionAvailable || strategicBridge?.PendingIntent != null);
+            // The dock approval card is the sole visible decision row for both
+            // candidates and strategies; the buttons above retain their existing
+            // authority methods but must not form a second visible stage.
+            if (strategicApprovalRow != null) strategicApprovalRow.SetActive(false);
             UpdateStrategicHostControls();
+            if (strategicBridge?.PendingIntent != null && !tasksVisible && strategicHostRow != null)
+                strategicHostRow.SetActive(false);
         }
 
         public async Task<CommanderAIChatSubmission> SubmitCurrentInputAsync()
@@ -859,7 +967,7 @@ namespace OpenEmpires
             }
             if (voiceController?.State == CommanderVoiceState.Preview) voiceController.Cancel();
             CommanderAIChatSubmission result = await SubmitMessageAsync(text);
-            if (!submitting && InputText == text) InputText = string.Empty;
+            if (!submitting && !retainSubmittedDraft && InputText == text) InputText = string.Empty;
             return result;
         }
 
@@ -885,6 +993,7 @@ namespace OpenEmpires
                 if (i > 0) transcript.AppendLine().AppendLine();
                 transcript.Append(transcriptEntries[i]);
             }
+            RefreshChatBubbles();
             if (transcriptText != null)
             {
                 transcriptText.text = transcript.ToString();
@@ -950,6 +1059,7 @@ namespace OpenEmpires
 
         private void OnDestroy()
         {
+            CancelActiveSemanticRequest();
             if (destroyCleanupComplete) return;
             destroyCleanupComplete = true;
             pendingClarification = null;
@@ -1182,12 +1292,21 @@ namespace OpenEmpires
         {
             if (commanderPanel == null || canvasRoot == null) return;
             var canvasRect = canvasRoot.GetComponent<RectTransform>();
-            commanderPanel.sizeDelta = new Vector2(Mathf.Min(460, Mathf.Max(320, canvasRect.rect.width - 32)),
-                Mathf.Min(540, Mathf.Max(400, canvasRect.rect.height - 96)));
-            // The existing player-list HUD ends at267 reference pixels in local
-            // play. Keep it visible, without pushing Commander off the right edge.
-            commanderPanel.anchoredPosition = new Vector2(
-                Mathf.Clamp(canvasRect.rect.width-commanderPanel.sizeDelta.x-16,16,280),-72);
+            float scale = Mathf.Max(.01f, canvasRoot.GetComponent<Canvas>().scaleFactor);
+            Rect safe = Screen.safeArea;
+            float left = safe.width > 0 ? safe.xMin / scale : 0;
+            float right = safe.width > 0 ? safe.xMax / scale : canvasRect.rect.width;
+            float top = safe.height > 0 ? safe.yMax / scale : canvasRect.rect.height;
+            float bottom = safe.height > 0 ? safe.yMin / scale : 0;
+            // Preserve the real player-list HUD at the left; a zoomed Editor
+            // preview is not a reason to translate every HUD element.
+            float x = Mathf.Max(left + 16, 280);
+            float width = Mathf.Clamp(Mathf.Min(420, right - x - 16), 320, 460);
+            if (x + width + 16 > right) x = Mathf.Max(left + 8, right - width - 16);
+            float yInset = Mathf.Min(72, Mathf.Max(16, top - bottom - 360));
+            float height = Mathf.Clamp(top - bottom - yInset - 16, 320, 620);
+            commanderPanel.sizeDelta = new Vector2(width, height);
+            commanderPanel.anchoredPosition = new Vector2(x, -(canvasRect.rect.height - top + yInset));
         }
 
         private Button StrategyButton(Transform parent, string label, float left, float width,
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs
index 2cf6816..8772a9f 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs	
@@ -1,5 +1,7 @@
 using System;
 using System.Collections.Generic;
+using System.Collections.ObjectModel;
+using System.Text;
 using System.Threading;
 using System.Threading.Tasks;
 using Newtonsoft.Json;
@@ -87,6 +89,18 @@ namespace OpenEmpires
         private readonly TimeSpan timeout;
         private readonly int semanticMaxTokens;
         public string LastRequestTrace { get; private set; } = string.Empty;
+        public CommanderProviderUsage LastUsage { get; private set; } = CommanderProviderUsage.Unknown;
+        public int? LastInputEstimate { get; private set; }
+        public IReadOnlyDictionary<string, int> LastInputSectionEstimates { get; private set; }
+        private readonly object semanticGate = new object();
+        private SemanticRun activeSemanticRun;
+        private int semanticHttpAttemptCount;
+        private readonly List<CommanderProviderAttempt> recentSemanticAttempts = new List<CommanderProviderAttempt>();
+        public int SemanticHttpAttemptCount { get { lock (semanticGate) return semanticHttpAttemptCount; } }
+        public IReadOnlyList<CommanderProviderAttempt> RecentSemanticAttempts { get { lock (semanticGate)
+            return Array.AsReadOnly(recentSemanticAttempts.ToArray()); } }
+        public IReadOnlyList<CommanderProviderAttempt> LastSemanticAttempts { get; private set; }
+            = Array.AsReadOnly(Array.Empty<CommanderProviderAttempt>());
         internal bool HasConfiguration => !string.IsNullOrWhiteSpace(apiKey);
         internal bool UsesGateway => transport is CommanderGatewaySemanticTransport;
         internal bool UsesTransport(ICommanderHttpTransport candidate) => ReferenceEquals(transport,candidate);
@@ -97,6 +111,45 @@ namespace OpenEmpires
         internal bool HasAttemptedRequest { get; private set; }
         internal bool HasValidatedSemanticResponse { get; private set; }
 
+        private sealed class SemanticRun
+        {
+            public readonly List<CommanderProviderAttempt> Attempts = new List<CommanderProviderAttempt>();
+            public string Trace = string.Empty;
+            public CommanderProviderUsage Usage = CommanderProviderUsage.Unknown;
+            public int? HttpStatusCode;
+            public bool NetworkFailure;
+            public bool TimedOut;
+            public bool Attempted;
+            public bool Validated;
+        }
+
+        private void Publish(SemanticRun run)
+        {
+            lock (semanticGate)
+            {
+                if (!ReferenceEquals(activeSemanticRun, run)) return;
+                LastRequestTrace = run.Trace;
+                LastUsage = run.Usage;
+                LastHttpStatusCode = run.HttpStatusCode;
+                LastNetworkFailure = run.NetworkFailure;
+                LastRequestTimedOut = run.TimedOut;
+                HasAttemptedRequest = run.Attempted;
+                HasValidatedSemanticResponse = run.Validated;
+                LastSemanticAttempts = Array.AsReadOnly(run.Attempts.ToArray());
+            }
+        }
+
+        private void PublishEstimate(SemanticRun run, int? estimate,
+            IReadOnlyDictionary<string, int> sections)
+        {
+            lock (semanticGate)
+            {
+                if (!ReferenceEquals(activeSemanticRun, run)) return;
+                LastInputEstimate = estimate;
+                LastInputSectionEstimates = sections;
+            }
+        }
+
         public OpenRouterCommanderProvider(string apiKey = null,
             ICommanderHttpTransport transport = null, TimeSpan? providerTimeout = null,
             int semanticMaxTokens = 4096)
@@ -189,7 +242,10 @@ namespace OpenEmpires
         {
             if (request == null) throw new ArgumentNullException(nameof(request));
             token.ThrowIfCancellationRequested();
-            HasValidatedSemanticResponse = false;
+            var run = new SemanticRun();
+            lock (semanticGate) activeSemanticRun = run;
+            Publish(run);
+            PublishEstimate(run, null, null);
             if (string.IsNullOrWhiteSpace(apiKey))
                 return CommanderSemanticResult.ProviderRejected("OpenRouter is not configured.");
             if (request.IsPlayerMessageTooLong)
@@ -197,67 +253,73 @@ namespace OpenEmpires
             if (string.IsNullOrWhiteSpace(request.PlayerMessage))
                 return CommanderSemanticResult.ProviderRejected("A Commander request is required.");
 
-            string body = new JObject
+            if (!CommanderSemanticRequestPack.TryBuild(request, Model, semanticMaxTokens, out var packed, out string limitReason))
             {
-                ["model"] = Model,
-                ["messages"] = new JArray(
-                    new JObject { ["role"] = "system", ["content"] = SemanticInstruction
-                        + CommanderDynamicProviderVocabulary.Build(request.SerializedContext) + SemanticFidelityCheck },
-                    new JObject
-                    {
-                        ["role"] = "user",
-                        ["content"] = "Current detached Commander context:\n"
-                            + request.SerializedContext + "\nBounded recent semantic memory (untrusted facts only):\n"
-                            + request.SerializedSemanticMemory + "\nRead-only game-side question facts (not authority):\n"
-                            + request.QuestionFacts + "\nBounded pending clarification (untrusted semantic draft only):\n"
-                            + (request.IsReadOnlyQuestion?"INFORMATION-ONLY turn: Answer/Clarify/Unsupported only; never Request/DynamicPlan, instructions or approval. Tactical status is observed game-side; do not invent a reason or choose an ambiguous request.\n":string.Empty)
-                            + request.SerializedPendingClarification + "\nPlayer request:\n" + request.PlayerMessage
-                    }),
-                ["max_tokens"] = semanticMaxTokens,
-                ["reasoning"] = new JObject { ["effort"] = "none" }
-            }.ToString(Formatting.None);
+                PublishEstimate(run, packed?.EstimatedInputTokens, packed?.SectionEstimates);
+                return CommanderSemanticResult.ProviderRejected(limitReason);
+            }
+            PublishEstimate(run, packed.EstimatedInputTokens, packed.SectionEstimates);
+            string body = packed.Json;
 
             try
             {
                 string text = UnwrapJsonFence(await PostAndExtractTextAsync(body, token,
-                    CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
+                    CommanderDynamicPlan.MaximumCharacters, "initial", packed.EstimatedInputTokens,
+                    packed.SectionEstimates, run).ConfigureAwait(false));
                 CommanderSemanticResult result = CommanderSemanticJson.ParseProviderResponse(text);
-                if (!result.IsValid) Trace("schema=" + result.SchemaDiagnostic);
+                MarkLastSemanticAttempt(run, result.IsValid ? "semantic-valid" : "schema-invalid");
+                if (!result.IsValid) Trace("schema=" + result.SchemaDiagnostic, run);
                 if (!result.IsValid && CommanderSemanticSchemaRepair.TryCreateTemplate(text,
                     out JObject template, requireProviderDeclarations: true))
                 {
                     string repairBody = BuildRepairBody(template);
-                    string initialTrace = LastRequestTrace;
+                    int repairEstimate = CommanderSemanticRequestPack.EstimateBody(JObject.Parse(repairBody));
+                    if (repairEstimate >= CommanderSemanticRequestPack.InputLimit)
+                        return CommanderSemanticResult.ProviderRejected("Commander input limit: required schema repair cannot fit safely; no repair was submitted.");
+                    JObject repairJson = JObject.Parse(repairBody);
+                    var repairSections = new Dictionary<string, int> {
+                        ["contract"] = Encoding.UTF8.GetByteCount((string)repairJson["messages"]?[0]?["content"] ?? string.Empty),
+                        ["repairTemplate"] = Encoding.UTF8.GetByteCount((string)repairJson["messages"]?[1]?["content"] ?? string.Empty),
+                        ["messageMargin"] = CommanderSemanticRequestPack.MessageMargin };
+                    string initialTrace = run.Trace;
                     string repairedText = UnwrapJsonFence(await PostAndExtractTextAsync(repairBody,
-                        token, CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
-                    LastRequestTrace = initialTrace + "repair-attempt:\n" + LastRequestTrace;
+                        token, CommanderDynamicPlan.MaximumCharacters, "repair", repairEstimate,
+                        repairSections, run).ConfigureAwait(false));
+                    run.Trace = initialTrace + "repair-attempt:\n" + run.Trace;
+                    Publish(run);
                     result = CommanderSemanticJson.ParseProviderResponse(repairedText);
-                    if (!result.IsValid) Trace("repair-schema=" + result.SchemaDiagnostic);
+                    text = repairedText;
+                    if (!result.IsValid) Trace("repair-schema=" + result.SchemaDiagnostic, run);
                     if (!result.IsValid || !CommanderSemanticSchemaRepair.MatchesTemplate(
                         repairedText, template))
                         result = CommanderSemanticResult.ProviderRejected(
                             "Commander AI returned an invalid semantic response; no order was submitted.");
+                    MarkLastSemanticAttempt(run, result.IsValid ? "semantic-valid" : "schema-invalid");
                 }
                 Trace("semantic=" + (result.IsValid ? result.Outcome.ToString() : "invalid")
-                    + ";nodes=" + result.Nodes.Count);
-                HasValidatedSemanticResponse = result.IsValid;
-                return result.IsValid ? result : CommanderSemanticResult.ProviderRejected(
+                    + ";nodes=" + result.Nodes.Count, run);
+                run.Validated = result.IsValid;
+                Publish(run);
+                return result.IsValid ? result.RequireNameConfirmation(text, request) : CommanderSemanticResult.ProviderRejected(
                     "Commander AI returned an invalid semantic response; no order was submitted.");
             }
             catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
             catch (OperationCanceledException)
             {
-                Trace("request=timeout");
-                LastRequestTimedOut = true;
+                Trace("request=timeout", run);
+                run.TimedOut = true;
+                Publish(run);
                 return CommanderSemanticResult.ProviderRejected("Commander AI request timed out.");
             }
             catch (OpenRouterFailure failure)
             {
+                if (run.Attempts.Count > 0 && run.Attempts[run.Attempts.Count - 1].ResultStage == "response-extracted")
+                    MarkLastSemanticAttempt(run, "content-invalid-format");
                 return CommanderSemanticResult.ProviderRejected(failure.Message);
             }
             catch (Exception error)
             {
-                Trace("request=failed;exception=" + error.GetType().Name);
+                Trace("request=failed;exception=" + error.GetType().Name, run);
                 return CommanderSemanticResult.ProviderRejected(Unavailable);
             }
         }
@@ -305,18 +367,56 @@ namespace OpenEmpires
             }.ToString(Formatting.None);
         }
 
+        private void MarkLastSemanticAttempt(SemanticRun run, string stage)
+        {
+            if (run.Attempts.Count > 0)
+            {
+                var previous = run.Attempts[run.Attempts.Count - 1];
+                var updated = previous.WithResultStage(stage);
+                run.Attempts[run.Attempts.Count - 1] = updated;
+                lock (semanticGate)
+                {
+                    int index = recentSemanticAttempts.FindLastIndex(attempt => ReferenceEquals(attempt, previous));
+                    if (index >= 0) recentSemanticAttempts[index] = updated;
+                }
+                Publish(run);
+            }
+        }
+
         private async Task<string> PostAndExtractTextAsync(string body,
-            CancellationToken cancellationToken, int maximumContentCharacters)
+            CancellationToken cancellationToken, int maximumContentCharacters, string semanticKind = null,
+            int? inputEstimate = null, IReadOnlyDictionary<string, int> sectionEstimates = null,
+            SemanticRun run = null)
         {
-            LastRequestTrace = string.Empty;
-            HasAttemptedRequest = true; LastHttpStatusCode = null;
-            LastNetworkFailure = false; LastRequestTimedOut = false;
+            if (run != null)
+            {
+                run.Trace = string.Empty;
+                run.Usage = CommanderProviderUsage.Unknown;
+                run.Attempted = true;
+                run.HttpStatusCode = null;
+                run.NetworkFailure = false;
+                run.TimedOut = false;
+                lock (semanticGate) semanticHttpAttemptCount++;
+                Publish(run);
+            }
+            else
+            {
+                LastRequestTrace = string.Empty;
+                LastUsage = CommanderProviderUsage.Unknown;
+                HasAttemptedRequest = true; LastHttpStatusCode = null;
+                LastNetworkFailure = false; LastRequestTimedOut = false;
+            }
+            CommanderProviderUsage attemptUsage = CommanderProviderUsage.Unknown;
+            int? attemptHttpStatus = null;
             var elapsed = System.Diagnostics.Stopwatch.StartNew();
-            Trace("utc=" + DateTime.UtcNow.ToString("O") + ";model=" + Model + ";request=built");
+            string resultStage = "http-start";
+            Trace("utc=" + DateTime.UtcNow.ToString("O") + ";model=" + Model + ";request=built", run);
+            try
+            {
             using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
             {
                 deadline.CancelAfter(timeout);
-                Trace("http=sent");
+                Trace("http=sent", run);
                 CommanderHttpResponse response;
                 try { response = await transport.PostJsonAsync(new Uri(Endpoint),
                     body, new Dictionary<string, string>
@@ -325,41 +425,92 @@ namespace OpenEmpires
                     }, deadline.Token).ConfigureAwait(false); }
                 catch (CommanderHttpResponseLimitException error)
                 {
-                    LastHttpStatusCode = error.StatusCode;
-                    Trace("http=" + error.StatusCode + ";receive=byte-limit;durationMs=" + elapsed.ElapsedMilliseconds);
+                    resultStage = "response-byte-limit";
+                    attemptHttpStatus = error.StatusCode;
+                    if (run != null) { run.HttpStatusCode = attemptHttpStatus; Publish(run); }
+                    else LastHttpStatusCode = attemptHttpStatus;
+                    Trace("http=" + error.StatusCode + ";receive=byte-limit;durationMs=" + elapsed.ElapsedMilliseconds, run);
                     throw new OpenRouterFailure(error.StatusCode < 200 || error.StatusCode > 299
                         ? HttpFailure(error.StatusCode) : "Commander AI returned an oversized response.");
                 }
                 catch (System.Net.Http.HttpRequestException)
                 {
-                    LastNetworkFailure = true;
-                    Trace("http=network-failure;durationMs=" + elapsed.ElapsedMilliseconds);
+                    resultStage = "network-failure";
+                    if (run != null) { run.NetworkFailure = true; Publish(run); }
+                    else LastNetworkFailure = true;
+                    Trace("http=network-failure;durationMs=" + elapsed.ElapsedMilliseconds, run);
                     throw new OpenRouterFailure("Could not reach Commander AI. Check your connection.");
                 }
                 deadline.Token.ThrowIfCancellationRequested();
-                if (response == null) throw new OpenRouterFailure(Unavailable);
-                LastHttpStatusCode = response.StatusCode;
-                Trace("http=" + response.StatusCode + ";durationMs=" + elapsed.ElapsedMilliseconds);
+                if (response == null)
+                {
+                    resultStage = "transport-null";
+                    throw new OpenRouterFailure(Unavailable);
+                }
+                attemptHttpStatus = response.StatusCode;
+                if (run != null) { run.HttpStatusCode = attemptHttpStatus; Publish(run); }
+                else LastHttpStatusCode = attemptHttpStatus;
+                resultStage = response.StatusCode >= 200 && response.StatusCode <= 299 ? "http-success" : "http-failure";
+                Trace("http=" + response.StatusCode + ";durationMs=" + elapsed.ElapsedMilliseconds, run);
                 if (response.StatusCode < 200 || response.StatusCode > 299)
                     throw new OpenRouterFailure(HttpFailure(response.StatusCode));
+                // A 2xx status is transport success only. Every rejected envelope must
+                // have a failure stage even if a malformed token raises unexpectedly.
+                resultStage = "response-invalid-envelope";
                 try
                 {
+                    if (response.Body == null)
+                        throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
                     if (response.Body.Length > 65536)
+                    {
+                        resultStage = "response-oversize";
                         throw new OpenRouterFailure("Commander AI returned an oversized response.");
+                    }
                     JObject result = JObject.Parse(response.Body);
-                    if (result["error"] is JObject error)
-                        throw new OpenRouterFailure(HttpFailure((int?)error["code"] ?? 500));
-                    if (!(result["choices"] is JArray choices) || choices.Count != 1)
+                    attemptUsage = CommanderProviderUsage.Read(result);
+                    if (run != null) { run.Usage = attemptUsage; Publish(run); }
+                    else LastUsage = attemptUsage;
+                    if (result["error"] != null && result["error"].Type != JTokenType.Null)
+                    {
+                        if (!(result["error"] is JObject error))
+                            throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                        resultStage = "provider-error";
+                        JToken codeToken = error["code"];
+                        int code = 500;
+                        if (codeToken?.Type == JTokenType.Integer)
+                            int.TryParse(codeToken.ToString(Formatting.None), out code);
+                        if (code <= 0) code = 500;
+                        throw new OpenRouterFailure(HttpFailure(code));
+                    }
+                    if (!(result["choices"] is JArray choices) || choices.Count != 1 || !(choices[0] is JObject))
+                    {
+                        resultStage = "response-invalid-envelope";
+                        throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                    }
+                    JToken finishToken = choices[0]["finish_reason"];
+                    if (finishToken != null && finishToken.Type != JTokenType.Null
+                        && finishToken.Type != JTokenType.String)
                         throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
-                    string finish = (string)choices[0]["finish_reason"];
+                    string finish = finishToken?.Type == JTokenType.String ? (string)finishToken : null;
                     // Bounded diagnostic category BEFORE rejection; never echo
                     // arbitrary provider finish strings or the response body.
-                    Trace("finish="+(finish=="stop"?"stop":finish=="length"?"length":finish==null?"unspecified":"other"));
+                    Trace("finish="+(finish=="stop"?"stop":finish=="length"?"length":finish==null?"unspecified":"other"), run);
                     if (finish == "length")
+                    {
+                        resultStage = "response-truncated";
                         throw new OpenRouterFailure("Commander AI response was truncated; no order was submitted.");
+                    }
                     if (finish != null && finish != "stop")
+                    {
+                        resultStage = "response-incomplete";
                         throw new OpenRouterFailure("Commander AI did not return a completed semantic response.");
-                    JToken content = choices[0]["message"]?["content"];
+                    }
+                    if (!(choices[0]["message"] is JObject message))
+                    {
+                        resultStage = "response-invalid-envelope";
+                        throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                    }
+                    JToken content = message["content"];
                     string text = null;
                     if (content?.Type == JTokenType.String) text = (string)content;
                     else if (content is JArray parts)
@@ -367,20 +518,67 @@ namespace OpenEmpires
                         var buffer = new System.Text.StringBuilder();
                         foreach (JToken part in parts)
                         {
-                            if ((string)part["type"] != "text" || part["text"]?.Type != JTokenType.String)
+                            if (!(part is JObject textPart))
+                            {
+                                resultStage = "response-unsupported-parts";
+                                throw new OpenRouterFailure("Commander AI returned unsupported content parts.");
+                            }
+                            JToken partType = textPart["type"];
+                            if (partType != null && partType.Type != JTokenType.String)
+                                throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                            if ((string)partType != "text" || textPart["text"]?.Type != JTokenType.String)
+                            {
+                                resultStage = "response-unsupported-parts";
                                 throw new OpenRouterFailure("Commander AI returned unsupported content parts.");
-                            buffer.Append((string)part["text"]);
+                            }
+                            buffer.Append((string)textPart["text"]);
                         }
                         text = buffer.ToString();
                     }
+                    else if (content != null && content.Type != JTokenType.Null)
+                        throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
                     if (string.IsNullOrWhiteSpace(text))
+                    {
+                        resultStage = "response-empty";
                         throw new OpenRouterFailure("Commander AI returned an empty semantic response.");
+                    }
                     if (text.Length > maximumContentCharacters + 16)
+                    {
+                        resultStage = "response-oversize";
                         throw new OpenRouterFailure("Commander AI returned an oversized response.");
-                    Trace("assistantContent=extracted;characters=" + text.Length + ";finish=" + (finish ?? "unspecified"));
+                    }
+                    Trace("assistantContent=extracted;characters=" + text.Length + ";finish=" + (finish ?? "unspecified"), run);
+                    resultStage = "response-extracted";
                     return text;
                 }
-                catch (JsonException) { throw new OpenRouterFailure("Commander AI returned an invalid response envelope."); }
+                catch (JsonException)
+                {
+                    resultStage = "response-invalid-envelope";
+                    throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                }
+            }
+            }
+            catch (OperationCanceledException)
+            {
+                resultStage = cancellationToken.IsCancellationRequested ? "caller-canceled" : "timeout";
+                throw;
+            }
+            finally
+            {
+                if (semanticKind != null)
+                {
+                    var attempt = new CommanderProviderAttempt(semanticKind,
+                        inputEstimate ?? CommanderSemanticRequestPack.EstimateBody(JObject.Parse(body)),
+                        sectionEstimates ?? new Dictionary<string, int>(), attemptUsage,
+                        elapsed.ElapsedMilliseconds, attemptHttpStatus, resultStage);
+                    run.Attempts.Add(attempt);
+                    lock (semanticGate)
+                    {
+                        recentSemanticAttempts.Add(attempt);
+                        if (recentSemanticAttempts.Count > 32) recentSemanticAttempts.RemoveAt(0);
+                    }
+                    Publish(run);
+                }
             }
         }
 
@@ -414,10 +612,15 @@ namespace OpenEmpires
             return inside; // Strict parser still rejects prose, duplicate objects and authority fields.
         }
 
-        private void Trace(string safeMetadata)
+        private void Trace(string safeMetadata, SemanticRun run = null)
         {
             // Call sites supply only controlled metadata, never input, output, headers or exceptions' messages.
-            if (LastRequestTrace.Length < 2000) LastRequestTrace += safeMetadata + "\n";
+            if (run != null)
+            {
+                if (run.Trace.Length < 2000) run.Trace += safeMetadata + "\n";
+                Publish(run);
+            }
+            else if (LastRequestTrace.Length < 2000) LastRequestTrace += safeMetadata + "\n";
             UnityEngine.Debug.Log("[CommanderProvider] " + safeMetadata);
         }
 
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticAdmission.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticAdmission.cs
index cfbe033..e775c9c 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticAdmission.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticAdmission.cs	
@@ -87,6 +87,7 @@ namespace OpenEmpires
 
                 case CommanderSemanticNodeType.MoveUnits:
                 case CommanderSemanticNodeType.ScoutArea:
+                case CommanderSemanticNodeType.DiscoverEnemy:
                 case CommanderSemanticNodeType.PatrolArea:
                 case CommanderSemanticNodeType.SetRallyPoint:
                 case CommanderSemanticNodeType.AttackTarget:
@@ -94,6 +95,8 @@ namespace OpenEmpires
                 case CommanderSemanticNodeType.RetreatUnits:
                 case CommanderSemanticNodeType.RepairTarget:
                 case CommanderSemanticNodeType.ResearchTechnology:
+                case CommanderSemanticNodeType.GarrisonUnits:
+                case CommanderSemanticNodeType.UngarrisonBuilding:
                     if (!node.UnitSelector.HasValue || !node.LocationSelector.HasValue)
                         return false;
                     if (node.Type == CommanderSemanticNodeType.ResearchTechnology && !node.Technology.HasValue)
@@ -101,7 +104,8 @@ namespace OpenEmpires
                     if (node.TargetSelector.HasValue && !node.TargetSelector.Value.IsCompatible(ToCapabilityAction(node.Type),
                         ToLocationSelector(node.LocationSelector.Value))) return false;
                     intent = new CapabilityActionIntent(context.PlayerId,
-                        ToCapabilityAction(node.Type), ToUnitSelector(node.UnitSelector.Value, node.UnitType, node.Count),
+                        ToCapabilityAction(node.Type), ToUnitSelector(node.UnitSelector.Value, node.UnitType, node.Count,
+                            node.AllMatchingUnits),
                         new CommanderLocationSelector(ToLocationSelector(node.LocationSelector.Value),
                             node.ResourceType), node.Technology, node.BuildingType,
                         node.Constraints, node.TargetSelector);
@@ -132,7 +136,7 @@ namespace OpenEmpires
         }
 
         private static CommanderUnitSelector ToUnitSelector(CommanderSemanticUnitSelector selector,
-            int? unitType, int? count)
+            int? unitType, int? count, bool allMatching = false)
         {
             CommanderUnitSelectorKind kind;
             switch (selector)
@@ -145,7 +149,8 @@ namespace OpenEmpires
                 case CommanderSemanticUnitSelector.DamagedMilitary: kind = CommanderUnitSelectorKind.DamagedMilitary; break;
                 default: kind = CommanderUnitSelectorKind.Military; break;
             }
-            return new CommanderUnitSelector(kind, Math.Max(1, count ?? 1), unitType ?? -1);
+            return new CommanderUnitSelector(kind, Math.Max(1, count ?? 1), unitType ?? -1,
+                allMatching);
         }
 
         private static CommanderLocationSelectorKind ToLocationSelector(CommanderSemanticLocationSelector selector)
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs
index 07b3acd..dc10efa 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs	
@@ -178,6 +178,14 @@ namespace OpenEmpires
                 if (sourceIntent is EnsureUnitCountIntent ensure)
                 {
                     if (!CanBindUnits(action, ensure.UnitType)) return false;
+                    if (action.ActionType == CommanderCapabilityActionType.GarrisonUnits
+                        && (ensure.UnitType != 0 || !ensure.NewProductionCount.HasValue
+                            || !(action.UnitSelector.Kind == CommanderUnitSelectorKind.Villagers
+                                || action.UnitSelector.Kind == CommanderUnitSelectorKind.UnitType
+                                    && action.UnitSelector.UnitType == 0)
+                            || action.UnitSelector.AllMatching
+                            || action.UnitSelector.Count != ensure.NewProductionCount.Value))
+                        return false;
                 }
                 else if (sourceIntent is BuildStructureIntent build)
                 {
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs
index 856fc5f..d5e01f6 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs	
@@ -78,6 +78,12 @@ namespace OpenEmpires
                 {
                     if (outcome == CommanderSemanticOutcome.Clarify && root.Property("pending") != null)
                     {
+                        if (root["pending"] is JObject graph && graph.Property("nodes") != null)
+                        {
+                            CheckFields(root, "outcome", "message", "pending", "missingSlots");
+                            return new CommanderSemanticResult(true, outcome, Array.Empty<CommanderSemanticNode>(),
+                                OptionalExplanation(root), pendingGraphDraft: CommanderRequestGraphDraft.Parse(root));
+                        }
                         CheckFields(root, "outcome", "message", "pending", "missingFields");
                         return new CommanderSemanticResult(true, outcome, Array.Empty<CommanderSemanticNode>(),
                             OptionalExplanation(root), ParsePendingDraft(root));
@@ -243,6 +249,8 @@ namespace OpenEmpires
                         throw new JsonException();
                     ResourceSourceKind? watchedSource = node.Property("sourceKind") == null
                         ? (ResourceSourceKind?)null : RequiredSemanticEnum<ResourceSourceKind>(node, "sourceKind");
+                    if (watchedSource.HasValue && !ResourceSourceRules.IsCompatible(watchedResource, watchedSource.Value))
+                        throw FailField(node, "sourceKind", SchemaFailureCode.IncompatibleDeclaration);
                     int? watchedOrdinal = node.Property("producerOrdinal") == null ? (int?)null
                         : RequiredBoundedInteger(node, "producerOrdinal", 1, 8);
                     if (!CanProduce(watchedProducer, watchedUnit)) throw new JsonException();
@@ -265,8 +273,19 @@ namespace OpenEmpires
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
@@ -287,9 +306,13 @@ namespace OpenEmpires
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
@@ -301,8 +324,14 @@ namespace OpenEmpires
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
@@ -314,7 +343,8 @@ namespace OpenEmpires
             int? resultFromNode = ParseOptionalNodeIndex(node, "resultFromNode");
             return new CommanderSemanticNode(action, count: count, resourceType: resource,
                 buildingType: structure, unitSelector: unit, locationSelector: location,
-                dependsOn: dependsOn, resultFromNode: resultFromNode, targetSelector: target);
+                dependsOn: dependsOn, resultFromNode: resultFromNode, targetSelector: target,
+                allMatchingUnits: allMatching);
         }
 
         // Shared strict target vocabulary for both semantic requests and legacy DTOs.
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs
index a901a06..d2a0365 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs	
@@ -28,6 +28,8 @@ namespace OpenEmpires
         public string SerializedPendingClarification { get; }
         public bool IsPlayerMessageTooLong { get; }
         public bool IsReadOnlyQuestion { get; }
+        // Game-owned retrieval metadata, retained independently of optional model-pack rows.
+        internal string UnresolvedNameInterpretations { get; }
 
         public CommanderSemanticProviderRequest(string playerMessage, CommanderContext context,
             IReadOnlyList<CommanderSemanticMemoryEntry> semanticMemory = null, string questionFacts = null,
@@ -38,12 +40,18 @@ namespace OpenEmpires
             string text = playerMessage ?? string.Empty;
             IsPlayerMessageTooLong = text.Length > MaximumPlayerMessageCharacters;
             PlayerMessage = IsPlayerMessageTooLong ? string.Empty : text;
+            var knowledge = ParseKnowledge(context.KnowledgeContext);
+            UnresolvedNameInterpretations = new JArray((knowledge["nameInterpretations"] as JArray ?? new JArray())
+                .OfType<JObject>().Where(h => (bool?)h["interpretationOnly"] == true && (bool?)h["ambiguous"] == false))
+                .ToString(Formatting.None);
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
@@ -104,7 +112,7 @@ namespace OpenEmpires
                     "DefensiveTurtle"),
                 ["actionCapabilities"] = new JArray("AllocateWorkers", "MoveUnits", "ScoutArea", "PatrolArea",
                     "SetRallyPoint", "AttackTarget", "DefendArea", "RetreatUnits",
-                    "RepairTarget", "ResearchTechnology"),
+                    "RepairTarget", "ResearchTechnology", "GarrisonUnits", "UngarrisonBuilding", "DiscoverEnemy"),
                 ["selectorCapabilities"] = new JObject
                 {
                     ["units"] = new JArray("Military", "Scout", "Villagers", "Spearman", "Archer", "Knight", "DamagedMilitary"),
@@ -122,7 +130,16 @@ namespace OpenEmpires
             root["workerCounts"] = new JObject { ["idleUnqueued"] = Math.Min(200, context.IdleVillagers), ["gatheringByResource"] = gathering };
             if(status!=null)root["tacticalStatus"]=status.Json();
             string serialized = root.ToString(Formatting.None);
-            if(status!=null&&serialized.Length>MaximumContextCharacters)
+            if (serialized.Length > MaximumContextCharacters
+                && root["knowledge"]?["requiredCanonicalIds"] is JArray required && required.Count > 0)
+            {
+                // Do not silently drop required rows to satisfy the detached-context bound.
+                // The pack recognizes this small marker and refuses the complete request.
+                root["knowledge"] = new JObject { ["requiredReferenceOverflow"] = true };
+                serialized = root.ToString(Formatting.None);
+            }
+            if(status!=null&&serialized.Length>MaximumContextCharacters
+                && (bool?)root["knowledge"]?["requiredReferenceOverflow"] != true)
             {
                 root["knowledge"]=new JObject{["omitted"]=true,["reason"]="Tactical status prioritized within unchanged context bound."};
                 serialized=root.ToString(Formatting.None);
@@ -145,6 +162,32 @@ namespace OpenEmpires
 
     public sealed partial class CommanderSemanticResult
     {
+        internal CommanderSemanticResult RequireNameConfirmation(string strictProposal, CommanderSemanticProviderRequest request)
+        {
+            if (!IsValid) return this;
+            var hints = JArray.Parse(request.UnresolvedNameInterpretations);
+            return RequireNameConfirmation(strictProposal, hints);
+        }
+
+        internal CommanderSemanticResult RequireNameConfirmation(string strictProposal, JArray hints)
+        {
+            if (!IsValid) return this;
+            if (hints.Count == 0) return this;
+            if (Outcome == CommanderSemanticOutcome.Clarify && PendingGraphDraft != null)
+                return new CommanderSemanticResult(true, Outcome, Nodes, SafeExplanation,
+                    pendingGraphDraft: PendingGraphDraft.WithNameRequirements(hints));
+            // The legacy worker draft cannot represent a unit/building hint. Preserve
+            // its entire strict proposal in the existing non-approvable local blocker.
+            if (Outcome != CommanderSemanticOutcome.Request && Outcome != CommanderSemanticOutcome.DynamicPlan
+                && !(Outcome == CommanderSemanticOutcome.Clarify && PendingDraft != null)) return this;
+            var draft = CommanderRequestGraphDraft.ConfirmNames(strictProposal, hints);
+            return NameConfirmation(draft);
+        }
+
+        internal static CommanderSemanticResult NameConfirmation(CommanderRequestGraphDraft draft)
+            => new CommanderSemanticResult(true, CommanderSemanticOutcome.Clarify,
+                Array.Empty<CommanderSemanticNode>(), draft.NameConfirmationQuestion, pendingGraphDraft: draft);
+
         internal static CommanderSemanticResult ProviderRejected(string safeExplanation)
         {
             return new CommanderSemanticResult(false, CommanderSemanticOutcome.Unsupported,
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs
index 423adf1..078c8f0 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs	
@@ -29,7 +29,10 @@ namespace OpenEmpires
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
@@ -93,6 +96,7 @@ namespace OpenEmpires
         public CommanderSemanticPlacementRelation? PlacementRelation { get; }
         public int? ClearGapTiles { get; }
         public CommanderSemanticUnitSelector? UnitSelector { get; }
+        public bool AllMatchingUnits { get; }
         public CommanderSemanticLocationSelector? LocationSelector { get; }
         public CommanderTargetSelector? TargetSelector { get; }
         public CommanderProductionQuantityMode QuantityMode { get; }
@@ -127,7 +131,8 @@ namespace OpenEmpires
             CommanderProductionQuantityMode quantityMode = CommanderProductionQuantityMode.TargetTotal,
             int? producerOrdinal = null,
             CommanderFutureUnitAction? futureAction = null,
-            ResourceSourceKind? sourceKind = null)
+            ResourceSourceKind? sourceKind = null,
+            bool allMatchingUnits = false)
         {
             Type = type;
             UnitType = unitType;
@@ -141,6 +146,7 @@ namespace OpenEmpires
             ClearGapTiles = clearGapTiles;
             AgeTarget = ageTarget;
             UnitSelector = unitSelector;
+            AllMatchingUnits = allMatchingUnits;
             LocationSelector = locationSelector;
             TargetSelector = targetSelector;
             QuantityMode = quantityMode;
@@ -168,17 +174,19 @@ namespace OpenEmpires
         public IReadOnlyList<CommanderSemanticNode> Nodes { get; }
         public string SafeExplanation { get; }
         public CommanderWorkerAllocationDraft PendingDraft { get; }
+        public CommanderRequestGraphDraft PendingGraphDraft { get; }
         public CommanderDynamicPlan DynamicPlan { get; }
 
         private CommanderSemanticResult(bool isValid, CommanderSemanticOutcome outcome,
             IReadOnlyList<CommanderSemanticNode> nodes, string safeExplanation, CommanderWorkerAllocationDraft pendingDraft = null,
-            CommanderDynamicPlan dynamicPlan = null)
+            CommanderDynamicPlan dynamicPlan = null, CommanderRequestGraphDraft pendingGraphDraft = null)
         {
             IsValid = isValid;
             Outcome = outcome;
             Nodes = nodes ?? Array.Empty<CommanderSemanticNode>();
             SafeExplanation = safeExplanation ?? string.Empty;
             PendingDraft = pendingDraft;
+            PendingGraphDraft = pendingGraphDraft;
             DynamicPlan = dynamicPlan;
         }
     }
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4G/CommanderCapability.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4G/CommanderCapability.cs
index d3eee0e..06b810a 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4G/CommanderCapability.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4G/CommanderCapability.cs	
@@ -13,7 +13,10 @@ namespace OpenEmpires
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
@@ -30,8 +33,10 @@ namespace OpenEmpires
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
@@ -41,6 +46,7 @@ namespace OpenEmpires
             Kind = kind;
             Count = count;
             UnitType = unitType;
+            AllMatching = allMatching;
         }
     }
 
@@ -82,7 +88,9 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4G/CommanderCapabilityExecutor.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4G/CommanderCapabilityExecutor.cs
index e4ad574..bf04325 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4G/CommanderCapabilityExecutor.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4G/CommanderCapabilityExecutor.cs	
@@ -1,6 +1,7 @@
 using System;
 using System.Collections.Generic;
 using System.Linq;
+using UnityEngine;
 
 namespace OpenEmpires
 {
@@ -114,8 +115,14 @@ namespace OpenEmpires
                     reason = "Resolved a bounded owned-unit movement action.";
                     return true;
                 case CommanderCapabilityActionType.PatrolArea:
+                    if (!TryResolvePatrolPoint(intent.PlayerId, intent.LocationSelector, resource,
+                        unitIds, position, out position))
+                    {
+                        reason = "No known walkable patrol point with a complete route for every exact selected unit exists near the requested anchor.";
+                        return false;
+                    }
                     command = new PatrolCommand(intent.PlayerId, unitIds, position);
-                    reason = "Resolved a point patrol route from each selected unit's starting position to a visible game-side anchor.";
+                    reason = "Resolved a point patrol route from each selected unit's starting position to a known walkable point beside the selected game-side anchor.";
                     return true;
                 case CommanderCapabilityActionType.SetRallyPoint:
                     return TryRally(intent.PlayerId, intent.StructureType, position, resource,
@@ -143,6 +150,100 @@ namespace OpenEmpires
             }
         }
 
+        // A semantic anchor may be the occupied center of a TC or resource. Native patrol
+        // needs a point it can reach in both directions; search only the anchor footprint
+        // plus two tiles, in deterministic nearest-first order. Never substitute another anchor.
+        private bool TryResolvePatrolPoint(int playerId, CommanderLocationSelector selector,
+            ResourceNodeData resource, int[] unitIds, FixedVector3 anchor, out FixedVector3 point)
+        {
+            point = default;
+            if (unitIds == null || unitIds.Length == 0) return false;
+            Vector2Int anchorTile = simulation.MapData.WorldToTile(anchor);
+            int left = anchorTile.x, bottom = anchorTile.y, right = left + 1, top = bottom + 1;
+            if (selector.Kind == CommanderLocationSelectorKind.PlayerBase)
+            {
+                BuildingData tc = simulation.BuildingRegistry.GetAllBuildings()
+                    .Where(b => b != null && b.PlayerId == playerId && !b.IsDestroyed
+                        && simulation.GetEffectiveBuildingType(b) == BuildingType.TownCenter)
+                    .OrderBy(b => b.Id).FirstOrDefault();
+                if (tc == null || simulation.MapData.WorldToTile(tc.SimPosition) != anchorTile) return false;
+                left = tc.OriginTileX; bottom = tc.OriginTileZ;
+                right = left + tc.TileFootprintWidth; top = bottom + tc.TileFootprintHeight;
+            }
+            else if (resource != null)
+            {
+                left = resource.TileX; bottom = resource.TileZ;
+                right = left + resource.FootprintWidth; top = bottom + resource.FootprintHeight;
+            }
+            var candidates = new List<Vector2Int>();
+            for (int x = left - 2; x < right + 2; x++)
+                for (int z = bottom - 2; z < top + 2; z++)
+                {
+                    if (x >= left && x < right && z >= bottom && z < top) continue;
+                    candidates.Add(new Vector2Int(x, z));
+                }
+            foreach (Vector2Int tile in candidates.OrderBy(candidate =>
+                (candidate.x - anchorTile.x) * (candidate.x - anchorTile.x)
+                + (candidate.y - anchorTile.y) * (candidate.y - anchorTile.y))
+                .ThenBy(candidate => candidate.x).ThenBy(candidate => candidate.y))
+            {
+                if (!simulation.MapData.IsWalkable(tile.x, tile.y)
+                    || !simulation.MapData.IsWalkable(tile.x, tile.y, playerId, simulation.BuildingRegistry)
+                    || !IsKnownPatrolTile(playerId, tile)) continue;
+                bool allReachable = true;
+                foreach (int id in unitIds)
+                {
+                    UnitData unit = simulation.UnitRegistry.GetUnit(id);
+                    if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
+                        || unit.State == UnitState.Dead) { allReachable = false; break; }
+                    Vector2Int start = simulation.MapData.WorldToTile(unit.SimPosition);
+                    if (start == tile || !simulation.MapData.IsWalkable(start.x, start.y, playerId, simulation.BuildingRegistry)
+                        || !GridPathfinder.TryFindCompletePath(simulation.MapData, start, tile,
+                            out List<Vector2Int> outward, playerId, simulation.BuildingRegistry)
+                        || !IsKnownPatrolPath(playerId, start, outward)
+                        || !GridPathfinder.TryFindCompletePath(simulation.MapData, tile, start,
+                            out List<Vector2Int> returning, playerId, simulation.BuildingRegistry)
+                        || !IsKnownPatrolPath(playerId, tile, returning))
+                    { allReachable = false; break; }
+                }
+                if (!allReachable) continue;
+                point = simulation.MapData.TileToWorldFixed(tile.x, tile.y);
+                return true;
+            }
+            return false;
+        }
+
+        private bool IsKnownPatrolPath(int playerId, Vector2Int start, List<Vector2Int> path)
+        {
+            Vector2Int from = start;
+            foreach (Vector2Int to in path)
+            {
+                int x = from.x, z = from.y;
+                int dx = Mathf.Abs(to.x - x), dz = Mathf.Abs(to.y - z);
+                int sx = x < to.x ? 1 : -1, sz = z < to.y ? 1 : -1;
+                int error = dx - dz;
+                while (true)
+                {
+                    if (!IsKnownPatrolTile(playerId, new Vector2Int(x, z))) return false;
+                    if (x == to.x && z == to.y) break;
+                    int twice = 2 * error;
+                    bool stepX = twice > -dz, stepZ = twice < dx;
+                    if (stepX && stepZ && (!IsKnownPatrolTile(playerId, new Vector2Int(x + sx, z))
+                        || !IsKnownPatrolTile(playerId, new Vector2Int(x, z + sz)))) return false;
+                    if (stepX) { error -= dz; x += sx; }
+                    if (stepZ) { error += dx; z += sz; }
+                }
+                from = to;
+            }
+            return IsKnownPatrolTile(playerId, start);
+        }
+
+        private bool IsKnownPatrolTile(int playerId, Vector2Int tile)
+        {
+            TileVisibility visibility = simulation.FogOfWar.GetVisibility(playerId, tile.x, tile.y);
+            return visibility == TileVisibility.Visible || visibility == TileVisibility.Explored;
+        }
+
         private bool TryResolveUnits(int playerId, CommanderUnitSelector selector,
             IReadOnlyList<int> boundIds, out int[] ids)
         {
@@ -187,7 +288,7 @@ namespace OpenEmpires
             return true;
         }
 
-        private bool MatchesSelector(UnitData unit, CommanderUnitSelector selector)
+        internal bool MatchesSelector(UnitData unit, CommanderUnitSelector selector)
         {
             switch (selector.Kind)
             {
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderActionPlanCandidate.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderActionPlanCandidate.cs
index a1811e6..894c3be 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderActionPlanCandidate.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderActionPlanCandidate.cs	
@@ -153,7 +153,10 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderBoundLocation.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderBoundLocation.cs
index 615ac04..a1993c6 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderBoundLocation.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderBoundLocation.cs	
@@ -21,16 +21,18 @@ namespace OpenEmpires
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
@@ -52,6 +54,7 @@ namespace OpenEmpires
             this.sourceKind = sourceKind;
             this.worked = worked;
             this.futureSource = futureSource;
+            this.resourceSideIds = resourceSideIds;
             Relation = relation;
             ClearGapTiles = gap;
         }
@@ -82,6 +85,70 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderChatUI.ActionPlans.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderChatUI.ActionPlans.cs
index 67b6879..8f6e266 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderChatUI.ActionPlans.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderChatUI.ActionPlans.cs	
@@ -37,6 +37,7 @@ namespace OpenEmpires
             if (cancel)
             {
                 ClearActionPlanPreview();
+                SetLatestRequestState("Cancelled", "Action plan dismissed; nothing started.");
                 AppendLine("Commander", "Action plan dismissed; nothing started.");
                 return true;
             }
@@ -49,17 +50,21 @@ namespace OpenEmpires
                 if (graph == null)
                 {
                     candidate.Cancel();
+                    SetLatestRequestState("Failed", "Action plan changed or expired; nothing started.");
                     AppendLine("Commander", "Action plan is stale or changed; nothing started. Request a fresh preview.");
                     return true;
                 }
                 var submission = semanticDispatcher.SubmitSemanticGraph(graph);
                 if (submission?.CreatedGoal == true) RecordAcceptedSemanticNodes(candidate.Interpretation.Nodes);
                 TrackCurrentRequest(submission);
+                SetLatestRequestState(submission?.CreatedGoal == true ? "Accepted" : "Failed",
+                    submission?.Response ?? "Action plan could not be admitted safely.");
                 AppendLine("Commander", submission?.Response ?? "Action plan could not be admitted safely.");
             }
             catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
             {
                 candidate.Cancel();
+                SetLatestRequestState("Failed", "Action plan admission did not finish safely.");
                 AppendLine("Commander", "Action plan admission did not finish safely. Check its current goal status before retrying.");
             }
             return true;
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicCompiler.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicCompiler.cs
index e9276fb..fee0919 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicCompiler.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicCompiler.cs	
@@ -146,7 +146,8 @@ namespace OpenEmpires
                             || !TryWorkerSelection(workers, byId, simulation.Config.MaxPopulation,
                                 out _, out _, out _))) return false;
                     if (node.Inputs.TryGetValue("location", out string location)
-                        && (!byId.TryGetValue(location, out var site) || !TryLocation(site, byId)))
+                        && (!byId.TryGetValue(location, out var site) || !TryLocation(site, byId)
+                            || site.Parameters.ContainsKey("resource")))
                         return false;
                     intent = new BuildStructureIntent(playerId, building.BuildingType,
                         node.Parameter<int>("count"), constraints);
@@ -162,6 +163,13 @@ namespace OpenEmpires
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
@@ -172,11 +180,28 @@ namespace OpenEmpires
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
@@ -290,6 +315,10 @@ namespace OpenEmpires
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
@@ -320,8 +349,9 @@ namespace OpenEmpires
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
@@ -332,6 +362,7 @@ namespace OpenEmpires
                     || current.ToString() != (string)resourceName) return false;
                 currentResource = current;
             }
+            if (all && state == CommanderWorkerState.Any) return false;
             return true;
         }
 
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlan.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlan.cs
index 58db8e6..22e418f 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlan.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlan.cs	
@@ -7,7 +7,7 @@ namespace OpenEmpires
     public enum CommanderDynamicMechanic
     {
         SelectWorkers, PartitionWorkers, SelectUnits, SelectStructures,
-        SelectResources, ResolveLocation, Build, AllocateWorkers, Produce
+        SelectResources, ResolveLocation, Build, AllocateWorkers, MoveWorkers, Produce
     }
 
     public enum CommanderDynamicResultKind
@@ -18,13 +18,13 @@ namespace OpenEmpires
 
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
 
@@ -78,7 +78,8 @@ namespace OpenEmpires
             {
                 new CommanderDynamicPrimitive("select-workers", CommanderDynamicMechanic.SelectWorkers,
                     CommanderDynamicResultKind.WorkerSet, CommanderDynamicEffectClass.Selection,
-                    new[] { P("count", CommanderDynamicFieldKind.Count),
+                    new[] { P("count", CommanderDynamicFieldKind.Count, false),
+                        P("countMode", CommanderDynamicFieldKind.WorkerCountMode, false),
                         P("state", CommanderDynamicFieldKind.WorkerState, false),
                         P("currentResource", CommanderDynamicFieldKind.Resource, false) },
                     Array.Empty<CommanderDynamicInput>()),
@@ -109,7 +110,9 @@ namespace OpenEmpires
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
@@ -122,7 +125,13 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlanParser.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlanParser.cs
index 213df11..3863b47 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlanParser.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlanParser.cs	
@@ -159,6 +159,9 @@ namespace OpenEmpires
                 case CommanderDynamicFieldKind.WorkerState:
                     if (!NamedEnum<CommanderWorkerState>(s)) throw new JsonException();
                     break;
+                case CommanderDynamicFieldKind.WorkerCountMode:
+                    if (s != "AllMatching") throw new JsonException();
+                    break;
                 case CommanderDynamicFieldKind.ResourceMode:
                     if (s != "Visible" && s != "Worked") throw new JsonException();
                     break;
@@ -202,11 +205,26 @@ namespace OpenEmpires
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
@@ -260,7 +278,8 @@ namespace OpenEmpires
                     DynamicWorkerSpan(i, nodes, byId);
                 if (nodes[i].Inputs.TryGetValue("workers", out string source) &&
                     (nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.Build ||
-                     nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.AllocateWorkers))
+                     nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.AllocateWorkers ||
+                     nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.MoveWorkers))
                 {
                     var span = DynamicWorkerSpan(byId[source], nodes, byId);
                     workerUses.Add((i, span.root, span.start, span.end));
@@ -302,9 +321,12 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicLocations.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicLocations.cs
index 3d81dfa..63954a6 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicLocations.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicLocations.cs	
@@ -62,7 +62,12 @@ namespace OpenEmpires
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
@@ -107,6 +112,8 @@ namespace OpenEmpires
             }
 
             var buildBindings = new List<(BuildStructureGoal goal, CommanderBoundLocation location)>();
+            var allocationBindings = new List<(AllocateWorkersGoal goal, CommanderBoundLocation location)>();
+            var moveBindings = new List<(CommanderCapabilityGoal goal, CommanderBoundLocation location)>();
             var producerBindings = new List<(EnsureUnitCountGoal goal, IReadOnlyList<int> ids)>();
             foreach (var node in program.Nodes)
             {
@@ -119,6 +126,25 @@ namespace OpenEmpires
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
@@ -135,6 +161,8 @@ namespace OpenEmpires
 
             foreach (BuildStructureGoal source in futureAnchors) source.HasResultConsumer = true;
             foreach (var binding in buildBindings) binding.goal.DynamicLocation = binding.location;
+            foreach (var binding in allocationBindings) binding.goal.DynamicResourceLocation = binding.location;
+            foreach (var binding in moveBindings) binding.goal.DynamicMoveLocation = binding.location;
             foreach (var binding in producerBindings) binding.goal.BoundProducerBuildingIds = binding.ids;
         }
 
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicWorkers.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicWorkers.cs
index 1115eb4..d2e750c 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicWorkers.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicWorkers.cs	
@@ -21,7 +21,8 @@ namespace OpenEmpires
             {
                 if (selections.TryGetValue(id, out var captured)) return captured;
                 var node = nodes[id];
-                int count = node.Parameter<int>("count");
+                bool allMatching = node.Parameters.ContainsKey("countMode");
+                int count = allMatching ? 0 : node.Parameter<int>("count");
                 int[] ids;
                 if (node.Primitive.Mechanic == CommanderDynamicMechanic.PartitionWorkers)
                 {
@@ -54,10 +55,16 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderPlanPreview.Descriptions.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderPlanPreview.Descriptions.cs
index 4d71220..7b5a91e 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderPlanPreview.Descriptions.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderPlanPreview.Descriptions.cs	
@@ -63,7 +63,7 @@ namespace OpenEmpires
         }
         private static string DescribeAction(CapabilityActionIntent action,string resultBuildingName)
         {
-            string actors=action.UnitSelector.Count+" owned "+(action.UnitSelector.Kind==CommanderUnitSelectorKind.UnitType
+            string actors=(action.UnitSelector.AllMatching?"all currently eligible owned ":action.UnitSelector.Count+" owned ")+(action.UnitSelector.Kind==CommanderUnitSelectorKind.UnitType
                 ?CommanderIntentCatalog.GetUnitDisplayName(action.UnitSelector.UnitType):UnitKind(action.UnitSelector.Kind.ToString()));
             string location=Location(action.LocationSelector);
             string target=action.TargetSelector.HasValue
@@ -76,6 +76,7 @@ namespace OpenEmpires
             {
                 case CommanderCapabilityActionType.MoveUnits:effect="Move "+actors+" to "+location;break;
                 case CommanderCapabilityActionType.ScoutArea:effect="Scout by moving "+actors+" to "+location;break;
+                case CommanderCapabilityActionType.DiscoverEnemy:effect="Discover "+target+" with one owned Scout over a finite route of known frontier waypoints; complete only when the requested enemy becomes visibly observed; stop on route exhaustion or scout loss";break;
                 case CommanderCapabilityActionType.PatrolArea:effect="Patrol with "+actors+"; point patrol from each starting position to "+location;break;
                 case CommanderCapabilityActionType.DefendArea:effect="Move "+actors+" to "+location+" to help defend";break;
                 case CommanderCapabilityActionType.RetreatUnits:effect="Retreat "+actors+" to "+location;break;
@@ -83,9 +84,11 @@ namespace OpenEmpires
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
@@ -140,7 +143,9 @@ namespace OpenEmpires
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
@@ -154,9 +159,14 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs
index 5e60fc2..a1bef7c 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs	
@@ -5,6 +5,8 @@ namespace OpenEmpires
 {
     internal sealed partial class CommanderPlanner
     {
+        private const int MaximumDynamicSiteSearchNodes = 8192;
+
         // Called only after ordinary age/cost/prerequisite checks in PlanBuilding.
         // The anchor is a frozen game-side identity, never a provider tile or runtime ID.
         private CommanderPlan PlanDynamicSemanticBuilding(BuildStructureGoal goal,
@@ -35,9 +37,28 @@ namespace OpenEmpires
                 simulation.MapData.Width, simulation.MapData.Height,
                 location.Relation, location.ClearGapTiles);
             int border = type == BuildingType.Farm ? 0 : 1;
-            for (int i = 0; i < candidates.Count; i++)
+            // Before placing the first building, require enough mutually legal sites
+            // for the entire repeated request. Later external occupancy/authority
+            // changes may still block honestly; a known impossible prefix is never issued.
+            IReadOnlyList<Vector2Int> planned = null;
+            bool exhausted = false;
+            if (goal.Count > 1 && goal.DynamicPlannedSiteSet == null
+                && !TrySelectFullDynamicSiteSet(goal, type, currentTick, candidates,
+                    width, height, border, out planned, out exhausted))
             {
-                Vector2Int tile = candidates[i];
+                goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
+                return new CommanderPlan(CommanderGoalStatus.Blocked,
+                    (exhausted ? "The bounded compatible-site search budget was exhausted; "
+                        : $"Only a partial set of distinct legal {type} sites is available near the exact anchor; ")
+                    + $"none of the requested {goal.Count} placements was started.", owned, queued);
+            }
+            if (goal.Count > 1 && goal.DynamicPlannedSiteSet == null)
+                goal.DynamicPlannedSiteSet = planned;
+            IReadOnlyList<Vector2Int> placementCandidates = goal.DynamicPlannedSiteSet != null
+                ? new[] { goal.DynamicPlannedSiteSet[goal.AttributedBuildingIds.Count] } : candidates;
+            for (int i = 0; i < placementCandidates.Count; i++)
+            {
+                Vector2Int tile = placementCandidates[i];
                 if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y, width, height,
                     border, type)) continue;
                 UnitData builder = FindReachableSemanticBuilder(goal, tile, width,
@@ -52,8 +73,62 @@ namespace OpenEmpires
             }
             goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
             return new CommanderPlan(CommanderGoalStatus.Blocked,
-                $"No visible, buildable, reachable location satisfies the exact {type} "
-                + "anchor and bounded relation.", owned, queued);
+                goal.DynamicPlannedSiteSet != null
+                    ? "A preflight-selected exact site became unavailable; no alternate placement was started."
+                    : $"No visible, buildable, reachable location satisfies the exact {type} "
+                        + "anchor and bounded relation.", owned, queued);
+        }
+
+        private bool TrySelectFullDynamicSiteSet(BuildStructureGoal goal, BuildingType type,
+            int currentTick, IReadOnlyList<Vector2Int> candidates,
+            int width, int height, int border, out IReadOnlyList<Vector2Int> planned,
+            out bool exhausted)
+        {
+            planned = null;
+            exhausted = false;
+            bool budgetExceeded = false;
+            var legal = new List<Vector2Int>();
+            for (int i = 0; i < candidates.Count; i++)
+            {
+                Vector2Int tile = candidates[i];
+                if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y,
+                    width, height, border, type)) continue;
+                if (FindReachableSemanticBuilder(goal, tile, width, height, currentTick) != null)
+                    legal.Add(tile);
+            }
+            var selected = new List<Vector2Int>(goal.Count);
+            int checkedNodes = 0;
+            bool Search(int start)
+            {
+                if (selected.Count == goal.Count) return true;
+                if (legal.Count - start < goal.Count - selected.Count) return false;
+                for (int i = start; i < legal.Count; i++)
+                {
+                    if (++checkedNodes > MaximumDynamicSiteSearchNodes)
+                    { budgetExceeded = true; return false; }
+                    Vector2Int tile = legal[i];
+                    bool conflicts = false;
+                    for (int j = 0; j < selected.Count; j++)
+                    {
+                        Vector2Int prior = selected[j];
+                        if (tile.x - border < prior.x + width + border
+                            && tile.x + width + border > prior.x - border
+                            && tile.y - border < prior.y + height + border
+                            && tile.y + height + border > prior.y - border)
+                        { conflicts = true; break; }
+                    }
+                    if (conflicts) continue;
+                    selected.Add(tile);
+                    if (Search(i + 1)) return true;
+                    selected.RemoveAt(selected.Count - 1);
+                    if (budgetExceeded) return false;
+                }
+                return false;
+            }
+            if (!Search(0))
+            { exhausted = budgetExceeded; return false; }
+            planned = selected.ToArray();
+            return true;
         }
     }
 }
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderRequestTicket.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderRequestTicket.cs
index f815a0c..113b38d 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderRequestTicket.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderRequestTicket.cs	
@@ -11,7 +11,7 @@ namespace OpenEmpires
         internal GameSimulation Runtime { get; }
         internal string OriginalInput { get; }
         internal int Generation { get; }
-        private readonly Func<bool> current;
+        private Func<bool> current;
         private bool claimed;
 
         internal CommanderRequestTicket(long id, CommanderGoalManager owner, string input,
@@ -28,6 +28,21 @@ namespace OpenEmpires
             catch (Exception) { return false; }
         }
 
+        // A retained local clarification is a new UI turn of the same player
+        // request. Its old turn-revision predicate must not authorize admission,
+        // but the ticket's identity and original utterance must remain stable.
+        internal bool BindLocalContinuation(CommanderGoalManager owner, int generation,
+            Func<bool> currentTurn)
+        {
+            if (claimed || !ReferenceEquals(owner, Owner) || generation != Generation
+                || Owner.IsDisposed || !ReferenceEquals(Runtime, owner.Simulation)
+                || currentTurn == null) return false;
+            try { if (!currentTurn()) return false; }
+            catch (Exception) { return false; }
+            current = currentTurn;
+            return true;
+        }
+
         internal bool Claim(CommanderGoalManager owner, string input, int generation)
         {
             if (claimed || !ReferenceEquals(owner, Owner) || generation != Generation
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderScopeEquivalence.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderScopeEquivalence.cs
index 49cd37f..b2ddbf1 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderScopeEquivalence.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderScopeEquivalence.cs	
@@ -34,7 +34,9 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderChatUI.TaskBoard.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderChatUI.TaskBoard.cs
index 56e3c2e..f5f0b9a 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderChatUI.TaskBoard.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderChatUI.TaskBoard.cs	
@@ -12,6 +12,7 @@ namespace OpenEmpires
         private RectTransform taskBoardContent;
         private string taskBoardFingerprint;
         private int currentRequestGoalId;
+        private bool taskHistoryExpanded;
 
         private void TrackCurrentRequest(CommanderIntentSubmission submission)
         {
@@ -25,8 +26,8 @@ namespace OpenEmpires
             taskBadgeText.richText = false;
             PanelElement(taskBadgeText.gameObject, 20);
             var board = UIObject("TaskBoard", commanderPanel);
-            board.AddComponent<Image>().color = new Color(.045f, .065f, .09f, .97f);
-            PanelElement(board, 90);
+            board.AddComponent<Image>().color = new Color(.09f, .105f, .12f, 1f);
+            PanelElement(board, 140, true);
             taskBoardScroll = board.AddComponent<ScrollRect>();
             taskBoardScroll.horizontal = false;
             taskBoardScroll.vertical = true;
@@ -57,6 +58,7 @@ namespace OpenEmpires
             transcriptLayout.minHeight = 70;
             transcriptLayout.preferredHeight = 70;
             taskBoardFingerprint = null;
+            taskHistoryExpanded = false;
             RefreshTaskBoard();
         }
 
@@ -68,7 +70,7 @@ namespace OpenEmpires
             RefreshCurrentRequestStatus(snapshot);
             taskBadgeText.text = snapshot.ActiveCount + " active task" + (snapshot.ActiveCount == 1 ? "" : "s");
             taskBadgeText.gameObject.SetActive(snapshot.ActiveCount > 0);
-            string fingerprint = Fingerprint(snapshot);
+            string fingerprint = Fingerprint(snapshot) + "|history=" + taskHistoryExpanded;
             if (fingerprint == taskBoardFingerprint) return;
             taskBoardFingerprint = fingerprint;
             for (int i = taskBoardContent.childCount - 1; i >= 0; i--)
@@ -83,7 +85,17 @@ namespace OpenEmpires
                 empty.richText = false; PanelElement(empty.gameObject, 24);
                 return;
             }
-            foreach (var card in snapshot.Cards) RenderTaskCard(card);
+            for (int i = 0; i < snapshot.ActiveCount; i++) RenderTaskCard(snapshot.Cards[i]);
+            if (snapshot.Cards.Count > snapshot.ActiveCount)
+            {
+                var history = CompactButton(taskBoardContent, "HistoryToggle",
+                    (taskHistoryExpanded ? "▾ Hide" : "▸ Show") + " completed history ("
+                        + (snapshot.Cards.Count - snapshot.ActiveCount) + ")", () =>
+                    { taskHistoryExpanded = !taskHistoryExpanded; taskBoardFingerprint = null; RefreshTaskBoard(); });
+                PanelElement(history.gameObject, 32);
+                if (taskHistoryExpanded)
+                    for (int i = snapshot.ActiveCount; i < snapshot.Cards.Count; i++) RenderTaskCard(snapshot.Cards[i]);
+            }
         }
 
         private void RefreshCurrentRequestStatus(CommanderTaskBoardSnapshot snapshot)
@@ -105,6 +117,9 @@ namespace OpenEmpires
                 string detail = card.Status == CommanderTaskStatus.Blocked || card.Status == CommanderTaskStatus.Failed
                     ? card.Blocker : card.Status == CommanderTaskStatus.Completed
                         ? card.Progress : card.CurrentStep;
+                latestRequestState = card.Status == CommanderTaskStatus.Blocked
+                    || card.Status == CommanderTaskStatus.Waiting ? "Waiting/Blocked" : card.Status.ToString();
+                latestRequestDetail = detail ?? string.Empty;
                 string message = card.Status + (string.IsNullOrWhiteSpace(detail) ? string.Empty : " — " + detail);
                 commanderStatusText.text = message.Length > 120 ? message.Substring(0, 117) + "..." : message;
                 commanderStatusText.color = card.Status == CommanderTaskStatus.Blocked
@@ -133,30 +148,18 @@ namespace OpenEmpires
         private void RenderTaskCard(CommanderTaskCardSnapshot card)
         {
             var root = UIObject("TaskCard", taskBoardContent);
-            root.AddComponent<Image>().color = new Color(.075f, .105f, .15f, .98f);
+            root.AddComponent<Image>().color = new Color(.13f, .15f, .17f, 1f);
             var layout = root.AddComponent<VerticalLayoutGroup>();
-            layout.padding = new RectOffset(6, 6, 6, 6);
-            layout.spacing = 3;
+            layout.padding = new RectOffset(8, 8, 8, 8);
+            layout.spacing = 8;
             layout.childControlWidth = layout.childControlHeight = true;
             layout.childForceExpandWidth = true;
             layout.childForceExpandHeight = false;
             string attention = card.Status == CommanderTaskStatus.Blocked || card.Status == CommanderTaskStatus.Failed
                 ? card.Blocker : string.Empty;
             var status = Text("Status", root.transform, card.Status + " — "
-                + (string.IsNullOrWhiteSpace(attention) ? card.Progress : attention), 12, TextAlignmentOptions.Left);
-            status.richText = false; PanelElement(status.gameObject, 32);
-            var title = Text("Objective", root.transform, card.Objective, 13, TextAlignmentOptions.Left);
-            title.richText = false; PanelElement(title.gameObject, 34);
-            var stepText = Text("CurrentStep", root.transform,
-                "Current: " + card.CurrentStep + (string.IsNullOrEmpty(card.Blocker) ? "" : " — " + card.Blocker),
-                11, TextAlignmentOptions.Left);
-            stepText.richText = false; PanelElement(stepText.gameObject, 32);
-            foreach (var step in card.Steps)
-            {
-                var detail = Text("Step", root.transform, "• " + step.Label + " — " + step.Status
-                    + (string.IsNullOrEmpty(step.Progress) ? "" : " — " + step.Progress), 11, TextAlignmentOptions.Left);
-                detail.richText = false; PanelElement(detail.gameObject, 34);
-            }
+                + (string.IsNullOrWhiteSpace(attention) ? card.Progress : attention), 14, TextAlignmentOptions.Left);
+            status.richText = false; PanelElement(status.gameObject, 40);
             if (card.CanCancel)
             {
                 var cancel = CompactButton(root.transform, "CancelTask", "Cancel task", () =>
@@ -168,8 +171,33 @@ namespace OpenEmpires
                         RefreshTaskBoard();
                     }
                 });
-                PanelElement(cancel.gameObject, 27);
+                PanelElement(cancel.gameObject, 28);
             }
+            var title = Text("Objective", root.transform, card.Objective, 15, TextAlignmentOptions.Left);
+            title.richText = false; PanelElement(title.gameObject, 44);
+            var stepText = Text("CurrentStep", root.transform,
+                "Current: " + card.CurrentStep + (string.IsNullOrEmpty(card.Blocker) ? "" : " — " + card.Blocker),
+                14, TextAlignmentOptions.Left);
+            stepText.richText = false; PanelElement(stepText.gameObject, 40);
+            var details = UIObject("Details", root.transform);
+            var detailsLayout = details.AddComponent<VerticalLayoutGroup>();
+            detailsLayout.spacing = 4;
+            detailsLayout.childControlWidth = detailsLayout.childControlHeight = true;
+            detailsLayout.childForceExpandWidth = true; detailsLayout.childForceExpandHeight = false;
+            foreach (var step in card.Steps)
+            {
+                var detail = Text("Step", details.transform, "• " + step.Label + " — " + step.Status
+                    + (string.IsNullOrEmpty(step.Progress) ? "" : " — " + step.Progress), 13, TextAlignmentOptions.Left);
+                detail.richText = false; PanelElement(detail.gameObject, 42);
+            }
+            details.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
+            var toggle = CompactButton(root.transform,"DetailsToggle","Show details",()=>
+            {
+                details.SetActive(!details.activeSelf);
+                root.GetComponent<RectTransform>().ForceUpdateRectTransforms();
+            });
+            PanelElement(toggle.gameObject, 28);
+            details.SetActive(false);
             root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
         }
     }
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs
index c7d1678..b34bfd0 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs	
@@ -10,52 +10,85 @@ namespace OpenEmpires
     // Retrieval only: lexical similarity never chooses an entity or authorizes an effect.
     internal static class CommanderKnowledgeRetrieval
     {
-        internal const int MaximumEntries = 8;
-        internal static string Retrieve(GameKnowledgeCatalog catalog, string input, Civilization civilization)
+        // Required references are independent of optional lexical suggestions. The finite
+        // ceiling is a refusal boundary, never permission to omit the seventeenth reference.
+        internal const int MaximumRequiredEntries = 16;
+        private const int MaximumOptionalInterpretations = 2;
+        private const int MaximumOptionalEntries = 2;
+        internal static string Retrieve(GameKnowledgeCatalog catalog, string input, Civilization civilization,
+            string boundedPending = null)
         {
-            var unitScores = new Dictionary<string, int>(StringComparer.Ordinal);
-            var buildingScores = new Dictionary<string, int>(StringComparer.Ordinal);
+            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
+            var required = new HashSet<string>(StringComparer.Ordinal);
+            var matchedPhrases = new HashSet<string>(StringComparer.Ordinal);
             var resolutions = new List<JObject>();
-            string text = (input ?? string.Empty);
-            if (text.Length > 1024) text = text.Substring(0, 1024);
-            var words = Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+")
-                .Cast<Match>().Select(x => x.Value).Take(96).ToArray();
-            string searchable = " " + string.Join(" ", words) + " ";
-            foreach (var unit in catalog.Units)
-                ScoreAliases(unitScores, unit.StableId, unit.DisplayName, unit.Aliases, searchable);
-            foreach (var building in catalog.Buildings)
-                ScoreAliases(buildingScores, building.StableId, building.DisplayName, building.Aliases, searchable);
-
-            var phrases = new HashSet<string>(StringComparer.Ordinal);
-            for (int i = 0; i < words.Length && phrases.Count < 128; i++)
-                for (int length = 1; length <= 4 && i + length <= words.Length && phrases.Count < 128; length++)
+            var optionalInterpretations = new List<JObject>();
+            var optional = new HashSet<string>(StringComparer.Ordinal);
+            foreach (string source in new[] { input ?? string.Empty, boundedPending ?? string.Empty })
+            {
+                // Both sources already have independent caller bounds (1024 player, 4096 pending).
+                // Scan the complete bounded strings; prefix/word/phrase cuts lose tail facts.
+                string searchable = " " + string.Join(" ", Regex.Matches(source.ToLowerInvariant(), @"[\p{L}\p{N}]+")
+                    .Cast<Match>().Select(x => x.Value)) + " ";
+                foreach (var unit in catalog.Units)
+                    MatchAliases(required, matchedPhrases, unit.StableId, unit.DisplayName, unit.Aliases, searchable);
+                foreach (var building in catalog.Buildings)
+                    MatchAliases(required, matchedPhrases, building.StableId, building.DisplayName, building.Aliases, searchable);
+                // Role words such as "cavalry" are resolver-defined ambiguous names, not
+                // a new local name table. Deduplication bounds resolver work for long replies.
+                foreach (string word in Regex.Matches(source.ToLowerInvariant(), @"[\p{L}\p{N}]+")
+                    .Cast<Match>().Select(x => x.Value).Distinct(StringComparer.Ordinal))
+                    if (word.Length >= 3) matchedPhrases.Add(word);
+            }
+            foreach (string phrase in matchedPhrases.OrderBy(x => x, StringComparer.Ordinal))
+            {
+                var unit = CommanderContentNameResolver.ResolveUnit(phrase, catalog, civilization);
+                var building = CommanderContentNameResolver.ResolveBuilding(phrase, catalog, civilization);
+                if (unit.Status == CommanderContentResolutionStatus.Ambiguous
+                    || building.Status == CommanderContentResolutionStatus.Ambiguous)
+                {
+                    foreach (var candidate in unit.Suggestions) required.Add(candidate.StableId);
+                    foreach (var candidate in building.Suggestions) required.Add(candidate.StableId);
+                    resolutions.Add(new JObject { ["phrase"] = phrase, ["interpretationOnly"] = true,
+                        ["unitCandidates"] = new JArray(unit.Suggestions.Select(x => x.StableId)),
+                        ["buildingCandidates"] = new JArray(building.Suggestions.Select(x => x.StableId)),
+                        ["ambiguous"] = true });
+                    continue;
+                }
+                // Only a narrow, resolver-defined near-name can become an optional
+                // interpretation. Common short words never fill the catalog with fuzz.
+                if (phrase.Length < 7 || unit.Status != CommanderContentResolutionStatus.Unknown
+                    || building.Status != CommanderContentResolutionStatus.Unknown) continue;
+                var unitCandidates = unit.Suggestions.Take(MaximumOptionalEntries).Select(x => x.StableId).ToArray();
+                var buildingCandidates = building.Suggestions.Take(MaximumOptionalEntries).Select(x => x.StableId).ToArray();
+                if (unitCandidates.Length + buildingCandidates.Length == 0
+                    || unitCandidates.Length + buildingCandidates.Length > MaximumOptionalEntries) continue;
+                optionalInterpretations.Add(new JObject { ["phrase"] = phrase, ["interpretationOnly"] = true,
+                    ["unitCandidates"] = new JArray(unitCandidates),
+                    ["buildingCandidates"] = new JArray(buildingCandidates), ["ambiguous"] = false });
+            }
+            bool overflow = required.Count > MaximumRequiredEntries || resolutions.Count > 6;
+            int addedHints = 0;
+            if (!overflow)
+                foreach (JObject hint in optionalInterpretations)
                 {
-                    string phrase = string.Join(" ", words.Skip(i).Take(length));
-                    if (phrase.Length > 64 || !phrases.Add(phrase)) continue;
-                    var unit = CommanderContentNameResolver.ResolveUnit(phrase, catalog, civilization);
-                    var building = CommanderContentNameResolver.ResolveBuilding(phrase, catalog, civilization);
-                    if (unit.Match != null) Raise(unitScores, unit.Match.StableId, 20 + phrase.Length);
-                    foreach (var suggestion in unit.Suggestions) Raise(unitScores, suggestion.StableId, 3);
-                    if (building.Match != null) Raise(buildingScores, building.Match.StableId, 20 + phrase.Length);
-                    foreach (var suggestion in building.Suggestions) Raise(buildingScores, suggestion.StableId, 3);
-                    if (resolutions.Count < 6 && (unit.Suggestions.Count > 0 || building.Suggestions.Count > 0))
-                        resolutions.Add(new JObject { ["phrase"] = phrase,
-                            ["interpretationOnly"] = true,
-                            ["unitCandidates"] = new JArray(unit.Suggestions.Select(x => x.StableId)),
-                            ["buildingCandidates"] = new JArray(building.Suggestions.Select(x => x.StableId)),
-                            ["ambiguous"] = unit.Status == CommanderContentResolutionStatus.Ambiguous
-                                || building.Status == CommanderContentResolutionStatus.Ambiguous });
+                    if (resolutions.Count >= 6 || addedHints >= MaximumOptionalInterpretations) break;
+                    var candidates = ((JArray)hint["unitCandidates"]).Concat((JArray)hint["buildingCandidates"])
+                        .Select(x => (string)x).Where(id => !required.Contains(id)).Distinct(StringComparer.Ordinal).ToArray();
+                    if (optional.Count + candidates.Count(id => !optional.Contains(id)) > MaximumOptionalEntries) continue;
+                    foreach (string id in candidates) optional.Add(id);
+                    resolutions.Add(hint);
+                    addedHints++;
                 }
-            var chosen = unitScores.Select(x => (id: x.Key, score: x.Value, unit: true))
-                .Concat(buildingScores.Select(x => (id: x.Key, score: x.Value, unit: false)))
-                .OrderByDescending(x => x.score).ThenBy(x => x.id, StringComparer.Ordinal)
-                .Take(MaximumEntries).ToArray();
+            var requiredIds = overflow ? Array.Empty<string>() : required.OrderBy(x => x, StringComparer.Ordinal).ToArray();
+            var optionalIds = overflow ? Array.Empty<string>() : optional.OrderBy(x => x, StringComparer.Ordinal).ToArray();
+            var chosen = requiredIds.Concat(optionalIds).ToArray();
             var units = new JArray(); var buildings = new JArray();
-            foreach (var entry in chosen)
+            foreach (string id in chosen)
             {
-                if (entry.unit)
+                if (id.StartsWith("unit:", StringComparison.Ordinal))
                 {
-                    var data = catalog.FindUnit(entry.id);
+                    var data = catalog.FindUnit(id);
                     var resolution = CommanderContentNameResolver.ResolveUnit(data.StableId, catalog, civilization);
                     units.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                         ["age"] = data.RequiredAge, ["producer"] = data.ProductionBuildingType < 0
@@ -65,7 +98,7 @@ namespace OpenEmpires
                 }
                 else
                 {
-                    var data = catalog.FindBuilding(entry.id);
+                    var data = catalog.FindBuilding(id);
                     var resolution = CommanderContentNameResolver.ResolveBuilding(data.StableId, catalog, civilization);
                     buildings.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                         ["age"] = data.RequiredAge, ["cost"] = Cost(data.Cost),
@@ -76,23 +109,23 @@ namespace OpenEmpires
                 }
             }
             return new JObject { ["units"] = units, ["buildings"] = buildings,
-                ["technologies"] = new JArray(), ["nameInterpretations"] = new JArray(resolutions),
+                ["technologies"] = new JArray(), ["nameInterpretations"] = new JArray(overflow ? Enumerable.Empty<JObject>() : resolutions),
+                ["requiredCanonicalIds"] = new JArray(requiredIds), ["optionalCanonicalIds"] = new JArray(optionalIds),
+                ["requiredReferenceOverflow"] = overflow,
                 ["policy"] = "Knowledge is not execution authority. Show canonical interpretations; clarify ambiguous names. Availability and support are separate." }
                 .ToString(Formatting.None);
         }
 
         private static JObject Cost(KnowledgeCost value) => new JObject
             { ["food"] = value.Food, ["wood"] = value.Wood, ["gold"] = value.Gold, ["stone"] = value.Stone };
-        private static void Raise(Dictionary<string, int> scores, string id, int score)
-        { if (!scores.TryGetValue(id, out int old) || old < score) scores[id] = score; }
-        private static void ScoreAliases(Dictionary<string, int> scores, string id, string name,
+        private static void MatchAliases(HashSet<string> required, HashSet<string> matchedPhrases, string id, string name,
             IEnumerable<string> aliases, string searchable)
         {
             foreach (string label in new[] { name }.Concat(aliases))
             {
                 string normalized = Regex.Replace(label.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
                 if (normalized.Length > 0 && searchable.Contains(" " + normalized + " "))
-                    Raise(scores, id, 30 + normalized.Length);
+                { required.Add(id); matchedPhrases.Add(normalized); }
             }
         }
     }
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderTaskBoardProjection.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderTaskBoardProjection.cs
index 39dbc24..23f3fee 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderTaskBoardProjection.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderTaskBoardProjection.cs	
@@ -194,6 +194,7 @@ namespace OpenEmpires
 
         private static string GoalLabel(GameSimulation simulation, CommanderGoal goal)
         {
+            if (goal is CommanderCapabilityGoal capability) return CapabilityLabel(capability);
             if (goal is ReachAgeGoal age) return "Reach Age " + age.TargetAge;
             if (goal is AllocateWorkersGoal workers)
             {
@@ -219,6 +220,7 @@ namespace OpenEmpires
 
         private static string GoalProgress(GameSimulation simulation, CommanderGoal goal)
         {
+            if (goal is CommanderCapabilityGoal capability) return CapabilityProgress(simulation, capability);
             if (goal is ReachAgeGoal age) return "Age " + simulation.GetPlayerAge(goal.PlayerId) + " / " + age.TargetAge;
             if (goal is AllocateWorkersGoal workers)
             {
@@ -255,6 +257,84 @@ namespace OpenEmpires
             return goal.StatusReason ?? string.Empty;
         }
 
+        private static string CapabilityLabel(CommanderCapabilityGoal goal)
+        {
+            var action = goal.Action;
+            string subjects = action.UnitSelector.AllMatching ? "all matching villagers"
+                : action.UnitSelector.Count + " " + action.UnitSelector.Kind;
+            switch (action.ActionType)
+            {
+                case CommanderCapabilityActionType.MoveUnits:
+                    return "Move " + subjects + " to the validated destination";
+                case CommanderCapabilityActionType.GarrisonUnits:
+                    return "Garrison " + subjects + " in the exact owned "
+                        + (action.StructureType?.ToString() ?? "shelter");
+                case CommanderCapabilityActionType.UngarrisonBuilding:
+                    return "Ungarrison the exact owned " + (action.StructureType?.ToString() ?? "shelter");
+                case CommanderCapabilityActionType.DiscoverEnemy:
+                    return "Discover visible enemy " + (action.TargetSelector?.Kind == CommanderTargetSelectorKind.BuildingType
+                        ? action.TargetSelector?.StructureType?.ToString() : "unit");
+                default:
+                    return action.ActionType.ToString();
+            }
+        }
+
+        private static string CapabilityProgress(GameSimulation simulation, CommanderCapabilityGoal goal)
+        {
+            var action = goal.Action.ActionType;
+            if (action == CommanderCapabilityActionType.MoveUnits)
+            {
+                int count = goal.IssuedCommand is MoveCommand move && move.UnitIds != null
+                    ? move.UnitIds.Length : goal.FrozenWorkerIds?.Count ?? 0;
+                if (count == 0) return "Awaiting exact bound subjects; no placeholder count.";
+                if (!(goal.IssuedCommand is MoveCommand issued))
+                    return "Frozen subjects: " + count + "; native order pending.";
+                int receipts = 0, arrived = 0;
+                for (int i = 0; i < count; i++)
+                {
+                    if (goal.NativeMovementProcessed == null || i >= goal.NativeMovementProcessed.Length
+                        || !goal.NativeMovementProcessed[i]) continue;
+                    receipts++;
+                    if (goal.NativeMovementResults == null || i >= goal.NativeMovementResults.Length
+                        || goal.NativeMovementResults[i] == NativeMovePathResult.Partial
+                        || goal.NativeMovementResults[i] == NativeMovePathResult.NoPath
+                        || goal.NativeMovementEndpoints == null || i >= goal.NativeMovementEndpoints.Length) continue;
+                    var unit = simulation.UnitRegistry.GetUnit(issued.UnitIds[i]);
+                    if (unit == null || unit.State != UnitState.Idle || unit.HasPath) continue;
+                    long dx = (long)unit.SimPosition.x.Raw - goal.NativeMovementEndpoints[i].x.Raw;
+                    long dz = (long)unit.SimPosition.z.Raw - goal.NativeMovementEndpoints[i].z.Raw;
+                    long limit = Fixed32.FromInt(1).Raw;
+                    if (dx * dx + dz * dz <= limit * limit) arrived++;
+                }
+                return "Native arrivals: " + arrived + " / " + count + "; route receipts: " + receipts + " / " + count;
+            }
+            if (action == CommanderCapabilityActionType.GarrisonUnits)
+            {
+                if (goal.GarrisonCandidateIds == null) return "Awaiting exact eligible shelter snapshot.";
+                int housed = 0;
+                if (goal.GarrisonShelter != null)
+                    foreach (int id in goal.GarrisonIssuedIds ?? Array.Empty<int>())
+                        if (goal.GarrisonShelter.GarrisonedUnitIds.Contains(id)
+                            && simulation.UnitRegistry.GetGarrisonedUnit(id) != null) housed++;
+                return "Native shelter entries: " + housed + " / " + goal.GarrisonCandidateIds.Length
+                    + "; protected: " + goal.GarrisonProtectedCount
+                    + "; unaccommodated: " + goal.GarrisonUnaccommodatedCount;
+            }
+            if (action == CommanderCapabilityActionType.UngarrisonBuilding)
+            {
+                if (goal.UngarrisonInitialIds == null) return "Awaiting exact shelter occupant snapshot.";
+                int released = 0;
+                foreach (int id in goal.UngarrisonInitialIds)
+                    if (simulation.UnitRegistry.GetUnit(id) != null
+                        && (goal.GarrisonShelter == null || !goal.GarrisonShelter.GarrisonedUnitIds.Contains(id))) released++;
+                return "Native released: " + released + " / " + goal.UngarrisonInitialIds.Length;
+            }
+            if (action == CommanderCapabilityActionType.DiscoverEnemy)
+                return "Known-frontier moves: " + goal.DiscoveryMovesIssued + " / 8; candidates checked: "
+                    + goal.DiscoveryCandidatesChecked + ". " + (goal.StatusReason ?? string.Empty);
+            return goal.StatusReason ?? string.Empty;
+        }
+
         private static long Stockpile(PlayerResources resources, ResourceType type) => type switch
         {
             ResourceType.Food => resources.Food, ResourceType.Wood => resources.Wood,
diff --git a/Open Empires/Assets/Scripts/Commands/CommandBuffer.cs b/Open Empires/Assets/Scripts/Commands/CommandBuffer.cs
index 5390049..01b3960 100644
--- a/Open Empires/Assets/Scripts/Commands/CommandBuffer.cs	
+++ b/Open Empires/Assets/Scripts/Commands/CommandBuffer.cs	
@@ -47,5 +47,31 @@ namespace OpenEmpires
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
diff --git a/Open Empires/Assets/Scripts/Core/CommanderCommandOriginLedger.cs b/Open Empires/Assets/Scripts/Core/CommanderCommandOriginLedger.cs
index 3ebfa8e..9249279 100644
--- a/Open Empires/Assets/Scripts/Core/CommanderCommandOriginLedger.cs	
+++ b/Open Empires/Assets/Scripts/Core/CommanderCommandOriginLedger.cs	
@@ -102,19 +102,40 @@ namespace OpenEmpires
 
             // Without an explicit relay FIFO contract, equal payloads from distinct
             // original boxes cannot be assigned to their true issuers by ordinal.
+            var ambiguousMoves = new HashSet<int>();
+            var ambiguousGarrisons = new HashSet<int>();
             for (int i = 0; i < batch.Stamped.Length; i++)
             {
                 for (int j = i + 1; j < batch.Stamped.Length; j++)
                 {
                     if (batch.Stamped[i].Equals(batch.Stamped[j])
-                        && !ReferenceEquals(batch.Originals[i], batch.Originals[j])
-                        && (batch.Originals[i] is PlaceBuildingCommand
+                        && !ReferenceEquals(batch.Originals[i], batch.Originals[j]))
+                    {
+                        if (batch.Originals[i] is MoveCommand)
+                        {
+                            // Identical relay payloads have no provable individual
+                            // origin; leave only these Move receipts unattributed.
+                            ambiguousMoves.Add(i);
+                            ambiguousMoves.Add(j);
+                            continue;
+                        }
+                        if (batch.Originals[i] is GarrisonCommand)
+                        {
+                            // An opt-in exact order cannot inherit a different
+                            // identical box's origin; native rejects unmatched
+                            // replay candidates while its marker is pending.
+                            ambiguousGarrisons.Add(i);
+                            ambiguousGarrisons.Add(j);
+                            continue;
+                        }
+                        if (batch.Originals[i] is PlaceBuildingCommand
                             || batch.Originals[j] is PlaceBuildingCommand
                             || simulation.HasPendingTrainingOrigin(batch.Originals[i])
-                            || simulation.HasPendingTrainingOrigin(batch.Originals[j])))
-                    {
-                        Discard(batch, simulation);
-                        return verified;
+                            || simulation.HasPendingTrainingOrigin(batch.Originals[j]))
+                        {
+                            Discard(batch, simulation);
+                            return verified;
+                        }
                     }
                 }
             }
@@ -122,9 +143,11 @@ namespace OpenEmpires
             for (int i = 0; i < batch.Originals.Length; i++)
             {
                 ICommand original = batch.Originals[i];
-                if (!(original is PlaceBuildingCommand)
+                if (!(original is PlaceBuildingCommand) && !(original is MoveCommand)
+                    && !(original is GarrisonCommand)
                     && (!(original is TrainUnitCommand) || !simulation.HasPendingTrainingOrigin(original)))
                     continue;
+                if (ambiguousMoves.Contains(i) || ambiguousGarrisons.Contains(i)) continue;
                 // A replay box must identify only one original box in this tick.
                 if (verified.ContainsKey(local[i]))
                 {
diff --git a/Open Empires/Assets/Scripts/Core/GameSimulation.cs b/Open Empires/Assets/Scripts/Core/GameSimulation.cs
index d9d98ef..2025161 100644
--- a/Open Empires/Assets/Scripts/Core/GameSimulation.cs	
+++ b/Open Empires/Assets/Scripts/Core/GameSimulation.cs	
@@ -840,6 +840,7 @@ namespace OpenEmpires
             MapData.ApplyGenerationResult(tiles, heights, forestDensity);
             MapData.BasePositions = basePositions;
             CommandBuffer = new CommandBuffer();
+            CommandBuffer.CommandEnqueued += ObserveLocalGarrisonEnqueue;
             AiCommandBuffer = new CommandBuffer();
             movementSystem = new UnitMovementSystem();
             separationSystem = new UnitSeparationSystem();
@@ -1717,10 +1718,13 @@ namespace OpenEmpires
         private void ProcessCommand(ICommand command, ICommand verifiedOriginal = null)
         {
             if (ConsumeSuppressedLocalAction(command)) return;
+            if (RejectUnattributedExactGarrison(command, verifiedOriginal)) return;
+            if (!ObserveExactGarrisonCommand(command, verifiedOriginal ?? command,
+                out bool exactGarrison)) return;
             switch (command)
             {
                 case MoveCommand move:
-                    ProcessMoveCommand(move);
+                    ProcessMoveCommand(move, verifiedOriginal ?? command);
                     break;
                 case GatherCommand gather:
                     ProcessGatherCommand(gather);
@@ -1771,7 +1775,7 @@ namespace OpenEmpires
                     ProcessCancelUpgradeCommand(cancelUpgrade);
                     break;
                 case GarrisonCommand garrison:
-                    ProcessGarrisonCommand(garrison);
+                    ProcessGarrisonCommand(garrison, exactGarrison);
                     break;
                 case UngarrisonCommand ungarrison:
                     ProcessUngarrisonCommand(ungarrison);
@@ -1849,6 +1853,7 @@ namespace OpenEmpires
             if (command is GatherCommand || command is PatrolCommand
                 || command is SlaughterSheepCommand)
                 PublishLocalActionCommandProcessed(command);
+            PublishNativeUnitCommandProcessed(verifiedOriginal ?? command, command);
         }
 
         public void SpawnNeutralSheep()
@@ -3090,7 +3095,7 @@ namespace OpenEmpires
             CleanUpDestroyedBuilding(cmd.BuildingId);
         }
 
-        private void ProcessMoveCommand(MoveCommand cmd)
+        private void ProcessMoveCommand(MoveCommand cmd, ICommand originalCommand)
         {
             // === Retarget if click target is impassable or a hole ===
             Vector2Int cmdTargetTile = MapData.WorldToTile(cmd.TargetPosition);
@@ -3103,40 +3108,7 @@ namespace OpenEmpires
                 cmd.TargetPosition = MapData.TileToWorldFixed(newTarget.x, newTarget.y);
             }
 
-            // === Validate formation connectivity ===
-            // Ensure all formation positions are in the same connected walkable region as the main target.
-            if (cmd.FormationPositions != null)
-            {
-                Vector2Int anchorTile = MapData.WorldToTile(cmd.TargetPosition);
-
-                // Compute search radius: max Chebyshev distance from anchor to any formation pos + margin
-                int maxChebyshev = 0;
-                for (int fi = 0; fi < cmd.FormationPositions.Length; fi++)
-                {
-                    Vector2Int fpTile = MapData.WorldToTile(cmd.FormationPositions[fi]);
-                    int chebyshev = Mathf.Max(Mathf.Abs(fpTile.x - anchorTile.x), Mathf.Abs(fpTile.y - anchorTile.y));
-                    if (chebyshev > maxChebyshev) maxChebyshev = chebyshev;
-                }
-                int floodRadius = maxChebyshev * 2;
-
-                HashSet<int> reachable = GridPathfinder.FloodFillWalkable(MapData, anchorTile, floodRadius);
-
-                if (reachable.Count > 0)
-                {
-                    int mapWidth = MapData.Width;
-                    for (int fi = 0; fi < cmd.FormationPositions.Length; fi++)
-                    {
-                        Vector2Int fpTile = MapData.WorldToTile(cmd.FormationPositions[fi]);
-                        int key = fpTile.y * mapWidth + fpTile.x;
-                        if (!reachable.Contains(key))
-                        {
-                            Vector2Int snapped = GridPathfinder.FindConnectedTileToward(fpTile, anchorTile, reachable, mapWidth, floodRadius);
-                            if (snapped.x >= 0)
-                                cmd.FormationPositions[fi] = MapData.TileToWorldFixed(snapped.x, snapped.y);
-                        }
-                    }
-                }
-            }
+            SnapNativeMoveFormationPositions(ref cmd);
 
             // Assign a formation group ID when creating a new drag formation
             int formGroupId = 0;
@@ -3188,32 +3160,8 @@ namespace OpenEmpires
                 }
                 else if (cmd.FormationPositions != null && formPosIdx < cmd.FormationPositions.Length)
                 {
-                    targetPos = cmd.FormationPositions[formPosIdx];
-
-                    // Scale formation spacing proportionally to unit radius
-                    // (cavalry 0.55 → 1.375x spread, infantry 0.4 → 1.0x)
-                    Fixed32 radiusScale = unit.Radius / cachedUnitRadius;
-                    if (radiusScale != Fixed32.One)
-                    {
-                        FixedVector3 offset = targetPos - cmd.TargetPosition;
-                        targetPos = cmd.TargetPosition + new FixedVector3(
-                            offset.x * radiusScale, offset.y, offset.z * radiusScale);
-                    }
-
-                    Fixed32 scatterFactor = Fixed32.Max(Fixed32.Zero,
-                        Fixed32.One - (unit.Mass - Fixed32.One) / (cachedSpearmanMass - Fixed32.One));
-                    Fixed32 scatter = cachedFormationScatter * scatterFactor;
-
-                    if (scatter > cachedScatterThreshold)
-                    {
-                        uint hash = (uint)(unit.Id * 2654435761u + (uint)currentTick * 1103515245u);
-                        int hashX = (int)(hash & 0xFFFF) - 32768;
-                        int hashZ = (int)((hash >> 16) & 0xFFFF) - 32768;
-                        Fixed32 normX = new Fixed32(hashX);
-                        Fixed32 normZ = new Fixed32(hashZ);
-                        targetPos.x = targetPos.x + normX * scatter;
-                        targetPos.z = targetPos.z + normZ * scatter;
-                    }
+                    targetPos = ResolveNativeMoveFormationTarget(cmd.TargetPosition,
+                        cmd.FormationPositions[formPosIdx], unit, currentTick);
 
                     if (!cmd.IsQueued)
                     {
@@ -3285,12 +3233,17 @@ namespace OpenEmpires
                         unit.FinalDestination = new FixedVector3(targetPos.x, Fixed32.Zero, targetPos.z);
                         unit.State = UnitState.Moving;
                         unit.PlayerCommanded = !cmd.IsAttackMove;
+                        PublishNativeMoveProcessed(originalCommand, unit.Id, unit.FinalDestination,
+                            pathEnd == goalTile
+                                ? startTile == goalTile ? NativeMovePathResult.AlreadyAtTarget : NativeMovePathResult.Complete
+                                : NativeMovePathResult.Partial);
                     }
                     else
                     {
                         unit.ClearPath();
                         unit.State = UnitState.Idle;
                         unit.PlayerCommanded = false;
+                        PublishNativeMoveProcessed(originalCommand, unit.Id, targetPos, NativeMovePathResult.NoPath);
                     }
 
                     unit.TargetResourceNodeId = -1;
@@ -6445,6 +6398,7 @@ namespace OpenEmpires
 
         private void ProcessArrivedGarrisonUnits()
         {
+            PruneExactGarrisonSubjects();
             // Collect units to process first to avoid collection modification during iteration
             garrisonUnitsBuffer.Clear();
             foreach (var unit in UnitRegistry.GetAllUnits())
@@ -6531,6 +6485,7 @@ namespace OpenEmpires
 
         private void RedirectToAlternativeGarrison(UnitData unit)
         {
+            if (RejectExactGarrisonFallback(unit)) return;
             var alternativeBuilding = FindNearestAvailableGarrisonBuilding(unit.PlayerId, unit.SimPosition, unit.TargetGarrisonBuildingId);
             
             if (alternativeBuilding == null)
@@ -6604,14 +6559,14 @@ namespace OpenEmpires
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
@@ -6620,6 +6575,8 @@ namespace OpenEmpires
 
             // Get all available garrison buildings for this player, sorted by distance from the first unit
             var availableBuildings = GetAvailableGarrisonBuildings(cmd.PlayerId, validUnits[0].SimPosition);
+            if (exactTarget)
+                availableBuildings.RemoveAll(building => building.Id != cmd.TargetBuildingId);
             if (availableBuildings.Count == 0) return;
 
             // Try to prioritize the specified building if it's available
@@ -6739,7 +6696,8 @@ namespace OpenEmpires
         private bool GarrisonUnit(UnitData unit, BuildingData building)
         {
             // Check if building can actually garrison this unit
-            if (!building.CanGarrison)
+            if (unit.State == UnitState.Dead || unit.CurrentHealth <= 0
+                || !building.CanGarrison || !IsExactGarrisonTarget(unit, building.Id))
             {
                 return false;
             }
@@ -6768,6 +6726,8 @@ namespace OpenEmpires
             // Move to garrison storage (won't be moved/attacked/etc)
             UnitRegistry.GarrisonUnit(unit.Id);
 
+            activeExactGarrison.Remove(unit.Id);
+
             OnUnitGarrisoned?.Invoke(unit.Id, building.Id);
             return true;
         }
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs b/Open Empires/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs
index 4befa62..02f2c81 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs	
@@ -178,6 +178,134 @@ namespace OpenEmpires.Tests
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
@@ -193,6 +321,108 @@ namespace OpenEmpires.Tests
             }
         }
 
+        [Test]
+        public void BerriesRecovery_AdvancesPastFourNearerUnreachableSourcesWithBoundedChecks()
+        {
+            using (var f = new EconomyFixture())
+            {
+                var worker = f.Worker(0);
+                var first = f.Node(ResourceType.Food, 6);
+                var goal = (AllocateWorkersGoal)f.SubmitWithAmount("SelectedCount", "Exact", "1", "Idle", "", "Food", "Berries");
+                long origin = goal.GatheredIncomeAtActivation;
+                f.Manager.Tick(0);
+                var initial = f.Commands();
+                Assert.That(initial.OfType<GatherCommand>().Single().ResourceNodeId, Is.EqualTo(first.Id));
+                Assert.That(Subjects(initial), Is.EqualTo(new[] { worker.Id }));
+                f.Simulation.Tick(initial);
+                first.RemainingAmount = 0; worker.ClearPath(); worker.State = UnitState.Idle; worker.TargetResourceNodeId = -1;
+                foreach (var tile in new[] { new Vector2Int(f.Base.x + 12, f.Base.y + 10),
+                    new Vector2Int(f.Base.x + 17, f.Base.y + 10), new Vector2Int(f.Base.x + 12, f.Base.y + 15),
+                    new Vector2Int(f.Base.x + 17, f.Base.y + 15) }) f.IsolatedBerries(tile.x, tile.y);
+                var far = f.Simulation.MapData.AddResourceNode(ResourceType.Food,
+                    f.Simulation.MapData.TileToWorldFixed(f.Base.x + 23, f.Base.y + 22), 1000);
+                var sheep = f.Simulation.UnitRegistry.CreateUnit(0, worker.SimPosition, Fixed32.One, Fixed32.One, Fixed32.One);
+                sheep.IsSheep = true; sheep.CurrentHealth = 20;
+
+                f.Manager.ResetDiagnosticPathCheckCount(); f.Manager.Tick(15);
+                Assert.That(f.Manager.DiagnosticPathCheckCount, Is.LessThanOrEqualTo(4));
+                Assert.That(f.Commands(), Is.Empty, "The first bounded window has no reachable authorized source.");
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing), goal.StatusReason);
+                Assert.That(goal.RecoveryCandidateOffsets[worker.Id], Is.EqualTo(4));
+                f.Simulation.Tick(new List<ICommand>());
+                f.Manager.ResetDiagnosticPathCheckCount(); f.Manager.Tick(30);
+                var recovered = f.Commands();
+                Assert.That(f.Manager.DiagnosticPathCheckCount, Is.LessThanOrEqualTo(4));
+                Assert.That(recovered.OfType<GatherCommand>().Single().ResourceNodeId, Is.EqualTo(far.Id));
+                Assert.That(recovered.OfType<GatherCommand>().Single().SourceKind, Is.EqualTo(ResourceSourceKind.Berries));
+                Assert.That(Subjects(recovered), Is.EqualTo(new[] { worker.Id }));
+                Assert.That(recovered.OfType<SlaughterSheepCommand>(), Is.Empty);
+                Assert.That(goal.SelectedWorkerIds, Is.EqualTo(new[] { worker.Id }));
+                Assert.That(goal.GatheredIncomeAtActivation, Is.EqualTo(origin));
+                f.Simulation.Tick(recovered);
+                Assert.That(worker.TargetResourceNodeId, Is.EqualTo(far.Id));
+            }
+        }
+
+        [Test]
+        public void BerriesRecovery_ExhaustedVisibleSourcesFailClosedWithoutReplacement()
+        {
+            using (var f = new EconomyFixture())
+            {
+                var worker = f.Worker(0); var first = f.Node(ResourceType.Food, 6);
+                var goal = (AllocateWorkersGoal)f.SubmitWithAmount("SelectedCount", "Exact", "1", "Idle", "", "Food", "Berries");
+                f.Manager.Tick(0); var initial = f.Commands(); f.Simulation.Tick(initial);
+                first.RemainingAmount = 0; worker.ClearPath(); worker.State = UnitState.Idle; worker.TargetResourceNodeId = -1;
+                foreach (var tile in new[] { new Vector2Int(f.Base.x + 12, f.Base.y + 10),
+                    new Vector2Int(f.Base.x + 17, f.Base.y + 10), new Vector2Int(f.Base.x + 12, f.Base.y + 15),
+                    new Vector2Int(f.Base.x + 17, f.Base.y + 15) }) f.IsolatedBerries(tile.x, tile.y);
+                var unused = f.Worker(3);
+                f.Manager.ResetDiagnosticPathCheckCount(); f.Manager.Tick(15);
+                Assert.That(f.Manager.DiagnosticPathCheckCount, Is.LessThanOrEqualTo(4));
+                Assert.That(goal.RecoverySearchExhausted, Is.True);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
+                Assert.That(f.Commands(), Is.Empty);
+                Assert.That(goal.SelectedWorkerIds, Is.EqualTo(new[] { worker.Id }));
+                Assert.That(f.Manager.GetWorkerReservation(unused.Id), Is.Null);
+                f.Manager.Tick(1815);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Failed), goal.StatusReason);
+                Assert.That(f.Commands(), Is.Empty);
+            }
+        }
+
+        [Test]
+        public void BerriesAdditionalIncome_OtherWorkerNativeDepositCompletesWithoutStoppingGather()
+        {
+            using (var f = new EconomyFixture())
+            {
+                var townCenter = f.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Base.x, f.Base.y, false, true);
+                var first = f.Node(ResourceType.Food, 6); var selected = f.Worker(0);
+                var goal = (AllocateWorkersGoal)f.SubmitWithAmount(10, "SelectedCount", "Exact", "1", "Idle", "", "Food", "Berries");
+                f.Manager.Tick(0); var initial = f.Commands();
+                Assert.That(Subjects(initial), Is.EqualTo(new[] { selected.Id }));
+                f.Simulation.Tick(initial);
+                Assert.That(selected.TargetResourceNodeId, Is.EqualTo(first.Id));
+                var other = f.Worker(7);
+                other.SimPosition = f.Simulation.MapData.TileToWorldFixed(townCenter.OriginTileX + townCenter.TileFootprintWidth,
+                    townCenter.OriginTileZ + 1);
+                other.CarriedResourceType = ResourceType.Food; other.CarriedResourceAmount = 10;
+                f.Simulation.Tick(new List<ICommand> { new DropOffCommand(0, new[] { other.Id }, townCenter.Id) });
+                for (int i = 0; i < 60 && f.Simulation.ResourceManager.GetGatheredIncome(0, ResourceType.Food) < 10; i++)
+                    f.Simulation.Tick(new List<ICommand>());
+                Assert.That(f.Simulation.ResourceManager.GetGatheredIncome(0, ResourceType.Food), Is.GreaterThanOrEqualTo(10));
+                f.Manager.Tick(15);
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
+                Assert.That(goal.ResourceProgress, Is.EqualTo(10));
+                Assert.That(selected.TargetResourceNodeId, Is.EqualTo(first.Id), "The ordinary selected gather order is left running.");
+                Assert.That(selected.State == UnitState.MovingToGather || selected.State == UnitState.Gathering
+                    || selected.State == UnitState.MovingToDropoff || selected.State == UnitState.DroppingOff,
+                    Is.True, "The selected worker keeps its ordinary gather lifecycle after task completion.");
+                Assert.That(f.Commands(), Is.Empty);
+                for (int tick = 30; tick <= 120; tick += 15)
+                { f.Manager.Tick(tick); Assert.That(f.Commands(), Is.Empty); f.Simulation.Tick(new List<ICommand>()); }
+            }
+        }
+
         private static int[] Subjects(List<ICommand> commands) => commands.SelectMany(c => CommanderWorkerAuthority.GetSubjectUnitIds(c) ?? Array.Empty<int>()).ToArray();
 
         private sealed class EconomyFixture : IDisposable
@@ -223,6 +453,15 @@ namespace OpenEmpires.Tests
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
@@ -233,6 +472,28 @@ namespace OpenEmpires.Tests
                 var result = new CommanderIntentResolver().Resolve(intent, Simulation, Manager);
                 Assert.That(result.CreatedGoal, Is.True, result.Reason); return result.Goal;
             }
+            internal CommanderGoal SubmitWithAmount(params string[] values)
+                => SubmitWithAmount(100, values);
+            internal CommanderGoal SubmitWithAmount(int amount, params string[] values)
+            {
+                string node = AllocationNode(values);
+                node = node.Substring(0, node.Length - 1)
+                    + ",\"resourceAmount\":" + amount + ",\"resourceAmountMode\":\"AdditionalGathered\"}";
+                var parsed = CommanderSemanticJson.Parse(Request(node));
+                Assert.That(parsed.IsValid, Is.True);
+                Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], Context(), out var intent, out _), Is.True);
+                var result = new CommanderIntentResolver().Resolve(intent, Simulation, Manager);
+                Assert.That(result.CreatedGoal, Is.True, result.Reason); return result.Goal;
+            }
+            internal ResourceNodeData IsolatedBerries(int x, int z)
+            {
+                var node = Simulation.MapData.AddResourceNode(ResourceType.Food,
+                    Simulation.MapData.TileToWorldFixed(x, z), 1000);
+                for (int tx = node.TileX - 1; tx <= node.TileX + node.FootprintWidth; tx++)
+                    for (int tz = node.TileZ - 1; tz <= node.TileZ + node.FootprintHeight; tz++)
+                        Simulation.MapData.Tiles[tx, tz] = TileType.Water;
+                return node;
+            }
             internal List<ICommand> Commands() => Simulation.CommandBuffer.FlushCommands();
             public void Dispose() { Manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
         }
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderGrandFixSpatialTruthTests.cs b/Open Empires/Assets/Tests/EditMode/CommanderGrandFixSpatialTruthTests.cs
index c2699a3..e1262ef 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderGrandFixSpatialTruthTests.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderGrandFixSpatialTruthTests.cs	
@@ -53,6 +53,9 @@ namespace OpenEmpires.Tests
         { Assert.Throws<ArgumentException>(()=>CommanderIntentDtoCodec.FromIntent(Radius(CommanderCapabilityActionType.PatrolArea,5))); }
         [Test]public void PointPatrol_UsesNormalPatrolCommandWithNoAroundPromise()
         {
+            int x=sim.MapData.Width/2,z=sim.MapData.Height/2;
+            for(int tx=x-8;tx<=x+8;tx++)for(int tz=z-8;tz<=z+8;tz++)
+                sim.FogOfWar.SetVisible(0,tx,tz);
             Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(Point(CommanderCapabilityActionType.PatrolArea),out var command,out var reason),Is.True);
             Assert.That(command,Is.TypeOf<PatrolCommand>());Assert.That(reason.ToLowerInvariant(),Does.Not.Contain("around"));
         }
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderGrandFixStatusTests.cs b/Open Empires/Assets/Tests/EditMode/CommanderGrandFixStatusTests.cs
index 2fbe348..1b8640f 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderGrandFixStatusTests.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderGrandFixStatusTests.cs	
@@ -75,6 +75,69 @@ namespace OpenEmpires.Tests
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
+        [TestCase("What happened to the six units?", "One")]
+        [TestCase("What happened to the seven units?", "None")]
+        public async Task CausalCountedUnitsQuestion_SelectsOnlyMatchingOwnedRequest(string question, string selection)
+        {
+            var goal = manager.SubmitEnsureUnitCount(1, 6, newProductionCount: 6);
+            manager.Tick(0); sim.CommandBuffer.FlushCommands();
+            var provider = new FactsProvider(Answer); var chat = Chat(provider);
+            int before = manager.Goals.Count;
+            await chat.SubmitMessageAsync(question);
+            var status = JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext).tacticalStatus;
+            Assert.That(status, Is.Not.Null);
+            Assert.That(status.selection, Is.EqualTo(selection));
+            if (selection == "One")
+            {
+                Assert.That(status.requests[0].steps[0].requestedNew, Is.EqualTo(6));
+                Assert.That(status.requests[0].steps[0].reason, Is.EqualTo(goal.StatusReason));
+            }
+            Assert.That(manager.Goals.Count, Is.EqualTo(before));
+            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
+        }
+
+        [Test]
+        public async Task CausalCountedUnitsQuestion_PreservesAmbiguityBetweenOwnedRequests()
+        {
+            manager.SubmitEnsureUnitCount(1, 6, newProductionCount: 6);
+            manager.SubmitEnsureUnitCount(2, 6, newProductionCount: 6);
+            manager.Tick(0); sim.CommandBuffer.FlushCommands();
+            var provider = new FactsProvider(Answer); var chat = Chat(provider);
+            await chat.SubmitMessageAsync("What happened to the six units?");
+            var status = JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext).tacticalStatus;
+            Assert.That(status, Is.Not.Null);
+            Assert.That(status.selection, Is.EqualTo("Ambiguous"));
+            Assert.That(manager.Goals.Count, Is.EqualTo(2));
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
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderPhase4E1ProviderTests.cs b/Open Empires/Assets/Tests/EditMode/CommanderPhase4E1ProviderTests.cs
index b601725..f3a56d7 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderPhase4E1ProviderTests.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderPhase4E1ProviderTests.cs	
@@ -54,7 +54,8 @@ namespace OpenEmpires.Tests
             string sent = body.messages[1].content;
             Assert.That(sent, Does.Contain("Spearman"));
             Assert.That(sent, Does.Contain("Barracks"));
-            Assert.That(sent, Does.Contain("RangedReinforcement"));
+            Assert.That(sent, Does.Not.Contain("RangedReinforcement"),
+                "Unrequested strategy facts are excluded from a unit-production request.");
             Assert.That(sent, Does.Contain("population"));
             Assert.That(sent, Does.Not.Contain("PlayerId"));
             // Canonical building IDs are permitted content facts, not runtime identity.
@@ -106,13 +107,13 @@ namespace OpenEmpires.Tests
         {
             var transport = new FakeTransport(Completion(200, UnitJson));
             await new OpenRouterCommanderProvider(DummyKey, transport).TranslateSemanticAsync(
-                new CommanderSemanticProviderRequest("unclear order", Context()),
+                new CommanderSemanticProviderRequest("Build what?", Context()),
                 CancellationToken.None);
 
             ChatBody body = JsonUtility.FromJson<ChatBody>(transport.Body);
             string instruction = body.messages[0].content;
-            Assert.That(instruction, Does.Contain("\"outcome\":\"Clarify\""));
-            Assert.That(instruction, Does.Contain("\"outcome\":\"Unsupported\""));
+            Assert.That(instruction, Does.Contain("outcome:Clarify"));
+            Assert.That(instruction, Does.Contain("Unsupported"));
             Assert.That(instruction, Does.Not.Contain("\"outcome\":\"Clarify|Unsupported\""));
         }
 
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderPhase4GCapabilityTests.cs b/Open Empires/Assets/Tests/EditMode/CommanderPhase4GCapabilityTests.cs
index 9a9280e..176c1ff 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderPhase4GCapabilityTests.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderPhase4GCapabilityTests.cs	
@@ -247,6 +247,426 @@ namespace OpenEmpires.Tests
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
+        public void NearSlotIdleSubject_CannotCompleteBeforeItsNativeMoveIsProcessed()
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
+                Assert.That(simulation.CommandBuffer.FlushCommands(), Has.Count.EqualTo(1));
+                unit.SimPosition = goal.NativeMovementSlots[0];
+                unit.State = UnitState.Idle;
+                simulation.Tick(new List<ICommand>()); // The exact boxed Move never executes.
+                manager.Tick(15);
+                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [Test]
+        public void NativePartialPathAfterPreflight_CannotBecomeInventedArrival()
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
+                var commands = simulation.CommandBuffer.FlushCommands();
+                Assert.That(commands, Has.Count.EqualTo(1));
+                var start = simulation.MapData.WorldToTile(unit.SimPosition);
+                simulation.MapData.Tiles[start.x - 1, start.y] = TileType.Water;
+                simulation.MapData.Tiles[start.x + 1, start.y] = TileType.Water;
+                simulation.MapData.Tiles[start.x, start.y - 1] = TileType.Water;
+                simulation.MapData.Tiles[start.x, start.y + 1] = TileType.Water;
+                simulation.Tick(commands);
+                manager.Tick(15);
+                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
+                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Contain("stopped short"));
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [Test]
+        public void ReplacementNativeMoveToSameEndpoint_CannotCompleteOriginalRequest()
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 10, z + 10),
+                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
+                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
+                for (int ix = x - 18; ix <= x + 18; ix++)
+                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
+                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
+                    CommanderCapabilityActionType.MoveUnits,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
+                manager.Tick(0);
+                var issued = simulation.CommandBuffer.FlushCommands();
+                simulation.Tick(issued);
+                Assert.That(goal.NativeMovementSlots, Has.Length.EqualTo(1));
+                var unrelated = new MoveCommand(0, new[] { unit.Id }, goal.NativeMovementSlots[0]);
+                simulation.Tick(new List<ICommand> { unrelated });
+                for (int tick = 15; tick <= 15000 && !goal.IsTerminal; tick += 15)
+                { simulation.Tick(new List<ICommand>()); manager.Tick(tick); }
+                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [Test]
+        public void UnrelatedNativeStopNearEndpoint_CannotCompleteOriginalRequest()
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 10, z + 10),
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
+                Assert.That(goal.NativeMovementProcessed[0], Is.True);
+                unit.SimPosition = goal.NativeMovementEndpoints[0];
+                simulation.Tick(new List<ICommand> { new StopCommand(0, new[] { unit.Id }) });
+                manager.Tick(15);
+                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
+            }
+            finally { manager.Dispose(); Object.DestroyImmediate(config); }
+        }
+
+        [Test]
+        public void MixedRadiusFormation_UsesLegalNativeEndpointsAndCompletes_AcceleratedFixture()
+        {
+            var config = ScriptableObject.CreateInstance<SimulationConfig>();
+            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
+            var manager = new CommanderGoalManager(simulation, 0);
+            try
+            {
+                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
+                var tc = simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
+                var ids = new List<int>();
+                for (int i = 0; i < 2; i++)
+                {
+                    var unit = simulation.UnitRegistry.CreateUnit(0,
+                        simulation.MapData.TileToWorldFixed(x + 10 + i, z + 10), Fixed32.One,
+                        Fixed32.FromFloat(i == 0 ? .4f : 1.2f), Fixed32.One);
+                    unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100; ids.Add(unit.Id);
+                }
+                for (int ix = x - 20; ix <= x + 20; ix++)
+                    for (int iz = z - 20; iz <= z + 20; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
+                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
+                    CommanderCapabilityActionType.DefendArea,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 2, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
+                manager.Tick(0);
+                var commands = simulation.CommandBuffer.FlushCommands();
+                Assert.That(commands, Has.Count.EqualTo(1), goal.StatusReason);
+                simulation.Tick(commands);
+                var anchor = simulation.MapData.WorldToTile(tc.SimPosition);
+                var nativeTiles = new HashSet<Vector2Int>();
+                foreach (int id in ids)
+                {
+                    var unit = simulation.UnitRegistry.GetUnit(id);
+                    var tile = simulation.MapData.WorldToTile(unit.FinalDestination);
+                    Assert.That(Mathf.Max(Mathf.Abs(tile.x - anchor.x), Mathf.Abs(tile.y - anchor.y)), Is.LessThanOrEqualTo(10));
+                    Assert.That(simulation.MapData.IsWalkable(tile.x, tile.y, 0, simulation.BuildingRegistry), Is.True);
+                    Assert.That(nativeTiles.Add(tile), Is.True);
+                }
+                for (int tick = 15; tick <= 15000 && !goal.IsTerminal; tick += 15)
+                { simulation.Tick(new List<ICommand>()); manager.Tick(tick); }
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
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
@@ -259,16 +679,19 @@ namespace OpenEmpires.Tests
                 int z = simulation.MapData.Height / 2;
                 simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                 UnitData military = simulation.UnitRegistry.CreateUnit(0,
-                    simulation.MapData.TileToWorldFixed(x + 2, z + 2), Fixed32.One,
+                    simulation.MapData.TileToWorldFixed(x + 10, z + 10), Fixed32.One,
                     Fixed32.FromFloat(.4f), Fixed32.One);
                 military.UnitType = 1;
                 military.MaxHealth = military.CurrentHealth = 100;
+                for (int ix = x - 18; ix <= x + 18; ix++)
+                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                 var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                     new CommanderUnitSelector(CommanderUnitSelectorKind.Military),
                     new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
                 CommanderCapabilityGoal goal = manager.SubmitCapabilityAction(intent);
                 manager.Tick(0);
-                simulation.CommandBuffer.FlushCommands();
+                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
+                Assert.That(simulation.CommandBuffer.FlushCommands(), Has.Some.TypeOf<MoveCommand>());
                 simulation.CommandBuffer.EnqueueCommand(new MoveCommand(0, new[] { military.Id },
                     simulation.MapData.TileToWorldFixed(x + 12, z + 12)));
                 simulation.CommandBuffer.FlushCommands();
@@ -294,6 +717,13 @@ namespace OpenEmpires.Tests
             {
                 int x = simulation.MapData.Width / 2;
                 int z = simulation.MapData.Height / 2;
+                for (int tx = x - 12; tx <= x + 12; tx++)
+                    for (int tz = z - 12; tz <= z + 12; tz++)
+                    {
+                        simulation.MapData.Tiles[tx, tz] = TileType.Grass;
+                        simulation.MapData.ForestDensity[tx, tz] = 0f;
+                        simulation.FogOfWar.SetVisible(0, tx, tz);
+                    }
                 simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                 UnitData unrelated = simulation.UnitRegistry.CreateUnit(0,
                     simulation.MapData.TileToWorldFixed(x + 1, z + 1), Fixed32.One,
@@ -303,7 +733,7 @@ namespace OpenEmpires.Tests
                 for (int i = 0; i < 3; i++)
                 {
                     UnitData spearman = simulation.UnitRegistry.CreateUnit(0,
-                        simulation.MapData.TileToWorldFixed(x + 2 + i, z + 2), Fixed32.One,
+                        simulation.MapData.TileToWorldFixed(x + 2 + i, z + 6), Fixed32.One,
                         Fixed32.FromFloat(.4f), Fixed32.One);
                     spearman.UnitType = 1;
                     spearman.MaxHealth = spearman.CurrentHealth = 100;
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderPhase4GHostileAuditTests.cs b/Open Empires/Assets/Tests/EditMode/CommanderPhase4GHostileAuditTests.cs
index 079f5d4..1790f7b 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderPhase4GHostileAuditTests.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderPhase4GHostileAuditTests.cs	
@@ -52,6 +52,7 @@ namespace OpenEmpires.Tests
                 for (int tz = cz - 25; tz <= cz + 25; tz++)
                 {
                     simulation.MapData.Tiles[tx, tz] = TileType.Grass;
+                    simulation.MapData.ForestDensity[tx, tz] = 0f;
                     simulation.FogOfWar.SetVisible(0, tx, tz);
                 }
             simulation.CreateBuilding(0, BuildingType.TownCenter, cx, cz, false, true);
@@ -842,7 +843,7 @@ namespace OpenEmpires.Tests
             for (int i = 0; i < 5; i++)
             {
                 var u = simulation.UnitRegistry.CreateUnit(0,
-                    simulation.MapData.TileToWorldFixed(cx + i, cz + 3), Fixed32.One,
+                    simulation.MapData.TileToWorldFixed(cx + i, cz + 6), Fixed32.One,
                     Fixed32.FromFloat(.4f), Fixed32.One);
                 u.UnitType = 1;
                 u.CurrentHealth = u.MaxHealth = 100;
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs b/Open Empires/Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs
index 98329ac..5bd16c2 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs	
@@ -8,6 +8,846 @@ namespace OpenEmpires.Tests
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
+        public void TwoHousesNearTc_BacktracksPastCentralCandidateAndBuildsCompatiblePair()
+        {
+            using var f = new Fixture(3);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            // Only the west strip can build. Its ranked center overlaps both
+            // separated sites, while the two separated sites are compatible.
+            for (int tx = x - 8; tx <= x + 8; tx++)
+                for (int tz = z - 8; tz <= z + 8; tz++)
+                    if (tx < x || tx >= x + 4 || tz < z || tz >= z + 4)
+                        f.Sim.MapData.Tiles[tx, tz] = TileType.Foundation;
+            for (int tx = x - 5; tx <= x - 2; tx++)
+                for (int tz = z - 2; tz <= z + 5; tz++)
+                    f.Sim.MapData.Tiles[tx, tz] = TileType.Grass;
+            string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
+                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
+                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
+            var parsed = CommanderSemanticJson.Parse(json);
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "build two Houses near my TC", 1);
+            var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
+            for (int tick = 0; tick < 6000 && goal.Status != CommanderGoalStatus.Completed; tick++)
+            {
+                if (tick % 15 == 0) f.Manager.Tick(tick);
+                f.Sim.Tick(); // Accelerated fixture, unchanged native construction timers.
+            }
+            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed),
+                goal.StatusReason + " planned=" + string.Join(";", goal.DynamicPlannedSiteSet ?? Array.Empty<Vector2Int>())
+                + " attributed=" + string.Join(",", goal.AttributedBuildingIds));
+            var sites = goal.ResultBuildingIds.Select(id => f.Sim.BuildingRegistry.GetBuilding(id))
+                .Select(b => new Vector2Int(b.OriginTileX, b.OriginTileZ)).ToArray();
+            Assert.That(sites, Is.EquivalentTo(new[] {
+                new Vector2Int(x - 4, z - 1), new Vector2Int(x - 4, z + 3) }));
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
+        [TestCase(0)]
+        [TestCase(1)]
+        [TestCase(2)]
+        public void ExactGarrison_DelayedPastPendingExpiryCannotBecomeOrdinaryFallback(int delivery)
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
+            for (int tick = 0; tick < 258; tick++) f.Sim.Tick();
+            ICommand delivered = delivery == 0 ? original
+                : new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            if (delivery == 2)
+            {
+                var ledger = new CommanderCommandOriginLedger();
+                ICommand stamped = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+                ledger.Record(f.Sim.CurrentTick, 0, new[] { original }, new[] { stamped }, f.Sim);
+                var commands = new System.Collections.Generic.List<ICommand> { delivered, new NoopCommand(0) };
+                var origins = ledger.Consume(f.Sim.CurrentTick, 0, commands, f.Sim);
+                Assert.That(origins[delivered], Is.SameAs(original));
+                f.Sim.Tick(commands, origins);
+            }
+            else f.Sim.Tick(new System.Collections.Generic.List<ICommand> { delivered });
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.Not.EqualTo(other.Id));
+            Assert.That(other.GarrisonedUnitIds, Is.Empty);
+        }
+
+        [Test]
+        public void ExpiredExactMarker_PreservesProvenHumanReplacementAndBoundsNewAdmissions()
+        {
+            using var f = new Fixture(2);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
+            requested.GarrisonCapacity = 1;
+            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
+            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
+            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
+            for (int tick = 0; tick < 258; tick++) f.Sim.Tick();
+            ICommand human = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            f.Sim.CommandBuffer.EnqueueCommand(human, CommandEnqueueSource.Human);
+            var flushed = f.Sim.CommandBuffer.FlushCommands();
+            var ledger = new CommanderCommandOriginLedger();
+            ICommand stamped = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            ledger.Record(259, 0, flushed, new[] { stamped }, f.Sim);
+            ICommand replay = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
+            var received = new System.Collections.Generic.List<ICommand> { replay, new NoopCommand(0) };
+            var origins = ledger.Consume(259, 0, received, f.Sim);
+            Assert.That(origins[replay], Is.SameAs(human));
+            f.Sim.Tick(received, origins);
+            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(other.Id),
+                "Only a proven, separate local human order keeps ordinary native fallback.");
+
+            // One pending/tombstoned subject occupies one bounded slot; expiration
+            // must not silently free that safety evidence for another exact order.
+            using var bounded = new Fixture(1);
+            var shelter = bounded.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
+            var boundedWorkers = new System.Collections.Generic.List<UnitData> { bounded.Workers[0] };
+            for (int i = 1; i < 201; i++)
+            {
+                var worker = bounded.Sim.UnitRegistry.CreateUnit(0, bounded.Workers[0].SimPosition,
+                    Fixed32.One, Fixed32.One, Fixed32.One);
+                worker.IsVillager = true;
+                worker.CurrentHealth = worker.MaxHealth = 100;
+                boundedWorkers.Add(worker);
+            }
+            foreach (var worker in boundedWorkers.Take(200))
+                Assert.That(bounded.Sim.RegisterExactGarrisonCommand(
+                    new GarrisonCommand(0, new[] { worker.Id }, shelter.Id)), Is.True);
+            for (int tick = 0; tick < 258; tick++) bounded.Sim.Tick();
+            Assert.That(bounded.Sim.RegisterExactGarrisonCommand(
+                new GarrisonCommand(0, new[] { boundedWorkers[200].Id }, shelter.Id)), Is.False);
+        }
+
+        [Test]
+        public void ResultBoundVillagerGarrison_UsesOnlyExactProducedResultIds()
+        {
+            using var f = new Fixture(2);
+            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
+            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
+                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Villager\",\"count\":2,\"quantityMode\":\"New\"},"
+                + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"count\":2,\"location\":\"PlayerBase\",\"structure\":\"TownCenter\",\"dependsOn\":[0],\"resultFromNode\":0}]}" );
+            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
+            var candidate = f.Manager.PrepareActionPlan(parsed, "train two new villagers then garrison those exact two", 1);
+            var goals = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
+            var producer = (EnsureUnitCountGoal)goals[0];
+            var garrison = (CommanderCapabilityGoal)goals[1];
+            var produced = new int[2];
+            for (int i = 0; i < produced.Length; i++)
+            {
+                var unit = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 3 - i, z + 3),
+                    Fixed32.One, Fixed32.One, Fixed32.One);
+                unit.IsVillager = true; unit.UnitType = 0;
+                unit.CurrentHealth = unit.MaxHealth = 100;
+                produced[i] = unit.Id;
+            }
+            producer.CaptureUnitResult(produced, 0);
+            producer.SetStatus(CommanderGoalStatus.Completed, "Focused exact producer-result fixture");
+            f.Manager.Tick(0);
+            var command = f.Sim.CommandBuffer.FlushCommands().OfType<GarrisonCommand>().Single();
+            Assert.That(command.UnitIds, Is.EquivalentTo(produced));
+            Assert.That(command.UnitIds.Intersect(f.Workers.Select(w => w.Id)), Is.Empty);
+            Assert.That(garrison.GarrisonCandidateIds, Is.EquivalentTo(produced));
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
@@ -428,11 +1268,12 @@ namespace OpenEmpires.Tests
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
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderPhase5AProviderContractTests.cs b/Open Empires/Assets/Tests/EditMode/CommanderPhase5AProviderContractTests.cs
index 15857b9..9136e8c 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderPhase5AProviderContractTests.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderPhase5AProviderContractTests.cs	
@@ -33,8 +33,8 @@ namespace OpenEmpires.Tests
             Assert.That(body.max_tokens, Is.EqualTo(4096));
             Assert.That(transport.Bodies[0], Does.Not.Contain("response_format"));
             string instruction = body.messages[0].content;
-            Assert.That(instruction, Does.Contain("\"outcome\":\"DynamicPlan\""));
-            Assert.That(instruction, Does.Contain("building:Farm"));
+            Assert.That(instruction, Does.Contain("DynamicPlan"));
+            Assert.That(instruction, Does.Contain("BuildStructure"));
             foreach (var primitive in CommanderDynamicPrimitiveRegistry.All)
             {
                 Assert.That(instruction, Does.Contain(primitive.Id), primitive.Id);
@@ -44,9 +44,9 @@ namespace OpenEmpires.Tests
                     Assert.That(instruction, Does.Contain(input.Role), primitive.Id + "." + input.Role);
             }
             Assert.That(instruction, Does.Contain("never authorize"));
-            Assert.That(instruction, Does.Contain("question"));
-            Assert.That(instruction, Does.Contain("Castle"));
-            Assert.That(instruction, Does.Contain("StrategicObjective"));
+            Assert.That(instruction, Does.Contain("missingSlots"));
+            Assert.That(instruction, Does.Contain("count:1..20"));
+            Assert.That(instruction, Does.Contain("Max12 nodes"));
             Assert.That(transport.Bodies[0], Does.Not.Contain(Key));
         }
 
@@ -70,19 +70,13 @@ namespace OpenEmpires.Tests
             await Translate(transport, "send two of three idle workers to sheep and the third to build a mill near berries");
 
             string instruction = JsonUtility.FromJson<ChatBody>(transport.Bodies[0]).messages[0].content;
-            Assert.That(instruction.Contains("one shared worker selection"), Is.True,
+            Assert.That(instruction, Does.Contain("one shared worker selection").IgnoreCase,
                 "System message must describe one shared worker set before partitioning.");
             Assert.That(instruction.Contains("partition-workers"), Is.True,
                 "System message must expose the registry partition mechanic.");
-            var example = CommanderSemanticJson.Parse(Example(instruction, "SharedWorkersExample:"));
-            Assert.That(example.IsValid, Is.True, example.SafeExplanation);
-            Assert.That(example.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
-            Assert.That(example.DynamicPlan.Nodes, Has.Count.EqualTo(7));
-            Assert.That(example.DynamicPlan.Nodes[0].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.SelectWorkers));
-            Assert.That(example.DynamicPlan.Nodes[1].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.PartitionWorkers));
-            Assert.That(example.DynamicPlan.Nodes[2].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.PartitionWorkers));
-            Assert.That(example.DynamicPlan.Nodes[5].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.Build));
-            Assert.That(example.DynamicPlan.Nodes[6].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.AllocateWorkers));
+            Assert.That(instruction, Does.Contain("select-workers"));
+            Assert.That(instruction, Does.Contain("allocate-workers"));
+            Assert.That(instruction, Does.Contain("dependsOn"));
         }
 
         [Test]
@@ -96,14 +90,11 @@ namespace OpenEmpires.Tests
                 "System message must route repeated placement through DynamicPlan.");
             Assert.That(instruction.Contains("exact producer set"), Is.True,
                 "System message must bind production to the new building result.");
-            Assert.That(instruction.Contains("simple known requests"), Is.True,
+            Assert.That(instruction, Does.Contain("simple known requests").IgnoreCase,
                 "Known fast paths must remain available.");
-            var example = CommanderSemanticJson.Parse(Example(instruction, "ExactProducersExample:"));
-            Assert.That(example.IsValid, Is.True, example.SafeExplanation);
-            Assert.That(example.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
-            Assert.That(example.DynamicPlan.Nodes, Has.Count.EqualTo(3));
-            Assert.That(example.DynamicPlan.Nodes[1].Parameter<int>("count"), Is.EqualTo(2));
-            Assert.That(example.DynamicPlan.Nodes[2].Parameter<string>("quantityMode"), Is.EqualTo("New"));
+            Assert.That(instruction, Does.Contain("build"));
+            Assert.That(instruction, Does.Contain("train"));
+            Assert.That(instruction, Does.Contain("quantityMode"));
         }
 
         [Test]
diff --git a/Open Empires/Assets/Tests/PlayMode/CommanderGrandFixInputDispatchPlayModeTests.cs b/Open Empires/Assets/Tests/PlayMode/CommanderGrandFixInputDispatchPlayModeTests.cs
index 54bdc76..60ec914 100644
--- a/Open Empires/Assets/Tests/PlayMode/CommanderGrandFixInputDispatchPlayModeTests.cs	
+++ b/Open Empires/Assets/Tests/PlayMode/CommanderGrandFixInputDispatchPlayModeTests.cs	
@@ -115,17 +115,24 @@ namespace OpenEmpires.Tests
         }
 
         [UnityTest]
-        public IEnumerator CompactLauncher_PrecedesSettingsWithoutOverlappingControls()
+        public IEnumerator CompactLauncher_UsesSeparateSettingsWithoutOverlappingControls()
         {
             chat.enabled=true;yield return null;Canvas.ForceUpdateCanvases();
             var compact=chat.transform.Find("CommanderCanvas/CompactCommander");
-            var launcher=compact.Find("Launcher").GetComponent<RectTransform>();
-            var settings=compact.Find("TextAIOptions").GetComponent<RectTransform>();
-            var launcherCorners=new Vector3[4];var settingsCorners=new Vector3[4];
-            launcher.GetWorldCorners(launcherCorners);settings.GetWorldCorners(settingsCorners);
-            Assert.That(launcherCorners[0].y,Is.GreaterThan(settingsCorners[1].y),
-                "Primary Commander/Record/Voice controls belong above Text AI settings, with a clear gap.");
+            Assert.That(compact.Find("Launcher").gameObject.activeInHierarchy,Is.True);
+            Assert.That(compact.Find("TextAIOptions").gameObject.activeInHierarchy,Is.False);
+            Assert.That(compact.Find("VoiceOptions").gameObject.activeInHierarchy,Is.False);
             Assert.That(goals.Goals,Is.Empty);Assert.That(chat.IsExpanded,Is.False);
+            chat.OpenCommander();yield return null;Canvas.ForceUpdateCanvases();
+            var panel=chat.transform.Find("CommanderCanvas/Panel");
+            var settings=panel.Find("Settings").GetComponent<RectTransform>();
+            var minimize=panel.Find("Minimize").GetComponent<RectTransform>();
+            var settingsCorners=new Vector3[4];var minimizeCorners=new Vector3[4];
+            settings.GetWorldCorners(settingsCorners);minimize.GetWorldCorners(minimizeCorners);
+            Assert.That(settings.gameObject.activeInHierarchy,Is.True);
+            Assert.That(settingsCorners[2].x,Is.LessThanOrEqualTo(minimizeCorners[0].x-2),
+                "Settings and Minimize must be distinct hit targets in the expanded dock.");
+            Assert.That(goals.Goals,Is.Empty);
         }
 
         [UnityTest]
diff --git a/Open Empires/Assets/Tests/Shared/ScenarioEvidenceRecorder.cs b/Open Empires/Assets/Tests/Shared/ScenarioEvidenceRecorder.cs
index 5fddf93..27833b6 100644
--- a/Open Empires/Assets/Tests/Shared/ScenarioEvidenceRecorder.cs	
+++ b/Open Empires/Assets/Tests/Shared/ScenarioEvidenceRecorder.cs	
@@ -2,6 +2,7 @@ using System;
 using System.IO;
 using System.Linq;
 using System.Text;
+using System.Text.RegularExpressions;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
@@ -9,6 +10,25 @@ namespace OpenEmpires.TestSupport
 {
     public sealed class ScenarioEvidenceRecorder
     {
+        private static readonly string[] Phase5CTurnFields = {
+            "schemaVersion", "caseId", "requestId", "turnCorrelationId", "utterance", "turnText", "turnIndex", "status", "evidenceMode", "uiState",
+            "fixtureKind", "semanticValid", "semanticOutcome", "fidelity", "normalizedFields", "admittedFields", "turnHistory",
+            "draftBefore", "draftAfter", "draftChanges", "approvalRequired", "approved", "approvalMode",
+            "providerCalls", "httpAttempts", "packAccepted", "commandCount", "commandTypes", "nativeResults",
+            "taskStates", "estimatedInputTokens", "actualPromptTokens", "actualCompletionTokens",
+            "actualCachedTokens", "actualReasoningTokens", "reportedCost", "attemptCosts",
+            "uiSubmitMs", "packingMs", "transportMs", "parseMs", "admissionMs",
+            "firstNativeActionMs", "fogNewlyExploredCount", "garrisonHousedCount", "garrisonProtectedCount",
+            "garrisonUnaccommodatedCount", "futureObservedCount", "futureAssignedCount", "humanTrainCommands",
+            "resourceProgress", "gatheredFoodIncome", "foodCompletionTick", "foodIncomeAtCompletion",
+            "commanderCommandsAtFoodCompletion", "ordinaryFoodGatherActive",
+            "fixtureAssumption", "previewMentionsOwnedBase", "newResultCount", "oldUnitExcluded",
+            "humanBirthExcluded", "exactPatrolBinding", "nativePatrolActorCount",
+            "nativePatrolWaypointsMatch", "nativePatrolDestinationMatch",
+            "builderAffinityVerified", "assignedBuilderHumanProtected",
+            "ownedTaskFactCount", "readOnlyProviderRequest", "questionFactClass",
+            "errorStage", "errorCategory", "gap", "tickMode", "ticks"
+        };
         private readonly string path,run;
         private string lastStage="setup",firstFailure,firstCategory;
         private JObject diagnostics=new JObject{["initial_http_attempts"]="unavailable",["repair_http_attempts"]="unavailable",["http_status"]="unavailable",["finish_category"]="unavailable",["attribution"]="unavailable"};
@@ -45,6 +65,55 @@ namespace OpenEmpires.TestSupport
                 ["normalized_nodes"]=Normalize(result),["diagnostics"]=diagnostics.DeepClone()};
             // Never serialize provider prose, input, raw response, headers, errors,
             // context/world objects, symbolic provider node names or credentials.
+            WriteAtomic(root);
+        }
+        // A distinct bounded per-turn projection. The caller supplies only detached, allow-listed
+        // observations; the recorder never serializes provider envelopes or simulation objects.
+        public void RecordPhase5CTurn(JObject safeTurn)
+        {
+            if(safeTurn==null || safeTurn.Count>Phase5CTurnFields.Length)
+                throw new InvalidOperationException("Invalid Phase 5C turn record.");
+            foreach(var field in safeTurn.Properties())
+                if(!Phase5CTurnFields.Contains(field.Name) || !SafePhase5CValue(field.Value,0))
+                    throw new InvalidOperationException("Unsafe Phase 5C turn field.");
+            if((int?)safeTurn["schemaVersion"]!=1 || !SafeSlug((string)safeTurn["caseId"])
+                || (safeTurn["requestId"]?.Type!=JTokenType.Null && !SafeRequestId((string)safeTurn["requestId"]))
+                || (safeTurn["turnCorrelationId"]?.Type!=JTokenType.Null
+                    && safeTurn["turnCorrelationId"]!=null && !SafeRequestId((string)safeTurn["turnCorrelationId"]))
+                || safeTurn["utterance"]?.Type!=JTokenType.String
+                || ((string)safeTurn["utterance"]).Length>1500
+                || !new[]{"started","observed","failed","open-gap"}.Contains((string)safeTurn["status"]))
+                throw new InvalidOperationException("Invalid Phase 5C turn identity or state.");
+            string stage=(string)safeTurn["errorStage"];
+            if(!string.IsNullOrEmpty(stage) && firstFailure==null)
+            {firstFailure=stage;firstCategory=(string)safeTurn["errorCategory"] ?? "unclassified";}
+            var root=new JObject { ["schema"]="openempires-phase5c-scenario-turn@1",["run_id"]=run,
+                ["first_failing_stage"]=firstFailure,["first_failure_category"]=firstCategory };
+            foreach(var field in safeTurn.Properties())root[field.Name]=field.Value.DeepClone();
+            WriteAtomic(root);
+        }
+        private static bool SafeSlug(string value)=>value!=null&&Regex.IsMatch(value,@"^[a-z0-9-]{1,80}$");
+        private static bool SafeRequestId(string value)=>value!=null&&Regex.IsMatch(value,@"^[A-Za-z0-9._:-]{1,128}$");
+        private static bool SafePhase5CValue(JToken value,int depth)
+        {
+            if(value==null||depth>7)return false;
+            if(value.Type==JTokenType.Null||value.Type==JTokenType.Boolean||value.Type==JTokenType.Integer||value.Type==JTokenType.Float)return true;
+            if(value.Type==JTokenType.String)
+            {string text=(string)value;return text==null||text.Length<=1500&&!text.Any(ch=>char.IsControl(ch)&&ch!='\n'&&ch!='\r'&&ch!='\t');}
+            if(value is JArray array)return array.Count<=32&&array.All(item=>SafePhase5CValue(item,depth+1));
+            if(value is JObject objectValue)
+            {
+                if(objectValue.Count>32)return false;
+                foreach(var field in objectValue.Properties())
+                    if(!Regex.IsMatch(field.Name,@"^[A-Za-z][A-Za-z0-9_]{0,63}$")
+                        ||Regex.IsMatch(field.Name,@"(?i)(entityid|unitid|buildingid|coordinate|position|tile|header|raw|secret|password|apikey|hiddenenemy)")
+                        ||!SafePhase5CValue(field.Value,depth+1))return false;
+                return true;
+            }
+            return false;
+        }
+        private void WriteAtomic(JObject root)
+        {
             byte[] bytes=new UTF8Encoding(false,true).GetBytes(root.ToString(Formatting.Indented));
             if(bytes.Length>16384)throw new InvalidOperationException("Evidence artifact exceeds its bound.");
             CheckPaths();Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
diff --git a/backend/speech-gateway/gateway.mjs b/backend/speech-gateway/gateway.mjs
index eaa0ded..943cc9a 100644
--- a/backend/speech-gateway/gateway.mjs
+++ b/backend/speech-gateway/gateway.mjs
@@ -20,12 +20,14 @@ function semanticRequest(body) {
   if(!body||typeof body!=='object'||Array.isArray(body)||Object.keys(body).some(k=>!['model','messages','max_tokens','reasoning'].includes(k))
     ||body.model!=='openai/gpt-6-luna'||!Number.isInteger(body.max_tokens)||body.max_tokens<1||body.max_tokens>4096
     ||!Array.isArray(body.messages)||body.messages.length<1||body.messages.length>16)throw deny('INVALID_SEMANTIC_REQUEST');
-  let length=0;
+  let length=0,conservativeInput=256;
   for(const m of body.messages) {
     if(!m||Object.keys(m).some(k=>!['role','content'].includes(k))||!['system','user','assistant'].includes(m.role)||typeof m.content!=='string')throw deny('INVALID_SEMANTIC_REQUEST');
     length+=m.content.length;
+    conservativeInput+=Buffer.byteLength(m.role,'utf8')+Buffer.byteLength(m.content,'utf8');
   }
-  if(length>24576||body.reasoning!==undefined&&(Object.keys(body.reasoning).length!==1||body.reasoning.effort!=='none'))throw deny('INVALID_SEMANTIC_REQUEST');
+  // Identical decoded model-visible fields and margin as the Unity preflight; the body is forwarded unchanged.
+  if(length>24576||conservativeInput>=4000||body.reasoning!==undefined&&(Object.keys(body.reasoning).length!==1||body.reasoning.effort!=='none'))throw deny('INVALID_SEMANTIC_REQUEST');
   return body;
 }
 export function createGateway(configuration) {
diff --git a/backend/speech-gateway/gateway.test.mjs b/backend/speech-gateway/gateway.test.mjs
index 1908ecd..4b025f1 100644
--- a/backend/speech-gateway/gateway.test.mjs
+++ b/backend/speech-gateway/gateway.test.mjs
@@ -110,3 +110,18 @@ test('semantic route rejects caller endpoint/model and bounds envelope while pre
     assert.equal((await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:{...semanticHeaders,'X-Commander-Job':'44444444-4444-4444-8444-444444444444'},body:JSON.stringify({...body,url:'https://evil.example.invalid'})})).status,400);
   }finally{await f.close();}
 });
+test('semantic route applies the same conservative decoded-payload input guard and forwards unchanged',async()=>{
+  let forwarded;
+  const f=await fixture({semantic:async({body})=>{forwarded=body;return {choices:[{finish_reason:'stop',message:{content:'{}'}}]};}});
+  try{
+    const semanticHeaders={...headers,'Content-Type':'application/json','X-Commander-Job':job};
+    const safe={model:'openai/gpt-6-luna',messages:[{role:'system',content:'rules'},{role:'user',content:'order'}],max_tokens:4096,reasoning:{effort:'none'}};
+    const accepted=await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:semanticHeaders,body:JSON.stringify(safe)});
+    assert.equal(accepted.status,200);
+    assert.deepEqual(forwarded,safe);
+    const over={...safe,messages:[{role:'user',content:'界'.repeat(1500)}]};
+    const rejected=await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:{...semanticHeaders,'X-Commander-Job':'33333333-3333-4333-8333-333333333333'},body:JSON.stringify(over)});
+    assert.equal(rejected.status,400);
+    assert.deepEqual(forwarded,safe);
+  }finally{await f.close();}
+});
```

## New source/config files

### `Assets/Scripts/AI/Commander/CommanderGoalManager.NativeMove.cs`

```cs
using System;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager
    {
        private void HandleNativeMoveProcessed(NativeMoveProcessingReceipt receipt)
        {
            if (disposed || receipt == null || !ReferenceEquals(receipt.Runtime, simulation)) return;
            for (int g = 0; g < activeGoals.Count; g++)
            {
                if (!(activeGoals[g] is CommanderCapabilityGoal goal) || goal.IsTerminal
                    || !goal.CommandIssued || !ReferenceEquals(goal.RuntimeOwner, this)
                    || !(goal.IssuedCommand is MoveCommand move) || move.UnitIds == null) continue;
                int index = Array.IndexOf(move.UnitIds, receipt.UnitId);
                if (index < 0 || goal.NativeMovementProcessed == null
                    || index >= goal.NativeMovementProcessed.Length) continue;
                if (!ReferenceEquals(receipt.OriginalCommand, goal.IssuedCommand))
                {
                    if (goal.NativeMovementProcessed[index]) goal.NativeMovementReplaced = true;
                    continue;
                }
                if (receipt.Tick < goal.CommandIssuedSimulationTick
                    || goal.NativeMovementProcessed[index])
                { goal.NativeMovementReplaced = true; continue; }
                goal.NativeMovementProcessed[index] = true;
                goal.NativeMovementEndpoints[index] = receipt.Endpoint;
                goal.NativeMovementResults[index] = receipt.Result;
            }
        }

        private void HandleNativeUnitCommandProcessed(NativeUnitCommandObservation observation)
        {
            if (disposed || observation == null || !ReferenceEquals(observation.Runtime, simulation)) return;
            int[] subjects = CommanderWorkerAuthority.GetSubjectUnitIds(observation.ExecutingCommand);
            if (subjects == null || subjects.Length == 0) return;
            for (int g = 0; g < activeGoals.Count; g++)
            {
                if (!(activeGoals[g] is CommanderCapabilityGoal goal) || goal.IsTerminal
                    || !goal.CommandIssued || !ReferenceEquals(goal.RuntimeOwner, this)
                    || ReferenceEquals(observation.OriginalCommand, goal.IssuedCommand)
                    || !(goal.IssuedCommand is MoveCommand move) || move.UnitIds == null
                    || goal.NativeMovementProcessed == null) continue;
                for (int i = 0; i < move.UnitIds.Length; i++)
                    if (goal.NativeMovementProcessed[i] && Array.IndexOf(subjects, move.UnitIds[i]) >= 0)
                    { goal.NativeMovementReplaced = true; break; }
            }
        }
    }
}
```

### `Assets/Scripts/AI/Commander/CommanderGoalManager.NativeMove.cs.meta`

```meta
fileFormatVersion: 2
guid: 5c9a20364893b064cb8665796bd48330
```

### `Assets/Scripts/AI/Commander/CommanderPlanner.Discovery.cs`

```cs
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
```

### `Assets/Scripts/AI/Commander/CommanderPlanner.Discovery.cs.meta`

```meta
fileFormatVersion: 2
guid: 96ae2ef5b6b154c46bcb986ebb2461c0
```

### `Assets/Scripts/AI/Commander/CommanderPlanner.DynamicMove.cs`

```cs
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
```

### `Assets/Scripts/AI/Commander/CommanderPlanner.DynamicMove.cs.meta`

```meta
fileFormatVersion: 2
guid: 6a3aa6a611e60674e85f3142b4bbf744
```

### `Assets/Scripts/AI/Commander/CommanderPlanner.Garrison.cs`

```cs
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
                if (goal.ResultSourceGoal != null || goal.RequiresResultBinding)
                {
                    if (!(goal.ResultSourceGoal is EnsureUnitCountGoal producer)
                        || producer.RequestedUnitType != 0 || !producer.IsExplicitNewProduction
                        || producer.RequiredNewProductionCount != goal.Action.UnitSelector.Count
                        || goal.Action.UnitSelector.AllMatching
                        || !(goal.Action.UnitSelector.Kind == CommanderUnitSelectorKind.Villagers
                            || goal.Action.UnitSelector.Kind == CommanderUnitSelectorKind.UnitType
                                && goal.Action.UnitSelector.UnitType == 0))
                        return new CommanderPlan(CommanderGoalStatus.Blocked,
                            "The exact produced villager result is incompatible with this shelter request; no selector fallback is allowed.", 0, 0);
                    if (producer.ResultCaptureTick < 0 || producer.ResultUnitIds == null
                        || producer.ResultUnitIds.Count != producer.RequiredNewProductionCount)
                        return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                            "Waiting for the exact produced villagers; no selector fallback is allowed.", 0, 0);
                    int[] resultIds = producer.ResultUnitIds.ToArray();
                    if (resultIds.Distinct().Count() != resultIds.Length)
                        return new CommanderPlan(CommanderGoalStatus.Blocked,
                            "The produced villager result contains duplicate identities; no shelter order was issued.", 0, 0);
                    if (resultIds.Any(id =>
                    {
                        UnitData unit = simulation.UnitRegistry.GetUnit(id);
                        return unit == null || !unit.IsVillager || unit.IsSheep;
                    }))
                        return new CommanderPlan(CommanderGoalStatus.Blocked,
                            "The exact producer result is not a current villager set; no substitute was selected.", 0, 0);
                    goal.GarrisonCandidateIds = resultIds;
                }
                else
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
```

### `Assets/Scripts/AI/Commander/CommanderPlanner.Garrison.cs.meta`

```meta
fileFormatVersion: 2
guid: d2168c454b067f3478cf9fa69685a851
```

### `Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs`

```cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Detached provider proposal. It carries typed, bounded data, never authority or a runtime selector.
    public sealed class CommanderRequestGraphDraft
    {
        public sealed class Slot
        {
            private readonly List<JToken> alternatives;
            public int Node { get; }
            public string Path { get; }
            public string Type { get; }
            public IReadOnlyList<JToken> Alternatives
            {
                get
                {
                    var detached = new List<JToken>();
                    foreach (var value in alternatives) detached.Add(value.DeepClone());
                    return detached.AsReadOnly();
                }
            }
            internal Slot(int node, string path, string type, IList<JToken> alternatives)
            {
                Node = node; Path = path; Type = type; this.alternatives = new List<JToken>();
                foreach (var value in alternatives) this.alternatives.Add(value.DeepClone());
            }
        }

        private readonly JObject graph;
        private readonly List<Slot> slots;
        private readonly List<KeyValuePair<string, string>> nameConfirmations;
        private readonly string unresolvedNameRequirements;
        internal bool IsNameConfirmation => nameConfirmations != null;
        internal string NameConfirmationQuestion => nameConfirmations.Count == 0
            ? "Blocked: the proposed content does not uniquely match the unresolved similar name. Original request retained; no action was submitted."
            : "Did you mean " + nameConfirmations[0].Value + " for that similar name? Confirm the name with yes or its name, or no to dismiss. Gameplay approval is separate.";
        public IReadOnlyList<Slot> MissingSlots => slots.AsReadOnly();
        public int NodeCount => (graph["nodes"] as JArray)?.Count ?? 0;
        private CommanderRequestGraphDraft(JObject graph, List<Slot> slots, string unresolvedNames = null)
        { this.graph = (JObject)graph.DeepClone(); this.slots = slots; unresolvedNameRequirements = unresolvedNames; }

        internal CommanderRequestGraphDraft WithNameRequirements(JArray hints)
            => new CommanderRequestGraphDraft(graph, slots, hints.ToString(Formatting.None));

        private CommanderRequestGraphDraft(JObject proposal, List<KeyValuePair<string, string>> confirmations)
            : this(proposal, new List<Slot>()) { nameConfirmations = confirmations; }

        // Only called after strict provider parsing. Retain the entire proposal, including
        // the DynamicPlan envelope, and re-enter the same parser/admission path locally.
        internal static CommanderRequestGraphDraft ConfirmNames(string strictProposal, JArray hints)
        {
            var root = JObject.Parse(strictProposal);
            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
            var confirmations = new List<KeyValuePair<string, string>>();
            foreach (JObject hint in hints)
            {
                var matches = new List<KeyValuePair<string, string>>();
                foreach (string id in ((JArray)hint["unitCandidates"]).Concat((JArray)hint["buildingCandidates"]).Values<string>())
                {
                    var unit = catalog.FindUnit(id);
                    var building = catalog.FindBuilding(id);
                    string display = unit?.DisplayName ?? building?.DisplayName;
                    if (display == null) continue;
                    bool named = root.Descendants().OfType<JProperty>().Any(p => p.Value.Type == JTokenType.String &&
                        (unit != null ? p.Name == "unit" : p.Name == "structure" || p.Name == "producer" || p.Name == "building") &&
                        ((string)p.Value == id || (unit != null
                            ? CommanderIntentCatalog.TryResolveUnit((string)p.Value, out int u) && u == unit.UnitType
                            : CommanderIntentCatalog.TryResolveStructure((string)p.Value, out BuildingType b) && b == building.BuildingType)));
                    if (named) matches.Add(new KeyValuePair<string, string>(id, display));
                }
                // An omitted, unrelated, or multiply interpreted suggestion cannot be approved.
                if (matches.Count != 1) return new CommanderRequestGraphDraft(root, new List<KeyValuePair<string, string>>());
                if (!confirmations.Any(c => c.Key == matches[0].Key)) confirmations.Add(matches[0]);
            }
            return new CommanderRequestGraphDraft(root, confirmations);
        }

        internal static CommanderRequestGraphDraft Parse(JObject root)
        {
            if (!(root["pending"] is JObject graph) || !(graph["nodes"] is JArray nodes)
                || nodes.Count < 1 || nodes.Count > CommanderSemanticJson.MaximumNodes
                || !(root["missingSlots"] is JArray sourceSlots) || sourceSlots.Count < 1 || sourceSlots.Count > 4)
                throw new JsonException();
            ExactFields(graph, "executionOrder", "nodes");
            string order = (string)graph["executionOrder"];
            if (order != "Independent" && order != "Sequential" && order != "DependencyGraph") throw new JsonException();
            var slots = new List<Slot>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in sourceSlots)
            {
                if (!(entry is JObject spec)) throw new JsonException();
                ExactFields(spec, "node", "path", "type", "alternatives");
                if (spec["node"]?.Type != JTokenType.Integer || spec["path"]?.Type != JTokenType.String
                    || spec["type"]?.Type != JTokenType.String || !(spec["alternatives"] is JArray alternatives)
                    || alternatives.Count < 1 || alternatives.Count > 8) throw new JsonException();
                int index = (int)spec["node"];
                string path = (string)spec["path"], type = (string)spec["type"];
                if (index < 0 || index >= nodes.Count || !(nodes[index] is JObject node)
                    || !Allowed(type, path) || !seen.Add(index + ":" + path)) throw new JsonException();
                JObject parent = Parent(node, path);
                string leaf = Leaf(path);
                if (parent == null || type == "Confirmation" && parent[leaf]?.Type != JTokenType.String
                    || type != "Confirmation" && parent.Property(leaf) != null) throw new JsonException();
                var values = new List<JToken>();
                foreach (var choice in alternatives)
                {
                    if (choice == null || (choice.Type != JTokenType.String && choice.Type != JTokenType.Integer)
                        || choice.ToString().Length > 64 || type == "Count" && choice.Type != JTokenType.Integer
                        || type != "Count" && choice.Type != JTokenType.String
                        || type == "Confirmation" && (string)choice != (string)parent[leaf]) throw new JsonException();
                    values.Add(choice.DeepClone());
                }
                slots.Add(new Slot(index, path, type, values));
            }
            var draft = new CommanderRequestGraphDraft(graph, slots);
            // Every offered completion, not just the first, must survive the existing strict
            // parser and graph checks. Temporary values never enter the stored graph.
            foreach (var slot in slots)
                foreach (var alternative in slot.Alternatives)
                    if (!draft.Probe(slot, alternative).IsValid) throw new JsonException();
            return draft;
        }

        private CommanderSemanticResult Probe(Slot changed, JToken value)
            => CommanderSemanticJson.ParseProviderResponse(CompletedProposal(changed, value).ToString(Formatting.None));

        private JObject CompletedProposal(Slot changed, JToken value)
        {
            var completed = (JObject)graph.DeepClone();
            foreach (var slot in slots)
            {
                var parent = Parent((JObject)((JArray)completed["nodes"])[slot.Node], slot.Path);
                if (slot.Type != "Confirmation") parent[Leaf(slot.Path)] = (slot == changed ? value : slot.Alternatives[0]).DeepClone();
            }
            return new JObject { ["outcome"] = "Request",
                ["executionOrder"] = completed["executionOrder"].DeepClone(),
                ["nodes"] = completed["nodes"].DeepClone() };
        }

        // Game-owned metadata is never serialized into provider fields. Each local
        // transition retains it; only an explicit typed name choice resolves a hint.
        private CommanderSemanticResult ParseLocal(JObject proposal, string selectedPath, JToken selectedValue)
        {
            string json = proposal.ToString(Formatting.None);
            var parsed = CommanderSemanticJson.ParseProviderResponse(json);
            if (!parsed.IsValid || string.IsNullOrEmpty(unresolvedNameRequirements)) return parsed;
            var hints = JArray.Parse(unresolvedNameRequirements);
            string selectedId = null;
            string candidates = null;
            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
            if (selectedValue?.Type == JTokenType.String)
            {
                if (selectedPath == "unit" && CommanderIntentCatalog.TryResolveUnit((string)selectedValue, out int unit))
                { selectedId = catalog.FindUnitByType(unit)?.StableId; candidates = "unitCandidates"; }
                else if ((selectedPath == "structure" || selectedPath == "producer")
                    && CommanderIntentCatalog.TryResolveStructure((string)selectedValue, out BuildingType building))
                { selectedId = catalog.Buildings.FirstOrDefault(b => b.BuildingType == building)?.StableId; candidates = "buildingCandidates"; }
            }
            if (selectedId != null)
                foreach (var hint in hints.OfType<JObject>().Where(h => ((JArray)h[candidates]).Values<string>().Contains(selectedId)).ToList())
                    hint.Remove();
            return parsed.RequireNameConfirmation(json, hints);
        }

        internal CommanderSemanticResult Resolve(string reply)
        {
            if (IsNameConfirmation)
            {
                if (nameConfirmations.Count == 0) return null;
                var proposed = nameConfirmations[0];
                string whole = (reply ?? string.Empty).Trim();
                if (!string.Equals(whole, "yes", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(whole, "y", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(whole, proposed.Value, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(whole, proposed.Key, StringComparison.Ordinal)) return null;
                if (nameConfirmations.Count > 1)
                    return CommanderSemanticResult.NameConfirmation(new CommanderRequestGraphDraft(graph, nameConfirmations.Skip(1).ToList()));
                return CommanderSemanticJson.ParseProviderResponse(graph.ToString(Formatting.None));
            }
            if (IsCorrectionUtterance(reply)) return Correct(reply);
            Slot slot = null;
            JToken chosen = null;
            foreach (var candidate in slots)
            {
                JToken match = Match(candidate, reply);
                if (match == null) continue;
                if (slot != null) return null; // A reply matching two distinct slots is ambiguous.
                slot = candidate; chosen = match;
            }
            if (slot == null) return null;
            if (slots.Count == 1)
            {
                var parsed = ParseLocal(CompletedProposal(slot, chosen), slot.Path, chosen);
                return parsed.IsValid ? parsed : null;
            }
            var next = (JObject)graph.DeepClone();
            if (slot.Type != "Confirmation")
                Parent((JObject)((JArray)next["nodes"])[slot.Node], slot.Path)[Leaf(slot.Path)] = chosen.DeepClone();
            var remaining = new JArray();
            foreach (var other in slots)
                if (!ReferenceEquals(other, slot))
                    remaining.Add(new JObject { ["node"] = other.Node, ["path"] = other.Path,
                        ["type"] = other.Type, ["alternatives"] = new JArray(other.Alternatives) });
            return ParseLocal(new JObject { ["outcome"] = "Clarify",
                ["message"] = "Please specify the remaining detail.", ["pending"] = next,
                ["missingSlots"] = remaining }, slot.Path, chosen);
        }

        internal static bool IsCorrectionUtterance(string reply)
        {
            string[] correctionWords = Words(reply);
            if (correctionWords.Length >= 3 && correctionWords[0] == "make" && correctionWords[1] == "that") return true;
            foreach (var word in correctionWords)
                if (word == "instead" || word == "actually" || word == "change" || word == "rather") return true;
            return false;
        }

        private sealed class Change
        {
            internal int Node;
            internal string Path;
            internal JToken Value;
        }

        private CommanderSemanticResult Correct(string reply)
        {
            if (!TryAffirmativeCorrection(reply, out string replacement, out string prior)) return null;
            string[] words = Words(replacement);
            if (words.Length == 0 || words.Length > 12) return null;
            var changes = new List<Change>();
            var nodes = (JArray)graph["nodes"];
            for (int start = 0; start < words.Length; start++)
                for (int length = 1; length <= 3 && start + length <= words.Length; length++)
                {
                    string phrase = string.Join(" ", words, start, length);
                    if (CommanderIntentCatalog.TryResolveStructure(phrase, out BuildingType building))
                    {
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            AddChange(changes, i, (JObject)nodes[i], "structure", new JValue(building.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "producer", new JValue(building.ToString()));
                        }
                    }
                    if (CommanderIntentCatalog.TryResolveUnit(phrase, out int unitType))
                    {
                        string canonical = CommanderIntentCatalog.GetUnitDisplayName(unitType);
                        for (int i = 0; i < nodes.Count; i++)
                            AddChange(changes, i, (JObject)nodes[i], "unit", new JValue(canonical));
                    }
                    if (CommanderIntentCatalog.TryResolveResource(phrase, out ResourceType resource))
                    {
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            AddChange(changes, i, (JObject)nodes[i], "resource", new JValue(resource.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "destination.resource", new JValue(resource.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "placement.resource", new JValue(resource.ToString()));
                        }
                    }
                    string sourceName = SourceAlias(phrase);
                    if (Enum.TryParse(sourceName, true, out ResourceSourceKind sourceKind)
                        && Enum.IsDefined(typeof(ResourceSourceKind), sourceKind))
                    {
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            AddChange(changes, i, (JObject)nodes[i], "sourceKind", new JValue(sourceKind.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "destination.sourceKind", new JValue(sourceKind.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "placement.sourceKind", new JValue(sourceKind.ToString()));
                        }
                    }
                }
            int? number = null;
            foreach (var word in words)
                if (CommanderClarificationReplies.TryParseCount(word, out int parsed))
                {
                    if (number.HasValue && number.Value != parsed) return null;
                    number = parsed;
                }
            if (number.HasValue)
            {
                bool workers = Contains(words, "villager") || Contains(words, "villagers") || Contains(words, "worker") || Contains(words, "workers");
                bool amount = prior == "amount" || Contains(words, "additional") || Contains(words, "stockpile") || Contains(words, "food")
                    || Contains(words, "wood") || Contains(words, "gold") || Contains(words, "stone");
                if (workers && amount) return null;
                int fields = 0;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = (JObject)nodes[i];
                    if (node["count"]?.Type == JTokenType.Integer && !amount) fields++;
                    if (node["resourceAmount"]?.Type == JTokenType.Integer && !workers) fields++;
                }
                if (fields != 1) return null;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = (JObject)nodes[i];
                    if (node["count"]?.Type == JTokenType.Integer && !amount)
                        AddChange(changes, i, node, "count", new JValue(number.Value));
                    if (node["resourceAmount"]?.Type == JTokenType.Integer && !workers)
                        AddChange(changes, i, node, "resourceAmount", new JValue(number.Value));
                }
            }
            if (changes.Count != 1) return null;
            Change change = changes[0];
            if (prior != null && !(prior == "amount" && change.Path == "resourceAmount")
                && !NamesCurrentValue(prior, change)) return null;
            if (change.Path == "resourceAmount" && prior != "amount"
                && !AmountQualifierMatchesGraph(replacement, change.Node)) return null;
            var next = (JObject)graph.DeepClone();
            Parent((JObject)((JArray)next["nodes"])[change.Node], change.Path)[Leaf(change.Path)] = change.Value.DeepClone();
            var remaining = new JArray();
            foreach (var slot in slots)
            {
                if (slot.Node == change.Node && slot.Path == change.Path) continue;
                remaining.Add(new JObject { ["node"] = slot.Node, ["path"] = slot.Path,
                    ["type"] = slot.Type, ["alternatives"] = new JArray(slot.Alternatives) });
            }
            if (remaining.Count == 0)
                return ParseLocal(new JObject { ["outcome"] = "Request",
                    ["executionOrder"] = next["executionOrder"].DeepClone(),
                    ["nodes"] = next["nodes"].DeepClone() }, change.Path, change.Value);
            return ParseLocal(new JObject { ["outcome"] = "Clarify",
                ["message"] = "Please specify the remaining detail.", ["pending"] = next,
                ["missingSlots"] = remaining }, change.Path, change.Value);
        }

        // Correction-like wording keeps a pending draft local, but only these complete,
        // affirmative shapes are allowed to change one typed leaf. Unknown language fails closed.
        private static bool TryAffirmativeCorrection(string reply, out string replacement, out string prior)
        {
            replacement = null; prior = null;
            string whole = Regex.Replace((reply ?? string.Empty).Trim().TrimEnd('.', '!', '?').Trim().ToLowerInvariant(), @"\s+", " ");
            if (!Regex.IsMatch(whole, @"^[a-z0-9 ]+$")) return false;
            for (int i = 0; i < 2; i++)
            {
                if (whole.StartsWith("please ", StringComparison.Ordinal)) whole = whole.Substring(7);
                else if (whole.StartsWith("actually ", StringComparison.Ordinal)) whole = whole.Substring(9);
                else break;
            }
            if (whole.StartsWith("change ", StringComparison.Ordinal))
            {
                string rest = whole.Substring(7);
                int separator = rest.IndexOf(" to ", StringComparison.Ordinal);
                if (separator <= 0 || rest.IndexOf(" to ", separator + 4, StringComparison.Ordinal) >= 0) return false;
                prior = rest.Substring(0, separator);
                replacement = rest.Substring(separator + 4);
                if (prior.StartsWith("the ", StringComparison.Ordinal)) prior = prior.Substring(4);
                if (prior.StartsWith("a ", StringComparison.Ordinal)) prior = prior.Substring(2);
                if (prior == "amount" && CommanderClarificationReplies.TryParseCount(replacement, out int _)) return true;
                if (!IsTypedCorrectionValue(prior) || !IsTypedCorrectionValue(replacement)) return false;
                return true;
            }
            if (whole.StartsWith("make that ", StringComparison.Ordinal))
            {
                replacement = whole.Substring(10);
                return IsTypedCorrectionValue(replacement);
            }
            if (!whole.EndsWith(" instead", StringComparison.Ordinal)) return false;
            replacement = whole.Substring(0, whole.Length - 8);
            if (replacement.StartsWith("build a ", StringComparison.Ordinal)) replacement = replacement.Substring(8);
            else if (replacement.StartsWith("make a ", StringComparison.Ordinal)) replacement = replacement.Substring(7);
            else if (replacement.StartsWith("a ", StringComparison.Ordinal)) replacement = replacement.Substring(2);
            else if (replacement.StartsWith("an ", StringComparison.Ordinal)) replacement = replacement.Substring(3);
            return IsTypedCorrectionValue(replacement);
        }

        private static bool IsTypedCorrectionValue(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase) || Words(phrase).Length > 3) return false;
            if (CommanderIntentCatalog.TryResolveStructure(phrase, out BuildingType _) ||
                CommanderIntentCatalog.TryResolveUnit(phrase, out int _) ||
                CommanderIntentCatalog.TryResolveResource(phrase, out ResourceType _)) return true;
            if (Enum.TryParse(SourceAlias(phrase), true, out ResourceSourceKind source)
                && Enum.IsDefined(typeof(ResourceSourceKind), source)) return true;
            string[] words = Words(phrase);
            if (words.Length < 2 || !CommanderClarificationReplies.TryParseCount(words[0], out int _)) return false;
            if (words.Length == 2 && (words[1] == "villager" || words[1] == "villagers"
                || words[1] == "worker" || words[1] == "workers")) return true;
            int resourceIndex = words[1] == "additional" || words[1] == "stockpile" ? 2 : 1;
            return words.Length == resourceIndex + 1 &&
                (words[resourceIndex] == "food" || words[resourceIndex] == "wood"
                    || words[resourceIndex] == "gold" || words[resourceIndex] == "stone");
        }

        private bool NamesCurrentValue(string prior, Change change)
        {
            JToken current = Parent((JObject)((JArray)graph["nodes"])[change.Node], change.Path)?[Leaf(change.Path)];
            if (current == null) return false;
            string leaf = Leaf(change.Path);
            if (leaf == "structure" || leaf == "producer")
                return CommanderIntentCatalog.TryResolveStructure(prior, out BuildingType building)
                    && string.Equals((string)current, building.ToString(), StringComparison.OrdinalIgnoreCase);
            if (leaf == "unit")
                return CommanderIntentCatalog.TryResolveUnit(prior, out int unit)
                    && string.Equals((string)current, CommanderIntentCatalog.GetUnitDisplayName(unit), StringComparison.OrdinalIgnoreCase);
            if (leaf == "resource")
                return CommanderIntentCatalog.TryResolveResource(prior, out ResourceType resource)
                    && string.Equals((string)current, resource.ToString(), StringComparison.OrdinalIgnoreCase);
            if (leaf == "sourceKind")
                return string.Equals((string)current, SourceAlias(prior), StringComparison.OrdinalIgnoreCase);
            string[] words = Words(prior);
            if (words.Length < 2 || !CommanderClarificationReplies.TryParseCount(words[0], out int number)
                || (int)current != number) return false;
            if (leaf == "count") return words.Length == 2 && (words[1] == "villager" || words[1] == "villagers"
                || words[1] == "worker" || words[1] == "workers");
            return leaf == "resourceAmount" && AmountQualifierMatchesGraph(prior, change.Node);
        }

        private bool AmountQualifierMatchesGraph(string phrase, int nodeIndex)
        {
            string[] words = Words(phrase);
            if (words.Length < 2 || words.Length > 3
                || !CommanderClarificationReplies.TryParseCount(words[0], out int _)
                || !CommanderIntentCatalog.TryResolveResource(words[words.Length - 1], out ResourceType namedResource)) return false;
            var node = (JObject)((JArray)graph["nodes"])[nodeIndex];
            if (!string.Equals((string)node["destination"]?["resource"], namedResource.ToString(),
                StringComparison.OrdinalIgnoreCase)) return false;
            if (words.Length == 2) return true; // An omitted mode cannot rewrite the retained mode.
            string namedMode = words[1] == "additional" ? "AdditionalGathered"
                : words[1] == "stockpile" ? "Stockpile" : null;
            return namedMode != null && string.Equals((string)node["resourceAmountMode"], namedMode,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void AddChange(List<Change> changes, int index, JObject node, string path, JToken replacement)
        {
            JObject parent = Parent(node, path);
            JToken current = parent?[Leaf(path)];
            if (current == null || current.Type != replacement.Type || JToken.DeepEquals(current, replacement)) return;
            foreach (var existing in changes)
                if (existing.Node == index && existing.Path == path && JToken.DeepEquals(existing.Value, replacement)) return;
            changes.Add(new Change { Node = index, Path = path, Value = replacement });
        }

        private static bool Contains(string[] words, string word)
        { foreach (string entry in words) if (entry == word) return true; return false; }

        private static string[] Words(string text)
        { return (text ?? string.Empty).Trim().ToLowerInvariant().Split(
            new[] { ' ', '\t', '\r', '\n', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries); }

        private static JToken Match(Slot slot, string reply)
        {
            // Only terminal sentence punctuation is presentation, not a slot value.
            // Preserve the original reply in the retained request and evidence.
            string whole = (reply ?? string.Empty).Trim().TrimEnd('.', '!', '?').Trim().ToLowerInvariant();
            JToken chosen = null;
            if (slot.Type == "Confirmation" && (whole == "yes" || whole == "y")) chosen = slot.Alternatives[0];
            else if (slot.Type == "Count" && CommanderClarificationReplies.TryParseCount(reply, out int count)) chosen = new JValue(count);
            else
            {
                string canonical = slot.Type == "SourceKind" ? SourceAlias(whole) : whole;
                foreach (var offered in slot.Alternatives)
                    if (string.Equals((string)offered, canonical, StringComparison.OrdinalIgnoreCase)) { chosen = offered; break; }
            }
            return chosen;
        }

        private static string SourceAlias(string reply)
        {
            switch (reply)
            {
                case "from trees": case "trees": case "tree": case "from tree": return "Tree";
                case "from berries": case "berries": case "berry": return "Berries";
                case "from sheep": case "sheep": return "Sheep";
                case "from farms": case "farms": case "farm": return "Farm";
                case "from gold mines": case "gold mines": case "gold mine": return "GoldMine";
                case "from stone mines": case "stone mines": case "stone mine": return "StoneMine";
                default: return reply;
            }
        }

        internal JObject ToPendingJson() => (JObject)graph.DeepClone();
        internal JArray ToSlotsJson()
        {
            var result = new JArray();
            foreach (var slot in slots)
                result.Add(new JObject { ["node"] = slot.Node, ["path"] = slot.Path,
                    ["type"] = slot.Type, ["alternatives"] = new JArray(slot.Alternatives) });
            return result;
        }

        private static bool Allowed(string type, string path)
        {
            if (type == "Confirmation") return path == "structure" || path == "unit" || path == "producer";
            if (type == "Count") return path == "count";
            if (type == "SourceKind") return path == "sourceKind" || path == "destination.sourceKind" || path == "placement.sourceKind";
            if (type == "CanonicalName") return path == "structure" || path == "unit" || path == "producer"
                || path == "technology" || path == "resource" || path == "destination.resource" || path == "placement.resource";
            return false;
        }
        private static JObject Parent(JObject node, string path)
        {
            int dot = path.IndexOf('.');
            return dot < 0 ? node : node[path.Substring(0, dot)] as JObject;
        }
        private static string Leaf(string path)
        { int dot = path.IndexOf('.'); return dot < 0 ? path : path.Substring(dot + 1); }
        private static void ExactFields(JObject obj, params string[] fields)
        { foreach (var property in obj.Properties()) if (Array.IndexOf(fields, property.Name) < 0) throw new JsonException(); }
    }
}
```

### `Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs.meta`

```meta
fileFormatVersion: 2
guid: 79ec3c9201926094fa0d19d74bab4f43
```

### `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.EvidenceCapture.cs`

```cs
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private const int MaximumEvidenceScreenshotsPerSession = 3;
        private int evidenceScreenshotCount;
        private bool evidenceScreenshotInProgress;
        private string nextEvidenceKind = "manual";
        private readonly List<string> offlineEvidenceCapturePaths = new List<string>();

        // An explicit local UI action only. It does not invoke the provider, issue orders,
        // start audio, or serialize Commander context. Browser evidence uses the browser's
        // own screenshot route; WebGL's virtual persistent store is not a user download.
        public string LastEvidenceScreenshotPath { get; private set; }
        public string LastEvidenceScreenshotKind { get; private set; }
        public int LastEvidenceScreenshotWidth { get; private set; }
        public int LastEvidenceScreenshotHeight { get; private set; }
        public IReadOnlyList<string> OfflineEvidenceCapturePaths => offlineEvidenceCapturePaths.AsReadOnly();

        private sealed class OfflineEvidenceProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private const string SyntheticTwoHouses = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            internal int Calls { get; private set; }
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,
                CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Calls++;
                return Task.FromResult(Calls == 1 && request?.PlayerMessage == "Build two Houses near my Town Center."
                    ? CommanderSemanticJson.ParseProviderResponse(SyntheticTwoHouses)
                    : CommanderSemanticResult.ProviderRejected("Offline fixture accepts exactly one declared request."));
            }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
                => Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "Offline fixture does not use the legacy provider route."));
        }

        private static bool ShouldStartCliOfflineFixture()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "--commander-evidence-offline-ui-fixture") return true;
#endif
            return false;
        }

        private IEnumerator CaptureOfflineFixtureOnce()
        {
            offlineEvidenceCapturePaths.Clear();
            AppendLine("Commander", "OFFLINE FIXTURE UI EVIDENCE — synthetic response; not live Luna or English-accuracy proof.", false);
            OpenCommander();
            var request = SubmitMessageAsync("Build two Houses near my Town Center.");
            for (int frame = 0; frame < 120 && !request.IsCompleted; frame++) yield return null;
            if (!request.IsCompleted || request.IsFaulted || request.IsCanceled || pendingActionPlan == null)
                yield break;
            yield return CaptureOfflineFrame("offline-fixture-pending-approval");
            if (offlineEvidenceCapturePaths.Count != 1) yield break;

            // Distinct UI turn; only the explicit fixture flag authorizes this normal admission.
            var approval = SubmitMessageAsync("Approve plan");
            for (int frame = 0; frame < 120 && !approval.IsCompleted; frame++) yield return null;
            if (!approval.IsCompleted || approval.IsFaulted || approval.IsCanceled
                || semanticGoalManager == null || semanticGoalManager.Goals.Count == 0) yield break;
            tasksTabButton?.onClick.Invoke();
            yield return null;
            yield return CaptureOfflineFrame("offline-fixture-tasks");
            if (offlineEvidenceCapturePaths.Count != 2) yield break;

            chatTabButton?.onClick.Invoke();
            var status = SubmitMessageAsync("strategy status"); // local read-only, zero provider calls
            for (int frame = 0; frame < 120 && !status.IsCompleted; frame++) yield return null;
            if (!status.IsCompleted || status.IsFaulted || status.IsCanceled) yield break;
            yield return null;
            yield return CaptureOfflineFrame("offline-fixture-chat");
        }

        private IEnumerator CaptureOfflineFrame(string kind)
        {
            string previous = LastEvidenceScreenshotPath;
            nextEvidenceKind = kind;
            if (!RequestEvidenceScreenshot()) yield break;
            for (int frame = 0; frame < 120 && evidenceScreenshotInProgress; frame++) yield return null;
            if (!evidenceScreenshotInProgress && LastEvidenceScreenshotPath != previous
                && LastEvidenceScreenshotKind == kind)
                offlineEvidenceCapturePaths.Add(LastEvidenceScreenshotPath);
        }

        private static bool ShouldStartCliEvidenceCapture()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "--commander-evidence-status-capture") return true;
#endif
            return false;
        }

        private IEnumerator CaptureStatusEvidenceOnce()
        {
            // Exact local information route in SubmitMessageAsync: no semantic provider,
            // no gameplay order, no microphone. This runs only with an explicit CLI flag.
            var status = SubmitMessageAsync("strategy status");
            while (!status.IsCompleted) yield return null;
            if (status.IsFaulted || status.IsCanceled) yield break;
            yield return null;
            nextEvidenceKind = "status-only-local";
            RequestEvidenceScreenshot();
        }

        public bool RequestEvidenceScreenshot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            if (!Application.isPlaying || evidenceScreenshotInProgress
                || evidenceScreenshotCount >= MaximumEvidenceScreenshotsPerSession
                || canvasRoot == null || !canvasRoot.activeInHierarchy)
                return false;
            CloseSettingsMenu();
            OpenCommander();
            evidenceScreenshotInProgress = true;
            StartCoroutine(CaptureEvidenceScreenshotAtEndOfFrame());
            return true;
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private IEnumerator CaptureEvidenceScreenshotAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            string temporary = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null) throw new InvalidOperationException("Screenshot unavailable.");
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length < 8 || png.Length > 16 * 1024 * 1024)
                    throw new InvalidOperationException("Screenshot encoding unavailable or out of bounds.");
                string directory = Path.Combine(Application.persistentDataPath, "CommanderEvidence");
                Directory.CreateDirectory(directory);
                string kind = nextEvidenceKind;
                string destination = Path.Combine(directory, "commander-ui-" + kind + "-"
                    + texture.width + "x" + texture.height + "-"
                    + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".png");
                temporary = destination + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(png, 0, png.Length); stream.Flush(true); }
                File.Move(temporary, destination); // unique destination, no overwrite
                temporary = null;
                LastEvidenceScreenshotPath = destination;
                LastEvidenceScreenshotKind = kind;
                LastEvidenceScreenshotWidth = texture.width;
                LastEvidenceScreenshotHeight = texture.height;
                evidenceScreenshotCount++;
                Debug.Log("[Commander] Explicit local UI evidence screenshot saved.");
            }
            catch (Exception)
            {
                Debug.LogWarning("[Commander] Local UI evidence screenshot could not be saved.");
            }
            finally
            {
                if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
                if (texture != null) Destroy(texture);
                nextEvidenceKind = "manual";
                evidenceScreenshotInProgress = false;
            }
        }
#endif
    }
}
```

### `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.EvidenceCapture.cs.meta`

```meta
fileFormatVersion: 2
guid: 84f9821d9cee9624d8484bb890c70ef7
```

### `Assets/Scripts/AI/Commander/Phase5C.meta`

```meta
fileFormatVersion: 2
guid: 12ac7357bf8384d4a89468a75d7e2c23
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

### `Assets/Scripts/AI/Commander/Phase5C/CommanderProviderUsage.cs`

```cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Reported observations only: no headers, response text, credentials or game authority.
    public sealed class CommanderProviderUsage
    {
        public long? PromptTokens { get; }
        public long? CompletionTokens { get; }
        public long? TotalTokens { get; }
        public long? CachedTokens { get; }
        public long? ReasoningTokens { get; }
        public decimal? Cost { get; }
        public bool HasReportedUsage { get; }
        public static CommanderProviderUsage Unknown { get; } = new CommanderProviderUsage(null);

        private CommanderProviderUsage(JObject value)
        {
            PromptTokens = NonnegativeInteger(value?["prompt_tokens"]);
            CompletionTokens = NonnegativeInteger(value?["completion_tokens"]);
            TotalTokens = NonnegativeInteger(value?["total_tokens"]);
            CachedTokens = NonnegativeInteger((value?["prompt_tokens_details"] as JObject)?["cached_tokens"]);
            ReasoningTokens = NonnegativeInteger((value?["completion_tokens_details"] as JObject)?["reasoning_tokens"]);
            try
            {
                JToken cost = value?["cost"];
                if (cost != null && (cost.Type == JTokenType.Float || cost.Type == JTokenType.Integer))
                { decimal amount = cost.Value<decimal>(); if (amount >= 0) Cost = amount; }
            }
            catch (Exception error) when (error is OverflowException || error is FormatException || error is InvalidCastException) { }
            HasReportedUsage = PromptTokens.HasValue || CompletionTokens.HasValue || TotalTokens.HasValue
                || CachedTokens.HasValue || ReasoningTokens.HasValue || Cost.HasValue;
        }

        public static CommanderProviderUsage Read(JObject envelope)
            => envelope?["usage"] is JObject usage ? new CommanderProviderUsage(usage) : Unknown;

        private static long? NonnegativeInteger(JToken value)
        {
            if (value == null || value.Type != JTokenType.Integer) return null;
            try { long count = value.Value<long>(); return count >= 0 ? count : (long?)null; }
            catch (Exception error) when (error is OverflowException || error is FormatException || error is InvalidCastException)
            { return null; }
        }
    }

    // Immutable, redacted observation of one HTTP attempt. It never retains payloads or headers.
    public sealed class CommanderProviderAttempt
    {
        public string Kind { get; }
        public int InputEstimate { get; }
        public IReadOnlyDictionary<string, int> SectionEstimates { get; }
        public CommanderProviderUsage Usage { get; }
        public long ElapsedMilliseconds { get; }
        public int? HttpStatusCode { get; }
        public string ResultStage { get; }

        public CommanderProviderAttempt(string kind, int inputEstimate, IReadOnlyDictionary<string, int> sections,
            CommanderProviderUsage usage, long elapsedMilliseconds, int? httpStatusCode, string resultStage)
        {
            Kind = kind;
            InputEstimate = inputEstimate;
            SectionEstimates = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(sections));
            Usage = usage ?? CommanderProviderUsage.Unknown;
            ElapsedMilliseconds = elapsedMilliseconds;
            HttpStatusCode = httpStatusCode;
            ResultStage = resultStage;
        }

        internal CommanderProviderAttempt WithResultStage(string stage) => new CommanderProviderAttempt(
            Kind, InputEstimate, SectionEstimates, Usage, ElapsedMilliseconds, HttpStatusCode, stage);
    }
}
```

### `Assets/Scripts/AI/Commander/Phase5C/CommanderProviderUsage.cs.meta`

```meta
fileFormatVersion: 2
guid: a7d46268612515446bbfd239e7804b96
```

### `Assets/Scripts/AI/Commander/Phase5C/CommanderSemanticRequestPack.cs`

```cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Retrieval/packing only. Nothing here parses an order, selects runtime actors or grants approval.
    internal sealed class CommanderSemanticRequestPack
    {
        internal const int InputLimit = 4000;
        internal const int MessageMargin = 256;
        internal string Json { get; }
        internal int EstimatedInputTokens { get; }
        internal IReadOnlyDictionary<string, int> SectionEstimates { get; }

        private CommanderSemanticRequestPack(JObject body, Dictionary<string, int> sections)
        {
            Json = body.ToString(Formatting.None);
            EstimatedInputTokens = EstimateBody(body);
            SectionEstimates = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(sections);
        }

        // Deliberately conservative UTF8 byte units, not characters/4 or an unverified tokenizer.
        // Actual endpoint-reported prompt usage is recorded separately; this is NOT certification
        // of a rolling alias's tokenizer/hidden endpoint overhead before live calibration.
        internal static int EstimateBody(JObject body)
        {
            JToken modelFacing = body["messages"];
            if (modelFacing == null) return int.MaxValue;
            long estimate = MessageMargin;
            if (!(modelFacing is JArray messages)) return int.MaxValue;
            foreach (var message in messages.OfType<JObject>())
                foreach (string field in new[] { "role", "content", "name", "tool_calls", "tool_call_id" })
                    if (message[field] != null) estimate += Encoding.UTF8.GetByteCount(message[field].Type == JTokenType.String
                        ? (string)message[field] : message[field].ToString(Formatting.None));
            foreach (string field in new[] { "tools", "response_format", "functions", "tool_choice", "function_call" })
                if (body[field] != null) estimate += Encoding.UTF8.GetByteCount(body[field].ToString(Formatting.None));
            return estimate > int.MaxValue ? int.MaxValue : (int)estimate;
        }

        internal static bool TryBuild(CommanderSemanticProviderRequest request, string model, int maxOutputTokens,
            out CommanderSemanticRequestPack pack, out string reason)
        {
            pack = null; reason = string.Empty;
            var context = JObject.Parse(request.SerializedContext);
            var knowledge = context["knowledge"] as JObject;
            if ((bool?)knowledge?["requiredReferenceOverflow"] == true)
            {
                reason = "Commander input limit: required canonical context cannot fit safely; the complete request was not submitted.";
                return false;
            }
            string search = request.PlayerMessage + " " + request.SerializedPendingClarification;
            var words = new HashSet<string>(Regex.Matches(search.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                .Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);
            bool Has(params string[] terms) => terms.Any(words.Contains);
            bool fallbackAll = false;
            bool construction = Has("build", "construct", "house", "houses", "mill", "farm", "farms", "barracks", "mine", "yard");
            bool allocation = Has("gather", "gathering", "food", "wood", "gold", "stone", "berries", "sheep", "farms", "split", "assign", "allocation", "allocate", "put");
            bool future = Has("next", "future", "births");
            bool production = Has("train", "produce", "recruit", "new", "more", "spearmen", "archers", "knights");
            bool age = Has("age", "ages", "feudal", "castle", "imperial", "advance");
            bool action = Has("move", "march", "go", "guard", "protect", "defend", "defense", "patrol", "scout", "explore", "find", "discover", "enemy", "attack", "strike", "retreat", "withdraw", "repair", "fix", "rally", "garrison", "ungarrison")
                || Has("send") && Has("spearmen", "archers", "knights", "army", "troops", "military");
            bool research = Has("research", "technology", "upgrade");
            bool strategy = Has("strategy", "strategic");
            if (!construction && !allocation && !future && !production && !age && !action && !research && !strategy)
            {
                construction = allocation = future = production = age = action = research = strategy = true;
                fallbackAll = true;
            }
            var selected = new HashSet<CommanderSemanticNodeType>();
            if (construction) selected.Add(CommanderSemanticNodeType.BuildStructure);
            if (allocation) selected.Add(CommanderSemanticNodeType.AllocateWorkers);
            if (fallbackAll || allocation && Has("allocation", "allocate", "set")) selected.Add(CommanderSemanticNodeType.SetResourceAllocation);
            if (future) selected.Add(CommanderSemanticNodeType.WatchFutureUnits);
            if (production) selected.Add(CommanderSemanticNodeType.EnsureUnitCount);
            if (age) selected.Add(CommanderSemanticNodeType.ReachAge);
            if (research) selected.Add(CommanderSemanticNodeType.ResearchTechnology);
            if (strategy) selected.Add(CommanderSemanticNodeType.StrategicObjective);
            if (action)
                foreach (CommanderCapabilityActionType capability in Enum.GetValues(typeof(CommanderCapabilityActionType)))
                    if (capability != CommanderCapabilityActionType.ResearchTechnology
                        && (fallbackAll || MatchesAction(capability, words))
                        && Enum.TryParse(capability.ToString(), out CommanderSemanticNodeType type)) selected.Add(type);

            string instruction = request.IsReadOnlyQuestion ? ReadOnlyRules : CommonRules + Vocabulary(selected);
            // Contract retrieval only: quantities on future units must not imply repeated buildings.
            bool repeated = Has("split", "partition") || construction && (Has("third") || Regex.IsMatch(search,
                @"\b(?:build|construct|make)\s+(?:two|three|four|five|six|seven|eight|nine|ten|[2-9]|1[0-9]|20)\b",
                RegexOptions.IgnoreCase));
            bool composedWoodline = Has("all") && Has("wood", "woodline", "trees", "tree")
                && Has("west", "western", "left") && Has("town", "center", "tc");
            if (repeated && !request.IsReadOnlyQuestion) instruction += DynamicVocabulary();
            else if (composedWoodline && !request.IsReadOnlyQuestion) instruction += ComposedWoodlineVocabulary;
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
            {
                reason = "Commander input limit: required canonical facts are missing from the complete context; nothing was submitted.";
                return false;
            }
            string compactContext = compact.ToString(Formatting.None);
            // Essential turn/pending data is never truncated to squeeze through the budget.
            string user = "Context:" + compactContext + "\nMemory:" + request.SerializedSemanticMemory
                + "\nFacts:" + request.QuestionFacts + "\nBounded pending:" + request.SerializedPendingClarification
                + "\nPlayer:" + request.PlayerMessage;
            var body = new JObject { ["model"] = model,
                ["messages"] = new JArray(new JObject { ["role"] = "system", ["content"] = packedInstruction },
                    new JObject { ["role"] = "user", ["content"] = user }),
                ["max_tokens"] = maxOutputTokens, ["reasoning"] = new JObject { ["effort"] = "none" } };
            var sections = new Dictionary<string, int> {
                ["contract"] = Encoding.UTF8.GetByteCount(packedInstruction),
                ["context"] = Encoding.UTF8.GetByteCount(compactContext),
                ["memory"] = Encoding.UTF8.GetByteCount(request.SerializedSemanticMemory),
                ["questionFacts"] = Encoding.UTF8.GetByteCount(request.QuestionFacts),
                ["pending"] = Encoding.UTF8.GetByteCount(request.SerializedPendingClarification),
                ["utterance"] = Encoding.UTF8.GetByteCount(request.PlayerMessage), ["messageMargin"] = MessageMargin };
            var candidate = new CommanderSemanticRequestPack(body, sections);
            pack = candidate; // Observed packing metrics survive a rejection; this is not dispatch authority.
            if (candidate.EstimatedInputTokens < InputLimit) return true;
            if (!optionalStripped && StripOptionalHints(knowledge))
            {
                optionalStripped = true;
                continue;
            }
            reason = "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question.";
            return false;
            }
        }

        private static bool StripOptionalHints(JObject knowledge)
        {
            if (knowledge == null) return false;
            var required = new HashSet<string>((knowledge["requiredCanonicalIds"] as JArray ?? new JArray())
                .Where(x => x.Type == JTokenType.String).Select(x => (string)x), StringComparer.Ordinal);
            var optional = new HashSet<string>((knowledge["optionalCanonicalIds"] as JArray ?? new JArray())
                .Where(x => x.Type == JTokenType.String).Select(x => (string)x), StringComparer.Ordinal);
            bool stripped = false;
            foreach (string table in new[] { "units", "buildings" })
                if (knowledge[table] is JArray rows)
                    foreach (JObject row in rows.OfType<JObject>().Where(row =>
                        optional.Contains((string)row["id"]) && !required.Contains((string)row["id"])).ToArray())
                    { row.Remove(); stripped = true; }
            if (knowledge["nameInterpretations"] is JArray interpretations)
                foreach (JObject hint in interpretations.OfType<JObject>()
                    .Where(hint => (bool?)hint["ambiguous"] == false).ToArray())
                { hint.Remove(); stripped = true; }
            return stripped;
        }

        private static bool RequiredRowsPresent(JArray required, JObject compactKnowledge)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (string table in new[] { "units", "buildings" })
            {
                if (!(compactKnowledge?[table + "Columns"] is JArray columns)
                    || !(compactKnowledge[table] is JArray rows)) continue;
                int id = columns.Select((value, index) => (value: (string)value, index))
                    .Where(value => value.value == "id").Select(value => value.index).DefaultIfEmpty(-1).First();
                if (id < 0) continue;
                foreach (JArray row in rows.OfType<JArray>())
                    if (id < row.Count && row[id].Type == JTokenType.String) present.Add((string)row[id]);
            }
            return required.All(id => id.Type == JTokenType.String && present.Contains((string)id));
        }

        private const string ReadOnlyRules = "Return one JSON object, no markdown: {\"outcome\":\"Answer\",\"message\":\"brief grounded answer\"}, "
            + "{\"outcome\":\"Clarify\",\"message\":\"specific missing fact\"} or {\"outcome\":\"Unsupported\",\"message\":\"reason\"}. "
            + "Message<=180 chars. INFORMATION ONLY: no nodes/plans/approval. Use owned task facts; unknown facts remain unknown. No hidden state, IDs or commands.";
        private static bool MatchesAction(CommanderCapabilityActionType type, HashSet<string> words)
        {
            bool Has(params string[] terms) => terms.Any(words.Contains);
            switch (type)
            {
                case CommanderCapabilityActionType.MoveUnits: return Has("move", "send", "march", "go");
                case CommanderCapabilityActionType.ScoutArea: return Has("scout", "explore") && !Has("find", "discover");
                case CommanderCapabilityActionType.DiscoverEnemy: return Has("find", "discover") && Has("enemy", "base", "town", "center");
                case CommanderCapabilityActionType.GarrisonUnits: return Has("garrison");
                case CommanderCapabilityActionType.UngarrisonBuilding: return Has("ungarrison", "evacuate");
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
        private const string CommonRules =
            "JSON only,strict fields/enums; never authorize IDs,coords,commands,hidden/extra goals. Keep counts,negations,actors,sources,restrictions,order,exact refs;unknown unknown. "
            + "Request{outcome:Request,nodes:1..4,executionOrder:Independent|Sequential|DependencyGraph}; after/then=completion dependsOn:[prior0-based]; resultFromNode=exact-new,producerFromNode=explicit-new; a/an=1,number words,left=MapWest. "
            + "Clarify{outcome:Clarify,message<=180,pending:{executionOrder,nodes:partial1..4},missingSlots:[{node,path,type:Confirmation|Count|SourceKind|CanonicalName,alternatives:[canonical]}]};retain known leaves/edges/refs,omit only missing;Confirmation retains offer. Worker pending+missingFields valid;not approval. Answer/Unsupported{outcome,message<=180};no nodes.";

        private static string Vocabulary(HashSet<CommanderSemanticNodeType> selected)
        {
            var text = new StringBuilder();
            foreach (CommanderSemanticNodeType type in Enum.GetValues(typeof(CommanderSemanticNodeType)))
            {
                if (!selected.Contains(type)) continue;
                switch (type)
                {
                    case CommanderSemanticNodeType.BuildStructure:
                        text.Append("BuildStructure{structure:canonical,count:1..20,builders:{state:Eligible(default)|IdleOnly(strict),count:1},placement?}. placement TC/Barracks:{anchor:MyTownCenter|MyBarracks,relation:Near|MapWest|MapEast,clearGapTiles?:1..20,ordinal?:1..8 TC-only}; resource:{anchor:VisibleResource(default)|WorkedResource(explicit),relation:Near,resource,sourceKind?}. Near gap1,no resource ordinal. "); break;
                    case CommanderSemanticNodeType.SetResourceAllocation:
                        text.Append("SetResourceAllocation{resource,count:0..200} sets worker target; distinct from AllocateWorkers. "); break;
                    case CommanderSemanticNodeType.AllocateWorkers:
                        text.Append("AllocateWorkers{mode:SelectedCount(default)|Additional(more workers)|TargetTotal(keep total),countMode:Exact|AllMatching(snapshot),count:1..200 omit if AllMatching,workers:{state:Any|Idle|Gathering,currentResource?},destination:{resource,sourceKind?},resourceAmount?:1..1000000 paired resourceAmountMode:Stockpile|AdditionalGathered(player-wide)}. Amount!=worker count;assignment!=income. "); break;
                    case CommanderSemanticNodeType.EnsureUnitCount:
                        text.Append("EnsureUnitCount:unit canonical,count0..200,quantityMode:TargetTotal default|New. New/more=exact new count, no existing substitutes. Game handles prerequisites; extra buildings only explicit. "); break;
                    case CommanderSemanticNodeType.WatchFutureUnits:
                        text.Append("WatchFutureUnits{unit:canonical,count:1..50,producer:canonical,producerOrdinal?:1..8,action:Gather(Villager only),resource,sourceKind?} OR action:Patrol,location:WorkedResource,resource. Next actual births incl human queues,never train;ambiguous producer clarify. "); break;
                    case CommanderSemanticNodeType.ReachAge:
                        text.Append("ReachAge:targetAge:Feudal|Castle|Imperial|Next. Explicit2/3/4=Feudal/Castle/Imperial, not Next. "); break;
                    case CommanderSemanticNodeType.ResearchTechnology:
                        text.Append("ResearchTechnology:technology:BlacksmithDamage|BlacksmithDefense|Ballistics|SiegeEngineering|Chemistry|MurderHoles. "); break;
                    case CommanderSemanticNodeType.StrategicObjective:
                        text.Append("StrategicObjective only explicit player strategy; objective from context strategyCapabilities, constraints retained. "); break;
                    default:
                        if (!Enum.TryParse(type.ToString(), out CommanderCapabilityActionType _)
                            || type == CommanderSemanticNodeType.ResearchTechnology)
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
                    .Append("unitSelector:Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary; location:PlayerBase|WorkedResource|VisibleResource|VisibleEnemy|RelativeToSelectedUnits. Ordinary ScoutArea is one visible point, not enemy discovery; no radius. Keep explicit source/side/current-resource criteria; use the typed DynamicPlan composition when advertised, otherwise Clarify/Unsupported, never drop criteria. ");
                if (actions.Contains(CommanderSemanticNodeType.AttackTarget) || actions.Contains(CommanderSemanticNodeType.RepairTarget))
                    text.Append("Attack/Repair target?{kind:UnitType,unit}|{kind:BuildingType,structure}. ");
                if (actions.Contains(CommanderSemanticNodeType.SetRallyPoint)) text.Append("Rally uses owned producer. ");
                if (actions.Contains(CommanderSemanticNodeType.GarrisonUnits))
                    text.Append("GarrisonUnits{unitSelector:Villagers,countMode:AllMatching omit count OR count:1..50,location:PlayerBase,structure:owned shelter type}; exact shelter, actual native entry/capacity and protected/unaccommodated remainder; no other shelter fallback. ");
                if (actions.Contains(CommanderSemanticNodeType.UngarrisonBuilding))
                    text.Append("UngarrisonBuilding{location:PlayerBase,structure:owned shelter type}; building-wide egress, not selective. ");
                if (actions.Contains(CommanderSemanticNodeType.DiscoverEnemy))
                    text.Append("DiscoverEnemy{unitSelector:Scout,count:1,location:VisibleEnemy,target:{kind:UnitType,unit}|{kind:BuildingType,structure}}; finite known-frontier Scout+Move route, complete only on legitimately visible requested enemy type; no hidden coordinates or resultFromNode. ");
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

        private const string ComposedWoodlineVocabulary =
            "For all current-Wood villagers and west-of-owned-TC Wood/Tree: DynamicPlan version1 with select-workers{state:Gathering,currentResource:Wood,countMode:AllMatching,omit count} -> WorkerSet; resolve-location{anchor:MyTownCenter,relation:MapWest,clearGapTiles:1,resource:Wood,sourceKind:Tree} -> LocationIntent. For explicit move, move-workers inputs workers:select-workers,location:resolve-location -> native Move (not Gather); for explicit gather, allocate-workers{resource:Wood,sourceKind:Tree} with same inputs -> native Gather. Include dependsOn refs; never replace ALL with count50 or silently change Move to Gather. Game binds owned workers/source/anchor and refuses overflow. No coordinates/IDs. ";

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
                    if (source[field] is JArray ids)
                        foreach (var id in ids) if (id.Type == JTokenType.String) supportedIds.Add((string)id);
                foreach (var property in knowledge.Properties())
                    if (property.Name != "policy" && property.Name != "requiredCanonicalIds"
                        && property.Name != "optionalCanonicalIds"
                        && property.Name != "requiredReferenceOverflow"
                        && (!(property.Value is JArray array) || array.Count > 0))
                    {
                        if (property.Value is JArray rows && rows.Count > 0 && rows.All(v => v is JObject))
                        {
                            var detachedRows = rows.OfType<JObject>().Select(v => (JObject)v.DeepClone()).ToArray();
                            if (property.Name == "units" || property.Name == "buildings")
                            {
                                // Existing availability/executable facts normally express this already.
                                // Retain an explicit override only when the current adapter differs.
                                bool differs = detachedRows.Any(row => supportedIds.Contains((string)row["id"])
                                    != ((bool?)row["availableForCivilization"] == true && (bool?)row["commanderExecutable"] == true));
                                if (differs) foreach (var row in detachedRows)
                                    row["capabilitySupported"] = supportedIds.Contains((string)row["id"]);
                            }
                            var columns = detachedRows.SelectMany(v => v.Properties().Select(p => p.Name))
                                .Distinct(StringComparer.Ordinal).ToArray();
                            facts[property.Name + "Columns"] = new JArray(columns);
                            facts[property.Name] = new JArray(detachedRows.Select(row => new JArray(columns.Select(column =>
                                column == "cost" && row[column] is JObject cost
                                    ? (JToken)new JArray(new[] { "food", "wood", "gold", "stone" }.Select(resource => cost[resource]?.DeepClone() ?? JValue.CreateNull()))
                                    : row[column]?.DeepClone() ?? JValue.CreateNull()))));
                        }
                        else facts[property.Name] = property.Value.DeepClone();
                    }
                if (facts.Count > 0) result["knowledge"] = facts;
            }
            foreach (string key in new[] { "ownedUnitCounts", "ownedBuildingCounts" })
            {
                var counts = new JObject();
                if (source[key] is JObject all)
                    foreach (var value in all.Properties())
                        if (value.Value.Type == JTokenType.Integer && (value.Value.Value<int>() != 0 || value.Name == "Villager" || value.Name == "Scout")) counts[value.Name] = value.Value.DeepClone();
                result[key] = counts;
            }
            // Public IDs/names/availability/capability flags occur together in detached fact rows.
            // Avoid a duplicate map; knowledge still grants no execution support or authority.
            if (includeStrategy && source["strategyCapabilities"] != null) result["strategyCapabilities"] = source["strategyCapabilities"].DeepClone();
            return result;
        }
    }
}
```

### `Assets/Scripts/AI/Commander/Phase5C/CommanderSemanticRequestPack.cs.meta`

```meta
fileFormatVersion: 2
guid: 161005c9677443d49b946f26ef6a7e8b
```

### `Assets/Scripts/Core/GameSimulation.ExactGarrison.cs`

```cs
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
        private readonly List<ICommand> localHumanGarrisonOriginals = new List<ICommand>();
        private readonly Dictionary<int, ExactGarrisonOrder> activeExactGarrison =
            new Dictionary<int, ExactGarrisonOrder>();

        private void ObserveLocalGarrisonEnqueue(ICommand command, CommandEnqueueSource source)
        {
            if (source == CommandEnqueueSource.Human && command is GarrisonCommand
                && localHumanGarrisonOriginals.Count < MaxExactGarrisonSubjects)
                localHumanGarrisonOriginals.Add(command);
        }

        private bool ConsumeProvenLocalHumanGarrison(ICommand original)
        {
            for (int i = 0; i < localHumanGarrisonOriginals.Count; i++)
                if (ReferenceEquals(localHumanGarrisonOriginals[i], original))
                {
                    localHumanGarrisonOriginals.RemoveAt(i);
                    return true;
                }
            return false;
        }

        internal bool RegisterExactGarrisonCommand(ICommand original)
        {
            if (!(original is GarrisonCommand command) || command.UnitIds == null
                || command.UnitIds.Length == 0 || command.UnitIds.Length > MaxExactGarrisonSubjects
                || command.TargetBuildingId < 0)
                return false;
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

        // A reconstructed relay order with no unique verified original cannot
        // inherit the opt-in guard. Retain expired signatures as bounded local
        // tombstones: a delayed original or replay must never become ordinary
        // Garrison just because time passed. The 200-subject admission cap also
        // bounds tombstones; at capacity new exact requests fail closed.
        private bool RejectUnattributedExactGarrison(ICommand command, ICommand verifiedOriginal)
        {
            if (!(command is GarrisonCommand garrison)) return false;
            bool provenHuman = ConsumeProvenLocalHumanGarrison(verifiedOriginal ?? command);
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
                if (provenHuman) return false;
                pending.ExpiresTick = Math.Min(pending.ExpiresTick, currentTick - 1);
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
            int[] subjects = CommandBuffer.GetSubjectUnitIds(command);
            if (subjects == null) return true;
            ExactGarrisonOrder exact = null;
            for (int i = 0; i < pendingExactGarrison.Count; i++)
                if (ReferenceEquals(pendingExactGarrison[i].Original, original))
                {
                    exact = pendingExactGarrison[i];
                    if (currentTick <= exact.ExpiresTick)
                        pendingExactGarrison.RemoveAt(i);
                    break;
                }
            if (exact != null)
            {
                if (currentTick > exact.ExpiresTick) return false;
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
```

### `Assets/Scripts/Core/GameSimulation.ExactGarrison.cs.meta`

```meta
fileFormatVersion: 2
guid: 37b29fc10dfcbdc4aae6c4649f317dd6
```

### `Assets/Scripts/Core/GameSimulation.MovementObservation.cs`

```cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    internal enum NativeMovePathResult { Complete, AlreadyAtTarget, Partial, NoPath }

    // Detached, local-only observation of ordinary MoveCommand processing. It is
    // never serialized, hashed, or consulted by gameplay simulation decisions.
    internal sealed class NativeMoveProcessingReceipt
    {
        internal GameSimulation Runtime { get; }
        internal ICommand OriginalCommand { get; }
        internal int UnitId { get; }
        internal int Tick { get; }
        internal FixedVector3 Endpoint { get; }
        internal NativeMovePathResult Result { get; }

        internal NativeMoveProcessingReceipt(GameSimulation runtime, ICommand originalCommand,
            int unitId, int tick, FixedVector3 endpoint, NativeMovePathResult result)
        {
            Runtime = runtime; OriginalCommand = originalCommand; UnitId = unitId;
            Tick = tick; Endpoint = endpoint; Result = result;
        }
    }

    internal sealed class NativeUnitCommandObservation
    {
        internal GameSimulation Runtime { get; }
        internal ICommand OriginalCommand { get; }
        internal ICommand ExecutingCommand { get; }
        internal int Tick { get; }

        internal NativeUnitCommandObservation(GameSimulation runtime, ICommand original,
            ICommand executing, int tick)
        { Runtime = runtime; OriginalCommand = original; ExecutingCommand = executing; Tick = tick; }
    }

    public partial class GameSimulation
    {
        internal event Action<NativeMoveProcessingReceipt> NativeMoveProcessed;
        internal event Action<NativeUnitCommandObservation> NativeUnitCommandProcessed;

        private void PublishNativeUnitCommandProcessed(ICommand originalCommand,
            ICommand executingCommand)
        {
            var handlers = NativeUnitCommandProcessed;
            if (handlers == null) return;
            var observation = new NativeUnitCommandObservation(this, originalCommand,
                executingCommand, currentTick);
            foreach (Action<NativeUnitCommandObservation> observer in handlers.GetInvocationList())
            {
                try { observer(observation); }
                catch (Exception error) { ReportTrainingObservationError(error); }
            }
        }

        private void PublishNativeMoveProcessed(ICommand originalCommand, int unitId,
            FixedVector3 endpoint, NativeMovePathResult result)
        {
            var handlers = NativeMoveProcessed;
            if (handlers == null) return;
            var receipt = new NativeMoveProcessingReceipt(this, originalCommand,
                unitId, currentTick, endpoint, result);
            foreach (Action<NativeMoveProcessingReceipt> observer in handlers.GetInvocationList())
            {
                try { observer(receipt); }
                catch (Exception error) { ReportTrainingObservationError(error); }
            }
        }

        // The same connectivity adjustment runs in ordinary native processing and
        // Commander preflight; no alternative formation transform is introduced.
        internal void SnapNativeMoveFormationPositions(ref MoveCommand cmd)
        {
            if (cmd.FormationPositions == null) return;
            Vector2Int anchorTile = MapData.WorldToTile(cmd.TargetPosition);
            int maxChebyshev = 0;
            for (int fi = 0; fi < cmd.FormationPositions.Length; fi++)
            {
                Vector2Int fpTile = MapData.WorldToTile(cmd.FormationPositions[fi]);
                int chebyshev = Mathf.Max(Mathf.Abs(fpTile.x - anchorTile.x), Mathf.Abs(fpTile.y - anchorTile.y));
                if (chebyshev > maxChebyshev) maxChebyshev = chebyshev;
            }
            int floodRadius = maxChebyshev * 2;
            HashSet<int> reachable = GridPathfinder.FloodFillWalkable(MapData, anchorTile, floodRadius);
            if (reachable.Count == 0) return;
            int mapWidth = MapData.Width;
            for (int fi = 0; fi < cmd.FormationPositions.Length; fi++)
            {
                Vector2Int fpTile = MapData.WorldToTile(cmd.FormationPositions[fi]);
                int key = fpTile.y * mapWidth + fpTile.x;
                if (reachable.Contains(key)) continue;
                Vector2Int snapped = GridPathfinder.FindConnectedTileToward(fpTile, anchorTile,
                    reachable, mapWidth, floodRadius);
                if (snapped.x >= 0) cmd.FormationPositions[fi] = MapData.TileToWorldFixed(snapped.x, snapped.y);
            }
        }

        internal FixedVector3 NativeMoveFormationCenter(FixedVector3 anchor,
            FixedVector3 slot, UnitData unit)
        {
            Fixed32 radiusScale = unit.Radius / cachedUnitRadius;
            if (radiusScale == Fixed32.One) return slot;
            FixedVector3 offset = slot - anchor;
            return anchor + new FixedVector3(offset.x * radiusScale,
                offset.y, offset.z * radiusScale);
        }

        internal Fixed32 MaximumNativeMoveScatter(UnitData unit)
        {
            Fixed32 scatterFactor = Fixed32.Max(Fixed32.Zero,
                Fixed32.One - (unit.Mass - Fixed32.One) / (cachedSpearmanMass - Fixed32.One));
            Fixed32 scatter = cachedFormationScatter * scatterFactor;
            return scatter > cachedScatterThreshold ? scatter : Fixed32.Zero;
        }

        internal FixedVector3 ResolveNativeMoveFormationTarget(FixedVector3 anchor,
            FixedVector3 slot, UnitData unit, int executionTick)
        {
            FixedVector3 targetPos = NativeMoveFormationCenter(anchor, slot, unit);
            Fixed32 scatter = MaximumNativeMoveScatter(unit);
            if (scatter > Fixed32.Zero)
            {
                uint hash = (uint)(unit.Id * 2654435761u + (uint)executionTick * 1103515245u);
                int hashX = (int)(hash & 0xFFFF) - 32768;
                int hashZ = (int)((hash >> 16) & 0xFFFF) - 32768;
                Fixed32 normX = new Fixed32(hashX);
                Fixed32 normZ = new Fixed32(hashZ);
                targetPos.x = targetPos.x + normX * scatter;
                targetPos.z = targetPos.z + normZ * scatter;
            }
            return targetPos;
        }
    }
}
```

### `Assets/Scripts/Core/GameSimulation.MovementObservation.cs.meta`

```meta
fileFormatVersion: 2
guid: 327fc8bda9e00574cad5a29b91b045a6
```

### `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs`

```cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        private static string GraphDraft(JArray nodes, string order, int index, string path, string type, params JToken[] alternatives) =>
            new JObject { ["outcome"] = "Clarify", ["message"] = "Please confirm the missing detail.",
                ["pending"] = new JObject { ["executionOrder"] = order, ["nodes"] = nodes },
                ["missingSlots"] = new JArray(new JObject { ["node"] = index, ["path"] = path,
                    ["type"] = type, ["alternatives"] = new JArray(alternatives) }) }.ToString();

        [Test]
        public async Task MineProposal_YesUsesOriginalRequestAndNoSecondTranslation()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 },
                ["placement"] = new JObject { ["anchor"] = "VisibleResource", ["relation"] = "Near",
                    ["resource"] = "Gold", ["sourceKind"] = "GoldMine" } };
            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal)))
            {
                f.Economy.Worker(0); f.Economy.Node(ResourceType.Gold, 8);
                await f.Chat.SubmitMessageAsync("build a gold mine near gold");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                var result = await f.Chat.SubmitMessageAsync("yes");
                Assert.That(result?.Success, Is.True);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                var goal = f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single();
                Assert.That(goal.StructureType, Is.EqualTo(BuildingType.Mine));
                Assert.That(goal.PlacementAnchorSelector, Is.EqualTo(CommanderSemanticAnchorSelector.VisibleResource));
                Assert.That(goal.PlacementResourceType, Is.EqualTo(ResourceType.Gold));
                Assert.That(goal.PlacementSourceKind, Is.EqualTo(ResourceSourceKind.GoldMine));
                Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo("build a gold mine near gold"));
            }
        }

        [Test]
        public async Task HouseThenNextFive_FromTreesKeepsGraphAndNeedsSeparateApproval()
        {
            var house = new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            var watch = new JObject { ["type"] = "WatchFutureUnits", ["unit"] = "Villager", ["count"] = 5,
                ["producer"] = "TownCenter", ["producerOrdinal"] = 1, ["action"] = "Gather", ["resource"] = "Wood",
                ["dependsOn"] = new JArray(0) };
            string proposal = GraphDraft(new JArray(house, watch), "Sequential", 1, "sourceKind", "SourceKind", new JValue("Tree"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal)))
            {
                f.Economy.Worker(0);
                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true).AutoProduceVillagers = false;
                await f.Chat.SubmitMessageAsync("Build a House, then send the next five Villagers from my first Town Center to Wood");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("From trees");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null);
                var nodes = f.Chat.PendingActionPlan.Interpretation.Nodes;
                Assert.That(nodes.Count, Is.EqualTo(2));
                Assert.That(nodes[1].DependsOn, Is.EquivalentTo(new[] { 0 }));
                Assert.That(nodes[1].Count, Is.EqualTo(5));
                Assert.That(nodes[1].ProducerOrdinal, Is.EqualTo(1));
                Assert.That(nodes[1].ResourceType, Is.EqualTo(ResourceType.Wood));
                Assert.That(nodes[1].SourceKind, Is.EqualTo(ResourceSourceKind.Tree));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2));
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task NoRejectsOnlyProposal_AndIndependentCommandSupersedes()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(
                Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}"), proposal, proposal,
                Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Archer\",\"count\":3}"))))
            {
                await f.Chat.SubmitMessageAsync("train four spearmen");
                int accepted = f.Economy.Manager.Goals.Count;
                await f.Chat.SubmitMessageAsync("build a gold mine");
                await f.Chat.SubmitMessageAsync("no");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(accepted));
                await f.Chat.SubmitMessageAsync("build a gold mine");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("train three archers");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(accepted + 1));
            }
        }

        [Test]
        public void GraphDraftRejectsInvalidAlternativesAndAuthority()
        {
            var build = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            var valid = JObject.Parse(GraphDraft(new JArray(build), "Independent", 0, "structure", "Confirmation", new JValue("Mine")));
            Assert.That(CommanderSemanticJson.Parse(valid.ToString()).IsValid, Is.True);
            foreach (Action<JObject> corrupt in new Action<JObject>[] {
                x => ((JObject)x["pending"])["playerId"] = 0,
                x => ((JObject)((JArray)((JObject)x["pending"])["nodes"])[0])["entityId"] = 10,
                x => ((JObject)((JArray)x["missingSlots"])[0])["type"] = "RuntimeId",
                x => ((JObject)((JArray)x["missingSlots"])[0])["path"] = "placement.tile",
                x => ((JObject)((JArray)x["missingSlots"])[0])["alternatives"] = new JArray("Barracks"),
                x => ((JObject)((JArray)x["missingSlots"])[0])["node"] = 2 })
            {
                var copy = (JObject)valid.DeepClone(); corrupt(copy);
                Assert.That(CommanderSemanticJson.Parse(copy.ToString()).IsValid, Is.False, copy.ToString());
            }
            var watch = new JObject { ["type"] = "WatchFutureUnits", ["unit"] = "Villager", ["count"] = 5,
                ["producer"] = "TownCenter", ["action"] = "Gather", ["resource"] = "Wood" };
            Assert.That(CommanderSemanticJson.Parse(GraphDraft(new JArray(watch), "Independent", 0,
                "sourceKind", "SourceKind", new JValue("GoldMine"))).IsValid, Is.False,
                "Every offered source must be compatible with the retained resource.");
        }

        [Test]
        public async Task SourceReply_PreservesAmountModeAndWoodWorkerCriterion()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 6,
                ["workers"] = new JObject { ["state"] = "Gathering", ["currentResource"] = "Wood" },
                ["destination"] = new JObject { ["resource"] = "Food" },
                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
            string proposal = GraphDraft(new JArray(allocation), "Independent", 0, "destination.sourceKind",
                "SourceKind", new JValue("Berries"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal)))
            {
                for (int i = 0; i < 6; i++) f.Economy.Assigned(f.Economy.Worker(i), f.Economy.Node(ResourceType.Wood, 15 + i));
                f.Economy.Node(ResourceType.Food, 30);
                await f.Chat.SubmitMessageAsync("send six wood villagers to gather 400 additional food");
                var submitted = await f.Chat.SubmitMessageAsync("from berries");
                Assert.That(submitted?.Success, Is.True);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(retained.Count, Is.EqualTo(6));
                Assert.That(retained.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood));
                Assert.That(retained.ResourceAmount, Is.EqualTo(400));
                Assert.That(retained.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
                Assert.That(retained.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Berries));
            }
        }

        [Test]
        public async Task ResetDropsGraphDraft_AndBareYesCannotBecomeANewGoal()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal,
                Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}"))))
            {
                await f.Chat.SubmitMessageAsync("build a gold mine");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                f.Chat.ResetConversation();
                Assert.That(f.Chat.PendingClarification, Is.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Provider.Calls, Is.EqualTo(1), "An orphan confirmation is not a new model order.");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        public async Task MovementCommandSupersedesGraphDraftWithoutCancellingAcceptedWork()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal,
                "{\"outcome\":\"Unsupported\",\"message\":\"No safe destination.\"}")))
            {
                await f.Chat.SubmitMessageAsync("build a gold mine");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("move villagers to my base");
                Assert.That(f.Provider.Calls, Is.EqualTo(2));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        public async Task TwoMissingSlots_CountThenSourceResumeSameDraftLocally()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food" } };
            var proposal = JObject.Parse(GraphDraft(new JArray(allocation), "Independent", 0,
                "count", "Count", new JValue(2)));
            ((JArray)proposal["missingSlots"]).Add(new JObject { ["node"] = 0,
                ["path"] = "destination.sourceKind", ["type"] = "SourceKind",
                ["alternatives"] = new JArray("Berries") });
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal.ToString())))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send idle villagers to food");
                Assert.That(f.Chat.PendingClarification?.GraphDraft?.MissingSlots.Count, Is.EqualTo(2));
                await f.Chat.SubmitMessageAsync("three");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.PendingClarification?.GraphDraft?.MissingSlots.Count, Is.EqualTo(1));
                await f.Chat.SubmitMessageAsync("from berries");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(retained.Count, Is.EqualTo(3));
                Assert.That(retained.Workers.State, Is.EqualTo(CommanderWorkerState.Idle));
                Assert.That(retained.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Berries));
            }
        }

        [Test]
        public async Task KnownStructureCorrection_BuildOrMakeMillInsteadPreservesMineProposalFields()
        {
            foreach (string correction in new[] { "build a Mill instead", "make a Mill instead" })
            {
                var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 },
                    ["placement"] = new JObject { ["anchor"] = "MyTownCenter", ["relation"] = "Near" } };
                using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
                    "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
                {
                    f.Economy.Worker(0);
                    f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true);
                    await f.Chat.SubmitMessageAsync("build a Mine near my Town Center");
                    var result = await f.Chat.SubmitMessageAsync(correction);
                    Assert.That(result?.Success, Is.True, correction);
                    Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
                    var goal = f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single();
                    Assert.That(goal.StructureType, Is.EqualTo(BuildingType.Mill));
                    Assert.That(goal.PlacementAnchorSelector, Is.EqualTo(CommanderSemanticAnchorSelector.MyTownCenter));
                    Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo("build a Mine near my Town Center"));
                }
            }
        }

        [Test]
        public async Task KnownCountCorrection_DoesNotChangeResourceAmount()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 6,
                ["workers"] = new JObject { ["state"] = "Gathering", ["currentResource"] = "Wood" },
                ["destination"] = new JObject { ["resource"] = "Food" },
                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
            {
                for (int i = 0; i < 3; i++) f.Economy.Assigned(f.Economy.Worker(i), f.Economy.Node(ResourceType.Wood, 15 + i));
                f.Economy.Node(ResourceType.Food, 30);
                await f.Chat.SubmitMessageAsync("send six wood villagers to gather 400 additional food");
                await f.Chat.SubmitMessageAsync("three villagers instead");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                var pending = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
                Assert.That((int)pending["count"], Is.EqualTo(3));
                Assert.That((int)pending["resourceAmount"], Is.EqualTo(400));
                await f.Chat.SubmitMessageAsync("from berries");
                var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(retained.Count, Is.EqualTo(3));
                Assert.That(retained.ResourceAmount, Is.EqualTo(400));
                Assert.That(retained.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
                Assert.That(retained.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood));
            }
        }

        [Test]
        public async Task UnrecognizedGraphReply_AndSourceYes_ReaskWithoutHttp()
        {
            var watch = new JObject { ["type"] = "WatchFutureUnits", ["unit"] = "Villager", ["count"] = 5,
                ["producer"] = "TownCenter", ["action"] = "Gather", ["resource"] = "Wood" };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(watch),
                "Independent", 0, "sourceKind", "SourceKind", new JValue("Tree")))))
            {
                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true);
                await f.Chat.SubmitMessageAsync("send the next five villagers to wood from trees");
                await f.Chat.SubmitMessageAsync("yes");
                await f.Chat.SubmitMessageAsync("maybe elsewhere");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                await f.Chat.SubmitMessageAsync("from trees");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        public void GraphSlotAlternatives_ExternalMutationCannotAlterDraft()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            var parsed = CommanderSemanticJson.Parse(GraphDraft(new JArray(mine), "Independent", 0,
                "structure", "Confirmation", new JValue("Mine")));
            var draft = parsed.PendingGraphDraft;
            ((JValue)draft.MissingSlots[0].Alternatives[0]).Value = "Mill";
            Assert.That((string)draft.MissingSlots[0].Alternatives[0], Is.EqualTo("Mine"));
            Assert.That((string)((JArray)draft.ToSlotsJson()[0]["alternatives"])[0], Is.EqualTo("Mine"));
            Assert.That(draft.Resolve("yes").Nodes[0].BuildingType, Is.EqualTo(BuildingType.Mine));
        }

        [Test]
        public async Task KnownResourceAmountCorrection_DoesNotChangeWorkerCount()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 3,
                ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food" },
                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send three idle villagers to gather 400 additional food");
                await f.Chat.SubmitMessageAsync("500 additional food instead");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                var node = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
                Assert.That((int)node["count"], Is.EqualTo(3));
                Assert.That((int)node["resourceAmount"], Is.EqualTo(500));
                Assert.That((string)node["resourceAmountMode"], Is.EqualTo("AdditionalGathered"));
                await f.Chat.SubmitMessageAsync("from berries");
                var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(retained.Count, Is.EqualTo(3));
                Assert.That(retained.ResourceAmount, Is.EqualTo(500));
            }
        }

        [Test]
        public async Task RepeatedSuccessfulGraphCorrections_ExpireBeforeSeventhReply()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 2,
                ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food" } };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
            {
                for (int i = 0; i < 8; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send two idle villagers to berries");
                foreach (string word in new[] { "three", "four", "five", "six", "seven", "eight" })
                    await f.Chat.SubmitMessageAsync(word + " villagers instead");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, "Six successful replies may remain pending.");
                Assert.That(f.Chat.PendingClarification.Turns, Is.EqualTo(6));
                await f.Chat.SubmitMessageAsync("from berries");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        public async Task PendingDraft_ForeignRuntimeAndReplayCannotCreateGoals()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
            var pendingField = typeof(CommanderChatUI).GetField("pendingClarification",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            using (var first = new ChatFixture(new SequenceSemanticProvider(proposal)))
            using (var foreign = new ChatFixture(new SequenceSemanticProvider(proposal)))
            using (var replacement = new EconomyFixture())
            using (var replacementDispatcher = new CommanderIntentDispatcher(replacement.Simulation, replacement.Manager))
            {
                first.Economy.Worker(0); foreign.Economy.Worker(0);
                await first.Chat.SubmitMessageAsync("build a Mine");
                var old = first.Chat.PendingClarification;
                pendingField.SetValue(foreign.Chat, old);
                await foreign.Chat.SubmitMessageAsync("yes");
                Assert.That(foreign.Economy.Manager.Goals, Is.Empty);
                Assert.That(foreign.Provider.Calls, Is.EqualTo(0));

                first.Chat.Initialize(first.Provider, replacement.Simulation, replacement.Manager, replacementDispatcher);
                pendingField.SetValue(first.Chat, old);
                await first.Chat.SubmitMessageAsync("yes");
                Assert.That(replacement.Manager.Goals, Is.Empty);
                Assert.That(first.Provider.Calls, Is.EqualTo(1));
            }
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal)))
            {
                f.Economy.Worker(0);
                await f.Chat.SubmitMessageAsync("build a Mine");
                var old = f.Chat.PendingClarification;
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                pendingField.SetValue(f.Chat, old);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public void OverlongGraphOriginal_IsRejectedWithoutTruncation()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            var draft = CommanderSemanticJson.Parse(GraphDraft(new JArray(mine), "Independent", 0,
                "structure", "Confirmation", new JValue("Mine"))).PendingGraphDraft;
            string exact = new string('x', CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters);
            Assert.That(new CommanderPendingClarification(draft, exact, "confirm", 1, 1).OriginalText, Is.EqualTo(exact));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommanderPendingClarification(draft,
                exact + "y", "confirm", 1, 1));
        }

        [Test]
        public async Task FourSlotGraph_AllLocalRepliesCompleteWithinBudget()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject() };
            var house = new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            var proposal = JObject.Parse(GraphDraft(new JArray(allocation, house), "Independent", 0, "count", "Count", new JValue(3)));
            var slots = (JArray)proposal["missingSlots"];
            slots.Add(new JObject { ["node"] = 0, ["path"] = "destination.resource", ["type"] = "CanonicalName",
                ["alternatives"] = new JArray("Food") });
            slots.Add(new JObject { ["node"] = 0, ["path"] = "destination.sourceKind", ["type"] = "SourceKind",
                ["alternatives"] = new JArray("Berries") });
            slots.Add(new JObject { ["node"] = 1, ["path"] = "structure", ["type"] = "Confirmation",
                ["alternatives"] = new JArray("House") });
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal.ToString())))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send three idle villagers to food and build a House");
                Assert.That(f.Chat.PendingClarification?.GraphDraft?.MissingSlots.Count, Is.EqualTo(4));
                foreach (string reply in new[] { "three", "Food", "from berries", "yes" })
                    await f.Chat.SubmitMessageAsync(reply);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        public async Task KnownSourceCorrection_ChangesOnlyNamedSource()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food", ["sourceKind"] = "Sheep" } };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "count", "Count", new JValue(3)))))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send idle villagers from sheep to food");
                await f.Chat.SubmitMessageAsync("from berries instead");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                var node = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
                Assert.That((string)node["destination"]["resource"], Is.EqualTo("Food"));
                Assert.That((string)node["destination"]["sourceKind"], Is.EqualTo("Berries"));
                await f.Chat.SubmitMessageAsync("three");
                Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation.Destination.SourceKind,
                    Is.EqualTo(ResourceSourceKind.Berries));
            }
        }

        [Test]
        public async Task IndependentBuildWithoutCorrectionMarker_SupersedesGraphDraft()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
                "Independent", 0, "structure", "Confirmation", new JValue("Mine")),
                Request("{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1}"))))
            {
                f.Economy.Worker(0);
                await f.Chat.SubmitMessageAsync("build a Mine");
                await f.Chat.SubmitMessageAsync("build a Mill");
                Assert.That(f.Provider.Calls, Is.EqualTo(2));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single().StructureType,
                    Is.EqualTo(BuildingType.Mill));
            }
        }

        [Test]
        public async Task AmbiguousKnownFieldCorrection_ReasksForOneNodeAndFieldLocally()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine.DeepClone(), mine.DeepClone()),
                "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
            {
                await f.Chat.SubmitMessageAsync("build two Mines");
                await f.Chat.SubmitMessageAsync("Mill instead");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("node"));
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("field"));
            }
        }

        [Test]
        public async Task NegatedStructureCorrection_RetainsMineTicketUntilYes()
        {
            foreach (string correction in new[] { "Don't change the Mine to a Mill", "do not change the Mine to a Mill", "never change the Mine to a Mill" })
            {
                var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
                using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
                    "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
                {
                    f.Economy.Worker(0);
                    await f.Chat.SubmitMessageAsync("build a Mine");
                    var old = f.Chat.PendingClarification;
                    var original = old.GraphDraft.ToPendingJson();
                    await f.Chat.SubmitMessageAsync(correction);
                    Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket), correction);
                    Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), original), Is.True, correction);
                    Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
                    Assert.That(f.Economy.Manager.Goals, Is.Empty, correction);
                    Assert.That(f.Economy.Commands(), Is.Empty, correction);
                    await f.Chat.SubmitMessageAsync("yes");
                    Assert.That(f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single().StructureType,
                        Is.EqualTo(BuildingType.Mine), correction);
                }
            }
        }

        [Test]
        public async Task NegatedCountCorrection_PreservesCountAndIndependentAmount()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 6,
                ["workers"] = new JObject { ["state"] = "Gathering", ["currentResource"] = "Wood" },
                ["destination"] = new JObject { ["resource"] = "Food" },
                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
            {
                for (int i = 0; i < 6; i++) f.Economy.Assigned(f.Economy.Worker(i), f.Economy.Node(ResourceType.Wood, 15 + i));
                f.Economy.Node(ResourceType.Food, 30);
                await f.Chat.SubmitMessageAsync("send six wood villagers to gather 400 additional food");
                var old = f.Chat.PendingClarification;
                var original = old.GraphDraft.ToPendingJson();
                await f.Chat.SubmitMessageAsync("don't change six villagers to three");
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket));
                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), original), Is.True);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                await f.Chat.SubmitMessageAsync("from berries");
                var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(retained.Count, Is.EqualTo(6));
                Assert.That(retained.ResourceAmount, Is.EqualTo(400));
                Assert.That(retained.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
                Assert.That(retained.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood));
            }
        }

        [Test]
        public async Task NegatedSourceCorrection_PreservesSheepUntilCountReply()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food", ["sourceKind"] = "Sheep" } };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "count", "Count", new JValue(3)))))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send idle villagers from sheep to food");
                var old = f.Chat.PendingClarification;
                var original = old.GraphDraft.ToPendingJson();
                await f.Chat.SubmitMessageAsync("do not change Sheep to Berries");
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket));
                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), original), Is.True);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                await f.Chat.SubmitMessageAsync("three");
                Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation.Destination.SourceKind,
                    Is.EqualTo(ResourceSourceKind.Sheep));
            }
        }

        [Test]
        public async Task ConditionalOrExampleCorrection_ReasksWithoutSupersedingMine()
        {
            foreach (string correction in new[] { "if we change Mine to Mill", "for example change Mine to Mill",
                "build a Mill instead if we have wood", "could we change Mine to Mill?" })
            {
                var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
                using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
                    "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
                {
                    f.Economy.Worker(0);
                    await f.Chat.SubmitMessageAsync("build a Mine");
                    var old = f.Chat.PendingClarification;
                    var original = old.GraphDraft.ToPendingJson();
                    await f.Chat.SubmitMessageAsync(correction);
                    Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket), correction);
                    Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), original), Is.True, correction);
                    Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
                    Assert.That(f.Economy.Manager.Goals, Is.Empty, correction);
                    Assert.That(f.Economy.Commands(), Is.Empty, correction);
                    await f.Chat.SubmitMessageAsync("yes");
                    Assert.That(f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single().StructureType,
                        Is.EqualTo(BuildingType.Mine), correction);
                }
            }
        }

        [Test]
        public async Task OldAmountWrongResource_ReasksAndRetainsAdditionalFood()
        { await AssertMismatchedAmountCorrectionRetainsDraft("change 400 wood to 500 food"); }

        [Test]
        public async Task OldAmountWrongMode_ReasksAndRetainsAdditionalFood()
        { await AssertMismatchedAmountCorrectionRetainsDraft("change 400 stockpile food to 500 food"); }

        [Test]
        public async Task NewAmountWrongResource_ReasksAndRetainsAdditionalFood()
        { await AssertMismatchedAmountCorrectionRetainsDraft("change 400 additional food to 500 wood"); }

        [Test]
        public async Task NewAmountWrongMode_ReasksAndRetainsAdditionalFood()
        { await AssertMismatchedAmountCorrectionRetainsDraft("change 400 additional food to 500 stockpile food"); }

        private static async Task AssertMismatchedAmountCorrectionRetainsDraft(string correction)
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 3,
                ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food" },
                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send three idle villagers to gather 400 additional food");
                var old = f.Chat.PendingClarification;
                var retained = old.GraphDraft.ToPendingJson();
                await f.Chat.SubmitMessageAsync(correction);
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket), correction);
                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), retained), Is.True, correction);
                Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
                Assert.That(f.Economy.Manager.Goals, Is.Empty, correction);
                Assert.That(f.Economy.Commands(), Is.Empty, correction);
                await f.Chat.SubmitMessageAsync("from berries");
                var admitted = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(admitted.Count, Is.EqualTo(3), correction);
                Assert.That(admitted.Destination.Resource, Is.EqualTo(ResourceType.Food), correction);
                Assert.That(admitted.ResourceAmount, Is.EqualTo(400), correction);
                Assert.That(admitted.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered), correction);
                Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
            }
        }

        [Test]
        public async Task QualifiedOldAmount_ChangesOnlyAmount()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 3,
                ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food" },
                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send three idle villagers to gather 400 additional food");
                var old = f.Chat.PendingClarification;
                await f.Chat.SubmitMessageAsync("change 400 additional food to 500 additional food");
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket));
                var node = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
                Assert.That((int)node["count"], Is.EqualTo(3));
                Assert.That((string)node["destination"]["resource"], Is.EqualTo("Food"));
                Assert.That((int)node["resourceAmount"], Is.EqualTo(500));
                Assert.That((string)node["resourceAmountMode"], Is.EqualTo("AdditionalGathered"));
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                await f.Chat.SubmitMessageAsync("from berries");
                var admitted = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(admitted.Count, Is.EqualTo(3));
                Assert.That(admitted.ResourceAmount, Is.EqualTo(500));
                Assert.That(admitted.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
            }
        }

        [Test]
        public async Task NamedOldToNewStructure_AdmitsOnlyMill()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
                "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
            {
                f.Economy.Worker(0);
                await f.Chat.SubmitMessageAsync("build a Mine");
                await f.Chat.SubmitMessageAsync("change Mine to Mill");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single().StructureType,
                    Is.EqualTo(BuildingType.Mill));
            }
        }

        [Test]
        public async Task NamedOldToNewSource_ChangesOnlySource()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food", ["sourceKind"] = "Sheep" } };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "count", "Count", new JValue(3)))))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send idle villagers from sheep to food");
                await f.Chat.SubmitMessageAsync("change Sheep to Berries");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                var node = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
                Assert.That((string)node["destination"]["resource"], Is.EqualTo("Food"));
                Assert.That((string)node["destination"]["sourceKind"], Is.EqualTo("Berries"));
                await f.Chat.SubmitMessageAsync("three");
                Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation.Destination.SourceKind,
                    Is.EqualTo(ResourceSourceKind.Berries));
            }
        }

        [Test]
        public async Task NamedOldToNewWorkerCount_PreservesAmount()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 6,
                ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food" },
                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
            {
                for (int i = 0; i < 6; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send six idle villagers to gather 400 additional food");
                await f.Chat.SubmitMessageAsync("change six villagers to three villagers");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                var node = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
                Assert.That((int)node["count"], Is.EqualTo(3));
                Assert.That((int)node["resourceAmount"], Is.EqualTo(400));
                await f.Chat.SubmitMessageAsync("from berries");
                var admitted = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(admitted.Count, Is.EqualTo(3));
                Assert.That(admitted.ResourceAmount, Is.EqualTo(400));
            }
        }
    }
}
```

### `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs.meta`

```meta
fileFormatVersion: 2
guid: c681daf630590fd46bd3fd8fadc1150b
```

### `Assets/Tests/EditMode/CommanderEconomyClarificationTests.HintGate.cs`

```cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        private const string LongbowRequest = "{\"outcome\":\"Request\",\"executionOrder\":\"Independent\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Longbowman\",\"count\":3,\"quantityMode\":\"New\"}]}";

        [TestCase("longbowmna", true)]
        [TestCase("Longbowman", false)]
        public async Task NearName_PartialCountRetainsOriginalNameRequirement(string name, bool needsName)
        {
            var node = (JObject)JObject.Parse(LongbowRequest)["nodes"][0];
            node.Remove("count");
            string proposal = GraphDraft(new JArray(node), "Independent", 0, "count", "Count", new JValue(3));
            using (var f = new HintFixture(proposal))
            {
                string original = "Train new " + name;
                await f.Chat.SubmitMessageAsync(original);
                var pending = f.Chat.PendingClarification;
                Assert.That(pending, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(JToken.DeepEquals(pending.GraphDraft.ToPendingJson(), JObject.Parse(proposal)["pending"]), Is.True);
                await f.Chat.SubmitMessageAsync("3");
                if (needsName)
                {
                    Assert.That(f.Economy.Manager.Goals, Is.Empty, "Count must not confirm the name.");
                    Assert.That(f.Economy.Commands(), Is.Empty);
                    Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(pending.RequestTicket));
                    Assert.That(f.Chat.PendingClarification.OriginalText, Is.EqualTo(original));
                    Assert.That(f.Chat.PendingClarification.RuntimeGeneration, Is.EqualTo(pending.RuntimeGeneration));
                    Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), JObject.Parse(LongbowRequest)), Is.True);
                    await f.Chat.SubmitMessageAsync("yes");
                }
                Assert.That(f.Chat.PendingClarification, Is.Null, f.Chat.DisplayedTranscript);
                var goal = f.Economy.Manager.Goals.Single();
                Assert.That(goal.RequestAuthority.RequestId, Is.EqualTo(pending.RequestTicket.Id));
                Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo(original));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task NearName_PartialCompoundCountAndSourceStillNeedNameAndGameplayApproval(bool correction)
        {
            var complete = JObject.Parse(LongbowRequest);
            complete["executionOrder"] = "Sequential";
            ((JObject)complete["nodes"][0])["constraints"] = new JArray(JObject.Parse("{\"type\":\"MaximumQueue\",\"amount\":2}"));
            ((JArray)complete["nodes"]).Add(JObject.Parse("{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":5,\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\",\"resource\":\"Wood\",\"sourceKind\":\"Tree\",\"dependsOn\":[0]}"));
            var partial = (JArray)complete["nodes"].DeepClone();
            ((JObject)partial[0]).Remove("count");
            if (!correction) ((JObject)partial[1]).Remove("sourceKind");
            else partial[1]["sourceKind"] = "Any";
            var proposal = JObject.Parse(GraphDraft(partial, "Sequential", 0, "count", "Count", new JValue(3)));
            if (!correction) ((JArray)proposal["missingSlots"]).Add(JObject.Parse("{\"node\":1,\"path\":\"sourceKind\",\"type\":\"SourceKind\",\"alternatives\":[\"Tree\"]}"));
            using (var f = new HintFixture(proposal.ToString(Formatting.None)))
            {
                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true).AutoProduceVillagers = false;
                const string original = "Train new longbowmna with maximum queue two, then send next five Villagers from my first Town Center to Wood";
                await f.Chat.SubmitMessageAsync(original);
                var pending = f.Chat.PendingClarification;
                Assert.That(pending, Is.Not.Null, f.Chat.DisplayedTranscript);
                await f.Chat.SubmitMessageAsync(correction ? "change Any to Tree" : "from trees");
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(pending.RequestTicket));
                await f.Chat.SubmitMessageAsync("3");
                Assert.That(f.Chat.PendingActionPlan, Is.Null, "Slot completion is not name confirmation.");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(pending.RequestTicket));
                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), complete), Is.True);
                await f.Chat.SubmitMessageAsync("Longbowman");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null, f.Chat.DisplayedTranscript);
                var nodes = f.Chat.PendingActionPlan.Interpretation.Nodes;
                Assert.That(nodes[0].Count, Is.EqualTo(3));
                Assert.That(((MaximumQueueConstraint)nodes[0].Constraints.Single()).MaximumQueue, Is.EqualTo(2));
                Assert.That(nodes[1].Count, Is.EqualTo(5));
                Assert.That(nodes[1].ProducerOrdinal, Is.EqualTo(1));
                Assert.That(nodes[1].SourceKind, Is.EqualTo(ResourceSourceKind.Tree));
                Assert.That(nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2), f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Manager.Goals.All(g => g.RequestAuthority.RequestId == pending.RequestTicket.Id), Is.True);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [TestCase("CanonicalName", "Longbowman")]
        [TestCase("Confirmation", "yes")]
        public async Task NearName_ExplicitNameSlotIsNotAskedAgainAfterCount(string type, string reply)
        {
            var node = (JObject)JObject.Parse(LongbowRequest)["nodes"][0];
            node.Remove("count");
            if (type == "CanonicalName") node.Remove("unit");
            var proposal = JObject.Parse(GraphDraft(new JArray(node), "Independent", 0, "unit", type, new JValue("Longbowman")));
            ((JArray)proposal["missingSlots"]).Add(JObject.Parse("{\"node\":0,\"path\":\"count\",\"type\":\"Count\",\"alternatives\":[3]}"));
            using (var f = new HintFixture(proposal.ToString(Formatting.None)))
            {
                await f.Chat.SubmitMessageAsync("Train new longbowmna");
                var ticket = f.Chat.PendingClarification.RequestTicket;
                await f.Chat.SubmitMessageAsync(reply);
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(ticket));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                await f.Chat.SubmitMessageAsync("3");
                Assert.That(f.Chat.PendingClarification, Is.Null, f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Manager.Goals.Single().RequestAuthority.RequestId, Is.EqualTo(ticket.Id));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_LegacyWorkerDraftCannotDropUnrepresentableName()
        {
            const string proposal = "{\"outcome\":\"Clarify\",\"message\":\"How many?\",\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"workers\":{\"state\":\"Idle\"},\"destination\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"}},\"missingFields\":[\"Count\"]}";
            using (var f = new HintFixture(proposal))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Wood, 12);
                const string original = "Send idle villagers to Wood then train longbowmna";
                await f.Chat.SubmitMessageAsync(original);
                var ticket = f.Chat.PendingClarification.RequestTicket;
                await f.Chat.SubmitMessageAsync("3");
                Assert.That(f.Economy.Manager.Goals, Is.Empty, "Legacy count draft cannot omit the hinted action and execute.");
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(ticket));
                Assert.That(f.Chat.PendingClarification.OriginalText, Is.EqualTo(original));
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("Blocked"));
                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), JObject.Parse(proposal)), Is.True);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                await f.Chat.SubmitMessageAsync("no");
                Assert.That(f.Chat.PendingClarification, Is.Null);
            }
        }

        [TestCase("yes")]
        [TestCase("Longbowman")]
        public async Task NearName_RequestCannotExecuteUntilLocalNameConfirmation(string reply)
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, f.Chat.DisplayedTranscript);
                var ticket = f.Chat.PendingClarification.RequestTicket;
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                await f.Chat.SubmitMessageAsync(reply);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                var goal = f.Economy.Manager.Goals.Single();
                Assert.That(goal.RequestAuthority.RequestId, Is.EqualTo(ticket.Id));
                Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo("Train three new longbowmna"));
            }
        }

        [Test]
        public async Task NearName_CompoundRetainsEveryFieldAndNeedsSeparateGameplayApproval()
        {
            var root = JObject.Parse(LongbowRequest);
            root["executionOrder"] = "Sequential";
            ((JArray)root["nodes"]).Add(JObject.Parse("{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":5,\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\",\"resource\":\"Wood\",\"sourceKind\":\"Tree\",\"dependsOn\":[0]}"));
            using (var f = new HintFixture(root.ToString(Formatting.None)))
            {
                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true).AutoProduceVillagers = false;
                string original = "Train three new longbowmna, then send the next five Villagers from my first Town Center to Wood from trees";
                await f.Chat.SubmitMessageAsync(original);
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, f.Chat.DisplayedTranscript);
                var ticket = f.Chat.PendingClarification.RequestTicket;
                Assert.That(f.Chat.PendingActionPlan, Is.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null, f.Chat.DisplayedTranscript);
                var nodes = f.Chat.PendingActionPlan.Interpretation.Nodes;
                Assert.That(nodes[0].Count, Is.EqualTo(3));
                Assert.That(nodes[0].QuantityMode, Is.EqualTo(CommanderProductionQuantityMode.New));
                Assert.That(nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
                Assert.That(nodes[1].Count, Is.EqualTo(5));
                Assert.That(nodes[1].ProducerOrdinal, Is.EqualTo(1));
                Assert.That(nodes[1].SourceKind, Is.EqualTo(ResourceSourceKind.Tree));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2), f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Manager.Goals.All(g => g.RequestAuthority.RequestId == ticket.Id), Is.True);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_DynamicProposalCannotBypassNameThenGameplayConfirmation()
        {
            Assert.That(CommanderIntentCatalog.TryResolveUnit("Longbowman", out int unit), Is.True);
            string proposal = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"train\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:" + unit + "\",\"count\":3,\"quantityMode\":\"New\"},\"inputs\":{},\"dependsOn\":[]}]}";
            using (var f = new HintFixture(proposal))
            {
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(f.Chat.PendingActionPlan, Is.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(f.Chat.PendingActionPlan.Interpretation.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_NoDismissesProposalOnly()
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new Longbowmen");
                int existing = f.Economy.Manager.Goals.Count;
                Assert.That(existing, Is.EqualTo(1), f.Chat.DisplayedTranscript);
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("no");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(existing));
                Assert.That(f.Transport.Calls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task NearName_UnrelatedProposalKeepsOriginalDraftBlockedLocally()
        {
            using (var f = new HintFixture(LongbowRequest.Replace("Longbowman", "Spearman")))
            {
                const string original = "Train three new longbowmna";
                await f.Chat.SubmitMessageAsync(original);
                Assert.That(f.Chat.PendingClarification?.OriginalText, Is.EqualTo(original));
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("Blocked"));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Chat.PendingClarification?.OriginalText, Is.EqualTo(original));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [TestCase("Longbowman")]
        [TestCase("Longbowmen")]
        public async Task NearName_ExactNamesAndEstablishedAliasesDoNotRequireConfirmation(string name)
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new " + name);
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1), f.Chat.DisplayedTranscript);
            }
        }

        [Test]
        public async Task NearName_ReadOnlyAnswerDoesNotCreatePendingAction()
        {
            using (var f = new HintFixture("{\"outcome\":\"Answer\",\"message\":\"Longbowman is an English unit.\"}"))
            {
                await f.Chat.SubmitMessageAsync("What is longbowmna?");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Chat.PendingActionPlan, Is.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("English unit"));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_PrunedModelHintsStillRequireGameOwnedConfirmation()
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                CommanderSemanticProviderRequest selected = null;
                for (int length = 0; length < 990; length += 10)
                {
                    string original = "Train three new longbowmna " + new string('\u754c', length);
                    var request = new CommanderSemanticProviderRequest(original,
                        new CommanderContextBuilder().Build(f.Economy.Simulation, f.Economy.Manager, original));
                    if (!CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096, out var pack, out _)) continue;
                    string user = (string)JObject.Parse(pack.Json)["messages"][1]["content"];
                    if (!user.Contains("nameInterpretations")) { selected = request; break; }
                }
                Assert.That(selected, Is.Not.Null, "Fixture must actually exercise optional hint pruning, not preflight rejection.");
                var result = await f.Provider.TranslateSemanticAsync(selected, CancellationToken.None);
                Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.Clarify));
                Assert.That(result.PendingGraphDraft, Is.Not.Null);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                Assert.That(f.Transport.Body, Does.Not.Contain("nameInterpretations"));
                Assert.That(f.Provider.LastInputEstimate, Is.LessThan(4000));
            }
        }

        [Test]
        public async Task NearName_ConfirmedCrossCivilizationUnitStillFailsNormalAdmission()
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                f.Economy.Simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.English });
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty, f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_OriginalTicketRejectsForeignResetAndReplay()
        {
            var pendingField = typeof(CommanderChatUI).GetField("pendingClarification",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            using (var f = new HintFixture(LongbowRequest))
            using (var foreign = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                var pending = f.Chat.PendingClarification;
                pendingField.SetValue(foreign.Chat, pending);
                await foreign.Chat.SubmitMessageAsync("yes");
                Assert.That(foreign.Economy.Manager.Goals, Is.Empty);
                Assert.That(foreign.Transport.Calls, Is.Zero);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                pendingField.SetValue(f.Chat, pending);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                f.Chat.ResetConversation();
                pendingField.SetValue(f.Chat, pending);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_LocalConfirmationRetainsFullStrictPayloadIncludingIndependentResourceAmount()
        {
            var root = JObject.Parse(LongbowRequest);
            root["executionOrder"] = "Sequential";
            ((JObject)root["nodes"][0])["constraints"] = new JArray(JObject.Parse("{\"type\":\"MaximumQueue\",\"amount\":2}"));
            ((JArray)root["nodes"]).Add(JObject.Parse("{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":3,\"workers\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\"},\"destination\":{\"resource\":\"Food\",\"sourceKind\":\"Sheep\"},\"resourceAmount\":400,\"resourceAmountMode\":\"AdditionalGathered\",\"dependsOn\":[0]}"));
            using (var f = new HintFixture(root.ToString(Formatting.None)))
            {
                const string original = "Train three new longbowmna with maximum queue two, then move three Wood gatherers to gather 400 additional Food from Sheep";
                await f.Chat.SubmitMessageAsync(original);
                var pending = f.Chat.PendingClarification;
                Assert.That(pending, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(JToken.DeepEquals(pending.GraphDraft.ToPendingJson(), root), Is.True,
                    "The confirmation adapter must retain the entire strict payload, not rebuild selected fields.");
                var resolved = pending.GraphDraft.Resolve("yes");
                Assert.That(resolved.IsValid, Is.True);
                Assert.That(((MaximumQueueConstraint)resolved.Nodes[0].Constraints.Single()).MaximumQueue, Is.EqualTo(2));
                var allocation = resolved.Nodes[1].WorkerAllocation;
                Assert.That(allocation.Count, Is.EqualTo(3));
                Assert.That(allocation.Mode, Is.EqualTo(CommanderWorkerAllocationMode.SelectedCount));
                Assert.That(allocation.CountMode, Is.EqualTo(CommanderWorkerCountMode.Exact));
                Assert.That(allocation.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood));
                Assert.That(allocation.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                Assert.That(allocation.ResourceAmount, Is.EqualTo(400));
                Assert.That(allocation.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
                Assert.That(resolved.Nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
                Assert.That(pending.OriginalText, Is.EqualTo(original));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty, "Detached local parsing carries no authority.");
            }
        }

        private sealed class HintTransport : ICommanderHttpTransport
        {
            internal int Calls;
            internal string Body;
            private readonly string response;
            internal HintTransport(string response) { this.response = response; }
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Calls++; Body = body;
                return Task.FromResult(new CommanderHttpResponse(200, new JObject { ["choices"] = new JArray(
                    new JObject { ["finish_reason"] = "stop", ["message"] = new JObject { ["content"] = response } }) }.ToString(Formatting.None)));
            }
        }

        private sealed class HintFixture : IDisposable
        {
            internal readonly EconomyFixture Economy = new EconomyFixture();
            internal readonly HintTransport Transport;
            internal readonly OpenRouterCommanderProvider Provider;
            internal readonly CommanderChatUI Chat;
            internal readonly CommanderIntentDispatcher Dispatcher;
            private readonly GameObject host;
            internal HintFixture(string response)
            {
                Economy.Simulation.SetPlayerCivilizations(new[] { Civilization.English, Civilization.English });
                Transport = new HintTransport(response);
                Provider = new OpenRouterCommanderProvider("fixture-not-a-credential", Transport);
                Dispatcher = new CommanderIntentDispatcher(Economy.Simulation, Economy.Manager);
                host = new GameObject("HintConfirmationFixture");
                Chat = host.AddComponent<CommanderChatUI>();
                Chat.Initialize(Provider, Economy.Simulation, Economy.Manager, Dispatcher);
            }
            public void Dispose()
            {
                typeof(CommanderChatUI).GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Chat, null);
                UnityEngine.Object.DestroyImmediate(host); Dispatcher.Dispose(); Economy.Dispose();
            }
        }
    }
}
```

### `Assets/Tests/EditMode/CommanderEconomyClarificationTests.HintGate.cs.meta`

```meta
fileFormatVersion: 2
guid: 29b7242ba11760c408b8319a59177696
```

### `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs`

```cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenEmpires.TestSupport;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusCorrection_TypedCountsPreserveOtherSlotAndUseNoSecondCall()
        {
            foreach (string id in new[] { "count-correction-only", "amount-correction-only" })
            {
                Assert.That(TryResponse(id, out string response), Is.True);
                using (var fixture = new CorpusFixture(response))
                {
                    fixture.Seed(id);
                    await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
                    Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                    string correction = id == "count-correction-only"
                        ? "Make that 4 villagers." : "Change the amount to 250.";
                    var local = fixture.Chat.PendingClarification.GraphDraft.Resolve(correction);
                    Assert.That(local?.PendingGraphDraft, Is.Not.Null, id + " local correction resolution");
                    await fixture.Chat.SubmitMessageAsync(correction);
                    Assert.That(fixture.Provider.Calls, Is.EqualTo(1), id);
                    var pending = fixture.Chat.PendingClarification?.GraphDraft;
                    Assert.That(pending, Is.Not.Null, id);
                    var node = (JObject)((JArray)pending.ToPendingJson()["nodes"])[0];
                    Assert.That((int)node["count"], Is.EqualTo(id == "count-correction-only" ? 4 : 3), id);
                    Assert.That((int)node["resourceAmount"], Is.EqualTo(id == "amount-correction-only" ? 250 : 200), id);
                    Assert.That((string)node["resourceAmountMode"], Is.EqualTo("Stockpile"), id);
                }
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusCancelReset_NegativeControlAndResetKeepLateYesInert()
        {
            Assert.That(TryResponse("cancel-reset-late-retry", out string response), Is.True);
            using (var fixture = new CorpusFixture(response))
            {
                fixture.Seed("cancel-reset-late-retry");
                await fixture.Chat.SubmitMessageAsync("Build a House by the visible gold.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                await fixture.Chat.SubmitMessageAsync("No, cancel that proposal.");
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(LatestUiState(fixture.Chat), Is.EqualTo("Cancelled"));
                await fixture.Chat.SubmitMessageAsync("Reset Commander.");
                Assert.That(LatestUiState(fixture.Chat), Is.EqualTo("Ready"));
                await fixture.Chat.SubmitMessageAsync("Yes.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusPunctuation_NoPeriodRejectsOnlyRetainedProposal()
        {
            Assert.That(TryResponse("mine-yes-original-request", out string response), Is.True);
            using (var fixture = new CorpusFixture(response))
            {
                fixture.Seed("mine-yes-original-request");
                await fixture.Chat.SubmitMessageAsync("Build a gold mine near gold.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                await fixture.Chat.SubmitMessageAsync("No.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(fixture.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusPunctuation_ResolvesRetainedGraphWithoutAnotherProviderCall()
        {
            Assert.That(TryResponse("mine-yes-original-request", out string mineResponse), Is.True);
            using (var fixture = new CorpusFixture(mineResponse))
            {
                fixture.Seed("mine-yes-original-request");
                await fixture.Chat.SubmitMessageAsync("Build a Mine near a visible gold deposit.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                var locallyResolved = fixture.Chat.PendingClarification.GraphDraft.Resolve("Yes.");
                Assert.That(locallyResolved?.IsValid, Is.True);
                Assert.That(fixture.Economy.Manager.TryCompileActionPlan(locallyResolved, out _, out string reason),
                    Is.True, reason);
                await fixture.Chat.SubmitMessageAsync("Yes.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(fixture.Economy.Manager.Goals.OfType<BuildStructureGoal>()
                    .Any(goal => goal.StructureType == BuildingType.Mine), Is.True);
            }
            Assert.That(TryResponse("house-next-five-from-trees", out string houseResponse), Is.True);
            using (var fixture = new CorpusFixture(houseResponse))
            {
                fixture.Seed("house-next-five-from-trees");
                await fixture.Chat.SubmitMessageAsync("Build a House and have the next five villagers gather wood from trees.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                await fixture.Chat.SubmitMessageAsync("From trees.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(fixture.Chat.PendingActionPlan, Is.Not.Null);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusDiscovery_TravelRevealsRequestedHostileAndStops()
        {
            Assert.That(TryResponse("finite-discovery-distinct-from-point-scout", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "finite-discovery-distinct-from-point-scout"))
            {
                Assert.That(fixture.Discovery.TargetInitiallyHidden, Is.True);
                Assert.That(fixture.Discovery.NoOrderControlStable, Is.True,
                    "Native fog bootstrap plus idle/no-order ticks must not reveal the hostile target.");
                await fixture.Chat.SubmitMessageAsync("Find the enemy Town Center with my Scout.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                fixture.TickBounded(1500);
                Assert.That(fixture.Commands, Does.Contain("MoveCommand"));
                Assert.That(fixture.Native.Any(entry => entry == "Move:Complete"), Is.True);
                Assert.That(fixture.Discovery.TravelRevealedCount(), Is.GreaterThan(0));
                Assert.That(fixture.Discovery.TargetEverVisibleAfterTravel, Is.True);
                Assert.That(fixture.Manager.Goals.Single().Status, Is.EqualTo(CommanderGoalStatus.Completed));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public void CorpusIdleOnlyFixture_ParsesStrictBuilderSelection()
        {
            Assert.That(TryResponse("idle-only-protection", out string response), Is.True);
            var parsed = CommanderSemanticJson.ParseProviderResponse(response);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.That(parsed.Nodes.Single().Constraints.OfType<PreferredWorkersConstraint>().Count(), Is.EqualTo(1));
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public void CorpusOrderedHistoricalSynthetic_RejectsAtStrictParseBoundary()
        {
            var oldResponse = Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2,\"quantityMode\":\"New\"},"
                + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,\"location\":\"WorkedResource\",\"resource\":\"Gold\",\"dependsOn\":[0],\"resultFromNode\":0}");
            var parsed = CommanderSemanticJson.ParseProviderResponse(oldResponse);
            Assert.That(parsed.IsValid, Is.False);
            Assert.That(parsed.SafeExplanation, Is.Not.Empty);
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusAssignedBuilder_NativeDeathIsNotConfusedWithHumanTakeover()
        {
            Assert.That(TryResponse("death-versus-takeover", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "death-versus-takeover"))
            {
                fixture.Seed("death-versus-takeover", deleteAssignedBuilder: true);
                Assert.That(fixture.BuilderAffinityVerified, Is.True);
                Assert.That(fixture.AssignedBuilderMissing, Is.True,
                    "The native delete must remove the already-attributed builder before the question.");
                int before = fixture.Commands.Count;
                await fixture.Chat.SubmitMessageAsync("What happened to the assigned builders?");
                Assert.That(QuestionFactClass(fixture.Provider.LastRequest), Is.EqualTo("dead-or-missing"));
                Assert.That(fixture.Commands.Count, Is.EqualTo(before));
                Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusUnattributedWorker_HumanStopDoesNotBecomeAnAssignedBuilderFact()
        {
            Assert.That(TryResponse("death-versus-takeover", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "unattributed-builder-control"))
            {
                fixture.Seed("unattributed-builder-control");
                Assert.That(fixture.BuilderAffinityVerified, Is.False);
                Assert.That(fixture.Native, Does.Not.Contain("PlaceBuildingCommand:processed"));
                await fixture.Chat.SubmitMessageAsync("What happened to the assigned builders?");
                Assert.That(QuestionFactClass(fixture.Provider.LastRequest), Is.Not.EqualTo("human-override"));
                Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusMalformedSubmission_RetainsRealTicketInsteadOfCaseLabel()
        {
            Assert.That(TryResponse("malformed-provider-json", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "malformed-provider-json"))
            {
                await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(RequestCorrelation(fixture, "malformed-provider-json"), Does.StartWith("runtime:"));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusReadOnlySubmission_HasNoExecutableTicketOrCaseLabel()
        {
            Assert.That(TryResponse("causal-information-owned-task-facts", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "causal-information-owned-task-facts"))
            {
                fixture.Seed("causal-information-owned-task-facts");
                await fixture.Chat.SubmitMessageAsync("Why is my troop production stuck?");
                Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
                Assert.That(RequestCorrelation(fixture, "causal-information-owned-task-facts"), Is.Null);
                Assert.That((string)NewTurn("causal-information-owned-task-facts", "Why is my troop production stuck?")["requestId"], Is.Null);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusMalformedVerdict_AcceptedResponseCannotRemainObservedRejection()
        {
            string accepted = Request(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile"));
            using (var fixture = new CorpusFixture(accepted, "malformed-provider-json"))
            {
                await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
                var row = NewTurn("malformed-provider-json", "Gather 200 gold with 3 villagers.");
                row["status"] = "observed";
                EvaluateFixture("malformed-provider-json", fixture, row);
                Assert.That((string)row["status"], Is.EqualTo("open-gap"));
                Assert.That((string)row["fidelity"], Is.EqualTo("mismatch"));
            }
        }

        [TestCase("{not-json")]
        [TestCase("faithful")]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusWrongVerdict_NonWrongResponseCannotRemainCertifiedWrong(string variant)
        {
            string response = variant == "faithful"
                ? Request(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile")) : variant;
            using (var fixture = new CorpusFixture(response, "schema-valid-wrong-provider-result"))
            {
                fixture.Seed("schema-valid-wrong-provider-result");
                await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
                var row = NewTurn("schema-valid-wrong-provider-result", "Gather 200 gold with 3 villagers.");
                row["status"] = "observed";
                EvaluateFixture("schema-valid-wrong-provider-result", fixture, row);
                Assert.That((string)row["status"], Is.EqualTo("open-gap"));
                Assert.That((string)row["fidelity"], Is.EqualTo("mismatch"));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusOrderedVerdict_ProcessedCallbackWithoutPatrollingActorIsGap()
        {
            Assert.That(TryResponse("ordered-production-exact-new-units", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "ordered-production-exact-new-units"))
            {
                fixture.Seed("ordered-production-exact-new-units");
                await fixture.Chat.SubmitMessageAsync("Train two spearmen, then send those new spearmen to patrol.");
                bool previewAtBase = fixture.Chat.PendingActionPlan?.Preview?
                    .IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0;
                await fixture.Chat.SubmitMessageAsync("Approve plan");
                fixture.TickBounded(Math.Min(8000, 4 * fixture.Simulation.Config.SpearmanTrainTimeTicks + 4000));
                var result = fixture.Manager.Goals.OfType<EnsureUnitCountGoal>().Single().ResultUnitIds;
                Assert.That(result.Count, Is.EqualTo(2));
                var actor = fixture.Simulation.UnitRegistry.GetUnit(result[0]);
                Assert.That(actor.IsPatrolling, Is.True, "The adversarial control starts from real native patrol state.");
                actor.ClearPatrol(); // replay a native skip/loss despite a processed callback
                var row = NewTurn("ordered-production-exact-new-units", "Train two spearmen, then send those new spearmen to patrol.");
                row["status"] = "observed"; row["approved"] = true;
                row["previewMentionsOwnedBase"] = previewAtBase;
                row["newResultCount"] = 2; row["oldUnitExcluded"] = true;
                row["humanBirthExcluded"] = true; row["exactPatrolBinding"] = true;
                EvaluateFixture("ordered-production-exact-new-units", fixture, row);
                Assert.That((string)row["status"], Is.EqualTo("open-gap"));
                Assert.That((string)row["fidelity"], Is.EqualTo("mismatch"));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public void CorpusPatrolPoint_StaysBesideOwnedBaseAndBlocksWithoutAnyReachablePoint()
        {
            using (var fixture = new CorpusFixture("{not-json", "ordered-production-exact-new-units"))
            {
                fixture.Seed("ordered-production-exact-new-units");
                fixture.TickBounded(1); // native fog bootstrap from the owned TC
                var tc = fixture.Simulation.BuildingRegistry.GetAllBuildings()
                    .Single(building => building.PlayerId == 0
                        && fixture.Simulation.GetEffectiveBuildingType(building) == BuildingType.TownCenter);
                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
                var executor = new CommanderCapabilityExecutor(fixture.Simulation);
                Assert.That(executor.TryCreateCommand(intent, out ICommand command, out string reason), Is.True, reason);
                var target = fixture.Simulation.MapData.WorldToTile(((PatrolCommand)command).TargetPosition);
                Assert.That(target.x < tc.OriginTileX || target.x >= tc.OriginTileX + tc.TileFootprintWidth
                    || target.y < tc.OriginTileZ || target.y >= tc.OriginTileZ + tc.TileFootprintHeight, Is.True);
                Assert.That(fixture.Simulation.MapData.IsWalkable(target.x, target.y, 0,
                    fixture.Simulation.BuildingRegistry), Is.True);

                // Remove every candidate inside the documented two-tile neighborhood;
                // the executor cannot widen the search or enqueue a prefix command.
                for (int x = tc.OriginTileX - 2; x < tc.OriginTileX + tc.TileFootprintWidth + 2; x++)
                    for (int z = tc.OriginTileZ - 2; z < tc.OriginTileZ + tc.TileFootprintHeight + 2; z++)
                        if (x < tc.OriginTileX || x >= tc.OriginTileX + tc.TileFootprintWidth
                            || z < tc.OriginTileZ || z >= tc.OriginTileZ + tc.TileFootprintHeight)
                            fixture.Simulation.MapData.Tiles[x, z] = TileType.Water;
                Assert.That(executor.TryCreateCommand(intent, out command, out reason), Is.False);
                Assert.That(command, Is.Null);
                Assert.That(reason, Does.Contain("complete route"));
                Assert.That(fixture.Simulation.CommandBuffer.FlushCommands(), Is.Empty);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusGarrison_CapacityAndProtectedRemainderAreNativeFacts()
        {
            Assert.That(TryResponse("garrison-capacity-protected-remainder", out string response), Is.True);
            using (var fixture = new CorpusFixture(response))
            {
                fixture.Seed("garrison-capacity-protected-remainder");
                await fixture.Chat.SubmitMessageAsync("Garrison all my villagers in my Town Center.");
                fixture.TickBounded(500);
                var goal = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>().Single();
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
                Assert.That(goal.GarrisonShelter.GarrisonCount, Is.EqualTo(1));
                Assert.That(goal.GarrisonProtectedCount, Is.EqualTo(1));
                Assert.That(goal.GarrisonUnaccommodatedCount, Is.EqualTo(1));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusReplay_UsesProductionChatBoundaryAndPersistsAllDeclaredCases()
        {
            var run = typeof(CommanderEconomyClarificationTests).GetMethod("RunPhase5CCorpusAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(run, Is.Not.Null, "The declared 25-case corpus needs an executable production-chat replay.");
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string corpus = Path.Combine(projectRoot, "Docs", "CommanderPhase5C", "evidence", "phase5c-replay-corpus.v1.json");
            string directory = Path.Combine(projectRoot, "Docs", "CommanderPhase5C", "evidence", "task-6-runs",
                Guid.NewGuid().ToString("N"));
            string result = await (Task<string>)run.Invoke(null, new object[] { corpus, directory });
            Assert.That(result, Is.EqualTo(directory));
            Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(25));
            var declared = (JArray)JObject.Parse(File.ReadAllText(corpus))["cases"];
            Assert.That(declared.Count, Is.EqualTo(25));
            int declaredTurns = 0, replayedDeclaredTurns = 0;
            foreach (JObject specification in declared)
            {
                string caseId = (string)specification["id"];
                int count = ((JArray)specification["turns"]).Count;
                JObject record = JObject.Parse(File.ReadAllText(Path.Combine(directory, caseId + ".json")));
                int replayed = ((JArray)record["turnHistory"]).Count(entry =>
                    (string)entry["origin"] != "offline-fixture-control");
                Assert.That(replayed, Is.EqualTo(count), caseId + " declared turns");
                declaredTurns += count; replayedDeclaredTurns += replayed;
            }
            Assert.That(declaredTurns, Is.EqualTo(35));
            Assert.That(replayedDeclaredTurns, Is.EqualTo(35));
            JObject mine = JObject.Parse(File.ReadAllText(Path.Combine(directory, "mine-yes-original-request.json")));
            JObject house = JObject.Parse(File.ReadAllText(Path.Combine(directory, "house-next-five-from-trees.json")));
            JObject malformed = JObject.Parse(File.ReadAllText(Path.Combine(directory, "malformed-provider-json.json")));
            JObject wrong = JObject.Parse(File.ReadAllText(Path.Combine(directory, "schema-valid-wrong-provider-result.json")));
            JObject ordered = JObject.Parse(File.ReadAllText(Path.Combine(directory, "ordered-production-exact-new-units.json")));
            JObject countCorrection = JObject.Parse(File.ReadAllText(Path.Combine(directory, "count-correction-only.json")));
            JObject amountCorrection = JObject.Parse(File.ReadAllText(Path.Combine(directory, "amount-correction-only.json")));
            JObject additionalFood = JObject.Parse(File.ReadAllText(Path.Combine(directory, "additional-gathered-food-bounded-objective.json")));
            JObject cancelled = JObject.Parse(File.ReadAllText(Path.Combine(directory, "cancel-reset-late-retry.json")));
            JObject garrison = JObject.Parse(File.ReadAllText(Path.Combine(directory, "garrison-capacity-protected-remainder.json")));
            JObject readOnly = JObject.Parse(File.ReadAllText(Path.Combine(directory, "causal-information-owned-task-facts.json")));
            Assert.That((string)mine["status"], Is.EqualTo("observed"));
            Assert.That((string)mine["fidelity"], Is.EqualTo("matched"));
            Assert.That((int)mine["providerCalls"], Is.EqualTo(1));
            Assert.That((string)house["status"], Is.EqualTo("observed"));
            Assert.That((string)house["fidelity"], Is.EqualTo("matched"));
            Assert.That((int?)house["futureObservedCount"], Is.EqualTo(5));
            Assert.That((int?)house["futureAssignedCount"], Is.EqualTo(5));
            Assert.That((int)house["providerCalls"], Is.EqualTo(1));
            Assert.That((string)malformed["errorStage"], Is.EqualTo("parse"));
            Assert.That((string)malformed["status"], Is.EqualTo("observed"));
            Assert.That((string)malformed["fidelity"], Is.EqualTo("not-evaluable"));
            Assert.That((bool?)malformed["semanticValid"], Is.False);
            Assert.That((string)malformed["errorCategory"], Is.EqualTo("malformed-rejected"));
            Assert.That((string)malformed["requestId"], Does.StartWith("runtime:"));
            Assert.That((string)malformed["turnCorrelationId"], Does.StartWith("ui-turn:"));
            Assert.That((int)malformed["providerCalls"], Is.EqualTo(1));
            Assert.That((int)malformed["commandCount"], Is.Zero);
            Assert.That(((JArray)malformed["admittedFields"]).Count, Is.Zero);
            Assert.That((string)wrong["status"], Is.EqualTo("observed"));
            Assert.That((string)wrong["fidelity"], Is.EqualTo("wrong"));
            Assert.That((bool?)wrong["semanticValid"], Is.True);
            Assert.That((string)wrong["errorCategory"], Is.EqualTo("schema-valid-wrong-action"));
            Assert.That(((JArray)wrong["admittedFields"]).Count, Is.EqualTo(1));
            Assert.That((string)wrong["admittedFields"][0]["originalUtterance"],
                Is.EqualTo("Gather 200 gold with 3 villagers."));
            Assert.That((string)wrong["admittedFields"][0]["destinationResource"], Is.EqualTo("Wood"));
            Assert.That((int)wrong["admittedFields"][0]["count"], Is.EqualTo(2));
            Assert.That((string)readOnly["requestId"], Is.Null);
            Assert.That((string)readOnly["turnCorrelationId"], Does.StartWith("ui-turn:"));
            Assert.That((int)readOnly["commandCount"], Is.Zero);
            Assert.That((bool?)ordered["semanticValid"], Is.True);
            Assert.That((string)ordered["status"], Is.EqualTo("observed"));
            Assert.That((string)ordered["fidelity"], Is.EqualTo("matched"));
            Assert.That((int?)ordered["newResultCount"], Is.EqualTo(2));
            Assert.That((bool?)ordered["oldUnitExcluded"], Is.True);
            Assert.That((bool?)ordered["humanBirthExcluded"], Is.True);
            Assert.That((bool?)ordered["exactPatrolBinding"], Is.True);
            Assert.That((int?)ordered["nativePatrolActorCount"], Is.EqualTo(2));
            Assert.That((bool?)ordered["nativePatrolWaypointsMatch"], Is.True);
            Assert.That((bool?)ordered["nativePatrolDestinationMatch"], Is.True);
            Assert.That((bool?)ordered["previewMentionsOwnedBase"], Is.True);
            Assert.That((string)countCorrection["status"], Is.EqualTo("observed"));
            Assert.That((string)amountCorrection["status"], Is.EqualTo("observed"));
            Assert.That((string)countCorrection["fidelity"], Is.EqualTo("matched"));
            Assert.That((string)amountCorrection["fidelity"], Is.EqualTo("matched"));
            Assert.That((string)((JArray)countCorrection["admittedFields"])[0]["mode"], Is.EqualTo("TargetTotal"));
            Assert.That((string)((JArray)amountCorrection["admittedFields"])[0]["mode"], Is.EqualTo("TargetTotal"));
            Assert.That((int)countCorrection["providerCalls"], Is.EqualTo(1));
            Assert.That((int)amountCorrection["providerCalls"], Is.EqualTo(1));
            Assert.That((string)additionalFood["status"], Is.EqualTo("observed"));
            Assert.That((string)additionalFood["fidelity"], Is.EqualTo("matched"));
            Assert.That((int?)additionalFood["resourceProgress"], Is.EqualTo(400));
            Assert.That((long?)additionalFood["gatheredFoodIncome"], Is.GreaterThan(400));
            Assert.That((bool?)additionalFood["ordinaryFoodGatherActive"], Is.True);
            Assert.That((string)countCorrection["requestId"], Does.StartWith("runtime:"));
            Assert.That((string)amountCorrection["requestId"], Does.StartWith("runtime:"));
            Assert.That((string)cancelled["uiState"], Is.EqualTo("Ready"));
            Assert.That(((JArray)cancelled["normalizedFields"]), Is.Empty);
            Assert.That((string)cancelled["requestId"], Does.StartWith("runtime:"));
            Assert.That((bool?)mine["packAccepted"], Is.True);
            Assert.That((int?)mine["estimatedInputTokens"], Is.GreaterThan(0));
            Assert.That((int?)garrison["garrisonHousedCount"], Is.EqualTo(1));
            Assert.That((int?)garrison["garrisonProtectedCount"], Is.EqualTo(1));
            Assert.That((int?)garrison["garrisonUnaccommodatedCount"], Is.EqualTo(1));
        }

        // The provider is substituted only at the external semantic boundary. Every turn still
        // goes through the production CommanderChatUI and its normal admission/goal path.
        private sealed class CorpusProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private readonly Queue<string> responses;
            internal int Calls;
            internal CommanderSemanticResult LastResult;
            internal CommanderSemanticProviderRequest LastRequest;
            internal long FirstCallTicks;
            internal long LastReturnTicks;
            internal double? ParseMs;
            internal double? PackMs;
            internal int? InputEstimate;
            internal bool PackSucceeded;
            internal CorpusProvider(string response) => responses = new Queue<string>(new[] { response });
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,
                CancellationToken token)
            {
                if (FirstCallTicks == 0) FirstCallTicks = Stopwatch.GetTimestamp();
                LastRequest = request;
                Calls++;
                if (responses.Count == 0) throw new InvalidOperationException("Unexpected second fixture translation.");
                long packStart = Stopwatch.GetTimestamp();
                PackSucceeded = CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                    out var pack, out _);
                PackMs = Milliseconds(packStart, Stopwatch.GetTimestamp());
                InputEstimate = pack?.EstimatedInputTokens;
                long started = Stopwatch.GetTimestamp();
                LastResult = CommanderSemanticJson.ParseProviderResponse(responses.Dequeue());
                LastReturnTicks = Stopwatch.GetTimestamp();
                ParseMs = Milliseconds(started, LastReturnTicks);
                return Task.FromResult(LastResult);
            }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token) =>
                Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "Legacy provider path is not part of the corpus."));
        }

        private sealed class CorpusFixture : IDisposable
        {
            internal readonly EconomyFixture Economy = new EconomyFixture();
            internal readonly DiscoveryFixture Discovery;
            internal GameSimulation Simulation => Discovery?.Simulation ?? Economy.Simulation;
            internal CommanderGoalManager Manager => Discovery?.Manager ?? Economy.Manager;
            internal readonly CorpusProvider Provider;
            internal readonly CommanderIntentDispatcher Dispatcher;
            internal readonly CommanderChatUI Chat;
            internal readonly List<string> Commands = new List<string>();
            internal readonly List<string> Native = new List<string>();
            internal long FirstNativeTicks;
            internal int InitialGoalCount;
            internal int InitialCommanderCommandCount;
            internal int HumanTrainCommands;
            internal int? FoodCompletionTick;
            internal long? FoodIncomeAtCompletion;
            internal int? CommanderCommandsAtFoodCompletion;
            internal bool OrdinaryFoodGatherActive;
            internal int OldSpearmanId = -1;
            internal int HumanBirthId = -1;
            internal int[] PatrolSubjects;
            internal FixedVector3? PatrolTarget;
            internal bool BuilderAffinityVerified;
            internal bool AssignedBuilderHumanProtected;
            internal bool AssignedBuilderMissing;
            private readonly string caseId;
            private bool houseHumanQueueSeeded;
            private readonly GameObject host;
            internal CorpusFixture(string response, string id = null)
            {
                caseId = id;
                if (id == "finite-discovery-distinct-from-point-scout") Discovery = new DiscoveryFixture();
                Provider = new CorpusProvider(response);
                Dispatcher = new CommanderIntentDispatcher(Simulation, Manager);
                host = new GameObject("Phase5CCorpusProductionChat");
                Chat = host.AddComponent<CommanderChatUI>();
                Chat.Initialize(Provider, Simulation, Manager, Dispatcher);
                Simulation.CommandBuffer.CommandEnqueued += OnCommandEnqueued;
                Simulation.NativeMoveProcessed += OnMoveProcessed;
                Simulation.NativeUnitCommandProcessed += OnUnitCommandProcessed;
            }
            private void OnCommandEnqueued(ICommand command, CommandEnqueueSource source)
            {
                if (source == CommandEnqueueSource.Commander && Commands.Count < 32)
                    Commands.Add(command.GetType().Name);
                if (source == CommandEnqueueSource.Commander && command is PatrolCommand patrol)
                {
                    PatrolSubjects = patrol.UnitIds?.ToArray();
                    PatrolTarget = patrol.TargetPosition;
                }
            }
            private void OnMoveProcessed(NativeMoveProcessingReceipt receipt)
            {
                if (FirstNativeTicks == 0) FirstNativeTicks = Stopwatch.GetTimestamp();
                if (Native.Count < 32) Native.Add("Move:" + receipt.Result);
            }
            private void OnUnitCommandProcessed(NativeUnitCommandObservation observation)
            {
                if (FirstNativeTicks == 0) FirstNativeTicks = Stopwatch.GetTimestamp();
                if (Native.Count < 32) Native.Add(observation.ExecutingCommand.GetType().Name + ":processed");
            }
            internal void Seed(string id, bool deleteAssignedBuilder = false)
            {
                if (Discovery != null) return;
                if (id == "mine-yes-original-request")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    Economy.Worker(0); Economy.Node(ResourceType.Gold, 8);
                }
                else if (id == "house-next-five-from-trees")
                {
                    Economy.Worker(0);
                    var producer = Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true);
                    producer.AutoProduceVillagers = false;
                    Economy.Node(ResourceType.Wood, 25);
                }
                else if (id.StartsWith("worker-mode-", StringComparison.Ordinal))
                {
                    for (int i = 0; i < 8; i++) Economy.Worker(i);
                    Economy.Node(ResourceType.Food, 8);
                }
                else if (id == "amount-versus-worker-count" || id == "count-correction-only"
                    || id == "amount-correction-only" || id == "additional-gathered-food-bounded-objective")
                {
                    for (int i = 0; i < 6; i++) Economy.Worker(i);
                    Economy.Node(ResourceType.Gold, 14);
                    if (id == "additional-gathered-food-bounded-objective")
                        Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                            Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    Economy.Node(ResourceType.Food,
                        id == "additional-gathered-food-bounded-objective" ? 8 : 20);
                }
                else if (id == "all-current-wood-to-berries" || id == "exact-current-wood-to-berries")
                {
                    var wood = Economy.Node(ResourceType.Wood, 15);
                    Economy.Node(ResourceType.Food, 24);
                    for (int i = 0; i < 7; i++) Economy.Assigned(Economy.Worker(i), wood);
                }
                else if (id == "all-wood-west-tc-explicit-move" || id == "all-wood-west-tc-explicit-gather")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    var oldWood = Economy.Node(ResourceType.Wood, 18);
                    Economy.Simulation.MapData.AddResourceNode(ResourceType.Wood,
                        Economy.Simulation.MapData.TileToWorldFixed(Economy.Base.x - 14, Economy.Base.y + 4), 10000);
                    for (int i = 0; i < 3; i++) Economy.Assigned(Economy.Worker(i), oldWood);
                }
                else if (id == "idle-only-protection" || id == "two-houses-near-town-center"
                    || id == "cancel-reset-late-retry")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    for (int i = 0; i < 3; i++) Economy.Worker(i);
                    Economy.Node(ResourceType.Gold, 18);
                }
                else if (id == "occupied-farms-and-capacity")
                {
                    var farmA = Economy.Farm(16); var farmB = Economy.Farm(24);
                    Economy.Farm(32); Economy.Farm(40);
                    Economy.Assigned(Economy.Worker(0), farmA);
                    Economy.Assigned(Economy.Worker(1), farmB);
                    for (int i = 2; i < 6; i++) Economy.Worker(i);
                }
                else if (id == "garrison-capacity-protected-remainder")
                {
                    var center = Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true);
                    center.GarrisonCapacity = 1; center.AutoProduceVillagers = false;
                    var protectedWorker = Economy.Worker(0);
                    for (int i = 1; i < 3; i++) Economy.Worker(i);
                    Economy.Simulation.CommandBuffer.EnqueueCommand(
                        new StopCommand(0, new[] { protectedWorker.Id }), CommandEnqueueSource.Human);
                    Economy.Simulation.CommandBuffer.FlushCommands(); // retain the human protection lease
                }
                else if (id == "ordered-production-exact-new-units")
                {
                    Economy.Simulation.SetPlayerAge(0, 3);
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    Economy.Simulation.CreateBuilding(0, BuildingType.Barracks,
                        Economy.Base.x + 10, Economy.Base.y, false, true);
                    var old = Economy.Simulation.UnitRegistry.CreateUnit(0,
                        Economy.Simulation.MapData.TileToWorldFixed(Economy.Base.x + 9, Economy.Base.y + 7),
                        Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                    old.UnitType = 1; old.MaxHealth = old.CurrentHealth = 100;
                    OldSpearmanId = old.Id;
                }
                else if (id == "defense-arrival-native-receipt")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    for (int i = 0; i < 6; i++)
                    {
                        var unit = Economy.Simulation.UnitRegistry.CreateUnit(0,
                            Economy.Simulation.MapData.TileToWorldFixed(Economy.Base.x + 10 + i, Economy.Base.y + 8),
                            Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                        unit.UnitType = 1; unit.MaxHealth = unit.CurrentHealth = 100;
                    }
                }
                else if (id == "schema-valid-wrong-provider-result") Economy.Worker(0);
                else if (id == "causal-information-owned-task-facts"
                    || id == "death-versus-takeover" || id == "unattributed-builder-control")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    if (id == "causal-information-owned-task-facts")
                    {
                        Economy.Simulation.CreateBuilding(0, BuildingType.Barracks,
                            Economy.Base.x + 10, Economy.Base.y, false, true);
                        Economy.Manager.SubmitEnsureUnitCount(1, 4, newProductionCount: 4);
                    }
                    else
                    {
                        Economy.Worker(0);
                        Economy.Manager.SubmitBuildStructure(BuildingType.House, 1);
                    }
                }
                var stock = Economy.Simulation.ResourceManager.GetPlayerResources(0);
                stock.Food = stock.Wood = stock.Gold = stock.Stone = 2000;
                if (id == "amount-versus-worker-count" || id == "count-correction-only"
                    || id == "amount-correction-only") stock.Gold = 0;
                if (id == "causal-information-owned-task-facts") stock.Wood = 0;
                if (id == "unattributed-builder-control") stock.Wood = 0;
                if (id == "ordered-production-exact-new-units")
                {
                    var barracks = Economy.Simulation.BuildingRegistry.GetAllBuildings()
                        .Single(building => building.PlayerId == 0 && building.Type == BuildingType.Barracks);
                    Economy.Simulation.CommandBuffer.EnqueueCommand(
                        new TrainUnitCommand(0, barracks.Id, 1), CommandEnqueueSource.Human);
                    HumanTrainCommands++;
                    Economy.Simulation.Tick(); // native acceptance; the ordinary human birth remains queued first
                }
                if (id == "causal-information-owned-task-facts" || id == "death-versus-takeover"
                    || id == "unattributed-builder-control")
                {
                    Economy.Manager.Tick(0);
                    if (id == "death-versus-takeover")
                    {
                        Economy.Simulation.Tick(); // native placement must create the exact owned foundation first
                        var build = Manager.Goals.OfType<BuildStructureGoal>().Single();
                        var assigned = Economy.Simulation.UnitRegistry.GetAllUnits()
                            .Single(unit => unit.PlayerId == 0 && unit.IsVillager);
                        BuilderAffinityVerified = build.PlacedBuildingId >= 0
                            && build.AttributedBuildingIds.Contains(build.PlacedBuildingId)
                            && build.LastAcceptedBuilderUnitIds.SequenceEqual(new[] { assigned.Id })
                            && assigned.ConstructionTargetBuildingId == build.PlacedBuildingId
                            && Native.Contains("PlaceBuildingCommand:processed");
                        Economy.Simulation.CommandBuffer.EnqueueCommand(
                            deleteAssignedBuilder
                                ? (ICommand)new DeleteUnitsCommand(0, new[] { assigned.Id })
                                : new StopCommand(0, new[] { assigned.Id }),
                            CommandEnqueueSource.Human);
                        Economy.Simulation.Tick(); // native human control, not an injected status flag
                        AssignedBuilderHumanProtected = Manager.ObserveHumanProtection(assigned.Id);
                        AssignedBuilderMissing = Economy.Simulation.UnitRegistry.GetUnit(assigned.Id) == null;
                        Economy.Manager.Tick(15);
                    }
                    if (id == "unattributed-builder-control")
                    {
                        var idle = Economy.Simulation.UnitRegistry.GetAllUnits()
                            .Single(unit => unit.PlayerId == 0 && unit.IsVillager);
                        Economy.Simulation.CommandBuffer.EnqueueCommand(
                            new StopCommand(0, new[] { idle.Id }), CommandEnqueueSource.Human);
                        Economy.Simulation.Tick();
                        Economy.Manager.Tick(15);
                    }
                    InitialGoalCount = Manager.Goals.Count;
                    InitialCommanderCommandCount = Commands.Count;
                }
            }
            internal int TickBounded(int bound)
            {
                for (int i = 0; i < bound; i++)
                {
                    if (i % 15 == 0) Manager.Tick(i);
                    if (caseId == "additional-gathered-food-bounded-objective"
                        && FoodCompletionTick == null
                        && Manager.Goals.OfType<AllocateWorkersGoal>()
                            .Any(goal => goal.Status == CommanderGoalStatus.Completed))
                    {
                        FoodCompletionTick = i;
                        FoodIncomeAtCompletion = Simulation.ResourceManager.GetGatheredIncome(0, ResourceType.Food);
                        CommanderCommandsAtFoodCompletion = Commands.Count;
                    }
                    if (caseId == "house-next-five-from-trees")
                    {
                        var townCenter = Simulation.BuildingRegistry.GetAllBuildings()
                            .FirstOrDefault(building => building.PlayerId == 0
                                && building.Type == BuildingType.TownCenter);
                        if (!houseHumanQueueSeeded && Manager.Goals.OfType<BuildStructureGoal>()
                                .Any(goal => goal.Status == CommanderGoalStatus.Completed))
                        {
                            for (int birth = 0; birth < 5; birth++)
                            {
                                Simulation.CommandBuffer.EnqueueCommand(
                                    new TrainUnitCommand(0, townCenter.Id, 0), CommandEnqueueSource.Human);
                                HumanTrainCommands++;
                            }
                            houseHumanQueueSeeded = true;
                        }
                    }
                    Simulation.Tick(); Discovery?.ObserveAfterNativeTick();
                    if (caseId == "ordered-production-exact-new-units" && HumanBirthId < 0)
                    {
                        var newborn = Simulation.UnitRegistry.GetAllUnits()
                            .Where(unit => unit.PlayerId == 0 && unit.UnitType == 1
                                && unit.CurrentHealth > 0 && unit.Id != OldSpearmanId)
                            .OrderBy(unit => unit.Id).FirstOrDefault();
                        if (newborn != null) HumanBirthId = newborn.Id;
                    }
                }
                if (caseId == "additional-gathered-food-bounded-objective")
                    OrdinaryFoodGatherActive = Simulation.UnitRegistry.GetAllUnits().Any(unit =>
                        unit.PlayerId == 0 && unit.IsVillager && unit.CurrentHealth > 0
                        && unit.TargetResourceNodeId >= 0
                        && Simulation.MapData.GetResourceNode(unit.TargetResourceNodeId)?.Type == ResourceType.Food);
                return bound;
            }
            public void Dispose()
            {
                Simulation.CommandBuffer.CommandEnqueued -= OnCommandEnqueued;
                Simulation.NativeMoveProcessed -= OnMoveProcessed;
                Simulation.NativeUnitCommandProcessed -= OnUnitCommandProcessed;
                if (host != null)
                {
                    if (!Application.isPlaying) typeof(CommanderChatUI).GetMethod("OnDestroy",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Chat, null);
                    UnityEngine.Object.DestroyImmediate(host);
                }
                Dispatcher.Dispose(); Discovery?.Dispose(); Economy.Dispose();
            }
        }

        private sealed class DiscoveryFixture : IDisposable
        {
            private readonly SimulationConfig config;
            internal readonly GameSimulation Simulation;
            internal readonly CommanderGoalManager Manager;
            private readonly bool[,] initiallyUnexplored;
            private readonly BuildingData target;
            private readonly UnitData scout;
            private readonly Vector2Int scoutBaseline;
            internal readonly bool TargetInitiallyHidden;
            internal readonly bool NoOrderControlStable;
            internal bool TargetEverVisibleAfterTravel { get; private set; }
            internal DiscoveryFixture()
            {
                config = ScriptableObject.CreateInstance<SimulationConfig>();
                Simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                int x = Simulation.MapData.Width / 2, z = Simulation.MapData.Height / 2;
                for (int tx = x - 7; tx <= x + 7; tx++)
                    for (int tz = z - 7; tz <= z + 7; tz++)
                    {
                        Simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                        Simulation.MapData.ForestDensity[tx, tz] = 0;
                        Simulation.MapData.FoundationCount[tx, tz] = 0;
                        Simulation.FogOfWar.SetVisible(0, tx, tz);
                    }
                // Discovery requires an owned Scout, not an owned completed TC. Omitting the TC
                // prevents its radius-20 building vision from substituting for Scout travel.
                scout = Simulation.UnitRegistry.CreateUnit(0, Simulation.MapData.TileToWorldFixed(x - 1, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100; scout.State = UnitState.Idle;
                target = Simulation.CreateBuilding(1, BuildingType.TownCenter, x - 10, z - 9, false, true);
                Simulation.Tick(); // Native owner-vision bootstrap before the observed baseline.
                TargetInitiallyHidden = Simulation.FogOfWar.GetVisibility(0, target.OriginTileX, target.OriginTileZ)
                    == TileVisibility.Unexplored;
                var scoutStart = Simulation.MapData.WorldToTile(scout.SimPosition);
                for (int i = 0; i < 30; i++) Simulation.Tick(); // no Commander/human orders
                var scoutAfterControl = Simulation.MapData.WorldToTile(scout.SimPosition);
                NoOrderControlStable = scoutAfterControl == scoutStart
                    && Simulation.FogOfWar.GetVisibility(0, target.OriginTileX, target.OriginTileZ)
                        == TileVisibility.Unexplored;
                scoutBaseline = scoutAfterControl;
                var stock = Simulation.ResourceManager.GetPlayerResources(0);
                stock.Food = stock.Wood = stock.Gold = stock.Stone = 2000;
                initiallyUnexplored = new bool[Simulation.MapData.Width, Simulation.MapData.Height];
                for (int tx = 0; tx < Simulation.MapData.Width; tx++)
                    for (int tz = 0; tz < Simulation.MapData.Height; tz++)
                        initiallyUnexplored[tx, tz] = Simulation.FogOfWar.GetVisibility(0, tx, tz) == TileVisibility.Unexplored;
                Manager = new CommanderGoalManager(Simulation, 0);
            }
            internal int TravelRevealedCount()
            {
                int count = 0;
                for (int tx = 0; tx < Simulation.MapData.Width; tx++)
                    for (int tz = 0; tz < Simulation.MapData.Height; tz++)
                        if (initiallyUnexplored[tx, tz]
                            && Simulation.FogOfWar.GetVisibility(0, tx, tz) != TileVisibility.Unexplored) count++;
                return count;
            }
            internal bool TargetNowVisible => Simulation.FogOfWar.GetVisibility(0, target.OriginTileX,
                target.OriginTileZ) == TileVisibility.Visible;
            internal void ObserveAfterNativeTick()
            {
                if (Simulation.MapData.WorldToTile(scout.SimPosition) != scoutBaseline && TargetNowVisible)
                    TargetEverVisibleAfterTravel = true;
            }
            public void Dispose() { Manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        private static async Task<string> RunPhase5CCorpusAsync(string corpusPath, string directory)
        {
            JObject corpus = JObject.Parse(File.ReadAllText(corpusPath));
            if ((int?)corpus["schemaVersion"] != 1 || (string)corpus["executionState"] != "not-run")
                throw new InvalidOperationException("Unexpected corpus version or execution state.");
            JArray cases = corpus["cases"] as JArray;
            if (cases == null || cases.Count != 25) throw new InvalidOperationException("The bounded corpus must contain 25 cases.");
            string runId = Guid.NewGuid().ToString("D");
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject item in cases)
            {
                string id = (string)item["id"];
                if (!uniqueIds.Add(id)) throw new InvalidOperationException("Duplicate corpus case.");
                var turns = item["turns"] as JArray;
                if (turns == null || turns.Count < 1 || turns.Count > 4)
                    throw new InvalidOperationException("Unbounded corpus turns.");
                string original = (string)turns[0];
                string path = Path.Combine(directory, id + ".json");
                var evidence = new ScenarioEvidenceRecorder(path, runId);
                var row = NewTurn(id, original);
                var turnHistory = (JArray)row["turnHistory"];
                string correlationId = null;
                evidence.RecordPhase5CTurn(row); // durable before any provider/UI work
                if (!TryResponse(id, out string response))
                {
                    row["status"] = "open-gap";
                    row["gap"] = "No independent synthetic provider fixture or seeded native setup is declared for this case.";
                    row["errorStage"] = "fixture-coverage";
                    row["errorCategory"] = "not-executed";
                    evidence.RecordPhase5CTurn(row);
                    continue;
                }
                long started = Stopwatch.GetTimestamp();
                try
                {
                    using (var fixture = new CorpusFixture(response, id))
                    {
                        fixture.Seed(id);
                        row["fixtureKind"] = id == "malformed-provider-json" ? "malformed"
                            : id == "schema-valid-wrong-provider-result" ? "schema-valid-wrong" : "correct-synthetic";
                        for (int i = 0; i < turns.Count; i++)
                        {
                            string turn = (string)turns[i];
                            var before = Missing(fixture.Chat.PendingClarification);
                            var beforeFields = fixture.Chat.PendingClarification?.GraphDraft == null
                                ? new JArray() : ProjectGraphDraft(fixture.Chat.PendingClarification.GraphDraft);
                            bool awaitingApproval = fixture.Chat.PendingActionPlan != null;
                            row["turnIndex"] = i;
                            row["turnText"] = turn;
                            row["draftBefore"] = before;
                            long turnStart = Stopwatch.GetTimestamp();
                            int callsBefore = fixture.Provider.Calls;
                            CommanderAIChatSubmission submission = await fixture.Chat.SubmitMessageAsync(turn);
                            row["uiSubmitMs"] = Milliseconds(turnStart, Stopwatch.GetTimestamp());
                            row["draftAfter"] = Missing(fixture.Chat.PendingClarification);
                            var afterFields = fixture.Chat.PendingClarification?.GraphDraft == null
                                ? new JArray() : ProjectGraphDraft(fixture.Chat.PendingClarification.GraphDraft);
                            row["draftChanges"] = DraftDelta(before, (JArray)row["draftAfter"], beforeFields, afterFields);
                            row["providerCalls"] = fixture.Provider.Calls;
                            row["packAccepted"] = fixture.Provider.Calls == 0 ? JValue.CreateNull()
                                : (JToken)fixture.Provider.PackSucceeded;
                            CommanderSemanticResult observedSemantic = fixture.Chat.PendingActionPlan?.Interpretation
                                ?? (fixture.Provider.Calls > callsBefore ? fixture.Provider.LastResult : null);
                            row["semanticValid"] = observedSemantic?.IsValid;
                            row["semanticOutcome"] = observedSemantic?.Outcome.ToString()
                                ?? (submission?.Success == true ? "Request" : null);
                            if (observedSemantic != null) row["normalizedFields"] = NormalizeCorpus(observedSemantic);
                            else if (fixture.Chat.PendingClarification?.GraphDraft != null)
                                row["normalizedFields"] = ProjectGraphDraft(fixture.Chat.PendingClarification.GraphDraft);
                            else row["normalizedFields"] = new JArray();
                            row["admittedFields"] = AdmittedFields(fixture);
                            row["ownedTaskFactCount"] = OwnedTaskFactCount(fixture.Provider.LastRequest);
                            row["readOnlyProviderRequest"] = fixture.Provider.LastRequest?.IsReadOnlyQuestion;
                            row["questionFactClass"] = QuestionFactClass(fixture.Provider.LastRequest);
                            if (id == "death-versus-takeover")
                            {
                                row["builderAffinityVerified"] = fixture.BuilderAffinityVerified;
                                row["assignedBuilderHumanProtected"] = fixture.AssignedBuilderHumanProtected;
                            }
                            row["approvalRequired"] = fixture.Chat.PendingActionPlan != null;
                            if (id == "ordered-production-exact-new-units")
                            {
                                row["fixtureAssumption"] = "The utterance omits patrol destination; this valid offline fixture supplies the bounded owned-base PlayerBase selector.";
                                row["previewMentionsOwnedBase"] = fixture.Chat.PendingActionPlan?.Preview?
                                    .IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0;
                            }
                            row["approved"] = awaitingApproval && fixture.Chat.PendingActionPlan == null
                                && fixture.Manager.Goals.Count > 0;
                            string observedCorrelation = RequestCorrelation(fixture, id);
                            if (observedCorrelation != null) correlationId = observedCorrelation;
                            else if (fixture.Provider.Calls > callsBefore
                                && fixture.Provider.LastRequest?.IsReadOnlyQuestion == true)
                                correlationId = null;
                            row["requestId"] = correlationId == null ? JValue.CreateNull() : (JToken)correlationId;
                            row["turnCorrelationId"] = "ui-turn:" + fixture.Chat.LastSubmittedUiTurnId;
                            row["uiState"] = LatestUiState(fixture.Chat);
                            row["packingMs"] = fixture.Provider.Calls > callsBefore && fixture.Provider.PackMs.HasValue
                                ? (JToken)fixture.Provider.PackMs.Value : JValue.CreateNull();
                            row["estimatedInputTokens"] = fixture.Provider.InputEstimate.HasValue
                                ? (JToken)fixture.Provider.InputEstimate.Value : JValue.CreateNull();
                            row["parseMs"] = fixture.Provider.Calls > callsBefore && fixture.Provider.ParseMs.HasValue
                                ? (JToken)fixture.Provider.ParseMs.Value : JValue.CreateNull();
                            row["admissionMs"] = fixture.Provider.Calls == callsBefore || fixture.Provider.LastReturnTicks < turnStart ? JValue.CreateNull()
                                : (JToken)Milliseconds(fixture.Provider.LastReturnTicks, Stopwatch.GetTimestamp());
                            row["commandCount"] = fixture.Commands.Count;
                            row["commandTypes"] = new JArray(fixture.Commands);
                            row["nativeResults"] = new JArray(fixture.Native);
                            row["taskStates"] = GoalStates(fixture);
                            row["status"] = "observed";
                            turnHistory.Add(new JObject { ["index"] = i, ["text"] = turn,
                                ["requestId"] = row["requestId"].DeepClone(),
                                ["turnCorrelationId"] = row["turnCorrelationId"].DeepClone(),
                                ["uiState"] = row["uiState"].DeepClone(),
                                ["providerCalls"] = fixture.Provider.Calls,
                                ["packAccepted"] = fixture.Provider.Calls > callsBefore
                                    ? (JToken)fixture.Provider.PackSucceeded : JValue.CreateNull(),
                                ["estimatedInputTokens"] = fixture.Provider.Calls > callsBefore && fixture.Provider.InputEstimate.HasValue
                                    ? (JToken)fixture.Provider.InputEstimate.Value : JValue.CreateNull(),
                                ["semanticValid"] = row["semanticValid"].DeepClone(),
                                ["semanticOutcome"] = row["semanticOutcome"].DeepClone(),
                                ["normalizedFields"] = row["normalizedFields"].DeepClone(),
                                ["admittedFields"] = row["admittedFields"].DeepClone(),
                                ["draftBefore"] = before.DeepClone(),
                                ["draftAfter"] = row["draftAfter"].DeepClone(),
                                ["draftChanges"] = row["draftChanges"].DeepClone(),
                                ["approvalRequired"] = row["approvalRequired"].DeepClone(),
                                ["approved"] = row["approved"].DeepClone(),
                                ["commandTypes"] = row["commandTypes"].DeepClone(),
                                ["taskStates"] = row["taskStates"].DeepClone(),
                                ["uiSubmitMs"] = row["uiSubmitMs"].DeepClone(),
                                ["packingMs"] = row["packingMs"].DeepClone(),
                                ["transportMs"] = JValue.CreateNull(),
                                ["parseMs"] = row["parseMs"].DeepClone(),
                                ["admissionMs"] = row["admissionMs"].DeepClone() });
                            evidence.RecordPhase5CTurn(row); // atomic checkpoint after every declared turn
                        }
                        if (fixture.Chat.PendingActionPlan != null && turns.Count == 1)
                        {
                            string correlation = correlationId;
                            long approvalStart = Stopwatch.GetTimestamp();
                            await fixture.Chat.SubmitMessageAsync("Approve plan");
                            row["requestId"] = correlation == null ? JValue.CreateNull() : (JToken)correlation;
                            row["turnCorrelationId"] = "ui-turn:" + fixture.Chat.LastSubmittedUiTurnId;
                            row["approvalMode"] = "offline-fixture-control-not-declared-player-turn";
                            row["approved"] = fixture.Chat.PendingActionPlan == null
                                && fixture.Manager.Goals.Count > 0;
                            row["admittedFields"] = AdmittedFields(fixture);
                            row["uiState"] = LatestUiState(fixture.Chat);
                            row["taskStates"] = GoalStates(fixture);
                            turnHistory.Add(new JObject { ["index"] = turns.Count, ["text"] = "Approve plan",
                                ["origin"] = "offline-fixture-control", ["requestId"] = correlation,
                                ["turnCorrelationId"] = row["turnCorrelationId"].DeepClone(),
                                ["uiState"] = row["uiState"].DeepClone(),
                                ["providerCalls"] = fixture.Provider.Calls, ["approved"] = row["approved"].DeepClone(),
                                ["admittedFields"] = row["admittedFields"].DeepClone(),
                                ["taskStates"] = row["taskStates"].DeepClone(),
                                ["uiSubmitMs"] = Milliseconds(approvalStart, Stopwatch.GetTimestamp()) });
                            evidence.RecordPhase5CTurn(row);
                        }
                        int tickBound = id == "malformed-provider-json" ? 0
                            : id == "causal-information-owned-task-facts" || id == "death-versus-takeover" ? 0
                            : id == "mine-yes-original-request" ? 1500
                            : id == "two-houses-near-town-center" ? 6000
                            : id == "house-next-five-from-trees" ? Math.Min(8000,
                                fixture.Simulation.Config.HouseConstructionTicks
                                + 5 * fixture.Simulation.Config.VillagerTrainTimeTicks + 4000)
                            : id == "additional-gathered-food-bounded-objective" ? 7000
                            : id == "all-wood-west-tc-explicit-move" ? 1500
                            : id == "defense-arrival-native-receipt" ? 1500
                            : id == "garrison-capacity-protected-remainder" ? 500
                            : id == "finite-discovery-distinct-from-point-scout" ? 1500
                            : id == "ordered-production-exact-new-units" ? Math.Min(8000,
                                4 * fixture.Simulation.Config.SpearmanTrainTimeTicks + 4000) : 60;
                        int ticks = fixture.TickBounded(tickBound);
                        row["tickMode"] = ticks == 0 ? "not-run" : "accelerated-fixture";
                        row["ticks"] = ticks;
                        row["commandCount"] = fixture.Commands.Count;
                        row["commandTypes"] = new JArray(fixture.Commands);
                        row["nativeResults"] = new JArray(fixture.Native);
                        row["taskStates"] = GoalStates(fixture);
                        row["fogNewlyExploredCount"] = fixture.Discovery == null ? JValue.CreateNull()
                            : (JToken)fixture.Discovery.TravelRevealedCount();
                        var garrison = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>()
                            .FirstOrDefault(goal => goal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits);
                        row["garrisonHousedCount"] = garrison?.GarrisonShelter == null ? JValue.CreateNull()
                            : (JToken)garrison.GarrisonShelter.GarrisonCount;
                        row["garrisonProtectedCount"] = garrison == null ? JValue.CreateNull()
                            : (JToken)garrison.GarrisonProtectedCount;
                        row["garrisonUnaccommodatedCount"] = garrison == null ? JValue.CreateNull()
                            : (JToken)garrison.GarrisonUnaccommodatedCount;
                        var future = fixture.Manager.Goals.OfType<WatchFutureUnitsGoal>().FirstOrDefault();
                        row["futureObservedCount"] = future == null ? JValue.CreateNull()
                            : (JToken)future.ObservedCount;
                        row["futureAssignedCount"] = future == null ? JValue.CreateNull()
                            : (JToken)future.AssignedCount;
                        row["humanTrainCommands"] = id == "house-next-five-from-trees"
                            || id == "ordered-production-exact-new-units"
                            ? (JToken)fixture.HumanTrainCommands : JValue.CreateNull();
                        if (id == "ordered-production-exact-new-units")
                        {
                            var resultIds = fixture.Manager.Goals.OfType<EnsureUnitCountGoal>()
                                .SingleOrDefault()?.ResultUnitIds ?? Array.Empty<int>();
                            row["newResultCount"] = resultIds.Count;
                            row["oldUnitExcluded"] = fixture.OldSpearmanId >= 0
                                && !resultIds.Contains(fixture.OldSpearmanId);
                            row["humanBirthExcluded"] = fixture.HumanBirthId >= 0
                                && !resultIds.Contains(fixture.HumanBirthId);
                            row["exactPatrolBinding"] = fixture.PatrolSubjects != null
                                && resultIds.Count == 2
                                && fixture.PatrolSubjects.OrderBy(value => value)
                                    .SequenceEqual(resultIds.OrderBy(value => value));
                            CaptureNativePatrolFacts(fixture, row, resultIds);
                        }
                        var resourceGoal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().FirstOrDefault();
                        row["resourceProgress"] = resourceGoal == null ? JValue.CreateNull()
                            : (JToken)resourceGoal.ResourceProgress;
                        row["gatheredFoodIncome"] = id == "additional-gathered-food-bounded-objective"
                            ? (JToken)fixture.Simulation.ResourceManager.GetGatheredIncome(0, ResourceType.Food)
                            : JValue.CreateNull();
                        row["foodCompletionTick"] = fixture.FoodCompletionTick.HasValue
                            ? (JToken)fixture.FoodCompletionTick.Value : JValue.CreateNull();
                        row["foodIncomeAtCompletion"] = fixture.FoodIncomeAtCompletion.HasValue
                            ? (JToken)fixture.FoodIncomeAtCompletion.Value : JValue.CreateNull();
                        row["commanderCommandsAtFoodCompletion"] = fixture.CommanderCommandsAtFoodCompletion.HasValue
                            ? (JToken)fixture.CommanderCommandsAtFoodCompletion.Value : JValue.CreateNull();
                        row["ordinaryFoodGatherActive"] = id == "additional-gathered-food-bounded-objective"
                            ? (JToken)fixture.OrdinaryFoodGatherActive : JValue.CreateNull();
                        row["firstNativeActionMs"] = fixture.FirstNativeTicks == 0 ? JValue.CreateNull()
                            : (JToken)Milliseconds(started, fixture.FirstNativeTicks);
                        EvaluateFixture(id, fixture, row);
                        evidence.RecordPhase5CTurn(row);
                    }
                }
                catch (Exception error)
                {
                    row["status"] = "failed";
                    row["errorStage"] = "fixture-runner";
                    row["errorCategory"] = error.GetType().Name;
                    row["gap"] = "First integrated fixture failure; inspect focused NUnit output and the preserved case artifact.";
                    try { evidence.RecordPhase5CTurn(row); }
                    catch (InvalidOperationException)
                    {
                        var boundedFailure = NewTurn(id, original);
                        boundedFailure["status"] = "failed";
                        boundedFailure["errorStage"] = "fixture-runner";
                        boundedFailure["errorCategory"] = error.GetType().Name;
                        boundedFailure["gap"] = "The richer record exceeded the evidence allow-list or size bound; inspect the earlier atomic checkpoint.";
                        evidence.RecordPhase5CTurn(boundedFailure);
                    }
                }
            }
            return directory;
        }

        private static JObject NewTurn(string id, string original) => new JObject
        {
            ["schemaVersion"] = 1, ["caseId"] = id, ["requestId"] = JValue.CreateNull(),
            ["turnCorrelationId"] = JValue.CreateNull(),
            ["utterance"] = original, ["turnIndex"] = 0, ["status"] = "started",
            ["evidenceMode"] = "offline-production-chat-fixture", ["providerCalls"] = 0,
            ["httpAttempts"] = 0, ["packAccepted"] = JValue.CreateNull(),
            ["fidelity"] = "not-evaluated", ["semanticValid"] = JValue.CreateNull(),
            ["estimatedInputTokens"] = JValue.CreateNull(), ["actualPromptTokens"] = JValue.CreateNull(),
            ["actualCompletionTokens"] = JValue.CreateNull(), ["actualCachedTokens"] = JValue.CreateNull(),
            ["actualReasoningTokens"] = JValue.CreateNull(), ["reportedCost"] = JValue.CreateNull(),
            ["attemptCosts"] = new JArray(), ["packingMs"] = JValue.CreateNull(),
            ["transportMs"] = JValue.CreateNull(), ["parseMs"] = JValue.CreateNull(),
            ["admissionMs"] = JValue.CreateNull(), ["firstNativeActionMs"] = JValue.CreateNull(),
            ["commandCount"] = 0, ["commandTypes"] = new JArray(), ["nativeResults"] = new JArray(),
            ["taskStates"] = new JArray(), ["draftBefore"] = new JArray(), ["draftAfter"] = new JArray(),
            ["draftChanges"] = new JArray(), ["normalizedFields"] = new JArray(),
            ["admittedFields"] = new JArray(), ["turnHistory"] = new JArray(),
            ["fogNewlyExploredCount"] = JValue.CreateNull(),
            ["garrisonHousedCount"] = JValue.CreateNull(), ["garrisonProtectedCount"] = JValue.CreateNull(),
            ["garrisonUnaccommodatedCount"] = JValue.CreateNull(),
            ["futureObservedCount"] = JValue.CreateNull(), ["futureAssignedCount"] = JValue.CreateNull(),
            ["humanTrainCommands"] = JValue.CreateNull(),
            ["resourceProgress"] = JValue.CreateNull(), ["gatheredFoodIncome"] = JValue.CreateNull(),
            ["foodCompletionTick"] = JValue.CreateNull(), ["foodIncomeAtCompletion"] = JValue.CreateNull(),
            ["commanderCommandsAtFoodCompletion"] = JValue.CreateNull(),
            ["ordinaryFoodGatherActive"] = JValue.CreateNull(),
            ["ownedTaskFactCount"] = JValue.CreateNull(), ["readOnlyProviderRequest"] = JValue.CreateNull(),
            ["questionFactClass"] = JValue.CreateNull()
        };

        private static bool TryResponse(string id, out string response)
        {
            response = null;
            if (id == "mine-yes-original-request")
            {
                var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 },
                    ["placement"] = new JObject { ["anchor"] = "VisibleResource", ["relation"] = "Near",
                        ["resource"] = "Gold", ["sourceKind"] = "GoldMine" } };
                response = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
                return true;
            }
            if (id == "house-next-five-from-trees")
            {
                var house = new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
                var watch = new JObject { ["type"] = "WatchFutureUnits", ["unit"] = "Villager", ["count"] = 5,
                    ["producer"] = "TownCenter", ["producerOrdinal"] = 1, ["action"] = "Gather",
                    ["resource"] = "Wood", ["dependsOn"] = new JArray(0) };
                response = GraphDraft(new JArray(house, watch), "Sequential", 1, "sourceKind", "SourceKind", new JValue("Tree"));
                return true;
            }
            if (id == "malformed-provider-json") { response = "{not-json"; return true; }
            if (id == "schema-valid-wrong-provider-result")
            {
                response = Request(AllocationWithAmount("2", "Wood", "Tree", 200, "Stockpile"));
                return true;
            }
            if (id.StartsWith("worker-mode-", StringComparison.Ordinal))
            {
                string mode = id == "worker-mode-additional-four" ? "Additional"
                    : id == "worker-mode-target-total-four" ? "TargetTotal" : "SelectedCount";
                response = Request(AllocationNode(new[] { mode, "Exact", "4", "Any", "", "Food", "Any" }));
                return true;
            }
            if (id == "amount-versus-worker-count")
                response = Request(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile"));
            else if (id == "count-correction-only" || id == "amount-correction-only")
            {
                var allocation = JObject.Parse(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile"));
                allocation["mode"] = "TargetTotal";
                ((JObject)allocation["destination"]).Remove("sourceKind");
                response = GraphDraft(new JArray(allocation), "Independent", 0, "destination.sourceKind",
                    "SourceKind", new JValue("GoldMine"));
            }
            else if (id == "all-current-wood-to-berries" || id == "exact-current-wood-to-berries")
                response = Request(AllocationNode(new[] { "SelectedCount",
                    id == "all-current-wood-to-berries" ? "AllMatching" : "Exact",
                    id == "all-current-wood-to-berries" ? "" : "6",
                    "Gathering", "Wood", "Food", "Berries" }));
            else if (id == "additional-gathered-food-bounded-objective")
                response = Request(AllocationWithAmount("4", "Food", "Berries", 400, "AdditionalGathered"));
            else if (id == "idle-only-protection")
                response = Request("{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1,"
                    + "\"builders\":{\"state\":\"IdleOnly\",\"count\":1},"
                    + "\"constraints\":[{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}]}");
            else if (id == "all-wood-west-tc-explicit-move" || id == "all-wood-west-tc-explicit-gather")
                response = WestWoodline(id == "all-wood-west-tc-explicit-move");
            else if (id == "two-houses-near-town-center")
                response = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                    + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                    + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            else if (id == "ordered-production-exact-new-units")
                response = "{\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2,\"quantityMode\":\"New\"},"
                    + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}";
            else if (id == "occupied-farms-and-capacity")
                response = Request(AllocationNode(new[] { "SelectedCount", "Exact", "4", "Any", "", "Food", "Farm" }));
            else if (id == "defense-arrival-native-receipt")
                response = Request("{\"type\":\"DefendArea\",\"unitSelector\":\"Spearman\",\"count\":6,\"location\":\"PlayerBase\"}");
            else if (id == "garrison-capacity-protected-remainder")
                response = Request("{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"countMode\":\"AllMatching\",\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}");
            else if (id == "finite-discovery-distinct-from-point-scout")
                response = Request("{\"type\":\"DiscoverEnemy\",\"unitSelector\":\"Scout\",\"count\":1,\"location\":\"VisibleEnemy\",\"target\":{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}}");
            else if (id == "cancel-reset-late-retry")
                response = GraphDraft(new JArray(new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } }),
                    "Independent", 0, "structure", "Confirmation", new JValue("House"));
            else if (id == "death-versus-takeover" || id == "causal-information-owned-task-facts")
                response = "{\"outcome\":\"Answer\",\"message\":\"Owned task status is unavailable in this fixture.\"}";
            return response != null;
        }

        private static string AllocationWithAmount(string count, string resource, string source, int amount, string mode)
        {
            string node = AllocationNode(new[] { "SelectedCount", "Exact", count, "Any", "", resource, source });
            return node.Substring(0, node.Length - 1) + ",\"resourceAmount\":" + amount
                + ",\"resourceAmountMode\":\"" + mode + "\"}";
        }

        private static string WestWoodline(bool move) => "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"all\",\"mechanic\":\"select-workers\",\"parameters\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\",\"countMode\":\"AllMatching\"},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"woodline\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":1,\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},\"inputs\":{},\"dependsOn\":[]},"
            + (move
                ? "{\"id\":\"effect\",\"mechanic\":\"move-workers\",\"parameters\":{},"
                : "{\"id\":\"effect\",\"mechanic\":\"allocate-workers\",\"parameters\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},")
            + "\"inputs\":{\"workers\":\"all\",\"location\":\"woodline\"},\"dependsOn\":[\"all\",\"woodline\"]}]}";

        private static JArray Missing(CommanderPendingClarification pending)
        {
            if (pending?.GraphDraft != null)
                return new JArray(pending.GraphDraft.MissingSlots.Select(slot => slot.Node + ":" + slot.Path + ":" + slot.Type));
            if (pending?.Draft != null)
                return new JArray(pending.Draft.MissingFields.Select(field => field.ToString()));
            return new JArray();
        }
        private static JArray DraftDelta(JArray before, JArray after, JArray beforeFields, JArray afterFields)
        {
            var changes = new JArray(before.Values<string>().Except(after.Values<string>(), StringComparer.Ordinal));
            for (int i = 1; i < Math.Min(beforeFields.Count, afterFields.Count); i++)
            {
                if (!(beforeFields[i] is JObject original) || !(afterFields[i] is JObject current)) continue;
                foreach (var field in original.Properties())
                    if (field.Name != "dependencies" && !JToken.DeepEquals(field.Value, current[field.Name]))
                        changes.Add("node" + (i - 1) + "." + field.Name + ":"
                            + field.Value.ToString(Formatting.None) + "->" + current[field.Name]?.ToString(Formatting.None));
            }
            return changes;
        }
        private static JArray NormalizeCorpus(CommanderSemanticResult result)
        {
            var fields = new JArray();
            if (result == null || !result.IsValid) return fields;
            foreach (var node in result.Nodes.Take(4))
                fields.Add(new JObject { ["type"] = node.Type.ToString(), ["building"] = node.BuildingType?.ToString(),
                    ["unit"] = node.UnitType, ["resource"] = node.ResourceType?.ToString(), ["count"] = node.Count,
                    ["quantityMode"] = node.QuantityMode.ToString(), ["anchor"] = node.PlacementAnchorSelector?.ToString(),
                    ["anchorOrdinal"] = node.PlacementAnchorOrdinal, ["relation"] = node.PlacementRelation?.ToString(),
                    ["placementSource"] = node.SourceKind?.ToString(), ["unitSelector"] = node.UnitSelector?.ToString(),
                    ["locationSelector"] = node.LocationSelector?.ToString(),
                    ["producerOrdinal"] = node.ProducerOrdinal, ["producerFrom"] = node.ProducerFromNode,
                    ["resultFrom"] = node.ResultFromNode, ["futureAction"] = node.FutureAction?.ToString(),
                    ["workerMode"] = node.WorkerAllocation?.Mode.ToString(),
                    ["workerCountMode"] = node.WorkerAllocation?.CountMode.ToString(),
                    ["workerCount"] = node.WorkerAllocation?.Count,
                    ["workerState"] = node.WorkerAllocation?.Workers.State.ToString(),
                    ["workerCurrentResource"] = node.WorkerAllocation?.Workers.CurrentResource?.ToString(),
                    ["destinationResource"] = node.WorkerAllocation?.Destination.Resource.ToString(),
                    ["destinationSource"] = node.WorkerAllocation?.Destination.SourceKind.ToString(),
                    ["resourceAmount"] = node.WorkerAllocation?.ResourceAmount,
                    ["resourceAmountMode"] = node.WorkerAllocation?.ResourceAmountMode?.ToString(),
                    ["dependencies"] = new JArray(node.DependsOn) });
            if (result.PendingGraphDraft != null)
            {
                foreach (var field in ProjectGraphDraft(result.PendingGraphDraft)) fields.Add(field);
            }
            if (result.DynamicPlan != null)
            {
                var program = result.DynamicPlan.Nodes.ToList();
                foreach (var step in program.Take(12))
                {
                    var parameters = new JObject();
                    foreach (var field in step.Primitive.Parameters)
                        if (step.Parameters.TryGetValue(field.Name, out object value))
                            parameters[field.Name == "clearGapTiles" ? "clearGap" : field.Name] = value is int count ? (JToken)count
                                : value is string name && name.Length <= 64 ? (JToken)name : "unavailable";
                    var inputs = new JObject();
                    foreach (var input in step.Inputs)
                        inputs[input.Key] = program.FindIndex(node => node.Id == input.Value);
                    fields.Add(new JObject { ["mechanic"] = step.Primitive.Mechanic.ToString(),
                        ["parameters"] = parameters, ["inputs"] = inputs,
                        ["dependencies"] = new JArray(step.DependsOn.Select(dep => program.FindIndex(node => node.Id == dep))) });
                }
            }
            return fields;
        }
        private static JArray ProjectGraphDraft(CommanderRequestGraphDraft draft)
        {
            var fields = new JArray { new JObject { ["draftNodeCount"] = draft.NodeCount,
                ["missingSlots"] = MissingSlots(draft) } };
            var pendingNodes = (JArray)draft.ToPendingJson()["nodes"];
            foreach (JObject node in pendingNodes.Take(4))
                fields.Add(new JObject { ["draftType"] = (string)node["type"],
                        ["structure"] = (string)node["structure"], ["count"] = (int?)node["count"],
                        ["resource"] = (string)node["resource"],
                        ["resourceAmount"] = (int?)node["resourceAmount"],
                        ["resourceAmountMode"] = (string)node["resourceAmountMode"],
                        ["workerMode"] = (string)node["mode"], ["countMode"] = (string)node["countMode"],
                        ["workerState"] = (string)node["workers"]?["state"],
                        ["workerCurrentResource"] = (string)node["workers"]?["currentResource"],
                        ["destinationResource"] = (string)node["destination"]?["resource"],
                        ["destinationSource"] = (string)node["destination"]?["sourceKind"],
                        ["producerOrdinal"] = (int?)node["producerOrdinal"],
                        ["dependencies"] = node["dependsOn"] is JArray dependencies
                            ? new JArray(dependencies.Values<int>()) : new JArray() });
            return fields;
        }
        private static JArray AdmittedFields(CorpusFixture fixture)
        {
            var admitted = new JArray();
            foreach (var goal in fixture.Manager.Goals.Take(12))
            {
                var projection = new JObject { ["type"] = goal.GetType().Name, ["status"] = goal.Status.ToString(),
                    ["originalUtterance"] = goal.RequestAuthority?.OriginalInput };
                if (goal is BuildStructureGoal build)
                {
                    projection["building"] = build.StructureType.ToString(); projection["count"] = build.Count;
                    projection["anchor"] = build.PlacementAnchorSelector?.ToString();
                    projection["anchorOrdinal"] = build.PlacementAnchorOrdinal;
                    projection["relation"] = build.PlacementRelation?.ToString();
                    projection["resource"] = build.PlacementResourceType?.ToString();
                    projection["source"] = build.PlacementSourceKind?.ToString();
                }
                if (goal is AllocateWorkersGoal workers)
                {
                    var allocation = workers.Allocation;
                    projection["mode"] = allocation.Mode.ToString(); projection["countMode"] = allocation.CountMode.ToString();
                    projection["count"] = allocation.Count; projection["state"] = allocation.Workers.State.ToString();
                    projection["currentResource"] = allocation.Workers.CurrentResource?.ToString();
                    projection["destinationResource"] = allocation.Destination.Resource.ToString();
                    projection["destinationSource"] = allocation.Destination.SourceKind.ToString();
                    projection["resourceAmount"] = allocation.ResourceAmount;
                    projection["resourceAmountMode"] = allocation.ResourceAmountMode?.ToString();
                }
                admitted.Add(projection);
            }
            return admitted;
        }
        private static JArray MissingSlots(CommanderRequestGraphDraft draft)
            => new JArray(draft.MissingSlots.Select(slot => slot.Node + ":" + slot.Path + ":" + slot.Type));
        private static JArray GoalStates(CorpusFixture fixture)
            => new JArray(fixture.Manager.Goals.Take(16).Select(goal =>
                goal.GetType().Name + ":" + goal.Status));
        private static string LatestUiState(CommanderChatUI chat) =>
            (string)typeof(CommanderChatUI).GetField("latestRequestState",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chat);
        private static string RequestCorrelation(CorpusFixture fixture, string id)
        {
            long? request = fixture.Chat.LastSubmittedSemanticRequestId;
            return request > 0 ? "runtime:" + request.Value : null;
        }
        private static int? OwnedTaskFactCount(CommanderSemanticProviderRequest request)
        {
            if (request == null) return null;
            try { return (JObject.Parse(request.SerializedContext)["tacticalStatus"]?["requests"] as JArray)?.Count; }
            catch (Newtonsoft.Json.JsonException) { return null; }
        }
        private static string QuestionFactClass(CommanderSemanticProviderRequest request)
        {
            if (request == null || !request.IsReadOnlyQuestion) return null;
            string facts = request.QuestionFacts ?? string.Empty;
            if (facts.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0) return "wood-shortage";
            if (facts.IndexOf("dead or missing", StringComparison.OrdinalIgnoreCase) >= 0) return "dead-or-missing";
            if (facts.IndexOf("human", StringComparison.OrdinalIgnoreCase) >= 0) return "human-override";
            if (facts.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0) return "unavailable";
            return string.IsNullOrEmpty(facts) ? "empty" : "other-owned-status";
        }
        private static void CaptureNativePatrolFacts(CorpusFixture fixture, JObject row, IReadOnlyList<int> resultIds)
        {
            int active = 0;
            bool waypointsMatch = resultIds.Count == 2 && fixture.PatrolTarget.HasValue;
            bool destinationMatch = waypointsMatch;
            foreach (int id in resultIds)
            {
                UnitData actor = fixture.Simulation.UnitRegistry.GetUnit(id);
                bool current = actor != null && actor.PlayerId == 0 && actor.CurrentHealth > 0
                    && actor.State != UnitState.Dead && actor.IsPatrolling;
                if (current) active++;
                bool route = current && actor.PatrolWaypoints != null && actor.PatrolWaypoints.Count == 2
                    && actor.PatrolWaypoints[1] == fixture.PatrolTarget.Value;
                waypointsMatch &= route;
                destinationMatch &= route && (actor.FinalDestination == actor.PatrolWaypoints[0]
                    || actor.FinalDestination == actor.PatrolWaypoints[1]);
            }
            row["nativePatrolActorCount"] = active;
            row["nativePatrolWaypointsMatch"] = waypointsMatch;
            row["nativePatrolDestinationMatch"] = destinationMatch;
        }

        private static void EvaluateFixture(string id, CorpusFixture fixture, JObject row)
        {
            if (fixture.Provider.Calls > 0 && !fixture.Provider.PackSucceeded)
            {
                row["status"] = "open-gap"; row["fidelity"] = "not-evaluable";
                row["errorStage"] = "packing"; row["errorCategory"] = "offline-request-pack-rejected";
                return;
            }
            bool oneCall = fixture.Provider.Calls == 1;
            bool noEffect = fixture.Manager.Goals.Count == 0 && fixture.Commands.Count == 0;
            string states = string.Join("|", fixture.Manager.Goals.Select(goal => goal.GetType().Name + ":" + goal.Status));
            bool hasNative(string name) => fixture.Native.Any(entry => entry.StartsWith(name, StringComparison.Ordinal));
            bool hasCommand(string name) => fixture.Commands.Contains(name);
            void Mark(bool success, string stage, string category, bool terminal = true)
            {
                row["fidelity"] = success ? "matched" : "mismatch";
                if (success) return;
                row["status"] = terminal ? "observed" : "open-gap";
                row["errorStage"] = stage;
                row["errorCategory"] = category;
            }
            if (id == "malformed-provider-json")
            {
                bool rejected = oneCall && fixture.Provider.LastResult?.IsValid == false
                    && noEffect && fixture.Chat.PendingClarification == null
                    && fixture.Chat.PendingActionPlan == null;
                row["semanticValid"] = fixture.Provider.LastResult?.IsValid;
                row["fidelity"] = rejected ? "not-evaluable" : "mismatch";
                row["status"] = rejected ? "observed" : "open-gap";
                row["errorStage"] = "parse";
                row["errorCategory"] = rejected ? "malformed-rejected" : "malformed-rejection-regressed";
                if (!rejected) row["gap"] = "The malformed response did not produce exactly one invalid parse with no clarification, approval, goal or Commander command.";
                return;
            }
            if (id == "schema-valid-wrong-provider-result")
            {
                var admitted = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                var allocation = admitted?.Allocation;
                bool wrongButAdmitted = oneCall && fixture.Provider.LastResult?.IsValid == true
                    && fixture.Provider.LastResult.Outcome == CommanderSemanticOutcome.Request
                    && fixture.Manager.Goals.Count == 1 && admitted != null
                    && admitted.RequestAuthority?.OriginalInput == (string)row["utterance"]
                    && allocation.Mode.ToString() == "SelectedCount"
                    && allocation.CountMode.ToString() == "Exact" && allocation.Count == 2
                    && allocation.Destination.Resource == ResourceType.Wood
                    && allocation.Destination.SourceKind == ResourceSourceKind.Tree
                    && allocation.ResourceAmount == 200
                    && allocation.ResourceAmountMode.ToString() == "Stockpile";
                row["semanticValid"] = fixture.Provider.LastResult?.IsValid;
                row["fidelity"] = wrongButAdmitted ? "wrong" : "mismatch";
                row["status"] = wrongButAdmitted ? "observed" : "open-gap";
                row["errorStage"] = wrongButAdmitted ? "semantic-fidelity" : "fixture-verdict";
                row["errorCategory"] = wrongButAdmitted ? "schema-valid-wrong-action" : "wrong-response-diagnostic-regressed";
                row["gap"] = wrongButAdmitted
                    ? "Synthetic Wood/count-2 result differs from declared Gold/count-3/amount-200 request; actual admission is recorded separately."
                    : "The deliberately wrong fixture was not both schema-valid and admitted as the exact Wood/count-2 action; do not certify a semantic mismatch.";
                return;
            }
            if (id == "cancel-reset-late-retry")
            {
                Mark(oneCall && noEffect && fixture.Chat.PendingClarification == null
                    && fixture.Chat.PendingActionPlan == null && LatestUiState(fixture.Chat) == "Ready",
                    "local-continuation", "stale-proposal-effect");
                return;
            }
            if (id == "count-correction-only" || id == "amount-correction-only")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                var admitted = goal?.Allocation;
                Mark(oneCall && fixture.Chat.PendingClarification == null && goal != null
                    && admitted.Mode.ToString() == "TargetTotal"
                    && admitted.CountMode.ToString() == "Exact"
                    && admitted.Count == (id == "count-correction-only" ? 4 : 3)
                    && admitted.ResourceAmount == (id == "amount-correction-only" ? 250 : 200)
                    && admitted.ResourceAmountMode.ToString() == "Stockpile"
                    && admitted.Destination.Resource == ResourceType.Gold
                    && admitted.Destination.SourceKind == ResourceSourceKind.GoldMine
                    && goal.RequestAuthority?.OriginalInput == (string)row["utterance"],
                    "local-continuation", "typed-correction-or-admission-not-retained");
                return;
            }
            if (id == "death-versus-takeover" || id == "causal-information-owned-task-facts")
            {
                bool grounded = (string)row["questionFactClass"] ==
                    (id == "causal-information-owned-task-facts" ? "wood-shortage" : "human-override");
                Mark(oneCall && fixture.Manager.Goals.Count == fixture.InitialGoalCount
                    && fixture.Commands.Count == fixture.InitialCommanderCommandCount
                    && fixture.Provider.LastResult?.Outcome == CommanderSemanticOutcome.Answer
                    && fixture.Provider.LastRequest?.IsReadOnlyQuestion == true
                    && (int?)row["ownedTaskFactCount"] > 0 && grounded
                    && (id != "death-versus-takeover" || fixture.BuilderAffinityVerified
                        && fixture.AssignedBuilderHumanProtected),
                    "read-only-answer", "owned-task-cause-not-observed", terminal: false);
                if (!grounded) row["gap"] = "Owned task facts were seeded, but this fixture did not prove the requested native cause.";
                return;
            }
            if (id == "mine-yes-original-request")
            {
                var goal = fixture.Manager.Goals.OfType<BuildStructureGoal>().SingleOrDefault();
                Mark(oneCall && goal?.StructureType == BuildingType.Mine
                    && goal.RequestAuthority?.OriginalInput == (string)row["utterance"]
                    && fixture.Chat.PendingClarification == null, "admission", "original-mine-ticket-lost");
                if ((string)row["fidelity"] == "matched" && goal.Status != CommanderGoalStatus.Completed)
                { row["status"] = "open-gap"; row["errorStage"] = "native-placement";
                  row["errorCategory"] = "mine-placement-not-complete"; row["gap"] = states; }
                return;
            }
            if (id == "house-next-five-from-trees")
            {
                var history = (JArray)row["turnHistory"];
                var fields = history.Count > 1
                    ? (JArray)history[1]["normalizedFields"] : (JArray)row["normalizedFields"];
                bool graph = fields.Count >= 2 && (string)fields[0]["type"] == "BuildStructure"
                    && (string)fields[0]["building"] == "House"
                    && (string)fields[1]["type"] == "WatchFutureUnits"
                    && (int?)fields[1]["count"] == 5 && (int?)fields[1]["producerOrdinal"] == 1
                    && (string)fields[1]["resource"] == "Wood"
                    && (string)fields[1]["placementSource"] == "Tree"
                    && ((JArray)fields[1]["dependencies"]).Values<int>().SequenceEqual(new[] { 0 });
                Mark(oneCall && graph && (bool?)row["approved"] == true
                    && fixture.Manager.Goals.Count == 2 && hasNative("PlaceBuildingCommand:processed")
                    && (int?)row["humanTrainCommands"] == 5
                    && (int?)row["futureObservedCount"] == 5 && (int?)row["futureAssignedCount"] == 5
                    && fixture.Native.Count(name => name == "GatherCommand:processed") >= 5,
                    "native-future-births", "five-attributed-births-not-complete");
                if ((string)row["fidelity"] == "matched" && !states.Contains("WatchFutureUnitsGoal:Completed"))
                { row["status"] = "open-gap"; row["errorStage"] = "native-future-births";
                  row["errorCategory"] = "five-attributed-births-not-complete"; row["gap"] = states; }
                return;
            }
            if (id == "finite-discovery-distinct-from-point-scout")
            {
                Mark(oneCall && fixture.Discovery != null && fixture.Discovery.TargetInitiallyHidden
                    && fixture.Discovery.NoOrderControlStable && hasCommand("MoveCommand")
                    && hasNative("Move:Complete") && fixture.Discovery.TravelRevealedCount() > 0
                    && fixture.Discovery.TargetEverVisibleAfterTravel,
                    "native-discovery", "travel-fog-not-revealed");
                if ((string)row["fidelity"] == "matched" && !states.Contains("CommanderCapabilityGoal:Completed")
                    && !states.Contains("CommanderCapabilityGoal:Blocked") && !states.Contains("CommanderCapabilityGoal:Failed"))
                { row["status"] = "open-gap"; row["errorStage"] = "native-discovery";
                  row["errorCategory"] = "finite-outcome-not-reached"; row["gap"] = states; }
                return;
            }
            if (id == "ordered-production-exact-new-units")
            {
                var producer = fixture.Manager.Goals.OfType<EnsureUnitCountGoal>().SingleOrDefault();
                var patrol = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>()
                    .SingleOrDefault(goal => goal.Action.ActionType == CommanderCapabilityActionType.PatrolArea);
                var resultIds = producer?.ResultUnitIds ?? Array.Empty<int>();
                CaptureNativePatrolFacts(fixture, row, resultIds);
                Mark(oneCall && fixture.Provider.LastResult?.IsValid == true
                    && (bool?)row["approved"] == true && (bool?)row["previewMentionsOwnedBase"] == true
                    && fixture.HumanTrainCommands == 1 && fixture.HumanBirthId >= 0
                    && producer?.Status == CommanderGoalStatus.Completed && patrol != null
                    && (int?)row["newResultCount"] == 2
                    && (bool?)row["oldUnitExcluded"] == true
                    && (bool?)row["humanBirthExcluded"] == true
                    && (bool?)row["exactPatrolBinding"] == true
                    && (int?)row["nativePatrolActorCount"] == 2
                    && (bool?)row["nativePatrolWaypointsMatch"] == true
                    && (bool?)row["nativePatrolDestinationMatch"] == true
                    && hasCommand("TrainUnitCommand") && hasCommand("PatrolCommand")
                    && hasNative("PatrolCommand:processed"),
                    "native-result-binding", "exact-new-patrol-chain-not-observed", terminal: false);
                if ((string)row["fidelity"] != "matched")
                    row["gap"] = "Check ordinary human queue, exactly two Commander-attributed newborn results, owned-base preview, exact native patrol subjects, both persistent routes and current destinations independently.";
                return;
            }
            if (id == "idle-only-protection")
            {
                Mark(oneCall && fixture.Provider.LastResult?.IsValid == true
                    && fixture.Manager.Goals.OfType<BuildStructureGoal>().Any(goal => goal.UseIdleWorkersOnly)
                    && hasNative("PlaceBuildingCommand:processed"),
                    "native-construction", "idle-only-selector-or-placement-missing");
                return;
            }
            if (id == "garrison-capacity-protected-remainder")
            {
                var goal = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Status == CommanderGoalStatus.Completed
                    && hasNative("GarrisonCommand:processed") && goal.GarrisonShelter?.GarrisonCount == 1
                    && goal.GarrisonProtectedCount == 1 && goal.GarrisonUnaccommodatedCount == 1,
                    "native-garrison", "capacity-or-protected-remainder-missing");
                return;
            }
            if (id == "occupied-farms-and-capacity")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Status == CommanderGoalStatus.Completed
                    && goal.Allocation.Count == 4 && goal.Allocation.Destination.SourceKind == ResourceSourceKind.Farm
                    && fixture.Commands.Count(name => name == "GatherCommand") == 2
                    && fixture.Native.Count(name => name == "GatherCommand:processed") == 2,
                    "native-farm-allocation", "incumbent-or-capacity-count-mismatch");
                return;
            }
            if (id == "amount-versus-worker-count")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Allocation.Count == 3
                    && goal.Allocation.ResourceAmount == 200
                    && goal.Allocation.ResourceAmountMode?.ToString() == "Stockpile"
                    && goal.Allocation.Destination.Resource == ResourceType.Gold
                    && goal.Status == CommanderGoalStatus.WaitingForResources
                    && hasNative("GatherCommand:processed"),
                    "native-resource-objective", "amount-worker-slots-collapsed-or-premature-completion");
                return;
            }
            if (id.StartsWith("worker-mode-", StringComparison.Ordinal))
            {
                string expected = id == "worker-mode-additional-four" ? "Additional"
                    : id == "worker-mode-target-total-four" ? "TargetTotal" : "SelectedCount";
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Allocation.Mode.ToString() == expected
                    && goal.Allocation.CountMode.ToString() == "Exact" && goal.Allocation.Count == 4
                    && goal.Status == CommanderGoalStatus.Completed && hasNative("GatherCommand:processed"),
                    "native-worker-allocation", "quantity-mode-or-four-worker-effect-missing");
                return;
            }
            if (id == "all-current-wood-to-berries" || id == "exact-current-wood-to-berries")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Allocation.Mode.ToString() == "SelectedCount"
                    && goal.Allocation.CountMode.ToString() == (id == "all-current-wood-to-berries" ? "AllMatching" : "Exact")
                    && goal.Allocation.Workers.State.ToString() == "Gathering"
                    && goal.Allocation.Workers.CurrentResource == ResourceType.Wood
                    && goal.Allocation.Destination.Resource == ResourceType.Food
                    && goal.Allocation.Destination.SourceKind == ResourceSourceKind.Berries
                    && goal.Status == CommanderGoalStatus.Completed && hasNative("GatherCommand:processed"),
                    "native-worker-allocation", "all-exact-source-or-destination-mismatch");
                return;
            }
            if (id == "two-houses-near-town-center")
            {
                Mark(oneCall && (bool?)row["approved"] == true && fixture.Commands.Count(name => name == "PlaceBuildingCommand") == 2
                    && fixture.Native.Count(name => name == "PlaceBuildingCommand:processed") == 2
                    && states.Contains("BuildStructureGoal:Completed"), "native-construction", "two-distinct-build-results-missing");
                return;
            }
            if (id == "defense-arrival-native-receipt")
            {
                Mark(oneCall && hasCommand("MoveCommand") && fixture.Native.Count(name => name == "Move:Complete") == 6
                    && states.Contains("CommanderCapabilityGoal:Completed"), "native-arrival", "six-exact-arrivals-missing");
                return;
            }
            if (id == "all-wood-west-tc-explicit-move")
            {
                Mark(oneCall && hasCommand("MoveCommand") && !hasCommand("GatherCommand")
                    && hasNative("Move:Complete") && states.Contains("CommanderCapabilityGoal:Completed"),
                    "native-movement", "explicit-move-rewritten-or-incomplete");
                return;
            }
            if (id == "all-wood-west-tc-explicit-gather")
            {
                Mark(oneCall && hasCommand("GatherCommand") && !hasCommand("MoveCommand")
                    && hasNative("GatherCommand:processed") && states.Contains("AllocateWorkersGoal:Completed"),
                    "native-gather", "explicit-gather-rewritten-or-incomplete");
                return;
            }
            if (id == "additional-gathered-food-bounded-objective")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && hasCommand("GatherCommand") && hasNative("GatherCommand:processed")
                    && goal?.Status == CommanderGoalStatus.Completed
                    && goal.ResourceProgress == 400
                    && (long?)row["foodIncomeAtCompletion"] == 400
                    && (long?)row["gatheredFoodIncome"] > 400
                    && (int?)row["commanderCommandsAtFoodCompletion"] == fixture.Commands.Count
                    && (bool?)row["ordinaryFoodGatherActive"] == true,
                    "native-resource-objective", "completion-or-post-target-gather-not-observed");
                return;
            }
            Mark(oneCall && fixture.Provider.LastResult?.IsValid == true && fixture.Manager.Goals.Count > 0,
                "admission", "expected-goal-not-admitted");
        }
        private static double Milliseconds(long start, long end)
            => Math.Round((end - start) * 1000.0 / Stopwatch.Frequency, 3);
    }
}
```

### `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs.meta`

```meta
fileFormatVersion: 2
guid: 3c5fa05a3261a67458cb958960628f8f
```

### `Assets/Tests/EditMode/CommanderPhase5CProviderBudgetTests.cs`

```cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5C")]
    public sealed class CommanderPhase5CProviderBudgetTests
    {
        [TestCase("Garrison all villagers in my Town Center.", "GarrisonUnits")]
        [TestCase("Ungarrison my Town Center.", "UngarrisonBuilding")]
        [TestCase("Find the enemy Town Center with my Scout.", "DiscoverEnemy")]
        public void NewNativeAdapters_ReceiveAccurateCompactContract(string utterance, string action)
        {
            var request = new CommanderSemanticProviderRequest(utterance,
                CommanderEconomyClarificationTests.Context());
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            string instruction = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            Assert.That(instruction, Does.Contain(action));
            if (action == "GarrisonUnits") Assert.That(instruction, Does.Contain("AllMatching"));
            if (action == "UngarrisonBuilding") Assert.That(instruction, Does.Contain("building-wide"));
            if (action == "DiscoverEnemy") Assert.That(instruction, Does.Contain("finite"));
            Assert.That(pack.EstimatedInputTokens, Is.LessThan(4000));
        }

        [Test]
        public void ExplicitCurrentWoodMove_ReceivesTypedComposedDynamicContractWithoutCount50Downgrade()
        {
            var request = new CommanderSemanticProviderRequest(
                "Move all of my Wood villagers to the woodline west of my Town Center.",
                CommanderEconomyClarificationTests.Context());
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            string instruction = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            Assert.That(instruction, Does.Contain("move-workers"));
            Assert.That(instruction, Does.Contain("countMode:AllMatching"));
            Assert.That(instruction, Does.Contain("sourceKind:Tree"));
            Assert.That(instruction, Does.Not.Contain("ALL/current-resource workers/composed side+resource+TC location unsupported"));
            Assert.That(pack.EstimatedInputTokens, Is.LessThan(4000));
        }
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
            var messages = (JArray)body["messages"];
            // Count model-visible content/roles, not HTTP JSON escaping that the model never sees.
            int byteUnits = 0;
            foreach (JObject message in messages) byteUnits += Encoding.UTF8.GetByteCount((string)message["role"])
                + Encoding.UTF8.GetByteCount((string)message["content"]);
            TestContext.WriteLine("Offline pack audit: " + new JObject {
                ["utterance"] = utterance, ["systemUtf8Units"] = Encoding.UTF8.GetByteCount((string)messages[0]["content"]),
                ["userUtf8Units"] = Encoding.UTF8.GetByteCount((string)messages[1]["content"]),
                ["estimateWithMargin"] = byteUnits + 256, ["estimateMethod"] = "UTF8 byte units plus256 message margin; not verified model tokenizer",
                ["actualPromptTokens"] = JValue.CreateNull() }.ToString(Formatting.None));
            Assert.That(byteUnits + 256, Is.LessThan(4000),
                "The whole outgoing model-facing request needs a conservative bound, not an output max_tokens or characters/4 claim.");
            Assert.That((string)messages[1]["content"], Does.Contain(utterance), "Never truncate the player goal to fit.");
        }

        [Test] public async Task TooLargeRequiredMeaning_IsRejectedWithoutDispatchingOrExecutingPrefix()
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            string message = new string('\u754c', 1000); // Valid character length, too large for the full input-byte margin.
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(message,
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.Zero);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation, Does.Contain("limit"));
        }

        [TestCase("Build a House, then send the next five villagers from my first Town Center to gather Wood from trees.")]
        [TestCase("Move all of my Wood villagers to the woodline left of my Town Center.")]
        public async Task RealProductionCatalogProjection_StillFitsCompoundAndComposedRequests(string utterance)
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                var context = new CommanderContextBuilder().Build(simulation, manager, utterance);
                var transport = new CaptureTransport();
                var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
                var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(utterance, context), CancellationToken.None);
                TestContext.WriteLine("pack sections=" + JsonConvert.SerializeObject(provider.LastInputSectionEstimates)
                    + "; estimate=" + provider.LastInputEstimate);
                if (transport.Body != null)
                {
                    string packedUser = (string)JObject.Parse(transport.Body)["messages"]?[1]?["content"];
                    string packedContext = packedUser.Substring("Context:".Length,
                        packedUser.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
                    var packedFacts = JObject.Parse(packedContext);
                    TestContext.WriteLine("context-unit-lengths=" + JsonConvert.SerializeObject(packedFacts.Properties()
                        .Select(p => new { key = p.Name, bytes = Encoding.UTF8.GetByteCount(p.Value.ToString(Formatting.None)) })));
                    if (packedFacts["knowledge"] is JObject observedKnowledge)
                        TestContext.WriteLine("knowledge-unit-lengths=" + JsonConvert.SerializeObject(observedKnowledge.Properties()
                            .Select(p => new { key = p.Name, bytes = Encoding.UTF8.GetByteCount(p.Value.ToString(Formatting.None)) })));
                }
                Assert.That(transport.Calls, Is.EqualTo(1), result.SafeExplanation + "; conservative estimate=" + provider.LastInputEstimate
                    + "; sections=" + JsonConvert.SerializeObject(provider.LastInputSectionEstimates));
                Assert.That(provider.LastInputEstimate, Is.LessThan(4000));
                TestContext.WriteLine("Production catalog estimate=" + provider.LastInputEstimate + "; actual tokens unknown; " + utterance);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test] public async Task MalformedUsageMetadata_DoesNotInvalidateOtherwiseValidSemanticReply()
        {
            var transport = new CaptureTransport { Usage = new JObject { ["prompt_tokens"] = -1,
                ["prompt_tokens_details"] = "not an object", ["cost"] = "unverified" } };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.True, "Invalid observation metadata cannot poison valid semantic content.");
            Assert.That(Read(provider.LastUsage, "PromptTokens"), Is.Null);
            Assert.That(Read(provider.LastUsage, "CachedTokens"), Is.Null);
            Assert.That(Read(provider.LastUsage, "Cost"), Is.Null);
        }

        [Test] public async Task ResponseUsage_IsRetainedAsTypedObservedMetricsIncludingCachedInput()
        {
            var transport = new CaptureTransport {
                Usage = new JObject { ["prompt_tokens"] = 900, ["completion_tokens"] = 60,
                    ["total_tokens"] = 960, ["cost"] = .002m,
                    ["prompt_tokens_details"] = new JObject { ["cached_tokens"] = 250 },
                    ["completion_tokens_details"] = new JObject { ["reasoning_tokens"] = 20 } } };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            var usageProperty = typeof(OpenRouterCommanderProvider).GetProperty("LastUsage");
            Assert.That(usageProperty, Is.Not.Null, "Production currently drops the usage envelope.");
            var usage = usageProperty.GetValue(provider);
            Assert.That(Read(usage, "PromptTokens"), Is.EqualTo(900));
            Assert.That(Read(usage, "CompletionTokens"), Is.EqualTo(60));
            Assert.That(Read(usage, "CachedTokens"), Is.EqualTo(250));
            Assert.That(Read(usage, "ReasoningTokens"), Is.EqualTo(20));
            Assert.That(Read(usage, "Cost"), Is.EqualTo(.002m));
        }

        [Test] public async Task MissingResponseUsage_RemainsUnknownNotZero()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new CaptureTransport());
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            var property = typeof(OpenRouterCommanderProvider).GetProperty("LastUsage");
            Assert.That(property, Is.Not.Null);
            Assert.That(Read(property.GetValue(provider), "PromptTokens"), Is.Null);
        }

        [TestCase("Set the Wood allocation to zero.", "SetResourceAllocation")]
        [TestCase("Send spearmen to guard my Gold mine.", "DefendArea")]
        [TestCase("Split all idle villagers between Food and Wood.", "partition-workers")]
        [TestCase("Recruit archers and send them to guard my Gold mine.", "EnsureUnitCount")]
        public async Task MixedAndAliasRequests_KeepRegisteredFormsVisible(string utterance, string required)
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(utterance,
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.EqualTo(1));
            string contract = (string)JObject.Parse(transport.Body)["messages"]?[0]?["content"];
            Assert.That(contract, Does.Contain(required));
        }

        [Test] public async Task GenericRequest_AdvertisesEveryStrictlyParsedSemanticFamily()
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var request = new CommanderSemanticProviderRequest("Help me with my army and economy.",
                CommanderEconomyClarificationTests.Context());
            CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096, out var pack, out _);
            string contract = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            foreach (CommanderSemanticNodeType type in Enum.GetValues(typeof(CommanderSemanticNodeType)))
                Assert.That(contract, Does.Contain(type.ToString()), type.ToString());
            foreach (string required in new[] { "count:1..20", "count:0..200", "count:1..200",
                "count:1..50", "targetAge:Feudal|Castle|Imperial|Next", "resourceAmount?:1..1000000",
                "MaximumQueue amount1..8", "constraints?<=4", "nodes:1..4", "message<=180",
                "sourceKind:Any", "currentResource?", "executionOrder:Independent|Sequential|DependencyGraph" })
                Assert.That(contract, Does.Contain(required), required);
            await provider.TranslateSemanticAsync(request, CancellationToken.None);
            Assert.That(transport.Calls == 1 || provider.LastInputEstimate >= 4000, Is.True,
                "An overfull all-family fallback must reject before HTTP, never send an oversized prompt.");
        }

        [Test] public async Task EmptyUsageObject_IsUnknownAndDoesNotInventPromptTokens()
        {
            var transport = new CaptureTransport { Usage = new JObject() };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastUsage.HasReportedUsage, Is.False);
            Assert.That(provider.LastUsage.PromptTokens, Is.Null);
        }

        [Test] public async Task Repair_RetainsBothAttemptEstimatesAndUsage()
        {
            const string invalid = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":\"3\"}]}";
            const string corrected = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}";
            var transport = new CaptureTransport { Replies = new[] { invalid, corrected },
                Usages = new[] { new JObject { ["prompt_tokens"] = 850, ["cost"] = .001m },
                    new JObject { ["prompt_tokens"] = 180, ["cost"] = .0002m } } };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Train 3 spearmen.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(transport.Calls, Is.EqualTo(2));
            var attemptsProperty = typeof(OpenRouterCommanderProvider).GetProperty("LastSemanticAttempts");
            Assert.That(attemptsProperty, Is.Not.Null);
            var attempts = (System.Collections.IList)attemptsProperty.GetValue(provider);
            Assert.That(attempts, Has.Count.EqualTo(2));
            Assert.That(Read(attempts[0], "InputEstimate"), Is.GreaterThan(0));
            Assert.That(Read(attempts[1], "InputEstimate"), Is.GreaterThan(0));
            Assert.That(Read(Read(attempts[0], "Usage"), "PromptTokens"), Is.EqualTo(850));
            Assert.That(Read(Read(attempts[1], "Usage"), "PromptTokens"), Is.EqualTo(180));
            Assert.That(Read(Read(attempts[0], "Usage"), "Cost"), Is.EqualTo(.001m));
            Assert.That(Read(Read(attempts[1], "Usage"), "Cost"), Is.EqualTo(.0002m));
            Assert.That(Read(attempts[0], "ResultStage"), Does.Contain("schema"));
            Assert.That(Read(attempts[1], "ResultStage"), Does.Contain("valid"));
        }

        [Test] public void ReferencedAndAmbiguousCanonicalNames_SurviveKnowledgeRetrieval()
        {
            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
            var relevant = JObject.Parse(CommanderKnowledgeRetrieval.Retrieve(catalog,
                "Build a House near my Town Center then train Spearman at the Barracks.", Civilization.English));
            var ids = new HashSet<string>();
            foreach (JObject row in (JArray)relevant["units"]) ids.Add((string)row["id"]);
            foreach (JObject row in (JArray)relevant["buildings"]) ids.Add((string)row["id"]);
            foreach (string id in new[] { "building:House", "building:TownCenter", "building:Barracks",
                catalog.FindUnit("Spearman").StableId })
                Assert.That(ids, Does.Contain(id), id);
            var ambiguous = JObject.Parse(CommanderKnowledgeRetrieval.Retrieve(catalog,
                "Build a dropoff.", Civilization.English));
            Assert.That(((JArray)ambiguous["nameInterpretations"]).ToString(Formatting.None), Does.Contain("dropoff"));
        }

        [Test] public void ReferencedFacts_PreserveColumnsNullsCapabilityAndBoundedPending()
        {
            var knowledge = new JObject { ["units"] = new JArray(
                new JObject { ["id"] = "unit:Spearman", ["name"] = "Spearman", ["age"] = 2,
                    ["producer"] = "Barracks", ["cost"] = new JObject { ["food"] = 50, ["wood"] = 20, ["gold"] = 0, ["stone"] = 0 },
                    ["availableForCivilization"] = true, ["commanderExecutable"] = true },
                new JObject { ["id"] = "unit:Horseman", ["name"] = "Horseman", ["age"] = 2,
                    ["producer"] = JValue.CreateNull(), ["cost"] = new JObject { ["food"] = 0, ["wood"] = 0, ["gold"] = 30, ["stone"] = 0 },
                    ["availableForCivilization"] = true, ["commanderExecutable"] = true }) };
            var context = new CommanderContext(0, 0, new CommanderResourceSnapshot(0, 0, 0, 0),
                0, 0, 200, 1, "English", new List<CommanderBuildingSnapshot>(), new List<CommanderUnitSnapshot>(),
                new List<CommanderBuildingSnapshot>(), new List<string>(), new List<CommanderGoalSnapshot>(),
                new List<CommanderVisibleResourceSnapshot>(), new List<CommanderUnitOptionSnapshot>(),
                new List<CommanderWorkerAllocationSnapshot>(), new List<CommanderVisibleEnemyMilitarySnapshot>(),
                knowledge.ToString(Formatting.None), canonicalUnitIds: new[] { "unit:Spearman" });
            var pending = new CommanderPendingClarification(new CommanderWorkerAllocationDraft(
                CommanderWorkerAllocationMode.SelectedCount, CommanderWorkerCountMode.Exact, null,
                new CommanderWorkerSelector(CommanderWorkerState.Gathering, ResourceType.Wood),
                new CommanderResourceDestination(ResourceType.Food, ResourceSourceKind.Berries),
                200, CommanderResourceAmountMode.AdditionalGathered),
                "Gather 200 additional Food from Berries with Wood villagers", "How many villagers?", 1, 12);
            var request = new CommanderSemanticProviderRequest("three", context, pendingClarification: pending);
            CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096, out var pack, out _);
            string user = (string)JObject.Parse(pack.Json)["messages"]?[1]?["content"];
            Assert.That(user, Does.Contain(pending.Serialize()));
            string compact = user.Substring("Context:".Length, user.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
            var facts = (JObject)JObject.Parse(compact)["knowledge"];
            var columns = ((JArray)facts["unitsColumns"]).Select(x => (string)x).ToArray();
            var rows = (JArray)facts["units"];
            int id = Array.IndexOf(columns, "id"), producer = Array.IndexOf(columns, "producer");
            int cost = Array.IndexOf(columns, "cost"), capability = Array.IndexOf(columns, "capabilitySupported");
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That((string)rows[0][id], Is.EqualTo("unit:Spearman"));
            Assert.That((string)rows[1][id], Is.EqualTo("unit:Horseman"));
            Assert.That(rows[1][producer].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((int)rows[0][cost][0], Is.EqualTo(50));
            Assert.That((bool)rows[1][capability], Is.False, "Knowledge must not grant unavailable execution capability.");
        }

        [Test] public async Task PreflightRejectionAfterASentRequest_ResetsAttemptCount()
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(new string('\u754c', 1000),
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts, Is.Empty);
            Assert.That(provider.HasAttemptedRequest, Is.False);
        }

        [Test] public async Task CompletedAttemptSnapshot_RemainsImmutableAcrossLaterRequests()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new CaptureTransport());
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            var completed = provider.LastSemanticAttempts;
            Assert.That(completed, Has.Count.EqualTo(1));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(new string('\u754c', 1000),
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(completed, Has.Count.EqualTo(1), "A later request may not erase an earlier HTTP attempt record.");
            Assert.That(provider.LastSemanticAttempts, Is.Empty);
        }

        [Test] public void NineExplicitCanonicalNames_SurviveRealContextAndPackedColumns()
        {
            const string utterance = "Train Villager, Spearman, Archer, Horseman, Scout, Knight, Crossbowman, Monk, and Longbowman.";
            var request = ProductionRequest(utterance);
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            var ids = PackedKnowledgeIds(pack);
            foreach (string id in new[] { "unit:0", "unit:1", "unit:2", "unit:3", "unit:4",
                "unit:7", "unit:8", "unit:9", "unit:10" })
                Assert.That(ids, Does.Contain(id), id);
        }

        [Test] public void SimilarNameHint_SurvivesRealRetrievalAndPackWithoutBecomingRequired()
        {
            var request = ProductionRequest("Train longbowmna.");
            var sourceKnowledge = (JObject)JObject.Parse(request.SerializedContext)["knowledge"];
            Assert.That(((JArray)sourceKnowledge["requiredCanonicalIds"]), Is.Empty,
                "A typo suggestion must not acquire required/executable authority.");
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            string user = (string)JObject.Parse(pack.Json)["messages"]?[1]?["content"];
            string compact = user.Substring("Context:".Length,
                user.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
            var packedKnowledge = (JObject)JObject.Parse(compact)["knowledge"];
            var columns = ((JArray)packedKnowledge["nameInterpretationsColumns"]).Select(x => (string)x).ToArray();
            var interpretations = (JArray)packedKnowledge["nameInterpretations"];
            Assert.That(interpretations, Is.Not.Null);
            var typo = interpretations.OfType<JArray>().Single(x => (string)x[Array.IndexOf(columns, "phrase")] == "longbowmna");
            Assert.That((bool)typo[Array.IndexOf(columns, "interpretationOnly")], Is.True);
            Assert.That((bool)typo[Array.IndexOf(columns, "ambiguous")], Is.False);
            Assert.That(((JArray)typo[Array.IndexOf(columns, "unitCandidates")]).Select(x => (string)x), Does.Contain("unit:10"));
            Assert.That(PackedKnowledgeIds(pack), Does.Contain("unit:10"));
            Assert.That(pack.EstimatedInputTokens, Is.LessThan(4000));
            string contract = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            Assert.That(contract, Does.Contain("Similar-name hints are interpretation only; Clarify before Request"));
        }

        [Test] public void SimilarNameHint_DoesNotEvictExactCanonicalReferences()
        {
            const string utterance = "Train Villager, Spearman, Archer, Horseman, Scout, Knight, Crossbowman, Monk, and longbowmna.";
            var request = ProductionRequest(utterance);
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            var ids = PackedKnowledgeIds(pack);
            foreach (string id in new[] { "unit:0", "unit:1", "unit:2", "unit:3", "unit:4",
                "unit:7", "unit:8", "unit:9" })
                Assert.That(ids, Does.Contain(id), id);
            Assert.That(ids, Does.Contain("unit:10"));
        }

        [Test] public void LateCanonicalName_AfterOldWordAndPhraseWindowsSurvivesRealPack()
        {
            string utterance = "Build " + string.Join(" ", Enumerable.Repeat("please", 100)) + " Market.";
            Assert.That(utterance.Length, Is.LessThanOrEqualTo(1024));
            var request = ProductionRequest(utterance);
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            Assert.That(PackedKnowledgeIds(pack), Does.Contain("building:Market"));
        }

        [Test] public void PendingGraphAndAmbiguousAlternatives_AreRetainedOrWholeRequestRefused()
        {
            var proposal = new JObject { ["outcome"] = "Clarify", ["message"] = "Which count?",
                ["pending"] = new JObject { ["executionOrder"] = "Independent", ["nodes"] = new JArray(
                    new JObject { ["type"] = "BuildStructure", ["structure"] = "Market",
                        ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } }) },
                ["missingSlots"] = new JArray(new JObject { ["node"] = 0, ["path"] = "count",
                    ["type"] = "Count", ["alternatives"] = new JArray(1) }) };
            var parsed = CommanderSemanticJson.Parse(proposal.ToString(Formatting.None));
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var pending = new CommanderPendingClarification(parsed.PendingGraphDraft,
                "Build a dropoff", "Which count?", 1, 9);
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                var builder = new CommanderContextBuilder();
                var overload = typeof(CommanderContextBuilder).GetMethod("Build", new[] {
                    typeof(GameSimulation), typeof(CommanderGoalManager), typeof(string), typeof(string) });
                Assert.That(overload, Is.Not.Null, "Current reply and bounded pending graph must both reach retrieval.");
                var context = (CommanderContext)overload.Invoke(builder, new object[] {
                    simulation, manager, "one", pending.Serialize() });
                var request = new CommanderSemanticProviderRequest("one", context, pendingClarification: pending);
                bool fit = CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                    out var pack, out string reason);
                if (!fit) Assert.That(reason, Does.Contain("required"));
                else
                {
                    var ids = PackedKnowledgeIds(pack);
                    Assert.That(ids, Does.Contain("building:Market"));
                    Assert.That((string)JObject.Parse(pack.Json)["messages"]?[1]?["content"], Does.Contain("dropoff"));
                }
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test] public async Task TooManyRequiredNames_RefuseWholeRequestBeforeHttp()
        {
            const string utterance = "Train Villager Spearman Archer Horseman Scout Knight Crossbowman Monk Longbowman "
                + "and build House Barracks TownCenter Mill LumberYard Mine ArcheryRange Stables Farm Market.";
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(ProductionRequest(utterance), CancellationToken.None);
            Assert.That(transport.Calls, Is.Zero);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation, Does.Contain("required").IgnoreCase);
        }

        [TestCase("{\"choices\":[]}", "response-invalid-envelope")]
        [TestCase("{\"choices\":[{},{}]}", "response-invalid-envelope")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"x\"}}]}", "response-truncated")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"content_filter\",\"message\":{\"content\":\"x\"}}]}", "response-incomplete")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"\"}}]}", "response-empty")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":[{\"type\":\"image\"}]}}]}", "response-unsupported-parts")]
        [TestCase("{\"error\":{\"code\":500}}", "provider-error")]
        [TestCase("{\"choices\":[{\"finish_reason\":{},\"message\":{\"content\":\"x\"}}]}", "response-invalid-envelope")]
        [TestCase("{\"error\":{\"code\":{}}}", "provider-error")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":[{\"type\":{},\"text\":\"x\"}]}}]}", "response-invalid-envelope")]
        public async Task RejectedSuccessfulHttp_RecordsActualResultStage(string envelope, string stage)
        {
            var transport = new EnvelopeTransport(new CommanderHttpResponse(200, envelope));
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.EqualTo(200));
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo(stage));
            Assert.That(result.SafeExplanation, Does.Not.Contain(envelope));
        }

        [Test] public async Task MalformedProviderErrorCode_UsesSafeDefaultProviderFailure()
        {
            var transport = new EnvelopeTransport(new CommanderHttpResponse(200, "{\"error\":{\"code\":{}}}"));
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SafeExplanation, Is.EqualTo("Commander AI provider unavailable. Please try again later."));
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("provider-error"));
        }

        [Test] public async Task NullTransportResponse_RecordsTransportNullNotSuccess()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new EnvelopeTransport(null));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("transport-null"));
            Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.Null);
        }

        [Test] public async Task OversizedSuccessfulHttpBody_RecordsResponseOversize()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential",
                new EnvelopeTransport(new CommanderHttpResponse(200, new string('x', 65537))));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.EqualTo(200));
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("response-oversize"));
        }

        [Test] public async Task OlderCancelledLateAttempt_DoesNotOverwriteNewerRequestMetrics()
        {
            var transport = new OverlapTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            using (var cancelled = new CancellationTokenSource())
            {
                var old = provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                    CommanderEconomyClarificationTests.Context()), cancelled.Token);
                cancelled.Cancel();
                await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a Mill.",
                    CommanderEconomyClarificationTests.Context()), CancellationToken.None);
                Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
                Assert.That(provider.LastUsage.PromptTokens, Is.EqualTo(222));
                Assert.That(provider.LastHttpStatusCode, Is.EqualTo(200));
                transport.ReleaseOld();
                Assert.ThrowsAsync<TaskCanceledException>(async () => await old);
                Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
                Assert.That(provider.LastSemanticAttempts[0].Usage.PromptTokens, Is.EqualTo(222));
                Assert.That(provider.LastUsage.PromptTokens, Is.EqualTo(222));
                Assert.That(provider.LastHttpStatusCode, Is.EqualTo(200));
                Assert.That(Read(provider, "SemanticHttpAttemptCount"), Is.EqualTo(2L));
                var history = (System.Collections.IEnumerable)Read(provider, "RecentSemanticAttempts");
                var observed = history.Cast<CommanderProviderAttempt>().ToArray();
                Assert.That(observed, Has.Length.EqualTo(2));
                Assert.That(observed.Single(x => x.ResultStage == "caller-canceled").Usage.PromptTokens, Is.Null);
            }
        }

        private static CommanderSemanticProviderRequest ProductionRequest(string utterance)
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                return new CommanderSemanticProviderRequest(utterance,
                    new CommanderContextBuilder().Build(simulation, manager, utterance));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        private static HashSet<string> PackedKnowledgeIds(CommanderSemanticRequestPack pack)
        {
            string user = (string)JObject.Parse(pack.Json)["messages"]?[1]?["content"];
            string compact = user.Substring("Context:".Length,
                user.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
            var knowledge = (JObject)JObject.Parse(compact)["knowledge"];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string table in new[] { "units", "buildings" })
            {
                if (!(knowledge[table] is JArray rows)) continue;
                var columns = ((JArray)knowledge[table + "Columns"]).Select(x => (string)x).ToArray();
                int idColumn = Array.IndexOf(columns, "id");
                foreach (JArray row in rows) ids.Add((string)row[idColumn]);
            }
            return ids;
        }

        private static object Read(object value, string name)
        { Assert.That(value, Is.Not.Null); var member = value.GetType().GetProperty(name); Assert.That(member, Is.Not.Null, name); return member.GetValue(value); }

        private sealed class CaptureTransport : ICommanderHttpTransport
        {
            internal string Body;
            internal int Calls;
            internal JObject Usage;
            internal string[] Replies;
            internal JObject[] Usages;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Calls++; Body = body;
                var envelope = new JObject { ["choices"] = new JArray(new JObject {
                    ["finish_reason"] = "stop", ["message"] = new JObject {
                        ["content"] = Replies != null ? Replies[Calls - 1] : "{\"outcome\":\"Answer\",\"message\":\"Offline fixture.\"}" } }) };
                if (Usages != null) envelope["usage"] = Usages[Calls - 1];
                else if (Usage != null) envelope["usage"] = Usage;
                return Task.FromResult(new CommanderHttpResponse(200, envelope.ToString(Formatting.None)));
            }
        }

        private sealed class EnvelopeTransport : ICommanderHttpTransport
        {
            private readonly CommanderHttpResponse response;
            internal int Calls;
            internal EnvelopeTransport(CommanderHttpResponse response) => this.response = response;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            { Calls++; return Task.FromResult(response); }
        }

        private sealed class OverlapTransport : ICommanderHttpTransport
        {
            private readonly TaskCompletionSource<CommanderHttpResponse> old =
                new TaskCompletionSource<CommanderHttpResponse>();
            private int calls;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                if (Interlocked.Increment(ref calls) == 1) return old.Task;
                return Task.FromResult(Reply(222));
            }
            internal void ReleaseOld() => old.TrySetResult(Reply(111));
            private static CommanderHttpResponse Reply(int prompt) => new CommanderHttpResponse(200,
                new JObject { ["usage"] = new JObject { ["prompt_tokens"] = prompt },
                    ["choices"] = new JArray(new JObject { ["finish_reason"] = "stop",
                        ["message"] = new JObject { ["content"] = "{\"outcome\":\"Answer\",\"message\":\"fixture\"}" } }) }
                .ToString(Formatting.None));
        }
    }
}
```

### `Assets/Tests/EditMode/CommanderPhase5CProviderBudgetTests.cs.meta`

```meta
fileFormatVersion: 2
guid: b1c395e40706a614ab0159dd56c0a2f5
```

### `Assets/Tests/EditMode/CommanderPhase5CScenarioArtifactTests.cs`

```cs
using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5CScenarioArtifactTests
    {
        [Test]
        public void PerTurnArtifact_IsAtomicBoundedAndRejectsUnsafeKeys()
        {
            Type writer = typeof(OpenEmpires.TestSupport.ScenarioEvidenceRecorder);
            MethodInfo write = writer.GetMethod("RecordPhase5CTurn", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(write, Is.Not.Null, "Task 6 extends the existing atomic recorder with a safe turn record.");
            string directory = Path.Combine(Path.GetTempPath(), "phase5c-artifact-" + Guid.NewGuid().ToString("N"));
            try
            {
                var safe = new JObject
                {
                    ["schemaVersion"] = 1, ["caseId"] = "mine-yes-original-request",
                    ["requestId"] = "request-1", ["utterance"] = "Build a gold mine near gold.",
                    ["actualPromptTokens"] = JValue.CreateNull(), ["status"] = "started"
                };
                string path = Path.Combine(directory, "mine-yes-original-request-turn-0.json");
                var recorder = new OpenEmpires.TestSupport.ScenarioEvidenceRecorder(path, Guid.NewGuid().ToString("D"));
                write.Invoke(recorder, new object[] { safe });
                Assert.That(File.Exists(path), Is.True);
                Assert.That(JObject.Parse(File.ReadAllText(path))["actualPromptTokens"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
                var unsafeRecord = (JObject)safe.DeepClone();
                unsafeRecord["entityId"] = 42;
                Assert.That(() => write.Invoke(recorder, new object[] { unsafeRecord }),
                    Throws.TypeOf<TargetInvocationException>());
                Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PerTurnArtifact_AllowsDistinctObservationTurnWithNoExecutionTicket()
        {
            string directory = Path.Combine(Path.GetTempPath(), "phase5c-observation-" + Guid.NewGuid().ToString("N"));
            try
            {
                string path = Path.Combine(directory, "causal-information-owned-task-facts.json");
                var recorder = new OpenEmpires.TestSupport.ScenarioEvidenceRecorder(path, Guid.NewGuid().ToString("D"));
                recorder.RecordPhase5CTurn(new JObject
                {
                    ["schemaVersion"] = 1, ["caseId"] = "causal-information-owned-task-facts",
                    ["requestId"] = JValue.CreateNull(), ["turnCorrelationId"] = "ui-turn:1",
                    ["utterance"] = "Why is my troop production stuck?", ["status"] = "observed"
                });
                var saved = JObject.Parse(File.ReadAllText(path));
                Assert.That(saved["requestId"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That((string)saved["turnCorrelationId"], Is.EqualTo("ui-turn:1"));
                Assert.That((string)saved["caseId"], Is.EqualTo("causal-information-owned-task-facts"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
```

### `Assets/Tests/EditMode/CommanderPhase5CScenarioArtifactTests.cs.meta`

```meta
fileFormatVersion: 2
guid: 26583bcda9a08a74c87cd7d78b56ce39
```

### `Assets/Tests/PlayMode/CommanderPhase5CUIPlayModeTests.cs`

```cs
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5COffline")]
    public sealed class CommanderPhase5CUIPlayModeTests
    {
        private CommanderChatUI chat;
        private CommanderGoalManager goals;
        private CommanderIntentDispatcher dispatcher;
        private GameSimulation simulation;
        private SimulationConfig config;
        private SlowProvider provider;
        private StrategicPlanner strategicPlanner;
        private StrategicPipeline strategicPipeline;
        private GameObject renderCameraObject;
        private int previousWidth, previousHeight;

        [SetUp]
        public void SetUp()
        {
            previousWidth = Screen.width; previousHeight = Screen.height;
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            goals = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goals);
            provider = new SlowProvider();
            chat = new GameObject("Phase5CCommanderUI").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromMilliseconds(60));
        }

        [TearDown]
        public void TearDown()
        {
            if (previousWidth > 0 && previousHeight > 0) Screen.SetResolution(previousWidth, previousHeight, false);
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            if (renderCameraObject != null) UnityEngine.Object.DestroyImmediate(renderCameraObject);
            dispatcher?.Dispose();
            strategicPipeline?.Dispose();
            strategicPlanner?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator MinimizedStartup_OpensTabbedDockWithFixedMultilineComposer()
        {
            chat.enabled = true;
            yield return null;
            Assert.That(chat.IsExpanded, Is.False);
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/Launcher"), Is.Not.Null);
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Views/ChatTab"), Is.Not.Null);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab"), Is.Not.Null);
            var input = chat.transform.Find("CommanderCanvas/Panel/TextSubmission/Input")?.GetComponent("TMP_InputField");
            Assert.That(input, Is.Not.Null);
            Assert.That(input.GetType().GetProperty("lineType")?.GetValue(input)?.ToString(), Is.EqualTo("MultiLineNewline"));
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Transcript")?.GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TaskBoard")?.GetComponent<ScrollRect>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Timeout_RetainsDraftAndOffersExplicitRetryEditCancel()
        {
            chat.enabled = true;
            chat.OpenCommander();
            chat.InputText = "gather 200 gold with 3 villagers";
            Task<CommanderAIChatSubmission> request = chat.SubmitCurrentInputAsync();
            while (!request.IsCompleted) yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(1));
            Assert.That(chat.InputText, Is.EqualTo("gather 200 gold with 3 villagers"));
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Retry")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Edit")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")), Does.StartWith("Failed"));
            Canvas.ForceUpdateCanvases();
            var recoveryCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            chat.transform.Find("CommanderCanvas/Panel/RequestRecovery").GetComponent<RectTransform>().GetWorldCorners(recoveryCorners);
            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
            Assert.That(recoveryCorners[0].y, Is.GreaterThanOrEqualTo(composerCorners[2].y));
            yield return new WaitForEndOfFrame();
            SaveCurrentFrame("timeout-recovery");
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DockInputOwnership_TracksActualExpandedAndCompactRegions()
        {
            chat.enabled = true;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var compact = chat.transform.Find("CommanderCanvas/CompactCommander").GetComponent<RectTransform>();
            Assert.That(chat.ContainsScreenPoint(RectTransformUtility.WorldToScreenPoint(null, compact.TransformPoint(compact.rect.center))), Is.True);
            Assert.That(chat.ContainsScreenPoint(new Vector2(10,10)), Is.False);
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var panel = chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>();
            var panelCenter = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(panel.rect.center));
            Assert.That(chat.ContainsScreenPoint(panelCenter), Is.True);
            Assert.That(chat.ContainsScreenPoint(new Vector2(10,10)), Is.False);
            Assert.That(CommanderChatUI.TryHandleEscapeInput(), Is.True);
            Assert.That(chat.IsExpanded, Is.False);
            Assert.That(chat.ContainsScreenPoint(panelCenter), Is.False);
        }

        [Test]
        public void CapabilityCards_NameRealActionAndDoNotInventAllMatchingCount()
        {
            var selector = new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers, 1, allMatching: true);
            var moveSelector = new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers, 3);
            var location = new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase);
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.MoveUnits, moveSelector, location));
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.GarrisonUnits, selector, location,
                structureType: BuildingType.TownCenter));
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.UngarrisonBuilding,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers), location,
                structureType: BuildingType.TownCenter));
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.DiscoverEnemy,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Scout),
                new CommanderLocationSelector(CommanderLocationSelectorKind.VisibleEnemy),
                targetSelector: new CommanderTargetSelector(BuildingType.TownCenter)));
            var cards = CommanderTaskBoardProjection.Capture(goals, null, 1).Cards;
            Assert.That(cards, Has.Count.EqualTo(4));
            Assert.That(cards[0].CurrentStep, Does.Contain("Discover"));
            Assert.That(cards[1].CurrentStep, Does.Contain("Ungarrison"));
            Assert.That(cards[2].CurrentStep, Does.Contain("Garrison"));
            Assert.That(cards[3].CurrentStep, Does.Contain("Move"));
            Assert.That(cards[1].Progress, Does.Contain("Awaiting exact shelter occupant snapshot"));
            Assert.That(cards[2].Progress, Does.Contain("Awaiting exact eligible shelter snapshot"));
            Assert.That(cards[3].Progress, Does.Contain("Awaiting exact bound subjects"));
        }

        [UnityTest]
        public IEnumerator TasksView_KeepsActiveCancelVisibleAndHistoryCollapsed()
        {
            chat.enabled = true;
            var completed = goals.SubmitBuildStructure(BuildingType.House);
            completed.SetStatus(CommanderGoalStatus.Completed, "One requested House completed.");
            var goal = goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            yield return null;
            chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var board = chat.transform.Find("CommanderCanvas/Panel/TaskBoard");
            Assert.That(board.gameObject.activeInHierarchy, Is.True);
            Assert.That(board.Find("Viewport/Content/TaskCard/CancelTask")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(board.Find("Viewport/Content/TaskCard/Details")?.gameObject.activeInHierarchy, Is.False);
            Assert.That(board.Find("Viewport/Content/HistoryToggle"), Is.Not.Null);
            yield return null;
            board.Find("Viewport/Content/TaskCard/CancelTask").GetComponent<Button>().onClick.Invoke();
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
        }

        [UnityTest]
        public IEnumerator TasksView_ActiveCancelIsInsideViewportWithoutScrolling()
        {
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            yield return null;
            chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var board = chat.transform.Find("CommanderCanvas/Panel/TaskBoard");
            var viewport = board.Find("Viewport").GetComponent<RectTransform>();
            var cancel = board.Find("Viewport/Content/TaskCard/CancelTask").GetComponent<RectTransform>();
            var viewCorners = new Vector3[4];
            var cancelCorners = new Vector3[4];
            viewport.GetWorldCorners(viewCorners);
            cancel.GetWorldCorners(cancelCorners);
            Assert.That(cancelCorners[0].y, Is.GreaterThanOrEqualTo(viewCorners[0].y + 2),
                "The active task's Cancel control must be visible without scrolling.");
            Assert.That(cancelCorners[2].y, Is.LessThanOrEqualTo(viewCorners[2].y - 2));
        }

        [UnityTest]
        public IEnumerator ExpandedDock_HidesItsLauncherAndHasOneActiveCommanderCanvas()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy, Is.False);
            int activeCanvases = 0;
            foreach (var ui in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                if (ui.gameObject.activeInHierarchy && ui.transform.Find("CommanderCanvas")?.gameObject.activeInHierarchy == true)
                    activeCanvases++;
            Assert.That(activeCanvases, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator IdleDock_ShowsReadableSettingsWithoutLegacyStatusAndTallVoiceStack()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var panel = chat.transform.Find("CommanderCanvas/Panel");
            Assert.That(TextOf(panel.Find("Settings/Label")), Is.EqualTo("Settings"));
            Assert.That(panel.Find("CommanderStatus").gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.Find("Current plan status").gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.Find("VoiceBar").GetComponent<LayoutElement>().preferredHeight,
                Is.LessThanOrEqualTo(42));
        }

        [UnityTest]
        public IEnumerator SettingsSurface_OffersProactiveVoiceAndTextAIWithoutConsent()
        {
            InstallRenderFixtureCamera();
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            chat.transform.Find("CommanderCanvas/Panel/Settings").GetComponent<Button>().onClick.Invoke();
            var menu = chat.transform.Find("CommanderCanvas/CompactCommander/SettingsMenu");
            Assert.That(menu?.gameObject.activeInHierarchy, Is.True);
            Assert.That(menu.Find("VoiceSettings")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(menu.Find("TextAISettings")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/VoiceSetup").gameObject.activeInHierarchy, Is.False);
            ForceFixtureRepaint();
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            SaveCurrentFrame("fix1-settings-menu");
            yield return null; // Separate physical click; same-frame UI actions are intentionally single-use.
            menu.Find("VoiceSettings").GetComponent<Button>().onClick.Invoke();
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/VoiceSetup").gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/VoiceSetup/SetupViewport/SetupContent/Actions/Action1")
                .GetComponent<Button>().interactable, Is.False, "Opening Voice settings does not grant audio consent.");
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator SettingsSurface_EscapeClosesBeforeGameplayReceivesIt()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            chat.transform.Find("CommanderCanvas/Panel/Settings").GetComponent<Button>().onClick.Invoke();
            var menu = chat.transform.Find("CommanderCanvas/CompactCommander/SettingsMenu");
            Assert.That(menu.gameObject.activeInHierarchy, Is.True);
            Assert.That(CommanderChatUI.TryHandleEscapeInput(), Is.True);
            Assert.That(menu.gameObject.activeInHierarchy, Is.False);
            Assert.That(chat.IsExpanded, Is.False);
        }

        [UnityTest]
        public IEnumerator Header_ShowsProviderStateSeparateFromLatestRequest()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            var header = chat.transform.Find("CommanderCanvas/Panel/ProviderConnection");
            Assert.That(header, Is.Not.Null);
            Assert.That(TextOf(header), Does.Contain("Text AI"));
            var question = chat.SubmitMessageAsync("strategy status");
            while (!question.IsCompleted) yield return null;
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")), Does.StartWith("Completed"));
            Assert.That(TextOf(header), Does.Not.Contain("Completed"));
        }

        [UnityTest]
        public IEnumerator StrategicPreview_ShowsValidatedMilestonesAndDirectSingleStageDecisions()
        {
            InstallRenderFixtureCamera();
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            strategicPlanner = new StrategicPlanner(goals, _ => 5000);
            strategicPipeline = new StrategicPipeline(simulation, goals, strategicPlanner);
            chat.InitializeStrategic(new MockStrategicAIProvider(), strategicPipeline);
            chat.enabled = true;
            chat.OpenCommander();
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"RangedReinforcement\"}]}");
            var turn = chat.SubmitMessageAsync("prepare ranged reinforcements");
            provider.CompleteLatest(result);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Awaiting approval"));
            var approval = chat.transform.Find("CommanderCanvas/Panel/Approval");
            Assert.That(approval.gameObject.activeInHierarchy, Is.True);
            string preview = TextOf(approval.Find("PreviewScroll/Viewport/Details"));
            Assert.That(preview, Does.Contain("8 food workers"));
            Assert.That(preview, Does.Contain("8 wood workers"));
            Assert.That(preview, Does.Contain("ArcheryRange"));
            Assert.That(preview, Does.Contain("10 total archers"));
            Assert.That(preview, Does.Contain("after"));
            Assert.That(approval.Find("ApproveStrategy")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(approval.Find("ConfirmStrategy")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(approval.Find("DismissStrategy")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(goals.Goals, Is.Empty, "Preview has no execution authority.");
            Canvas.ForceUpdateCanvases();
            var panelCorners = new Vector3[4];
            var decisionCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>().GetWorldCorners(panelCorners);
            approval.Find("DismissStrategy").GetComponent<RectTransform>().GetWorldCorners(decisionCorners);
            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
            Assert.That(decisionCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y));
            Assert.That(composerCorners[2].y, Is.LessThanOrEqualTo(decisionCorners[0].y - 4));
            ForceFixtureRepaint();
            yield return new WaitForEndOfFrame();
            SaveCurrentFrame("fix1-strategic-proposal");
            yield return null;
            approval.Find("DismissStrategy").GetComponent<Button>().onClick.Invoke();
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Cancelled"));
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator StrategicApprovalButtons_SameFrameCannotApproveAndConfirm()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            strategicPlanner = new StrategicPlanner(goals, _ => 5000);
            strategicPipeline = new StrategicPipeline(simulation, goals, strategicPlanner);
            chat.InitializeStrategic(new MockStrategicAIProvider(), strategicPipeline);
            chat.enabled = true;
            chat.OpenCommander();
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"RangedReinforcement\"}]}");
            var turn = chat.SubmitMessageAsync("prepare ranged reinforcements");
            provider.CompleteLatest(result);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var approval = chat.transform.Find("CommanderCanvas/Panel/Approval");
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            approval.Find("ApproveStrategy").GetComponent<Button>().onClick.Invoke();
            approval.Find("ConfirmStrategy").GetComponent<Button>().onClick.Invoke();
            Assert.That(chat.LatestStrategicDecision, Is.Not.Null);
            Assert.That(strategicPipeline.DecisionHistory.History, Has.Count.EqualTo(1));
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(provider.Attempts, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StrategicAdaptationPreview_NamesExistingSourceAndRequiresExplicitConfirm()
        {
            InstallRenderFixtureCamera();
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            strategicPlanner = new StrategicPlanner(goals, _ => 5000);
            strategicPipeline = new StrategicPipeline(simulation, goals, strategicPlanner);
            chat.InitializeStrategic(new MockStrategicAIProvider(), strategicPipeline);
            var old = strategicPlanner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
            Assert.That(old, Is.Not.Null);
            chat.enabled = true;
            chat.OpenCommander();
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"DefensiveTurtle\"}]}");
            var turn = chat.SubmitMessageAsync("prepare fortified defenses");
            provider.CompleteLatest(result);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            var approval = chat.transform.Find("CommanderCanvas/Panel/Approval");
            string preview = TextOf(approval.Find("PreviewScroll/Viewport/Details"));
            Assert.That(preview, Does.Contain("Strategy #" + old.StrategicPlanId));
            Assert.That(preview, Does.Contain("2 towers"));
            Assert.That(preview, Does.Contain("8 total spearmen"));
            Assert.That(preview, Does.Contain("Confirm as command"));
            Assert.That(approval.Find("ApproveStrategy").GetComponent<Button>().interactable, Is.False,
                "Ordinary approval cannot replace an existing plan.");
            Assert.That(approval.Find("ConfirmStrategy").GetComponent<Button>().interactable, Is.True);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Current plan status").gameObject.activeInHierarchy,
                Is.False, "The old plan summary belongs in Tasks while its adaptation preview owns Chat.");
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TaskControls").gameObject.activeInHierarchy,
                Is.False);
            Canvas.ForceUpdateCanvases();
            var panelCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>().GetWorldCorners(panelCorners);
            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
            Assert.That(composerCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y),
                "The fixed composer must remain inside the actual dock in adaptation view.");
            ForceFixtureRepaint();
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            SaveCurrentFrame("fix1-strategic-adaptation");
            yield return null;
            chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TaskControls").gameObject.activeInHierarchy,
                Is.True, "Existing owned plan controls remain accessible in Tasks.");
            chat.transform.Find("CommanderCanvas/Panel/Views/ChatTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            approval.Find("DismissStrategy").GetComponent<Button>().onClick.Invoke();
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [UnityTest]
        public IEnumerator Composer_LeavesTwoLineDraftAreaBesideCompactSend()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var input = chat.transform.Find("CommanderCanvas/Panel/TextSubmission/Input").GetComponent<RectTransform>();
            var send = chat.transform.Find("CommanderCanvas/Panel/TextSubmission/Send").GetComponent<RectTransform>();
            Assert.That(input.rect.height, Is.GreaterThanOrEqualTo(60));
            Assert.That(input.rect.width, Is.GreaterThanOrEqualTo(send.rect.width * 2));
        }

        [UnityTest]
        public IEnumerator CompoundApproval_ShowsFullValidatedPreviewAndExplicitDecisionsWithoutIssuing()
        {
            InstallRenderFixtureCamera();
            // The approval fixture performs real graph admission on the main thread;
            // its timeout is test-local, not a production timeout change.
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"producerFromNode\":0,\"dependsOn\":[0]}]}");
            var turn = chat.SubmitMessageAsync("build a Barracks, then train three Spearmen there");
            provider.CompleteLatest(parsed);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.PendingActionPlan, Is.Not.Null,
                $"attempts={provider.Attempts}; transcript={chat.DisplayedTranscript}");
            var approval = chat.transform.Find("CommanderCanvas/Panel/Approval");
            Assert.That(approval.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Transcript").gameObject.activeInHierarchy, Is.False,
                "Approval preview replaces Chat scroll while pending.");
            var scroll = approval.Find("PreviewScroll").GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(TextOf(scroll.content), Is.EqualTo(chat.PendingActionPlan.Preview));
            Assert.That(TextOf(scroll.content), Does.Contain("after step 1"));
            Assert.That(approval.Find("ApprovePlan")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(approval.Find("RejectPlan")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(goals.Goals, Is.Empty, "Displaying or scrolling details cannot authorize gameplay.");
            Canvas.ForceUpdateCanvases();
            var panelCorners = new Vector3[4];
            var approveCorners = new Vector3[4];
            var rejectCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TextSubmission").gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/VoiceBar").gameObject.activeInHierarchy, Is.True);
            chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>().GetWorldCorners(panelCorners);
            approval.Find("ApprovePlan").GetComponent<RectTransform>().GetWorldCorners(approveCorners);
            approval.Find("RejectPlan").GetComponent<RectTransform>().GetWorldCorners(rejectCorners);
            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
            Assert.That(approveCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y));
            Assert.That(rejectCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y));
            Assert.That(composerCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y),
                "Approval must not push the fixed composer below the dock.");
            Assert.That(composerCorners[2].y, Is.LessThanOrEqualTo(approveCorners[0].y - 4),
                "Composer must remain below the approval decision controls.");
            yield return null;
            ForceFixtureRepaint();
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var approvalImage = ScreenCapture.CaptureScreenshotAsTexture();
            var directory = NewRenderDirectory("compound-approval");
            File.WriteAllBytes(Path.Combine(directory,
                $"editor-approval-actual-{approvalImage.width}x{approvalImage.height}.png"),
                approvalImage.EncodeToPNG());
            UnityEngine.Object.Destroy(approvalImage);
            yield return null;
            approval.Find("RejectPlan").GetComponent<Button>().onClick.Invoke();
            Assert.That(chat.PendingActionPlan, Is.Null);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Cancelled"));
        }

        [UnityTest]
        public IEnumerator ReviewedVoiceDraft_IsVisibleInExpandedDockAndDiscardDoesNotSubmit()
        {
            chat.SetVoiceProvider(new MockCommanderSpeechToTextProvider("train three spearmen"),
                new FixtureAudioCapture());
            chat.VoiceController.AutoSubmit = false;
            chat.enabled = true;
            chat.OpenCommander();
            chat.VoiceController.StartRecording();
            var voice = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!voice.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.VoiceState, Is.EqualTo(CommanderVoiceState.Preview));
            var review = chat.transform.Find("CommanderCanvas/Panel/VoiceReview");
            Assert.That(review?.gameObject.activeInHierarchy, Is.True);
            Assert.That(TextOf(review.Find("ReviewText")), Does.Contain("not sent"));
            Assert.That(review.Find("SendReviewed")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(review.Find("EditReviewed")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(review.Find("DiscardReviewed")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(goals.Goals, Is.Empty);
            review.Find("DiscardReviewed").GetComponent<Button>().onClick.Invoke();
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ApprovePlan_UsesExistingSingleUseAuthorityAndUpdatesLatestStatus()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}");
            var turn = chat.SubmitMessageAsync("build a House and train three Spearmen");
            provider.CompleteLatest(parsed);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.PendingActionPlan, Is.Not.Null);
            Assert.That(goals.Goals, Is.Empty);
            var approve = chat.transform.Find("CommanderCanvas/Panel/Approval/ApprovePlan").GetComponent<Button>();
            approve.onClick.Invoke();
            approve.onClick.Invoke();
            Assert.That(chat.PendingActionPlan, Is.Null);
            int admittedEffects = goals.Goals.Count;
            Assert.That(admittedEffects, Is.EqualTo(2), "The approved graph has two effects.");
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.Not.StartWith("Awaiting approval"));
            yield return null;
            approve.onClick.Invoke();
            Assert.That(goals.Goals, Has.Count.EqualTo(admittedEffects));
            Assert.That(provider.Attempts, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingWorkerCount_ShowsProminentClarificationWithoutGameplay()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Clarify\",\"message\":\"How many villagers?\","
                + "\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\","
                + "\"workers\":{\"state\":\"Idle\"},\"destination\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"}},"
                + "\"missingFields\":[\"Count\"]}");
            var turn = chat.SubmitMessageAsync("send idle villagers to gather wood");
            provider.CompleteLatest(parsed);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var question = chat.transform.Find("CommanderCanvas/Panel/PendingClarification");
            Assert.That(chat.PendingClarification, Is.Not.Null);
            Assert.That(question.gameObject.activeInHierarchy, Is.True);
            Assert.That(TextOf(question.Find("Question")), Does.Contain("How many villagers"));
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Needs clarification"));
            Assert.That(goals.Goals, Is.Empty);
            yield return new WaitForEndOfFrame();
            SaveCurrentFrame("pending-clarification");
        }

        [UnityTest]
        public IEnumerator ChatView_RendersPlayerAndCommanderAsSeparateBubbles()
        {
            chat.enabled = true;
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var bubbles = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport/BubbleContent");
            Assert.That(bubbles, Is.Not.Null);
            Assert.That(bubbles.childCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(bubbles.GetChild(bubbles.childCount - 2).name, Is.EqualTo("PlayerBubble"));
            Assert.That(bubbles.GetChild(bubbles.childCount - 1).name, Is.EqualTo("CommanderBubble"));
        }

        [UnityTest]
        public IEnumerator LatestQuestionStatus_DoesNotBorrowOlderWorkingGoal()
        {
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var status = chat.transform.Find("CommanderCanvas/Panel/LatestRequest");
            Assert.That(TextOf(status), Does.StartWith("Completed"));
            Assert.That(TextOf(status), Does.Not.Contain("Reach Age"));
        }

        [UnityTest]
        public IEnumerator NewerInformationQuestion_KeepsLatestStatusWhileOlderOrderGetsOwnTask()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var olderOrder = chat.SubmitMessageAsync("train three spearmen");
            Assert.That(provider.Attempts, Is.EqualTo(1));
            var question = chat.SubmitMessageAsync("strategy status");
            while (!question.IsCompleted) yield return null;
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Completed"));
            provider.CompleteLatest("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}");
            while (!olderOrder.IsCompleted) yield return null;
            yield return null;
            Assert.That(goals.Goals, Has.Count.EqualTo(1), "The older valid order keeps its own task.");
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Completed"), "The older order cannot impersonate the newer answer.");
        }

        [UnityTest]
        public IEnumerator OlderOrderTimeout_AfterNewerQuestionGivesComposerRetryInsteadOfHiddenControls()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var olderOrder = chat.SubmitMessageAsync("train three spearmen");
            Assert.That(provider.Attempts, Is.EqualTo(1));
            var question = chat.SubmitMessageAsync("strategy status");
            while (!question.IsCompleted) yield return null;
            Assert.That(olderOrder.IsCompleted, Is.False,
                "This fixture requires the newer question to finish before the older order times out.");
            while (!olderOrder.IsCompleted) yield return null;
            yield return null;

            Assert.That(goals.Goals, Is.Empty, "The timed-out order must not admit a goal.");
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Completed"), "The newer information answer stays latest.");
            var recovery = chat.transform.Find("CommanderCanvas/Panel/RequestRecovery");
            Assert.That(recovery.gameObject.activeSelf, Is.False,
                "Old-order recovery controls must not impersonate the newer question.");
            Assert.That(chat.DisplayedTranscript, Does.Contain("timed out"),
                "The old request must have actually reached its timeout branch. Transcript: " + chat.DisplayedTranscript);
            var bubbles = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport/BubbleContent");
            string reply = TextOf(bubbles.GetChild(bubbles.childCount - 1).Find("Message"));
            Assert.That(reply, Does.Contain("earlier order").IgnoreCase);
            Assert.That(reply, Does.Contain("composer").And.Contain("Send"));
            Assert.That(reply, Does.Not.Contain("Choose Retry, Edit, or Cancel"),
                "Do not promise recovery buttons that are not available for the older order.");

            chat.InputText = "train three spearmen";
            var retry = chat.SubmitCurrentInputAsync();
            yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(2),
                "The instruction names a route the player can actually use.");
            chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel")
                .GetComponent<Button>().onClick.Invoke();
            while (!retry.IsCompleted) yield return null;
        }

        [UnityTest]
        public IEnumerator CancelledRequest_LateProviderFaultCannotReplaceCancelledStatus()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var request = chat.SubmitMessageAsync("train three spearmen");
            Assert.That(provider.Attempts, Is.EqualTo(1));
            provider.FailLatest(new InvalidOperationException("late fixture fault"));
            chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel")
                .GetComponent<Button>().onClick.Invoke();
            while (!request.IsCompleted) yield return null;
            yield return null;
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Cancelled"));
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator OlderProviderFault_CannotReplaceNewerInformationAnswer()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var olderOrder = chat.SubmitMessageAsync("train three spearmen");
            var question = chat.SubmitMessageAsync("strategy status");
            while (!question.IsCompleted) yield return null;
            provider.FailLatest(new InvalidOperationException("old provider fixture fault"));
            while (!olderOrder.IsCompleted) yield return null;
            yield return null;
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Completed"), "An old fault cannot become the newer question's status.");
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EarlierAsyncInformationAnswer_IsIdentifiedAfterNewerQuestion()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var earlier = chat.SubmitMessageAsync("what is happening nearby?");
            Assert.That(provider.Attempts, Is.EqualTo(1));
            var newer = chat.SubmitMessageAsync("strategy status");
            while (!newer.IsCompleted) yield return null;
            provider.CompleteLatest("{\"outcome\":\"Answer\",\"message\":\"No nearby observations.\"}");
            while (!earlier.IsCompleted) yield return null;
            yield return null;
            var bubbles = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport/BubbleContent");
            Assert.That(TextOf(bubbles.GetChild(bubbles.childCount - 1).Find("Message")),
                Does.StartWith("Earlier question"));
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Completed"));
        }

        [UnityTest]
        public IEnumerator PopulatedChatView_StaysInsideScreenAndAboveFixedComposer()
        {
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var panel = chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>();
            var viewport = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport").GetComponent<RectTransform>();
            var composer = chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>();
            var panelCorners = new Vector3[4];
            var viewportCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            panel.GetWorldCorners(panelCorners);
            viewport.GetWorldCorners(viewportCorners);
            composer.GetWorldCorners(composerCorners);
            Assert.That(panelCorners[0].x, Is.GreaterThanOrEqualTo(0));
            Assert.That(panelCorners[2].x, Is.LessThanOrEqualTo(Screen.width + 1));
            Assert.That(panelCorners[0].y, Is.GreaterThanOrEqualTo(0));
            Assert.That(viewportCorners[0].y, Is.GreaterThanOrEqualTo(composerCorners[1].y + 4),
                "Chat scroll must end above the fixed composer.");
        }

        [UnityTest]
        public IEnumerator ExplicitRuntimeCapture_UsesActualPopulatedCommanderCanvas()
        {
            InstallRenderFixtureCamera();
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var capture = chat.GetType().GetMethod("RequestEvidenceScreenshot");
            var lastPath = chat.GetType().GetProperty("LastEvidenceScreenshotPath");
            Assert.That(capture, Is.Not.Null);
            Assert.That(lastPath, Is.Not.Null);
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/SettingsMenu/CaptureEvidence"), Is.Not.Null);
            Assert.That(capture.Invoke(chat, null), Is.EqualTo(true));
            yield return new WaitForEndOfFrame();
            yield return null;
            string path = (string)lastPath.GetValue(chat);
            Assert.That(path, Is.Not.Null.And.Not.Empty);
            Assert.That(chat.LastEvidenceScreenshotKind, Is.EqualTo("manual"));
            Assert.That(chat.LastEvidenceScreenshotWidth, Is.GreaterThan(0));
            Assert.That(chat.LastEvidenceScreenshotHeight, Is.GreaterThan(0));
            Assert.That(File.Exists(path), Is.True);
            var signature = File.ReadAllBytes(path);
            Assert.That(signature.Length, Is.GreaterThan(8));
            Assert.That(signature.Take(8), Is.EqualTo(new byte[] { 137,80,78,71,13,10,26,10 }));
            Assert.That(provider.Attempts, Is.Zero, "A screenshot must never dispatch a semantic request.");
            File.Delete(path); // only this test's generated screenshot
        }

        [UnityTest]
        public IEnumerator ExplicitOfflineFixture_CapturesPendingApprovalTasksAndChat()
        {
            InstallRenderFixtureCamera();
            chat.enabled = true;
            foreach (var node in simulation.MapData.GetAllResourceNodes().ToArray())
            { simulation.MapData.ClearResourceTile(node.Id); simulation.MapData.RemoveResourceNode(node.Id); }
            for (int tx = 0; tx < simulation.MapData.Width; tx++)
                for (int tz = 0; tz < simulation.MapData.Height; tz++)
                { simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                  simulation.MapData.ForestDensity[tx, tz] = 0;
                  simulation.FogOfWar.SetVisible(0, tx, tz); }
            simulation.MapData.ComputeHoleMap();
            simulation.FogOfWar.SetVisionCheat(0, true);
            var basePosition = simulation.MapData.BasePositions[0];
            int x = basePosition.x, z = basePosition.y;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
            var worker = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 6, z + 6),
                Fixed32.One, Fixed32.One, Fixed32.One);
            worker.IsVillager = true; worker.UnitType = 0; worker.CurrentHealth = worker.MaxHealth = 100;
            var stock = simulation.ResourceManager.GetPlayerResources(0);
            stock.Food = stock.Wood = stock.Gold = stock.Stone = 2000;
            var nested = typeof(CommanderChatUI).GetNestedType("OfflineEvidenceProvider",
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(nested, Is.Not.Null);
            var offline = (ICommanderAIProvider)Activator.CreateInstance(nested, true);
            chat.Initialize(offline, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            var method = chat.GetType().GetMethod("CaptureOfflineFixtureOnce",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            yield return (IEnumerator)method.Invoke(chat, null);
            Assert.That(goals.Goals, Is.Not.Empty, chat.DisplayedTranscript);
            Assert.That(chat.PendingActionPlan, Is.Null);
            var captures = chat.GetType().GetProperty("OfflineEvidenceCapturePaths");
            Assert.That(captures, Is.Not.Null);
            var paths = (System.Collections.Generic.IReadOnlyList<string>)captures.GetValue(chat);
            Assert.That(paths.Count, Is.EqualTo(3));
            foreach (string path in paths)
            {
                Assert.That(File.Exists(path), Is.True, path);
                byte[] png = File.ReadAllBytes(path);
                Assert.That(png.Take(8), Is.EqualTo(new byte[] { 137,80,78,71,13,10,26,10 }));
                File.Delete(path); // only this test's generated image
            }
        }

        [UnityTest]
        public IEnumerator CaptureActualPopulatedEditorViews_AtRequestedDesktopSizes()
        {
            InstallRenderFixtureCamera();
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            var directory = NewRenderDirectory("populated-chat-tasks");
            Directory.CreateDirectory(directory);
            int activeCommanderCanvases = 0;
            foreach (var ui in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                if (ui.gameObject.activeInHierarchy && ui.transform.Find("CommanderCanvas")?.gameObject.activeInHierarchy == true)
                    activeCommanderCanvases++;
            Debug.Log($"[Commander Phase5C render] active Commander canvases={activeCommanderCanvases}; own compact active={chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy}");
            Assert.That(activeCommanderCanvases, Is.EqualTo(1));
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy, Is.False);
            foreach (int width in new[] { 1280, 1920 })
            {
                int height = width == 1280 ? 720 : 1080;
                Screen.SetResolution(width, height, false);
                yield return null;
                yield return null;
                ForceFixtureRepaint();
                Canvas.ForceUpdateCanvases();
                var launcher = GameObject.Find("CompactCommander");
                Debug.Log($"[Commander Phase5C render] active launcher object={(launcher == null ? "none" : launcher.transform.parent.name + "/" + launcher.name)}; own launcher={chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy}");
                yield return new WaitForEndOfFrame();
                var chatImage = ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(chatImage, Is.Not.Null);
                File.WriteAllBytes(Path.Combine(directory,
                    $"editor-chat-requested-{width}x{height}-actual-{chatImage.width}x{chatImage.height}.png"),
                    chatImage.EncodeToPNG());
                Debug.Log($"[Commander Phase5C render] requested={width}x{height}; captured={chatImage.width}x{chatImage.height}; Chat active goal and local reply");
                UnityEngine.Object.Destroy(chatImage);
                chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
                yield return null;
                ForceFixtureRepaint();
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                var tasksImage = ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(tasksImage, Is.Not.Null);
                File.WriteAllBytes(Path.Combine(directory,
                    $"editor-tasks-requested-{width}x{height}-actual-{tasksImage.width}x{tasksImage.height}.png"),
                    tasksImage.EncodeToPNG());
                Debug.Log($"[Commander Phase5C render] requested={width}x{height}; captured={tasksImage.width}x{tasksImage.height}; Tasks active goal and visible Cancel");
                UnityEngine.Object.Destroy(tasksImage);
                chat.transform.Find("CommanderCanvas/Panel/Views/ChatTab").GetComponent<Button>().onClick.Invoke();
                yield return null;
            }
        }

        private void InstallRenderFixtureCamera()
        {
            renderCameraObject = new GameObject("Phase5CRenderFixtureCamera");
            var camera = renderCameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depth = 100;
        }

        private void ForceFixtureRepaint()
        {
            foreach (var graphic in chat.GetComponentsInChildren<Graphic>(true))
                graphic.SetAllDirty();
        }

        private static string NewRenderDirectory(string state)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Docs/CommanderPhase5C/evidence/task-5-render",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + state));
            Directory.CreateDirectory(directory);
            Debug.Log("[Commander Phase5C render] evidence directory=" + directory);
            return directory;
        }

        private static void SaveCurrentFrame(string state)
        {
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            var directory = NewRenderDirectory(state);
            File.WriteAllBytes(Path.Combine(directory,
                $"editor-{state}-actual-{screenshot.width}x{screenshot.height}.png"),
                screenshot.EncodeToPNG());
            UnityEngine.Object.Destroy(screenshot);
        }

        [UnityTest]
        public IEnumerator TimedOutLateResponse_CannotAdmitAndDoubleRetryStartsOnce()
        {
            chat.enabled = true;
            chat.OpenCommander();
            chat.InputText = "train 10 spearmen";
            var first = chat.SubmitCurrentInputAsync();
            while (!first.IsCompleted) yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(1));
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"), "after initial timeout");
            provider.CompleteLatest("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}");
            yield return null;
            Assert.That(goals.Goals, Is.Empty);
            var retry = chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Retry").GetComponent<Button>();
            retry.onClick.Invoke();
            retry.onClick.Invoke();
            yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(2));
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"), "after retry begins");
            var cancel = chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel").GetComponent<Button>();
            cancel.onClick.Invoke();
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"), "immediately after Cancel");
            provider.CompleteLatest("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}");
            yield return null;
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"));
        }

        private static string TextOf(Transform target)
        {
            if (target == null) return string.Empty;
            foreach (var component in target.GetComponents<Component>())
                if (component.GetType().Name == "TextMeshProUGUI")
                    return (string)component.GetType().GetProperty("text").GetValue(component);
            return string.Empty;
        }

        private sealed class SlowProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            public int Attempts { get; private set; }
            private TaskCompletionSource<CommanderSemanticResult> latest;
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
                => Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure, "offline fixture"));
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request, CancellationToken token)
            {
                Attempts++;
                latest = new TaskCompletionSource<CommanderSemanticResult>();
                return latest.Task;
            }
            public void CompleteLatest(string json) => latest?.TrySetResult(CommanderSemanticJson.Parse(json));
            public void CompleteLatest(CommanderSemanticResult result) => latest?.TrySetResult(result);
            public void FailLatest(Exception error) => latest?.TrySetException(error);
        }

        private sealed class FixtureAudioCapture : ICommanderAudioCapture
        {
            public bool IsRecording { get; private set; }
            public string CurrentDevice => "FixtureMic";
            public bool StartRecording(string deviceName = null, float maxDurationSeconds = 15f)
            { IsRecording = true; return true; }
            public CommanderAudioData StopRecording()
            { IsRecording = false; return new CommanderAudioData(new float[16000], 16000, 1); }
            public void CancelRecording() => IsRecording = false;
            public void Dispose() => IsRecording = false;
        }
    }
}
```

### `Assets/Tests/PlayMode/CommanderPhase5CUIPlayModeTests.cs.meta`

```meta
fileFormatVersion: 2
guid: 976a282d0609a7148b2c6ba73dbcb07b
```

## Hash-only changed asset boundary

`Open Empires/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` is modified and may be runtime-relevant. Its body/atlas diff is intentionally omitted: SHA-256 `51949f49313166b97b6cec77d854a48d1a67320177b4210e61172d4869934773`, 535039 bytes. Task 7 should compare a preserved pre-Task7 baseline hash with the post-Task7 hash and inspect the change before attributing purpose or runtime effect.
