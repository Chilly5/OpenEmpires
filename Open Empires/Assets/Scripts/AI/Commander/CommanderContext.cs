using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // Detached values only: no registry, Unity object, goal or simulation references.
    public sealed class CommanderContext
    {
        public int PlayerId { get; }
        public int SnapshotTick { get; }
        public CommanderResourceSnapshot Resources { get; }
        public int Population { get; }
        public int PopulationCap { get; }
        public int MaximumPopulation { get; }
        public int AvailableCapacity => Math.Max(0, PopulationCap - Population);
        public int Age { get; }
        public string Civilization { get; }
        public IReadOnlyList<CommanderBuildingSnapshot> Buildings { get; }
        public IReadOnlyList<CommanderUnitSnapshot> Units { get; }
        public IReadOnlyList<CommanderBuildingSnapshot> Production { get; }
        public IReadOnlyList<string> UnlockedTechnologies { get; }
        public IReadOnlyList<CommanderGoalSnapshot> ActiveGoals { get; }
        public IReadOnlyList<CommanderVisibleResourceSnapshot> VisibleResources { get; }
        public IReadOnlyList<CommanderUnitOptionSnapshot> UnitOptions { get; }
        public IReadOnlyList<CommanderWorkerAllocationSnapshot> WorkerAllocation { get; }
        public IReadOnlyList<CommanderVisibleEnemyMilitarySnapshot> VisibleEnemyMilitary { get; }
        public string EnemyAwarenessPolicy =>
            "Currently visible enemy military aggregates only; no hidden, explored-only, or predicted enemy data.";
        public string ToJson() => Newtonsoft.Json.JsonConvert.SerializeObject(this,
            new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None });

        internal CommanderContext(int playerId, int tick, CommanderResourceSnapshot resources,
            int population, int cap, int maximum, int age, string civilization,
            List<CommanderBuildingSnapshot> buildings, List<CommanderUnitSnapshot> units,
            List<CommanderBuildingSnapshot> production, List<string> technologies,
            List<CommanderGoalSnapshot> goals, List<CommanderVisibleResourceSnapshot> visibleResources,
            List<CommanderUnitOptionSnapshot> unitOptions,
            List<CommanderWorkerAllocationSnapshot> workerAllocation,
            List<CommanderVisibleEnemyMilitarySnapshot> visibleEnemyMilitary)
        {
            PlayerId = playerId; SnapshotTick = tick; Resources = resources;
            Population = population; PopulationCap = cap; MaximumPopulation = maximum;
            Age = age; Civilization = civilization;
            Buildings = buildings.AsReadOnly(); Units = units.AsReadOnly();
            Production = production.AsReadOnly(); UnlockedTechnologies = technologies.AsReadOnly();
            ActiveGoals = goals.AsReadOnly(); VisibleResources = visibleResources.AsReadOnly();
            UnitOptions = unitOptions.AsReadOnly();
            WorkerAllocation = workerAllocation.AsReadOnly();
            VisibleEnemyMilitary = visibleEnemyMilitary.AsReadOnly();
        }
    }

    public sealed class CommanderResourceSnapshot
    {
        public int Food { get; }
        public int Wood { get; }
        public int Gold { get; }
        public int Stone { get; }
        internal CommanderResourceSnapshot(int food, int wood, int gold, int stone)
        { Food = food; Wood = wood; Gold = gold; Stone = stone; }
    }

    public sealed class CommanderBuildingSnapshot
    {
        public int BuildingId { get; }
        public string Type { get; }
        public string EffectiveType { get; }
        public bool IsUnderConstruction { get; }
        public bool IsCompleted => !IsUnderConstruction;
        public bool CanProduce => TrainableUnitTypes.Count > 0;
        public IReadOnlyList<int> TrainableUnitTypes { get; }
        public IReadOnlyList<int> TrainingQueue { get; }
        public int? CurrentlyTrainingUnit => IsCompleted && TrainingQueue.Count > 0 ? TrainingQueue[0] : (int?)null;
        public int TrainingTicksRemaining { get; }
        internal CommanderBuildingSnapshot(int id, string type, string effective, bool construction,
            List<int> trainable, List<int> queue, int remaining)
        {
            BuildingId = id; Type = type; EffectiveType = effective; IsUnderConstruction = construction;
            TrainableUnitTypes = trainable.AsReadOnly(); TrainingQueue = queue.AsReadOnly();
            TrainingTicksRemaining = remaining;
        }
    }

    public sealed class CommanderUnitSnapshot
    {
        public int UnitType { get; }
        public int Count { get; }
        public int QueuedCount { get; }
        internal CommanderUnitSnapshot(int type, int count, int queued)
        { UnitType = type; Count = count; QueuedCount = queued; }
    }

    public sealed class CommanderUnitOptionSnapshot
    {
        public string IntentUnit { get; }
        public int ResolvedUnitType { get; }
        public int RequiredAge { get; }
        public bool AgeUnlocked { get; }
        internal CommanderUnitOptionSnapshot(string name, int resolved, int requiredAge, int currentAge)
        { IntentUnit = name; ResolvedUnitType = resolved; RequiredAge = requiredAge; AgeUnlocked = currentAge >= requiredAge; }
    }

    public sealed class CommanderGoalSnapshot
    {
        public int GoalId { get; }
        public int PlayerId { get; }
        public int CreatedTick { get; }
        public int Priority { get; }
        public int? ParentGoalId { get; }
        public string Lifecycle { get; }
        public string Type { get; }
        public string Status { get; }
        public string Target { get; }
        public int Amount { get; }
        internal CommanderGoalSnapshot(int id, string type, string status, string target, int amount,
            int playerId, int createdTick, int priority, int? parentGoalId, string lifecycle)
        {
            GoalId = id; Type = type; Status = status; Target = target; Amount = amount;
            PlayerId = playerId; CreatedTick = createdTick; Priority = priority; ParentGoalId = parentGoalId; Lifecycle = lifecycle;
        }
    }

    public sealed class CommanderVisibleResourceSnapshot
    {
        public string Type { get; }
        public int TileX { get; }
        public int TileZ { get; }
        public int RemainingAmount { get; }
        internal CommanderVisibleResourceSnapshot(string type, int x, int z, int amount)
        { Type = type; TileX = x; TileZ = z; RemainingAmount = amount; }
    }

    public sealed class CommanderWorkerAllocationSnapshot
    {
        public ResourceType ResourceType { get; }
        public int AssignedWorkers { get; }

        internal CommanderWorkerAllocationSnapshot(ResourceType resourceType, int assignedWorkers)
        {
            ResourceType = resourceType;
            AssignedWorkers = Math.Max(0, assignedWorkers);
        }
    }

    public sealed class CommanderVisibleEnemyMilitarySnapshot
    {
        public int UnitType { get; }
        public int VisibleCount { get; }

        internal CommanderVisibleEnemyMilitarySnapshot(int unitType, int visibleCount)
        {
            UnitType = unitType;
            VisibleCount = Math.Max(0, visibleCount);
        }
    }
}
