using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    // Synthetic Input System devices, real UI and gameplay callbacks. Not a
    // physical microphone/browser or packaged-build acceptance substitute.
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixInputDispatchPlayModeTests
    {
        private CommanderChatUI chat;
        private CommanderGoalManager goals;
        private CommanderIntentDispatcher dispatcher;
        private SimulationConfig config;
        private GameObject selectionObject;
        private UnitSelectionManager selection,previousSelection;
        private Keyboard keyboard,previousKeyboard;
        private Mouse mouse,previousMouse;
        private Vector2 previousVirtualPoint;
        private bool previousVoiceEnabled;
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly FieldInfo SelectionSingleton=typeof(UnitSelectionManager).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            previousKeyboard=Keyboard.current;previousMouse=Mouse.current;
            previousVirtualPoint=VirtualCursor.Position;previousVoiceEnabled=CommanderVoiceSettings.VoiceEnabled;
            CommanderVoiceSettings.VoiceEnabled=false;
            foreach(var existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())UnityEngine.Object.DestroyImmediate(existing.gameObject);
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            previousSelection=UnitSelectionManager.Instance;
            selectionObject=new GameObject("GrandFixInputSelection");selection=selectionObject.AddComponent<UnitSelectionManager>();
            config=ScriptableObject.CreateInstance<SimulationConfig>();
            var sim=new GameSimulation(config,2,new[]{0,1},Array.Empty<int>());
            goals=new CommanderGoalManager(sim,0);dispatcher=new CommanderIntentDispatcher(sim,goals);
            chat=new GameObject("GrandFixInputChat").AddComponent<CommanderChatUI>();chat.enabled=false;
            chat.Initialize(new Provider(),sim,goals,dispatcher);
            // No bootstrap coroutine/voice permission, inference or provider network.
            UnitSelectionManager.SetChatFocused(false);SettingsMenuUI.Close();
        }

        [TearDown]
        public void TearDown()
        {
            SettingsMenuUI.Close();UnitSelectionManager.SetChatFocused(false);
            if(selectionObject!=null)UnityEngine.Object.DestroyImmediate(selectionObject);
            SelectionSingleton.SetValue(null,previousSelection);
            if(chat!=null)UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();goals?.Dispose();if(config!=null)UnityEngine.Object.DestroyImmediate(config);
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            previousKeyboard?.MakeCurrent();previousMouse?.MakeCurrent();
            typeof(VirtualCursor).GetProperty("Position").SetValue(null,previousVirtualPoint);
            CommanderVoiceSettings.VoiceEnabled=previousVoiceEnabled;
        }

        [UnityTest]
        public IEnumerator EscapeEventBeforeUiUpdate_MinimizesWithoutOpeningSettings_ThenNormalEscapeStillWorks()
        {
            chat.enabled=true;chat.OpenCommander();chat.InputText="unsent text";yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));InputSystem.Update();
            Assert.That(chat.IsExpanded,Is.False);
            Assert.That(UnitSelectionManager.IsSettingsMenuOpen,Is.False);
            yield return null;
            Assert.That(UnitSelectionManager.IsSettingsMenuOpen,Is.False,"Later UI polling must not forward the already consumed Escape.");
            Assert.That(chat.InputText,Is.EqualTo("unsent text"));Assert.That(goals.Goals,Is.Empty);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));InputSystem.Update();
            Assert.That(UnitSelectionManager.IsSettingsMenuOpen,Is.True,"Commander idle must not permanently consume gameplay Escape.");
        }

        [UnityTest]
        public IEnumerator PointerPressBeforeVirtualCursorUpdate_CannotStartWorldSelectionThroughCommander()
        {
            chat.enabled=true;yield return null;Canvas.ForceUpdateCanvases();
            var rect=chat.transform.Find("CommanderCanvas/CompactCommander").GetComponent<RectTransform>();
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            Vector2 center=RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);
            Vector2 stale=RectTransformUtility.WorldToScreenPoint(null,corners[0]-new Vector3(100,100,0));
            typeof(VirtualCursor).GetProperty("Position").SetValue(null,stale);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=center,buttons=1});InputSystem.Update();
            Assert.That((bool)typeof(UnitSelectionManager).GetField("selectHeld",Private).GetValue(selection),Is.False,
                "The input callback must use current pointer state, not the prior MonoBehaviour.Update position.");
            Assert.That(goals.Goals,Is.Empty);
        }

        [UnityTest]
        public IEnumerator ExpandedPanel_LeavesExistingPlayerHudVisibleAndStaysOnScreen()
        {
            var hudObject=new GameObject("AlignmentPlayerHud");
            try
            {
                var hud=hudObject.AddComponent<PlayerListUI>();hud.enabled=false;
                typeof(PlayerListUI).GetMethod("BuildRows",Private).Invoke(hud,new object[]{2,0});
                chat.enabled=true;chat.OpenCommander();yield return null;Canvas.ForceUpdateCanvases();
                var panel=(RectTransform)typeof(CommanderChatUI).GetField("commanderPanel",Private).GetValue(chat);
                var playerPanel=hudObject.transform.Find("PlayerListCanvas/PlayerListPanel").GetComponent<RectTransform>();
                var panelCorners=new Vector3[4];var hudCorners=new Vector3[4];
                panel.GetWorldCorners(panelCorners);playerPanel.GetWorldCorners(hudCorners);
                Assert.That(panelCorners[0].x,Is.GreaterThan(hudCorners[2].x+4),
                    "Expanded Commander must not obscure the actual player-list HUD.");
                Assert.That(panelCorners[2].x,Is.LessThanOrEqualTo(Screen.width-8));
                Assert.That(panelCorners[0].y,Is.GreaterThanOrEqualTo(8));
                Assert.That(goals.Goals,Is.Empty);
            }
            finally {UnityEngine.Object.DestroyImmediate(hudObject);}
        }

        [UnityTest]
        public IEnumerator CompactLauncher_PrecedesSettingsWithoutOverlappingControls()
        {
            chat.enabled=true;yield return null;Canvas.ForceUpdateCanvases();
            var compact=chat.transform.Find("CommanderCanvas/CompactCommander");
            var launcher=compact.Find("Launcher").GetComponent<RectTransform>();
            var settings=compact.Find("TextAIOptions").GetComponent<RectTransform>();
            var launcherCorners=new Vector3[4];var settingsCorners=new Vector3[4];
            launcher.GetWorldCorners(launcherCorners);settings.GetWorldCorners(settingsCorners);
            Assert.That(launcherCorners[0].y,Is.GreaterThan(settingsCorners[1].y),
                "Primary Commander/Record/Voice controls belong above Text AI settings, with a clear gap.");
            Assert.That(goals.Goals,Is.Empty);Assert.That(chat.IsExpanded,Is.False);
        }

        [UnityTest]
        public IEnumerator SetupCards_StayOnScreenAndExposeScrollableControlsWithoutGameplay()
        {
            chat.enabled=true;chat.ShowVoiceSetup();yield return null;Canvas.ForceUpdateCanvases();
            var voice=(GameObject)typeof(CommanderChatUI).GetField("voiceSetupCard",Private).GetValue(chat);
            var corners=new Vector3[4];voice.GetComponent<RectTransform>().GetWorldCorners(corners);
            Assert.That(corners[0].y,Is.GreaterThanOrEqualTo(8),"Voice/model setup must not hide its bottom below the screen.");
            Assert.That(voice.transform.Find("SetupViewport/SetupContent/LocalModels/ModelActions"),Is.Not.Null,
                "Actual model actions must belong to the scrollable content, not be omitted to make the panel fit.");
            chat.ShowSemanticSetup();yield return null;Canvas.ForceUpdateCanvases();
            var semantic=(GameObject)typeof(CommanderChatUI).GetField("semanticSetupCard",Private).GetValue(chat);
            semantic.GetComponent<RectTransform>().GetWorldCorners(corners);
            Assert.That(corners[0].y,Is.GreaterThanOrEqualTo(8));
            Assert.That(semantic.transform.Find("SetupViewport/SetupContent/SemanticSession"),Is.Not.Null);
            Assert.That(goals.Goals,Is.Empty);Assert.That(chat.IsExpanded,Is.False);
        }

        private sealed class Provider:ICommanderAIProvider,ICommanderSemanticProvider
        {
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,CancellationToken token)
                =>Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,"offline fixture"));
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,CancellationToken token)
                =>Task.FromResult(CommanderSemanticJson.Parse("{\"outcome\":\"Answer\",\"message\":\"offline fixture\"}"));
        }
    }
}
