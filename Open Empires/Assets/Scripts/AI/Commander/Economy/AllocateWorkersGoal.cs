using System.Collections.Generic;

namespace OpenEmpires
{
    internal enum CommanderWorkerTargetKind { ResourceNode, OwnedSheep }
    internal readonly struct CommanderWorkerAssignment
    {
        internal readonly int WorkerId, TargetId;
        internal readonly CommanderWorkerTargetKind Kind;
        internal CommanderWorkerAssignment(int worker, int target, CommanderWorkerTargetKind kind)
        { WorkerId = worker; TargetId = target; Kind = kind; }
    }

    public sealed class AllocateWorkersGoal : CommanderGoal
    {
        public CommanderWorkerAllocation Allocation { get; }
        internal bool Prepared, ReservationsAcquired, SnapshotCaptured, BaselineCaptured, HumanInterrupted;
        internal int BaselineCount, NextCommandGroup, LastIssuedSimulationTick = -1;
        internal readonly List<int> SnapshotWorkerIds = new List<int>();
        internal readonly List<int> SelectedWorkerIds = new List<int>();
        internal readonly List<CommanderWorkerAssignment> Assignments = new List<CommanderWorkerAssignment>();
        internal readonly List<ICommand> CommandGroups = new List<ICommand>();
        internal AllocateWorkersGoal(int playerId, CommanderWorkerAllocation allocation, int maxDurationTicks = 36000)
            : base(playerId, CommanderGoalType.AllocateWorkers, maxDurationTicks) => Allocation = allocation;
    }
}
