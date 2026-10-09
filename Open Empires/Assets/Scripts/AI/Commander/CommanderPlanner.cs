using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OpenEmpires
{
    internal readonly struct CommanderPlan
    {
        public readonly CommanderGoalStatus Status;
        public readonly string Reason;
        public readonly ICommand Command;
        public readonly int OwnedCount;
        public readonly int QueuedCount;

        public CommanderPlan(CommanderGoalStatus status, string reason, int ownedCount,
            int queuedCount, ICommand command = null)
        {
            Status = status;
            Reason = reason;
            OwnedCount = ownedCount;
            QueuedCount = queuedCount;
            Command = command;
        }
    }

    internal sealed partial class CommanderPlanner
    {
        public const int DefaultPathValidationCandidates = 4;
        public const int MinimumPathValidationCandidates = 3;
        public const int MaximumPathValidationCandidates = 5;
        private const int EconomyCommandCooldownTicks = 90;
        private const int ConstructionStallTicks = 150;
        private const int ConstructionRecoveryCooldownTicks = 150;
        private readonly GameSimulation simulation;
        private readonly CommanderWorkerAuthority workerAuthority;
        private readonly int pathValidationCandidates;

        public int DiagnosticPathCheckCount { get; internal set; }
        public void ResetDiagnosticPathCheckCount() => DiagnosticPathCheckCount = 0;

        public CommanderPlanner(GameSimulation simulation, CommanderWorkerAuthority workerAuthority,
            int pathValidationCandidates = DefaultPathValidationCandidates)
        {
            this.simulation = simulation;
            this.workerAuthority = workerAuthority;
            this.pathValidationCandidates = Mathf.Clamp(pathValidationCandidates,
                MinimumPathValidationCandidates, MaximumPathValidationCandidates);
        }

        public CommanderPlan Plan(CommanderGoal goal, int currentTick)
        {
            if (goal.FrozenWorkerHumanOverride)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The player took control of a request-bound worker; no substitute or reclaim is authorized.", 0, 0);
            if (goal.FrozenWorkerIds != null && goal.FrozenWorkerIds.Any(id =>
            {
                var worker = simulation.UnitRegistry.GetUnit(id);
                return worker == null || worker.PlayerId != goal.PlayerId || !worker.IsVillager
                    || worker.IsSheep || worker.CurrentHealth <= 0 || worker.State == UnitState.Dead;
            }))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "A request-bound worker is missing, dead or foreign; no substitute is authorized.", 0, 0);
            if (goal is AllocateWorkersGoal workers) return PlanWorkerAllocation(workers, currentTick);
            if (goal is WatchFutureUnitsGoal future) return PlanFutureUnits(future, currentTick);
            if (goal is EnsureUnitCountGoal units) return PlanUnits(units, currentTick);
            if (goal is BuildStructureGoal building) return PlanStructure(building, currentTick);
            if (goal is ResourceAllocationGoal resource) return PlanAllocation(resource, currentTick);
            if (goal is ReachAgeGoal reachAge) return PlanReachAge(reachAge, currentTick);
            if (goal is CommanderCapabilityGoal capability) return PlanCapability(capability, currentTick);
            return new CommanderPlan(CommanderGoalStatus.Failed, "Unknown goal type.", 0, 0);
        }

        private CommanderPlan PlanCapability(CommanderCapabilityGoal goal, int currentTick)
        {
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
                goal.TargetBinding = selectedTarget;
            }
            CommanderResultBinding? binding = null;
            if (goal.ResultSourceGoal is EnsureUnitCountGoal units)
            {
                if (units.ResultCaptureTick < 0 || units.ResultUnitIds == null
                    || units.ResultUnitIds.Count != units.RequiredNewProductionCount)
                    return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                        "Waiting for the referenced producer result; no unrelated unit may substitute.", 0, 0);
                binding = CommanderResultBinding.ForUnits(units.ResultUnitIds,
                    units.GoalId, units.CreatedTick, simulation, goal.RuntimeOwner);
            }
            else if (goal.ResultSourceGoal is BuildStructureGoal building)
            {
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
                    if (unit != null && unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
                        && unit.State != UnitState.Dead && !workerAuthority.IsHumanProtected(unit.Id, currentTick))
                        eligible++;
                }
                if (eligible == 0)
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        "The player took control of every eligible capability unit; Commander will not reclaim them.", 0, 0);
            }

            switch (goal.Action.ActionType)
            {
                case CommanderCapabilityActionType.MoveUnits:
                case CommanderCapabilityActionType.DefendArea:
                case CommanderCapabilityActionType.RetreatUnits:
                case CommanderCapabilityActionType.ScoutArea:
                    if (issued is MoveCommand move && AllUnitsAt(subjects, move.TargetPosition, currentTick))
                        return new CommanderPlan(CommanderGoalStatus.Completed,
                            "The movement objective is satisfied in simulation state.", eligible, 0);
                    return new CommanderPlan(CommanderGoalStatus.Executing,
                        "Waiting for the selected units to reach the resolved location.", eligible, 0);

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

                case CommanderCapabilityActionType.AttackTarget:
                    if (issued is AttackUnitCommand attackUnit)
                    {
                        UnitData target = simulation.UnitRegistry.GetUnit(attackUnit.TargetUnitId);
                        if (target == null || target.State == UnitState.Dead || target.CurrentHealth <= 0)
                            return new CommanderPlan(CommanderGoalStatus.Completed,
                                "The visible enemy unit target is no longer alive.", eligible, 0);
                    }
                    if (issued is AttackBuildingCommand attackBuilding)
                    {
                        BuildingData target = simulation.BuildingRegistry.GetBuilding(attackBuilding.TargetBuildingId);
                        if (target == null || target.IsDestroyed)
                            return new CommanderPlan(CommanderGoalStatus.Completed,
                                "The visible enemy building target is no longer present.", eligible, 0);
                    }
                    return new CommanderPlan(CommanderGoalStatus.Executing,
                        "Waiting for the resolved attack target to be destroyed.", eligible, 0);

                case CommanderCapabilityActionType.SetRallyPoint:
                    if (issued is SetRallyPointCommand rally)
                    {
                        BuildingData building = simulation.BuildingRegistry.GetBuilding(rally.BuildingId);
                        if (building != null && !building.IsDestroyed && building.HasRallyPoint)
                            return new CommanderPlan(CommanderGoalStatus.Completed,
                                "The production building has the requested rally point in simulation state.", 1, 0);
                    }
                    return new CommanderPlan(CommanderGoalStatus.Executing,
                        "Waiting for the rally point command to be applied.", 0, 0);

                case CommanderCapabilityActionType.RepairTarget:
                    if (issued is RepairBuildingCommand repair)
                    {
                        BuildingData building = simulation.BuildingRegistry.GetBuilding(repair.TargetBuildingId);
                        if (building == null || building.IsDestroyed)
                            return new CommanderPlan(CommanderGoalStatus.Failed,
                                "The repair target was destroyed or lost.", eligible, 0);
                        if (building.CurrentHealth >= building.MaxHealth)
                            return new CommanderPlan(CommanderGoalStatus.Completed,
                                "The owned building is fully repaired.", eligible, 0);
                    }
                    return new CommanderPlan(CommanderGoalStatus.Executing,
                        "Waiting for the owned building repair to complete.", eligible, 0);

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

        private bool AllUnitsAt(int[] subjects, FixedVector3 target, int currentTick)
        {
            if (subjects == null || subjects.Length == 0) return false;
            int eligible = 0;
            Fixed32 tolerance = Fixed32.FromInt(1);
            Fixed32 toleranceSquared = tolerance * tolerance;
            for (int i = 0; i < subjects.Length; i++)
            {
                UnitData unit = simulation.UnitRegistry.GetUnit(subjects[i]);
                if (unit == null || unit.PlayerId < 0 || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead || workerAuthority.IsHumanProtected(unit.Id, currentTick)) continue;
                eligible++;
                Fixed32 dx = unit.SimPosition.x - target.x;
                Fixed32 dz = unit.SimPosition.z - target.z;
                if (dx * dx + dz * dz > toleranceSquared) return false;
            }
            return eligible > 0;
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
        }

        internal void CaptureConstraints(CommanderGoal goal, IReadOnlyList<CommanderConstraint> constraints)
        {
            if (constraints == null) return;
            for (int i = 0; i < constraints.Count; i++)
            {
                if (constraints[i] is NoConstructionConstraint) goal.ConstructionForbidden = true;
                if (constraints[i] is ResourceSourceConstraint source)
                    goal.ResourceSourceRestrictions[source.Resource] = source.SourceKind;
                if (constraints[i] is MaximumQueueConstraint queue && goal is EnsureUnitCountGoal units)
                    units.MaxQueueDepth = queue.MaximumQueue;
                if (constraints[i] is PreferredWorkersConstraint workers)
                    goal.UseIdleWorkersOnly = workers.WorkerSource == CommanderPreferredWorkerSource.IdleOnly;
                if (constraints[i] is ProtectedResourceConstraint resource)
                    goal.ProtectedWorkerMinimums[resource.Resource] = resource.MinimumWorkers
                        ?? CountResourceWorkers(goal.PlayerId, resource.Resource);
            }
        }

        internal int CountCompletedBuildings(int playerId, BuildingType type)
        {
            int count = 0;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].PlayerId == playerId && buildings[i].Type == type
                    && !buildings[i].IsDestroyed && !buildings[i].IsUnderConstruction) count++;
            return count;
        }

        internal int CountResourceWorkers(int playerId, ResourceType resource)
        {
            int count = 0;
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId == playerId && unit.IsVillager && unit.CurrentHealth > 0
                    && IsGatheringResource(unit, resource)) count++;
            }
            return count;
        }

        private bool CanReassignWorker(CommanderGoal goal, UnitData worker)
        {
            if (goal.FrozenWorkerIds != null && !goal.FrozenWorkerIds.Contains(worker.Id)) return false;
            var reservation = workerAuthority.GetReservation(worker.Id);
            if (reservation.HasValue && reservation.Value.GoalId != goal.GoalId) return false;
            if (goal.UseIdleWorkersOnly && worker.State != UnitState.Idle) return false;
            if (worker.CommandQueue.Count > 0) return false;
            if (!IsGatheringState(worker.State)) return true;
            ResourceNodeData node = simulation.MapData.GetResourceNode(worker.TargetResourceNodeId);
            return node == null || !goal.ProtectedWorkerMinimums.TryGetValue(node.Type, out int minimum)
                || CountResourceWorkers(goal.PlayerId, node.Type) > minimum;
        }

        private CommanderPlan PlanStructure(BuildStructureGoal goal, int currentTick)
        {
            if (!CommanderIntentCatalog.IsSupportedStructure(goal.StructureType))
                return new CommanderPlan(CommanderGoalStatus.Failed, "Unsupported structure.", 0, 0);
            if (goal.HasSemanticPlacement)
                return PlanPlacedStructure(goal, currentTick);
            if (goal.HasResultConsumer)
            {
                var completedResults = new List<int>();
                BuildingData ownFoundation = null;
                foreach (int id in goal.AttributedBuildingIds)
                {
                    var produced = simulation.BuildingRegistry.GetBuilding(id);
                    if (produced == null || produced.IsDestroyed || produced.PlayerId != goal.PlayerId
                        || simulation.GetEffectiveBuildingType(produced) != goal.StructureType)
                        return new CommanderPlan(CommanderGoalStatus.Blocked,
                            "An exact constructed result is missing or foreign; no unrelated building or replacement is permitted.", completedResults.Count, 0);
                    if (produced.IsUnderConstruction) ownFoundation = ownFoundation ?? produced;
                    else completedResults.Add(id);
                }
                if (completedResults.Count == goal.Count)
                {
                    goal.CaptureBuildingResult(completedResults.AsReadOnly(), currentTick);
                    return new CommanderPlan(CommanderGoalStatus.Completed,
                        $"Completed {goal.Count} exact requested {goal.StructureType} results.", goal.Count, 0);
                }
                if (ownFoundation != null)
                    return PlanConstructionRecovery(goal, ownFoundation, currentTick, completedResults.Count, 1, "Requested construction");
                if (goal.PendingPlacementCommand != null)
                {
                    if (simulation.CurrentTick <= goal.PlacementIssuedSimulationTick)
                        return new CommanderPlan(CommanderGoalStatus.WaitingForConstruction,
                            "Waiting for the exact placement command to be processed.", completedResults.Count, 0);
                    goal.PendingPlacementCommand = null; // Rejected command: no result was created.
                }
                return PlanBuilding(goal, goal.StructureType, currentTick, completedResults.Count, 0);
            }
            int completed = CountCompletedBuildings(goal.PlayerId, goal.StructureType);
            if (completed >= goal.TargetTotal)
            {
                if (goal.HasResultConsumer)
                {
                    List<int> produced = FindNewCompletedBuildings(goal);
                    if (produced.Count < goal.Count)
                        return new CommanderPlan(CommanderGoalStatus.WaitingForConstruction,
                            "The requested structure count is met only by pre-existing buildings; waiting for the exact new result.",
                            completed, 0);
                    goal.CaptureBuildingResult(produced.Take(goal.Count).ToArray(), currentTick);
                }
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    $"{goal.StructureType} construction complete ({completed}/{goal.TargetTotal}).", completed, 0);
            }
            BuildingData foundation = FindOwnedBuilding(goal.PlayerId, goal.StructureType, true);
            if (foundation != null)
                return PlanConstructionRecovery(goal, foundation, currentTick, completed, 1, "Construction");
            return PlanBuilding(goal, goal.StructureType, currentTick, completed, 0);
        }

        private CommanderPlan PlanPlacedStructure(BuildStructureGoal goal, int currentTick)
        {
            goal.PlacementBlocker = CommanderPlacementBlocker.None;
            if (goal.HasResultConsumer && goal.Count > 1)
            {
                var completed = new List<int>();
                foreach (int id in goal.AttributedBuildingIds)
                {
                    var result = simulation.BuildingRegistry.GetBuilding(id);
                    if (result == null || result.IsDestroyed || result.PlayerId != goal.PlayerId
                        || simulation.GetEffectiveBuildingType(result) != goal.StructureType)
                        return new CommanderPlan(CommanderGoalStatus.Blocked,
                            "An exact placed structure is missing or foreign; no replacement is authorized.", completed.Count, 0);
                    if (!result.IsUnderConstruction) completed.Add(id);
                }
                if (completed.Count == goal.Count)
                {
                    goal.CaptureBuildingResult(completed.AsReadOnly(), currentTick);
                    return new CommanderPlan(CommanderGoalStatus.Completed,
                        $"Completed {goal.Count} distinct exact semantic placements.", goal.Count, 0);
                }
                var latest = simulation.BuildingRegistry.GetBuilding(goal.PlacedBuildingId);
                if (latest != null && !latest.IsUnderConstruction)
                {
                    // Keep all previous attribution, but resolve a fresh legal tile for
                    // the next structure against real current occupancy.
                    goal.PlacedBuildingId = -1;
                    goal.PendingPlacementCommand = null;
                    goal.PlacementIssuedTick = -1;
                }
            }
            if (goal.PlacedBuildingId >= 0)
            {
                BuildingData bound = simulation.BuildingRegistry.GetBuilding(goal.PlacedBuildingId);
                if (bound == null || bound.IsDestroyed || bound.PlayerId != goal.PlayerId
                    || simulation.GetEffectiveBuildingType(bound) != goal.StructureType
                    || bound.OriginTileX != goal.PlacedTileX || bound.OriginTileZ != goal.PlacedTileZ)
                {
                    goal.PlacementBlocker = CommanderPlacementBlocker.BoundBuildingUnavailable;
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        "The placed building is no longer owned and available at its requested tile.", 0, 0);
                }
                goal.PlacementBlocker = CommanderPlacementBlocker.None;
                if (!bound.IsUnderConstruction)
                {
                    if (goal.HasResultConsumer)
                        goal.CaptureBuildingResult(new[] { bound.Id }, currentTick);
                    return new CommanderPlan(CommanderGoalStatus.Completed,
                        $"{goal.StructureType} construction complete at ({bound.OriginTileX},{bound.OriginTileZ}) "
                        + $"as building #{bound.Id}.", 1, 0);
                }
                return PlanConstructionRecovery(goal, bound, currentTick, 0, 1,
                    $"Placed {goal.StructureType} at ({bound.OriginTileX},{bound.OriginTileZ})");
            }

            if (goal.PlacementIssuedTick >= 0)
            {
                if (simulation.CurrentTick <= goal.PlacementIssuedSimulationTick)
                    return new CommanderPlan(CommanderGoalStatus.WaitingForConstruction,
                        $"Waiting for the placement command at ({goal.PlacedTileX},{goal.PlacedTileZ}).", 0, 0);
                // The ordinary command was processed without creating this building. Recheck
                // current rules and the bounded candidate set instead of accepting another site.
                goal.PlacementIssuedTick = -1;
                goal.PendingPlacementCommand = null;
            }
            return PlanBuilding(goal, goal.StructureType, currentTick, 0, 0);
        }

        private CommanderPlan PlanAllocation(ResourceAllocationGoal goal, int currentTick)
        {
            int count = CountResourceWorkers(goal.PlayerId, goal.Resource);
            if (count >= goal.TargetWorkers)
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    $"{count}/{goal.TargetWorkers} villagers assigned to {goal.Resource}.", count, 0);
            return PlanGather(goal, goal.Resource, currentTick, count, 0,
                $"Assigning at least {goal.TargetWorkers} villagers to {goal.Resource}; currently {count}.");
        }

        private CommanderPlan PlanReachAge(ReachAgeGoal goal, int currentTick)
        {
            int currentAge = simulation.GetPlayerAge(goal.PlayerId);
            if (currentAge >= goal.TargetAge)
            {
                goal.Blocker = CommanderAgeBlocker.None;
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    $"Player has reached age {goal.TargetAge}.", currentAge, 0);
            }

            int nextAge = currentAge + 1;
            if (goal.ConstructionForbidden)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "This request forbids construction of the required age-up landmark.", currentAge, 0);
            Civilization civ = simulation.GetPlayerCivilization(goal.PlayerId);
            if (!LandmarkDefinitions.HasChoices(civ, goal.TargetAge))
            {
                goal.Blocker = CommanderAgeBlocker.UnsupportedTarget;
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    $"Civilization {civ} cannot reach requested age {goal.TargetAge}.", currentAge, 0);
            }
            if (!LandmarkDefinitions.HasChoices(civ, nextAge))
            {
                goal.Blocker = CommanderAgeBlocker.UnsupportedTarget;
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    $"Civilization {civ} has no supported landmark progression to age {nextAge}.", currentAge, 0);
            }

            if (goal.PendingAgeUpCommand != null)
            {
                if (simulation.CurrentTick <= goal.AgeUpIssuedSimulationTick)
                    return new CommanderPlan(CommanderGoalStatus.WaitingForConstruction,
                        "Waiting for the age-up placement command to be processed.", currentAge, 0);
                goal.PendingAgeUpCommand = null;
                goal.AgeUpIssuedSimulationTick = -1;
            }

            if (goal.AgeUpBuildingId >= 0)
            {
                BuildingData tracked = FindBuildingById(goal.PlayerId, goal.AgeUpBuildingId);
                if (tracked != null && !tracked.IsDestroyed)
                {
                    if (tracked.IsUnderConstruction)
                        return PlanConstructionRecovery(goal, tracked, currentTick, currentAge, 0,
                            $"Age {nextAge} landmark");
                    // A completed landmark should have advanced the simulation age. If it
                    // did not, forget the stale binding and re-evaluate the real state.
                    goal.AgeUpBuildingId = -1;
                }
                else
                {
                    goal.AgeUpBuildingId = -1;
                    goal.Blocker = CommanderAgeBlocker.LandmarkUnavailable;
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        "The age-up landmark was destroyed or is no longer owned; retrying from current state.", currentAge, 0);
                }
            }

            BuildingData inProgress = FindOwnedBuilding(goal.PlayerId, BuildingType.Landmark, true);
            if (inProgress != null)
            {
                goal.AgeUpBuildingId = inProgress.Id;
                return PlanConstructionRecovery(goal, inProgress, currentTick, currentAge, 0,
                    $"Age {nextAge} landmark");
            }

            var choices = LandmarkDefinitions.GetChoices(civ, nextAge);
            LandmarkId landmark = (LandmarkId)Math.Min((int)choices.a, (int)choices.b);
            LandmarkDefinition definition = LandmarkDefinitions.Get(landmark);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(goal.PlayerId);
            bool needsFood = resources.Food < definition.FoodCost;
            bool needsGold = resources.Gold < definition.GoldCost;
            if (needsFood && needsGold)
            {
                int foodWorkers = CountResourceWorkers(goal.PlayerId, ResourceType.Food);
                int goldWorkers = CountResourceWorkers(goal.PlayerId, ResourceType.Gold);
                // Keep both independent resource branches alive and use a bounded
                // number of spare workers. The opposing branch's gatherers are
                // preserved so a short age goal cannot oscillate between resources.
                if (foodWorkers < 3 && (goldWorkers >= 2 || foodWorkers <= goldWorkers))
                {
                    goal.Blocker = CommanderAgeBlocker.MissingFood;
                    return PlanGather(goal, ResourceType.Food, currentTick, currentAge, 0,
                        $"Need {definition.FoodCost} food and {definition.GoldCost} gold for age {nextAge}; preparing food.",
                        goldWorkers > 0 ? ResourceType.Gold : (ResourceType?)null);
                }
                if (goldWorkers < 2)
                {
                    goal.Blocker = CommanderAgeBlocker.MissingGold;
                    return PlanGather(goal, ResourceType.Gold, currentTick, currentAge, 0,
                        $"Need {definition.FoodCost} food and {definition.GoldCost} gold for age {nextAge}; preparing gold.",
                        ResourceType.Food);
                }
                if (foodWorkers < 3)
                {
                    goal.Blocker = CommanderAgeBlocker.MissingFood;
                    return PlanGather(goal, ResourceType.Food, currentTick, currentAge, 0,
                        $"Need {definition.FoodCost} food and {definition.GoldCost} gold for age {nextAge}; preparing food.",
                        ResourceType.Gold);
                }
                goal.Blocker = CommanderAgeBlocker.MissingFood;
                return new CommanderPlan(CommanderGoalStatus.WaitingForResources,
                    $"Gathering food and gold for age {nextAge}; have {resources.Food}/{definition.FoodCost} food and {resources.Gold}/{definition.GoldCost} gold.",
                    currentAge, 0);
            }
            if (needsFood)
            {
                goal.Blocker = CommanderAgeBlocker.MissingFood;
                return PlanGather(goal, ResourceType.Food, currentTick, currentAge, 0,
                    $"Need {definition.FoodCost} food for age {nextAge}; have {resources.Food}.");
            }
            if (needsGold)
            {
                goal.Blocker = CommanderAgeBlocker.MissingGold;
                return PlanGather(goal, ResourceType.Gold, currentTick, currentAge, 0,
                    $"Need {definition.GoldCost} gold for age {nextAge}; have {resources.Gold}.");
            }

            UnitData builder = SelectBuilder(goal, currentTick);
            if (builder == null)
            {
                goal.Blocker = CommanderAgeBlocker.NoEligibleBuilder;
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No owned living villager is available to start age {nextAge}.", currentAge, 0);
            }
            BuildingData primaryBase = FindPrimaryTownCenter(goal.PlayerId);
            if (primaryBase == null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "No owned Town Center is available as an age-up placement anchor.", currentAge, 0);
            if (!TryFindBuildableTile(goal.PlayerId, BuildingType.Landmark, primaryBase, builder,
                out Vector2Int tile))
            {
                goal.Blocker = CommanderAgeBlocker.NoLegalCandidate;
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No visible, reachable location is legal for the age {nextAge} landmark.", currentAge, 0);
            }

            var command = new PlaceBuildingCommand(goal.PlayerId, BuildingType.Landmark,
                tile.x, tile.y, new[] { builder.Id });
            command.LandmarkIdValue = (int)landmark;
            goal.Blocker = CommanderAgeBlocker.None;
            return new CommanderPlan(CommanderGoalStatus.Executing,
                $"Starting age {nextAge} with landmark {landmark} at ({tile.x},{tile.y}) using villager #{builder.Id}.",
                currentAge, 0, command);
        }

        private CommanderPlan PlanUnits(EnsureUnitCountGoal goal, int currentTick)
        {
            if (!CommanderIntentCatalog.IsSupportedUnit(goal.RequestedUnitType))
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "Unsupported unit type.", 0, 0);

            int resolvedUnitType = simulation.ResolveCivUnitType(goal.PlayerId, goal.RequestedUnitType);
            int owned = CountOwnedLivingUnits(goal.PlayerId, resolvedUnitType);
            int queued = CountQueuedUnits(goal.PlayerId, resolvedUnitType);
            if (goal.HasResultConsumer && goal.TrainingAttributionUnavailable)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "An issued exact production order lost attribution and may have executed; no substitute or replacement production is authorized.", owned, queued);
            if (goal.HasResultConsumer && goal.TrackedTrainingOrders.Any(r => r.IsCancelled))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "A tracked production order was cancelled or invalidated; no replacement result is authorized.", owned, queued);
            if (goal.HasResultConsumer && goal.AttributedUnitIds.Any(id =>
            {
                var unit = simulation.UnitRegistry.GetUnit(id);
                return unit == null || unit.PlayerId != goal.PlayerId || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead || unit.UnitType != resolvedUnitType;
            }))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "An exact produced unit is missing, dead or foreign; no substitute or replacement production is authorized.", owned, queued);
            List<int> attributable = goal.HasResultConsumer
                ? goal.AttributedUnitIds.Where(id =>
                {
                    var unit = simulation.UnitRegistry.GetUnit(id);
                    return unit != null && unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
                        && unit.State != UnitState.Dead && unit.UnitType == resolvedUnitType;
                }).ToList() : null;
            int requiredNew = goal.RequiredNewProductionCount >= 0
                ? goal.RequiredNewProductionCount : Math.Max(0, goal.TargetTotal - goal.BaselineUnitIds.Count);
            int attributableQueued = goal.HasResultConsumer
                ? goal.TrackedTrainingOrders.Count(r => r.IsQueued && !r.IsCancelled
                    && simulation.BuildingRegistry.GetBuilding(r.ProducerId) is BuildingData producer
                    && !producer.IsDestroyed && producer.PlayerId == goal.PlayerId)
                    + simulation.CountPendingTrainingOrders(issuer: goal) : 0;

            if (goal.HasResultConsumer ? attributable.Count >= requiredNew : owned >= goal.TargetTotal)
            {
                if (goal.HasResultConsumer && !goal.IsExplicitNewProduction && owned < goal.TargetTotal)
                    return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                        "The exact new units are ready; waiting for the other queued units to satisfy the requested total.", owned, queued);
                if (goal.HasResultConsumer)
                {
                    goal.CaptureUnitResult(attributable.Take(requiredNew).ToArray(), currentTick);
                }
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    $"Owned {owned}/{goal.TargetTotal} living units.", owned, queued);
            }

            int remainingOrders = goal.HasResultConsumer
                ? requiredNew - attributable.Count - attributableQueued : goal.TargetTotal - owned - queued;
            if (goal.HasResultConsumer && !goal.IsExplicitNewProduction && remainingOrders > 0
                && (long)owned - attributable.Count + queued - attributableQueued != goal.ExpectedOtherUnitContribution)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "Existing or unrelated queued units changed the approved total/new quantity; request a revised plan. No extra production is authorized.", owned, queued);
            int totalQueuedPopulation = CountAllQueuedUnits(goal.PlayerId);
            int population = simulation.GetPopulation(goal.PlayerId);
            int populationCap = simulation.GetPopulationCap(goal.PlayerId);
            if (population + totalQueuedPopulation > populationCap
                || (remainingOrders > 0 && population + totalQueuedPopulation >= populationCap))
            {
                if (populationCap >= simulation.Config.MaxPopulation)
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        $"Maximum population reached ({populationCap}/{simulation.Config.MaxPopulation}). "
                        + "Cannot increase capacity further.", owned, queued);
                BuildingData house = FindOwnedBuilding(goal.PlayerId, BuildingType.House, true);
                if (house == null)
                    return PlanBuilding(goal, BuildingType.House, currentTick, owned, queued);
                // A House foundation already exists. Leave its builder reserved and
                // continue discovering independent producer/resource work below.
            }
            if (remainingOrders <= 0)
                return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                    $"Owned {owned}, queued {queued}, target {goal.TargetTotal}.", owned, queued);

            int requiredAge = LandmarkDefinitions.GetUnitRequiredAge(resolvedUnitType);
            if (simulation.GetPlayerAge(goal.PlayerId) < requiredAge)
                return new CommanderPlan(CommanderGoalStatus.WaitingForPrerequisite,
                    $"{CommanderIntentCatalog.GetUnitDisplayName(goal.RequestedUnitType)} requires age {requiredAge}. Advance age to resume.", owned, queued);

            BuildingData requiredProducer = null;
            if (goal.RequiredProducerGoal != null)
            {
                BuildStructureGoal producerGoal = goal.RequiredProducerGoal;
                if (!simulation.TryGetProductionBuildingType(goal.PlayerId, goal.RequestedUnitType,
                    out BuildingType requiredProducerType))
                    return new CommanderPlan(CommanderGoalStatus.Failed,
                        "No canonical producer exists.", owned, queued);
                if (producerGoal.IsTerminal && producerGoal.Status != CommanderGoalStatus.Completed)
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        "The requested producer goal is no longer available.", owned, queued);
                if (!ReferenceEquals(producerGoal.RuntimeOwner, goal.RuntimeOwner)
                    || producerGoal.PlayerId != goal.PlayerId)
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        "The requested producer result belongs to a different runtime or owner.", owned, queued);
                if (producerGoal.ResultCaptureTick < 0)
                {
                    CommanderPlan preparation = PlanUnitResourcePreparation(goal, currentTick, owned, queued);
                    if (preparation.Command != null) return preparation;
                    return new CommanderPlan(CommanderGoalStatus.WaitingForPrerequisite,
                        "Waiting for the complete exact new producer result before training.", owned, queued);
                }
                if (producerGoal.Status != CommanderGoalStatus.Completed
                    || producerGoal.ResultBuildingIds.Count != producerGoal.Count
                    || producerGoal.ResultBuildingIds.Distinct().Count() != producerGoal.Count)
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        "The exact producer result is missing or incomplete; no substitute is permitted.", owned, queued);
                // Validate the entire captured collection before using any member. Loss
                // of one constrained producer cannot silently widen or shrink the scope.
                foreach (int producerId in producerGoal.ResultBuildingIds)
                {
                    BuildingData candidate = FindBuildingById(goal.PlayerId, producerId);
                    if (candidate == null || candidate.IsDestroyed || candidate.IsUnderConstruction
                        || simulation.GetEffectiveBuildingType(candidate) != requiredProducerType
                        || !simulation.IsCompatibleProductionBuilding(goal.PlayerId, candidate, goal.RequestedUnitType))
                        return new CommanderPlan(CommanderGoalStatus.Blocked,
                            "An exact requested producer is no longer owned, completed or compatible; no substitute is permitted.", owned, queued);
                    if (ProductionQueueDepth(candidate) < goal.MaxQueueDepth
                        && (requiredProducer == null
                            || ProductionQueueDepth(candidate) < ProductionQueueDepth(requiredProducer)
                            || (ProductionQueueDepth(candidate) == ProductionQueueDepth(requiredProducer)
                                && candidate.Id < requiredProducer.Id)))
                        requiredProducer = candidate;
                }
                if (requiredProducer == null)
                    return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                        $"All exact requested producer queues are at Commander limit {goal.MaxQueueDepth}.", owned, queued);
            }

            if (goal.BoundProducerBuildingIds != null)
            {
                if (goal.RequiredProducerGoal != null || goal.BoundProducerBuildingIds.Count == 0
                    || goal.BoundProducerBuildingIds.Count > CommanderDynamicPlan.MaximumAggregateCount
                    || goal.BoundProducerBuildingIds.Distinct().Count() != goal.BoundProducerBuildingIds.Count
                    || !simulation.TryGetProductionBuildingType(goal.PlayerId, goal.RequestedUnitType,
                        out var selectedProducerType))
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        "The selected producer result is incompatible or ambiguous; no substitute is permitted.", owned, queued);
                foreach (int id in goal.BoundProducerBuildingIds)
                {
                    var selected = FindBuildingById(goal.PlayerId, id);
                    if (selected == null || selected.IsDestroyed || selected.IsUnderConstruction
                        || simulation.GetEffectiveBuildingType(selected) != selectedProducerType
                        || !simulation.IsCompatibleProductionBuilding(goal.PlayerId, selected, goal.RequestedUnitType))
                        return new CommanderPlan(CommanderGoalStatus.Blocked,
                            "An exact selected producer is missing, foreign or incompatible; no substitute is permitted.", owned, queued);
                    if (ProductionQueueDepth(selected) < goal.MaxQueueDepth
                        && (requiredProducer == null || ProductionQueueDepth(selected) < ProductionQueueDepth(requiredProducer)
                            || (ProductionQueueDepth(selected) == ProductionQueueDepth(requiredProducer) && selected.Id < requiredProducer.Id)))
                        requiredProducer = selected;
                }
                if (requiredProducer == null)
                    return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                        "All exact selected producer queues are full; unrelated capacity will not be used.", owned, queued);
            }

            BuildingData barracks = requiredProducer;
            bool hasOperationalProducer = false;
            if (barracks == null)
                barracks = FindBestAvailableProductionBuilding(goal, out hasOperationalProducer);
            if (barracks == null)
            {
                if (hasOperationalProducer)
                    return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                        $"All compatible production queues are at Commander limit {goal.MaxQueueDepth}.",
                        owned, queued);

                BuildingData unfinished = FindCompatibleProductionBuilding(goal.PlayerId,
                    goal.RequestedUnitType, true);
                if (unfinished != null)
                {
                    CommanderPlan preparation = PlanUnitResourcePreparation(goal, currentTick, owned, queued);
                    if (preparation.Command != null) return preparation;
                    return PlanConstructionRecovery(goal, unfinished, currentTick, owned, queued,
                        "Production prerequisite");
                }

                if (!simulation.TryGetProductionBuildingType(goal.PlayerId, goal.RequestedUnitType,
                    out BuildingType producerType))
                    return new CommanderPlan(CommanderGoalStatus.Failed, "No canonical producer exists.", owned, queued);
                return PlanBuilding(goal, producerType, currentTick, owned, queued);
            }

            simulation.GetUnitTrainingCosts(barracks, goal.RequestedUnitType,
                out int foodCost, out int woodCost, out int goldCost);
            string unitName = CommanderIntentCatalog.GetUnitDisplayName(goal.RequestedUnitType);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(goal.PlayerId);
            if (resources.Food < foodCost)
                return PlanGather(goal, ResourceType.Food, currentTick, owned, queued,
                    $"Need {foodCost} food for the next {unitName}; have {resources.Food}.");
            if (resources.Wood < woodCost)
                return PlanGather(goal, ResourceType.Wood, currentTick, owned, queued,
                    $"Need {woodCost} wood for the next {unitName}; have {resources.Wood}.");
            if (resources.Gold < goldCost)
                return PlanGather(goal, ResourceType.Gold, currentTick, owned, queued,
                    $"Need {goldCost} gold for the next {unitName}; have {resources.Gold}.");

            return new CommanderPlan(CommanderGoalStatus.Executing,
                $"Queueing {unitName} at {barracks.Type} #{barracks.Id}.", owned, queued,
                new TrainUnitCommand(goal.PlayerId, barracks.Id, goal.RequestedUnitType));
        }

        private CommanderPlan PlanUnitResourcePreparation(EnsureUnitCountGoal goal, int currentTick,
            int owned, int queued)
        {
            simulation.GetUnitTrainingSpec(goal.PlayerId, goal.RequestedUnitType,
                out _, out int foodCost, out int woodCost, out int goldCost, out _);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(goal.PlayerId);
            string unitName = CommanderIntentCatalog.GetUnitDisplayName(goal.RequestedUnitType);
            if (resources.Food < foodCost)
                return PlanGather(goal, ResourceType.Food, currentTick, owned, queued,
                    $"Preparing food for the next {unitName}; need {foodCost}, have {resources.Food}.");
            if (resources.Wood < woodCost)
                return PlanGather(goal, ResourceType.Wood, currentTick, owned, queued,
                    $"Preparing wood for the next {unitName}; need {woodCost}, have {resources.Wood}.");
            if (resources.Gold < goldCost)
                return PlanGather(goal, ResourceType.Gold, currentTick, owned, queued,
                    $"Preparing gold for the next {unitName}; need {goldCost}, have {resources.Gold}.");
            return new CommanderPlan(CommanderGoalStatus.WaitingForPrerequisite,
                "No immediate unit-resource preparation is required.", owned, queued);
        }

        private CommanderPlan PlanConstructionRecovery(CommanderGoal goal, BuildingData building,
            int currentTick, int owned, int queued, string context)
        {
            if (goal.ConstructionForbidden)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "This request forbids construction, including resuming an unfinished prerequisite. Use completed capacity or wait for independent work.", owned, queued);
            bool buildingChanged = goal.ObservedConstructionBuildingId != building.Id;
            bool progressed = !buildingChanged && goal.LastConstructionTicksRemaining >= 0
                && building.ConstructionTicksRemaining < goal.LastConstructionTicksRemaining;

            if (buildingChanged)
            {
                goal.ObservedConstructionBuildingId = building.Id;
                goal.LastConstructionProgressTick = currentTick;
                goal.ConstructionBuilderInRange = false;
            }
            else if (progressed)
            {
                goal.LastConstructionProgressTick = currentTick;
            }
            goal.LastConstructionTicksRemaining = building.ConstructionTicksRemaining;

            UnitData activeBuilder = FindActiveConstructionBuilder(goal.PlayerId, building);
            bool inRange = activeBuilder != null && IsInConstructionRange(activeBuilder, building);
            if (!inRange || !goal.ConstructionBuilderInRange)
                goal.LastConstructionProgressTick = currentTick;
            goal.ConstructionBuilderInRange = inRange;
            bool stalled = currentTick - goal.LastConstructionProgressTick >= ConstructionStallTicks;
            if (activeBuilder != null && !stalled)
                return new CommanderPlan(CommanderGoalStatus.WaitingForConstruction,
                    inRange
                        ? $"{context}: building #{building.Id} is advancing with villager #{activeBuilder.Id}."
                        : $"{context}: villager #{activeBuilder.Id} is travelling to building #{building.Id}.",
                    owned, queued);

            if (currentTick - goal.LastConstructionRecoveryTick < ConstructionRecoveryCooldownTicks)
                return new CommanderPlan(CommanderGoalStatus.WaitingForConstruction,
                    $"{context}: waiting for recovery command on building #{building.Id}.", owned, queued);

            UnitData recoveryBuilder = SelectRecoveryBuilder(goal, building, currentTick,
                allowActiveBuilder: stalled);
            if (recoveryBuilder == null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"{context}: building #{building.Id} is stalled and no reachable, unprotected "
                    + "owned villager can resume it.", owned, queued);

            return new CommanderPlan(CommanderGoalStatus.Executing,
                $"{context}: assigning villager #{recoveryBuilder.Id} to recover building #{building.Id}.",
                owned, queued,
                new ConstructBuildingCommand(goal.PlayerId, new[] { recoveryBuilder.Id }, building.Id));
        }

        private CommanderPlan PlanBuilding(CommanderGoal goal, BuildingType type,
            int currentTick, int owned, int queued)
        {
            if (goal.ConstructionForbidden)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "This request forbids construction. An eligible existing completed building or population capacity is required.", owned, queued);
            int requiredAge = LandmarkDefinitions.GetBuildingRequiredAge(type);
            if (simulation.GetPlayerAge(goal.PlayerId) < requiredAge)
                return new CommanderPlan(CommanderGoalStatus.WaitingForPrerequisite,
                    $"{type} requires age {requiredAge}. Advance age to resume.", owned, queued);
            int woodCost = simulation.GetBuildingWoodCost(type);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(goal.PlayerId);
            if (resources.Wood < woodCost)
                return PlanGather(goal, ResourceType.Wood, currentTick, owned, queued,
                    $"Need {woodCost} wood for {type}; have {resources.Wood}.");
            int foodCost = simulation.GetBuildingFoodCost(type);
            int goldCost = simulation.GetBuildingGoldCost(type);
            int stoneCost = simulation.GetBuildingStoneCost(type);
            if (resources.Food < foodCost)
                return PlanGather(goal, ResourceType.Food, currentTick, owned, queued,
                    $"Need {foodCost} food for {type}; have {resources.Food}.");
            if (resources.Gold < goldCost)
                return PlanGather(goal, ResourceType.Gold, currentTick, owned, queued,
                    $"Need {goldCost} gold for {type}; have {resources.Gold}.");
            if (resources.Stone < stoneCost)
                return PlanGather(goal, ResourceType.Stone, currentTick, owned, queued,
                    $"Need {stoneCost} stone for {type}; have {resources.Stone}.");

            if (goal is BuildStructureGoal placed && placed.HasSemanticPlacement)
                return PlanSemanticBuilding(placed, type, currentTick, owned, queued);

            UnitData builder = SelectBuilder(goal, currentTick);
            if (builder == null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No owned living villager is available to build {type}.", owned, queued);

            BuildingData primaryBase = FindPrimaryTownCenter(goal.PlayerId);
            if (primaryBase == null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No owned Town Center is available as a {type} placement anchor.", owned, queued);

            if (!TryFindBuildableTile(goal.PlayerId, type, primaryBase, builder, out Vector2Int tile))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No visible, reachable buildable location was found for {type}.", owned, queued);

            return new CommanderPlan(CommanderGoalStatus.Executing,
                $"Placing {type} at ({tile.x},{tile.y}) with villager #{builder.Id}.", owned, queued,
                new PlaceBuildingCommand(goal.PlayerId, type, tile.x, tile.y, new[] { builder.Id }));
        }

        private CommanderPlan PlanSemanticBuilding(BuildStructureGoal goal, BuildingType type,
            int currentTick, int owned, int queued)
        {
            if (goal.DynamicLocation != null)
                return PlanDynamicSemanticBuilding(goal, type, currentTick, owned, queued);
            var resolver = new CommanderSemanticReferenceResolver(simulation);
            if (goal.PlacementAnchorSelector == CommanderSemanticAnchorSelector.WorkedResource)
                return PlanWorkedResourceBuilding(goal, type, resolver, currentTick, owned, queued);
            if (!resolver.TryResolveOwnedAnchor(goal.PlayerId, goal.PlacementAnchorSelector.Value,
                goal.PlacementAnchorOrdinal, out BuildingData anchor))
            {
                goal.PlacementBlocker = CommanderPlacementBlocker.AnchorUnavailable;
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The requested owned placement anchor is unavailable.", owned, queued);
            }
            if (SelectBuilder(goal, currentTick) == null)
            {
                goal.PlacementBlocker = CommanderPlacementBlocker.NoEligibleBuilder;
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No owned living villager is available to build {type}.", owned, queued);
            }

            GetFootprint(type, out int width, out int height);
            IReadOnlyList<Vector2Int> candidates = CommanderSemanticPlacementCandidates.Generate(
                anchor.OriginTileX, anchor.OriginTileZ, anchor.TileFootprintWidth,
                anchor.TileFootprintHeight, width, height, simulation.MapData.Width,
                simulation.MapData.Height, goal.PlacementRelation.Value, goal.ClearGapTiles);
            int border = type == BuildingType.Farm ? 0 : 1;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int tile = candidates[i];
                if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y, width, height,
                    border, type)) continue;
                UnitData builder = FindReachableSemanticBuilder(goal, tile, width, height, currentTick);
                if (builder == null) continue;
                goal.PlacementBlocker = CommanderPlacementBlocker.None;
                int actualGap = SemanticClearGap(goal.PlacementRelation.Value, anchor,
                    tile, width, height);
                string placement = IsExactSemanticCandidate(goal, anchor, tile, width,
                    height, actualGap) ? "exact" : "bounded fallback";
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    $"Placing {type} at ({tile.x},{tile.y}) using {placement} semantic placement "
                    + $"with {actualGap} clear-tile gap and villager #{builder.Id}.", owned, queued,
                    new PlaceBuildingCommand(goal.PlayerId, type, tile.x, tile.y,
                        new[] { builder.Id }));
            }
            goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
            return new CommanderPlan(CommanderGoalStatus.Blocked,
                $"No visible, buildable, reachable location satisfies the bounded {type} placement.",
                owned, queued);
        }

        private CommanderPlan PlanWorkedResourceBuilding(BuildStructureGoal goal, BuildingType type,
            CommanderSemanticReferenceResolver resolver, int currentTick, int owned, int queued)
        {
            if (!goal.PlacementResourceType.HasValue)
                return new CommanderPlan(CommanderGoalStatus.Failed, "A worked resource type is required.", owned, queued);
            if (!resolver.TryResolveWorkedResource(goal.PlayerId, goal.PlacementResourceType.Value,
                out ResourceNodeData resource))
            {
                goal.PlacementBlocker = CommanderPlacementBlocker.AnchorUnavailable;
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "No visible resource node currently worked by an owned villager matches the request.", owned, queued);
            }
            GetFootprint(type, out int width, out int height);
            IReadOnlyList<Vector2Int> candidates = CommanderSemanticPlacementCandidates.Generate(
                resource.TileX, resource.TileZ, resource.FootprintWidth, resource.FootprintHeight,
                width, height, simulation.MapData.Width, simulation.MapData.Height,
                CommanderSemanticPlacementRelation.Near, goal.ClearGapTiles);
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int tile = candidates[i];
                if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y, width, height, 0, type)) continue;
                UnitData builder = FindReachableSemanticBuilder(goal, tile, width, height, currentTick);
                if (builder == null) continue;
                goal.PlacementBlocker = CommanderPlacementBlocker.None;
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    $"Placing {type} near the worked {goal.PlacementResourceType.Value} node "
                    + $"at ({tile.x},{tile.y}) with villager #{builder.Id}.", owned, queued,
                    new PlaceBuildingCommand(goal.PlayerId, type, tile.x, tile.y, new[] { builder.Id }));
            }
            goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
            return new CommanderPlan(CommanderGoalStatus.Blocked,
                $"No visible, buildable, reachable location is available near the worked {goal.PlacementResourceType.Value} node.",
                owned, queued);
        }

        private UnitData FindReachableSemanticBuilder(CommanderGoal goal, Vector2Int tile,
            int width, int height, int currentTick)
        {
            UnitData best = null;
            int bestPriority = int.MaxValue;
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId != goal.PlayerId || !unit.IsVillager || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead || workerAuthority.IsHumanProtected(unit.Id, currentTick)
                    || !CanReassignWorker(goal, unit)) continue;
                int priority = unit.State == UnitState.Idle ? 0
                    : workerAuthority.IsCommanderControlled(unit.Id) && IsGatheringState(unit.State) ? 1
                    : IsGatheringState(unit.State) ? 2 : int.MaxValue;
                if (priority == int.MaxValue || priority > bestPriority
                    || (priority == bestPriority && best != null && unit.Id >= best.Id)) continue;
                if (!HasReachableAdjacentTile(simulation.MapData.WorldToTile(unit.SimPosition),
                    goal.PlayerId, tile.x, tile.y, width, height)) continue;
                best = unit;
                bestPriority = priority;
            }
            return best;
        }

        private static int SemanticClearGap(CommanderSemanticPlacementRelation relation,
            BuildingData anchor, Vector2Int tile, int width, int height)
        {
            if (relation == CommanderSemanticPlacementRelation.MapWest)
                return anchor.OriginTileX - (tile.x + width);
            if (relation == CommanderSemanticPlacementRelation.MapEast)
                return tile.x - (anchor.OriginTileX + anchor.TileFootprintWidth);
            if (tile.x + width <= anchor.OriginTileX)
                return anchor.OriginTileX - (tile.x + width);
            if (tile.x >= anchor.OriginTileX + anchor.TileFootprintWidth)
                return tile.x - (anchor.OriginTileX + anchor.TileFootprintWidth);
            return tile.y + height <= anchor.OriginTileZ
                ? anchor.OriginTileZ - (tile.y + height)
                : tile.y - (anchor.OriginTileZ + anchor.TileFootprintHeight);
        }

        private static bool IsExactSemanticCandidate(BuildStructureGoal goal,
            BuildingData anchor, Vector2Int tile, int width, int height, int actualGap)
        {
            if (actualGap != goal.ClearGapTiles) return false;
            int centeredX = anchor.OriginTileX
                + Mathf.FloorToInt((anchor.TileFootprintWidth - width) / 2f);
            int centeredZ = anchor.OriginTileZ
                + Mathf.FloorToInt((anchor.TileFootprintHeight - height) / 2f);
            switch (goal.PlacementRelation.Value)
            {
                case CommanderSemanticPlacementRelation.MapWest:
                    return tile.x == anchor.OriginTileX - goal.ClearGapTiles - width
                        && tile.y == centeredZ;
                case CommanderSemanticPlacementRelation.MapEast:
                    return tile.x == anchor.OriginTileX + anchor.TileFootprintWidth
                        + goal.ClearGapTiles && tile.y == centeredZ;
                case CommanderSemanticPlacementRelation.Near:
                    return (tile.x == anchor.OriginTileX - goal.ClearGapTiles - width
                            && tile.y == centeredZ)
                        || (tile.x == anchor.OriginTileX + anchor.TileFootprintWidth
                            + goal.ClearGapTiles && tile.y == centeredZ)
                        || (tile.y == anchor.OriginTileZ - goal.ClearGapTiles - height
                            && tile.x == centeredX)
                        || (tile.y == anchor.OriginTileZ + anchor.TileFootprintHeight
                            + goal.ClearGapTiles && tile.x == centeredX);
                default:
                    return false;
            }
        }

        private CommanderPlan PlanGather(CommanderGoal goal, ResourceType resourceType,
            int currentTick, int owned, int queued, string reason,
            ResourceType? preserveGatherersOf = null)
        {
            if (goal.ResourceSourceRestrictions.TryGetValue(resourceType, out var sourceKind)
                && sourceKind != ResourceSourceKind.Any)
                return PlanRestrictedPreparation(goal, resourceType, sourceKind, currentTick, owned, queued, reason, preserveGatherersOf);
            if (currentTick - goal.LastEconomyCommandTick < EconomyCommandCooldownTicks)
                return new CommanderPlan(CommanderGoalStatus.WaitingForResources, reason, owned, queued);

            bool hasVisibleNode = false;
            IReadOnlyList<ResourceNodeData> knownNodes = simulation.MapData.GetAllResourceNodes();
            for (int i = 0; i < knownNodes.Count; i++)
            {
                ResourceNodeData known = knownNodes[i];
                if (known.Type == resourceType && !known.IsDepleted
                    && simulation.FogOfWar.GetVisibility(goal.PlayerId,
                        known.TileX, known.TileZ) == TileVisibility.Visible)
                {
                    hasVisibleNode = true;
                    break;
                }
            }

            UnitData worker = SelectEconomyWorker(goal, resourceType, currentTick,
                out ResourceNodeData node, out bool checkedRoutesUnavailable, preserveGatherersOf);
            if (!hasVisibleNode)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"{reason} No explored non-depleted {resourceType} node is known.", owned, queued);
            if (worker == null)
            {
                if (checkedRoutesUnavailable)
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        $"{reason} No reachable known route was found among the checked worker/resource candidates.",
                        owned, queued);
                if ((goal is EnsureUnitCountGoal || goal is ReachAgeGoal)
                    && !goal.UseIdleWorkersOnly
                    && HasOwnedLivingVillager(goal.PlayerId))
                    return new CommanderPlan(CommanderGoalStatus.WaitingForResources,
                        $"{reason} No eligible owned villager is available; retrying when worker control is released.",
                        owned, queued);
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"{reason} No eligible owned villager is available.", owned, queued);
            }
            if (node == null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"{reason} No suitable visible {resourceType} node is available.", owned, queued);

            return new CommanderPlan(CommanderGoalStatus.WaitingForResources,
                $"{reason} Reassigning villager #{worker.Id} to {resourceType} node #{node.Id}.",
                owned, queued, new GatherCommand(goal.PlayerId, new[] { worker.Id }, node.Id));
        }

        private bool HasOwnedLivingVillager(int playerId)
        {
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId == playerId && unit.IsVillager
                    && unit.CurrentHealth > 0 && unit.State != UnitState.Dead)
                    return true;
            }
            return false;
        }

        private int CountOwnedLivingUnits(int playerId, int unitType)
            => CommanderProductionProjection.LivingOwnedIds(simulation, playerId, unitType).Count;

        private List<int> FindNewLivingUnits(int playerId, int unitType, HashSet<int> baseline)
        {
            var result = new List<int>();
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit != null && unit.PlayerId == playerId && unit.UnitType == unitType
                    && unit.CurrentHealth > 0 && unit.State != UnitState.Dead
                    && (baseline == null || !baseline.Contains(unit.Id)))
                    result.Add(unit.Id);
            }
            result.Sort();
            return result;
        }

        private List<int> FindNewCompletedBuildings(BuildStructureGoal goal)
        {
            var result = new List<int>();
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building != null && building.PlayerId == goal.PlayerId && !building.IsDestroyed
                    && !building.IsUnderConstruction
                    && simulation.GetEffectiveBuildingType(building) == goal.StructureType
                    && !goal.BaselineBuildingIds.Contains(building.Id))
                    result.Add(building.Id);
            }
            result.Sort();
            return result;
        }

        private int CountQueuedUnits(int playerId, int unitType)
        {
            int count = 0;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.PlayerId != playerId || building.IsDestroyed) continue;
                for (int q = 0; q < building.TrainingQueue.Count; q++)
                    if (building.TrainingQueue[q] == unitType) count++;
            }
            return count + simulation.CountPendingTrainingOrders(playerId: playerId, resolvedType: unitType);
        }

        private int CountAllQueuedUnits(int playerId)
        {
            int count = 0;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].PlayerId == playerId && !buildings[i].IsDestroyed)
                    count += buildings[i].TrainingQueue.Count;
            return count + simulation.CountPendingTrainingOrders(playerId: playerId);
        }

        private int ProductionQueueDepth(BuildingData building)
            => building.TrainingQueue.Count + simulation.CountPendingTrainingOrders(producerId: building.Id, playerId: building.PlayerId);

        private BuildingData FindBestAvailableProductionBuilding(EnsureUnitCountGoal goal,
            out bool hasOperationalProducer)
        {
            BuildingData best = null;
            hasOperationalProducer = false;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.PlayerId != goal.PlayerId || building.IsDestroyed || building.IsUnderConstruction
                    || !simulation.IsCompatibleProductionBuilding(goal.PlayerId, building,
                        goal.RequestedUnitType)) continue;
                hasOperationalProducer = true;
                if (ProductionQueueDepth(building) >= goal.MaxQueueDepth) continue;
                if (best == null || ProductionQueueDepth(building) < ProductionQueueDepth(best)
                    || (ProductionQueueDepth(building) == ProductionQueueDepth(best) && building.Id < best.Id))
                    best = building;
            }
            return best;
        }

        private BuildingData FindCompatibleProductionBuilding(int playerId, int requestedUnitType,
            bool underConstruction)
        {
            BuildingData best = null;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.PlayerId != playerId || building.IsDestroyed
                    || building.IsUnderConstruction != underConstruction
                    || !simulation.IsCompatibleProductionBuilding(playerId, building, requestedUnitType))
                    continue;
                if (best == null || building.Id < best.Id) best = building;
            }
            return best;
        }

        private BuildingData FindBuildingById(int playerId, int buildingId)
        {
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.Id == buildingId && building.PlayerId == playerId) return building;
            }
            return null;
        }

        private BuildingData FindOwnedBuilding(int playerId, BuildingType type, bool underConstruction)
        {
            BuildingData best = null;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.PlayerId != playerId || building.IsDestroyed || building.Type != type
                    || building.IsUnderConstruction != underConstruction) continue;
                if (best == null || building.Id < best.Id) best = building;
            }
            return best;
        }

        private BuildingData FindPrimaryTownCenter(int playerId)
        {
            BuildingData best = null;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.PlayerId != playerId || building.IsDestroyed || building.IsUnderConstruction
                    || simulation.GetEffectiveBuildingType(building) != BuildingType.TownCenter) continue;
                if (best == null || (building.IsMainTownCenter && !best.IsMainTownCenter)
                    || (building.IsMainTownCenter == best.IsMainTownCenter && building.Id < best.Id))
                    best = building;
            }
            return best;
        }

        private UnitData SelectBuilder(CommanderGoal goal, int currentTick)
        {
            int playerId = goal.PlayerId;
            UnitData best = null;
            int bestPriority = int.MaxValue;
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId != playerId || !unit.IsVillager || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead) continue;
                if (workerAuthority.IsHumanProtected(unit.Id, currentTick)) continue;
                if (!CanReassignWorker(goal, unit)) continue;
                int priority = unit.State == UnitState.Idle ? 0
                    : workerAuthority.IsCommanderControlled(unit.Id) && IsGatheringState(unit.State) ? 1
                    : IsGatheringState(unit.State) ? 2 : int.MaxValue;
                if (priority < bestPriority || (priority == bestPriority && (best == null || unit.Id < best.Id)))
                {
                    best = unit;
                    bestPriority = priority;
                }
            }
            return bestPriority == int.MaxValue ? null : best;
        }

        private struct CandidatePath
        {
            public UnitData Unit;
            public ResourceNodeData Node;
            public int Priority;
            public int DistanceSq;
        }

        private UnitData SelectEconomyWorker(CommanderGoal goal, ResourceType neededType, int currentTick,
            out ResourceNodeData selectedNode, out bool checkedRoutesUnavailable,
            ResourceType? preserveGatherersOf = null)
        {
            int playerId = goal.PlayerId;
            selectedNode = null;
            checkedRoutesUnavailable = false;

            // Stage 1 (Cheap filter): Collect visible, non-depleted nodes of needed type
            var visibleNodes = new List<ResourceNodeData>();
            IReadOnlyList<ResourceNodeData> allNodes = simulation.MapData.GetAllResourceNodes();
            for (int i = 0; i < allNodes.Count; i++)
            {
                ResourceNodeData node = allNodes[i];
                if (node.Type != neededType) continue;
                if (simulation.FogOfWar.GetVisibility(playerId, node.TileX, node.TileZ) != TileVisibility.Visible)
                    continue;
                if (node.IsDepleted) continue;
                visibleNodes.Add(node);
            }

            if (visibleNodes.Count == 0) return null;

            // Stage 1 (Cheap filter): Collect eligible workers with cheap distance scoring
            var eligibleWorkers = new List<UnitData>();
            var priorities = new Dictionary<int, int>();
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId != playerId || !unit.IsVillager || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead) continue;
                if (workerAuthority.IsHumanProtected(unit.Id, currentTick)) continue;
                if (!CanReassignWorker(goal, unit)) continue;
                if (goal is ResourceAllocationGoal && IsGatheringState(unit.State)
                    && workerAuthority.IsRecentGatherAssignment(unit.Id, currentTick)) continue;
                if (IsGatheringResource(unit, neededType)) continue;
                if (preserveGatherersOf.HasValue
                    && IsGatheringResource(unit, preserveGatherersOf.Value)) continue;

                int priority = unit.State == UnitState.Idle ? 0
                    : workerAuthority.IsCommanderControlled(unit.Id) && IsGatheringState(unit.State) ? 1
                    : IsGatheringState(unit.State) ? 2 : int.MaxValue;
                if (priority == int.MaxValue) continue;

                eligibleWorkers.Add(unit);
                priorities.Add(unit.Id, priority);
            }

            if (eligibleWorkers.Count == 0) return null;

            // Keep only the pairs that can actually reach the bounded validation stage.
            // Building/sorting the full worker x node Cartesian product made planning
            // cost and memory scale with the entire visible map even though only the
            // nearest top-K pairs are ever path-validated.
            int candidateLimit = Mathf.Max(1, pathValidationCandidates);
            var candidates = new List<CandidatePath>(candidateLimit);
            for (int w = 0; w < eligibleWorkers.Count; w++)
            {
                UnitData unit = eligibleWorkers[w];
                int originX = unit.SimPosition.x.Raw >> Fixed32.FractionalBits;
                int originZ = unit.SimPosition.z.Raw >> Fixed32.FractionalBits;
                for (int n = 0; n < visibleNodes.Count; n++)
                {
                    ResourceNodeData node = visibleNodes[n];
                    int dx = node.TileX - originX;
                    int dz = node.TileZ - originZ;
                    CandidatePath candidate = new CandidatePath
                    {
                        Unit = unit,
                        Node = node,
                        Priority = priorities[unit.Id],
                        DistanceSq = dx * dx + dz * dz
                    };

                    if (candidates.Count == candidateLimit
                        && CompareCandidatePath(candidate, candidates[candidates.Count - 1]) >= 0)
                        continue;

                    int insertion = FindCandidateInsertionIndex(candidates, candidate);
                    if (insertion >= candidates.Count) candidates.Add(candidate);
                    else candidates.Insert(insertion, candidate);
                    if (candidates.Count > candidateLimit) candidates.RemoveAt(candidates.Count - 1);
                }
            }

            // Expensive validation is strictly bounded to the top-K ranked pairs.
            int validationCount = Mathf.Min(pathValidationCandidates, candidates.Count);
            for (int c = 0; c < validationCount; c++)
            {
                UnitData unit = candidates[c].Unit;
                ResourceNodeData node = candidates[c].Node;
                Vector2Int workerTile = simulation.MapData.WorldToTile(unit.SimPosition);
                if (HasReachableAdjacentTile(workerTile, playerId, node.TileX,
                    node.TileZ, node.FootprintWidth, node.FootprintHeight))
                {
                    selectedNode = node;
                    return unit;
                }
            }

            // Eligible workers existed, but the bounded path checks found no legal
            // route. Use the existing blocked retry/timeout lifecycle rather than
            // reporting a temporary worker-ownership wait indefinitely.
            checkedRoutesUnavailable = true;
            return null;
        }

        private static int FindCandidateInsertionIndex(List<CandidatePath> candidates, CandidatePath candidate)
        {
            int low = 0;
            int high = candidates.Count;
            while (low < high)
            {
                int middle = low + ((high - low) >> 1);
                if (CompareCandidatePath(candidate, candidates[middle]) < 0) high = middle;
                else low = middle + 1;
            }
            return low;
        }

        private static int CompareCandidatePath(CandidatePath a, CandidatePath b)
        {
            int distance = a.DistanceSq.CompareTo(b.DistanceSq);
            if (distance != 0) return distance;
            int priority = a.Priority.CompareTo(b.Priority);
            if (priority != 0) return priority;
            int worker = a.Unit.Id.CompareTo(b.Unit.Id);
            return worker != 0 ? worker : a.Node.Id.CompareTo(b.Node.Id);
        }

        private UnitData FindActiveConstructionBuilder(int playerId, BuildingData building)
        {
            UnitData best = null;
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId != playerId || !unit.IsVillager || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead || unit.ConstructionTargetBuildingId != building.Id
                    || (unit.State != UnitState.MovingToBuild && unit.State != UnitState.Constructing))
                    continue;
                // An arrived builder must not be masked by a lower-ID travelling builder.
                bool arrived = IsInConstructionRange(unit, building);
                bool bestArrived = best != null && IsInConstructionRange(best, building);
                if (best == null || (arrived && !bestArrived)
                    || (arrived == bestArrived && unit.Id < best.Id)) best = unit;
            }
            return best;
        }

        private static bool IsInConstructionRange(UnitData unit, BuildingData building)
        {
            // Same fixed-point footprint-edge reach and overflow guard as BuildingConstructionSystem.
            Fixed32 nearX = Fixed32.Max(Fixed32.FromInt(building.OriginTileX),
                Fixed32.Min(Fixed32.FromInt(building.OriginTileX + building.TileFootprintWidth), unit.SimPosition.x));
            Fixed32 nearZ = Fixed32.Max(Fixed32.FromInt(building.OriginTileZ),
                Fixed32.Min(Fixed32.FromInt(building.OriginTileZ + building.TileFootprintHeight), unit.SimPosition.z));
            Fixed32 dx = nearX - unit.SimPosition.x;
            Fixed32 dz = nearZ - unit.SimPosition.z;
            return Fixed32.Abs(dx) <= Fixed32.One && Fixed32.Abs(dz) <= Fixed32.One
                && dx * dx + dz * dz <= Fixed32.One;
        }

        private UnitData SelectRecoveryBuilder(CommanderGoal goal, BuildingData building, int currentTick,
            bool allowActiveBuilder)
        {
            int playerId = goal.PlayerId;
            UnitData best = null;
            int bestPriority = int.MaxValue;
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId != playerId || !unit.IsVillager || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead || workerAuthority.IsHumanProtected(unit.Id, currentTick))
                    continue;

                if (!CanReassignWorker(goal, unit)) continue;
                bool activeOnTarget = unit.ConstructionTargetBuildingId == building.Id
                    && (unit.State == UnitState.MovingToBuild || unit.State == UnitState.Constructing);
                int priority = unit.State == UnitState.Idle ? 0
                    : workerAuthority.IsCommanderControlled(unit.Id) && IsGatheringState(unit.State) ? 1
                    : IsGatheringState(unit.State) ? 2
                    : allowActiveBuilder && activeOnTarget ? 3 : int.MaxValue;
                if (priority == int.MaxValue || !CanReachBuilding(unit, building)) continue;
                if (priority < bestPriority || (priority == bestPriority && (best == null || unit.Id < best.Id)))
                {
                    best = unit;
                    bestPriority = priority;
                }
            }
            return best;
        }

        private bool IsGatheringResource(UnitData unit, ResourceType type)
        {
            if (!IsGatheringState(unit.State)) return false;
            ResourceNodeData node = simulation.MapData.GetResourceNode(unit.TargetResourceNodeId);
            return node != null && !node.IsDepleted && node.Type == type;
        }

        private static bool IsGatheringState(UnitState state)
        {
            return state == UnitState.Gathering || state == UnitState.MovingToGather
                || state == UnitState.MovingToDropoff || state == UnitState.DroppingOff;
        }

        private ResourceNodeData FindKnownResourceNode(int playerId, FixedVector3 position, ResourceType type)
        {
            ResourceNodeData best = null;
            int originX = position.x.Raw >> Fixed32.FractionalBits;
            int originZ = position.z.Raw >> Fixed32.FractionalBits;
            int bestDistance = int.MaxValue;
            IReadOnlyList<ResourceNodeData> nodes = simulation.MapData.GetAllResourceNodes();
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNodeData node = nodes[i];
                if (node.Type != type) continue;
                if (simulation.FogOfWar.GetVisibility(playerId, node.TileX, node.TileZ) != TileVisibility.Visible)
                    continue;
                if (node.IsDepleted) continue;
                int dx = node.TileX - originX;
                int dz = node.TileZ - originZ;
                int distance = dx * dx + dz * dz;
                if (distance < bestDistance || (distance == bestDistance && (best == null || node.Id < best.Id)))
                {
                    if (!HasReachableAdjacentTile(simulation.MapData.WorldToTile(position), playerId,
                        node.TileX, node.TileZ, node.FootprintWidth, node.FootprintHeight)) continue;
                    best = node;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private bool TryFindBuildableTile(int playerId, BuildingType type, BuildingData anchor,
            UnitData builder, out Vector2Int result)
        {
            GetFootprint(type, out int width, out int height);
            int centerX = anchor.OriginTileX + anchor.TileFootprintWidth / 2;
            int centerZ = anchor.OriginTileZ + anchor.TileFootprintHeight / 2;
            int border = type == BuildingType.Farm ? 0 : 1;
            Vector2Int start = simulation.MapData.WorldToTile(builder.SimPosition);

            for (int radius = 4; radius <= 20; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                        int tileX = centerX + dx;
                        int tileZ = centerZ + dz;
                        if (!IsVisibleBuildableArea(playerId, tileX, tileZ, width, height, border, type)) continue;
                        if (!HasReachableAdjacentTile(start, playerId, tileX, tileZ, width, height)) continue;
                        result = new Vector2Int(tileX, tileZ);
                        return true;
                    }
                }
            }
            result = new Vector2Int(-1, -1);
            return false;
        }

        private bool IsVisibleBuildableArea(int playerId, int tileX, int tileZ, int width,
            int height, int border, BuildingType type)
        {
            bool farm = type == BuildingType.Farm;
            for (int x = tileX - border; x < tileX + width + border; x++)
            {
                for (int z = tileZ - border; z < tileZ + height + border; z++)
                {
                    if (simulation.FogOfWar.GetVisibility(playerId, x, z) != TileVisibility.Visible)
                        return false;
                    if (farm ? !simulation.MapData.IsBuildableForFarm(x, z) : !simulation.MapData.IsBuildable(x, z))
                        return false;
                }
            }
            return true;
        }

        private bool HasReachableAdjacentTile(Vector2Int start, int playerId, int tileX, int tileZ,
            int width, int height)
        {
            DiagnosticPathCheckCount++;
            for (int x = tileX - 1; x <= tileX + width; x++)
            {
                for (int z = tileZ - 1; z <= tileZ + height; z++)
                {
                    if (x >= tileX && x < tileX + width && z >= tileZ && z < tileZ + height) continue;
                    if (!simulation.MapData.IsWalkable(x, z, playerId, simulation.BuildingRegistry)) continue;
                    Vector2Int destination = new Vector2Int(x, z);
                    if (!GridPathfinder.TryFindCompletePath(simulation.MapData, start, destination,
                        out List<Vector2Int> path, playerId, simulation.BuildingRegistry)) continue;
                    if (IsKnownPath(playerId, start, path)) return true;
                }
            }
            return false;
        }

        private bool CanReachBuilding(UnitData unit, BuildingData building)
        {
            return HasReachableAdjacentTile(simulation.MapData.WorldToTile(unit.SimPosition), unit.PlayerId,
                building.OriginTileX, building.OriginTileZ,
                building.TileFootprintWidth, building.TileFootprintHeight);
        }

        private bool IsKnownPath(int playerId, Vector2Int start, List<Vector2Int> path)
        {
            // GridPathfinder returns smoothed waypoints and omits the start tile.
            // Validate intervening terrain too, using its integer line/corner rules.
            Vector2Int from = start;
            for (int i = 0; i < path.Count; i++)
            {
                if (!IsKnownSegment(playerId, from, path[i])) return false;
                from = path[i];
            }
            return IsKnownTile(playerId, start.x, start.y);
        }

        private bool IsKnownSegment(int playerId, Vector2Int from, Vector2Int to)
        {
            int x = from.x, z = from.y;
            int dx = Mathf.Abs(to.x - x), dz = Mathf.Abs(to.y - z);
            int sx = x < to.x ? 1 : -1, sz = z < to.y ? 1 : -1;
            int error = dx - dz;
            while (true)
            {
                if (!IsKnownTile(playerId, x, z)) return false;
                if (x == to.x && z == to.y) return true;
                int twiceError = 2 * error;
                bool stepX = twiceError > -dz, stepZ = twiceError < dx;
                if (stepX && stepZ && (!IsKnownTile(playerId, x + sx, z)
                    || !IsKnownTile(playerId, x, z + sz))) return false;
                if (stepX) { error -= dz; x += sx; }
                if (stepZ) { error += dx; z += sz; }
            }
        }

        private bool IsKnownTile(int playerId, int x, int z)
        {
            TileVisibility visibility = simulation.FogOfWar.GetVisibility(playerId, x, z);
            return visibility == TileVisibility.Visible || visibility == TileVisibility.Explored;
        }

        private void GetFootprint(BuildingType type, out int width, out int height)
        {
            SimulationConfig config = simulation.Config;
            switch (type)
            {
                case BuildingType.Barracks:
                    width = config.BarracksFootprintWidth; height = config.BarracksFootprintHeight; break;
                case BuildingType.House:
                    width = config.HouseFootprintWidth; height = config.HouseFootprintHeight; break;
                case BuildingType.Stables:
                    width = config.StablesFootprintWidth; height = config.StablesFootprintHeight; break;
                case BuildingType.ArcheryRange:
                    width = config.ArcheryRangeFootprintWidth; height = config.ArcheryRangeFootprintHeight; break;
                case BuildingType.Tower:
                    width = config.TowerFootprintWidth; height = config.TowerFootprintHeight; break;
                case BuildingType.TownCenter:
                    width = config.TownCenterFootprintWidth; height = config.TownCenterFootprintHeight; break;
                case BuildingType.LumberYard:
                    width = config.LumberYardFootprintWidth; height = config.LumberYardFootprintHeight; break;
                case BuildingType.Mine:
                    width = config.MineFootprintWidth; height = config.MineFootprintHeight; break;
                case BuildingType.Monastery:
                    width = config.MonasteryFootprintWidth; height = config.MonasteryFootprintHeight; break;
                case BuildingType.Blacksmith:
                    width = config.BlacksmithFootprintWidth; height = config.BlacksmithFootprintHeight; break;
                case BuildingType.Market:
                    width = config.MarketFootprintWidth; height = config.MarketFootprintHeight; break;
                case BuildingType.University:
                    width = config.UniversityFootprintWidth; height = config.UniversityFootprintHeight; break;
                case BuildingType.SiegeWorkshop:
                    width = config.SiegeWorkshopFootprintWidth; height = config.SiegeWorkshopFootprintHeight; break;
                case BuildingType.Keep:
                    width = config.KeepFootprintWidth; height = config.KeepFootprintHeight; break;
                case BuildingType.Landmark:
                    width = config.LandmarkFootprintWidth; height = config.LandmarkFootprintHeight; break;
                case BuildingType.Farm:
                    width = config.FarmFootprintWidth; height = config.FarmFootprintHeight; break;
                case BuildingType.Mill:
                    width = config.MillFootprintWidth; height = config.MillFootprintHeight; break;
                default:
                    width = 2; height = 2; break;
            }
        }
    }
}
