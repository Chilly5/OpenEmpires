using System.Collections.Generic;

namespace OpenEmpires
{
    internal sealed class CommanderWorkerAuthority
    {
        // Thirty simulation seconds at the current 30 Hz tick rate. This is long enough
        // to avoid the Commander visibly fighting a fresh manual order while remaining a
        // temporary lease that eventually returns scarce workers to goal planning.
        internal const int HumanProtectionTicks = 900;

        private readonly GameSimulation simulation;
        private readonly int playerId;
        private readonly Dictionary<int, int> humanProtectedUntilTick = new Dictionary<int, int>();
        private readonly HashSet<int> commanderControlledWorkers = new HashSet<int>();
        private readonly Dictionary<int, int> commanderGatherUntilTick = new Dictionary<int, int>();
        private readonly Dictionary<int, CommanderWorkerReservation> reservations = new Dictionary<int, CommanderWorkerReservation>();
        private readonly List<int> releaseScratch = new List<int>();

        public CommanderWorkerReservation? GetReservation(int workerId) =>
            reservations.TryGetValue(workerId, out var value) ? value : (CommanderWorkerReservation?)null;

        public bool CanUseWorker(int workerId, int goalId, int currentTick)
        {
            if (IsHumanProtected(workerId, currentTick)) return false;
            return !reservations.TryGetValue(workerId, out var reservation) || reservation.GoalId == goalId;
        }

        public bool TryReserve(int workerId, int goalId, CommanderWorkerReservationType role, int currentTick)
        {
            UnitData unit = simulation.UnitRegistry.GetUnit(workerId);
            if (goalId <= 0 || unit == null || unit.PlayerId != playerId || !unit.IsVillager
                || unit.CurrentHealth <= 0 || unit.State == UnitState.Dead || unit.CommandQueue.Count > 0
                || !CanUseWorker(workerId, goalId, currentTick)) return false;
            if (reservations.TryGetValue(workerId, out var existing) && existing.ReservationType == role) return true;
            reservations[workerId] = new CommanderWorkerReservation(workerId, playerId, goalId, role, currentTick);
            return true;
        }

        public bool TryReserveCommand(CommanderGoal goal, ICommand command, int currentTick)
        {
            int[] workers = GetSubjectUnitIds(command);
            if (workers == null) return true;
            // Check every subject before taking any reservation (no partial acquisition).
            for (int i = 0; i < workers.Length; i++)
            {
                var unit = simulation.UnitRegistry.GetUnit(workers[i]);
                if (unit == null || unit.PlayerId != playerId || !unit.IsVillager || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead || unit.CommandQueue.Count > 0
                    || !CanUseWorker(unit.Id, goal.GoalId, currentTick)) return false;
            }
            var role = command is GatherCommand ? CommanderWorkerReservationType.Gatherer : CommanderWorkerReservationType.Builder;
            for (int i = 0; i < workers.Length; i++) TryReserve(workers[i], goal.GoalId, role, currentTick);
            return true;
        }

        public void ReleaseGoal(int goalId)
        {
            releaseScratch.Clear();
            foreach (var pair in reservations) if (pair.Value.GoalId == goalId) releaseScratch.Add(pair.Key);
            for (int i = 0; i < releaseScratch.Count; i++) reservations.Remove(releaseScratch[i]);
        }

        public void PruneUnavailableWorkers()
        {
            releaseScratch.Clear();
            foreach (var pair in reservations)
            {
                var unit = simulation.UnitRegistry.GetUnit(pair.Key);
                if (unit == null || unit.PlayerId != playerId || !unit.IsVillager || unit.CurrentHealth <= 0 || unit.State == UnitState.Dead)
                    releaseScratch.Add(pair.Key);
            }
            for (int i = 0; i < releaseScratch.Count; i++) reservations.Remove(releaseScratch[i]);
        }

        public CommanderWorkerAuthority(GameSimulation simulation, int playerId)
        {
            this.simulation = simulation;
            this.playerId = playerId;
        }

        public void ObserveEnqueuedCommand(ICommand command, CommandEnqueueSource source, int currentTick)
        {
            int[] unitIds = GetSubjectUnitIds(command);
            if (unitIds == null || command.PlayerId != playerId) return;

            for (int i = 0; i < unitIds.Length; i++)
            {
                UnitData unit = simulation.UnitRegistry.GetUnit(unitIds[i]);
                if (unit == null || unit.PlayerId != playerId || !unit.IsVillager) continue;

                if (source == CommandEnqueueSource.Commander)
                {
                    humanProtectedUntilTick.Remove(unit.Id);
                    commanderControlledWorkers.Add(unit.Id);
                    if (command is GatherCommand) commanderGatherUntilTick[unit.Id] = currentTick + 300;
                    else commanderGatherUntilTick.Remove(unit.Id);
                }
                else
                {
                    reservations.Remove(unit.Id);
                    commanderControlledWorkers.Remove(unit.Id);
                    commanderGatherUntilTick.Remove(unit.Id);
                    humanProtectedUntilTick[unit.Id] = currentTick + HumanProtectionTicks;
                }
            }
        }

        public bool IsHumanProtected(int unitId, int currentTick)
        {
            if (!humanProtectedUntilTick.TryGetValue(unitId, out int protectedUntil)) return false;
            if (currentTick < protectedUntil) return true;
            humanProtectedUntilTick.Remove(unitId);
            return false;
        }

        public bool IsCommanderControlled(int unitId)
        {
            return commanderControlledWorkers.Contains(unitId);
        }

        public bool IsRecentGatherAssignment(int unitId, int currentTick)
        {
            return commanderGatherUntilTick.TryGetValue(unitId, out int until) && currentTick < until;
        }

        internal static int[] GetSubjectUnitIds(ICommand command)
        {
            switch (command)
            {
                case MoveCommand value: return value.UnitIds;
                case GatherCommand value: return value.UnitIds;
                case StopCommand value: return value.UnitIds;
                case AttackBuildingCommand value: return value.UnitIds;
                case AttackUnitCommand value: return value.UnitIds;
                case ConstructBuildingCommand value: return value.UnitIds;
                case DropOffCommand value: return value.UnitIds;
                case GarrisonCommand value: return value.UnitIds;
                case PatrolCommand value: return value.UnitIds;
                case DeleteUnitsCommand value: return value.UnitIds;
                case FollowUnitCommand value: return value.UnitIds;
                case HealUnitCommand value: return value.UnitIds;
                case RepairBuildingCommand value: return value.UnitIds;
                case PlaceBuildingCommand value: return value.VillagerUnitIds;
                case PlaceWallCommand value: return value.VillagerUnitIds;
                case SlaughterSheepCommand value: return value.VillagerIds;
                default: return null;
            }
        }
    }
}
