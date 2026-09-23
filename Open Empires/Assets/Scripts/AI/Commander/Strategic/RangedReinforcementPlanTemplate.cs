using System;

namespace OpenEmpires
{
    public sealed class RangedReinforcementPlanTemplate : IStrategicPlanTemplate
    {
        public const string Id = "ranged_reinforcement";
        public string TemplateId => Id;

        public bool CanHandle(StrategicIntent intent) => intent != null
            && intent.ObjectiveType == StrategicObjectiveType.RangedReinforcement;

        public StrategicIntentValidationResult ValidateParameters(StrategicIntent intent)
        {
            if (intent == null)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.MissingIntent, "A strategic intent is required.");
            if (!CanHandle(intent))
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.NoCompatibleTemplate,
                    "No available strategic plan template.");
            if (intent.Parameters.Count != 0)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.UnsupportedParameter,
                    "Ranged reinforcement accepts no parameters.");
            return StrategicIntentValidationResult.Accepted(this);
        }

        public StrategicPlan CreatePlan(StrategicIntent intent)
        {
            StrategicIntentValidationResult validation = ValidateParameters(intent);
            if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(intent));
            return new RangedReinforcementPlan(intent.PlayerId, intent.IntentId);
        }
    }
}
