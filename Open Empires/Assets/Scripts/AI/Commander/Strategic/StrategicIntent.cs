using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenEmpires
{
    public enum StrategicObjectiveType
    {
        AttackPreparation,
        DefensivePreparation,
        EconomicExpansion,
        MilitaryReinforcement
    }

    public enum StrategicIntentStatus
    {
        Created,
        Active,
        Rejected,
        Completed,
        Failed,
        Cancelled
    }

    public sealed class StrategicIntent : ICommanderIntentRequest
    {
        private readonly ReadOnlyDictionary<string, string> parameters;

        public int IntentId { get; }
        public int PlayerId { get; }
        public StrategicObjectiveType ObjectiveType { get; }
        public int CreatedTick { get; }
        public IReadOnlyDictionary<string, string> Parameters => parameters;
        public int? Priority { get; }
        public StrategicIntentStatus Status { get; internal set; }
        public string StatusReason { get; internal set; } = string.Empty;
        public CommanderIntentLayer IntentLayer => CommanderIntentLayer.Strategic;

        public StrategicIntent(int intentId, int playerId, StrategicObjectiveType objectiveType,
            int createdTick, IDictionary<string, string> parameters = null, int? priority = null)
        {
            if (intentId < 1) throw new ArgumentOutOfRangeException(nameof(intentId));
            if (createdTick < 0) throw new ArgumentOutOfRangeException(nameof(createdTick));

            var detached = new Dictionary<string, string>(StringComparer.Ordinal);
            if (parameters != null)
            {
                foreach (KeyValuePair<string, string> parameter in parameters)
                {
                    if (string.IsNullOrWhiteSpace(parameter.Key))
                        throw new ArgumentException("Strategic parameter names cannot be empty.",
                            nameof(parameters));
                    detached.Add(parameter.Key, parameter.Value ?? string.Empty);
                }
            }

            IntentId = intentId;
            PlayerId = playerId;
            ObjectiveType = objectiveType;
            CreatedTick = createdTick;
            this.parameters = new ReadOnlyDictionary<string, string>(detached);
            Priority = priority;
            Status = StrategicIntentStatus.Created;
        }
    }

    public enum StrategicIntentValidationError
    {
        None,
        MissingIntent,
        PlayerMismatch,
        UnknownObjective,
        InvalidPriority,
        UnsupportedParameter,
        NoCompatibleTemplate,
        DuplicateIntent,
        TemplateCreationFailed
    }

    public sealed class StrategicIntentValidationResult
    {
        public bool IsValid => Error == StrategicIntentValidationError.None;
        public StrategicIntentValidationError Error { get; }
        public string Reason { get; }
        public IStrategicPlanTemplate Template { get; }

        private StrategicIntentValidationResult(StrategicIntentValidationError error,
            string reason, IStrategicPlanTemplate template)
        {
            Error = error;
            Reason = reason ?? string.Empty;
            Template = template;
        }

        public static StrategicIntentValidationResult Accepted(IStrategicPlanTemplate template) =>
            new StrategicIntentValidationResult(StrategicIntentValidationError.None,
                string.Empty, template);

        public static StrategicIntentValidationResult Rejected(
            StrategicIntentValidationError error, string reason) =>
            new StrategicIntentValidationResult(error, reason, null);
    }

    public sealed class StrategicIntentValidator
    {
        public StrategicIntentValidationResult Validate(StrategicIntent intent,
            int expectedPlayerId, StrategicPlanRegistry registry)
        {
            if (intent == null)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.MissingIntent,
                    "A strategic intent is required.");
            if (intent.PlayerId != expectedPlayerId)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.PlayerMismatch,
                    "Strategic intent ownership does not match the local Commander.");
            if (!Enum.IsDefined(typeof(StrategicObjectiveType), intent.ObjectiveType))
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.UnknownObjective,
                    "The strategic objective type is not recognized.");
            if (intent.Priority.HasValue
                && (intent.Priority.Value < 0 || intent.Priority.Value > 100))
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.InvalidPriority,
                    "Strategic intent priority must be between 0 and 100.");
            if (registry == null)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.NoCompatibleTemplate,
                    "No available strategic plan template.");

            IStrategicPlanTemplate template = registry.FindCompatibleTemplate(intent);
            if (template == null)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.NoCompatibleTemplate,
                    "No available strategic plan template.");

            StrategicIntentValidationResult parameterValidation =
                template.ValidateParameters(intent);
            return parameterValidation.IsValid
                ? StrategicIntentValidationResult.Accepted(template)
                : parameterValidation;
        }
    }

    public enum StrategicIntentSubmissionStatus
    {
        PlanCreated,
        Rejected
    }

    public sealed class StrategicIntentSubmission
    {
        public StrategicIntentSubmissionStatus Status { get; }
        public StrategicIntent Intent { get; }
        public StrategicPlan Plan { get; }
        public StrategicIntentValidationError Error { get; }
        public string Reason { get; }
        public bool CreatedPlan => Status == StrategicIntentSubmissionStatus.PlanCreated
            && Plan != null;

        internal StrategicIntentSubmission(StrategicIntentSubmissionStatus status,
            StrategicIntent intent, StrategicPlan plan, StrategicIntentValidationError error,
            string reason)
        {
            Status = status;
            Intent = intent;
            Plan = plan;
            Error = error;
            Reason = reason ?? string.Empty;
        }
    }
}
