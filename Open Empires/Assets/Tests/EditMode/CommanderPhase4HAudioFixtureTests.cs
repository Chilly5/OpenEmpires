using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [TestFixture]
    public class CommanderPhase4HAudioFixtureTests
    {
        private WhisperCommanderSpeechToTextProvider ownedProvider;
        [TearDown]
        public async Task AwaitOwnedNativeRelease()
        {
            if(ownedProvider==null)return;
            ownedProvider.Dispose();
            var property=typeof(WhisperCommanderSpeechToTextProvider).GetProperty("ReleaseCompletion",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var release=(Task)property.GetValue(ownedProvider);
            Assert.That(await Task.WhenAny(release,Task.Delay(10000)),Is.SameAs(release),"Fixture must await its actual native release before the next model begins.");
            await release;ownedProvider=null;
        }
        private static string InitializationCategory(WhisperCommanderSpeechToTextProvider provider)
        {
            var context=typeof(WhisperCommanderSpeechToTextProvider).GetField("context",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(provider);
            return (string)context.GetType().GetProperty("FailureCode",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(context);
        }
        private static CommanderAudioData LoadWavFile(string relativePath)
        {
            string fullPath = Path.Combine(Application.dataPath, relativePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Test audio fixture not found at " + fullPath);

            byte[] bytes = File.ReadAllBytes(fullPath);
            int channels = BitConverter.ToInt16(bytes, 22);
            int sampleRate = BitConverter.ToInt32(bytes, 24);
            int bitsPerSample = BitConverter.ToInt16(bytes, 34);

            int pos = 12;
            while (pos < bytes.Length - 8)
            {
                string chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                if (chunkId == "data") { pos += 8; break; }
                pos += 8 + chunkSize;
            }

            int sampleCount = (bytes.Length - pos) / (bitsPerSample / 8);
            float[] samples = new float[sampleCount];
            if (bitsPerSample == 16)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    short val = BitConverter.ToInt16(bytes, pos + i * 2);
                    samples[i] = val / 32768.0f;
                }
            }

            return new CommanderAudioData(samples, sampleRate, channels);
        }

        [Test]
        public async Task AudioFixture_MakeTenSpearmen_TranscribesAccurately()
        {
            CommanderAudioData audio = LoadWavFile("Tests/Fixtures/Audio/make_ten_spearmen.wav");
            Assert.Greater(audio.Samples.Length, 0);

            using (var provider = ownedProvider = new WhisperCommanderSpeechToTextProvider())
            {
                bool initialized = provider.Initialize();
                Assert.IsTrue(initialized, "Verified provisioned model initialization failed; category="+InitializationCategory(provider));

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    CommanderSpeechToTextResult result = await provider.TranscribeAsync(audio, cts.Token);
                    Assert.IsTrue(result.Success, $"STT should succeed. Error: {result.UserFacingError}");

                    string text = result.Transcript.ToLowerInvariant();
                    Debug.Log($"[Test] AudioFixture_MakeTenSpearmen transcript: '{result.Transcript}' in {provider.LastInferenceTimeMs}ms");

                    Assert.IsTrue(text.Contains("make"), $"Transcript '{text}' should contain 'make'");
                    Assert.IsTrue(text.Contains("10") || text.Contains("ten"), $"Transcript '{text}' should contain '10' or 'ten'");
                    Assert.IsTrue(text.Contains("spear"), $"Transcript '{text}' should contain 'spear'");
                }
            }
        }

        [Test]
        public async Task AudioFixture_BuildMill_TranscribesAccurately()
        {
            CommanderAudioData audio = LoadWavFile("Tests/Fixtures/Audio/build_mill.wav");
            Assert.Greater(audio.Samples.Length, 0);

            using (var provider = ownedProvider = new WhisperCommanderSpeechToTextProvider())
            {
                bool initialized = provider.Initialize();
                Assert.IsTrue(initialized,"Verified provisioned model initialization failed; category="+InitializationCategory(provider));

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    CommanderSpeechToTextResult result = await provider.TranscribeAsync(audio, cts.Token);
                    Assert.IsTrue(result.Success);

                    string text = result.Transcript.ToLowerInvariant();
                    Debug.Log($"[Test] AudioFixture_BuildMill transcript: '{result.Transcript}' in {provider.LastInferenceTimeMs}ms");

                    Assert.IsTrue(text.Contains("build"), $"Transcript '{text}' should contain 'build'");
                    Assert.IsTrue(text.Contains("mill"), $"Transcript '{text}' should contain 'mill'");
                    Assert.IsTrue(text.Contains("berries") || text.Contains("berry"), $"Transcript '{text}' should contain 'berries'");
                }
            }
        }

        [Test]
        public async Task AudioFixture_BaseEnPureSilence_DoesNotHallucinateTranscript()
        {
            ownedProvider=new WhisperCommanderSpeechToTextProvider("ggml-base.en.bin");
            Assert.That(ownedProvider.Initialize(),Is.True,
                "Verified base.en candidate initialization failed; category="+InitializationCategory(ownedProvider));
            using(var cts=new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                var result=await ownedProvider.TranscribeAsync(new CommanderAudioData(new float[32000],16000,1),cts.Token);
                Assert.That(string.IsNullOrWhiteSpace(result.Transcript),Is.True,
                    "A physically empty waveform must not become a sendable native-model hallucination.");
                Assert.That(result.ErrorCode,Is.EqualTo("EMPTY_TRANSCRIPTION"),
                    "Unavailable/busy/cancelled/failed inference is not proof of the no-speech guard.");
                Assert.That(result.Success,Is.False);
            }
        }

        [Test]
        public async Task AudioFixture_PureSilence_DoesNotProduceGarbageOrder()
        {
            // 2 seconds of pure silence (all 0.0f)
            float[] silentSamples = new float[16000 * 2];
            CommanderAudioData silentAudio = new CommanderAudioData(silentSamples, 16000, 1);

            using (var provider = ownedProvider = new WhisperCommanderSpeechToTextProvider())
            {
                provider.Initialize();
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    CommanderSpeechToTextResult result = await provider.TranscribeAsync(silentAudio, cts.Token);
                    // Whisper returns either empty or no speech
                    string text = (result?.Transcript ?? string.Empty).Trim();
                    Assert.IsTrue(string.IsNullOrWhiteSpace(text) || !result.Success,
                        $"Pure silence should produce empty transcript or fail closed, but got '{text}'");
                }
            }
        }
    }
}
