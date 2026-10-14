using NUnit.Framework;
using System.Threading.Tasks;

namespace OpenEmpires.Tests
{
    public sealed class CommanderVoiceReviewSafetyTests
    {
        private static CommanderVoiceName[] Names => new[]
        {
            new CommanderVoiceName("unit:1", "Spearman", "spearmen", "spear"),
            new CommanderVoiceName("unit:7", "Knight", "knights"),
            new CommanderVoiceName("building:ArcheryRange", "Archery Range", "archeryrange"),
            new CommanderVoiceName("building:House", "House", "houses")
        };

        [Test]
        public void Correction_ChangesOnlyCatalogNounsAndRetainsOriginal()
        {
            const string original = "Don't train 12 spearmn; move my knights north, build 2 archeryrange east.";
            var review = CommanderVoiceTranscriptCorrection.Review(original, Names);
            Assert.AreEqual(original, review.Original);
            Assert.AreEqual("Don't train 12 Spearman; move my knights north, build 2 Archery Range east.", review.Text);
            Assert.IsTrue(review.RequiresReview);
        }

        [Test]
        public void SplitRecognition_IsCorrectedButProtectedMeaningIsNeverReplaced()
        {
            var review = CommanderVoiceTranscriptCorrection.Review("train twenty spear men, not enemy units to the right", Names);
            Assert.AreEqual("train twenty Spearman, not enemy units to the right", review.Text);
            Assert.AreEqual("approve", CommanderVoiceTranscriptCorrection.Review("approve", Names).Text);
        }

        [TestCase("move 4 spear men to my marker", "move 4 Spearman to my marker")]
        [TestCase("move 4 spear men to their markers", "move 4 Spearman to their markers")]
        [TestCase("move 4 spear men to my base location", "move 4 Spearman to my base location")]
        public void Correction_PreservesDestinationConceptsInsteadOfSimilarBuildingNames(string raw, string expected)
        {
            var names = new[] { new CommanderVoiceName("unit:1", "Spearman", "spearmen"),
                new CommanderVoiceName("building:Market", "Market", "markets") };
            var review = CommanderVoiceTranscriptCorrection.Review(raw, names);
            Assert.AreEqual(expected, review.Text);
            Assert.AreEqual(raw, review.Original);
        }

        [Test]
        public void AmbiguousCatalogAlias_RequiresChoiceAndRetainsText()
        {
            var names = new[] { new CommanderVoiceName("a", "Knight", "rider"), new CommanderVoiceName("b", "Horseman", "rider") };
            var review = CommanderVoiceTranscriptCorrection.Review("train 3 rider", names);
            Assert.AreEqual("train 3 rider", review.Text);
            Assert.IsTrue(review.RequiresReview);
            Assert.That(review.Message, Does.Contain("Knight").And.Contain("Horseman"));
        }

        [TestCase("do not approve")]
        [TestCase("\"approve\"")]
        [TestCase("approve plan")]
        [TestCase("disapprove")]
        [TestCase("a prove")]
        [TestCase("approve and train 10 knights")]
        public void Approval_RejectsAnythingExceptRawWholeApprove(string raw)
        {
            var candidate = new object();
            var ticket = new CommanderVoiceApprovalTicket(candidate, "preview", 1, true);
            Assert.IsFalse(ticket.TryConsume(raw, candidate, "preview", 1, true));
        }

        [Test]
        public void Approval_BindsDisplayedCandidateAndConsumesOnce()
        {
            var candidate = new object();
            var ticket = new CommanderVoiceApprovalTicket(candidate, "preview", 1, true);
            Assert.IsTrue(ticket.TryConsume(" APPROVE ", candidate, "preview", 1, true));
            Assert.IsFalse(ticket.TryConsume("approve", candidate, "preview", 1, true));
        }

        [Test]
        public void Approval_CandidateChangedAwayAndBackStillInvalidatesRecording()
        {
            var candidate = new object();
            var ticket = new CommanderVoiceApprovalTicket(candidate, "preview", 1, true);
            ticket.Observe(new object(), "other", 1, true);
            ticket.Observe(candidate, "preview", 1, true);
            Assert.IsFalse(ticket.TryConsume("approve", candidate, "preview", 1, true));
        }

        [Test]
        public void Approval_RejectsChangedHiddenNewOrStaleCandidate()
        {
            var candidate = new object();
            Assert.IsFalse(new CommanderVoiceApprovalTicket(candidate, "preview", 1, true).TryConsume("approve", new object(), "preview", 1, true));
            Assert.IsFalse(new CommanderVoiceApprovalTicket(candidate, "preview", 1, true).TryConsume("approve", candidate, "changed", 1, true));
            Assert.IsFalse(new CommanderVoiceApprovalTicket(candidate, "preview", 1, true).TryConsume("approve", candidate, "preview", 2, true));
            Assert.IsFalse(new CommanderVoiceApprovalTicket(candidate, "preview", 1, true).TryConsume("approve", candidate, "preview", 1, false));
            Assert.IsFalse(new CommanderVoiceApprovalTicket(candidate, "preview", 1, false).TryConsume("approve", candidate, "preview", 1, true));
            Assert.IsFalse(new CommanderVoiceApprovalTicket(null, "", 1, false).TryConsume("approve", candidate, "preview", 1, true));
        }

        [Test]
        public async Task RawApproval_IsConsumedBeforeCorrectionOrSemanticSubmission()
        {
            bool enabled = CommanderVoiceSettings.VoiceEnabled;
            CommanderVoiceSettings.VoiceEnabled = true;
            int corrections = 0, submissions = 0, approvals = 0;
            try
            {
                using var controller = new CommanderVoiceInputController(new MockCommanderSpeechToTextProvider("approve"), new Capture(),
                    _ => { submissions++; return Task.FromResult<CommanderAIChatSubmission>(null); });
                controller.AutoSubmit = true;
                controller.RawTranscriptHandler = raw => { if (raw == "approve") { approvals++; return true; } return false; };
                controller.TranscriptProcessor = raw => { corrections++; return CommanderVoiceTranscriptCorrection.Review(raw, Names); };
                Assert.IsTrue(controller.StartRecording());
                await controller.StopRecordingAndTranscribeAsync();
                Assert.AreEqual(1, approvals);
                Assert.AreEqual(0, corrections);
                Assert.AreEqual(0, submissions);
                Assert.AreEqual(CommanderVoiceState.Idle, controller.State);
            }
            finally { CommanderVoiceSettings.VoiceEnabled = enabled; }
        }

        [Test]
        public async Task CorrectedTranscript_RequiresPreviewEvenWhenAutoSubmitWasEnabled()
        {
            bool enabled = CommanderVoiceSettings.VoiceEnabled;
            CommanderVoiceSettings.VoiceEnabled = true;
            int submissions = 0;
            try
            {
                using var controller = new CommanderVoiceInputController(new MockCommanderSpeechToTextProvider("train 12 spearmn"), new Capture(),
                    _ => { submissions++; return Task.FromResult<CommanderAIChatSubmission>(null); });
                controller.AutoSubmit = true;
                controller.TranscriptProcessor = raw => CommanderVoiceTranscriptCorrection.Review(raw, Names);
                Assert.IsTrue(controller.StartRecording());
                await controller.StopRecordingAndTranscribeAsync();
                Assert.AreEqual("train 12 spearmn", controller.OriginalTranscript);
                Assert.AreEqual("train 12 Spearman", controller.CurrentTranscript);
                Assert.AreEqual(CommanderVoiceState.Preview, controller.State);
                Assert.AreEqual(0, submissions);
                controller.Cancel();
                Assert.IsEmpty(controller.OriginalTranscript);
                Assert.IsNull(controller.TranscriptReview);
            }
            finally { CommanderVoiceSettings.VoiceEnabled = enabled; }
        }

        private sealed class Capture : ICommanderAudioCapture
        {
            public bool IsRecording { get; private set; }
            public string CurrentDevice => "fixture";
            public bool StartRecording(string deviceName = null, float maxDurationSeconds = 15f) { IsRecording = true; return true; }
            public CommanderAudioData StopRecording() { IsRecording = false; return new CommanderAudioData(new float[1600], 16000, 1); }
            public void CancelRecording() { IsRecording = false; }
            public void Dispose() { IsRecording = false; }
        }
    }
}
