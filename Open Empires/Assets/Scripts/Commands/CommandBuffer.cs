using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum CommandEnqueueSource
    {
        Human,
        Commander
    }

    public class CommandBuffer
    {
        private readonly List<ICommand> pendingCommands = new List<ICommand>();
        private readonly List<ICommand> executingCommands = new List<ICommand>();
        public event Action<ICommand, CommandEnqueueSource> CommandEnqueued;

        public void EnqueueCommand(ICommand command)
        {
            EnqueueCommand(command, CommandEnqueueSource.Human);
        }

        public void EnqueueCommand(ICommand command, CommandEnqueueSource source)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            pendingCommands.Add(command);
            CommandEnqueued?.Invoke(command, source);
        }

        public List<ICommand> FlushCommands()
        {
            executingCommands.Clear();
            executingCommands.AddRange(pendingCommands);
            pendingCommands.Clear();
            return executingCommands;
        }

        // Only an exact still-pending boxed command can be removed. This never
        // changes an unrelated human order or a command already executed.
        internal bool RemovePendingExact(ICommand command)
        {
            for (int i = 0; i < pendingCommands.Count; i++)
                if (ReferenceEquals(pendingCommands[i], command))
                {
                    pendingCommands.RemoveAt(i);
                    return true;
                }
            return false;
        }

        // Shared command-subject extraction for human takeover and local native
        // order-affinity checks. This does not change command serialization.
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
