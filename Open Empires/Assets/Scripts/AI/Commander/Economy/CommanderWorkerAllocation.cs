using System;

namespace OpenEmpires
{
    public enum CommanderWorkerAllocationMode { TargetTotal, Additional, SelectedCount }
    public enum CommanderWorkerCountMode { Exact, AllMatching }
    public enum CommanderWorkerState { Any, Idle, Gathering }

    public sealed class CommanderWorkerSelector
    {
        public CommanderWorkerState State { get; }
        public ResourceType? CurrentResource { get; }
        public CommanderWorkerSelector(CommanderWorkerState state, ResourceType? currentResource = null)
        { State = state; CurrentResource = currentResource; }
    }

    public sealed class CommanderResourceDestination
    {
        public ResourceType Resource { get; }
        public ResourceSourceKind SourceKind { get; }
        public CommanderResourceDestination(ResourceType resource, ResourceSourceKind sourceKind = ResourceSourceKind.Any)
        { Resource = resource; SourceKind = sourceKind; }
    }

    // Detached semantic values; validation is shared by parsing, DTO admission and runtime admission.
    public sealed class CommanderWorkerAllocation
    {
        public CommanderWorkerAllocationMode Mode { get; }
        public CommanderWorkerCountMode CountMode { get; }
        public int? Count { get; }
        public CommanderWorkerSelector Workers { get; }
        public CommanderResourceDestination Destination { get; }
        public CommanderWorkerAllocation(CommanderWorkerAllocationMode mode, CommanderWorkerCountMode countMode,
            int? count, CommanderWorkerSelector workers, CommanderResourceDestination destination)
        { Mode = mode; CountMode = countMode; Count = count; Workers = workers; Destination = destination; }

        internal bool IsValid(int maximumPopulation)
        {
            if (!Enum.IsDefined(typeof(CommanderWorkerAllocationMode), Mode)
                || !Enum.IsDefined(typeof(CommanderWorkerCountMode), CountMode)
                || Workers == null || Destination == null
                || !Enum.IsDefined(typeof(CommanderWorkerState), Workers.State)
                || !Enum.IsDefined(typeof(ResourceType), Destination.Resource)
                || !Enum.IsDefined(typeof(ResourceSourceKind), Destination.SourceKind)) return false;
            if (Workers.CurrentResource.HasValue && (Workers.State != CommanderWorkerState.Gathering
                || !Enum.IsDefined(typeof(ResourceType), Workers.CurrentResource.Value))) return false;
            if (Mode == CommanderWorkerAllocationMode.TargetTotal && Workers.State != CommanderWorkerState.Any) return false;
            if (CountMode == CommanderWorkerCountMode.Exact)
            {
                if (!Count.HasValue || Count.Value < 1 || Count.Value > Math.Min(200, maximumPopulation)) return false;
            }
            else if (Count.HasValue || Mode != CommanderWorkerAllocationMode.SelectedCount
                || Workers.State == CommanderWorkerState.Any) return false;
            switch (Destination.SourceKind)
            {
                case ResourceSourceKind.Any: return true;
                case ResourceSourceKind.Sheep:
                case ResourceSourceKind.Berries:
                case ResourceSourceKind.Farm: return Destination.Resource == ResourceType.Food;
                case ResourceSourceKind.Tree: return Destination.Resource == ResourceType.Wood;
                case ResourceSourceKind.GoldMine: return Destination.Resource == ResourceType.Gold;
                case ResourceSourceKind.StoneMine: return Destination.Resource == ResourceType.Stone;
                default: return false;
            }
        }
    }
}
