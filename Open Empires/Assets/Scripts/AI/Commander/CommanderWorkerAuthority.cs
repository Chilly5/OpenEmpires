using System.Collections.Generic;

namespace OpenEmpires
{
    internal sealed class CommanderWorkerAuthority
    {
        // Fifteen simulation seconds at the current 60 Hz tick rate. This is long enough
        // to avoid the Commander visibly fighting a fresh manual order while remaining a
        // temporary lease that eventually returns scarce workers to goal planning.
        internal const int HumanProtectionTicks = 900;

        private readonly GameSimulation simulation;
        private readonly int playerId;
        private readonly Dictionary<int, int> humanProtectedUntilTick = new Dictionary<int, int>();
        private readonly HashSet<int> commanderControlledWorkers = new HashSet<int>();

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
                }
                else
                {
                    commanderControlledWorkers.Remove(unit.Id);
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

        private static int[] GetSubjectUnitIds(ICommand command)
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
