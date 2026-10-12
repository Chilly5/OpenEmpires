# Task3 FIX1 frozen incremental package

Review only the three Task3 findings and breakage introduced by FIX1. No commits;
HEAD remains 4c9de96adbf6342fbe9e1490fa9b898a0f99caf6. The seven changed tracked files
below use pre-FIX1 snapshots in task-3-fix1-baseline. OriginLedger was not modified
by original Task3; its diff is against HEAD. Two new partials are included in full.
The later recovery follow-up changed only economy tests and is excluded here.

Source-matched final affected job ad65a36b3ea143a7b7423e05d6452aa6 passed16/16;
raw evidence saved. No test rerun is requested. Report FIX1 section has full history.
diff --git a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__AI__Commander__CommanderGoal.cs.snapshot b/Assets/Scripts/AI/Commander/CommanderGoal.cs
index 7b7edae..68971fc 100644
--- a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__AI__Commander__CommanderGoal.cs.snapshot
+++ b/Assets/Scripts/AI/Commander/CommanderGoal.cs
@@ -149,25 +149,29 @@ namespace OpenEmpires
         }
 
         internal void SetDependencies(IEnumerable<CommanderGoal> requiredGoals)
         {
             dependencies.Clear();
             if (requiredGoals == null) return;
             foreach (CommanderGoal required in requiredGoals)
                 if (required != null && !dependencies.Contains(required)) dependencies.Add(required);
         }
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
             if(this is BuildStructureGoal build)build.PendingPlacementCommand=null;
             if(this is ReachAgeGoal age)age.PendingAgeUpCommand=null;
             if(this is AllocateWorkersGoal boundAllocation){boundAllocation.ResultSourceGoal=null;boundAllocation.RecoveryCandidateOffsets.Clear();boundAllocation.RecoverySearchExhausted=false;}
             if(this is WatchFutureUnitsGoal future)future.ReleaseObservations();
         }
     }
 
     public sealed class BuildStructureGoal : CommanderGoal
     {
         public BuildingType StructureType { get; }
         public int Count { get; }
@@ -299,24 +303,28 @@ namespace OpenEmpires
         }
     }
 
     public sealed class CommanderCapabilityGoal : CommanderGoal
     {
         internal CommanderTargetBinding TargetBinding { get; set; }
         public CapabilityActionIntent Action { get; }
         internal bool CommandIssued { get; set; }
         internal ICommand IssuedCommand { get; set; }
         internal int CommandIssuedSimulationTick { get; set; } = -1;
         internal FixedVector3[] NativeMovementSlots { get; set; }
         internal FixedVector3[] NativeMovementEndpoints { get; set; }
+        internal Fixed32[] NativeMovementScatter { get; set; }
+        internal bool[] NativeMovementProcessed { get; set; }
+        internal NativeMovePathResult[] NativeMovementResults { get; set; }
+        internal bool NativeMovementReplaced { get; set; }
         internal long NativeRemainingDistance { get; set; } = long.MaxValue;
         internal int NativeLastProgressSimulationTick { get; set; } = -1;
         // Runtime-only typed result source. This prevents provider node indices from
         // becoming entity references and prevents cross-manager/stale-result reuse.
         internal CommanderGoal ResultSourceGoal { get; set; }
         internal bool RequiresResultBinding { get; set; }
         internal bool ResultHumanOverride { get; set; }
 
         public CommanderCapabilityGoal(CapabilityActionIntent action, int maxDurationTicks = 36000)
             : base(action?.PlayerId ?? throw new ArgumentNullException(nameof(action)),
                 CommanderGoalType.CapabilityAction, maxDurationTicks)
         {
diff --git a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__AI__Commander__CommanderGoalManager.cs.snapshot b/Assets/Scripts/AI/Commander/CommanderGoalManager.cs
index 62480bf..662fb09 100644
--- a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__AI__Commander__CommanderGoalManager.cs.snapshot
+++ b/Assets/Scripts/AI/Commander/CommanderGoalManager.cs
@@ -108,24 +108,26 @@ namespace OpenEmpires
             int pathValidationCandidates = CommanderPlanner.DefaultPathValidationCandidates)
         {
             this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
             this.playerId = playerId;
             workerAuthority = new CommanderWorkerAuthority(simulation, playerId);
             simulation.CommandBuffer.CommandEnqueued += HandleCommandEnqueued;
             simulation.OnBuildingPlacedFromCommand += HandleBuildingPlacedFromCommand;
             simulation.TrainingOrderAccepted += HandleTrainingAccepted;
             simulation.TrackedUnitProduced += HandleTrackedUnitProduced;
             simulation.TrainingOriginLost += HandleTrainingOriginLost;
             simulation.ProducerUnitProduced += HandleFutureBirth;
             simulation.LocalActionCommandProcessed += HandleFutureCommandProcessed;
+            simulation.NativeMoveProcessed += HandleNativeMoveProcessed;
+            simulation.NativeUnitCommandProcessed += HandleNativeUnitCommandProcessed;
             planner = new CommanderPlanner(simulation, workerAuthority,
                 pathValidationCandidates);
         }
 
         public EnsureUnitCountGoal SubmitEnsureUnitCount(int requestedUnitType, int targetTotal,
             int maxQueueDepth = 3, int maxDurationTicks = 36000,
             IReadOnlyList<CommanderConstraint> constraints = null, int? newProductionCount = null)
         {
             if (newProductionCount.HasValue && (newProductionCount.Value < 1 || newProductionCount.Value > simulation.Config.MaxPopulation))
                 throw new ArgumentOutOfRangeException(nameof(newProductionCount));
             var goal = new EnsureUnitCountGoal(playerId, requestedUnitType, targetTotal,
                 maxQueueDepth, maxDurationTicks: maxDurationTicks);
@@ -629,24 +631,25 @@ namespace OpenEmpires
                         if (RequestTracingEnabled && goal.RequestAuthority != null)
                             Debug.Log($"[Commander] request={goal.RequestAuthority.RequestId};root={goal.RequestNodeIndex};goal={goal.GoalId};dispatch={plan.Command.GetType().Name}");
                         if (goal is AllocateWorkersGoal allocation)
                         {
                             allocation.NextCommandGroup++;
                             allocation.LastIssuedSimulationTick = simulation.CurrentTick;
                         }
                         if (goal is CommanderCapabilityGoal capability)
                         {
                             capability.CommandIssued = true;
                             capability.IssuedCommand = plan.Command;
                             capability.CommandIssuedSimulationTick = simulation.CurrentTick;
+                            capability.NativeLastProgressSimulationTick = simulation.CurrentTick;
                         }
                         if (goal is WatchFutureUnitsGoal future)
                         {
                             int[] subjects = CommanderWorkerAuthority.GetSubjectUnitIds(plan.Command);
                             if (subjects != null)
                                 foreach (int id in subjects)
                                 {
                                     future.issuedUnitIds.Add(id);
                                     future.pendingCommands[id] = plan.Command;
                                 }
                         }
                         if (plan.Command is GatherCommand) goal.LastEconomyCommandTick = currentTick;
@@ -849,24 +852,26 @@ namespace OpenEmpires
                 case CommanderGoalStatus.Cancelled: return CommanderGoalEventType.GoalCancelled;
                 default: return CommanderGoalEventType.GoalProgressChanged;
             }
         }
 
         public void Dispose()
         {
             if (disposed) return;
             disposed = true;
             ReleaseTrainingObservations();
             simulation.ProducerUnitProduced -= HandleFutureBirth;
             simulation.LocalActionCommandProcessed -= HandleFutureCommandProcessed;
+            simulation.NativeMoveProcessed -= HandleNativeMoveProcessed;
+            simulation.NativeUnitCommandProcessed -= HandleNativeUnitCommandProcessed;
             simulation.CommandBuffer.CommandEnqueued -= HandleCommandEnqueued;
             simulation.OnBuildingPlacedFromCommand -= HandleBuildingPlacedFromCommand;
             for (int i = 0; i < activeGoals.Count; i++)
             {
                 if (activeGoals[i] is WatchFutureUnitsGoal future)
                     SuppressFuturePendingActions(future);
                 if (activeGoals[i] is BuildStructureGoal spatial)
                     spatial.PendingPlacementCommand = null;
                 if (activeGoals[i] is ReachAgeGoal reachAge)
                     reachAge.PendingAgeUpCommand = null;
                 workerAuthority.ReleaseGoal(activeGoals[i].GoalId);
             }
diff --git a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__AI__Commander__CommanderPlanner.cs.snapshot b/Assets/Scripts/AI/Commander/CommanderPlanner.cs
index b1e3d54..2b590fc 100644
--- a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__AI__Commander__CommanderPlanner.cs.snapshot
+++ b/Assets/Scripts/AI/Commander/CommanderPlanner.cs
@@ -255,97 +255,144 @@ namespace OpenEmpires
         }
 
         private bool TryResolveNativeMovementSlots(CommanderCapabilityGoal goal, MoveCommand original,
             int currentTick, out MoveCommand move, out string reason)
         {
             move = original;
             reason = "No complete known reachable formation objective exists near the requested location.";
             if (original.UnitIds == null || original.UnitIds.Length == 0 || original.UnitIds.Length > 200) return false;
             Vector2Int anchor = simulation.MapData.WorldToTile(original.TargetPosition);
             if (!simulation.MapData.IsInBounds(anchor.x, anchor.y)) return false;
             var occupied = new HashSet<Vector2Int>();
             var slots = new FixedVector3[original.UnitIds.Length];
+            var nativeCenters = new FixedVector3[original.UnitIds.Length];
+            var scatterLimits = new Fixed32[original.UnitIds.Length];
             for (int i = 0; i < original.UnitIds.Length; i++)
             {
                 UnitData unit = simulation.UnitRegistry.GetUnit(original.UnitIds[i]);
                 if (unit == null || unit.PlayerId != goal.PlayerId || unit.CurrentHealth <= 0
                     || unit.State == UnitState.Dead || workerAuthority.IsHumanProtected(unit.Id, currentTick)) return false;
-                Vector2Int start = simulation.MapData.WorldToTile(unit.SimPosition);
                 bool found = false;
                 for (int radius = 0; radius <= 10 && !found; radius++)
                     for (int dx = -radius; dx <= radius && !found; dx++)
                         for (int dz = -radius; dz <= radius && !found; dz++)
                         {
                             if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius) continue;
                             int x = anchor.x + dx, z = anchor.y + dz;
                             var destination = new Vector2Int(x, z);
-                            if (occupied.Contains(destination)
+                            if (!simulation.MapData.IsWalkable(x, z)
                                 || !simulation.MapData.IsWalkable(x, z, goal.PlayerId, simulation.BuildingRegistry)
                                 || !IsKnownTile(goal.PlayerId, x, z)) continue;
-                            if (!GridPathfinder.TryFindCompletePath(simulation.MapData, start, destination,
-                                out List<Vector2Int> path, goal.PlayerId, simulation.BuildingRegistry)
-                                || !IsKnownPath(goal.PlayerId, start, path)) continue;
-                            occupied.Add(destination);
-                            slots[i] = simulation.MapData.TileToWorldFixed(x, z);
+                            FixedVector3 proposed = simulation.MapData.TileToWorldFixed(x, z);
+                            FixedVector3 commandAnchor = i == 0 ? proposed : slots[0];
+                            if (!TryKnownNativeMoveEnvelope(goal.PlayerId, anchor, unit,
+                                commandAnchor, proposed, occupied, out FixedVector3 nativeCenter,
+                                out Fixed32 scatter, out HashSet<Vector2Int> covered)) continue;
+                            occupied.UnionWith(covered);
+                            slots[i] = proposed;
+                            nativeCenters[i] = nativeCenter;
+                            scatterLimits[i] = scatter;
                             found = true;
                         }
                 if (!found) return false;
             }
-            goal.NativeMovementSlots = slots;
             move.TargetPosition = slots[0];
             move.FormationPositions = slots;
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
             reason = "Moving exact selected units to known reachable native formation positions.";
             return true;
         }
 
+        private bool TryKnownNativeMoveEnvelope(int playerId, Vector2Int semanticAnchor,
+            UnitData unit, FixedVector3 commandAnchor, FixedVector3 slot,
+            HashSet<Vector2Int> occupied, out FixedVector3 center,
+            out Fixed32 scatter, out HashSet<Vector2Int> covered)
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
         private bool ObserveNativeMovement(CommanderCapabilityGoal goal, MoveCommand move,
             int[] subjects, int currentTick, out string reason)
         {
             reason = "Waiting for the exact selected units to reach their native formation endpoints.";
             if (subjects == null || subjects.Length == 0 || goal.NativeMovementSlots == null
-                || goal.NativeMovementSlots.Length != subjects.Length) return false;
+                || goal.NativeMovementSlots.Length != subjects.Length
+                || goal.NativeMovementProcessed == null || goal.NativeMovementEndpoints == null
+                || goal.NativeMovementScatter == null || goal.NativeMovementResults == null) return false;
             if (simulation.CurrentTick <= goal.CommandIssuedSimulationTick) return false;
+            if (goal.NativeMovementReplaced)
+            { reason = "An exact subject has a replacement native order; unrelated movement cannot satisfy this request."; return false; }
             for (int i = 0; i < subjects.Length; i++)
             {
                 UnitData current = simulation.UnitRegistry.GetUnit(subjects[i]);
                 if (current == null || current.State != UnitState.Moving && current.State != UnitState.Idle)
                 {
                     reason = "An exact movement subject is temporarily unavailable; no replacement or takeover is inferred.";
                     return false;
                 }
             }
-            if (goal.NativeMovementEndpoints == null)
+            for (int i = 0; i < subjects.Length; i++)
             {
-                var endpoints = new FixedVector3[subjects.Length];
-                for (int i = 0; i < subjects.Length; i++)
+                if (!goal.NativeMovementProcessed[i])
                 {
-                    UnitData unit = simulation.UnitRegistry.GetUnit(subjects[i]);
-                    if (unit == null) return false;
-                    endpoints[i] = unit.HasPath ? unit.FinalDestination : unit.SimPosition;
-                    if (!Near(endpoints[i], goal.NativeMovementSlots[i], Fixed32.FromInt(2)))
-                    {
-                        reason = "A native route stopped short of its validated formation slot; no distant idle position counts as arrival.";
-                        return false;
-                    }
+                    if (simulation.CurrentTick - goal.CommandIssuedSimulationTick >= 150)
+                        reason = "The exact native formation made no progress for 150 simulation ticks; native processing was not observed.";
+                    return false;
                 }
-                goal.NativeMovementEndpoints = endpoints;
-                goal.NativeLastProgressSimulationTick = simulation.CurrentTick;
+                if (goal.NativeMovementResults[i] == NativeMovePathResult.Partial
+                    || goal.NativeMovementResults[i] == NativeMovePathResult.NoPath
+                    || Math.Abs((long)goal.NativeMovementEndpoints[i].x.Raw - goal.NativeMovementSlots[i].x.Raw) > goal.NativeMovementScatter[i].Raw
+                    || Math.Abs((long)goal.NativeMovementEndpoints[i].z.Raw - goal.NativeMovementSlots[i].z.Raw) > goal.NativeMovementScatter[i].Raw)
+                { reason = "A native route stopped short of its validated formation slot; no distant idle position counts as arrival."; return false; }
             }
             long remaining = 0;
             bool allArrived = true;
             for (int i = 0; i < subjects.Length; i++)
             {
                 UnitData unit = simulation.UnitRegistry.GetUnit(subjects[i]);
-                if (unit == null || !Near(unit.FinalDestination, goal.NativeMovementEndpoints[i], Fixed32.FromInt(2))
-                    && unit.HasPath)
+                if (unit == null || unit.HasPath
+                    && !Near(unit.FinalDestination, goal.NativeMovementEndpoints[i], Fixed32.FromInt(1)))
                 { reason = "An exact subject has a replacement native order; unrelated movement cannot satisfy this request."; return false; }
                 Fixed32 dx = unit.SimPosition.x - goal.NativeMovementEndpoints[i].x;
                 Fixed32 dz = unit.SimPosition.z - goal.NativeMovementEndpoints[i].z;
                 remaining += Math.Abs((long)dx.Raw) + Math.Abs((long)dz.Raw);
                 if (unit.State != UnitState.Idle || !Near(unit.SimPosition, goal.NativeMovementEndpoints[i], Fixed32.FromInt(1)))
                     allArrived = false;
             }
             if (allArrived) return true;
             if (remaining < goal.NativeRemainingDistance)
             { goal.NativeRemainingDistance = remaining; goal.NativeLastProgressSimulationTick = simulation.CurrentTick; }
             if (simulation.CurrentTick - goal.NativeLastProgressSimulationTick >= 150)
                 reason = "The exact native formation made no progress for 150 simulation ticks; distant idle units are not complete.";
diff --git a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__AI__Commander__CommanderTacticalStatusSnapshot.cs.snapshot b/Assets/Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs
index 28cd626..9a7ad15 100644
--- a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__AI__Commander__CommanderTacticalStatusSnapshot.cs.snapshot
+++ b/Assets/Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs
@@ -108,28 +108,41 @@ namespace OpenEmpires
         }
     }
 
     internal static class CommanderTacticalStatusProjection
     {
         internal static bool IsStatusQuestion(string input) => Regex.IsMatch(input??"",
             @"\b(status|progress|progressing|doing|waiting|finished|ready|blocked|stuck|request|army)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
         internal static bool IsCausalTaskQuestion(string input,GameSimulation sim,CommanderGoalManager manager)
         {
             string query=(input??"").Trim();
             if(!Regex.IsMatch(query,@"^(why|what happened|how come|what went wrong)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return false;
             if(Regex.IsMatch(query,@"\b(task|order|goal|arriv(?:e|ed|ing)|stop(?:ped)?|staff(?:ed|ing)?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
+            if(Regex.IsMatch(query,@"^what happened\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
+                &&ReferencedUnitCount(query).HasValue)return true;
             var catalog=GameKnowledgeCatalog.Build(sim);
             return catalog.Units.Any(u=>Mention(query,u.DisplayName)||u.Aliases.Any(a=>Mention(query,a)))
                 ||catalog.Buildings.Any(b=>Mention(query,b.DisplayName)||b.Aliases.Any(a=>Mention(query,a)));
         }
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
             if(Regex.IsMatch(text,@"^(why|what|how|when|where|which|who|is|are|did|does|have|has)\b|^(tell me|explain|show me|report|i want to know|can my civilization|status|progress|current strategy status|strategy status)\b",
                 RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
             if(Regex.IsMatch(text,@"^can (i|we) (make|train|build) (?:a |an )?[a-z]+\s*[?.!]*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
             if(Regex.IsMatch(text,@"^(can|could|would|will) you (?:(tell|show|remind) me\b|(?:explain|report)\b)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
             return text.EndsWith("?",StringComparison.Ordinal)
                 && !Regex.IsMatch(text,@"^(can|could|would|will) (you|we|i)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
                 && !SimpleTextIntentParser.IsExplicitOrderForm(text);
         }
 
@@ -152,24 +165,29 @@ namespace OpenEmpires
             {
                 var record=catalog.FindUnitByType(type);var actual=catalog.FindUnitByType(sim.ResolveCivUnitType(manager.PlayerId,type));
                 return Mention(query,CommanderIntentCatalog.GetUnitDisplayName(type))||Mention(query,CommanderIntentCatalog.GetUnitDisplayName(type,true))
                     || record!=null&&(Mention(query,record.DisplayName)||record.Aliases.Any(a=>Mention(query,a)))
                     || actual!=null&&(Mention(query,actual.DisplayName)||actual.Aliases.Any(a=>Mention(query,a)));
             }
             bool BuildingMention(BuildingType type)
             { var record=catalog.FindBuilding(type.ToString());return Mention(query,type.ToString())||record!=null&&(Mention(query,record.DisplayName)||record.Aliases.Any(a=>Mention(query,a))); }
             // Detect an explicit content reference independently of current goals;
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
                 .Select(m=>int.Parse(m.Value,System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
             if(specific&&counts.Count>0)
                 groups=groups.Where(g=>g.Any(s=>HasMention(s)&&(s is EnsureUnitCountGoal u
                     ?counts.Contains(u.IsExplicitNewProduction?u.RequiredNewProductionCount:u.TargetTotal)
                     :s is BuildStructureGoal b?counts.Contains(b.Count):s is CommanderCapabilityGoal c&&counts.Contains(c.Action.UnitSelector.Count)))).ToList();
             var active=groups.Where(g=>g.Any(s=>!s.IsTerminal)).ToList();
             if(active.Count>0)groups=active;else groups=groups.OrderByDescending(g=>g.Max(s=>s.CreatedTick)).Take(1).ToList();
             var selected=groups.OrderBy(g=>g.Min(s=>s.CreatedTick)).ThenBy(g=>g.Min(s=>s.GoalId)).Take(3).ToList();
             var requests=new List<CommanderTacticalRequestSnapshot>();bool truncated=groups.Count>selected.Count;
diff --git a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__Core__GameSimulation.cs.snapshot b/Assets/Scripts/Core/GameSimulation.cs
index d9d98ef..ec031ad 100644
--- a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Scripts__Core__GameSimulation.cs.snapshot
+++ b/Assets/Scripts/Core/GameSimulation.cs
@@ -1711,25 +1711,25 @@ namespace OpenEmpires
                     }
                     return;
                 }
             }
         }
 
         private void ProcessCommand(ICommand command, ICommand verifiedOriginal = null)
         {
             if (ConsumeSuppressedLocalAction(command)) return;
             switch (command)
             {
                 case MoveCommand move:
-                    ProcessMoveCommand(move);
+                    ProcessMoveCommand(move, verifiedOriginal ?? command);
                     break;
                 case GatherCommand gather:
                     ProcessGatherCommand(gather);
                     break;
                 case StopCommand stop:
                     ProcessStopCommand(stop);
                     break;
                 case AttackBuildingCommand attackBuilding:
                     ProcessAttackBuildingCommand(attackBuilding);
                     break;
                 case AttackUnitCommand attackUnit:
                     ProcessAttackUnitCommand(attackUnit);
@@ -1840,24 +1840,25 @@ namespace OpenEmpires
                     ProcessMarketTradeCommand(marketTrade);
                     break;
                 case TributeCommand tribute:
                     ProcessTributeCommand(tribute);
                     break;
                 case ResearchCommand research:
                     ProcessResearchCommand(research);
                     break;
             }
             if (command is GatherCommand || command is PatrolCommand
                 || command is SlaughterSheepCommand)
                 PublishLocalActionCommandProcessed(command);
+            PublishNativeUnitCommandProcessed(verifiedOriginal ?? command, command);
         }
 
         public void SpawnNeutralSheep()
         {
             var rng = new System.Random(config.MapSeed + 7777);
             int totalSheep = config.SheepPerPlayer * playerCount;
             int margin = 15;
             int mapW = MapData.Width;
             int mapH = MapData.Height;
 
             for (int i = 0; i < totalSheep; i++)
             {
@@ -3081,71 +3082,38 @@ namespace OpenEmpires
                 int refundGold = goldCost - (goldCost * enemyDamage / building.MaxHealth);
 
                 var resources = ResourceManager.GetPlayerResources(building.PlayerId);
                 resources.Wood += refundWood;
                 resources.Stone += refundStone;
                 resources.Food += refundFood;
                 resources.Gold += refundGold;
             }
 
             CleanUpDestroyedBuilding(cmd.BuildingId);
         }
 
-        private void ProcessMoveCommand(MoveCommand cmd)
+        private void ProcessMoveCommand(MoveCommand cmd, ICommand originalCommand)
         {
             // === Retarget if click target is impassable or a hole ===
             Vector2Int cmdTargetTile = MapData.WorldToTile(cmd.TargetPosition);
             bool needsRetarget = !MapData.IsWalkable(cmdTargetTile.x, cmdTargetTile.y);
 
             if (needsRetarget)
             {
                 Vector2Int newTarget = GridPathfinder.FindNearestWalkableTile(MapData, cmdTargetTile, 20);
                 if (newTarget.x < 0) return;
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
             int formGroupSize = 0;
             if (cmd.UnitIds.Length > 1 && !cmd.IsQueued)
             {
                 formGroupId = nextFormationGroupId++;
                 // Count only alive units for formation group size
                 for (int i = 0; i < cmd.UnitIds.Length; i++)
                 {
                     var u = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                     if (u != null && u.State != UnitState.Dead && u.PlayerId == cmd.PlayerId) formGroupSize++;
@@ -3179,50 +3147,26 @@ namespace OpenEmpires
                 bool hasFacing = cmd.HasFacing;
                 FixedVector3 facingDir = cmd.FacingDirection;
 
                 if (cmd.PreserveFormation && unit.InFormation)
                 {
                     targetPos = cmd.TargetPosition + unit.FormationOffset;
                     hasFacing = true;
                     facingDir = unit.FormationFacing;
                     unit.FormationMoveSpeed = formationMoveSpeed;
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
                         unit.FormationOffset = targetPos - cmd.TargetPosition;
                         if (cmd.HasFacing)
                         {
                             unit.FormationFacing = cmd.FacingDirection;
                             unit.InFormation = true;
                             unit.FormationGroupId = formGroupId;
                             unit.FormationGroupSize = formGroupSize;
                             unit.FormationMoveSpeed = formationMoveSpeed;
                         }
@@ -3276,30 +3220,35 @@ namespace OpenEmpires
 
                     if (path.Count > 0)
                     {
                         // Align FinalDestination with actual A* endpoint so waypoint line matches path
                         Vector2Int pathEnd = path[path.Count - 1];
                         if (pathEnd != goalTile)
                             targetPos = MapData.TileToWorldFixed(pathEnd.x, pathEnd.y);
 
                         unit.SetPath(path);
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
                     unit.ConstructionTargetBuildingId = -1;
                     unit.GatherTimer = Fixed32.Zero;
                     unit.ClearSavedPath();
                     unit.CombatTargetId = -1;
                     unit.CombatTargetBuildingId = -1;
                     unit.DropOffBuildingId = -1;
                     unit.TargetGarrisonBuildingId = -1;
 
                     // Sheep receiving a move command stops following
diff --git a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Tests__EditMode__CommanderGrandFixStatusTests.cs.snapshot b/Assets/Tests/EditMode/CommanderGrandFixStatusTests.cs
index 330136d..1b8640f 100644
--- a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Tests__EditMode__CommanderGrandFixStatusTests.cs.snapshot
+++ b/Assets/Tests/EditMode/CommanderGrandFixStatusTests.cs
@@ -81,24 +81,60 @@ namespace OpenEmpires.Tests
         {
             Waiting(); var provider = new FactsProvider(Answer); var chat = Chat(provider);
             int before = manager.Goals.Count;
             await chat.SubmitMessageAsync(question);
             var status = JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext).tacticalStatus;
             Assert.That(status, Is.Not.Null);
             Assert.That(status.requests[0].steps[0].reason.ToLowerInvariant(), Does.Contain("wood"));
             Assert.That(chat.Conversation.Memory.ToJson().ToLowerInvariant(), Does.Contain("wood"));
             Assert.That(manager.Goals.Count, Is.EqualTo(before));
             Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
         }
 
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
         [Test] public void LongCausalBlocker_IsExplicitlyUnavailableRatherThanMeaningTruncated()
         {
             var goal = Waiting();
             goal.SetStatus(CommanderGoalStatus.Blocked,
                 new string('a', 160) + " not the six requested archers; no survivor substitution is allowed.");
             var status = Observe("Why did my Spearmen task stop?", out string json);
             Assert.That(status, Is.Not.Null);
             Assert.That(status.requests[0].steps[0].reason, Does.Contain("unavailable"));
             Assert.That(status.requests[0].steps[0].reason, Does.Not.Contain("..."));
             Assert.That(json, Does.Not.Contain("no survivor substitution"));
         }
 
diff --git a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Tests__EditMode__CommanderPhase4GCapabilityTests.cs.snapshot b/Assets/Tests/EditMode/CommanderPhase4GCapabilityTests.cs
index 58783c0..94a6fac 100644
--- a/Docs/CommanderPhase5C/evidence/task-3-fix1-baseline/Assets__Tests__EditMode__CommanderPhase4GCapabilityTests.cs.snapshot
+++ b/Assets/Tests/EditMode/CommanderPhase4GCapabilityTests.cs
@@ -392,24 +392,196 @@ namespace OpenEmpires.Tests
                     CommanderCapabilityActionType.MoveUnits,
                     new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                     new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                 manager.Tick(0); simulation.CommandBuffer.FlushCommands();
                 simulation.Tick(new List<ICommand>()); // Issued command intentionally never reached native processing.
                 unit.State = UnitState.Idle;
                 manager.Tick(15);
                 Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
             }
             finally { manager.Dispose(); Object.DestroyImmediate(config); }
         }
 
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
         [Test]
         public void NativeMovementWithNoProgress_BecomesBoundedBlocker()
         {
             var config = ScriptableObject.CreateInstance<SimulationConfig>();
             var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
             var manager = new CommanderGoalManager(simulation, 0);
             try
             {
                 int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                 simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                 var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 12, z + 12),
                     Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
@@ -498,34 +670,37 @@ namespace OpenEmpires.Tests
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
                 manager.Tick(15);
                 manager.Tick(150);
 
                 Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
                 Assert.That(simulation.CommandBuffer.FlushCommands(), Has.None.TypeOf<MoveCommand>());
             }
             finally
             {
                 manager.Dispose();
diff --git a/Open Empires/Assets/Scripts/Core/CommanderCommandOriginLedger.cs b/Open Empires/Assets/Scripts/Core/CommanderCommandOriginLedger.cs
index 3ebfa8e..4410445 100644
--- a/Open Empires/Assets/Scripts/Core/CommanderCommandOriginLedger.cs	
+++ b/Open Empires/Assets/Scripts/Core/CommanderCommandOriginLedger.cs	
@@ -93,47 +93,59 @@ namespace OpenEmpires
             for (int i = 0; i < batch.Originals.Length; i++)
             {
                 if (local[i] is NoopCommand || !TryFingerprint(local[i], out Fingerprint replayed)
                     || !batch.Stamped[i].Equals(replayed))
                 {
                     Discard(batch, simulation);
                     return verified;
                 }
             }
 
             // Without an explicit relay FIFO contract, equal payloads from distinct
             // original boxes cannot be assigned to their true issuers by ordinal.
+            var ambiguousMoves = new HashSet<int>();
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
 
             for (int i = 0; i < batch.Originals.Length; i++)
             {
                 ICommand original = batch.Originals[i];
-                if (!(original is PlaceBuildingCommand)
+                if (!(original is PlaceBuildingCommand) && !(original is MoveCommand)
                     && (!(original is TrainUnitCommand) || !simulation.HasPendingTrainingOrigin(original)))
                     continue;
+                if (ambiguousMoves.Contains(i)) continue;
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
         }
 
Assets/Scripts/AI/Commander/CommanderGoalManager.NativeMove.cs
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
Assets/Scripts/Core/GameSimulation.MovementObservation.cs
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
