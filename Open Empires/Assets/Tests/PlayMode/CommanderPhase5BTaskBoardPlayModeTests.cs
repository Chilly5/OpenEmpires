using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5B")]
    public sealed class CommanderPhase5BTaskBoardPlayModeTests
    {
        private SimulationConfig config;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;

        [SetUp]
        public void SetUp()
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            manager = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, manager);
            chat = new GameObject("Phase5BTaskBoardChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(new OfflineProvider(), simulation, manager, dispatcher);
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            manager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator ActiveGoal_UpdatesCollapsedBadgeWithoutOpeningChat_AndCancelUsesCardControl()
        {
            chat.enabled = true;
            var goal = manager.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            yield return null;

            Assert.That(chat.IsExpanded, Is.False);
            var badge = chat.transform.Find("CommanderCanvas/CompactCommander/TaskBadge");
            Assert.That(badge, Is.Not.Null);
            Assert.That(TextOf(badge), Does.Contain("1"));

            chat.OpenCommander();
            yield return null;
            var board = chat.transform.Find("CommanderCanvas/Panel/TaskBoard");
            Assert.That(board, Is.Not.Null);
            Assert.That(board.GetComponent<ScrollRect>(), Is.Not.Null, "Long task history must scroll inside the panel.");
            Assert.That(board.GetComponentsInChildren<Component>(true).Any(x => x.GetType().Name == "TextMeshProUGUI"
                && TextOf(x.transform).Contains("Age") && TextOf(x.transform).Contains("3")), Is.True);
            var cancel = board.GetComponentsInChildren<Button>(true).Single(x => x.name == "CancelTask");
            cancel.onClick.Invoke();
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
        }

        [UnityTest]
        public IEnumerator ConversationReset_RebindsVisibleCancelToFreshCardWithoutOpeningChat()
        {
            chat.enabled = true;
            var goal = manager.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            yield return null;
            chat.ResetConversation();
            yield return null;

            Assert.That(chat.IsExpanded, Is.False, "Conversation reset retains the established minimized presentation.");
            chat.OpenCommander();
            yield return null;
            Assert.That(chat.IsExpanded, Is.True);
            var board = chat.transform.Find("CommanderCanvas/Panel/TaskBoard");
            var cancel = board.GetComponentsInChildren<Button>(true).Single(x => x.name == "CancelTask");
            cancel.onClick.Invoke();
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled),
                "Reset must refresh the captured card token while preserving live ordinary goals.");
        }

        [UnityTest]
        public IEnumerator BlockedReason_FitsInFirstTaskViewportWithoutForcingChatOpen()
        {
            chat.enabled = true;
            var goal = manager.SubmitBuildStructure(BuildingType.House);
            goal.SetStatus(CommanderGoalStatus.Blocked, "No owned living villager is available.");
            yield return null;

            Assert.That(chat.IsExpanded, Is.False, "A blocker must not force the Commander open.");
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var viewport = chat.transform.Find("CommanderCanvas/Panel/TaskBoard/Viewport")
                .GetComponent<RectTransform>();
            var card = viewport.Find("Content/TaskCard");
            Assert.That(card, Is.Not.Null);
            Assert.That(card.GetChild(0).name, Is.EqualTo("Status"));
            Assert.That(TextOf(card.GetChild(0)), Does.Contain("No owned living villager"));
            var status = card.GetChild(0).GetComponent<RectTransform>();
            var viewportCorners = new Vector3[4];
            var statusCorners = new Vector3[4];
            viewport.GetWorldCorners(viewportCorners);
            status.GetWorldCorners(statusCorners);
            Assert.That(statusCorners[0].y, Is.GreaterThanOrEqualTo(viewportCorners[0].y - 1f));
            Assert.That(statusCorners[1].y, Is.LessThanOrEqualTo(viewportCorners[1].y + 1f));
        }

        private static string TextOf(Transform transform)
        {
            var component = transform.GetComponents<Component>().FirstOrDefault(x => x.GetType().Name == "TextMeshProUGUI");
            return component == null ? string.Empty : (string)component.GetType().GetProperty("text").GetValue(component);
        }

        private sealed class OfflineProvider : ICommanderAIProvider
        {
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
                => Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure, "offline test"));
        }
    }
}
