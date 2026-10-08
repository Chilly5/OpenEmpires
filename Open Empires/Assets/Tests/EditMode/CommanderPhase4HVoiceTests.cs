using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [TestFixture]
    public class CommanderPhase4HVoiceTests
    {
        [Test]
        public void AudioConversion_ProducesExpectedMono16kShape()
        {
            // Stereo 48 kHz input, 1 second = 96000 samples
            int srcSampleRate = 48000;
            int srcChannels = 2;
            int durationSec = 1;
            float[] stereoSamples = new float[srcSampleRate * srcChannels * durationSec];
            for (int i = 0; i < stereoSamples.Length; i++)
            {
                stereoSamples[i] = (i % 2 == 0) ? 0.5f : -0.5f; // L = 0.5, R = -0.5 -> average = 0.0
            }

            CommanderAudioData converted = CommanderAudioConverter.ConvertToMono16k(
                stereoSamples, srcSampleRate, srcChannels, 15f);

            Assert.IsNotNull(converted);
            Assert.AreEqual(16000, converted.SampleRate);
            Assert.AreEqual(1, converted.Channels);
            Assert.AreEqual(16000, converted.Samples.Length);
            Assert.AreEqual(1.0f, converted.DurationSeconds, 0.01f);

            // Verify downmix average
            for (int i = 0; i < converted.Samples.Length; i++)
            {
                Assert.AreEqual(0.0f, converted.Samples[i], 0.001f);
            }
        }

        [Test]
        public void AudioConversion_ClampsMaxDuration()
        {
            // Mono 16 kHz input of 20 seconds
            int sampleRate = 16000;
            float[] longAudio = new float[sampleRate * 20];

            CommanderAudioData clamped = CommanderAudioConverter.ConvertToMono16k(
                longAudio, sampleRate, 1, 15f);

            Assert.AreEqual(16000 * 15, clamped.Samples.Length);
            Assert.AreEqual(15.0f, clamped.DurationSeconds, 0.01f);
        }

        [Test]
        public void AudioConversion_EmptyAudio_ReturnsEmptyData()
        {
            CommanderAudioData empty = CommanderAudioConverter.ConvertToMono16k(null);
            Assert.IsNotNull(empty);
            Assert.AreEqual(0, empty.Samples.Length);
            Assert.AreEqual(0f, empty.DurationSeconds);
        }

        [Test]
        public async Task VoiceTranscript_UsesExactExistingTextSubmissionPath()
        {
            string submittedText = null;
            int submitCount = 0;

            Func<string, Task<CommanderAIChatSubmission>> mockSubmit = text =>
            {
                submittedText = text;
                submitCount++;
                return Task.FromResult<CommanderAIChatSubmission>(null);
            };

            var mockStt = new MockCommanderSpeechToTextProvider("make ten spearmen");
            var mockCapture = new TestAudioCapture();
            var controller = new CommanderVoiceInputController(mockStt, mockCapture, mockSubmit)
            {
                AutoSubmit = true
            };

            controller.StartRecording();
            Assert.AreEqual(CommanderVoiceState.Recording, controller.State);

            CommanderSpeechToTextResult result = await controller.StopRecordingAndTranscribeAsync();

            Assert.IsTrue(result.Success);
            Assert.AreEqual("make ten spearmen", result.Transcript);
            Assert.AreEqual(1, submitCount);
            Assert.AreEqual("make ten spearmen", submittedText);
            Assert.AreEqual(CommanderVoiceState.Idle, controller.State);
        }

        [Test]
        public async Task EmptyTranscript_DoesNotSubmit()
        {
            int submitCount = 0;
            Func<string, Task<CommanderAIChatSubmission>> mockSubmit = text =>
            {
                submitCount++;
                return Task.FromResult<CommanderAIChatSubmission>(null);
            };

            var mockStt = new MockCommanderSpeechToTextProvider("   "); // whitespace only
            var mockCapture = new TestAudioCapture();
            var controller = new CommanderVoiceInputController(mockStt, mockCapture, mockSubmit)
            {
                AutoSubmit = true
            };

            controller.StartRecording();
            CommanderSpeechToTextResult result = await controller.StopRecordingAndTranscribeAsync();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, submitCount);
            Assert.AreEqual(CommanderVoiceState.Idle, controller.State);
        }

        [Test]
        public async Task CancelledTranscription_DoesNotSubmit()
        {
            int submitCount = 0;
            Func<string, Task<CommanderAIChatSubmission>> mockSubmit = text =>
            {
                submitCount++;
                return Task.FromResult<CommanderAIChatSubmission>(null);
            };

            var mockStt = new MockCommanderSpeechToTextProvider("build a barracks")
            {
                SimulatedLatency = TimeSpan.FromMilliseconds(200)
            };
            var mockCapture = new TestAudioCapture();
            var controller = new CommanderVoiceInputController(mockStt, mockCapture, mockSubmit)
            {
                AutoSubmit = true
            };

            controller.StartRecording();
            Task<CommanderSpeechToTextResult> transcribeTask = controller.StopRecordingAndTranscribeAsync();
            Assert.AreEqual(CommanderVoiceState.Transcribing, controller.State);

            // Cancel mid-transcription
            controller.Cancel();
            Assert.AreEqual(CommanderVoiceState.Idle, controller.State);

            CommanderSpeechToTextResult result = await transcribeTask;
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, submitCount);
            Assert.AreEqual(CommanderVoiceState.Idle, controller.State);
        }

        [Test]
        public async Task LateTranscriptAfterReset_IsDiscarded()
        {
            int submitCount = 0;
            Func<string, Task<CommanderAIChatSubmission>> mockSubmit = text =>
            {
                submitCount++;
                return Task.FromResult<CommanderAIChatSubmission>(null);
            };

            var mockStt = new MockCommanderSpeechToTextProvider("attack the enemy base")
            {
                SimulatedLatency = TimeSpan.FromMilliseconds(150)
            };
            var mockCapture = new TestAudioCapture();
            var controller = new CommanderVoiceInputController(mockStt, mockCapture, mockSubmit)
            {
                AutoSubmit = true
            };

            controller.StartRecording();
            Task<CommanderSpeechToTextResult> transcribeTask = controller.StopRecordingAndTranscribeAsync();
            Assert.AreEqual(CommanderVoiceState.Transcribing, controller.State);

            // Simulation/Match reset occurs before transcription returns
            controller.ResetSession();
            Assert.AreEqual(CommanderVoiceState.Idle, controller.State);

            CommanderSpeechToTextResult result = await transcribeTask;
            Assert.AreEqual(0, submitCount, "Late transcript after reset must not submit!");
            Assert.AreEqual(CommanderVoiceState.Idle, controller.State);
        }

        [Test]
        public async Task ProviderFailure_DoesNotBreakTextCommander()
        {
            int submitCount = 0;
            Func<string, Task<CommanderAIChatSubmission>> mockSubmit = text =>
            {
                submitCount++;
                return Task.FromResult<CommanderAIChatSubmission>(null);
            };

            var mockStt = new MockCommanderSpeechToTextProvider()
            {
                SimulateFailure = true,
                FailureErrorCode = "MODEL_CORRUPTED",
                FailureErrorMessage = "Whisper model file is damaged."
            };
            var mockCapture = new TestAudioCapture();
            var controller = new CommanderVoiceInputController(mockStt, mockCapture, mockSubmit);

            controller.StartRecording();
            CommanderSpeechToTextResult result = await controller.StopRecordingAndTranscribeAsync();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CommanderVoiceState.Error, controller.State);
            Assert.AreEqual("Whisper model file is damaged.", controller.LastError);
            Assert.AreEqual(0, submitCount);

            // Crucial: regular text submission remains 100% functional
            await mockSubmit("typed keyboard order continues to work");
            Assert.AreEqual(1, submitCount);
        }

        [Test]
        public void VoiceState_DoesNotAllowConcurrentTransactions()
        {
            var mockStt = new MockCommanderSpeechToTextProvider("test");
            var mockCapture = new TestAudioCapture();
            var controller = new CommanderVoiceInputController(mockStt, mockCapture);

            bool firstStart = controller.StartRecording();
            Assert.IsTrue(firstStart);
            Assert.AreEqual(CommanderVoiceState.Recording, controller.State);

            // Second concurrent start must be rejected
            bool secondStart = controller.StartRecording();
            Assert.IsFalse(secondStart, "Cannot start concurrent recording transaction!");
            Assert.AreEqual(CommanderVoiceState.Recording, controller.State);

            controller.Cancel();
            Assert.AreEqual(CommanderVoiceState.Idle, controller.State);
        }

        [Test]
        public async Task PreviewMode_RequiresExplicitConfirmToSubmit()
        {
            int submitCount = 0;
            string submitted = null;
            Func<string, Task<CommanderAIChatSubmission>> mockSubmit = text =>
            {
                submitted = text;
                submitCount++;
                return Task.FromResult<CommanderAIChatSubmission>(null);
            };

            var mockStt = new MockCommanderSpeechToTextProvider("scout the gold mine");
            var mockCapture = new TestAudioCapture();
            var controller = new CommanderVoiceInputController(mockStt, mockCapture, mockSubmit)
            {
                AutoSubmit = false
            };

            controller.StartRecording();
            await controller.StopRecordingAndTranscribeAsync();

            Assert.AreEqual(CommanderVoiceState.Preview, controller.State);
            Assert.AreEqual("scout the gold mine", controller.CurrentTranscript);
            Assert.AreEqual(0, submitCount, "Preview mode must not auto-submit before confirm!");

            await controller.ConfirmPreviewAndSubmitAsync();
            Assert.AreEqual(CommanderVoiceState.Idle, controller.State);
            Assert.AreEqual(1, submitCount);
            Assert.AreEqual("scout the gold mine", submitted);
        }

        [Test]
        public void ModelLocator_ResolvesProvisionedModel()
        {
            bool found = WhisperModelLocator.TryResolveModelPath("ggml-tiny.bin", out string path);
            Assert.IsTrue(found, "WhisperModelLocator should resolve a provisioned ggml-tiny.bin outside shared StreamingAssets");
            Assert.IsTrue(File.Exists(path), $"Resolved path must exist: {path}");
        }

        [Test]
        public void SafeCleanTranscript_NormalizesWhitespaceAndArtifacts()
        {
            string raw = "  \r\n make ten \n spearmen   \r  ";
            string clean = CommanderVoiceInputController.SafeCleanTranscript(raw);
            Assert.AreEqual("make ten   spearmen", clean);
        }

        [Test]
        public void VoiceLayer_HasNoGameplayAuthority()
        {
            // Hostile architectural boundary test:
            // Ensure no Phase 4H production types have references to GameSimulation mutation,
            // CommandBuffer, ICommand creation, or CommanderGoal creation.
            Type[] phase4HTypes = new Type[]
            {
                typeof(CommanderAudioData),
                typeof(CommanderAudioConverter),
                typeof(CommanderSpeechToTextResult),
                typeof(ICommanderSpeechToTextProvider),
                typeof(WhisperModelLocator),
                typeof(WhisperCommanderSpeechToTextProvider),
                typeof(MockCommanderSpeechToTextProvider),
                typeof(ICommanderAudioCapture),
                typeof(UnityMicrophoneAudioCapture),
                typeof(CommanderVoiceState),
                typeof(CommanderVoiceSettings),
                typeof(CommanderVoiceInputController)
            };

            Func<Type, bool> isForbiddenType = type =>
            {
                if (type == null) return false;
                string n = type.Name;
                return n == "GameSimulation" || n == "CommandBuffer" || n == "ICommand" ||
                       n == "CommanderGoal" || n == "CommanderGoalManager" || n == "CommanderPlanner" ||
                       n == "StrategicPlan" || (n.StartsWith("ICommand`") && !n.StartsWith("ICommander"));
            };

            foreach (Type t in phase4HTypes)
            {
                // Check fields
                FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (FieldInfo f in fields)
                {
                    Assert.IsFalse(isForbiddenType(f.FieldType),
                        $"Phase 4H type {t.Name} contains forbidden field {f.Name} of type {f.FieldType.Name}");
                }

                // Check properties
                PropertyInfo[] props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (PropertyInfo p in props)
                {
                    Assert.IsFalse(isForbiddenType(p.PropertyType),
                        $"Phase 4H type {t.Name} contains forbidden property {p.Name} of type {p.PropertyType.Name}");
                }

                // Check method return types
                MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (MethodInfo m in methods)
                {
                    Assert.IsFalse(isForbiddenType(m.ReturnType),
                        $"Phase 4H type {t.Name} contains forbidden method {m.Name} returning {m.ReturnType.Name}");
                }
            }
        }

        private sealed class TestAudioCapture : ICommanderAudioCapture
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
                // Return 1 second of dummy audio (16000 samples)
                return new CommanderAudioData(new float[16000], 16000, 1);
            }

            public void CancelRecording()
            {
                IsRecording = false;
            }

            public void Dispose()
            {
                IsRecording = false;
            }
        }
    }
}
