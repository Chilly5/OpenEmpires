namespace OpenEmpires
{
    public enum CommanderIntentResolutionStatus
    {
        GoalCreated,
        Rejected,
        ExecutionNotImplemented
    }

    public sealed class CommanderIntentResolution
    {
        public CommanderIntentResolutionStatus Status { get; }
        public CommanderIntent Intent { get; }
        public CommanderGoal Goal { get; }
        public CommanderIntentErrorCode ErrorCode { get; }
        public string Reason { get; }
        public bool CreatedGoal => Status == CommanderIntentResolutionStatus.GoalCreated;

        public CommanderIntentResolution(CommanderIntentResolutionStatus status,
            CommanderIntent intent, CommanderGoal goal, CommanderIntentErrorCode errorCode,
            string reason)
        {
            Status = status;
            Intent = intent;
            Goal = goal;
            ErrorCode = errorCode;
            Reason = reason ?? string.Empty;
        }
    }

    public sealed class CommanderIntentResolver
    {
        private readonly CommanderIntentValidator validator;

        public CommanderIntentResolver(CommanderIntentValidator validator = null)
        {
            this.validator = validator ?? new CommanderIntentValidator();
        }

        public CommanderIntentResolution Resolve(CommanderIntent intent,
            GameSimulation simulation, CommanderGoalManager goalManager)
        {
            if (goalManager == null)
                return Rejected(intent, CommanderIntentErrorCode.InvalidPlayer,
                    "The Commander goal manager is unavailable.");

            CommanderIntentValidationResult validation = validator.Validate(
                intent, simulation, goalManager.PlayerId);
            if (!validation.IsValid)
                return Rejected(intent, validation.ErrorCode, validation.Reason);

            if (intent is EnsureUnitCountIntent ensure)
                return ResolveEnsureUnitCount(ensure, goalManager);

            if (intent is SetResourceAllocationIntent allocation)
            {
                try
                {
                    return Created(intent, goalManager.SubmitResourceAllocation(allocation.Resource,
                        allocation.WorkerCount, allocation.Mode, constraints: intent.Constraints));
                }
                catch (System.ArgumentOutOfRangeException)
                {
                    return Rejected(intent, CommanderIntentErrorCode.AmountOutOfRange,
                        "Increased worker target exceeds maximum population.");
                }
            }
            if (intent is BuildStructureIntent build)
                return Created(intent, goalManager.SubmitBuildStructure(build.StructureType,
                    build.Count, constraints: intent.Constraints));

            return Rejected(intent, CommanderIntentErrorCode.UnknownCommand,
                "The Commander intent type is not recognized.");
        }

        private static CommanderIntentResolution ResolveEnsureUnitCount(
            EnsureUnitCountIntent intent, CommanderGoalManager goalManager)
        {
            int maximumQueue = 3;
            for (int i = 0; i < intent.Constraints.Count; i++)
            {
                if (intent.Constraints[i] is MaximumQueueConstraint queue)
                {
                    maximumQueue = queue.MaximumQueue;
                    continue;
                }
            }

            EnsureUnitCountGoal goal = goalManager.SubmitEnsureUnitCount(
                intent.UnitType, intent.TargetTotal, maximumQueue, constraints: intent.Constraints);
            return Created(intent, goal);
        }

        private static CommanderIntentResolution Created(CommanderIntent intent, CommanderGoal goal)
        {
            return new CommanderIntentResolution(CommanderIntentResolutionStatus.GoalCreated,
                intent, goal, CommanderIntentErrorCode.None, string.Empty);
        }

        private static CommanderIntentResolution Rejected(CommanderIntent intent,
            CommanderIntentErrorCode errorCode, string reason)
        {
            return new CommanderIntentResolution(CommanderIntentResolutionStatus.Rejected,
                intent, null, errorCode, reason);
        }

    }
}
