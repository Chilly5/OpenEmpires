using System;

namespace OpenEmpires
{
    public sealed class DefensiveTurtlePlanTemplate : IStrategicPlanTemplate
    {
        public const string Id = "defensive_turtle";
        public string TemplateId => Id;

        public bool CanHandle(StrategicIntent intent) => intent != null
            && intent.ObjectiveType == StrategicObjectiveType.DefensiveTurtle;

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
                    "Fortified defense accepts no parameters.");
            return StrategicIntentValidationResult.Accepted(this);
        }

        public StrategicPlan CreatePlan(StrategicIntent intent)
        {
            StrategicIntentValidationResult validation = ValidateParameters(intent);
            if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(intent));
            return new DefensiveTurtlePlan(intent.PlayerId, intent.IntentId);
        }
    }
}
