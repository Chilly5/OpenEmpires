using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixPresentationTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private CommanderChatUI previous;
        private SemanticProvider provider;
        private bool enabledBefore, autoBefore;
        private static readonly FieldInfo Instance = typeof(CommanderChatUI).GetField("instance", BindingFlags.NonPublic|BindingFlags.Static);

        [SetUp]
        public void Setup()
        {
            enabledBefore=CommanderVoiceSettings.VoiceEnabled; autoBefore=CommanderVoiceSettings.AutoSubmit;
            CommanderVoiceSettings.VoiceEnabled=true; CommanderVoiceSettings.AutoSubmit=false;
            previous=Instance.GetValue(null) as CommanderChatUI; Instance.SetValue(null,null);
            config=ScriptableObject.CreateInstance<SimulationConfig>();
            sim=new GameSimulation(config,2,new[]{0,1},Array.Empty<int>());
            manager=new CommanderGoalManager(sim,0); dispatcher=new CommanderIntentDispatcher(sim,manager);
            provider=new SemanticProvider();
            chat=new GameObject("GrandFixPresentation").AddComponent<CommanderChatUI>();
            chat.Initialize(provider,sim,manager,dispatcher);
            // EditMode AddComponent does not invoke Awake; model the real local
            // host registration for callback routing without creating another UI.
            Instance.SetValue(null,chat);
            chat.SetVoiceProvider(new Speech(),new Capture());
        }
        [TearDown]
        public void TearDown()
        {
            if(chat!=null)UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose(); manager?.Dispose(); if(config!=null)UnityEngine.Object.DestroyImmediate(config);
            Instance.SetValue(null,previous);
            CommanderVoiceSettings.VoiceEnabled=enabledBefore; CommanderVoiceSettings.AutoSubmit=autoBefore;
        }
        private RectTransform Panel=> (RectTransform)typeof(CommanderChatUI).GetField("commanderPanel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat);
        private void Surface(string method)=>typeof(CommanderChatUI).GetMethod(method,BindingFlags.Instance|BindingFlags.Public)?.Invoke(chat,null);
        private GameObject Compact=>chat.transform.Find("CommanderCanvas/CompactCommander")?.gameObject;

        [Test]
        public void Initialize_StartsCollapsedWithLiveHostAndSmallLauncher()
        {
            Assert.That(Panel.gameObject.activeSelf,Is.False);
            Assert.That(chat.gameObject.activeSelf,Is.True);
            Assert.That(chat.VoiceController,Is.Not.Null);
            Assert.That(Compact,Is.Not.Null); Assert.That(Compact.activeSelf,Is.True);
            Assert.That(chat.InputText,Is.Empty);
        }
        [Test]
        public void DeliberateOpenThenMinimize_DoesNotCancelAcceptedWork()
        {
            var goal=manager.SubmitEnsureUnitCount(1,4);
            Surface("OpenCommander"); Assert.That(Panel.gameObject.activeSelf,Is.True);
            Surface("MinimizeCommander"); Assert.That(Panel.gameObject.activeSelf,Is.False);
            Assert.That(goal.IsTerminal,Is.False); Assert.That(manager.ActiveGoals.Count,Is.EqualTo(1));
            Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }
        [Test]
        public async Task OrdinaryAnswerAndReset_DoNotReopenConversation()
        {
            await chat.SubmitMessageAsync("How do I reach castle age?");
            Assert.That(Panel.gameObject.activeSelf,Is.False);
            Surface("OpenCommander"); chat.ResetConversation();
            Assert.That(Panel.gameObject.activeSelf,Is.False); Assert.That(manager.Goals,Is.Empty);
        }
        [Test]
        public async Task CollapsedRecordingAndTranscriptReview_DoNotSubmitOrApprove()
        {
            Assert.That(chat.VoiceController.StartRecording(),Is.True);
            Assert.That(Panel.gameObject.activeSelf,Is.False);
            await chat.VoiceController.StopRecordingAndTranscribeAsync();
            Assert.That(chat.VoiceState,Is.EqualTo(CommanderVoiceState.Preview));
            Assert.That(Panel.gameObject.activeSelf,Is.False);
            Assert.That(chat.InputText,Is.EqualTo("make four spearmen"));
            Assert.That(provider.Calls,Is.Zero); Assert.That(manager.Goals,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public async Task DiscardVoiceDraft_ClearsOnlyUnchangedOwnedText(bool edited)
        {
            chat.VoiceController.StartRecording(); await chat.VoiceController.StopRecordingAndTranscribeAsync();
            if(edited)chat.InputText="my independent edited text";
            chat.VoiceController.Cancel();
            Assert.That(chat.InputText,Is.EqualTo(edited?"my independent edited text":""));
            Assert.That(provider.Calls,Is.Zero); Assert.That(manager.Goals,Is.Empty);
        }
        [Test]
        public async Task OversizedTypedDraft_IsNotTruncatedOrSent()
        {
            string text=new string('x',1025); chat.InputText=text;
            Assert.That(chat.InputText,Is.EqualTo(text),"Do not silently truncate pasted/recognized text.");
            Assert.That(await chat.SubmitCurrentInputAsync(),Is.Null);
            Assert.That(provider.Calls,Is.Zero); Assert.That(chat.InputText,Is.EqualTo(text));
            Assert.That(manager.Goals,Is.Empty);
        }
        [TestCase(false)][TestCase(true)]public async Task ProviderModeSwitch_DiscardsOnlyUnchangedVoiceDraft(bool edited)
        {
            chat.VoiceController.StartRecording();await chat.VoiceController.StopRecordingAndTranscribeAsync();
            if(edited)chat.InputText="my independent edit";
            chat.SetVoiceProvider(new Speech(),new Capture());
            Assert.That(chat.InputText,Is.EqualTo(edited?"my independent edit":""));Assert.That(provider.Calls,Is.Zero);
        }
        [Test]public void CompactVoiceSetup_IsHiddenInitiallyAndOpensWithoutConversationOrGoals()
        {
            var setup=chat.transform.Find("CommanderCanvas/CompactCommander/VoiceSetup");
            Assert.That(setup,Is.Not.Null);Assert.That(setup.gameObject.activeSelf,Is.False);
            Surface("ShowVoiceSetup");Assert.That(setup.gameObject.activeSelf,Is.True);Assert.That(chat.IsExpanded,Is.False);
            Assert.That(provider.Calls,Is.Zero);Assert.That(manager.Goals,Is.Empty);
        }
        [Test]public async Task OnlinePolicyReviewAndExplicitConsent_DoNotRecordSubmitOrApprove()
        {
            var method=typeof(CommanderChatUI).GetMethod("PrepareOnlineVoiceAsync");Assert.That(method,Is.Not.Null);
            var gateway=new GatewayPolicyTransport();
            bool prepared=await (Task<bool>)method.Invoke(chat,new object[]{"https://gateway.example.invalid",new Func<string>(()=>CommanderGrandFixOnlineVoiceTests.FixtureBackendToken),gateway});
            Assert.That(prepared,Is.True);Assert.That(gateway.PolicyCalls,Is.EqualTo(1));Assert.That(gateway.SpeechCalls,Is.Zero);
            var accept=typeof(CommanderChatUI).GetMethod("AcceptOnlineVoiceConsent");Assert.That(accept,Is.Not.Null);
            Assert.That((bool)accept.Invoke(chat,new object[]{new Capture()}),Is.True);
            Assert.That(chat.VoiceState,Is.EqualTo(CommanderVoiceState.Idle));Assert.That(chat.IsExpanded,Is.False);
            Assert.That(provider.Calls,Is.Zero);Assert.That(gateway.SpeechCalls,Is.Zero);Assert.That(manager.Goals,Is.Empty);
        }
        private sealed class GatewayPolicyTransport:ICommanderGatewayTransport
        {
            public int PolicyCalls,SpeechCalls;
            public Task<CommanderGatewayResponse> SendAsync(string gateway,Guid job,string token,string policy,string language,byte[] body,CommanderGatewayRequestKind kind,CancellationToken cancellation)
            {if(kind==CommanderGatewayRequestKind.Policy){PolicyCalls++;return Task.FromResult(new CommanderGatewayResponse(200,CommanderGrandFixOnlineVoiceTests.FixturePolicy));}
                SpeechCalls++;return Task.FromResult(new CommanderGatewayResponse(500,"{}"));}
            public void Dispose(){}
        }
        [Test]public async Task MicrophoneActivation_IsASeparateActionAndNeverRecordsOrSubmits()
        {
            var capture=new PermissionCapture();chat.SetVoiceProvider(new Speech(),capture);
            var method=typeof(CommanderChatUI).GetMethod("ActivateMicrophoneAsync");Assert.That(method,Is.Not.Null);
            bool ready=await (Task<bool>)method.Invoke(chat,null);
            Assert.That(ready,Is.True);Assert.That(capture.Requests,Is.EqualTo(1));Assert.That(capture.Starts,Is.Zero);
            Assert.That(provider.Calls,Is.Zero);Assert.That(manager.Goals,Is.Empty);Assert.That(chat.VoiceState,Is.EqualTo(CommanderVoiceState.Idle));
        }
        [Test]public async Task CancelledActivation_LatePermissionGrantCannotStartRecording()
        {
            var capture=new PermissionCapture{Deferred=true};chat.SetVoiceProvider(new Speech(),capture);
            var method=typeof(CommanderChatUI).GetMethod("ActivateMicrophoneAsync");Assert.That(method,Is.Not.Null);
            var task=(Task<bool>)method.Invoke(chat,null);Surface("HideVoiceSetup");capture.Complete();bool ready=await task;
            Assert.That(ready,Is.False);Assert.That(capture.Starts,Is.Zero);Assert.That(provider.Calls,Is.Zero);
        }
        private sealed class PermissionCapture:ICommanderAudioCapture,ICommanderCaptureReadiness
        {
            private readonly TaskCompletionSource<bool> pending=new TaskCompletionSource<bool>();public bool Deferred,Ended;public int Requests,Starts;public string Error="";
            public bool PermissionReady{get;private set;}public bool IsListening=>IsRecording;public bool CaptureEnded=>Ended;public string CaptureError=>Error;
            public bool IsRecording{get;private set;}public string CurrentDevice=>"synthetic";
            public bool StartRecording(string deviceName=null,float maxDurationSeconds=15){Starts++;return IsRecording=true;}
            public CommanderAudioData StopRecording(){IsRecording=false;return CommanderAudioData.Empty;}public void CancelRecording()=>IsRecording=false;public void Dispose()=>CancelRecording();
            public void SetHeldKey(string code){}public Task<bool> RequestPermissionAsync(CancellationToken token){Requests++;if(Deferred)return pending.Task;PermissionReady=true;return Task.FromResult(true);}
            public void Complete(){PermissionReady=true;pending.TrySetResult(true);}
        }
        [TestCase("MIC_FOCUS_LOST")][TestCase("MIC_OPEN_FAILED")]
        public void BrowserSafetyFailure_ShowsReasonWithoutTranscriptionOrOrders(string code)
        {
            var capture=new PermissionCapture();chat.SetVoiceProvider(new Speech(),capture);chat.VoiceController.StartRecording();
            capture.Ended=true;capture.Error=code;typeof(CommanderChatUI).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(chat,null);
            Assert.That(chat.VoiceState,Is.EqualTo(CommanderVoiceState.Error));Assert.That(chat.VoiceController.LastError,Does.Contain("No order"));
            Assert.That(chat.VoiceController.LastError,Does.Not.Contain(code));Assert.That(provider.Calls,Is.Zero);Assert.That(manager.Goals,Is.Empty);
        }
        [Test]
        public async Task TranscriptSend_OnlyStagesCompoundApprovalWhileCollapsed()
        {
            provider.Json="{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1},"
                +"{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"producerFromNode\":0,\"dependsOn\":[0]}]}";
            chat.VoiceController.StartRecording(); await chat.VoiceController.StopRecordingAndTranscribeAsync();
            await chat.SubmitCurrentInputAsync();
            Assert.That(provider.Calls,Is.EqualTo(1)); Assert.That(chat.PendingActionPlan,Is.Not.Null);
            Assert.That(Panel.gameObject.activeSelf,Is.False); Assert.That(manager.Goals,Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }

        [Test]
        public async Task SameFrameSendAndExpandedApproval_CannotConsumeTwoDecisions()
        {
            provider.Json="{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1},"
                +"{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}]}";
            Surface("OpenCommander"); chat.InputText="build a house and make four spearmen";
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            Click(typeof(CommanderChatUI).GetField("sendButton",flags).GetValue(chat));
            await Task.CompletedTask;
            Assert.That(chat.PendingActionPlan,Is.Not.Null);
            Click(typeof(CommanderChatUI).GetField("approveStrategyButton",flags).GetValue(chat));
            Assert.That(manager.Goals,Is.Empty,"One event frame must not Send then approve the newly created candidate.");
            Assert.That(chat.PendingActionPlan,Is.Not.Null);
        }

        [Test] public async Task ReadableApprovalCard_PreservesWholeScrollableTextAndDetailsDoesNotApprove()
        {
            provider.Json=CommanderPhase5ADynamicCompilerTests.SharedWorkers;
            await chat.SubmitMessageAsync("Assign the three workers to the two requested roles.");
            var candidate=chat.PendingActionPlan;
            Assert.That(candidate,Is.Not.Null);
            typeof(CommanderChatUI).GetMethod("UpdatePresentation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,null);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            // Keep this assembly's existing dependency boundary; inspect the real
            // runtime TMP component without adding a package reference for a test.
            var body=(Component)typeof(CommanderChatUI).GetField("compactApprovalText",flags).GetValue(chat);
            Assert.That(body.GetType().GetProperty("text").GetValue(body),Is.EqualTo(candidate.Preview));
            Assert.That(body.GetType().GetProperty("richText").GetValue(body),Is.False);
            var scroll=body.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            Assert.That(scroll,Is.Not.Null);Assert.That(scroll.content,Is.SameAs(body.transform));
            Assert.That(body.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit,Is.EqualTo(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize));
            Click(scroll.transform.parent.Find("Actions/Action0").GetComponent<UnityEngine.UI.Button>());
            Assert.That(Panel.gameObject.activeSelf,Is.True);
            Assert.That(chat.PendingActionPlan,Is.SameAs(candidate));
            Assert.That(manager.Goals,Is.Empty);Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }

        [Test]
        public async Task CompactStrategicAction_RequiresExistingDetailedConfirmationControls()
        {
            using var planner=new StrategicPlanner(manager,_=>5000);
            using var pipeline=new StrategicPipeline(sim,manager,planner);
            chat.InitializeStrategic(new MockStrategicAIProvider(),pipeline);
            provider.Json="{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"EconomicExpansion\"}]}";
            await chat.SubmitMessageAsync("expand economy");
            Assert.That(chat.PendingStrategicIntent,Is.Not.Null);
            typeof(CommanderChatUI).GetMethod("UpdatePresentation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,null);
            var buttonObject=chat.transform.Find("CommanderCanvas/CompactCommander/PlanApproval/Actions/Action1").gameObject;
            var label=Array.Find(buttonObject.GetComponentsInChildren<Component>(true),c=>c!=null&&c.GetType().Name=="TextMeshProUGUI");
            Assert.That((string)label.GetType().GetProperty("text").GetValue(label),Is.EqualTo("Review strategy"));
            var button=Array.Find(buttonObject.GetComponents<Component>(),c=>c!=null&&c.GetType().Name=="Button");
            Click(button);
            Assert.That(chat.IsReady,Is.True); Assert.That(Panel.gameObject.activeSelf,Is.True);
            Assert.That(planner.Plans,Is.Empty); Assert.That(manager.Goals,Is.Empty);
            Assert.That(chat.PendingStrategicIntent,Is.Not.Null);
        }

        private static void Click(object button)
        {
            var onClick=button.GetType().GetProperty("onClick").GetValue(button);
            onClick.GetType().GetMethod("Invoke").Invoke(onClick,null);
        }

        [Test]
        public void EditableFocus_SurvivesOtherChatClearingLegacyFocusFlag()
        {
            const string name="UnityEngine.EventSystems.EventSystem";
            var assembly=Array.Find(AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetType(name)!=null);
            var eventType=assembly.GetType(name);
            var current=eventType.GetProperty("current",BindingFlags.Static|BindingFlags.Public);
            var old=current.GetValue(null);
            var go=new GameObject("FocusFixture");
            var events=go.AddComponent(eventType);
            try
            {
                // EditMode AddComponent does not deliver normal UIBehaviour OnEnable.
                var registered=(System.Collections.IList)eventType.GetField("m_EventSystems",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                if (!registered.Contains(events)) eventType.GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events,null);
                current.SetValue(null,events);
                Assert.That(current.GetValue(null),Is.SameAs(events),"The fixture must register an actual current EventSystem.");
                Surface("OpenCommander");
                var input=typeof(CommanderChatUI).GetField("inputField",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat) as Component;
                eventType.GetMethod("SetSelectedGameObject",new[]{typeof(GameObject)}).Invoke(events,new object[]{input.gameObject});
                UnitSelectionManager.SetChatFocused(false);
                Assert.That(UnitSelectionManager.UIInputSuppressed,Is.True,"A different chat's update must not re-enable gameplay while an editable input is selected.");
            }
            finally
            {
                eventType.GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events,null);
                if (old != null) current.SetValue(null,old);
                UnityEngine.Object.DestroyImmediate(go); UnitSelectionManager.SetChatFocused(false);
            }
        }
        [Test]
        public void EscapeCallbackBeforeCommanderUpdate_MinimizesOnceWithoutOpeningSettings()
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var singleton=typeof(UnitSelectionManager).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
            var old=singleton.GetValue(null);
            var selectionObject=new GameObject("EscapeOrderFixture");
            var selection=selectionObject.AddComponent<UnitSelectionManager>();
            bool oldSettings=UnitSelectionManager.IsSettingsMenuOpen;
            try
            {
                SettingsMenuUI.Close();
                Surface("OpenCommander");chat.InputText="my unsent draft";
                var active=manager.SubmitEnsureUnitCount(1,4);
                var callback=typeof(UnitSelectionManager).GetMethod("OnEscapePressed",flags);
                callback.Invoke(selection,new[]{Activator.CreateInstance(callback.GetParameters()[0].ParameterType)});
                Assert.That(chat.IsExpanded,Is.False,"Input callbacks occur before MonoBehaviour.Update; Commander must own Escape first.");
                callback.Invoke(selection,new[]{Activator.CreateInstance(callback.GetParameters()[0].ParameterType)});
                Assert.That(UnitSelectionManager.IsSettingsMenuOpen,Is.False,"A duplicate callback in that frame cannot open game settings.");
                Assert.That(chat.InputText,Is.EqualTo("my unsent draft"));
                Assert.That(active.IsTerminal,Is.False);Assert.That(provider.Calls,Is.Zero);
            }
            finally
            {
                SettingsMenuUI.Close();if(oldSettings)SettingsMenuUI.Open();
                UnityEngine.Object.DestroyImmediate(selectionObject);singleton.SetValue(null,old);
            }
        }

        [Test]
        public void GameplayAttackMoveCallback_IsInertWhileAnotherTextFieldOwnsKeyboard()
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var singleton=typeof(UnitSelectionManager).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
            var old=singleton.GetValue(null);var obj=new GameObject("TypingAttackFixture");
            var selection=obj.AddComponent<UnitSelectionManager>();
            try
            {
                // This callback depends only on a nonempty selection; no fake unit behavior.
                ((System.Collections.IList)typeof(UnitSelectionManager).GetField("selectedUnits",flags).GetValue(selection)).Add(null);
                UnitSelectionManager.SetChatFocused(true);
                InvokeInputCallback(selection,"OnAttackMovePerformed");
                Assert.That(typeof(UnitSelectionManager).GetField("attackMoveMode",flags).GetValue(selection),Is.False);
                UnitSelectionManager.SetChatFocused(false);
                InvokeInputCallback(selection,"OnAttackMovePerformed");
                Assert.That(typeof(UnitSelectionManager).GetField("attackMoveMode",flags).GetValue(selection),Is.True,"Normal gameplay remains enabled outside text focus.");
            }
            finally {UnitSelectionManager.SetChatFocused(false);UnityEngine.Object.DestroyImmediate(obj);singleton.SetValue(null,old);}
        }

        [Test]
        public void CameraMiddleButton_IsInertWhileTypingAndDoesNotAcquirePointerLock()
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var obj=new GameObject("TypingCameraFixture");var camera=obj.AddComponent<RTSCameraController>();
            var oldLock=Cursor.lockState;bool oldVisible=Cursor.visible;
            try
            {
                UnitSelectionManager.SetChatFocused(true);
                InvokeInputCallback(camera,"BeginMouseControl");
                Assert.That(typeof(RTSCameraController).GetField("mousePanEnabled",flags).GetValue(camera),Is.False);
                Assert.That(typeof(RTSCameraController).GetField("rotateEnabled",flags).GetValue(camera),Is.False);
                Assert.That(Cursor.lockState,Is.EqualTo(oldLock));
            }
            finally
            {
                UnitSelectionManager.SetChatFocused(false);
                InvokeInputCallback(camera,"EndMouseControl");
                var pivot=(Transform)typeof(RTSCameraController).GetField("pivot",flags).GetValue(camera);
                UnityEngine.Object.DestroyImmediate(pivot!=null?pivot.gameObject:obj);
                Cursor.lockState=oldLock;Cursor.visible=oldVisible;
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void CommanderPointerOwnership_IsLimitedToCurrentlyVisiblePanel(bool expanded)
        {
            if(expanded)Surface("OpenCommander");else Surface("MinimizeCommander");
            Canvas.ForceUpdateCanvases();
            var region=expanded?Panel:Compact.GetComponent<RectTransform>();
            var corners=new Vector3[4];region.GetWorldCorners(corners);
            var center=RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);
            var outside=RectTransformUtility.WorldToScreenPoint(null,corners[0]-new Vector3(100,100,0));
            Assert.That(PointerOwned(center),Is.True,"A click on the visible Commander must not reach gameplay selection or camera control.");
            Assert.That(PointerOwned(outside),Is.False,"The rest of the game remains interactive; no full-screen hit blocker.");
        }

        [Test]
        public void CommanderPointerOwnership_InactiveHostCannotBlockGameplay()
        {
            Surface("OpenCommander");Canvas.ForceUpdateCanvases();
            var corners=new Vector3[4];Panel.GetWorldCorners(corners);
            var center=RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);
            Assert.That(PointerOwned(center),Is.True);
            chat.gameObject.SetActive(false);
            Assert.That(PointerOwned(center),Is.False);
        }

        [TestCase(3f,"3 s")][TestCase(3.25f,"3.25 s")][TestCase(3.1234567f,"3.1234567 s")]
        public void VisibleDurationLimit_IsFrozenForRecordingEvenIfSettingsChange(float requested,string displayed)
        {
            float prior=CommanderVoiceSettings.MaxDurationSeconds;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            string Label(string name)
            {
                var value=typeof(CommanderChatUI).GetField(name,flags).GetValue(chat);
                return (string)value.GetType().GetProperty("text").GetValue(value);
            }
            try
            {
                CommanderVoiceSettings.MaxDurationSeconds=requested;
                typeof(CommanderChatUI).GetMethod("UpdatePresentation",flags).Invoke(chat,null);
                Assert.That(Label("compactStatus"),Does.Contain(displayed));
                Assert.That(chat.VoiceController.StartRecording(maxDuration:requested),Is.True);
                CommanderVoiceSettings.MaxDurationSeconds=60;
                typeof(CommanderChatUI).GetMethod("UpdateVoiceUI",flags).Invoke(chat,null);
                Assert.That(Label("compactStatus"),Does.Contain(displayed));
                Assert.That(Label("voiceStatusText"),Does.Contain(displayed));
                Assert.That(Label("compactStatus"),Does.Not.Contain("60 s"));
            }
            finally {chat.VoiceController.Cancel();CommanderVoiceSettings.MaxDurationSeconds=prior;}
        }

        [Test]
        public void CaptureReadinessChange_RefreshesListeningIndicatorWithoutVoiceStateChange()
        {
            var audio=new ReadinessCapture();chat.SetVoiceProvider(new Speech(),audio);
            Assert.That(chat.VoiceController.StartRecording(maxDuration:3),Is.True);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            string Status()
            {
                var value=typeof(CommanderChatUI).GetField("compactStatus",flags).GetValue(chat);
                return (string)value.GetType().GetProperty("text").GetValue(value);
            }
            Assert.That(Status(),Does.Contain("Opening microphone"));
            audio.Listening=true;
            typeof(CommanderChatUI).GetMethod("Update",flags).Invoke(chat,null);
            Assert.That(Status(),Does.Contain("Listening"));Assert.That(Status(),Does.Contain("3 s"));
            Assert.That(provider.Calls,Is.Zero);Assert.That(manager.Goals,Is.Empty);
        }

        private sealed class ReadinessCapture:ICommanderAudioCapture,ICommanderCaptureReadiness
        {
            public bool Listening;public bool IsRecording{get;private set;}public string CurrentDevice=>"fixture";
            public bool PermissionReady=>true;public bool IsListening=>Listening;public bool CaptureEnded=>false;public string CaptureError=>"";
            public void SetHeldKey(string code){}public Task<bool> RequestPermissionAsync(CancellationToken token)=>Task.FromResult(true);
            public bool StartRecording(string deviceName=null,float maxDurationSeconds=15){IsRecording=true;return true;}
            public CommanderAudioData StopRecording(){IsRecording=false;return CommanderAudioData.Empty;}
            public void CancelRecording(){IsRecording=false;}public void Dispose(){CancelRecording();}
        }

        [Test]
        public void PlayerModelControls_AppearOnlyOnDeliberateSetupAndDoNotOpenMicrophone()
        {
            var controls=chat.transform.Find("CommanderCanvas/CompactCommander/VoiceSetup/SetupViewport/SetupContent/LocalModels");
            Assert.That(controls,Is.Not.Null,"Windows player needs import/download controls, not developer CLI instructions.");
            Assert.That(controls.gameObject.activeInHierarchy,Is.False);
            Surface("ShowVoiceSetup");Assert.That(controls.gameObject.activeInHierarchy,Is.True);
            Assert.That(chat.IsExpanded,Is.False);Assert.That(chat.VoiceState,Is.EqualTo(CommanderVoiceState.Idle));
            Assert.That(provider.Calls,Is.Zero);Assert.That(manager.Goals,Is.Empty);
        }
        [Test]
        public void SelectingUnmeasuredCandidate_DoesNotActivateItOrGrantGameplayAuthority()
        {
            string prior=CommanderVoiceSettings.ModelName;
            var method=typeof(CommanderChatUI).GetMethod("SelectLocalModelCandidate",BindingFlags.Public|BindingFlags.Instance);
            Assert.That(method,Is.Not.Null);Assert.That((bool)method.Invoke(chat,new object[]{"ggml-base.en.bin"}),Is.True);
            Assert.That(CommanderVoiceSettings.ModelName,Is.EqualTo(prior));
            Assert.That(chat.VoiceState,Is.EqualTo(CommanderVoiceState.Idle));Assert.That(provider.Calls,Is.Zero);Assert.That(manager.Goals,Is.Empty);
        }

        private bool PointerOwned(Vector2 point)
        {
            var method=typeof(CommanderChatUI).GetMethod("ContainsScreenPoint",BindingFlags.Instance|BindingFlags.Public);
            return method!=null&&(bool)method.Invoke(chat,new object[]{point});
        }

        private static void InvokeInputCallback(object target,string method)
        {
            var callback=target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic);
            callback.Invoke(target,new[]{Activator.CreateInstance(callback.GetParameters()[0].ParameterType)});
        }

        private sealed class Speech:ICommanderSpeechToTextProvider
        {
            public bool IsAvailable=>true;
            public Task<CommanderSpeechToTextResult> TranscribeAsync(CommanderAudioData audio,CancellationToken token)
                =>Task.FromResult(CommanderSpeechToTextResult.Accepted("make four spearmen"));
            public void Dispose(){}
        }
        private sealed class Capture:ICommanderAudioCapture
        {
            public bool IsRecording{get;private set;} public string CurrentDevice=>"fixture";
            public bool StartRecording(string deviceName=null,float maxDurationSeconds=15){IsRecording=true;return true;}
            public CommanderAudioData StopRecording(){IsRecording=false;return new CommanderAudioData(new float[1600],16000,1);}
            public void CancelRecording(){IsRecording=false;} public void Dispose(){IsRecording=false;}
        }
        private sealed class SemanticProvider:ICommanderAIProvider,ICommanderSemanticProvider
        {
            internal int Calls; internal string Json="{\"outcome\":\"Answer\",\"message\":\"fixture answer\"}";
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,CancellationToken token)
            {Calls++;return Task.FromResult(CommanderSemanticJson.Parse(Json));}
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,CancellationToken token)
            {Calls++;return Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,"fixture"));}
        }
    }
}
