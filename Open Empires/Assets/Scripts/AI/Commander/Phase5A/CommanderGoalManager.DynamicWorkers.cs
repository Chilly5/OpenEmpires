using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager
    {
        private void PrepareDynamicWorkerBindings(CommanderSemanticGraphPlan plan,
            IReadOnlyDictionary<int, CommanderGoal> pending)
        {
            var program = plan.DynamicProgram;
            if (program == null) return;
            var nodes = program.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            var selections = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
            var sourceClaims = new HashSet<int>();
            var effectClaims = new HashSet<int>();
            var effectOwners = new Dictionary<int, CommanderGoal>();

            IReadOnlyList<int> ResolveWorkers(string id)
            {
                if (selections.TryGetValue(id, out var captured)) return captured;
                var node = nodes[id];
                int count = node.Parameter<int>("count");
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
                {
                    string state = node.Parameters.TryGetValue("state", out var value) ? (string)value : "Any";
                    ResourceType? resource = node.Parameters.TryGetValue("currentResource", out var resourceValue)
                        ? (ResourceType)Enum.Parse(typeof(ResourceType), (string)resourceValue) : (ResourceType?)null;
                    bool Matches(UnitData worker)
                    {
                        if (worker == null || worker.PlayerId != playerId || !worker.IsVillager || worker.IsSheep
                            || worker.CurrentHealth <= 0 || worker.State == UnitState.Dead
                            || worker.CommandQueue.Count > 0 || sourceClaims.Contains(worker.Id)
                            || !workerAuthority.CanUseWorker(worker.Id, nextGoalId, simulation.CurrentTick)) return false;
                        bool gathering = worker.State == UnitState.Gathering || worker.State == UnitState.MovingToGather
                            || worker.State == UnitState.MovingToDropoff || worker.State == UnitState.DroppingOff;
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
                    ids = simulation.UnitRegistry.GetAllUnits().Where(Matches).OrderBy(w => w.Id)
                        .Take(count).Select(w => w.Id).ToArray();
                    if (ids.Length != count)
                        throw new InvalidOperationException("The full shared worker selection is unavailable; no goals or reservations were admitted.");
                    foreach (int worker in ids) sourceClaims.Add(worker);
                }
                else throw new InvalidOperationException("The worker reference is incompatible.");
                var frozen = Array.AsReadOnly(ids);
                selections.Add(id, frozen);
                return frozen;
            }

            // Freeze source queries once, in documented AST order, before partitioning.
            foreach (var source in program.Nodes)
                if (source.Primitive.Mechanic == CommanderDynamicMechanic.SelectWorkers)
                    ResolveWorkers(source.Id);
            foreach (var effect in plan.Nodes)
            {
                var goal = pending[effect.Index];
                var node = nodes[effect.DynamicNodeId];
                if (goal is BuildStructureGoal build) build.HasResultConsumer = true;
                if (!node.Inputs.TryGetValue("workers", out string workers)) continue;
                var frozen = ResolveWorkers(workers);
                foreach (int worker in frozen)
                {
                    if (!effectClaims.Add(worker))
                        throw new InvalidOperationException("Effectful worker roles overlap; no partial admission is allowed.");
                    effectOwners.Add(worker, goal);
                }
                goal.FrozenWorkerIds = frozen;
            }

            // Floors constrain the combined transfers, not each branch independently.
            // No reservations or registrations have happened at this point.
            foreach (var floor in program.Constraints.OfType<ProtectedResourceConstraint>())
            {
                int current = planner.CountResourceWorkers(playerId, floor.Resource);
                int minimum = floor.MinimumWorkers ?? current;
                int moving = 0;
                foreach (int id in effectClaims)
                {
                    if (effectOwners[id] is AllocateWorkersGoal allocation
                        && allocation.Allocation.Destination.Resource == floor.Resource) continue;
                    var worker = simulation.UnitRegistry.GetUnit(id);
                    var node = simulation.MapData.GetResourceNode(worker.TargetResourceNodeId);
                    bool gathering = worker.State == UnitState.Gathering || worker.State == UnitState.MovingToGather
                        || worker.State == UnitState.MovingToDropoff || worker.State == UnitState.DroppingOff;
                    if (gathering && node != null && node.Type == floor.Resource) moving++;
                }
                if (moving > 0 && current - moving < minimum)
                    throw new InvalidOperationException("The complete shared worker transfer violates a protected resource floor; nothing was admitted.");
            }
        }
    }
}
