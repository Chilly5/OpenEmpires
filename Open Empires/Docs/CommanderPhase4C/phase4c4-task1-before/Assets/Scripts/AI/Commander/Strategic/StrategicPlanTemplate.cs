using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public interface IStrategicPlanTemplate
    {
        string TemplateId { get; }
        bool CanHandle(StrategicIntent intent);
        StrategicIntentValidationResult ValidateParameters(StrategicIntent intent);
        StrategicPlan CreatePlan(StrategicIntent intent);
    }

    public sealed class StrategicPlanRegistry
    {
        private readonly List<IStrategicPlanTemplate> templates =
            new List<IStrategicPlanTemplate>();

        public IReadOnlyList<IStrategicPlanTemplate> Templates => templates;

        public static StrategicPlanRegistry CreateDefault()
        {
            var registry = new StrategicPlanRegistry();
            registry.Register(new CavalryPressurePlanTemplate());
            registry.Register(new DefensivePreparationPlanTemplate());
            registry.Register(new EconomicExpansionPlanTemplate());
            registry.Register(new MilitaryReinforcementPlanTemplate());
            return registry;
        }

        public void Register(IStrategicPlanTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (string.IsNullOrWhiteSpace(template.TemplateId))
                throw new ArgumentException("A strategic plan template requires an identifier.",
                    nameof(template));
            for (int i = 0; i < templates.Count; i++)
                if (string.Equals(templates[i].TemplateId, template.TemplateId,
                    StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Strategic plan template '{template.TemplateId}' is already registered.");
            templates.Add(template);
        }

        public IStrategicPlanTemplate FindCompatibleTemplate(StrategicIntent intent)
        {
            if (intent == null) return null;
            for (int i = 0; i < templates.Count; i++)
                if (templates[i].CanHandle(intent)) return templates[i];
            return null;
        }

        public StrategicPlan CreatePlan(StrategicIntent intent)
        {
            IStrategicPlanTemplate template = FindCompatibleTemplate(intent);
            if (template == null)
                throw new InvalidOperationException("No available strategic plan template.");
            StrategicIntentValidationResult validation = template.ValidateParameters(intent);
            if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(intent));
            StrategicPlan plan = template.CreatePlan(intent);
            return plan ?? throw new InvalidOperationException(
                $"Strategic plan template '{template.TemplateId}' returned no plan.");
        }
    }

    public sealed class CavalryPressurePlanTemplate : IStrategicPlanTemplate
    {
        public const string Id = "CavalryPressure";
        public string TemplateId => Id;

        public bool CanHandle(StrategicIntent intent)
        {
            return intent != null
                && intent.ObjectiveType == StrategicObjectiveType.AttackPreparation;
        }

        public StrategicIntentValidationResult ValidateParameters(StrategicIntent intent)
        {
            if (intent == null)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.MissingIntent,
                    "A strategic intent is required.");
            if (!CanHandle(intent))
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.NoCompatibleTemplate,
                    "No available strategic plan template.");
            var paramValidation = StrategicPlanTemplateValidation.ValidateCommonParameters(intent);
            if (paramValidation != null) return paramValidation;
            return StrategicIntentValidationResult.Accepted(this);
        }

        public StrategicPlan CreatePlan(StrategicIntent intent)
        {
            StrategicIntentValidationResult validation = ValidateParameters(intent);
            if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(intent));
            return new CavalryPressurePlan(intent.PlayerId, intent.IntentId);
        }
    }

    public sealed class DefensivePreparationPlanTemplate : IStrategicPlanTemplate
    {
        public const string Id = "DefensivePreparation";
        public string TemplateId => Id;

        public bool CanHandle(StrategicIntent intent)
        {
            return intent != null
                && intent.ObjectiveType == StrategicObjectiveType.DefensivePreparation;
        }

        public StrategicIntentValidationResult ValidateParameters(StrategicIntent intent)
        {
            if (intent == null)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.MissingIntent,
                    "A strategic intent is required.");
            if (!CanHandle(intent))
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.NoCompatibleTemplate,
                    "No available strategic plan template.");
            var paramValidation = StrategicPlanTemplateValidation.ValidateCommonParameters(intent);
            if (paramValidation != null) return paramValidation;
            return StrategicIntentValidationResult.Accepted(this);
        }

        public StrategicPlan CreatePlan(StrategicIntent intent)
        {
            StrategicIntentValidationResult validation = ValidateParameters(intent);
            if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(intent));
            return new DefensivePreparationPlan(intent.PlayerId, intent.IntentId);
        }
    }

    public sealed class EconomicExpansionPlanTemplate : IStrategicPlanTemplate
    {
        public const string Id = "EconomicExpansion";
        public string TemplateId => Id;

        public bool CanHandle(StrategicIntent intent)
        {
            return intent != null
                && intent.ObjectiveType == StrategicObjectiveType.EconomicExpansion;
        }

        public StrategicIntentValidationResult ValidateParameters(StrategicIntent intent)
        {
            if (intent == null)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.MissingIntent,
                    "A strategic intent is required.");
            if (!CanHandle(intent))
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.NoCompatibleTemplate,
                    "No available strategic plan template.");
            var paramValidation = StrategicPlanTemplateValidation.ValidateCommonParameters(intent);
            if (paramValidation != null) return paramValidation;
            return StrategicIntentValidationResult.Accepted(this);
        }

        public StrategicPlan CreatePlan(StrategicIntent intent)
        {
            StrategicIntentValidationResult validation = ValidateParameters(intent);
            if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(intent));
            return new EconomicExpansionPlan(intent.PlayerId, intent.IntentId);
        }
    }

    public sealed class MilitaryReinforcementPlanTemplate : IStrategicPlanTemplate
    {
        public const string Id = "MilitaryReinforcement";
        public string TemplateId => Id;

        public bool CanHandle(StrategicIntent intent)
        {
            return intent != null
                && intent.ObjectiveType == StrategicObjectiveType.MilitaryReinforcement;
        }

        public StrategicIntentValidationResult ValidateParameters(StrategicIntent intent)
        {
            if (intent == null)
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.MissingIntent,
                    "A strategic intent is required.");
            if (!CanHandle(intent))
                return StrategicIntentValidationResult.Rejected(
                    StrategicIntentValidationError.NoCompatibleTemplate,
                    "No available strategic plan template.");
            var paramValidation = StrategicPlanTemplateValidation.ValidateCommonParameters(intent);
            if (paramValidation != null) return paramValidation;
            return StrategicIntentValidationResult.Accepted(this);
        }

        public StrategicPlan CreatePlan(StrategicIntent intent)
        {
            StrategicIntentValidationResult validation = ValidateParameters(intent);
            if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(intent));
            return new MilitaryReinforcementPlan(intent.PlayerId, intent.IntentId);
        }
    }

    internal static class StrategicPlanTemplateValidation
    {
        public static StrategicIntentValidationResult ValidateCommonParameters(StrategicIntent intent)
        {
            if (intent?.Parameters != null && intent.Parameters.TryGetValue("targetCount", out string targetCountStr))
            {
                if (!int.TryParse(targetCountStr, out int count) || count <= 0)
                    return StrategicIntentValidationResult.Rejected(
                        StrategicIntentValidationError.UnsupportedParameter,
                        "Parameter 'targetCount' must be a positive integer.");
            }
            return null;
        }
    }
}
