using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderVoiceApprovalIntegrationTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat, previous;
        private Provider provider;
        private Speech speech;
        private bool previousEnabled, previousAuto;
        private static readonly FieldInfo Instance = typeof(CommanderChatUI).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);

        [SetUp]
        public void Setup()
        {
            previousEnabled = CommanderVoiceSettings.VoiceEnabled;
            previousAuto = CommanderVoiceSettings.AutoSubmit;
            CommanderVoiceSettings.VoiceEnabled = true;
            CommanderVoiceSettings.AutoSubmit = false;
            previous = Instance.GetValue(null) as CommanderChatUI;
            Instance.SetValue(null, null);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            manager = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, manager);
            provider = new Provider(); speech = new Speech();
            chat = new GameObject("VoiceApprovalIntegration").AddComponent<CommanderChatUI>();
            chat.Initialize(provider, simulation, manager, dispatcher);
            Instance.SetValue(null, chat);
            chat.SetVoiceProvider(speech, new Capture());
        }

        [TearDown]
        public void Cleanup()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose(); manager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            Instance.SetValue(null, previous);
            CommanderVoiceSettings.VoiceEnabled = previousEnabled;
            CommanderVoiceSettings.AutoSubmit = previousAuto;
        }

        private async Task Preview()
        {
            await chat.SubmitMessageAsync("Train four new Spearmen and three new Archers");
            Assert.That(chat.PendingActionPlan, Is.Not.Null, chat.DisplayedTranscript);
            chat.OpenCommander();
            Assert.That((bool)typeof(CommanderChatUI).GetProperty("IsVoiceApprovalCandidateDisplayed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chat), Is.True);
        }

        [Test]
        public async Task RawApprove_UsesDisplayedPlanApprovalWithoutAnotherSemanticCall()
        {
            await Preview(); int calls = provider.Calls;
            Assert.That(chat.VoiceController.StartRecording(), Is.True);
            var recognition = chat.VoiceController.StopRecordingAndTranscribeAsync();
            speech.Complete("approve"); await recognition;
            Assert.That(provider.Calls, Is.EqualTo(calls));
            Assert.That(chat.PendingActionPlan, Is.Null, chat.DisplayedTranscript);
            Assert.That(manager.Goals.Count, Is.GreaterThan(0), chat.DisplayedTranscript);
            Assert.That(chat.VoiceState, Is.EqualTo(CommanderVoiceState.Idle));
        }

        [TestCase("do not approve")]
        [TestCase("\"approve\"")]
        [TestCase("approve and train five knights")]
        [TestCase("disapprove")]
        public async Task NonExactApproval_ClarifiesLocallyAndKeepsCandidate(string raw)
        {
            await Preview(); var candidate = chat.PendingActionPlan; int calls = provider.Calls;
            chat.VoiceController.StartRecording(); var recognition = chat.VoiceController.StopRecordingAndTranscribeAsync();
            speech.Complete(raw); await recognition;
            Assert.That(chat.PendingActionPlan, Is.SameAs(candidate));
            Assert.That(provider.Calls, Is.EqualTo(calls));
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Approval needs review"));
        }

        [Test]
        public async Task CandidateCreatedDuringRecording_CannotAcquireApprovalFromThatRecording()
        {
            chat.VoiceController.StartRecording();
            await Preview(); var candidate = chat.PendingActionPlan; int calls = provider.Calls;
            var recognition = chat.VoiceController.StopRecordingAndTranscribeAsync();
            speech.Complete("approve"); await recognition;
            Assert.That(chat.PendingActionPlan, Is.SameAs(candidate));
            Assert.That(provider.Calls, Is.EqualTo(calls)); Assert.That(manager.Goals, Is.Empty);
        }

        [Test]
        public async Task ReplacedCandidate_CannotAcquireApprovalFromEarlierRecording()
        {
            await Preview(); var first = chat.PendingActionPlan;
            chat.VoiceController.StartRecording();
            await chat.SubmitMessageAsync("cancel plan");
            await Preview(); var replacement = chat.PendingActionPlan; int calls = provider.Calls;
            Assert.That(replacement, Is.Not.SameAs(first));
            var recognition = chat.VoiceController.StopRecordingAndTranscribeAsync();
            speech.Complete("approve"); await recognition;
            Assert.That(chat.PendingActionPlan, Is.SameAs(replacement));
            Assert.That(provider.Calls, Is.EqualTo(calls)); Assert.That(manager.Goals, Is.Empty);
        }

        private sealed class Provider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            public int Calls;
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request, CancellationToken token)
            {
                Calls++;
                var result = CommanderSemanticJson.ParseProviderResponse("{\"outcome\":\"Request\",\"executionOrder\":\"Independent\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"quantityMode\":\"New\"},{\"type\":\"EnsureUnitCount\",\"unit\":\"Archer\",\"count\":3,\"quantityMode\":\"New\"}]}");
                Assert.That(result.IsValid, Is.True, result.SchemaDiagnostic);
                return Task.FromResult(result);
            }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
            { Calls++; return Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure, "fixture")); }
        }
        private sealed class Speech : ICommanderSpeechToTextProvider
        {
            private readonly TaskCompletionSource<CommanderSpeechToTextResult> completion = new TaskCompletionSource<CommanderSpeechToTextResult>();
            public bool IsAvailable => true;
            public Task<CommanderSpeechToTextResult> TranscribeAsync(CommanderAudioData audio, CancellationToken token) => completion.Task;
            public void Complete(string raw) => completion.TrySetResult(CommanderSpeechToTextResult.Accepted(raw));
            public void Dispose() { }
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

