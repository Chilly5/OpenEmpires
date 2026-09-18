using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4C2")]
    public sealed class CommanderPhase4C2Tests
    {
        [Test]
        public void Explanation_MatchesDecisionReason()
        {
            var context = new ExplanationContext(playerId: 0, decisionId: 17,
                decisionTick: 450, outcome: ExplanationOutcome.Rejected,
                reason: "Insufficient available gold.", requestedObjective: "AttackPreparation");

            ExplanationResult result = new CommanderExplanationService().Explain(context);

            Assert.That(result.DisplayText, Does.Contain("Insufficient available gold."));
            Assert.That(result.DisplayText, Does.Contain("450"));
            Assert.That(result.DisplayText, Does.Contain("Historical decision tick"));
            Assert.That(result.DisplayText, Does.Not.Contain("gold income"));
            Assert.That(result.Outcome, Is.EqualTo(ExplanationOutcome.Rejected));
        }

        [TestCase(ExplanationOutcome.NoDecision, "No recorded decision is available")]
        [TestCase(ExplanationOutcome.Rejected, "recorded decision was rejected")]
        [TestCase(ExplanationOutcome.TransitionRefused, "blocked before submission")]
        [TestCase(ExplanationOutcome.PlannerRejected, "planner rejected the submitted intent")]
        [TestCase(ExplanationOutcome.PlanCreated, "created plan #8 (CavalryPressure)")]
        [TestCase(ExplanationOutcome.SelectionNotSubmitted, "selected but was not submitted")]
        public void Outcomes_HaveHandSpecifiedMeaning(ExplanationOutcome outcome, string meaning)
        {
            var context = new ExplanationContext(0, 4, 90, outcome, "recorded reason",
                "AttackPreparation", 8, "CavalryPressure");

            Assert.That(new CommanderExplanationService().Explain(context).DisplayText,
                Does.Contain(meaning));
        }

        [Test]
        public void RejectedPlanHasReason()
        {
            var context = new ExplanationContext(0, 2, 100,
                ExplanationOutcome.PlannerRejected, "No feasible placement was found.",
                "DefensivePreparation");

            string text = new CommanderExplanationService().Explain(context).DisplayText;

            Assert.That(text, Does.Contain("No feasible placement was found."));
            Assert.That(text, Does.Contain("DefensivePreparation"));
            Assert.That(text, Does.Contain("No accepted plan was created"));
        }

        [Test]
        public void MissingReason_StatesEvidenceIsUnavailable()
        {
            var context = new ExplanationContext(0, 2, 100, ExplanationOutcome.Rejected);

            Assert.That(new CommanderExplanationService().Explain(context).DisplayText,
                Does.Contain("No recorded reason is available"));
        }

        [Test]
        public void Context_CopiesAndBoundsPrimitiveInputs()
        {
            var source = new List<ExplanationPlanState>();
            for (int i = 40; i >= 1; i--)
                source.Add(new ExplanationPlanState(i, new string('p', 700), new string('s', 700),
                    new string('m', 700), new string('t', 700), new string('r', 700)));
            var context = new ExplanationContext(0, reason: new string('x', 700),
                requestedObjective: new string('o', 700), acceptedPlanType: new string('a', 700),
                currentSnapshotTick: 12, currentPlans: source);
            source.Clear();

            Assert.That(context.Reason.Length, Is.EqualTo(512));
            Assert.That(context.RequestedObjective.Length, Is.EqualTo(512));
            Assert.That(context.AcceptedPlanType.Length, Is.EqualTo(512));
            Assert.That(context.CurrentPlans.Count, Is.EqualTo(32));
            Assert.That(context.CurrentPlans[0].PlanType.Length, Is.EqualTo(512));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<ExplanationPlanState>)context.CurrentPlans).Add(
                    new ExplanationPlanState(99, null, null, null, null, null)));
        }

        [Test]
        public void Context_RejectsInvalidOwnerIdentityTicksAndEnums()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0, decisionId: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0, decisionTick: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0, acceptedPlanId: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0, currentSnapshotTick: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0,
                outcome: (ExplanationOutcome)99));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ExplanationPlanState(-1, null, null, null, null, null));
        }

        [Test]
        public void CurrentPlan_IsSortedAndDistinguishesUnavailableEmptyAndCapped()
        {
            var service = new CommanderExplanationService();
            var unavailable = new ExplanationContext(0);
            var empty = new ExplanationContext(0, currentSnapshotTick: 45,
                currentPlans: Array.Empty<ExplanationPlanState>());
            var plans = Enumerable.Range(1, 32).Reverse().Select(i =>
                new ExplanationPlanState(i, "Type" + i, "Active", "Step" + i,
                    "InProgress", "Observed" + i)).ToList();
            var capped = new ExplanationContext(0, currentSnapshotTick: 46, currentPlans: plans);

            Assert.That(service.Explain(unavailable, CommanderExplanationQuery.CurrentPlan).DisplayText,
                Does.Contain("Current plan state is unavailable"));
            Assert.That(service.Explain(empty, CommanderExplanationQuery.CurrentPlan).DisplayText,
                Does.Contain("No active plan was observed at current snapshot tick 45"));
            string rendered = service.Explain(capped, CommanderExplanationQuery.CurrentPlan).DisplayText;
            Assert.That(rendered.IndexOf("Plan #1 ", StringComparison.Ordinal),
                Is.LessThan(rendered.IndexOf("Plan #2 ", StringComparison.Ordinal)));
            Assert.That(rendered, Does.Contain("showing up to 32 plans"));
            Assert.That(rendered, Does.Contain("observed current milestone"));
            Assert.That(rendered, Does.Not.Contain("will"));
        }

        [Test]
        public void QuerySemantics_DoNotFabricateRejectionOrAttackAttribution()
        {
            var service = new CommanderExplanationService();
            var created = new ExplanationContext(0, 1, 2, ExplanationOutcome.PlanCreated,
                "Accepted.", "DefensivePreparation", 5, "DefensivePreparation");
            var unknown = new ExplanationContext(0, 2, 3, ExplanationOutcome.Rejected,
                "Insufficient available gold.");

            Assert.That(service.Explain(created, CommanderExplanationQuery.LastRejection).DisplayText,
                Does.Contain("not a rejection"));
            string attack = service.Explain(unknown, CommanderExplanationQuery.AttackReason).DisplayText;
            Assert.That(attack, Does.Contain("No recorded rejection is attributable to AttackPreparation"));
            Assert.That(attack, Does.Not.Contain("Insufficient available gold."));
            Assert.That(attack, Does.Not.Contain("income"));
        }

        [Test]
        public void Rendering_IsInvariantAndBounded()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fa-IR");
                var context = new ExplanationContext(0, 17, 450, ExplanationOutcome.Rejected,
                    new string('z', 512), "AttackPreparation");
                var result = new CommanderExplanationService().Explain(context);
                Assert.That(result.DisplayText, Does.Contain("450"));
                Assert.That(result.DisplayText.Length, Is.LessThanOrEqualTo(8192));
                Assert.That(new ExplanationResult(new string('q', 9000),
                    ExplanationOutcome.NoDecision).DisplayText.Length, Is.EqualTo(8192));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void Service_RejectsNullContextAndInvalidQuery()
        {
            var service = new CommanderExplanationService();
            Assert.Throws<ArgumentNullException>(() => service.Explain(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.Explain(
                new ExplanationContext(0), (CommanderExplanationQuery)99));
        }
    }
}
