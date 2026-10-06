using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4H")]
    public sealed class CommanderPhase4HVoicePlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private SemanticMockProvider semanticProvider;

        [SetUp]
        public void SetUp()
        {
            foreach (CommanderChatUI existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });

            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 14, z, false, true).AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.Barracks, x + 8, z, false);
            simulation.CreateBuilding(0, BuildingType.ArcheryRange, x + 11, z, false);

            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;

            goals = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goals);
            semanticProvider = new SemanticMockProvider();

            chat = new GameObject("CommanderPhase4HTestChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(semanticProvider, simulation, goals, dispatcher);
            chat.enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator EditedPreview_NormalSendConsumesVoicePreviewExactlyOnce()
        {
            chat.SetVoiceProvider(new MockCommanderSpeechToTextProvider("make ten spearmen"),
                new PlayModeTestAudioCapture());
            chat.VoiceController.AutoSubmit = false;
            chat.VoiceController.StartRecording();
            var transcribe = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!transcribe.IsCompleted) yield return null;
            chat.InputText = "make five archers";
            var send = chat.SubmitCurrentInputAsync();
            while (!send.IsCompleted) yield return null;
            Assert.That(chat.VoiceState, Is.EqualTo(CommanderVoiceState.Idle));
            var duplicate = chat.VoiceController.ConfirmPreviewAndSubmitAsync();
            while (!duplicate.IsCompleted) yield return null;
            Assert.That(duplicate.Result, Is.Null);
            Assert.That(chat.DisplayedTranscript, Does.Contain("make five archers"));
            Assert.That(goals.Goals.Count, Is.EqualTo(1));
            Assert.That(chat.InputText, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ScenarioA_VoiceTranscript_SubmitsThroughNormalCommanderPath()
        {
            var mockStt = new MockCommanderSpeechToTextProvider("make ten spearmen");
            var mockCapture = new PlayModeTestAudioCapture();
            chat.SetVoiceProvider(mockStt, mockCapture);
            chat.VoiceController.AutoSubmit = true;

            chat.VoiceController.StartRecording();
            Assert.AreEqual(CommanderVoiceState.Recording, chat.VoiceState);

            Task<CommanderSpeechToTextResult> voiceTask = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!voiceTask.IsCompleted) yield return null;

            CommanderSpeechToTextResult result = voiceTask.Result;
            Assert.IsTrue(result.Success);
            Assert.AreEqual("make ten spearmen", result.Transcript);

            // Verify transcript was submitted through Commander pipeline
            // Note: SubmitMessageAsync resets chat.InputText to string.Empty upon completion
            Assert.IsNotNull(chat.LatestSubmission);
            Assert.AreEqual(CommanderVoiceState.Idle, chat.VoiceState);
            StringAssert.Contains("make ten spearmen", chat.DisplayedTranscript);
        }

        [UnityTest]
        public IEnumerator ScenarioA_PreviewMode_PopulatesInputField_AndSubmitsOnConfirmation()
        {
            var mockStt = new MockCommanderSpeechToTextProvider("build a mill near berries");
            var mockCapture = new PlayModeTestAudioCapture();
            chat.SetVoiceProvider(mockStt, mockCapture);
            chat.VoiceController.AutoSubmit = false;

            chat.VoiceController.StartRecording();
            Assert.AreEqual(CommanderVoiceState.Recording, chat.VoiceState);

            Task<CommanderSpeechToTextResult> voiceTask = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!voiceTask.IsCompleted) yield return null;

            CommanderSpeechToTextResult result = voiceTask.Result;
            Assert.IsTrue(result.Success);
            Assert.AreEqual("build a mill near berries", result.Transcript);

            // In preview mode: State is Preview, InputText is populated, but not yet submitted
            Assert.AreEqual(CommanderVoiceState.Preview, chat.VoiceState);
            Assert.AreEqual("build a mill near berries", chat.InputText);
            Assert.IsNull(chat.LatestSubmission);

            // Confirm preview and submit
            Task<CommanderAIChatSubmission> confirmTask = chat.VoiceController.ConfirmPreviewAndSubmitAsync();
            while (!confirmTask.IsCompleted) yield return null;

            Assert.IsNotNull(confirmTask.Result);
            Assert.IsNotNull(chat.LatestSubmission);
            Assert.AreEqual(CommanderVoiceState.Idle, chat.VoiceState);
            StringAssert.Contains("build a mill near berries", chat.DisplayedTranscript);
        }

        [UnityTest]
        public IEnumerator ScenarioB_LateTranscriptAfterReset_IsSafelyDiscarded()
        {
            var mockStt = new MockCommanderSpeechToTextProvider("make ten spearmen")
            {
                SimulatedLatency = TimeSpan.FromMilliseconds(200)
            };
            var mockCapture = new PlayModeTestAudioCapture();
            chat.SetVoiceProvider(mockStt, mockCapture);
            chat.VoiceController.AutoSubmit = true;

            chat.VoiceController.StartRecording();
            Task<CommanderSpeechToTextResult> voiceTask = chat.VoiceController.StopRecordingAndTranscribeAsync();
            Assert.AreEqual(CommanderVoiceState.Transcribing, chat.VoiceState);

            // Match reset / conversation reset happens while transcription is still in-flight
            chat.ResetConversation();
            Assert.IsNull(chat.LatestSubmission);
            Assert.AreEqual(CommanderVoiceState.Idle, chat.VoiceState);

            while (!voiceTask.IsCompleted) yield return null;

            // Late transcript must not submit
            Assert.IsNull(chat.LatestSubmission, "Late transcript after reset must NOT submit!");
            Assert.AreEqual(CommanderVoiceState.Idle, chat.VoiceState);
        }

        [UnityTest]
        public IEnumerator ScenarioC_STTFailure_DoesNotBreakKeyboardCommander()
        {
            var mockStt = new MockCommanderSpeechToTextProvider()
            {
                SimulateFailure = true,
                FailureErrorCode = "NATIVE_CRASH_SIMULATION",
                FailureErrorMessage = "Native Whisper library failed."
            };
            var mockCapture = new PlayModeTestAudioCapture();
            chat.SetVoiceProvider(mockStt, mockCapture);

            chat.VoiceController.StartRecording();
            Task<CommanderSpeechToTextResult> voiceTask = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!voiceTask.IsCompleted) yield return null;

            Assert.IsFalse(voiceTask.Result.Success);
            Assert.AreEqual(CommanderVoiceState.Error, chat.VoiceState);
            Assert.IsNull(chat.LatestSubmission);

            // Keyboard text Commander remains completely functional
            Task<CommanderAIChatSubmission> keybTask = chat.SubmitMessageAsync("make ten spearmen");
            while (!keybTask.IsCompleted) yield return null;

            Assert.IsNotNull(keybTask.Result);
            Assert.IsNotNull(chat.LatestSubmission);
            StringAssert.Contains("make ten spearmen", chat.DisplayedTranscript);
        }

        [UnityTest]
        public IEnumerator ScenarioD_MixedConversation_VoiceThenTyped_SharesMemory()
        {
            var mockStt = new MockCommanderSpeechToTextProvider("make five archers");
            var mockCapture = new PlayModeTestAudioCapture();
            chat.SetVoiceProvider(mockStt, mockCapture);
            chat.VoiceController.AutoSubmit = true;

            // Step 1: Voice command
            chat.VoiceController.StartRecording();
            Task<CommanderSpeechToTextResult> voiceTask = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!voiceTask.IsCompleted) yield return null;

            Assert.IsNotNull(chat.LatestSubmission);

            // Step 2: Typed follow-up
            Task<CommanderAIChatSubmission> typedTask = chat.SubmitMessageAsync("make five more");
            while (!typedTask.IsCompleted) yield return null;

            Assert.IsNotNull(typedTask.Result);

            // Both entries exist in single unified conversation memory
            IReadOnlyList<MemoryEntry> entries = chat.Conversation.Snapshot();
            Assert.GreaterOrEqual(entries.Count, 2);

            bool foundVoice = false;
            bool foundTyped = false;
            foreach (var e in entries)
            {
                if (e.Text.Contains("make five archers")) foundVoice = true;
                if (e.Text.Contains("make five more")) foundTyped = true;
            }

            Assert.IsTrue(foundVoice, "Conversation memory must contain the spoken turn.");
            Assert.IsTrue(foundTyped, "Conversation memory must contain the typed turn.");
        }

        [UnityTest]
        public IEnumerator ScenarioE_VoiceQA_CounterQuery_ExecutesCleanly()
        {
            var mockStt = new MockCommanderSpeechToTextProvider("what counters spearmen?");
            var mockCapture = new PlayModeTestAudioCapture();
            chat.SetVoiceProvider(mockStt, mockCapture);
            chat.VoiceController.AutoSubmit = true;

            chat.VoiceController.StartRecording();
            Task<CommanderSpeechToTextResult> voiceTask = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!voiceTask.IsCompleted) yield return null;

            Assert.IsTrue(voiceTask.Result.Success);
            StringAssert.Contains("what counters spearmen", chat.DisplayedTranscript);
            // 0 gameplay goals created for Q&A query
            Assert.AreEqual(0, goals.ActiveGoals.Count);
        }

        private sealed class PlayModeTestAudioCapture : ICommanderAudioCapture
        {
            public bool IsRecording { get; private set; }
            public string CurrentDevice => "MockMic";

            public bool StartRecording(string deviceName = null, float maxDurationSeconds = 15f)
            {
                IsRecording = true;
                return true;
            }

            public CommanderAudioData StopRecording()
            {
                IsRecording = false;
                return new CommanderAudioData(new float[16000], 16000, 1);
            }

            public void CancelRecording() => IsRecording = false;
            public void Dispose() => IsRecording = false;
        }

        private sealed class SemanticMockProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            public Task<CommanderAIProviderResult> TranslateAsync(
                CommanderAIRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(CommanderAIProviderResult.Accepted(
                    new CommanderIntentDTO { action = "TrainUnit", unit = "Spearman", amount = 10 },
                    "mock", "Mock order accepted."));
            }

            public Task<CommanderSemanticResult> TranslateSemanticAsync(
                CommanderSemanticProviderRequest request, CancellationToken cancellationToken)
            {
                string text = (request?.PlayerMessage ?? string.Empty).ToLowerInvariant();

                if (text.Contains("what counters"))
                {
                    return Task.FromResult(CommanderSemanticJson.Parse(
                        "{\"outcome\":\"Clarify\",\"message\":\"Archers and Crossbowmen counter spearmen.\"}"));
                }

                if (text.Contains("archer") || text.Contains("five more"))
                {
                    return Task.FromResult(CommanderSemanticJson.Parse(
                        "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Archer\",\"count\":5}]}"));
                }

                return Task.FromResult(CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}"));
            }
        }
    }
}
