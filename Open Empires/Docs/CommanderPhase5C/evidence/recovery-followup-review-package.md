# Narrow recovery follow-up frozen review package

No commits. Baseline is the pre-follow-up test snapshot captured by the implementer.
Only the economy test file changed for this follow-up. Production search was already
progressive; the focused cases characterize it, not a new production fix.

The mechanical diff and relevant unchanged production context follow. No broad
Phase5C gate, old Task3 movement review or full regression is part of this package.
diff --git a/Docs/CommanderPhase5C/evidence/recovery-followup-baseline/CommanderEconomyClarificationTests.Execution.cs b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs
index 0be4acd..02f2c81 100644
--- a/Docs/CommanderPhase5C/evidence/recovery-followup-baseline/CommanderEconomyClarificationTests.Execution.cs
+++ b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs
@@ -312,24 +312,126 @@ namespace OpenEmpires.Tests
             using (var f = new EconomyFixture())
             {
                 f.Node(ResourceType.Food, 20); var worker = f.Worker(0); f.Worker(1);
                 var goal = f.Submit("SelectedCount", "Exact", "1", "Idle", "", "Food", "Any");
                 f.Manager.Tick(0); int selectedId = Subjects(f.Commands()).Single();
                 f.Simulation.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { selectedId })); f.Commands();
                 Assert.That(f.Manager.GetWorkerReservation(selectedId), Is.Null);
                 f.Manager.Tick(15); Assert.That(f.Commands(), Is.Empty);
                 f.Manager.Tick(915); Assert.That(f.Commands(), Is.Empty); Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
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
         {
             private readonly SimulationConfig config;
             internal readonly GameSimulation Simulation;
             internal readonly CommanderGoalManager Manager;
             internal readonly Vector2Int Base;
             internal EconomyFixture()
             {
                 config = ScriptableObject.CreateInstance<SimulationConfig>();
                 Simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
@@ -362,27 +464,38 @@ namespace OpenEmpires.Tests
             }
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
             internal CommanderGoal SubmitWithAmount(params string[] values)
+                => SubmitWithAmount(100, values);
+            internal CommanderGoal SubmitWithAmount(int amount, params string[] values)
             {
                 string node = AllocationNode(values);
                 node = node.Substring(0, node.Length - 1)
-                    + ",\"resourceAmount\":100,\"resourceAmountMode\":\"AdditionalGathered\"}";
+                    + ",\"resourceAmount\":" + amount + ",\"resourceAmountMode\":\"AdditionalGathered\"}";
                 var parsed = CommanderSemanticJson.Parse(Request(node));
                 Assert.That(parsed.IsValid, Is.True);
                 Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], Context(), out var intent, out _), Is.True);
                 var result = new CommanderIntentResolver().Resolve(intent, Simulation, Manager);
                 Assert.That(result.CreatedGoal, Is.True, result.Reason); return result.Goal;
             }
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
     }
 }
                    stalePending = !VisibleEconomyTargets(goal).Any(t => t.Id == targetId && t.Kind == kind);
                }
                if (allIssued || stalePending)
                {
                    var stranded = new List<UnitData>();
                    foreach (int id in goal.SelectedWorkerIds)
                    {
                        var worker = simulation.UnitRegistry.GetUnit(id);
                        if (MatchesDestination(worker, request.Destination)) continue;
                        // Finish a real delivery before changing its source; no synthetic credit.
                        if (worker.State == UnitState.MovingToDropoff || worker.State == UnitState.DroppingOff) continue;
                        // Another selected worker can slaughter the sheep first. The original
                        // native order still travels to its carcass; do not mislabel or replace it.
                        if (AwaitingAssignedSheepTransition(goal, worker)) continue;
                        if (worker.State != UnitState.Idle && !IsGatheringState(worker.State))
                            return AllocationBlocked("An original selected worker is busy with another activity; it will not be reclaimed or replaced.");
                        stranded.Add(worker);
                    }
                    if (stranded.Count > 0)
                    {
                        var targets = VisibleEconomyTargets(goal);
                        if (targets.Count == 0)
                            return AllocationBlocked($"No usable visible {request.Destination.SourceKind}/{request.Destination.Resource} source remains for the original selected workers.");
                        if (!TryResolveWorkerAssignments(goal, stranded, targets, out var recovery, progressiveRecovery: true))
                            return goal.RecoverySearchExhausted
                                ? AllocationBlocked("No visible reachable authorized source has capacity for the original stranded workers; no substitute is allowed.")
                                : new CommanderPlan(CommanderGoalStatus.Executing,
                                    "Searching further visible authorized sources for the original stranded workers within the per-tick path bound.", 0, 0);
                        goal.RecoveryCandidateOffsets.Clear(); goal.RecoverySearchExhausted = false;
                        var changed = new HashSet<int>(stranded.Select(w => w.Id));
                        goal.Assignments.RemoveAll(a => changed.Contains(a.WorkerId));
                        goal.Assignments.AddRange(recovery);
                        goal.CommandGroups.Clear(); goal.NextCommandGroup = 0;
                        foreach (var group in recovery.GroupBy(a => new { a.Kind, a.TargetId }).OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.TargetId))
                        {
                            int[] ids = group.Select(a => a.WorkerId).OrderBy(id => id).ToArray();
                            goal.CommandGroups.Add(group.Key.Kind == CommanderWorkerTargetKind.ResourceNode
                                ? (ICommand)new GatherCommand(goal.PlayerId, ids, group.Key.TargetId, request.Destination.SourceKind)
                                : new SlaughterSheepCommand { PlayerId = goal.PlayerId, VillagerIds = ids,
                                    SheepUnitId = group.Key.TargetId, SourceKind = request.Destination.SourceKind });
                        }
                        // Selected IDs, requested count/source and gathered-income origin never reset.
                    }
                }
            }
            if (goal.NextCommandGroup < goal.CommandGroups.Count)
            {
                ICommand command = goal.CommandGroups[goal.NextCommandGroup];
                int targetId = command is GatherCommand gather ? gather.ResourceNodeId : ((SlaughterSheepCommand)command).SheepUnitId;
        private bool TryResolveWorkerAssignments(AllocateWorkersGoal goal, List<UnitData> workers, List<EconomyTarget> targets,
            out List<CommanderWorkerAssignment> assignments, bool progressiveRecovery = false)
        {
            assignments = new List<CommanderWorkerAssignment>();
            var selected = new HashSet<int>(workers.Select(w => w.Id));
            var retained = new Dictionary<int, EconomyTarget>();
            foreach (UnitData worker in workers)
                if (MatchesDestination(worker, goal.Allocation.Destination)
                    && simulation.MapData.GetResourceNode(worker.TargetResourceNodeId)?.IsFarmNode == true)
                {
                    var target = targets.FirstOrDefault(t => t.Kind == CommanderWorkerTargetKind.ResourceNode
                        && t.Id == worker.TargetResourceNodeId);
                    if (targets.Any(t => t.Kind == CommanderWorkerTargetKind.ResourceNode
                        && t.Id == worker.TargetResourceNodeId)) retained[worker.Id] = target;
                }
            var used = new Dictionary<long, int>();
            foreach (UnitData worker in simulation.UnitRegistry.GetAllUnits())
            {
                if (worker.CurrentHealth <= 0 || worker.State == UnitState.Dead
                    || (selected.Contains(worker.Id) && !retained.ContainsKey(worker.Id))) continue;
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
                if (retained.TryGetValue(worker.Id, out EconomyTarget currentTarget))
                {
                    assignments.Add(new CommanderWorkerAssignment(worker.Id, currentTarget.Id, currentTarget.Kind));
                    continue;
                }
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
                    // Sheep commands path to their actual tile; adjacent reachability alone is insufficient.
                    if (target.Kind == CommanderWorkerTargetKind.OwnedSheep && (!GridPathfinder.TryFindCompletePath(simulation.MapData,
                        start, new Vector2Int(target.X, target.Z), out var sheepPath, goal.PlayerId, simulation.BuildingRegistry)
                        || !IsKnownPath(goal.PlayerId, start, sheepPath))) continue;
                    assignments.Add(new CommanderWorkerAssignment(worker.Id, target.Id, target.Kind));
                    // Retain a found index while later workers continue searching, but revalidate
                    // it every retry (no cached route, ownership, visibility or capacity authority).
                    if (progressiveRecovery) goal.RecoveryCandidateOffsets[worker.Id] = index;
                    used.TryGetValue(target.Key, out int count); used[target.Key] = count + 1; found = true; break;
                }
                if (!found)
                {
                    if (progressiveRecovery)
                    {
                        goal.RecoveryCandidateOffsets[worker.Id] = end < candidates.Count ? end : 0;
                        // A complete failed sweep is a real blocker. Later bounded sweeps may
                        // recover changed terrain/sources, without resetting the blocked timeout.
                        if (end >= candidates.Count) goal.RecoverySearchExhausted = true;
                    }
                    assignments.Clear(); return false;
                }
            }
            return true;
        }
    }
}
