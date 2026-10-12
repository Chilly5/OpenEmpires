# Task4 fix round1 scoped review package
HEAD unchanged4c9de96adbf6342fbe9e1490fa9b898a0f99caf6; no commits. Delta from prior reviewed source. GraphAdmission.cs baseline reconstructed from clean HEAD after omission; all other snapshots pre-edit.

## Assets/Scripts/AI/Commander/CommanderGoal.cs
Prior-reviewed SHA256:F6229C4DA01C9ADF5377B3C24873A33F2AF2F58AA3E54A4ACC035ADB0B4C9524; current SHA256:7A01CF916889B72E23DB1EC336A6044B9B3E4F873C695D6606100093729AE57D
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs"
index d911256..76ab9ce 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs"	
@@ -1,14 +1,15 @@
 using System;
 using System.Collections.Generic;
+using UnityEngine;
 
 namespace OpenEmpires
 {
     public enum CommanderGoalType
     {
         EnsureUnitCount,
         BuildStructure,
         ResourceAllocation,
         ReachAge,
         CapabilityAction,
         AllocateWorkers,
         WatchFutureUnits
@@ -155,25 +156,25 @@ namespace OpenEmpires
             foreach (CommanderGoal required in requiredGoals)
                 if (required != null && !dependencies.Contains(required)) dependencies.Add(required);
         }
         internal void ReleaseRuntimeReferences()
         {
             RuntimeOwner=null;RequestAuthority=null;dependencies.Clear();PreparationAllocators.Clear();FrozenWorkerIds=null;
             if(this is CommanderCapabilityGoal capability)
             {capability.TargetBinding=null;capability.ResultSourceGoal=null;capability.IssuedCommand=null;
                 capability.NativeMovementSlots=null;capability.NativeMovementEndpoints=null;
                 capability.NativeMovementProcessed=null;capability.NativeMovementResults=null;
                 capability.NativeMovementScatter=null;}
             if(this is EnsureUnitCountGoal units){units.RequiredProducerGoal=null;units.TrackedTrainingOrders.Clear();units.BaselineUnitIds.Clear();}
-            if(this is BuildStructureGoal build)build.PendingPlacementCommand=null;
+            if(this is BuildStructureGoal build){build.PendingPlacementCommand=null;build.DynamicPlannedSiteSet=null;}
             if(this is ReachAgeGoal age)age.PendingAgeUpCommand=null;
             if(this is AllocateWorkersGoal boundAllocation){boundAllocation.ResultSourceGoal=null;boundAllocation.RecoveryCandidateOffsets.Clear();boundAllocation.RecoverySearchExhausted=false;}
             if(this is WatchFutureUnitsGoal future)future.ReleaseObservations();
         }
     }
 
     public sealed class BuildStructureGoal : CommanderGoal
     {
         public BuildingType StructureType { get; }
         public int Count { get; }
         public int TargetTotal { get; internal set; }
         public CommanderSemanticAnchorSelector? PlacementAnchorSelector { get; }
@@ -185,24 +186,25 @@ namespace OpenEmpires
         public int? PlacedTileX { get; internal set; }
         public int? PlacedTileZ { get; internal set; }
         public int PlacedBuildingId { get; internal set; } = -1;
         public CommanderPlacementBlocker PlacementBlocker { get; internal set; }
         internal int PlacementIssuedTick { get; set; } = -1;
         internal int PlacementIssuedSimulationTick { get; set; } = -1;
         internal ICommand PendingPlacementCommand { get; set; }
         // Result-binding state is captured by the game-side manager, never by the provider.
         internal bool HasResultConsumer { get; set; }
         internal readonly HashSet<int> BaselineBuildingIds = new HashSet<int>();
         internal readonly List<int> AttributedBuildingIds = new List<int>();
         internal CommanderBoundLocation DynamicLocation { get; set; }
+        internal IReadOnlyList<Vector2Int> DynamicPlannedSiteSet { get; set; }
         internal IReadOnlyList<int> ResultBuildingIds { get; private set; } = Array.Empty<int>();
         internal int ResultCaptureTick { get; private set; } = -1;
         public bool HasSemanticPlacement => PlacementAnchorSelector.HasValue || DynamicLocation != null;
 
         internal void CaptureBuildingResult(IReadOnlyList<int> buildingIds, int tick)
         {
             ResultBuildingIds = buildingIds ?? Array.Empty<int>();
             ResultCaptureTick = tick;
         }
 
         public BuildStructureGoal(int playerId, BuildingType structureType, int count = 1,
             int maxDurationTicks = 36000,

## Assets/Scripts/AI/Commander/CommanderPlanner.cs
Prior-reviewed SHA256:DAA11C65CE2B3B315A27CE7B8CED9E195F5F059C301D82E4B342AE0F13EE43E1; current SHA256:D519024E297442CB410582D635ACFB0E166ECE7FA117CA61BAB524A4865DAD52
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.cs"
index c485984..c06b8c7 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.cs"	
@@ -66,41 +66,41 @@ namespace OpenEmpires
             if (goal is EnsureUnitCountGoal units) return PlanUnits(units, currentTick);
             if (goal is BuildStructureGoal building) return PlanStructure(building, currentTick);
             if (goal is ResourceAllocationGoal resource) return PlanAllocation(resource, currentTick);
             if (goal is ReachAgeGoal reachAge) return PlanReachAge(reachAge, currentTick);
             if (goal is CommanderCapabilityGoal capability) return PlanCapability(capability, currentTick);
             return new CommanderPlan(CommanderGoalStatus.Failed, "Unknown goal type.", 0, 0);
         }
 
         private CommanderPlan PlanCapability(CommanderCapabilityGoal goal, int currentTick)
         {
             if (goal.Action.ActionType == CommanderCapabilityActionType.DiscoverEnemy)
                 return PlanDiscovery(goal, currentTick);
-            if (goal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits
-                || goal.Action.ActionType == CommanderCapabilityActionType.UngarrisonBuilding)
-                return PlanShelterCapability(goal, currentTick);
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
+            if (goal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits
+                || goal.Action.ActionType == CommanderCapabilityActionType.UngarrisonBuilding)
+                return PlanShelterCapability(goal, currentTick);
             if (goal.DynamicMoveLocation != null)
             {
                 if (goal.CommandIssued)
                 {
                     ResourceNodeData source = simulation.MapData.GetResourceNode(goal.DynamicMoveResourceId);
                     if (!goal.DynamicMoveLocation.AllowsResource(simulation, goal.PlayerId, source))
                         return new CommanderPlan(CommanderGoalStatus.Blocked,
                             "The exact selected woodline source or owned Town Center was lost; no substitute move is authorized.", 0, 0);
                     return ObserveCapability(goal, currentTick);
                 }
                 return PlanDynamicWorkerMove(goal, currentTick);
             }

## Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs
Prior-reviewed SHA256:E56883BBF3391DEA699BE9DC61E301B21CC7F787AE262E88ACFA54FEB58B4CD8; current SHA256:D611AE3B0E3DC2A727431647602595D5968E41EADC283F7729706A5FF48ED746
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanner.DynamicPlacement.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanner.DynamicPlacement.cs"
index 6b634e4..a1bef7c 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanner.DynamicPlacement.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderPlanner.DynamicPlacement.cs"	
@@ -1,19 +1,21 @@
 using System.Collections.Generic;
 using UnityEngine;
 
 namespace OpenEmpires
 {
     internal sealed partial class CommanderPlanner
     {
+        private const int MaximumDynamicSiteSearchNodes = 8192;
+
         // Called only after ordinary age/cost/prerequisite checks in PlanBuilding.
         // The anchor is a frozen game-side identity, never a provider tile or runtime ID.
         private CommanderPlan PlanDynamicSemanticBuilding(BuildStructureGoal goal,
             BuildingType type, int currentTick, int owned, int queued)
         {
             if (goal.ConstructionForbidden)
                 return new CommanderPlan(CommanderGoalStatus.Blocked,
                     "This request forbids construction.", owned, queued);
             CommanderBoundLocation location = goal.DynamicLocation;
             if (location == null || !location.TryResolve(simulation, goal.PlayerId,
                 out int anchorX, out int anchorZ, out int anchorWidth, out int anchorHeight))
             {
@@ -29,68 +31,104 @@ namespace OpenEmpires
                     $"No eligible owned villager is available to build {type}.", owned, queued);
             }
 
             GetFootprint(type, out int width, out int height);
             IReadOnlyList<Vector2Int> candidates = CommanderSemanticPlacementCandidates.Generate(
                 anchorX, anchorZ, anchorWidth, anchorHeight, width, height,
                 simulation.MapData.Width, simulation.MapData.Height,
                 location.Relation, location.ClearGapTiles);
             int border = type == BuildingType.Farm ? 0 : 1;
             // Before placing the first building, require enough mutually legal sites
             // for the entire repeated request. Later external occupancy/authority
             // changes may still block honestly; a known impossible prefix is never issued.
-            if (goal.Count > 1 && goal.AttributedBuildingIds.Count == 0
-                && !HasFullDynamicSiteSet(goal, type, currentTick, candidates,
-                    width, height, border))
+            IReadOnlyList<Vector2Int> planned = null;
+            bool exhausted = false;
+            if (goal.Count > 1 && goal.DynamicPlannedSiteSet == null
+                && !TrySelectFullDynamicSiteSet(goal, type, currentTick, candidates,
+                    width, height, border, out planned, out exhausted))
             {
                 goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
                 return new CommanderPlan(CommanderGoalStatus.Blocked,
-                    $"Only a partial set of distinct legal {type} sites is available near the exact anchor; "
+                    (exhausted ? "The bounded compatible-site search budget was exhausted; "
+                        : $"Only a partial set of distinct legal {type} sites is available near the exact anchor; ")
                     + $"none of the requested {goal.Count} placements was started.", owned, queued);
             }
-            for (int i = 0; i < candidates.Count; i++)
+            if (goal.Count > 1 && goal.DynamicPlannedSiteSet == null)
+                goal.DynamicPlannedSiteSet = planned;
+            IReadOnlyList<Vector2Int> placementCandidates = goal.DynamicPlannedSiteSet != null
+                ? new[] { goal.DynamicPlannedSiteSet[goal.AttributedBuildingIds.Count] } : candidates;
+            for (int i = 0; i < placementCandidates.Count; i++)
             {
-                Vector2Int tile = candidates[i];
+                Vector2Int tile = placementCandidates[i];
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
-                $"No visible, buildable, reachable location satisfies the exact {type} "
-                + "anchor and bounded relation.", owned, queued);
+                goal.DynamicPlannedSiteSet != null
+                    ? "A preflight-selected exact site became unavailable; no alternate placement was started."
+                    : $"No visible, buildable, reachable location satisfies the exact {type} "
+                        + "anchor and bounded relation.", owned, queued);
         }
 
-        private bool HasFullDynamicSiteSet(BuildStructureGoal goal, BuildingType type,
+        private bool TrySelectFullDynamicSiteSet(BuildStructureGoal goal, BuildingType type,
             int currentTick, IReadOnlyList<Vector2Int> candidates,
-            int width, int height, int border)
+            int width, int height, int border, out IReadOnlyList<Vector2Int> planned,
+            out bool exhausted)
         {
-            var selected = new List<Vector2Int>(goal.Count);
-            for (int i = 0; i < candidates.Count && selected.Count < goal.Count; i++)
+            planned = null;
+            exhausted = false;
+            bool budgetExceeded = false;
+            var legal = new List<Vector2Int>();
+            for (int i = 0; i < candidates.Count; i++)
             {
                 Vector2Int tile = candidates[i];
                 if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y,
                     width, height, border, type)) continue;
-                bool conflicts = false;
-                for (int j = 0; j < selected.Count; j++)
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
                 {
-                    Vector2Int prior = selected[j];
-                    if (tile.x - border < prior.x + width && tile.x + width + border > prior.x
-                        && tile.y - border < prior.y + height && tile.y + height + border > prior.y)
-                    { conflicts = true; break; }
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
                 }
-                if (conflicts || FindReachableSemanticBuilder(goal, tile,
-                    width, height, currentTick) == null) continue;
-                selected.Add(tile);
+                return false;
             }
-            return selected.Count == goal.Count;
+            if (!Search(0))
+            { exhausted = budgetExceeded; return false; }
+            planned = selected.ToArray();
+            return true;
         }
     }
 }

## Assets/Scripts/Core/GameSimulation.cs
Prior-reviewed SHA256:5EA5528B8B062424CAEC3F7C9013D3050744EBC485108C9BF4FAF13E6E6898CA; current SHA256:A8AE196F25DAFF462A59E42D1515CB1C9E4B00F8F4D62C927651438B9DC54877
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\Core\\GameSimulation.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Core\\GameSimulation.cs"
index df3f360..2025161 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\Core\\GameSimulation.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Core\\GameSimulation.cs"	
@@ -831,24 +831,25 @@ namespace OpenEmpires
             this.config = config;
             this.playerCount = playerCount;
             int mapSize = SimulationConfig.GetMapSize(playerCount);
             UnitRegistry = new UnitRegistry();
             BuildingRegistry = new BuildingRegistry();
             ResourceManager = new ResourceManager();
             MapData = new MapData(mapSize, mapSize);
             var (tiles, heights, basePositions, forestDensity) = MapGenerator.Generate(mapSize, mapSize,
                 config.MapSeed, config.WaterThreshold, playerCount, teamAssignments, config.MapType);
             MapData.ApplyGenerationResult(tiles, heights, forestDensity);
             MapData.BasePositions = basePositions;
             CommandBuffer = new CommandBuffer();
+            CommandBuffer.CommandEnqueued += ObserveLocalGarrisonEnqueue;
             AiCommandBuffer = new CommandBuffer();
             movementSystem = new UnitMovementSystem();
             separationSystem = new UnitSeparationSystem();
             combatSystem = new UnitCombatSystem();
             spatialGrid = new SpatialGrid(Fixed32.FromInt(4));
             towerCombatSystem = new TowerCombatSystem();
             gatheringSystem = new ResourceGatheringSystem();
             fogOfWarSystem = new FogOfWarSystem();
             trainingSystem = new BuildingTrainingSystem();
             constructionSystem = new BuildingConstructionSystem();
             repairSystem = new BuildingRepairSystem();
             towerUpgradeSystem = new TowerUpgradeSystem();

## Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs
Prior-reviewed SHA256:8AF860030765A4876F52F85AADA38FCC2D3C15C7F47AFDDF41344FC79FD622A0; current SHA256:9990FF6290AE5B9CEFAE07CA974208EBA0021C146265FF94A4CDCD2A9E2BC920
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderPhase5ADynamicRuntimeTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase5ADynamicRuntimeTests.cs"
index 4becfca..5bd16c2 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderPhase5ADynamicRuntimeTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase5ADynamicRuntimeTests.cs"	
@@ -320,43 +320,189 @@ namespace OpenEmpires.Tests
                 + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                 + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":20},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
             var parsed = CommanderSemanticJson.Parse(json);
             Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
             var candidate = f.Manager.PrepareActionPlan(parsed, "build twenty Houses near my TC", 1);
             var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
             f.Manager.Tick(0);
             Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
             Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<PlaceBuildingCommand>(), Is.Empty,
                 "A statically impossible full site count must not execute a legal prefix.");
         }
 
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
         [Test]
         public void ExactTargetGarrison_FullRequestedShelterMustNotDispatchToAnotherShelter()
         {
             using var f = new Fixture(2);
             int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
             var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
             var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
             requested.GarrisonCapacity = 1;
             f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
             requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
             Assert.That(requested.GarrisonedUnitIds, Does.Contain(f.Workers[0].Id));
             ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
             Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
             f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
             Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.Not.EqualTo(other.Id),
                 "An exact-target request must not dispatch to an unrelated shelter when the requested one fills.");
             Assert.That(other.GarrisonedUnitIds.Contains(f.Workers[1].Id), Is.False);
         }
 
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
         [Test]
         public void ExactTargetGarrison_RelayOriginRemainsExactAndAmbiguousReplayFailsClosed()
         {
             using var f = new Fixture(2);
             int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
             var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
             var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
             requested.GarrisonCapacity = 1;
             f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
             requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
             ICommand original = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
             Assert.That(f.Sim.RegisterExactGarrisonCommand(original), Is.True);

## Assets/Scripts/AI/Commander/CommanderPlanner.Garrison.cs
Prior-reviewed SHA256:142056AAFE49F0346EB2231BCE3533CB099D6C0244B0E88FBE53DD3D2CACA6A4; current SHA256:EDC5BC39CEEC1D237E2C7E5EC280417A3612FAD58159D2F1F8B1AE884998E73A
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.Garrison.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.Garrison.cs"
index 88e0cb1..124696e 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.Garrison.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderPlanner.Garrison.cs"	
@@ -26,33 +26,64 @@ namespace OpenEmpires
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
-                var executor = new CommanderCapabilityExecutor(simulation);
-                var eligible = simulation.UnitRegistry.GetAllUnits()
-                    .Where(unit => unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
-                        && unit.State != UnitState.Dead && !unit.IsSheep
-                    && executor.MatchesSelector(unit, goal.Action.UnitSelector))
-                    .OrderBy(unit => unit.Id);
-                goal.GarrisonCandidateIds = eligible
-                    .Take(goal.Action.UnitSelector.AllMatching ? MaxGarrisonAllMatching + 1
-                        : goal.Action.UnitSelector.Count).Select(unit => unit.Id).ToArray();
+                if (goal.ResultSourceGoal != null || goal.RequiresResultBinding)
+                {
+                    if (!(goal.ResultSourceGoal is EnsureUnitCountGoal producer)
+                        || producer.RequestedUnitType != 0 || !producer.IsExplicitNewProduction
+                        || producer.RequiredNewProductionCount != goal.Action.UnitSelector.Count
+                        || goal.Action.UnitSelector.AllMatching
+                        || !(goal.Action.UnitSelector.Kind == CommanderUnitSelectorKind.Villagers
+                            || goal.Action.UnitSelector.Kind == CommanderUnitSelectorKind.UnitType
+                                && goal.Action.UnitSelector.UnitType == 0))
+                        return new CommanderPlan(CommanderGoalStatus.Blocked,
+                            "The exact produced villager result is incompatible with this shelter request; no selector fallback is allowed.", 0, 0);
+                    if (producer.ResultCaptureTick < 0 || producer.ResultUnitIds == null
+                        || producer.ResultUnitIds.Count != producer.RequiredNewProductionCount)
+                        return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
+                            "Waiting for the exact produced villagers; no selector fallback is allowed.", 0, 0);
+                    int[] resultIds = producer.ResultUnitIds.ToArray();
+                    if (resultIds.Distinct().Count() != resultIds.Length)
+                        return new CommanderPlan(CommanderGoalStatus.Blocked,
+                            "The produced villager result contains duplicate identities; no shelter order was issued.", 0, 0);
+                    if (resultIds.Any(id =>
+                    {
+                        UnitData unit = simulation.UnitRegistry.GetUnit(id);
+                        return unit == null || !unit.IsVillager || unit.IsSheep;
+                    }))
+                        return new CommanderPlan(CommanderGoalStatus.Blocked,
+                            "The exact producer result is not a current villager set; no substitute was selected.", 0, 0);
+                    goal.GarrisonCandidateIds = resultIds;
+                }
+                else
+                {
+                    var executor = new CommanderCapabilityExecutor(simulation);
+                    var eligible = simulation.UnitRegistry.GetAllUnits()
+                        .Where(unit => unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
+                            && unit.State != UnitState.Dead && !unit.IsSheep
+                        && executor.MatchesSelector(unit, goal.Action.UnitSelector))
+                        .OrderBy(unit => unit.Id);
+                    goal.GarrisonCandidateIds = eligible
+                        .Take(goal.Action.UnitSelector.AllMatching ? MaxGarrisonAllMatching + 1
+                            : goal.Action.UnitSelector.Count).Select(unit => unit.Id).ToArray();
+                }
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

## Assets/Scripts/Core/GameSimulation.ExactGarrison.cs
Prior-reviewed SHA256:9034145F4DB30E853FBAB68DAF333FD52AF0EDDC803E309E83A0F299CF99E5DF; current SHA256:2234D8200A0C0D582B7BC2021C80A5F577792DC8D3958517F456976EACAB3EC0
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\Core\\GameSimulation.ExactGarrison.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Core\\GameSimulation.ExactGarrison.cs"
index 04124c1..b495f15 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-4-fix1-baseline\\Assets\\Scripts\\Core\\GameSimulation.ExactGarrison.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\Core\\GameSimulation.ExactGarrison.cs"	
@@ -9,100 +9,117 @@ namespace OpenEmpires
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
+        private readonly List<ICommand> localHumanGarrisonOriginals = new List<ICommand>();
         private readonly Dictionary<int, ExactGarrisonOrder> activeExactGarrison =
             new Dictionary<int, ExactGarrisonOrder>();
 
+        private void ObserveLocalGarrisonEnqueue(ICommand command, CommandEnqueueSource source)
+        {
+            if (source == CommandEnqueueSource.Human && command is GarrisonCommand
+                && localHumanGarrisonOriginals.Count < MaxExactGarrisonSubjects)
+                localHumanGarrisonOriginals.Add(command);
+        }
+
+        private bool ConsumeProvenLocalHumanGarrison(ICommand original)
+        {
+            for (int i = 0; i < localHumanGarrisonOriginals.Count; i++)
+                if (ReferenceEquals(localHumanGarrisonOriginals[i], original))
+                {
+                    localHumanGarrisonOriginals.RemoveAt(i);
+                    return true;
+                }
+            return false;
+        }
+
         internal bool RegisterExactGarrisonCommand(ICommand original)
         {
             if (!(original is GarrisonCommand command) || command.UnitIds == null
                 || command.UnitIds.Length == 0 || command.UnitIds.Length > MaxExactGarrisonSubjects
                 || command.TargetBuildingId < 0)
                 return false;
-            PruneExpiredPendingExactGarrison();
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
 
-        private void PruneExpiredPendingExactGarrison()
-        {
-            pendingExactGarrison.RemoveAll(order => currentTick > order.ExpiresTick);
-        }
-
         // A reconstructed relay order with no unique verified original cannot
-        // inherit the opt-in guard. Suppress every identical candidate in this
-        // tick: permitting one would allow an unauthorized shelter fallback.
+        // inherit the opt-in guard. Retain expired signatures as bounded local
+        // tombstones: a delayed original or replay must never become ordinary
+        // Garrison just because time passed. The 200-subject admission cap also
+        // bounds tombstones; at capacity new exact requests fail closed.
         private bool RejectUnattributedExactGarrison(ICommand command, ICommand verifiedOriginal)
         {
-            if (verifiedOriginal != null || !(command is GarrisonCommand garrison)) return false;
-            PruneExpiredPendingExactGarrison();
+            if (!(command is GarrisonCommand garrison)) return false;
+            bool provenHuman = ConsumeProvenLocalHumanGarrison(verifiedOriginal ?? command);
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
-                pending.ExpiresTick = currentTick;
+                if (provenHuman) return false;
+                pending.ExpiresTick = Math.Min(pending.ExpiresTick, currentTick - 1);
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
-            PruneExpiredPendingExactGarrison();
             int[] subjects = CommandBuffer.GetSubjectUnitIds(command);
             if (subjects == null) return true;
             ExactGarrisonOrder exact = null;
             for (int i = 0; i < pendingExactGarrison.Count; i++)
                 if (ReferenceEquals(pendingExactGarrison[i].Original, original))
                 {
                     exact = pendingExactGarrison[i];
-                    pendingExactGarrison.RemoveAt(i);
+                    if (currentTick <= exact.ExpiresTick)
+                        pendingExactGarrison.RemoveAt(i);
                     break;
                 }
             if (exact != null)
             {
+                if (currentTick > exact.ExpiresTick) return false;
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


## Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs
Baseline reconstructed after omission from clean HEAD; Git blob verified07b3acdd90089a4aab5a5a35d03c7e1aa5851c4b; SHA25640D54C94ECB8DBD36621B55356D29FB61619BF8315DA17BFB92E2BB7A4FF8A4E; current E82E1EBFC7F155D08A9ABFECCA311FE356B2C193E718092259763EF0D6BA763A
diff --git a/Docs/CommanderPhase5C/evidence/task-4-fix1-baseline/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs
index 07b3acd..dc10efa 100644
--- a/Docs/CommanderPhase5C/evidence/task-4-fix1-baseline/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs
+++ b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs
@@ -169,24 +169,32 @@ namespace OpenEmpires
                         || produced.UnitType != 0 || !produced.NewProductionCount.HasValue
                         || workers.Allocation.Mode != CommanderWorkerAllocationMode.SelectedCount
                         || workers.Allocation.CountMode != CommanderWorkerCountMode.Exact
                         || workers.Allocation.Count != produced.NewProductionCount.Value)
                         return false;
                     continue;
                 }
                 if (!(node.Intent is CapabilityActionIntent action)) return false;
 
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
                     if (action.ActionType != CommanderCapabilityActionType.SetRallyPoint)
                         return false;
                     if (action.StructureType.HasValue && action.StructureType.Value != build.StructureType)
                         return false;
                 }
                 else return false;
             }
             return true;
         }
