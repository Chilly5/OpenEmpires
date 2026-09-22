# Phase 4C.2 fix-round 1 review package

Scoped package for the two independent-review findings. Prior versions were extracted from `phase4c2-review-package.md` using exact path headings and fenced `csharp` delimiters; extraction succeeded for all three files. No source was modified.

## Findings

1. **Important — A recorded `NoDecision` event is conflated with complete absence of evidence.** The host deliberately projects a real `NoDecision` event and copies its decision ID, historical tick, outcome reason, and objective into `ExplanationContext` (`Docs/CommanderPhase4C/phase4c2-review-package.md:775-813`, especially lines 786-787 and 809-812). The renderer then returns only `No recorded decision is available.` for every `NoDecision` context (`Docs/CommanderPhase4C/phase4c2-review-package.md:1029-1033`), discarding the copied ID, tick, and recorded reason. This violates the binding requirements to preserve the original outcome/reason/ID/tick, cover all six outcomes with their actual meaning, and allow an initially empty source to describe an observed `NoDecision`. The same conflation makes empty-context `LastRejection` and `AttackReason` responses claim that the latest recorded outcome was `NoDecision` even when no event was recorded (`Docs/CommanderPhase4C/phase4c2-review-package.md:999-1011`). The focused outcome test hard-codes this conflation by passing a decision ID, tick, and reason while expecting the unavailable message (`Docs/CommanderPhase4C/phase4c2-review-package.md:1147-1159`). Distinguish a truly empty context from an observed `NoDecision`; render the observed event's bounded reason and historical metadata, while all decision-derived queries over a truly empty context report evidence as unavailable.

2. **Minor — The focused tests do not exercise all required source-copy and host-projection cases.** The value test proves collection copying only by clearing the caller's list (`Docs/CommanderPhase4C/phase4c2-review-package.md:1185-1205`), and the host mutation test proves that asking questions does not mutate pending intent or gameplay state (`Docs/CommanderPhase4C/phase4c2-review-package.md:1401-1450`). Neither mutates an original decision/intent/plan after host projection and proves the captured explanation remains unchanged, as required by the binding verification contract. In addition, the six-outcome test constructs `ExplanationContext` directly (`Docs/CommanderPhase4C/phase4c2-review-package.md:1147-1159`), so it does not verify the host classification branches for `TransitionRefused`, `PlannerRejected`, or `SelectionNotSubmitted` at `Docs/CommanderPhase4C/phase4c2-review-package.md:775-793`. Static inspection shows primitive copying, so this is an evidence-quality gap rather than a demonstrated authority defect.

## Exact changed-file hashes

| Path | Bytes | SHA-256 |
|---|---:|---|
| Assets/Scripts/AI/Commander/Phase4C/CommanderExplanationService.cs | 7717 | 02820AAF58CD193D35A2E448D711E4C9586E4D513C80A765CABD6EE2F2EFCA01 |
| Assets/Tests/EditMode/CommanderPhase4C2Tests.cs | 11904 | 046BBA408E061EC5E7BE6E1CE510D5548B88AB9E1DD44AF233658556C3E7B9C4 |
| Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs | 20781 | 96DE63C4E7D34593FDB3AC50EA0BA39CBD21FBCEC4B85A35020F769582DB12C5 |

## Exact unified diff: Assets/Scripts/AI/Commander/Phase4C/CommanderExplanationService.cs

```diff
diff --git "a/C:\\Users\\RS\\AppData\\Local\\Temp\\phase4c2-fixround1-7e4bbafb2104409ba074314d36a68a3b\\CommanderExplanationService.cs" b/Assets/Scripts/AI/Commander/Phase4C/CommanderExplanationService.cs
index 0a4769d..eb70c27 100644
--- "a/C:\\Users\\RS\\AppData\\Local\\Temp\\phase4c2-fixround1-7e4bbafb2104409ba074314d36a68a3b\\CommanderExplanationService.cs"
+++ b/Assets/Scripts/AI/Commander/Phase4C/CommanderExplanationService.cs
@@ -13,6 +13,7 @@ namespace OpenEmpires
             if (context == null) throw new ArgumentNullException(nameof(context));
             if (!Enum.IsDefined(typeof(CommanderExplanationQuery), query))
                 throw new ArgumentOutOfRangeException(nameof(query));
+            bool hasRecordedDecisionEvidence = HasRecordedDecisionEvidence(context);
             string text;
             switch (query)
             {
@@ -20,13 +21,18 @@ namespace OpenEmpires
                     text = RenderDecision(context);
                     break;
                 case CommanderExplanationQuery.LastRejection:
-                    text = IsRejection(context.Outcome)
+                    text = !hasRecordedDecisionEvidence
+                        ? "No recorded decision or rejection evidence is available."
+                        : IsRejection(context.Outcome)
                         ? RenderDecision(context)
                         : "The latest recorded outcome was " + context.Outcome
                             + ", not a rejection.";
                     break;
                 case CommanderExplanationQuery.AttackReason:
-                    text = IsRejection(context.Outcome)
+                    text = !hasRecordedDecisionEvidence
+                        ? "No recorded decision or rejection evidence is available for "
+                            + "AttackPreparation."
+                        : IsRejection(context.Outcome)
                         && string.Equals(context.RequestedObjective, "AttackPreparation",
                             StringComparison.Ordinal)
                         ? RenderDecision(context)
@@ -49,14 +55,27 @@ namespace OpenEmpires
                 || outcome == ExplanationOutcome.PlannerRejected;
         }
 
+        private static bool HasRecordedDecisionEvidence(ExplanationContext context)
+        {
+            return context.DecisionId.HasValue
+                || context.DecisionTick.HasValue
+                || context.Reason.Length > 0
+                || context.RequestedObjective.Length > 0
+                || context.AcceptedPlanId.HasValue
+                || context.AcceptedPlanType.Length > 0;
+        }
+
         private static string RenderDecision(ExplanationContext context)
         {
-            if (context.Outcome == ExplanationOutcome.NoDecision)
+            if (!HasRecordedDecisionEvidence(context))
                 return "No recorded decision is available.";
 
             var result = new StringBuilder();
             switch (context.Outcome)
             {
+                case ExplanationOutcome.NoDecision:
+                    result.Append("No strategic decision was selected.");
+                    break;
                 case ExplanationOutcome.Rejected:
                     result.Append("The recorded decision was rejected.");
                     break;
@@ -134,4 +153,4 @@ namespace OpenEmpires
             return string.IsNullOrEmpty(value) ? "unavailable" : value;
         }
     }
-}
\ No newline at end of file
+}
```

## Exact unified diff: Assets/Tests/EditMode/CommanderPhase4C2Tests.cs

```diff
diff --git "a/C:\\Users\\RS\\AppData\\Local\\Temp\\phase4c2-fixround1-7e4bbafb2104409ba074314d36a68a3b\\CommanderPhase4C2Tests.cs" b/Assets/Tests/EditMode/CommanderPhase4C2Tests.cs
index 8766ff0..66e2263 100644
--- "a/C:\\Users\\RS\\AppData\\Local\\Temp\\phase4c2-fixround1-7e4bbafb2104409ba074314d36a68a3b\\CommanderPhase4C2Tests.cs"
+++ b/Assets/Tests/EditMode/CommanderPhase4C2Tests.cs
@@ -25,7 +25,7 @@ namespace OpenEmpires.Tests
             Assert.That(result.Outcome, Is.EqualTo(ExplanationOutcome.Rejected));
         }
 
-        [TestCase(ExplanationOutcome.NoDecision, "No recorded decision is available")]
+        [TestCase(ExplanationOutcome.NoDecision, "No strategic decision was selected")]
         [TestCase(ExplanationOutcome.Rejected, "recorded decision was rejected")]
         [TestCase(ExplanationOutcome.TransitionRefused, "blocked before submission")]
         [TestCase(ExplanationOutcome.PlannerRejected, "planner rejected the submitted intent")]
@@ -63,6 +63,39 @@ namespace OpenEmpires.Tests
                 Does.Contain("No recorded reason is available"));
         }
 
+        [Test]
+        public void PristineContext_DecisionQueriesStateThatRecordedEvidenceIsUnavailable()
+        {
+            var service = new CommanderExplanationService();
+            var context = new ExplanationContext(0);
+
+            Assert.That(service.Explain(context, CommanderExplanationQuery.LastDecision).DisplayText,
+                Is.EqualTo("No recorded decision is available."));
+            Assert.That(service.Explain(context, CommanderExplanationQuery.LastRejection).DisplayText,
+                Is.EqualTo("No recorded decision or rejection evidence is available."));
+            Assert.That(service.Explain(context, CommanderExplanationQuery.AttackReason).DisplayText,
+                Is.EqualTo("No recorded decision or rejection evidence is available for AttackPreparation."));
+        }
+
+        [Test]
+        public void RecordedNoDecision_PreservesOutcomeReasonIdAndHistoricalTick()
+        {
+            var context = new ExplanationContext(0, decisionId: 23, decisionTick: 811,
+                outcome: ExplanationOutcome.NoDecision,
+                reason: "No eligible strategic recommendation was available.",
+                requestedObjective: "AttackPreparation");
+
+            ExplanationResult result = new CommanderExplanationService().Explain(context);
+
+            Assert.That(result.Outcome, Is.EqualTo(ExplanationOutcome.NoDecision));
+            Assert.That(result.DisplayText, Does.Contain("No strategic decision was selected."));
+            Assert.That(result.DisplayText, Does.Contain("Decision ID: 23."));
+            Assert.That(result.DisplayText, Does.Contain("Historical decision tick: 811."));
+            Assert.That(result.DisplayText,
+                Does.Contain("Recorded reason: No eligible strategic recommendation was available."));
+            Assert.That(result.DisplayText, Does.Contain("Requested objective: AttackPreparation."));
+        }
+
         [Test]
         public void Context_CopiesAndBoundsPrimitiveInputs()
         {
@@ -85,6 +118,27 @@ namespace OpenEmpires.Tests
                     new ExplanationPlanState(99, null, null, null, null, null)));
         }
 
+        [Test]
+        public void Context_OwnsPlanListAfterCallerReplacesAndRemovesEntries()
+        {
+            var original = new ExplanationPlanState(7, "OriginalType", "Active",
+                "OriginalMilestone", "InProgress", "OriginalReason");
+            var source = new List<ExplanationPlanState> { original };
+            var context = new ExplanationContext(0, currentSnapshotTick: 12,
+                currentPlans: source);
+
+            source[0] = new ExplanationPlanState(99, "ReplacementType", "Failed",
+                "ReplacementMilestone", "Failed", "ReplacementReason");
+            source.RemoveAt(0);
+
+            Assert.That(context.CurrentPlans, Has.Count.EqualTo(1));
+            Assert.That(context.CurrentPlans[0], Is.SameAs(original));
+            string rendered = new CommanderExplanationService().Explain(context,
+                CommanderExplanationQuery.CurrentPlan).DisplayText;
+            Assert.That(rendered, Does.Contain("Plan #7 OriginalType"));
+            Assert.That(rendered, Does.Not.Contain("ReplacementType"));
+        }
+
         [Test]
         public void Context_RejectsInvalidOwnerIdentityTicksAndEnums()
         {
@@ -167,4 +221,4 @@ namespace OpenEmpires.Tests
                 new ExplanationContext(0), (CommanderExplanationQuery)99));
         }
     }
-}
\ No newline at end of file
+}
```

## Exact unified diff: Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs

```diff
diff --git "a/C:\\Users\\RS\\AppData\\Local\\Temp\\phase4c2-fixround1-7e4bbafb2104409ba074314d36a68a3b\\CommanderPhase4C2PlayModeTests.cs" b/Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs
index 2c95330..dae773c 100644
--- "a/C:\\Users\\RS\\AppData\\Local\\Temp\\phase4c2-fixround1-7e4bbafb2104409ba074314d36a68a3b\\CommanderPhase4C2PlayModeTests.cs"
+++ b/Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs
@@ -236,6 +236,63 @@ namespace OpenEmpires.Tests
             Assert.That(chat.DisplayedTranscript, Does.Contain("Unsupported or mixed Commander request."));
         }
 
+        [UnityTest]
+        public IEnumerator HostProjection_ClassifiesRefusedPlannerRejectedAndUnsubmittedSelection()
+        {
+            chat.InitializeStrategic(new ThrowingStrategicProvider(), pipeline);
+            var intent = new StrategicIntent(901, 0, StrategicObjectiveType.AttackPreparation, 70);
+            StrategicDecisionResult selected = StrategicDecisionResult.Selected(intent, null, 70,
+                StrategicPriorityLevel.Normal, "Attack preparation was selected.");
+
+            ProjectExplanation(new StrategicDecisionRecord(31, 70,
+                StrategicEvaluationTriggerType.PlayerRequest, "test", null, null,
+                "Transition policy refused the selected intent.", decision: selected,
+                transitionAllowed: false));
+            intent.StatusReason = "mutated after projection";
+            Task<CommanderAIChatSubmission> refused = chat.SubmitMessageAsync("explain last decision");
+            while (!refused.IsCompleted) yield return null;
+            Assert.That(chat.LatestExplanation.Outcome,
+                Is.EqualTo(ExplanationOutcome.TransitionRefused));
+            Assert.That(chat.LatestExplanation.DisplayText,
+                Does.Contain("Transition policy refused the selected intent."));
+            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain("AttackPreparation"));
+            Assert.That(chat.LatestExplanation.DisplayText,
+                Does.Not.Contain("mutated after projection"));
+
+            var rejectedSubmission = new StrategicIntentSubmission(
+                StrategicIntentSubmissionStatus.Rejected, intent, null,
+                StrategicIntentValidationError.CommitmentBlocked,
+                "Planner commitment was blocked.");
+            ProjectExplanation(new StrategicDecisionRecord(32, 71,
+                StrategicEvaluationTriggerType.PlayerRequest, "test", null, null,
+                "Planner commitment was blocked.", decision: selected,
+                submission: rejectedSubmission));
+            Task<CommanderAIChatSubmission> plannerRejected =
+                chat.SubmitMessageAsync("explain last decision");
+            while (!plannerRejected.IsCompleted) yield return null;
+            Assert.That(chat.LatestExplanation.Outcome,
+                Is.EqualTo(ExplanationOutcome.PlannerRejected));
+            Assert.That(chat.LatestExplanation.DisplayText,
+                Does.Contain("Planner commitment was blocked."));
+
+            ProjectExplanation(new StrategicDecisionRecord(33, 72,
+                StrategicEvaluationTriggerType.PlayerRequest, "test", null, null,
+                "Selection was recorded without submission.", decision: selected));
+            Task<CommanderAIChatSubmission> notSubmitted =
+                chat.SubmitMessageAsync("explain last decision");
+            while (!notSubmitted.IsCompleted) yield return null;
+            Assert.That(chat.LatestExplanation.Outcome,
+                Is.EqualTo(ExplanationOutcome.SelectionNotSubmitted));
+            Assert.That(chat.LatestExplanation.DisplayText,
+                Does.Contain("Selection was recorded without submission."));
+        }
+
+        private void ProjectExplanation(StrategicDecisionRecord record)
+        {
+            typeof(CommanderChatUI).GetMethod("ProjectStrategicExplanation",
+                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(chat, new object[] { record });
+        }
+
         private StrategicPlan CreateEmergencyDefense()
         {
             var request = new StrategicAIRequest("prepare defenses", pipeline.CaptureContext(),
@@ -329,4 +386,4 @@ namespace OpenEmpires.Tests
             }
         }
     }
-}
\ No newline at end of file
+}
```

## Current full file: Assets/Scripts/AI/Commander/Phase4C/CommanderExplanationService.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenEmpires
{
    public sealed class CommanderExplanationService
    {
        public ExplanationResult Explain(ExplanationContext context,
            CommanderExplanationQuery query = CommanderExplanationQuery.LastDecision)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!Enum.IsDefined(typeof(CommanderExplanationQuery), query))
                throw new ArgumentOutOfRangeException(nameof(query));
            bool hasRecordedDecisionEvidence = HasRecordedDecisionEvidence(context);
            string text;
            switch (query)
            {
                case CommanderExplanationQuery.LastDecision:
                    text = RenderDecision(context);
                    break;
                case CommanderExplanationQuery.LastRejection:
                    text = !hasRecordedDecisionEvidence
                        ? "No recorded decision or rejection evidence is available."
                        : IsRejection(context.Outcome)
                        ? RenderDecision(context)
                        : "The latest recorded outcome was " + context.Outcome
                            + ", not a rejection.";
                    break;
                case CommanderExplanationQuery.AttackReason:
                    text = !hasRecordedDecisionEvidence
                        ? "No recorded decision or rejection evidence is available for "
                            + "AttackPreparation."
                        : IsRejection(context.Outcome)
                        && string.Equals(context.RequestedObjective, "AttackPreparation",
                            StringComparison.Ordinal)
                        ? RenderDecision(context)
                        : "No recorded rejection is attributable to AttackPreparation. "
                            + "The latest recorded outcome was " + context.Outcome + ".";
                    break;
                case CommanderExplanationQuery.CurrentPlan:
                    text = RenderCurrentPlans(context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(query));
            }
            return new ExplanationResult(text, context.Outcome);
        }

        private static bool IsRejection(ExplanationOutcome outcome)
        {
            return outcome == ExplanationOutcome.Rejected
                || outcome == ExplanationOutcome.TransitionRefused
                || outcome == ExplanationOutcome.PlannerRejected;
        }

        private static bool HasRecordedDecisionEvidence(ExplanationContext context)
        {
            return context.DecisionId.HasValue
                || context.DecisionTick.HasValue
                || context.Reason.Length > 0
                || context.RequestedObjective.Length > 0
                || context.AcceptedPlanId.HasValue
                || context.AcceptedPlanType.Length > 0;
        }

        private static string RenderDecision(ExplanationContext context)
        {
            if (!HasRecordedDecisionEvidence(context))
                return "No recorded decision is available.";

            var result = new StringBuilder();
            switch (context.Outcome)
            {
                case ExplanationOutcome.NoDecision:
                    result.Append("No strategic decision was selected.");
                    break;
                case ExplanationOutcome.Rejected:
                    result.Append("The recorded decision was rejected.");
                    break;
                case ExplanationOutcome.TransitionRefused:
                    result.Append("The selected intent was blocked before submission.");
                    break;
                case ExplanationOutcome.PlannerRejected:
                    result.Append("The planner rejected the submitted intent. No accepted plan was created.");
                    break;
                case ExplanationOutcome.PlanCreated:
                    if (context.AcceptedPlanId.HasValue)
                    {
                        result.Append("The decision created plan #")
                            .Append(context.AcceptedPlanId.Value.ToString(CultureInfo.InvariantCulture));
                        if (context.AcceptedPlanType.Length > 0)
                            result.Append(" (").Append(context.AcceptedPlanType).Append(')');
                        result.Append('.');
                    }
                    else result.Append("The decision created an accepted plan.");
                    break;
                case ExplanationOutcome.SelectionNotSubmitted:
                    result.Append("An intent was selected but was not submitted.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(context.Outcome));
            }

            if (context.DecisionId.HasValue)
                result.Append(" Decision ID: ")
                    .Append(context.DecisionId.Value.ToString(CultureInfo.InvariantCulture)).Append('.');
            if (context.DecisionTick.HasValue)
                result.Append(" Historical decision tick: ")
                    .Append(context.DecisionTick.Value.ToString(CultureInfo.InvariantCulture)).Append('.');
            if (context.RequestedObjective.Length > 0)
                result.Append(" Requested objective: ").Append(context.RequestedObjective).Append('.');
            result.Append(context.Reason.Length > 0
                ? " Recorded reason: " + context.Reason
                : " No recorded reason is available.");
            return result.ToString();
        }

        private static string RenderCurrentPlans(ExplanationContext context)
        {
            if (!context.CurrentSnapshotTick.HasValue)
                return "Current plan state is unavailable.";
            string tick = context.CurrentSnapshotTick.Value.ToString(CultureInfo.InvariantCulture);
            if (context.CurrentPlans.Count == 0)
                return "No active plan was observed at current snapshot tick " + tick
                    + ". This does not assert that any historical plan completed.";

            var plans = new List<ExplanationPlanState>(context.CurrentPlans);
            plans.Sort((left, right) => left.PlanId.CompareTo(right.PlanId));
            var result = new StringBuilder("Current snapshot tick ").Append(tick)
                .Append(" contains observed plan progress.");
            if (plans.Count == ExplanationContext.MaximumPlans)
                result.Append(" This view is showing up to 32 plans.");
            for (int i = 0; i < plans.Count; i++)
            {
                ExplanationPlanState plan = plans[i];
                result.Append(" Plan #")
                    .Append(plan.PlanId.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(ValueOrUnavailable(plan.PlanType))
                    .Append("; observed status: ").Append(ValueOrUnavailable(plan.Status))
                    .Append("; observed current milestone: ")
                    .Append(ValueOrUnavailable(plan.CurrentMilestone))
                    .Append("; observed milestone status: ")
                    .Append(ValueOrUnavailable(plan.MilestoneStatus))
                    .Append("; observed reason: ").Append(ValueOrUnavailable(plan.Reason)).Append('.');
            }
            return result.ToString();
        }

        private static string ValueOrUnavailable(string value)
        {
            return string.IsNullOrEmpty(value) ? "unavailable" : value;
        }
    }
}
```

## Current full file: Assets/Tests/EditMode/CommanderPhase4C2Tests.cs

```csharp
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

        [TestCase(ExplanationOutcome.NoDecision, "No strategic decision was selected")]
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
        public void PristineContext_DecisionQueriesStateThatRecordedEvidenceIsUnavailable()
        {
            var service = new CommanderExplanationService();
            var context = new ExplanationContext(0);

            Assert.That(service.Explain(context, CommanderExplanationQuery.LastDecision).DisplayText,
                Is.EqualTo("No recorded decision is available."));
            Assert.That(service.Explain(context, CommanderExplanationQuery.LastRejection).DisplayText,
                Is.EqualTo("No recorded decision or rejection evidence is available."));
            Assert.That(service.Explain(context, CommanderExplanationQuery.AttackReason).DisplayText,
                Is.EqualTo("No recorded decision or rejection evidence is available for AttackPreparation."));
        }

        [Test]
        public void RecordedNoDecision_PreservesOutcomeReasonIdAndHistoricalTick()
        {
            var context = new ExplanationContext(0, decisionId: 23, decisionTick: 811,
                outcome: ExplanationOutcome.NoDecision,
                reason: "No eligible strategic recommendation was available.",
                requestedObjective: "AttackPreparation");

            ExplanationResult result = new CommanderExplanationService().Explain(context);

            Assert.That(result.Outcome, Is.EqualTo(ExplanationOutcome.NoDecision));
            Assert.That(result.DisplayText, Does.Contain("No strategic decision was selected."));
            Assert.That(result.DisplayText, Does.Contain("Decision ID: 23."));
            Assert.That(result.DisplayText, Does.Contain("Historical decision tick: 811."));
            Assert.That(result.DisplayText,
                Does.Contain("Recorded reason: No eligible strategic recommendation was available."));
            Assert.That(result.DisplayText, Does.Contain("Requested objective: AttackPreparation."));
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
        public void Context_OwnsPlanListAfterCallerReplacesAndRemovesEntries()
        {
            var original = new ExplanationPlanState(7, "OriginalType", "Active",
                "OriginalMilestone", "InProgress", "OriginalReason");
            var source = new List<ExplanationPlanState> { original };
            var context = new ExplanationContext(0, currentSnapshotTick: 12,
                currentPlans: source);

            source[0] = new ExplanationPlanState(99, "ReplacementType", "Failed",
                "ReplacementMilestone", "Failed", "ReplacementReason");
            source.RemoveAt(0);

            Assert.That(context.CurrentPlans, Has.Count.EqualTo(1));
            Assert.That(context.CurrentPlans[0], Is.SameAs(original));
            string rendered = new CommanderExplanationService().Explain(context,
                CommanderExplanationQuery.CurrentPlan).DisplayText;
            Assert.That(rendered, Does.Contain("Plan #7 OriginalType"));
            Assert.That(rendered, Does.Not.Contain("ReplacementType"));
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
```

## Current full file: Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4C2")]
    public sealed class CommanderPhase4C2PlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;

        [SetUp]
        public void SetUp()
        {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation))[0] = 3;
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            foreach (var node in simulation.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            for (int tx = x - 35; tx <= x + 35; tx++)
                for (int tz = z - 22; tz <= z + 22; tz++)
                {
                    simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                    simulation.MapData.ForestDensity[tx, tz] = 0;
                    simulation.MapData.FoundationCount[tx, tz] = 0;
                    simulation.FogOfWar.SetVisible(0, tx, tz);
                }
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 22, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z + 6, false);
            simulation.CreateBuilding(0, BuildingType.Barracks, x - 10, z, false);
            simulation.CreateBuilding(0, BuildingType.ArcheryRange, x - 16, z, false);
            simulation.CreateBuilding(0, BuildingType.Stables, x - 22, z, false);
            Gatherers(ResourceType.Food, 10, x - 22, z);
            Gatherers(ResourceType.Gold, 6, x, z);
            Gatherers(ResourceType.Wood, 4, x - 10, z);
            for (int i = 0; i < 12; i++) AddUnit(2);
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, CurrentResource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            chat = new GameObject("Phase4C2RuntimeChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(new MockAIProvider(), simulation, goals, dispatcher);
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            pipeline?.Dispose();
            planner?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator RejectedAttackQuestion_MatchesRecordedReasonDespiteUnrelatedDefense()
        {
            var provider = new CountingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            StrategicPlan defense = CreateEmergencyDefense();
            Task<CommanderAIChatSubmission> prepare = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!prepare.IsCompleted) yield return null;
            StrategicDecisionRecord rejected = chat.ApproveStrategicRecommendation();
            Assert.That(rejected.Decision.Status, Is.EqualTo(StrategicDecisionStatus.Rejected));
            Assert.That(rejected.Outcome, Does.Contain("Emergency defense has higher priority"));

            Task<CommanderAIChatSubmission> explain =
                chat.SubmitMessageAsync("  WHY   are we not attacking?  ");
            while (!explain.IsCompleted) yield return null;

            Assert.That(chat.LatestExplanation, Is.Not.Null);
            Assert.That(chat.LatestExplanation.Outcome, Is.EqualTo(ExplanationOutcome.Rejected));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain(rejected.Outcome));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain("AttackPreparation"));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Not.Contain("income"));
            Assert.That(defense.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(provider.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExplanationCannotModifyIntent()
        {
            var provider = new CountingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            CreateEmergencyDefense();
            Task<CommanderAIChatSubmission> first = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!first.IsCompleted) yield return null;
            StrategicDecisionRecord projected = chat.ApproveStrategicRecommendation();
            Task<CommanderAIChatSubmission> second = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!second.IsCompleted) yield return null;
            StrategicIntent pending = chat.PendingStrategicIntent;
            Assert.That(pending, Is.Not.Null);

            int history = pipeline.DecisionHistory.History.Count;
            int plans = planner.Plans.Count;
            int reservations = planner.Reservations.Count;
            int archivedReservations = planner.ArchivedReservations.Count;
            int goalCount = goals.Goals.Count;
            int commandCount = PendingCommandCount();
            string planState = PlanState();
            string reservationState = ReservationState();
            string goalState = GoalState();
            int memoryBefore = chat.Conversation.Snapshot().Count;
            string[] questions = {
                "why are we not attacking?", "why was that rejected.",
                "explain   last decision", "what is the plan doing?"
            };
            foreach (string question in questions)
            {
                Task<CommanderAIChatSubmission> ask = chat.SubmitMessageAsync(question);
                while (!ask.IsCompleted) yield return null;
            }

            Assert.That(chat.PendingStrategicIntent, Is.SameAs(pending));
            Assert.That(pending.Status, Is.EqualTo(StrategicIntentStatus.Created));
            Assert.That(chat.LatestStrategicDecision, Is.SameAs(projected));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(history));
            Assert.That(planner.Plans.Count, Is.EqualTo(plans));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
            Assert.That(planner.ArchivedReservations.Count, Is.EqualTo(archivedReservations));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(PendingCommandCount(), Is.EqualTo(commandCount));
            Assert.That(PlanState(), Is.EqualTo(planState));
            Assert.That(ReservationState(), Is.EqualTo(reservationState));
            Assert.That(GoalState(), Is.EqualTo(goalState));
            Assert.That(provider.CallCount, Is.EqualTo(2));
            Assert.That(chat.Conversation.Snapshot().Skip(memoryBefore).Count(), Is.EqualTo(4));
            Assert.That(chat.Conversation.Snapshot().Skip(memoryBefore)
                .All(entry => entry.Kind == MemoryEntryKind.Explanation), Is.True);
        }

        [UnityTest]
        public IEnumerator CurrentPlanQuery_MatchesFreshDetachedSnapshotWithoutAdvancing()
        {
            var provider = new CountingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            Task<CommanderAIChatSubmission> prepare = chat.SubmitMessageAsync("prepare defenses");
            while (!prepare.IsCompleted) yield return null;
            StrategicDecisionRecord accepted = chat.ApproveStrategicRecommendation();
            Assert.That(accepted.Submission?.CreatedPlan, Is.True, accepted.Outcome);
            StrategicPlan plan = accepted.Submission.Plan;
            Assert.That(planner.CompleteMilestoneAndAdvance(plan.StrategicPlanId), Is.True);
            StrategicContext before = pipeline.CaptureContext();
            StrategicPlanState expected = before.ActivePlans.Single(p =>
                p.StrategicPlanId == plan.StrategicPlanId);
            string planState = PlanState();
            int history = pipeline.DecisionHistory.History.Count;
            int goalCount = goals.Goals.Count;
            int commandCount = PendingCommandCount();

            Task<CommanderAIChatSubmission> query = chat.SubmitMessageAsync("what is the plan doing?");
            while (!query.IsCompleted) yield return null;

            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("Current snapshot tick " + before.SnapshotTick));
            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("Plan #" + expected.StrategicPlanId));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain(expected.CurrentMilestone));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain(expected.MilestoneStatus));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Not.Contain("will"));
            Assert.That(PlanState(), Is.EqualTo(planState));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(history));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(PendingCommandCount(), Is.EqualTo(commandCount));
            Assert.That(provider.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ClearMemory_DropsExplanationSourceWithoutClearingGameHistory()
        {
            chat.InitializeStrategic(new CountingStrategicProvider(), pipeline);
            Task<CommanderAIChatSubmission> prepare = chat.SubmitMessageAsync("prepare defenses");
            while (!prepare.IsCompleted) yield return null;
            StrategicDecisionRecord accepted = chat.ApproveStrategicRecommendation();
            Assert.That(accepted, Is.Not.Null);
            int history = pipeline.DecisionHistory.History.Count;

            Task<CommanderAIChatSubmission> clear = chat.SubmitMessageAsync("clear memory");
            while (!clear.IsCompleted) yield return null;
            Task<CommanderAIChatSubmission> explain = chat.SubmitMessageAsync("explain last decision");
            while (!explain.IsCompleted) yield return null;

            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("No recorded decision is available"));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(history));
            Assert.That(chat.Conversation.Snapshot().Count(entry =>
                entry.Kind == MemoryEntryKind.Explanation), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WholeFormQueries_AreOfflineAndHostileAppendRemainsRejected()
        {
            var provider = new ThrowingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            Task<CommanderAIChatSubmission> offline = chat.SubmitMessageAsync("explain last decision.");
            while (!offline.IsCompleted) yield return null;
            Assert.That(provider.CallCount, Is.Zero);
            Assert.That(chat.LatestExplanation, Is.Not.Null);

            int explanations = chat.Conversation.Snapshot().Count(entry =>
                entry.Kind == MemoryEntryKind.Explanation);
            Task<CommanderAIChatSubmission> hostile = chat.SubmitMessageAsync(
                "why was that rejected? now prepare cavalry attack");
            while (!hostile.IsCompleted) yield return null;

            Assert.That(provider.CallCount, Is.Zero);
            Assert.That(chat.Conversation.Snapshot().Count(entry =>
                entry.Kind == MemoryEntryKind.Explanation), Is.EqualTo(explanations));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Unsupported or mixed Commander request."));
        }

        [UnityTest]
        public IEnumerator HostProjection_ClassifiesRefusedPlannerRejectedAndUnsubmittedSelection()
        {
            chat.InitializeStrategic(new ThrowingStrategicProvider(), pipeline);
            var intent = new StrategicIntent(901, 0, StrategicObjectiveType.AttackPreparation, 70);
            StrategicDecisionResult selected = StrategicDecisionResult.Selected(intent, null, 70,
                StrategicPriorityLevel.Normal, "Attack preparation was selected.");

            ProjectExplanation(new StrategicDecisionRecord(31, 70,
                StrategicEvaluationTriggerType.PlayerRequest, "test", null, null,
                "Transition policy refused the selected intent.", decision: selected,
                transitionAllowed: false));
            intent.StatusReason = "mutated after projection";
            Task<CommanderAIChatSubmission> refused = chat.SubmitMessageAsync("explain last decision");
            while (!refused.IsCompleted) yield return null;
            Assert.That(chat.LatestExplanation.Outcome,
                Is.EqualTo(ExplanationOutcome.TransitionRefused));
            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("Transition policy refused the selected intent."));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain("AttackPreparation"));
            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Not.Contain("mutated after projection"));

            var rejectedSubmission = new StrategicIntentSubmission(
                StrategicIntentSubmissionStatus.Rejected, intent, null,
                StrategicIntentValidationError.CommitmentBlocked,
                "Planner commitment was blocked.");
            ProjectExplanation(new StrategicDecisionRecord(32, 71,
                StrategicEvaluationTriggerType.PlayerRequest, "test", null, null,
                "Planner commitment was blocked.", decision: selected,
                submission: rejectedSubmission));
            Task<CommanderAIChatSubmission> plannerRejected =
                chat.SubmitMessageAsync("explain last decision");
            while (!plannerRejected.IsCompleted) yield return null;
            Assert.That(chat.LatestExplanation.Outcome,
                Is.EqualTo(ExplanationOutcome.PlannerRejected));
            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("Planner commitment was blocked."));

            ProjectExplanation(new StrategicDecisionRecord(33, 72,
                StrategicEvaluationTriggerType.PlayerRequest, "test", null, null,
                "Selection was recorded without submission.", decision: selected));
            Task<CommanderAIChatSubmission> notSubmitted =
                chat.SubmitMessageAsync("explain last decision");
            while (!notSubmitted.IsCompleted) yield return null;
            Assert.That(chat.LatestExplanation.Outcome,
                Is.EqualTo(ExplanationOutcome.SelectionNotSubmitted));
            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("Selection was recorded without submission."));
        }

        private void ProjectExplanation(StrategicDecisionRecord record)
        {
            typeof(CommanderChatUI).GetMethod("ProjectStrategicExplanation",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(chat, new object[] { record });
        }

        private StrategicPlan CreateEmergencyDefense()
        {
            var request = new StrategicAIRequest("prepare defenses", pipeline.CaptureContext(),
                planner.IntentIds);
            StrategicIntent defense = new MockStrategicAIProvider()
                .InterpretStrategicIntentAsync(request, default).Result.Intent;
            StrategicIntentSubmission submission = planner.SubmitIntent(defense, true, false);
            Assert.That(submission.CreatedPlan, Is.True, submission.Reason);
            return submission.Plan;
        }

        private string PlanState() => string.Join("|", planner.Plans.Select(plan =>
            plan.StrategicPlanId + ":" + plan.Status + ":" + plan.OutcomeMessage + ":"
            + (plan.CurrentMilestone == null ? "none" :
                plan.CurrentMilestone.Name + ":" + plan.CurrentMilestone.Status)));

        private string ReservationState() => string.Join("|", planner.Reservations.Select(value =>
            value.ReservationId + ":" + value.PlanId + ":" + value.Status + ":" + value.Amount));

        private string GoalState() => string.Join("|", goals.Goals.Select(value =>
            value.GoalId + ":" + value.Status + ":" + value.StatusReason));

        private int PendingCommandCount()
        {
            var pending = (ICollection<ICommand>)typeof(CommandBuffer)
                .GetField("pendingCommands", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(simulation.CommandBuffer);
            return pending.Count;
        }

        private UnitData AddUnit(int type)
        {
            var unit = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(simulation.MapData.Width / 2,
                    simulation.MapData.Height / 2), Fixed32.One, Fixed32.One, Fixed32.One);
            unit.UnitType = type;
            unit.IsVillager = type == 0;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private void Gatherers(ResourceType resource, int count, int start, int z)
        {
            var node = simulation.MapData.AddResourceNode(resource,
                simulation.MapData.TileToWorldFixed(start + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                UnitData worker = AddUnit(0);
                worker.SimPosition = simulation.MapData.TileToWorldFixed(
                    start + 3 + i % 2, z + 7 + i / 2);
                worker.FinalDestination = worker.SimPosition;
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = node.Id;
            }
        }

        private int CurrentResource(ResourceType type)
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        private sealed class CountingStrategicProvider : IStrategicAIInterpreter
        {
            public int CallCount { get; private set; }
            public async Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken)
            {
                CallCount++;
                return await new MockStrategicAIProvider().InterpretStrategicIntentAsync(
                    request, cancellationToken);
            }
        }

        private sealed class ThrowingStrategicProvider : IStrategicAIInterpreter
        {
            public int CallCount { get; private set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken)
            {
                CallCount++;
                throw new InvalidOperationException("Explanation queries must remain offline.");
            }
        }
    }
}
```

