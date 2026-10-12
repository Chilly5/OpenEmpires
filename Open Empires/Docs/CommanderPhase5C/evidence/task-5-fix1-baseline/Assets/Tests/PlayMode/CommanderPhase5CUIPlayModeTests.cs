using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5COffline")]
    public sealed class CommanderPhase5CUIPlayModeTests
    {
        private CommanderChatUI chat;
        private CommanderGoalManager goals;
        private CommanderIntentDispatcher dispatcher;
        private GameSimulation simulation;
        private SimulationConfig config;
        private SlowProvider provider;
        private GameObject renderCameraObject;
        private int previousWidth, previousHeight;

        [SetUp]
        public void SetUp()
        {
            previousWidth = Screen.width; previousHeight = Screen.height;
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            goals = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goals);
            provider = new SlowProvider();
            chat = new GameObject("Phase5CCommanderUI").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromMilliseconds(60));
        }

        [TearDown]
        public void TearDown()
        {
            if (previousWidth > 0 && previousHeight > 0) Screen.SetResolution(previousWidth, previousHeight, false);
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            if (renderCameraObject != null) UnityEngine.Object.DestroyImmediate(renderCameraObject);
            dispatcher?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator MinimizedStartup_OpensTabbedDockWithFixedMultilineComposer()
        {
            chat.enabled = true;
            yield return null;
            Assert.That(chat.IsExpanded, Is.False);
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/Launcher"), Is.Not.Null);
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Views/ChatTab"), Is.Not.Null);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab"), Is.Not.Null);
            var input = chat.transform.Find("CommanderCanvas/Panel/TextSubmission/Input")?.GetComponent("TMP_InputField");
            Assert.That(input, Is.Not.Null);
            Assert.That(input.GetType().GetProperty("lineType")?.GetValue(input)?.ToString(), Is.EqualTo("MultiLineNewline"));
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Transcript")?.GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TaskBoard")?.GetComponent<ScrollRect>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Timeout_RetainsDraftAndOffersExplicitRetryEditCancel()
        {
            chat.enabled = true;
            chat.OpenCommander();
            chat.InputText = "gather 200 gold with 3 villagers";
            Task<CommanderAIChatSubmission> request = chat.SubmitCurrentInputAsync();
            while (!request.IsCompleted) yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(1));
            Assert.That(chat.InputText, Is.EqualTo("gather 200 gold with 3 villagers"));
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Retry")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Edit")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")), Does.StartWith("Failed"));
            Canvas.ForceUpdateCanvases();
            var recoveryCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            chat.transform.Find("CommanderCanvas/Panel/RequestRecovery").GetComponent<RectTransform>().GetWorldCorners(recoveryCorners);
            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
            Assert.That(recoveryCorners[0].y, Is.GreaterThanOrEqualTo(composerCorners[2].y));
            yield return new WaitForEndOfFrame();
            SaveCurrentFrame("timeout-recovery");
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DockInputOwnership_TracksActualExpandedAndCompactRegions()
        {
            chat.enabled = true;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var compact = chat.transform.Find("CommanderCanvas/CompactCommander").GetComponent<RectTransform>();
            Assert.That(chat.ContainsScreenPoint(RectTransformUtility.WorldToScreenPoint(null, compact.TransformPoint(compact.rect.center))), Is.True);
            Assert.That(chat.ContainsScreenPoint(new Vector2(10,10)), Is.False);
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var panel = chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>();
            var panelCenter = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(panel.rect.center));
            Assert.That(chat.ContainsScreenPoint(panelCenter), Is.True);
            Assert.That(chat.ContainsScreenPoint(new Vector2(10,10)), Is.False);
            Assert.That(CommanderChatUI.TryHandleEscapeInput(), Is.True);
            Assert.That(chat.IsExpanded, Is.False);
            Assert.That(chat.ContainsScreenPoint(panelCenter), Is.False);
        }

        [Test]
        public void CapabilityCards_NameRealActionAndDoNotInventAllMatchingCount()
        {
            var selector = new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers, 1, allMatching: true);
            var moveSelector = new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers, 3);
            var location = new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase);
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.MoveUnits, moveSelector, location));
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.GarrisonUnits, selector, location,
                structureType: BuildingType.TownCenter));
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.UngarrisonBuilding,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers), location,
                structureType: BuildingType.TownCenter));
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.DiscoverEnemy,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Scout),
                new CommanderLocationSelector(CommanderLocationSelectorKind.VisibleEnemy),
                targetSelector: new CommanderTargetSelector(BuildingType.TownCenter)));
            var cards = CommanderTaskBoardProjection.Capture(goals, null, 1).Cards;
            Assert.That(cards, Has.Count.EqualTo(4));
            Assert.That(cards[0].CurrentStep, Does.Contain("Discover"));
            Assert.That(cards[1].CurrentStep, Does.Contain("Ungarrison"));
            Assert.That(cards[2].CurrentStep, Does.Contain("Garrison"));
            Assert.That(cards[3].CurrentStep, Does.Contain("Move"));
            Assert.That(cards[1].Progress, Does.Contain("Awaiting exact shelter occupant snapshot"));
            Assert.That(cards[2].Progress, Does.Contain("Awaiting exact eligible shelter snapshot"));
            Assert.That(cards[3].Progress, Does.Contain("Awaiting exact bound subjects"));
        }

        [UnityTest]
        public IEnumerator TasksView_KeepsActiveCancelVisibleAndHistoryCollapsed()
        {
            chat.enabled = true;
            var completed = goals.SubmitBuildStructure(BuildingType.House);
            completed.SetStatus(CommanderGoalStatus.Completed, "One requested House completed.");
            var goal = goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            yield return null;
            chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var board = chat.transform.Find("CommanderCanvas/Panel/TaskBoard");
            Assert.That(board.gameObject.activeInHierarchy, Is.True);
            Assert.That(board.Find("Viewport/Content/TaskCard/CancelTask")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(board.Find("Viewport/Content/TaskCard/Details")?.gameObject.activeInHierarchy, Is.False);
            Assert.That(board.Find("Viewport/Content/HistoryToggle"), Is.Not.Null);
            yield return null;
            board.Find("Viewport/Content/TaskCard/CancelTask").GetComponent<Button>().onClick.Invoke();
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
        }

        [UnityTest]
        public IEnumerator TasksView_ActiveCancelIsInsideViewportWithoutScrolling()
        {
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            yield return null;
            chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var board = chat.transform.Find("CommanderCanvas/Panel/TaskBoard");
            var viewport = board.Find("Viewport").GetComponent<RectTransform>();
            var cancel = board.Find("Viewport/Content/TaskCard/CancelTask").GetComponent<RectTransform>();
            var viewCorners = new Vector3[4];
            var cancelCorners = new Vector3[4];
            viewport.GetWorldCorners(viewCorners);
            cancel.GetWorldCorners(cancelCorners);
            Assert.That(cancelCorners[0].y, Is.GreaterThanOrEqualTo(viewCorners[0].y + 2),
                "The active task's Cancel control must be visible without scrolling.");
            Assert.That(cancelCorners[2].y, Is.LessThanOrEqualTo(viewCorners[2].y - 2));
        }

        [UnityTest]
        public IEnumerator ExpandedDock_HidesItsLauncherAndHasOneActiveCommanderCanvas()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy, Is.False);
            int activeCanvases = 0;
            foreach (var ui in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                if (ui.gameObject.activeInHierarchy && ui.transform.Find("CommanderCanvas")?.gameObject.activeInHierarchy == true)
                    activeCanvases++;
            Assert.That(activeCanvases, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator IdleDock_ShowsReadableSettingsWithoutLegacyStatusAndTallVoiceStack()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var panel = chat.transform.Find("CommanderCanvas/Panel");
            Assert.That(TextOf(panel.Find("Settings/Label")), Is.EqualTo("Settings"));
            Assert.That(panel.Find("CommanderStatus").gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.Find("Current plan status").gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.Find("VoiceBar").GetComponent<LayoutElement>().preferredHeight,
                Is.LessThanOrEqualTo(42));
        }

        [UnityTest]
        public IEnumerator Composer_LeavesTwoLineDraftAreaBesideCompactSend()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var input = chat.transform.Find("CommanderCanvas/Panel/TextSubmission/Input").GetComponent<RectTransform>();
            var send = chat.transform.Find("CommanderCanvas/Panel/TextSubmission/Send").GetComponent<RectTransform>();
            Assert.That(input.rect.height, Is.GreaterThanOrEqualTo(60));
            Assert.That(input.rect.width, Is.GreaterThanOrEqualTo(send.rect.width * 2));
        }

        [UnityTest]
        public IEnumerator CompoundApproval_ShowsFullValidatedPreviewAndExplicitDecisionsWithoutIssuing()
        {
            InstallRenderFixtureCamera();
            // The approval fixture performs real graph admission on the main thread;
            // its timeout is test-local, not a production timeout change.
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"producerFromNode\":0,\"dependsOn\":[0]}]}");
            var turn = chat.SubmitMessageAsync("build a Barracks, then train three Spearmen there");
            provider.CompleteLatest(parsed);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.PendingActionPlan, Is.Not.Null,
                $"attempts={provider.Attempts}; transcript={chat.DisplayedTranscript}");
            var approval = chat.transform.Find("CommanderCanvas/Panel/Approval");
            Assert.That(approval.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Transcript").gameObject.activeInHierarchy, Is.False,
                "Approval preview replaces Chat scroll while pending.");
            var scroll = approval.Find("PreviewScroll").GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(TextOf(scroll.content), Is.EqualTo(chat.PendingActionPlan.Preview));
            Assert.That(TextOf(scroll.content), Does.Contain("after step 1"));
            Assert.That(approval.Find("ApprovePlan")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(approval.Find("RejectPlan")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(goals.Goals, Is.Empty, "Displaying or scrolling details cannot authorize gameplay.");
            Canvas.ForceUpdateCanvases();
            var panelCorners = new Vector3[4];
            var approveCorners = new Vector3[4];
            var rejectCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TextSubmission").gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/VoiceBar").gameObject.activeInHierarchy, Is.True);
            chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>().GetWorldCorners(panelCorners);
            approval.Find("ApprovePlan").GetComponent<RectTransform>().GetWorldCorners(approveCorners);
            approval.Find("RejectPlan").GetComponent<RectTransform>().GetWorldCorners(rejectCorners);
            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
            Assert.That(approveCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y));
            Assert.That(rejectCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y));
            Assert.That(composerCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y),
                "Approval must not push the fixed composer below the dock.");
            Assert.That(composerCorners[2].y, Is.LessThanOrEqualTo(approveCorners[0].y - 4),
                "Composer must remain below the approval decision controls.");
            yield return null;
            ForceFixtureRepaint();
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var approvalImage = ScreenCapture.CaptureScreenshotAsTexture();
            var directory = NewRenderDirectory("compound-approval");
            File.WriteAllBytes(Path.Combine(directory,
                $"editor-approval-actual-{approvalImage.width}x{approvalImage.height}.png"),
                approvalImage.EncodeToPNG());
            UnityEngine.Object.Destroy(approvalImage);
            yield return null;
            approval.Find("RejectPlan").GetComponent<Button>().onClick.Invoke();
            Assert.That(chat.PendingActionPlan, Is.Null);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Cancelled"));
        }

        [UnityTest]
        public IEnumerator ReviewedVoiceDraft_IsVisibleInExpandedDockAndDiscardDoesNotSubmit()
        {
            chat.SetVoiceProvider(new MockCommanderSpeechToTextProvider("train three spearmen"),
                new FixtureAudioCapture());
            chat.VoiceController.AutoSubmit = false;
            chat.enabled = true;
            chat.OpenCommander();
            chat.VoiceController.StartRecording();
            var voice = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!voice.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.VoiceState, Is.EqualTo(CommanderVoiceState.Preview));
            var review = chat.transform.Find("CommanderCanvas/Panel/VoiceReview");
            Assert.That(review?.gameObject.activeInHierarchy, Is.True);
            Assert.That(TextOf(review.Find("ReviewText")), Does.Contain("not sent"));
            Assert.That(review.Find("SendReviewed")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(review.Find("EditReviewed")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(review.Find("DiscardReviewed")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(goals.Goals, Is.Empty);
            review.Find("DiscardReviewed").GetComponent<Button>().onClick.Invoke();
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ApprovePlan_UsesExistingSingleUseAuthorityAndUpdatesLatestStatus()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}");
            var turn = chat.SubmitMessageAsync("build a House and train three Spearmen");
            provider.CompleteLatest(parsed);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.PendingActionPlan, Is.Not.Null);
            Assert.That(goals.Goals, Is.Empty);
            var approve = chat.transform.Find("CommanderCanvas/Panel/Approval/ApprovePlan").GetComponent<Button>();
            approve.onClick.Invoke();
            approve.onClick.Invoke();
            Assert.That(chat.PendingActionPlan, Is.Null);
            int admittedEffects = goals.Goals.Count;
            Assert.That(admittedEffects, Is.EqualTo(2), "The approved graph has two effects.");
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.Not.StartWith("Awaiting approval"));
            yield return null;
            approve.onClick.Invoke();
            Assert.That(goals.Goals, Has.Count.EqualTo(admittedEffects));
            Assert.That(provider.Attempts, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingWorkerCount_ShowsProminentClarificationWithoutGameplay()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Clarify\",\"message\":\"How many villagers?\","
                + "\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\","
                + "\"workers\":{\"state\":\"Idle\"},\"destination\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"}},"
                + "\"missingFields\":[\"Count\"]}");
            var turn = chat.SubmitMessageAsync("send idle villagers to gather wood");
            provider.CompleteLatest(parsed);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var question = chat.transform.Find("CommanderCanvas/Panel/PendingClarification");
            Assert.That(chat.PendingClarification, Is.Not.Null);
            Assert.That(question.gameObject.activeInHierarchy, Is.True);
            Assert.That(TextOf(question.Find("Question")), Does.Contain("How many villagers"));
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Needs clarification"));
            Assert.That(goals.Goals, Is.Empty);
            yield return new WaitForEndOfFrame();
            SaveCurrentFrame("pending-clarification");
        }

        [UnityTest]
        public IEnumerator ChatView_RendersPlayerAndCommanderAsSeparateBubbles()
        {
            chat.enabled = true;
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var bubbles = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport/BubbleContent");
            Assert.That(bubbles, Is.Not.Null);
            Assert.That(bubbles.childCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(bubbles.GetChild(bubbles.childCount - 2).name, Is.EqualTo("PlayerBubble"));
            Assert.That(bubbles.GetChild(bubbles.childCount - 1).name, Is.EqualTo("CommanderBubble"));
        }

        [UnityTest]
        public IEnumerator LatestQuestionStatus_DoesNotBorrowOlderWorkingGoal()
        {
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var status = chat.transform.Find("CommanderCanvas/Panel/LatestRequest");
            Assert.That(TextOf(status), Does.StartWith("Completed"));
            Assert.That(TextOf(status), Does.Not.Contain("Reach Age"));
        }

        [UnityTest]
        public IEnumerator PopulatedChatView_StaysInsideScreenAndAboveFixedComposer()
        {
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var panel = chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>();
            var viewport = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport").GetComponent<RectTransform>();
            var composer = chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>();
            var panelCorners = new Vector3[4];
            var viewportCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            panel.GetWorldCorners(panelCorners);
            viewport.GetWorldCorners(viewportCorners);
            composer.GetWorldCorners(composerCorners);
            Assert.That(panelCorners[0].x, Is.GreaterThanOrEqualTo(0));
            Assert.That(panelCorners[2].x, Is.LessThanOrEqualTo(Screen.width + 1));
            Assert.That(panelCorners[0].y, Is.GreaterThanOrEqualTo(0));
            Assert.That(viewportCorners[0].y, Is.GreaterThanOrEqualTo(composerCorners[1].y + 4),
                "Chat scroll must end above the fixed composer.");
        }

        [UnityTest]
        public IEnumerator CaptureActualPopulatedEditorViews_AtRequestedDesktopSizes()
        {
            InstallRenderFixtureCamera();
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            var directory = NewRenderDirectory("populated-chat-tasks");
            Directory.CreateDirectory(directory);
            int activeCommanderCanvases = 0;
            foreach (var ui in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                if (ui.gameObject.activeInHierarchy && ui.transform.Find("CommanderCanvas")?.gameObject.activeInHierarchy == true)
                    activeCommanderCanvases++;
            Debug.Log($"[Commander Phase5C render] active Commander canvases={activeCommanderCanvases}; own compact active={chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy}");
            Assert.That(activeCommanderCanvases, Is.EqualTo(1));
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy, Is.False);
            foreach (int width in new[] { 1280, 1920 })
            {
                int height = width == 1280 ? 720 : 1080;
                Screen.SetResolution(width, height, false);
                yield return null;
                yield return null;
                ForceFixtureRepaint();
                Canvas.ForceUpdateCanvases();
                var launcher = GameObject.Find("CompactCommander");
                Debug.Log($"[Commander Phase5C render] active launcher object={(launcher == null ? "none" : launcher.transform.parent.name + "/" + launcher.name)}; own launcher={chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy}");
                yield return new WaitForEndOfFrame();
                var chatImage = ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(chatImage, Is.Not.Null);
                File.WriteAllBytes(Path.Combine(directory,
                    $"editor-chat-requested-{width}x{height}-actual-{chatImage.width}x{chatImage.height}.png"),
                    chatImage.EncodeToPNG());
                Debug.Log($"[Commander Phase5C render] requested={width}x{height}; captured={chatImage.width}x{chatImage.height}; Chat active goal and local reply");
                UnityEngine.Object.Destroy(chatImage);
                chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
                yield return null;
                ForceFixtureRepaint();
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                var tasksImage = ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(tasksImage, Is.Not.Null);
                File.WriteAllBytes(Path.Combine(directory,
                    $"editor-tasks-requested-{width}x{height}-actual-{tasksImage.width}x{tasksImage.height}.png"),
                    tasksImage.EncodeToPNG());
                Debug.Log($"[Commander Phase5C render] requested={width}x{height}; captured={tasksImage.width}x{tasksImage.height}; Tasks active goal and visible Cancel");
                UnityEngine.Object.Destroy(tasksImage);
                chat.transform.Find("CommanderCanvas/Panel/Views/ChatTab").GetComponent<Button>().onClick.Invoke();
                yield return null;
            }
        }

        private void InstallRenderFixtureCamera()
        {
            renderCameraObject = new GameObject("Phase5CRenderFixtureCamera");
            var camera = renderCameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depth = 100;
        }

        private void ForceFixtureRepaint()
        {
            foreach (var graphic in chat.GetComponentsInChildren<Graphic>(true))
                graphic.SetAllDirty();
        }

        private static string NewRenderDirectory(string state)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Docs/CommanderPhase5C/evidence/task-5-render",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + state));
            Directory.CreateDirectory(directory);
            Debug.Log("[Commander Phase5C render] evidence directory=" + directory);
            return directory;
        }

        private static void SaveCurrentFrame(string state)
        {
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            var directory = NewRenderDirectory(state);
            File.WriteAllBytes(Path.Combine(directory,
                $"editor-{state}-actual-{screenshot.width}x{screenshot.height}.png"),
                screenshot.EncodeToPNG());
            UnityEngine.Object.Destroy(screenshot);
        }

        [UnityTest]
        public IEnumerator TimedOutLateResponse_CannotAdmitAndDoubleRetryStartsOnce()
        {
            chat.enabled = true;
            chat.OpenCommander();
            chat.InputText = "train 10 spearmen";
            var first = chat.SubmitCurrentInputAsync();
            while (!first.IsCompleted) yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(1));
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"), "after initial timeout");
            provider.CompleteLatest("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}");
            yield return null;
            Assert.That(goals.Goals, Is.Empty);
            var retry = chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Retry").GetComponent<Button>();
            retry.onClick.Invoke();
            retry.onClick.Invoke();
            yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(2));
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"), "after retry begins");
            var cancel = chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel").GetComponent<Button>();
            cancel.onClick.Invoke();
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"), "immediately after Cancel");
            provider.CompleteLatest("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}");
            yield return null;
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"));
        }

        private static string TextOf(Transform target)
        {
            if (target == null) return string.Empty;
            foreach (var component in target.GetComponents<Component>())
                if (component.GetType().Name == "TextMeshProUGUI")
                    return (string)component.GetType().GetProperty("text").GetValue(component);
            return string.Empty;
        }

        private sealed class SlowProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            public int Attempts { get; private set; }
            private TaskCompletionSource<CommanderSemanticResult> latest;
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
                => Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure, "offline fixture"));
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request, CancellationToken token)
            {
                Attempts++;
                latest = new TaskCompletionSource<CommanderSemanticResult>();
                return latest.Task;
            }
            public void CompleteLatest(string json) => latest?.TrySetResult(CommanderSemanticJson.Parse(json));
            public void CompleteLatest(CommanderSemanticResult result) => latest?.TrySetResult(result);
        }

        private sealed class FixtureAudioCapture : ICommanderAudioCapture
        {
            public bool IsRecording { get; private set; }
            public string CurrentDevice => "FixtureMic";
            public bool StartRecording(string deviceName = null, float maxDurationSeconds = 15f)
            { IsRecording = true; return true; }
            public CommanderAudioData StopRecording()
            { IsRecording = false; return new CommanderAudioData(new float[16000], 16000, 1); }
            public void CancelRecording() => IsRecording = false;
            public void Dispose() => IsRecording = false;
        }
    }
}
