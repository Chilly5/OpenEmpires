using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixStatusTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private readonly List<GameObject> objects = new List<GameObject>();
        private int x,z;
        [SetUp] public void SetUp()
        {
            config=ScriptableObject.CreateInstance<SimulationConfig>();
            sim=new GameSimulation(config,2,new[]{0,1},Array.Empty<int>());
            sim.SetPlayerCivilizations(new[]{Civilization.French,Civilization.French});
            sim.SetPlayerAge(0,3); x=sim.MapData.Width/2;z=sim.MapData.Height/2;
            sim.CreateBuilding(0,BuildingType.TownCenter,x,z,false,true).AutoProduceVillagers=false;
            sim.CreateBuilding(0,BuildingType.Barracks,x+8,z,false,true);
            sim.ResourceManager.GetPlayerResources(0).Food=1000;
            sim.ResourceManager.GetPlayerResources(0).Wood=0;
            manager=new CommanderGoalManager(sim,0);dispatcher=new CommanderIntentDispatcher(sim,manager);
        }
        [TearDown] public void TearDown()
        { foreach(var go in objects) UnityEngine.Object.DestroyImmediate(go); objects.Clear(); dispatcher.Dispose();manager.Dispose();UnityEngine.Object.DestroyImmediate(config); }
        private CommanderChatUI Chat(FactsProvider provider)
        { var go=new GameObject("status-test");objects.Add(go);var chat=go.AddComponent<CommanderChatUI>();chat.Initialize(provider,sim,manager,dispatcher);return chat; }
        private static string Request="{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":7}]}";
        private static string Answer="{\"outcome\":\"Answer\",\"message\":\"Everything is completed.\"}";
        private EnsureUnitCountGoal Waiting()
        { var goal=manager.SubmitEnsureUnitCount(1,4,newProductionCount:4); manager.Tick(0);sim.CommandBuffer.FlushCommands();return goal; }

        [TestCase("Why haven't you finished making my army?")]
        [TestCase("Tell me why my Spearmen are still waiting.")]
        [TestCase("How is my request progressing?")]
        public async Task NaturalInformationQuestion_RejectsExecutableReplyWithoutNewWork(string question)
        {
            var goal=Waiting();var provider=new FactsProvider(Request);var chat=Chat(provider);
            int before=manager.Goals.Count;
            Assert.That(await chat.SubmitMessageAsync(question),Is.Null);
            Assert.That(manager.Goals.Count,Is.EqualTo(before));
            Assert.That(goal.Status,Is.Not.EqualTo(CommanderGoalStatus.Cancelled));
            Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
            Assert.That(chat.PendingActionPlan,Is.Null);
        }

        [Test] public async Task FactsCarryActualReasonCountsDeficitAndProducerWithoutTrustingInventedAnswer()
        {
            var goal=Waiting();var provider=new FactsProvider(Answer);var chat=Chat(provider);
            string originalReason=goal.StatusReason;
            await chat.SubmitMessageAsync("Why haven't you finished making my army?");
            var context=JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext);
            var status=context.tacticalStatus;
            Assert.That(status,Is.Not.Null,"Detached tactical facts must survive provider projection.");
            var step=status.requests[0].steps[0];
            Assert.That(step.requestedNew,Is.EqualTo(4));
            Assert.That(step.produced,Is.EqualTo(0));
            Assert.That(step.queued,Is.EqualTo(0));
            Assert.That(step.producerReady,Is.EqualTo(1));
            Assert.That(step.deficits.Wood,Is.EqualTo(20));
            Assert.That(step.reason.ToLowerInvariant(),Does.Contain("wood"));
            Assert.That(goal.StatusReason,Is.EqualTo(originalReason));
            string conversation=chat.Conversation.Memory.ToJson().ToLowerInvariant();
            Assert.That(conversation,Does.Contain("wood"));
            Assert.That(conversation,Does.Not.Contain("everything is completed"));
            Assert.That(provider.Last.QuestionFacts.Length,Is.LessThanOrEqualTo(512));
            Assert.That(provider.Last.SerializedContext.Length,Is.LessThanOrEqualTo(8192));
        }

        [Test] public async Task AmbiguousArmyQuestion_ShowsOwnedChoicesRatherThanSelectingFirstGoal()
        {
            Waiting();manager.SubmitEnsureUnitCount(2,3,newProductionCount:3);
            var provider=new FactsProvider(Answer);var chat=Chat(provider);
            await chat.SubmitMessageAsync("Why is my army still not ready?");
            var status=JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext).tacticalStatus;
            Assert.That(status,Is.Not.Null);
            Assert.That(status.selection,Is.EqualTo("Ambiguous"));
            string text=chat.Conversation.Memory.ToJson().ToLowerInvariant();
            Assert.That(text,Does.Contain("which request"));
            Assert.That(text,Does.Contain("spearman"));Assert.That(text,Does.Contain("archer"));
            Assert.That(manager.Goals.Count,Is.EqualTo(2));Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }

        [Test] public async Task NamedStatusQuestion_SelectsRelevantRequestAndIgnoresForeignOrHiddenFacts()
        {
            Waiting();manager.SubmitEnsureUnitCount(2,3,newProductionCount:3);
            var enemy=sim.UnitRegistry.CreateUnit(1,sim.MapData.TileToWorldFixed(x+40,z),Fixed32.One,Fixed32.One,Fixed32.One);
            enemy.UnitType=2;enemy.CurrentHealth=enemy.MaxHealth=777;
            var provider=new FactsProvider(Answer);var chat=Chat(provider);
            await chat.SubmitMessageAsync("Why are my Spearmen waiting?");
            var status=JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext).tacticalStatus;
            Assert.That(status,Is.Not.Null);
            Assert.That(status.selection,Is.EqualTo("One"));
            Assert.That(status.requests.Length,Is.EqualTo(1));
            Assert.That(provider.Last.SerializedContext,Does.Not.Contain("777"));
            Assert.That(provider.Last.SerializedContext,Does.Not.Contain("\"UnitIds\""));
            Assert.That(provider.Last.SerializedContext,Does.Not.Contain("\"GoalId\""));
        }

        [Test] public async Task Question_DoesNotDismissOrApprovePendingCompoundPreview()
        {
            var provider=new FactsProvider("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1},{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"quantityMode\":\"New\"}]}");
            var chat=Chat(provider);await chat.SubmitMessageAsync("Build a house and produce four new Spearmen");
            var preview=chat.PendingActionPlan;Assert.That(preview,Is.Not.Null);
            provider.Json=Answer;
            await chat.SubmitMessageAsync("How is my army request doing?");
            Assert.That(chat.PendingActionPlan,Is.SameAs(preview));Assert.That(preview.Cancelled,Is.False);
            Assert.That(preview.AuthorizationEvidence,Is.EqualTo("None"));
            Assert.That(manager.Goals,Is.Empty);Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }

        [Test] public async Task NewMatch_DropsLateReadOnlyAnswerWithoutGameplayOrDisclosure()
        {
            Waiting();var provider=new FactsProvider(Answer){Hold=true};var chat=Chat(provider);
            var query=chat.SubmitMessageAsync("Why are my Spearmen waiting?");
            chat.Initialize(new FactsProvider(Answer),sim,manager,dispatcher);
            provider.Release();await query;
            Assert.That(chat.Conversation.Memory.ToJson(),Does.Not.Contain("Everything is completed"));
            Assert.That(manager.Goals.Count,Is.EqualTo(1));Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }

        private sealed class FactsProvider:ICommanderAIProvider,ICommanderSemanticProvider
        {
            public string Json;public bool Hold;public CommanderSemanticProviderRequest Last;
            public Exception Failure;
            private TaskCompletionSource<bool> gate=new TaskCompletionSource<bool>();
            public FactsProvider(string json){Json=json;}
            public async Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,CancellationToken token)
            {Last=request;if(Hold)await gate.Task;if(Failure!=null)throw Failure;return CommanderSemanticJson.Parse(Json);}
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,CancellationToken token)
                =>Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,"Unused legacy route"));
            public void Release()=>gate.TrySetResult(true);
        }

        [Test] public async Task NamedLaterCompoundStep_IsPrioritizedOverUnrelatedBlockedConstruction()
        {
            var parsed=CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1},{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"quantityMode\":\"New\"}]}");
            manager.SubmitSemanticGraph(manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,"House plus new Spearmen",0),0));manager.Tick(0);sim.CommandBuffer.FlushCommands();
            var provider=new FactsProvider(Answer);var chat=Chat(provider);await chat.SubmitMessageAsync("Why are my Spearmen waiting?");
            var status=JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext).tacticalStatus;
            Assert.That(status.requests[0].steps[0].requestedNew,Is.EqualTo(4));
        }
        [Test] public async Task NamedAbsentRequest_DoesNotFallbackToUnrelatedSpearmen()
        {
            Waiting();var provider=new FactsProvider(Answer);var chat=Chat(provider);await chat.SubmitMessageAsync("Why are my Archers waiting?");
            var status=JsonUtility.FromJson<StatusContext>(provider.Last.SerializedContext).tacticalStatus;
            Assert.That(status.selection,Is.EqualTo("None"));
        }
        [Test] public async Task FailedProvider_StillDisplaysAvailableGroundedStatus()
        {
            Waiting();var provider=new FactsProvider(Answer){Failure=new System.IO.IOException("fixture")};var chat=Chat(provider);
            await chat.SubmitMessageAsync("Why are my Spearmen waiting?");
            Assert.That(chat.Conversation.Memory.ToJson().ToLowerInvariant(),Does.Contain("wood"));
            Assert.That(manager.Goals.Count,Is.EqualTo(1));Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }
        [Test] public async Task TellUnitsToAttack_IsNotMistakenForTellMeInformation()
        {
            var provider=new FactsProvider("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"AttackTarget\",\"unitSelector\":\"Archer\",\"count\":1,\"location\":\"VisibleEnemy\"}]}");
            var chat=Chat(provider);await chat.SubmitMessageAsync("Can you tell my archers to attack?");
            Assert.That(provider.Last.IsReadOnlyQuestion,Is.False);
        }
        [Test] public async Task GenericEconomicWhy_IsNotReplacedWithUnrelatedArmyStatus()
        {
            Waiting();var provider=new FactsProvider("{\"outcome\":\"Answer\",\"message\":\"More economic evidence is needed.\"}");var chat=Chat(provider);
            await chat.SubmitMessageAsync("Why is my gold low?");
            Assert.That(provider.Last.SerializedContext,Does.Not.Contain("tacticalStatus"));
            Assert.That(chat.Conversation.Memory.ToJson(),Does.Contain("More economic evidence"));
        }
        [Test] public async Task CancellationReason_IsNotMisrepresentedAsAPlannerDecision()
        {
            var goal=Waiting();manager.CancelGoal(goal.GoalId);
            var chat=Chat(new FactsProvider(Answer));await chat.SubmitMessageAsync("Why did my Spearman request stop?");
            Assert.That(chat.Conversation.Memory.ToJson(),Does.Not.Contain("Last planning check: Cancelled"));
        }

        [Serializable] private class StatusContext { public StatusDto tacticalStatus; }
        [Serializable] private class StatusDto { public string selection; public RequestDto[] requests; }
        [Serializable] private class RequestDto { public StepDto[] steps; }
        [Serializable] private class StepDto
        { public int requestedNew,produced,queued,producerReady,producerUnfinished;public string reason,state;public DeficitDto deficits; }
        [Serializable] private class DeficitDto { public int Wood; }

        private StatusDto Observe(string query,out string json)
        {
            var snapshot=CommanderTacticalStatusProjection.Observe(sim,manager,query);
            json=new CommanderSemanticProviderRequest(query,new CommanderContextBuilder().Build(sim,manager),
                tacticalStatus:snapshot,readOnlyQuestion:true).SerializedContext;
            return JsonUtility.FromJson<StatusContext>(json).tacticalStatus;
        }

        [TestCase(false)][TestCase(true)]
        public void HiddenEnemyBuilding_ReportsVisibilityNotHiddenDeathOrRepairState(bool generic)
        {
            var enemy=sim.CreateBuilding(1,BuildingType.House,x+14,z,false,true);
            sim.FogOfWar.SetVisible(0,enemy.OriginTileX,enemy.OriginTileZ);
            var intent=new CapabilityActionIntent(0,CommanderCapabilityActionType.AttackTarget,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military),new CommanderLocationSelector(CommanderLocationSelectorKind.VisibleEnemy),
                targetSelector:generic?(CommanderTargetSelector?)null:new CommanderTargetSelector(BuildingType.House));
            var goal=manager.SubmitCapabilityAction(intent);
            if(generic){goal.IssuedCommand=new AttackBuildingCommand(0,Array.Empty<int>(),enemy.Id);goal.CommandIssued=true;}
            else goal.TargetBinding=new CommanderTargetBinding(sim,enemy);
            sim.FogOfWar.DemoteAllVisible(0);enemy.CurrentHealth=0;
            goal.SetStatus(CommanderGoalStatus.Completed,"The visible enemy building target is no longer present.");
            var data=Observe("Why is my attack request waiting?",out var json);
            Assert.That(data.requests[0].steps[0].state,Is.EqualTo("TargetUnavailable"));
            Assert.That(json,Does.Not.Contain("no longer present"));
        }

        [Test] public void ExplicitCount_SelectsTheMatchingNamedRequestRatherThanAmbiguousSibling()
        {
            Waiting();manager.SubmitEnsureUnitCount(1,8,newProductionCount:8);
            var data=Observe("Why are my 4 new Spearmen waiting?",out _);
            Assert.That(data.selection,Is.EqualTo("One"));Assert.That(data.requests[0].steps[0].requestedNew,Is.EqualTo(4));
        }

        [TestCase(false)][TestCase(true)]
        public void HiddenEnemyUnit_ReportsVisibilityNotHiddenDeathViaTypedOrGenericTarget(bool generic)
        {
            var enemy=sim.UnitRegistry.CreateUnit(1,sim.MapData.TileToWorldFixed(x+14,z),Fixed32.One,Fixed32.One,Fixed32.One);
            enemy.UnitType=2;enemy.CurrentHealth=enemy.MaxHealth=777;
            var intent=new CapabilityActionIntent(0,CommanderCapabilityActionType.AttackTarget,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military),new CommanderLocationSelector(CommanderLocationSelectorKind.VisibleEnemy),
                targetSelector:generic?(CommanderTargetSelector?)null:new CommanderTargetSelector(2));
            var goal=manager.SubmitCapabilityAction(intent);
            if(generic){goal.IssuedCommand=new AttackUnitCommand(0,Array.Empty<int>(),enemy.Id);goal.CommandIssued=true;}
            else goal.TargetBinding=new CommanderTargetBinding(sim,enemy);
            sim.FogOfWar.DemoteAllVisible(0);enemy.CurrentHealth=0;enemy.State=UnitState.Dead;
            goal.SetStatus(CommanderGoalStatus.Completed,"The visible enemy unit target is no longer alive.");
            var data=Observe("Why is my attack request waiting?",out var json);
            Assert.That(data.requests[0].steps[0].state,Is.EqualTo("TargetUnavailable"));
            Assert.That(json,Does.Not.Contain("no longer alive"));Assert.That(json,Does.Not.Contain("777"));
        }

        [Test] public void SuspendedGoal_IsReportedPausedWithoutResumingOrPlanningIt()
        {
            var goal=Waiting();manager.SuspendGoal(goal.GoalId);
            var data=Observe("Why is my Spearman request waiting?",out _);
            Assert.That(data.requests[0].steps[0].state,Is.EqualTo("Paused"));
            Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }

        [Test] public void ExactProducerUnderConstruction_IsObservedAsUnfinishedNotUnrelatedReadyBarracks()
        {
            var parent=manager.SubmitBuildStructure(BuildingType.Barracks,1);
            var building=sim.CreateBuilding(0,BuildingType.Barracks,x+16,z,true,true);
            parent.AttributedBuildingIds.Add(building.Id);parent.PlacedBuildingId=building.Id;
            var goal=manager.SubmitEnsureUnitCount(1,4,newProductionCount:4);goal.RequiredProducerGoal=parent;goal.SetDependencies(new[]{parent});
            var data=Observe("Why are my Spearmen waiting?",out _);
            Assert.That(data.requests[0].steps[0].producerReady,Is.EqualTo(0));
            Assert.That(data.requests[0].steps[0].producerUnfinished,Is.EqualTo(1));
        }

        [Test] public void BoundedProjection_NeverEmitsEmptyStepShellsWhenRequestsDoNotFit()
        {
            for(int i=0;i<3;i++)
            {
                var goal=manager.SubmitEnsureUnitCount(i%2==0?1:2,20+i,newProductionCount:20+i);
                goal.SetStatus(CommanderGoalStatus.Blocked,new string('x',160));
            }
            var data=Observe("Why is my army not ready?",out var json);
            Assert.That(json.Length,Is.LessThanOrEqualTo(8192));
            Assert.That(data.requests.All(r=>r.steps!=null&&r.steps.Length>0),Is.True,
                "Whole requests are omitted instead of label-only shells.");
        }

        [Test] public void ExpiredHumanProtection_ObservationDoesNotPruneAuthorityHistoryOrReservations()
        {
            var goal=Waiting();var worker=sim.UnitRegistry.CreateUnit(0,sim.MapData.TileToWorldFixed(x-5,z),Fixed32.One,Fixed32.One,Fixed32.One);
            worker.UnitType=0;worker.IsVillager=true;worker.CurrentHealth=worker.MaxHealth=100;
            var authority=typeof(CommanderGoalManager).GetField("workerAuthority",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(manager);
            var field=authority.GetType().GetField("humanProtectedUntilTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var history=(Dictionary<int,int>)field.GetValue(authority);history[worker.Id]=-1;
            Assert.That(manager.TryReserveWorker(goal.GoalId,worker.Id,CommanderWorkerReservationType.Builder),Is.True);
            history[worker.Id]=-1;int before=history.Count;var reservation=manager.GetWorkerReservation(worker.Id);
            Observe("Why are my Spearmen waiting?",out _);
            Assert.That(history.Count,Is.EqualTo(before));Assert.That(manager.GetWorkerReservation(worker.Id),Is.EqualTo(reservation));
        }
    }
}
