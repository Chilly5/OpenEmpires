namespace OpenEmpires
{
    public sealed class CommanderResponseGenerator
    {
        public string GenerateInterpretationRejection(CommanderIntentInterpretation interpretation)
        {
            return "I could not understand that command. " + interpretation.Reason;
        }

        public string GenerateResolutionResponse(CommanderIntentResolution resolution)
        {
            if (resolution.Status == CommanderIntentResolutionStatus.Rejected)
                return "I cannot accept that request. " + resolution.Reason;

            if (resolution.Status == CommanderIntentResolutionStatus.ExecutionNotImplemented)
                return "I understood the request, but cannot execute it yet. " + resolution.Reason;

            if (resolution.Intent is EnsureUnitCountIntent ensure)
            {
                string units = CommanderIntentCatalog.GetUnitDisplayName(
                    ensure.UnitType, plural: ensure.TargetTotal != 1);
                return $"Understood.\nPreparing {ensure.TargetTotal} {units}.";
            }

            if (resolution.Intent is BuildStructureIntent build)
                return $"Understood. I will construct {build.Count} {CommanderIntentCatalog.GetStructureDisplayName(build.StructureType).ToLowerInvariant()}.";
            if (resolution.Goal is ResourceAllocationGoal allocation)
                return $"Understood. I will assign at least {allocation.TargetWorkers} villagers to {allocation.Resource.ToString().ToLowerInvariant()}.";

            return "Understood.";
        }

        public string GenerateGoalResponse(CommanderGoalEvent goalEvent, CommanderIntent intent)
        {
            switch (goalEvent.EventType)
            {
                case CommanderGoalEventType.GoalCompleted:
                    if (intent is EnsureUnitCountIntent ensure)
                    {
                        string units = CommanderIntentCatalog.GetUnitDisplayName(ensure.UnitType, plural: ensure.TargetTotal != 1);
                        return ensure.TargetTotal == 1 ? $"Your 1 {units} is ready."
                            : $"Your {ensure.TargetTotal} {units} are ready.";
                    }
                    if (intent is BuildStructureIntent build)
                        return build.Count == 1
                            ? $"The {CommanderIntentCatalog.GetStructureDisplayName(build.StructureType).ToLowerInvariant()} is complete."
                            : $"The {build.Count} requested structures are complete.";
                    if (goalEvent.Goal is ResourceAllocationGoal allocation)
                        return $"At least {allocation.TargetWorkers} villagers are assigned to {allocation.Resource.ToString().ToLowerInvariant()}.";
                    return null;
                case CommanderGoalEventType.GoalBlocked:
                    return "I am blocked. " + goalEvent.Goal.StatusReason;
                case CommanderGoalEventType.GoalFailed:
                    return "I could not complete the request. " + goalEvent.Goal.StatusReason;
                case CommanderGoalEventType.GoalCancelled:
                    return "The Commander request was cancelled.";
                case CommanderGoalEventType.GoalProgressChanged:
                    if (goalEvent.Goal.Status == CommanderGoalStatus.WaitingForResources
                        || goalEvent.Goal.Status == CommanderGoalStatus.WaitingForPrerequisite)
                        return "I am waiting. " + goalEvent.Goal.StatusReason;
                    return null;
                default:
                    return null;
            }
        }
    }
}
