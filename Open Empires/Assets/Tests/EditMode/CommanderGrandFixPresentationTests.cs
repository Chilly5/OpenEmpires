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
        [Test]
        public void MarkerWithoutPendingRequest_AcknowledgesStorageAndExplainsInvalidClick()
        {
            int x=sim.MapData.Width/2+6,z=sim.MapData.Height/2+6;sim.FogOfWar.SetVisible(0,x,z);
            Assert.That(chat.TryPlaceDestinationMarker(x,z),Is.True);
            Assert.That(chat.DisplayedTranscript,Does.Contain("Destination saved"));
            Assert.That(manager.Goals,Is.Empty);Assert.That(provider.Calls,Is.Zero);
            Assert.That(chat.TryPlaceDestinationMarker(-1,z),Is.False);
            Assert.That(chat.DisplayedTranscript,Does.Contain("inside the map"));
        }

        [Test]
        public async Task MarkerStorage_AfterAnAdmittedTaskRemainsVisibleInMinimizedStatus()
        {
            provider.Json="{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}]}";
            await chat.SubmitMessageAsync("train four spearmen");
            Assert.That(manager.Goals,Has.Count.EqualTo(1));
            int x=sim.MapData.Width/2+6,z=sim.MapData.Height/2+6;sim.FogOfWar.SetVisible(0,x,z);
            chat.TryPlaceDestinationMarker(x,z);
            typeof(CommanderChatUI).GetMethod("UpdatePresentation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,null);
            var status=typeof(CommanderChatUI).GetField("compactStatus",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat);
            Assert.That((string)status.GetType().GetProperty("text").GetValue(status),Is.EqualTo("Destination saved"));
            Assert.That(manager.Goals,Has.Count.EqualTo(1),"Saving a marker cannot append movement to production.");
        }

        [Test]
        public async Task MultiplePendingLocations_AskForStepAndFillOnlyChosenSlot()
        {
            provider.Json="{\"outcome\":\"Clarify\",\"message\":\"Where?\",\"pending\":{\"executionOrder\":\"DependencyGraph\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"quantityMode\":\"New\"},{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":4,\"dependsOn\":[0],\"resultFromNode\":0},{\"type\":\"MoveUnits\",\"unitSelector\":\"Scout\",\"count\":1}]},\"missingSlots\":[{\"node\":1,\"path\":\"location\",\"type\":\"Location\",\"alternatives\":[\"PlayerMarker\",\"PlayerBase\"]},{\"node\":2,\"path\":\"location\",\"type\":\"Location\",\"alternatives\":[\"PlayerMarker\",\"PlayerBase\"]}]}";
            await chat.SubmitMessageAsync("Train four new spearmen, move them, and move my scout");
            Assert.That(chat.PendingClarification,Is.Not.Null,chat.DisplayedTranscript);
            int x=sim.MapData.Width/2+6,z=sim.MapData.Height/2+6;sim.FogOfWar.SetVisible(0,x,z);
            Assert.That(chat.TryPlaceDestinationMarker(x,z),Is.True);
            Assert.That(chat.DisplayedTranscript,Does.Contain("Which step"));
            Assert.That(chat.PendingClarification.GraphDraft.MissingSlots.Count,Is.EqualTo(2));
            typeof(CommanderChatUI).GetField("markerInputFrame",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(chat,-1);
            await chat.SubmitMessageAsync("step 2");
            Assert.That(chat.PendingClarification.GraphDraft.MissingSlots.Count,Is.EqualTo(1));
            Assert.That(chat.PendingClarification.GraphDraft.MissingSlots[0].Node,Is.EqualTo(2));
            typeof(CommanderChatUI).GetMethod("UpdatePresentation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,null);
            var status=typeof(CommanderChatUI).GetField("latestRequestState",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat);
            Assert.That(status,Is.EqualTo("Needs clarification"),"Resolving a destination step must clear the obsolete step-choice heading.");
            Assert.That(provider.Calls,Is.EqualTo(1));Assert.That(manager.Goals,Is.Empty);
        }

        [Test]
        public async Task MultiplePendingLocations_KeepEachSelectedPointThroughApprovalAndLaterMarkerChanges()
        {
            provider.UseProviderDeclarations=true;
            provider.Json="{\"outcome\":\"Clarify\",\"message\":\"Where?\",\"pending\":{\"executionOrder\":\"Independent\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1},{\"type\":\"MoveUnits\",\"unitSelector\":\"Scout\",\"count\":1}]},\"missingSlots\":[{\"node\":0,\"path\":\"location\",\"type\":\"Location\",\"alternatives\":[\"PlayerMarker\",\"PlayerBase\"]},{\"node\":1,\"path\":\"location\",\"type\":\"Location\",\"alternatives\":[\"PlayerMarker\",\"PlayerBase\"]}]}";
            int x=sim.MapData.Width/2,z=sim.MapData.Height/2;
            for(int tx=x-3;tx<=x+22;tx++)
                for(int tz=z-3;tz<=z+10;tz++)
                {
                    sim.MapData.Tiles[tx,tz]=TileType.Grass;
                    sim.MapData.ForestDensity[tx,tz]=0;
                    sim.MapData.FoundationCount[tx,tz]=0;
                    sim.FogOfWar.SetVisible(0,tx,tz);
                }
            var actors=new System.Collections.Generic.List<UnitData>();
            foreach(string name in new[]{"Spearman","Scout"})
            {
                Assert.That(CommanderIntentCatalog.TryResolveUnit(name,out int type),Is.True);
                var actor=sim.UnitRegistry.CreateUnit(0,sim.MapData.TileToWorldFixed(x,z),Fixed32.One,Fixed32.One,Fixed32.One);
                actor.UnitType=type;actor.IsVillager=false;actor.MaxHealth=actor.CurrentHealth=100;actor.State=UnitState.Idle;
                actors.Add(actor);
            }
            var first=new Vector2Int(x+6,z+6);var second=new Vector2Int(x+12,z+6);var later=new Vector2Int(x+18,z+6);
            foreach(var point in new[]{first,second,later})sim.FogOfWar.SetVisible(0,point.x,point.y);
            await chat.SubmitMessageAsync("Move my Spearman and my Scout to their chosen destinations");
            long requestId=chat.PendingClarification.RequestId;
            Assert.That(chat.TryPlaceDestinationMarker(first.x,first.y),Is.True);
            typeof(CommanderChatUI).GetField("markerInputFrame",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(chat,-1); // Next input frame.
            await chat.SubmitMessageAsync("step 1");
            Assert.That(chat.PendingClarification.GraphDraft.MissingSlots.Count,Is.EqualTo(1));
            Assert.That(chat.TryPlaceDestinationMarker(second.x,second.y),Is.True);
            Assert.That(chat.PendingActionPlan,Is.Not.Null,chat.DisplayedTranscript);
            Assert.That(chat.PendingActionPlan.RequestId,Is.EqualTo(requestId));
            Assert.That(manager.Goals,Is.Empty);Assert.That(provider.Calls,Is.EqualTo(1));
            typeof(CommanderChatUI).GetField("markerInputFrame",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(chat,-1); // Separate approval frame.
            await chat.SubmitMessageAsync("approve plan");
            var moves=manager.Goals.OfType<CommanderCapabilityGoal>().OrderBy(g=>g.RequestNodeIndex).ToArray();
            Assert.That(moves.Length,Is.EqualTo(2),chat.DisplayedTranscript);
            Assert.That(moves[0].MarkerMoveLocation.TryResolve(sim,0,out int firstX,out int firstZ,out _,out _),Is.True);
            Assert.That(new Vector2Int(firstX,firstZ),Is.EqualTo(first),"The second slot must not silently replace the first selected destination.");
            Assert.That(moves[1].MarkerMoveLocation.TryResolve(sim,0,out int secondX,out int secondZ,out _,out _),Is.True);
            Assert.That(new Vector2Int(secondX,secondZ),Is.EqualTo(second));
            Assert.That(manager.TrySetPlayerMarkerSnapshot(later.x,later.y),Is.True);
            Assert.That(moves[0].MarkerMoveLocation.TryResolve(sim,0,out firstX,out firstZ,out _,out _),Is.True);
            Assert.That(new Vector2Int(firstX,firstZ),Is.EqualTo(first));
            Assert.That(moves[1].MarkerMoveLocation.TryResolve(sim,0,out secondX,out secondZ,out _,out _),Is.True);
            Assert.That(new Vector2Int(secondX,secondZ),Is.EqualTo(second));
            var commands=new System.Collections.Generic.List<MoveCommand>();
            sim.CommandBuffer.CommandEnqueued+=(command,origin)=>
            {if(origin==CommandEnqueueSource.Commander&&command is MoveCommand move)commands.Add(move);};
            for(int step=0;step<2000&&moves.Any(move=>move.Status!=CommanderGoalStatus.Completed);step++)
            {manager.Tick(sim.CurrentTick);sim.Tick();}
            Assert.That(commands,Has.Count.EqualTo(2));
            for(int i=0;i<moves.Length;i++)
            {
                Assert.That(moves[i].Status,Is.EqualTo(CommanderGoalStatus.Completed),moves[i].StatusReason);
                var target=sim.MapData.TileToWorldFixed(i==0?first.x:second.x,i==0?first.y:second.y);
                Assert.That((actors[i].SimPosition-target).ToVector3().magnitude,Is.LessThanOrEqualTo(3f));
                Assert.That(commands.Count(command=>command.UnitIds.Contains(actors[i].Id)),Is.EqualTo(1));
            }
            provider.Json="{\"outcome\":\"Request\",\"executionOrder\":\"Independent\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"},{\"type\":\"MoveUnits\",\"unitSelector\":\"Scout\",\"count\":1,\"location\":\"PlayerMarker\"}]}";
            await chat.SubmitMessageAsync("Move both to my latest marker");
            Assert.That(chat.PendingActionPlan,Is.Not.Null,chat.DisplayedTranscript);
            await chat.SubmitMessageAsync("approve plan");
            var next=manager.Goals.OfType<CommanderCapabilityGoal>().Where(g=>!moves.Contains(g)).ToArray();
            Assert.That(next.Length,Is.EqualTo(2));Assert.That(provider.Calls,Is.EqualTo(2));
            foreach(var move in next)
            {
                Assert.That(move.MarkerMoveLocation.TryResolve(sim,0,out int nextX,out int nextZ,out _,out _),Is.True);
                Assert.That(new Vector2Int(nextX,nextZ),Is.EqualTo(later),"A new request cannot inherit the previous request's slot snapshots.");
            }
        }

        [Test]
        public async Task ProviderLocationClarification_MarkerResumesSameSingleBuildExactlyOnce()
        {
            provider.Json=CommanderPhase5CStabilizationTests.MissingBuildLocation;
            await chat.SubmitMessageAsync("Build a lumber yard");
            Assert.That(chat.PendingClarification,Is.Not.Null,"A displayed location question must retain the typed draft.");
            long request=chat.LastSubmittedSemanticRequestId.Value;
            var tile=new Vector2Int(sim.MapData.Width/2+6,sim.MapData.Height/2+6);
            sim.MapData.Tiles[tile.x,tile.y]=TileType.Grass;
            sim.FogOfWar.SetVisible(0,tile.x,tile.y);
            Assert.That(chat.TryPlaceDestinationMarker(tile.x,tile.y),Is.True);
            Assert.That(provider.Calls,Is.EqualTo(1));
            Assert.That(chat.LastSubmittedSemanticRequestId,Is.EqualTo(request));
            Assert.That(chat.PendingClarification,Is.Null);
            Assert.That(chat.Conversation.SemanticMemory.Snapshot().Any(e=>e.Kind==CommanderSemanticMemoryKind.Clarification),Is.False);
            Assert.That(chat.PendingActionPlan,Is.Null,"An ordinary authorized single build retains its automatic submission rule.");
            Assert.That(manager.Goals.Count,Is.EqualTo(1));
            Assert.That(chat.TryPlaceDestinationMarker(tile.x,tile.y),Is.True);
            Assert.That(manager.Goals.Count,Is.EqualTo(1),"A late duplicate marker cannot create another building goal.");
        }

        [Test]
        public async Task MarkerAndOtherMissingSlot_AnswersOnlyLocationAndStillRequiresPlanApproval()
        {
            var draft=Newtonsoft.Json.Linq.JObject.Parse(CommanderPhase5CStabilizationTests.MissingBuildLocation);
            draft["pending"]["executionOrder"]="Sequential";
            var nodes=(Newtonsoft.Json.Linq.JArray)draft["pending"]["nodes"];
            ((Newtonsoft.Json.Linq.JObject)nodes[0]).Remove("count");
            nodes.Add(Newtonsoft.Json.Linq.JObject.Parse("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"quantityMode\":\"New\",\"dependsOn\":[0]}"));
            ((Newtonsoft.Json.Linq.JArray)draft["missingSlots"]).Insert(0,
                Newtonsoft.Json.Linq.JObject.Parse("{\"node\":0,\"path\":\"count\",\"type\":\"Count\",\"alternatives\":[1]}"));
            provider.Json=draft.ToString();
            var parsed=CommanderSemanticJson.Parse(provider.Json);
            Assert.That(parsed.IsValid,Is.True,parsed.SchemaDiagnostic);
            await chat.SubmitMessageAsync("Build a lumber yard then train 3 new Spearmen");
            Assert.That(chat.PendingClarification?.GraphDraft,Is.Not.Null,chat.DisplayedTranscript);
            int x=sim.MapData.Width/2+6,z=sim.MapData.Height/2+6;sim.FogOfWar.SetVisible(0,x,z);
            Assert.That(chat.TryPlaceDestinationMarker(x,z),Is.True);
            Assert.That(chat.PendingClarification.GraphDraft.MissingSlots.Count,Is.EqualTo(1));
            Assert.That(chat.PendingClarification.GraphDraft.MissingSlots[0].Type,Is.EqualTo("Count"));
            Assert.That(manager.Goals,Is.Empty);Assert.That(provider.Calls,Is.EqualTo(1));
            // EditMode has no advancing input frames. Model marker release before a
            // later text reply; the actual PlayMode callback checks real frame ownership.
            typeof(CommanderChatUI).GetField("markerInputFrame",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(chat,-1);
            await chat.SubmitMessageAsync("1");
            Assert.That(chat.PendingActionPlan,Is.Not.Null,chat.DisplayedTranscript);Assert.That(manager.Goals,Is.Empty);
            Assert.That(provider.Calls,Is.EqualTo(1));
            Assert.That(chat.PendingActionPlan.RequestId,Is.EqualTo(chat.LastSubmittedSemanticRequestId));
            Assert.That(chat.PendingActionPlan.Interpretation.Nodes[1].DependsOn,Does.Contain(0));
        }

        [Test]
        public async Task ResetClearsPendingMarkerDraftAndLateClickCannotResumeOldRequest()
        {
            provider.Json=CommanderPhase5CStabilizationTests.MissingBuildLocation;
            await chat.SubmitMessageAsync("Build a lumber yard");
            Assert.That(chat.PendingClarification,Is.Not.Null);
            chat.ResetConversation();
            int x=sim.MapData.Width/2+6,z=sim.MapData.Height/2+6;sim.FogOfWar.SetVisible(0,x,z);
            Assert.That(chat.TryPlaceDestinationMarker(x,z),Is.True);
            Assert.That(manager.Goals,Is.Empty);Assert.That(chat.PendingClarification,Is.Null);
            Assert.That(chat.PendingActionPlan,Is.Null);Assert.That(provider.Calls,Is.EqualTo(1));
        }

        [Test]
        public async Task TextualLocationReply_PreservesTenNewUnitsAndDependencyWithoutAnotherInterpretation()
        {
            provider.Json="{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"quantityMode\":\"New\"},{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":10,\"location\":\"PlayerMarker\",\"dependsOn\":[0],\"resultFromNode\":0}]}";
            await chat.SubmitMessageAsync("Train ten new Spearmen and send them there");
            Assert.That(chat.DisplayedTranscript,Does.Contain("Where?"));
            await chat.SubmitMessageAsync("my town center");
            Assert.That(provider.Calls,Is.EqualTo(1));
            Assert.That(chat.PendingActionPlan,Is.Not.Null);
            var nodes=chat.PendingActionPlan.Interpretation.Nodes;
            Assert.That(nodes[0].Count,Is.EqualTo(10));
            Assert.That(nodes[0].QuantityMode,Is.EqualTo(CommanderProductionQuantityMode.New));
            Assert.That(nodes[1].ResultFromNode,Is.EqualTo(0));
            Assert.That(nodes[1].DependsOn,Does.Contain(0));
            Assert.That(nodes[1].LocationSelector,Is.EqualTo(CommanderSemanticLocationSelector.PlayerBase));
            Assert.That(manager.Goals,Is.Empty);
        }
        [Test]
        public void DestinationPlacement_OwnsGameplayInputUntilCancelled()
        {
            var begin=typeof(CommanderChatUI).GetMethod("BeginDestinationPlacement");
            Assert.That(begin,Is.Not.Null,"The player needs a destination placement control.");
            begin.Invoke(chat,null);
            Assert.That(UnitSelectionManager.UIInputSuppressed,Is.True);
            typeof(CommanderChatUI).GetMethod("ClearDestinationMarker").Invoke(chat,null);
            Assert.That(UnitSelectionManager.UIInputSuppressed,Is.False);
            Assert.That(manager.Goals,Is.Empty);
        }

        [Test]
        public async Task MarkerContinuation_PreservesRequestAndDoesNotInterpretOrApproveAgain()
        {
            provider.Json="{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"quantityMode\":\"New\"},{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":3,\"location\":\"PlayerMarker\",\"dependsOn\":[0],\"resultFromNode\":0}]}";
            await chat.SubmitMessageAsync("Train three new spearmen then send them to my marker");
            Assert.That(provider.Calls,Is.EqualTo(1));
            Assert.That(chat.DisplayedTranscript,Does.Contain("Where?"));
            Assert.That(manager.Goals,Is.Empty);
            var tile=new Vector2Int(sim.MapData.Width/2,sim.MapData.Height/2);
            sim.FogOfWar.SetVisible(0,tile.x+6,tile.y+6);
            var place=typeof(CommanderChatUI).GetMethod("TryPlaceDestinationMarker");
            Assert.That(place,Is.Not.Null);
            Assert.That((bool)place.Invoke(chat,new object[]{tile.x+6,tile.y+6}),Is.True);
            Assert.That(provider.Calls,Is.EqualTo(1),"Marker click fills the retained semantic location locally.");
            Assert.That(chat.PendingActionPlan,Is.Not.Null,"Location completion must prepare a separate preview.");
            Assert.That(manager.Goals,Is.Empty,"A marker click is not gameplay approval.");
            var question=(GameObject)typeof(CommanderChatUI).GetField("pendingQuestion",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat);
            Assert.That(question.activeSelf,Is.False,"A resolved destination must not still display the old location question beside its approval preview.");
            var candidate=chat.PendingActionPlan;
            string transcript=chat.DisplayedTranscript;
            Assert.That(chat.TryPlaceDestinationMarker(tile.x+6,tile.y+6),Is.True);
            Assert.That(chat.PendingActionPlan,Is.SameAs(candidate),"A duplicate callback must not replace the approval candidate.");
            Assert.That(chat.DisplayedTranscript,Is.EqualTo(transcript),"A duplicate callback must not repeat the preview/question.");
            chat.BeginDestinationPlacement();
            typeof(CommanderChatUI).GetMethod("UpdatePresentation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,null);
            var compact=typeof(CommanderChatUI).GetField("compactStatus",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat);
            Assert.That((string)compact.GetType().GetProperty("text").GetValue(compact),Is.EqualTo("Select destination"),
                "An approval preview must not mask the active world-click input state.");
            Assert.That(chat.PendingActionPlan,Is.SameAs(candidate));
            Assert.That(provider.Calls,Is.EqualTo(1));Assert.That(manager.Goals,Is.Empty);
        }
        [Test]
        public async Task UnsupportedRequest_IsRejectedRatherThanCompleted()
        {
            provider.Json="{\"outcome\":\"Unsupported\",\"message\":\"That action is not supported. Try a supported building order.\"}";
            await chat.SubmitMessageAsync("build an unsupported object");
            var state=typeof(CommanderChatUI).GetField("latestRequestState",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat);
            Assert.That(state,Is.EqualTo("Rejected"));
            Assert.That(manager.Goals,Is.Empty);
        }
        private void Surface(string method)=>typeof(CommanderChatUI).GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.Invoke(chat,null);
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
        public void LauncherAndSettings_UseNarrowRightDockAndVisibleVoiceControls()
        {
            var compactRect=Compact.GetComponent<RectTransform>();
            Assert.That(compactRect.anchorMin,Is.EqualTo(new Vector2(1,1)));
            Assert.That(compactRect.anchoredPosition.x,Is.LessThan(0));
            Assert.That(compactRect.anchoredPosition.y,Is.LessThanOrEqualTo(-48));
            Assert.That(Compact.transform.Find("Launcher").gameObject.activeSelf,Is.True);
            Assert.That(Compact.transform.Find("VoiceControls/Record").gameObject.activeSelf,Is.True);
            Assert.That(Compact.transform.Find("VoiceControls/CancelVoice").gameObject.activeSelf,Is.True);
            var statusObject=Compact.transform.Find("Status").GetComponent("TextMeshProUGUI");
            var status=(string)statusObject.GetType().GetProperty("text").GetValue(statusObject);
            Assert.That(status,Does.Not.Contain("\n"));
            Assert.That(status.Length,Is.LessThanOrEqualTo(30));
            Surface("OpenCommander");
            Assert.That(Panel.anchoredPosition.x,Is.EqualTo(compactRect.anchoredPosition.x).Within(1));
            Assert.That(Panel.anchoredPosition.y,Is.LessThanOrEqualTo(-48));
            Surface("OpenSettingsMenu");
            var menu=Panel.Find("SettingsMenu");
            Assert.That(chat.IsExpanded,Is.True);
            Assert.That(menu,Is.Not.Null);
            Assert.That(menu.gameObject.activeInHierarchy,Is.True);
            Canvas.ForceUpdateCanvases();
            Assert.That(menu.GetComponent<RectTransform>().rect.width,Is.LessThanOrEqualTo(360));
            Assert.That(provider.Calls,Is.Zero);
            Assert.That(manager.Goals,Is.Empty);
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
            Assert.That(Compact.transform.Find("TranscriptReview").gameObject.activeSelf,Is.True);
            var editor=Compact.transform.Find("TranscriptReview/Editor").GetComponent("TMP_InputField");
            Assert.That(editor,Is.Not.Null);
            editor.GetType().GetProperty("text").SetValue(editor,"move my archers to marker");
            Assert.That(chat.InputText,Is.EqualTo("move my archers to marker"));
            Canvas.ForceUpdateCanvases();
            var bounds=new Vector3[4];Compact.GetComponent<RectTransform>().GetWorldCorners(bounds);
            float scale=Compact.GetComponentInParent<Canvas>().scaleFactor;
            Assert.That(bounds[0].y,Is.GreaterThanOrEqualTo(216*scale),"Compact review must remain above the native minimap.");
            Assert.That(provider.Calls,Is.Zero); Assert.That(manager.Goals,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public async Task ExpandedVoiceReview_ShowsFullOriginalAndGuidanceInBoundedScroll(bool ambiguous)
        {
            string raw="train four rider " + string.Join(" ",Enumerable.Repeat("original recognition details",12));
            string reviewed=ambiguous?raw:"train four Spearmen";
            string guidance=ambiguous?"Choose Knight or Horseman. Edit the transcript before Send.":"Catalog spelling corrected. Review the editable text before Send.";
            chat.SetVoiceProvider(new MockCommanderSpeechToTextProvider(raw),new Capture());
            chat.VoiceController.TranscriptProcessor=text=>new CommanderVoiceTranscriptReview(text,reviewed,guidance,ambiguous);
            Surface("OpenCommander");
            Assert.That(chat.VoiceController.StartRecording(),Is.True);
            await chat.VoiceController.StopRecordingAndTranscribeAsync();
            Canvas.ForceUpdateCanvases();
            var review=Panel.Find("VoiceReview");
            Assert.That(review.gameObject.activeInHierarchy,Is.True);
            var scroll=review.Find("Scroll");
            Assert.That(scroll,Is.Not.Null,"Original recognition and choices need a bounded scroll surface, not an ellipsis label.");
            var scrollComponent=scroll.GetComponent("ScrollRect");
            Assert.That((bool)scrollComponent.GetType().GetProperty("vertical").GetValue(scrollComponent),Is.True);
            var details=scroll.Find("Viewport/ReviewText").GetComponent("TextMeshProUGUI");
            string displayed=(string)details.GetType().GetProperty("text").GetValue(details);
            Assert.That(displayed,Does.Contain(raw).And.Contain(guidance));
            Assert.That(chat.InputText,Is.EqualTo(reviewed));
            var input=Panel.Find("TextSubmission/Input").GetComponent("TMP_InputField");
            Assert.That(input.gameObject.activeInHierarchy,Is.True);
            input.GetType().GetProperty("text").SetValue(input,"my reviewed correction");
            Assert.That(chat.InputText,Is.EqualTo("my reviewed correction"));
            // Reproduce the 350-canvas-unit dock observed in the 1520x656 game
            // view even when the EditMode runner happens to have a taller view.
            Panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,350);
            Canvas.ForceUpdateCanvases();
            var conversation=(Component)typeof(CommanderChatUI).GetField("transcriptScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat);
            Assert.That(conversation.gameObject.activeSelf,Is.False,"Review replaces the conversation body to fit the short dock.");
            var cardBounds=new Vector3[4];var panelBounds=new Vector3[4];
            review.GetComponent<RectTransform>().GetWorldCorners(cardBounds);Panel.GetWorldCorners(panelBounds);
            Assert.That(cardBounds[0].y,Is.GreaterThanOrEqualTo(panelBounds[0].y-1));
            Assert.That(cardBounds[1].y,Is.LessThanOrEqualTo(panelBounds[1].y+1));
            var composerBounds=new Vector3[4];
            Panel.Find("TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerBounds);
            Assert.That(composerBounds[0].y,Is.GreaterThanOrEqualTo(panelBounds[0].y-1),"Editable composer must remain inside the short dock above the minimap.");
            Assert.That(composerBounds[1].y,Is.LessThanOrEqualTo(panelBounds[1].y+1));
            Assert.That(scroll.GetComponent<RectTransform>().rect.height,Is.GreaterThanOrEqualTo(20),"Original and guidance must retain an accessible scroll viewport.");
            Assert.That(provider.Calls,Is.Zero);Assert.That(manager.Goals,Is.Empty);
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
            var setup=chat.transform.Find("CommanderCanvas/Panel/VoiceSetup");
            Assert.That(setup,Is.Not.Null);Assert.That(setup.gameObject.activeSelf,Is.False);
            Surface("ShowVoiceSetup");Assert.That(setup.gameObject.activeSelf,Is.True);Assert.That(chat.IsExpanded,Is.True);
            Canvas.ForceUpdateCanvases();Assert.That(setup.GetComponent<RectTransform>().rect.width,Is.GreaterThanOrEqualTo(320));
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
            Assert.That(chat.VoiceState,Is.EqualTo(CommanderVoiceState.Idle));Assert.That(chat.IsExpanded,Is.True,"Explicit policy review opens Settings; consent must not start recording or an order.");
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
            Assert.That(Compact.transform.Find("PlanApproval").gameObject.activeSelf,Is.True,
                "The candidate must be visible for recording-bound spoken approval.");
            Surface("OpenCommander");
            typeof(CommanderChatUI).GetMethod("UpdatePresentation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chat,null);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            // Inspect the actually visible expanded review body after opening Details.
            // Keep this assembly's existing TMP dependency boundary via reflection.
            var body=(Component)typeof(CommanderChatUI).GetField("expandedApprovalText",flags).GetValue(chat);
            Assert.That(body.GetType().GetProperty("text").GetValue(body),Is.EqualTo(candidate.Preview));
            Assert.That(body.GetType().GetProperty("richText").GetValue(body),Is.False);
            var scroll=body.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            Assert.That(scroll,Is.Not.Null);Assert.That(scroll.content,Is.SameAs(body.transform));
            Assert.That(body.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit,Is.EqualTo(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize));
            Assert.That(scroll.gameObject.activeInHierarchy,Is.True);
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
            Assert.That(Status(),Does.Contain("Mic opening"));
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
            var controls=chat.transform.Find("CommanderCanvas/Panel/VoiceSetup/SetupViewport/SetupContent/LocalModels");
            Assert.That(controls,Is.Not.Null,"Windows player needs import/download controls, not developer CLI instructions.");
            Assert.That(controls.gameObject.activeInHierarchy,Is.False);
            Surface("ShowVoiceSetup");Assert.That(controls.gameObject.activeInHierarchy,Is.True);
            Assert.That(chat.IsExpanded,Is.True);Assert.That(chat.VoiceState,Is.EqualTo(CommanderVoiceState.Idle));
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
            internal bool UseProviderDeclarations;
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,CancellationToken token)
            {Calls++;return Task.FromResult(UseProviderDeclarations?CommanderSemanticJson.ParseProviderResponse(Json):CommanderSemanticJson.Parse(Json));}
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,CancellationToken token)
            {Calls++;return Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,"fixture"));}
        }
    }
}
