using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E1")]
    public sealed class CommanderPhase4E1ChatTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private CommanderIntentDispatcher dispatcher;
        private readonly List<GameObject> objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            goals = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goals);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            objects.Clear();
            dispatcher?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [TestCase("Could you make sure we have ten spearmen?")]
        [TestCase("I'd like our spearman force to reach ten.")]
        [TestCase("We need a total of ten spearmen in the army.")]
        [TestCase("Please bring the spearmen count up to 10.")]
        [TestCase("Can we field ten spearmen?")]
        [TestCase("Aim for a squad of ten spearmen.")]
        [TestCase("Get us to 10 spearmen when you can.")]
        public async Task SemanticProvider_AdmitsSpearmanParaphrasesWithoutExactPhraseGate(string phrase)
        {
            var provider = new FakeSemanticProvider(Unit(10));
            CommanderChatUI chat = CreateChat(provider);

            CommanderAIChatSubmission result = await chat.SubmitMessageAsync(phrase);

            Assert.That(provider.SemanticCalls, Is.EqualTo(1));
            Assert.That(provider.LegacyCalls, Is.Zero);
            Assert.That(provider.LastRequest.PlayerMessage, Is.EqualTo(phrase));
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True, result?.DisplayText);
            Assert.That(goals.Goals.OfType<EnsureUnitCountGoal>().Single().TargetTotal, Is.EqualTo(10));
            Assert.That(result.Interpretation.Intent.PlayerId, Is.EqualTo(0));
        }

        [Test]
        public void VoiceBar_HasSeparateSpaceFromStrategyControls()
        {
            var chat = CreateChat(new FakeSemanticProvider(Unit(3)));
            var voice = chat.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "VoiceBar");
            var pause = chat.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "Pause");
            pause.gameObject.SetActive(true);
            pause.parent.gameObject.SetActive(true);
            var panel = voice.parent.GetComponent<RectTransform>();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            var voiceCorners = new Vector3[4];
            var pauseCorners = new Vector3[4];
            voice.GetWorldCorners(voiceCorners);
            pause.GetWorldCorners(pauseCorners);
            Assert.That(voiceCorners[1].y, Is.LessThanOrEqualTo(pauseCorners[0].y));
        }

        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}]}", typeof(BuildStructureGoal))]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"SetResourceAllocation\",\"resource\":\"Food\",\"count\":2}]}", typeof(ResourceAllocationGoal))]
        public async Task SemanticProvider_AdmitsOtherTacticalNodes(string json, Type goalType)
        {
            var provider = new FakeSemanticProvider(json);
            CommanderChatUI chat = CreateChat(provider);

            CommanderAIChatSubmission result = await chat.SubmitMessageAsync("Please arrange this for us.");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True, result?.DisplayText);
            Assert.That(goals.Goals.Count, Is.EqualTo(1));
            Assert.That(goals.Goals.Single(), Is.InstanceOf(goalType));
            Assert.That(provider.LegacyCalls, Is.Zero);
        }

        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Trebuchet\",\"count\":10}]}")]
        [TestCase("{\"outcome\":\"Clarify\",\"message\":\"Which unit?\"}")]
        [TestCase("{\"outcome\":\"Unsupported\",\"message\":\"I cannot do that.\"}")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]")]
        public async Task NonTacticalOrInvalidSemanticResult_CreatesNoGoal(string json)
        {
            var provider = new FakeSemanticProvider(json);
            CommanderChatUI chat = CreateChat(provider);

            CommanderAIChatSubmission result = await chat.SubmitMessageAsync("Could you help with this?");

            Assert.That(provider.SemanticCalls, Is.EqualTo(1));
            Assert.That(provider.LegacyCalls, Is.Zero);
            Assert.That(result == null || !result.Success, Is.True);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public async Task TwoNodeResponse_IsAdmittedAtomically()
        {
            var provider = new FakeSemanticProvider("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}");
            CommanderChatUI chat = CreateChat(provider);

            CommanderAIChatSubmission result = await chat.SubmitMessageAsync("Build and train these.");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True, result?.DisplayText);
            Assert.That(goals.Goals, Has.Count.EqualTo(2));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public async Task BusyRequest_DoesNotStartSecondProviderCall_AndControlsRecover()
        {
            var provider = new FakeSemanticProvider(Unit(10)) { Hold = true };
            CommanderChatUI chat = CreateChat(provider);

            Task<CommanderAIChatSubmission> first = chat.SubmitMessageAsync("Please field ten spearmen.");
            Assert.That(first.IsCompleted, Is.False);
            Assert.That(ReadInteractable(chat, "TMP_InputField"), Is.False);
            Assert.That(ReadInteractable(chat, "Button"), Is.False);
            Assert.That(await chat.SubmitMessageAsync("And another order"), Is.Null);
            Assert.That(provider.SemanticCalls, Is.EqualTo(1));

            provider.Release();
            Assert.That((await first).Success, Is.True);
            Assert.That(ReadInteractable(chat, "TMP_InputField"), Is.True);
            Assert.That(ReadInteractable(chat, "Button"), Is.True);
        }

        [Test]
        public async Task ResetWhileProviderPending_RejectsLateResultWithoutGoal()
        {
            var provider = new FakeSemanticProvider(Unit(10)) { Hold = true };
            CommanderChatUI chat = CreateChat(provider);

            Task<CommanderAIChatSubmission> pending = chat.SubmitMessageAsync("Please field ten spearmen.");
            chat.ResetConversation();
            provider.Release();
            CommanderAIChatSubmission result = await pending;

            Assert.That(result == null || !result.Success, Is.True);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(ReadInteractable(chat, "TMP_InputField"), Is.True);
        }

        [Test]
        public async Task ReinitializeForDifferentRuntimeAndOwner_RejectsOldProviderResult()
        {
            var oldProvider = new FakeSemanticProvider(Unit(10)) { Hold = true };
            CommanderChatUI chat = CreateChat(oldProvider);
            Task<CommanderAIChatSubmission> pending = chat.SubmitMessageAsync(
                "Please field ten spearmen.");
            Assert.That(pending.IsCompleted, Is.False);
            Assert.That(oldProvider.SemanticCalls, Is.EqualTo(1));

            var replacementSimulation = new GameSimulation(config, 2,
                new[] { 0, 1 }, Array.Empty<int>());
            var replacementGoals = new CommanderGoalManager(replacementSimulation, 1);
            var replacementDispatcher = new CommanderIntentDispatcher(
                replacementSimulation, replacementGoals);
            try
            {
                var replacementProvider = new FakeSemanticProvider(Unit(10));
                chat.Initialize(replacementProvider, replacementSimulation,
                    replacementGoals, replacementDispatcher);
                Assert.That(chat.Conversation.PlayerId, Is.EqualTo(1));

                oldProvider.Release();
                CommanderAIChatSubmission staleResult = await pending;

                Assert.That(staleResult == null || !staleResult.Success, Is.True);
                Assert.That(goals.Goals, Is.Empty);
                Assert.That(replacementGoals.Goals, Is.Empty);
                Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(replacementSimulation.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(replacementProvider.SemanticCalls, Is.Zero);
                Assert.That(ReadInteractable(chat, "TMP_InputField"), Is.True);
                Assert.That(ReadInteractable(chat, "Button"), Is.True);
            }
            finally
            {
                replacementDispatcher.Dispose();
                replacementGoals.Dispose();
            }
        }

        [Test]
        public async Task ProviderFailure_LeavesControlsUsable_AndDoesNotEchoUnsafeText()
        {
            var provider = new FakeSemanticProvider(Unit(10)) { Failure = new InvalidOperationException("secret-provider-detail") };
            CommanderChatUI chat = CreateChat(provider);

            CommanderAIChatSubmission result = await chat.SubmitMessageAsync("Please field ten spearmen.");

            Assert.That(result == null || !result.Success, Is.True);
            Assert.That(goals.Goals, Is.Empty);
            StringAssert.DoesNotContain("secret-provider-detail", chat.DisplayedTranscript);
            Assert.That(ReadInteractable(chat, "TMP_InputField"), Is.True);
            Assert.That(ReadInteractable(chat, "Button"), Is.True);
        }

        [Test]
        public async Task NonCooperativeProvider_TimeoutUnlocksControls_AndLateReplyCannotAdmit()
        {
            var provider = new FakeSemanticProvider(Unit(10)) { Hold = true };
            var gameObject = new GameObject("CommanderPhase4E1TimeoutChat");
            objects.Add(gameObject);
            CommanderChatUI chat = gameObject.AddComponent<CommanderChatUI>();
            chat.Initialize(provider, simulation, goals, dispatcher,
                TimeSpan.FromMilliseconds(30));

            CommanderAIChatSubmission result = await chat.SubmitMessageAsync("Please field ten spearmen.");

            Assert.That(result == null || !result.Success, Is.True);
            Assert.That(ReadInteractable(chat, "TMP_InputField"), Is.True);
            Assert.That(ReadInteractable(chat, "Button"), Is.True);
            Assert.That(goals.Goals, Is.Empty);
            provider.Release();
            await Task.Yield();
            Assert.That(goals.Goals, Is.Empty);
        }

        [Test]
        public async Task OfflineStatus_BypassesSemanticProvider()
        {
            var provider = new FakeSemanticProvider(Unit(10));
            CommanderChatUI chat = CreateChat(provider);

            await chat.SubmitMessageAsync("strategy status");

            Assert.That(provider.SemanticCalls, Is.Zero);
            Assert.That(provider.LegacyCalls, Is.Zero);
            Assert.That(goals.Goals, Is.Empty);
        }

        [Test]
        public void TacticalAdmission_RejectsStrategicNodeWithoutConvertingToPlayerDirect()
        {
            CommanderContext context = new CommanderContextBuilder().Build(simulation, goals);
            CommanderSemanticNode node = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"RangedReinforcement\"}]}")
                .Nodes.Single();

            bool admitted = CommanderSemanticAdmission.TryCreateTacticalIntent(node, context,
                out CommanderIntent intent, out string reason);

            Assert.That(admitted, Is.False);
            Assert.That(intent, Is.Null);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public async Task LunaQuestion_RejectsExecutableResponseWithoutMutatingGoals()
        {
            var transport = new QuestionTransport();
            var chat = CreateChat(new OpenRouterCommanderProvider("test-only-key", transport));
            var submission = await chat.SubmitMessageAsync("what counters spearmen?");
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(submission, Is.Null);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        private sealed class QuestionTransport : ICommanderHttpTransport
        {
            public int Calls;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Calls++;
                string encoded = Unit(3).Replace("\"", "\\\"");
                return Task.FromResult(new CommanderHttpResponse(200,
                    "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"" + encoded + "\"}}]}"));
            }
        }

        private CommanderChatUI CreateChat(ICommanderAIProvider provider)
        {
            var gameObject = new GameObject("CommanderPhase4E1Chat");
            objects.Add(gameObject);
            CommanderChatUI chat = gameObject.AddComponent<CommanderChatUI>();
            chat.Initialize(provider, simulation, goals, dispatcher);
            return chat;
        }

        private static bool ReadInteractable(CommanderChatUI chat, string componentName)
        {
            FieldInfo field = typeof(CommanderChatUI).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(item => item.FieldType.Name == componentName && item.Name ==
                    (componentName == "TMP_InputField" ? "inputField" : "sendButton"));
            return (bool)field.GetValue(chat).GetType().GetProperty("interactable").GetValue(field.GetValue(chat));
        }

        private static string Unit(int count) => "{\"outcome\":\"Request\",\"nodes\":["
            + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":" + count + "}]}";

        private sealed class FakeSemanticProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private readonly string json;
            private TaskCompletionSource<bool> release;
            public bool Hold { get; set; }
            public Exception Failure { get; set; }
            public int SemanticCalls { get; private set; }
            public int LegacyCalls { get; private set; }
            public CommanderSemanticProviderRequest LastRequest { get; private set; }

            public FakeSemanticProvider(string json) { this.json = json; }

            public async Task<CommanderSemanticResult> TranslateSemanticAsync(
                CommanderSemanticProviderRequest request, CancellationToken token)
            {
                SemanticCalls++;
                LastRequest = request;
                if (Hold)
                {
                    release = new TaskCompletionSource<bool>();
                    await release.Task;
                }
                if (Failure != null) throw Failure;
                return CommanderSemanticJson.Parse(json);
            }

            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
                CancellationToken token)
            {
                LegacyCalls++;
                return Task.FromResult(CommanderAIProviderResult.Rejected(
                    CommanderIntentErrorCode.ProviderFailure, "Legacy route was called."));
            }

            public void Release() => release?.SetResult(true);
        }
    }
}
