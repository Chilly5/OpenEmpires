using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed class CommanderIntentValidationResult
    {
        public bool IsValid { get; }
        public CommanderIntentErrorCode ErrorCode { get; }
        public string Reason { get; }

        private CommanderIntentValidationResult(bool isValid,
            CommanderIntentErrorCode errorCode, string reason)
        {
            IsValid = isValid;
            ErrorCode = errorCode;
            Reason = reason ?? string.Empty;
        }

        public static CommanderIntentValidationResult Valid()
        {
            return new CommanderIntentValidationResult(true,
                CommanderIntentErrorCode.None, string.Empty);
        }

        public static CommanderIntentValidationResult Invalid(
            CommanderIntentErrorCode errorCode, string reason)
        {
            return new CommanderIntentValidationResult(false, errorCode, reason);
        }
    }

    public sealed class CommanderIntentValidator
    {
        public const int MaximumStructureCount = 20;
        public const int MaximumQueuePolicy = 8;

        public CommanderIntentValidationResult Validate(CommanderIntent intent,
            GameSimulation simulation, int owningPlayerId)
        {
            if (intent == null)
                return Invalid(CommanderIntentErrorCode.UnknownCommand,
                    "No Commander intent was provided.");
            if (simulation == null)
                return Invalid(CommanderIntentErrorCode.UnknownCommand,
                    "The simulation is unavailable.");
            if (intent.PlayerId < 0 || intent.PlayerId >= simulation.PlayerCount)
                return Invalid(CommanderIntentErrorCode.InvalidPlayer,
                    "The requested player is not part of this match.");
            if (intent.PlayerId != owningPlayerId)
                return Invalid(CommanderIntentErrorCode.PlayerMismatch,
                    "Commander intents can only target the owning local player.");

            CommanderIntentValidationResult constraints = ValidateConstraints(intent.Constraints);
            if (!constraints.IsValid) return constraints;
            for (int i = 0; i < intent.Constraints.Count; i++)
            {
                if (intent.Constraints[i] is MaximumQueueConstraint && !(intent is EnsureUnitCountIntent))
                    return Invalid(CommanderIntentErrorCode.UnsupportedConstraint,
                        "A maximum queue constraint applies only to unit production.");
                if (intent.Constraints[i] is ProtectedResourceConstraint resource
                    && resource.MinimumWorkers > simulation.Config.MaxPopulation)
                    return Invalid(CommanderIntentErrorCode.AmountOutOfRange,
                        "Protected worker minimum exceeds maximum population.");
            }

            if (intent is EnsureUnitCountIntent ensure)
                return ValidateEnsureUnitCount(ensure, simulation.Config.MaxPopulation);
            if (intent is SetResourceAllocationIntent allocation)
                return ValidateResourceAllocation(allocation, simulation.Config.MaxPopulation);
            if (intent is BuildStructureIntent build)
                return ValidateBuildStructure(build);

            return Invalid(CommanderIntentErrorCode.UnknownCommand,
                "The Commander intent type is not recognized.");
        }

        private static CommanderIntentValidationResult ValidateEnsureUnitCount(
            EnsureUnitCountIntent intent, int maximumPopulation)
        {
            if (!CommanderIntentCatalog.IsSupportedUnit(intent.UnitType))
                return Invalid(CommanderIntentErrorCode.UnknownUnit,
                    $"Unit type {intent.UnitType} is not supported by the intent catalog.");
            if (intent.TargetTotal < 1 || intent.TargetTotal > maximumPopulation)
                return Invalid(CommanderIntentErrorCode.AmountOutOfRange,
                    $"Unit target must be between 1 and {maximumPopulation}.");
            return CommanderIntentValidationResult.Valid();
        }

        private static CommanderIntentValidationResult ValidateResourceAllocation(
            SetResourceAllocationIntent intent, int maximumPopulation)
        {
            if (!Enum.IsDefined(typeof(ResourceType), intent.Resource))
                return Invalid(CommanderIntentErrorCode.UnknownResource,
                    "The requested resource is not recognized.");
            if (!Enum.IsDefined(typeof(ResourceAllocationMode), intent.Mode))
                return Invalid(CommanderIntentErrorCode.UnknownCommand, "Unknown allocation mode.");
            if (intent.Mode == ResourceAllocationMode.SetExact && !intent.WorkerCount.HasValue)
                return Invalid(CommanderIntentErrorCode.MissingAmount,
                    "An exact resource assignment needs a worker count.");
            if (intent.WorkerCount.HasValue
                && (intent.WorkerCount.Value < 0 || intent.WorkerCount.Value > maximumPopulation))
            {
                return Invalid(CommanderIntentErrorCode.AmountOutOfRange,
                    $"Worker count must be between 0 and {maximumPopulation}.");
            }
            return CommanderIntentValidationResult.Valid();
        }

        private static CommanderIntentValidationResult ValidateBuildStructure(
            BuildStructureIntent intent)
        {
            if (!CommanderIntentCatalog.IsSupportedStructure(intent.StructureType))
                return Invalid(CommanderIntentErrorCode.UnknownStructure,
                    "The requested structure is not supported by the intent catalog.");
            if (intent.Count < 1 || intent.Count > MaximumStructureCount)
                return Invalid(CommanderIntentErrorCode.AmountOutOfRange,
                    $"Structure count must be between 1 and {MaximumStructureCount}.");
            return CommanderIntentValidationResult.Valid();
        }

        private static CommanderIntentValidationResult ValidateConstraints(
            IReadOnlyList<CommanderConstraint> constraints)
        {
            var seen = new HashSet<CommanderConstraintType>();
            for (int i = 0; i < constraints.Count; i++)
            {
                CommanderConstraint constraint = constraints[i];
                if (constraint == null || !seen.Add(constraint.Type))
                    return Invalid(CommanderIntentErrorCode.UnsupportedConstraint,
                        "Duplicate or empty Commander constraints are not allowed.");
                if (!(constraint is MaximumQueueConstraint) && !(constraint is ProtectedResourceConstraint)
                    && !(constraint is PreferredWorkersConstraint))
                    return Invalid(CommanderIntentErrorCode.UnsupportedConstraint, "Unknown constraint implementation.");

                if (constraint is MaximumQueueConstraint maximumQueue
                    && (maximumQueue.MaximumQueue < 1
                        || maximumQueue.MaximumQueue > MaximumQueuePolicy))
                {
                    return Invalid(CommanderIntentErrorCode.AmountOutOfRange,
                        $"Maximum queue must be between 1 and {MaximumQueuePolicy}.");
                }
                if (constraint is ProtectedResourceConstraint protectedResource
                    && (!Enum.IsDefined(typeof(ResourceType), protectedResource.Resource)
                        || protectedResource.MinimumWorkers < 0))
                {
                    return Invalid(CommanderIntentErrorCode.UnknownResource,
                        "The protected resource is not recognized.");
                }
                if (constraint is PreferredWorkersConstraint preferredWorkers
                    && !Enum.IsDefined(typeof(CommanderPreferredWorkerSource),
                        preferredWorkers.WorkerSource))
                {
                    return Invalid(CommanderIntentErrorCode.UnsupportedConstraint,
                        "The preferred worker source is not recognized.");
                }
            }
            return CommanderIntentValidationResult.Valid();
        }

        private static CommanderIntentValidationResult Invalid(
            CommanderIntentErrorCode errorCode, string reason)
        {
            return CommanderIntentValidationResult.Invalid(errorCode, reason);
        }
    }
}
