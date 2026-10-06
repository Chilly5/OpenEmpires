using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private readonly struct EconomyTarget
        {
            internal readonly int Id, X, Z, Width, Height, Capacity;
            internal readonly CommanderWorkerTargetKind Kind;
            internal EconomyTarget(int id, int x, int z, int width, int height, int capacity, CommanderWorkerTargetKind kind)
            { Id = id; X = x; Z = z; Width = width; Height = height; Capacity = capacity; Kind = kind; }
            internal long Key => ((long)Kind << 32) | (uint)Id;
        }

        private CommanderPlan PlanWorkerAllocation(AllocateWorkersGoal goal, int currentTick)
        {
            if (goal.HumanInterrupted) return AllocationBlocked("The player took control of a selected worker; this request will not reclaim or replace it.");
            if (!ReferenceEquals(goal.RuntimeOwner?.Simulation, simulation)) return AllocationBlocked("The worker request belongs to another runtime.");
            CommanderWorkerAllocation request = goal.Allocation;
            if (!goal.Prepared)
            {
                if (!goal.BaselineCaptured)
                {
                    goal.BaselineCount = simulation.UnitRegistry.GetAllUnits().Count(u => OwnedLivingWorker(u, goal.PlayerId)
                        && MatchesDestination(u, request.Destination));
                    goal.BaselineCaptured = true;
                }
                int requested = request.CountMode == CommanderWorkerCountMode.AllMatching ? 0 : request.Count.Value;
                if (request.Mode == CommanderWorkerAllocationMode.TargetTotal)
                {
                    int current = simulation.UnitRegistry.GetAllUnits().Count(u => OwnedLivingWorker(u, goal.PlayerId)
                        && MatchesDestination(u, request.Destination));
                    requested = Math.Max(0, requested - current);
                    if (requested == 0) return new CommanderPlan(CommanderGoalStatus.Completed, "The requested worker total is already satisfied.", current, 0);
                }
                List<EconomyTarget> targets = VisibleEconomyTargets(goal);
                List<UnitData> eligible = simulation.UnitRegistry.GetAllUnits().Where(u => EligibleAllocationWorker(goal, u, currentTick)).ToList();
                eligible.Sort((a, b) => CompareEconomyWorkers(a, b, targets));
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
                var selected = new List<UnitData>(requested);
                var transfers = new Dictionary<ResourceType, int>();
                foreach (UnitData worker in eligible)
                {
                    ResourceNodeData previous = simulation.MapData.GetResourceNode(worker.TargetResourceNodeId);
                    if (IsGatheringState(worker.State) && previous != null && previous.Type != request.Destination.Resource
                        && goal.ProtectedWorkerMinimums.TryGetValue(previous.Type, out int minimum))
                    {
                        transfers.TryGetValue(previous.Type, out int moved);
                        if (CountResourceWorkers(goal.PlayerId, previous.Type) - moved <= minimum) continue;
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
                    int[] ids = group.Select(a => a.WorkerId).OrderBy(id => id).ToArray();
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
            if (goal.LastIssuedSimulationTick >= simulation.CurrentTick)
                return new CommanderPlan(CommanderGoalStatus.Executing, "Waiting for the issued worker command to be processed.", 0, 0);
            if (goal.NextCommandGroup < goal.CommandGroups.Count)
            {
                ICommand command = goal.CommandGroups[goal.NextCommandGroup];
                int targetId = command is GatherCommand gather ? gather.ResourceNodeId : ((SlaughterSheepCommand)command).SheepUnitId;
                CommanderWorkerTargetKind kind = command is GatherCommand ? CommanderWorkerTargetKind.ResourceNode : CommanderWorkerTargetKind.OwnedSheep;
                if (!VisibleEconomyTargets(goal).Any(t => t.Id == targetId && t.Kind == kind)) return AllocationBlocked("A selected source is no longer usable; no unrelated fallback was issued.");
                return new CommanderPlan(CommanderGoalStatus.Executing, $"Assigning selected workers to {goal.Allocation.Destination.Resource}.", 0, 0, command);
            }
            int observed = goal.SelectedWorkerIds.Count(id => MatchesDestination(simulation.UnitRegistry.GetUnit(id), request.Destination));
            if (request.Mode == CommanderWorkerAllocationMode.TargetTotal
                && simulation.UnitRegistry.GetAllUnits().Count(u => OwnedLivingWorker(u, goal.PlayerId)
                    && MatchesDestination(u, request.Destination)) < request.Count.Value)
                return AllocationBlocked("The observed total is below the requested worker total; the frozen selection will not be silently replaced.");
            return observed == goal.SelectedWorkerIds.Count
                ? new CommanderPlan(CommanderGoalStatus.Completed, $"The {observed} selected workers have matching assignments.", observed, 0)
                : new CommanderPlan(CommanderGoalStatus.Blocked, $"Only {observed}/{goal.SelectedWorkerIds.Count} selected workers have matching assignments; no substitute will be used.", observed, 0);
        }

        private static CommanderPlan AllocationBlocked(string reason) => new CommanderPlan(CommanderGoalStatus.Blocked, reason, 0, 0);
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
            return request.Mode == CommanderWorkerAllocationMode.SelectedCount || !MatchesDestination(worker, request.Destination);
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
                && worker.State == UnitState.MovingToSlaughter)
            {
                UnitData sheep = simulation.UnitRegistry.GetUnit(worker.CombatTargetId);
                return sheep != null && sheep.IsSheep && sheep.PlayerId == worker.PlayerId && sheep.CurrentHealth > 0 && sheep.State != UnitState.Dead;
            }
            return false;
        }

        private bool LegalFarm(ResourceNodeData node, int player)
        {
            if (!node.IsFarmNode) return true;
            BuildingData farm = simulation.BuildingRegistry.GetBuilding(node.LinkedBuildingId);
            return farm != null && farm.Type == BuildingType.Farm && farm.PlayerId == player && !farm.IsDestroyed && !farm.IsUnderConstruction;
        }

        private List<EconomyTarget> VisibleEconomyTargets(AllocateWorkersGoal goal)
        {
            var result = new List<EconomyTarget>(); var destination = goal.Allocation.Destination;
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                if (!node.IsDepleted && node.Type == destination.Resource && ResourceSourceRules.Matches(node, destination.SourceKind)
                    && LegalFarm(node, goal.PlayerId) && simulation.FogOfWar.GetVisibility(goal.PlayerId, node.TileX, node.TileZ) == TileVisibility.Visible)
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
                    }
            return result;
        }

        private int CompareEconomyWorkers(UnitData a, UnitData b, List<EconomyTarget> targets)
        {
            int priority = WorkerPriority(a).CompareTo(WorkerPriority(b)); if (priority != 0) return priority;
            long da = targets.Count == 0 ? 0 : targets.Min(t => TargetDistance(a, t));
            long db = targets.Count == 0 ? 0 : targets.Min(t => TargetDistance(b, t));
            int distance = da.CompareTo(db); return distance != 0 ? distance : a.Id.CompareTo(b.Id);
        }
        private int WorkerPriority(UnitData u) => u.State == UnitState.Idle ? 0 : workerAuthority.IsCommanderControlled(u.Id) ? 1 : 2;
        private long TargetDistance(UnitData worker, EconomyTarget target)
        {
            Vector2Int tile = simulation.MapData.WorldToTile(worker.SimPosition);
            long dx = (long)tile.x - target.X, dz = (long)tile.y - target.Z; return dx * dx + dz * dz;
        }

        private bool TryResolveWorkerAssignments(AllocateWorkersGoal goal, List<UnitData> workers, List<EconomyTarget> targets,
            out List<CommanderWorkerAssignment> assignments)
        {
            assignments = new List<CommanderWorkerAssignment>();
            var selected = new HashSet<int>(workers.Select(w => w.Id));
            var used = new Dictionary<long, int>();
            foreach (UnitData worker in simulation.UnitRegistry.GetAllUnits())
            {
                if (worker.CurrentHealth <= 0 || worker.State == UnitState.Dead || selected.Contains(worker.Id)) continue;
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
                var candidates = targets.Where(t => !used.TryGetValue(t.Key, out int count) || count < t.Capacity)
                    .OrderBy(t => TargetDistance(worker, t)).ThenBy(t => t.Kind).ThenBy(t => t.Id).Take(pathValidationCandidates);
                bool found = false;
                foreach (EconomyTarget target in candidates)
                {
                    Vector2Int start = simulation.MapData.WorldToTile(worker.SimPosition);
                    if (!HasReachableAdjacentTile(start, goal.PlayerId, target.X, target.Z, target.Width, target.Height)) continue;
                    // Sheep commands path to their actual tile; adjacent reachability alone is insufficient.
                    if (target.Kind == CommanderWorkerTargetKind.OwnedSheep && (!GridPathfinder.TryFindCompletePath(simulation.MapData,
                        start, new Vector2Int(target.X, target.Z), out var sheepPath, goal.PlayerId, simulation.BuildingRegistry)
                        || !IsKnownPath(goal.PlayerId, start, sheepPath))) continue;
                    assignments.Add(new CommanderWorkerAssignment(worker.Id, target.Id, target.Kind));
                    used.TryGetValue(target.Key, out int count); used[target.Key] = count + 1; found = true; break;
                }
                if (!found) { assignments.Clear(); return false; }
            }
            return true;
        }
    }
}
