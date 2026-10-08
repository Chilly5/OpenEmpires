using System;
using System.Collections;
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
    public sealed class CommanderGrandFixRetentionTests
    {
        private SimulationConfig config;private GameSimulation sim;private CommanderGoalManager manager;private StrategicPlanner planner;
        [SetUp]public void SetUp()
        {config=ScriptableObject.CreateInstance<SimulationConfig>();sim=new GameSimulation(config,1,new[]{0},Array.Empty<int>());manager=new CommanderGoalManager(sim,0);planner=new StrategicPlanner(manager,_=>0);}
        [TearDown]public void TearDown(){planner.Dispose();manager.Dispose();UnityEngine.Object.DestroyImmediate(config);}
        private static int Store(object owner,string field)
        {object value=owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);return (int)value.GetType().GetProperty("Count").GetValue(value);}
        private StrategicContext Context()=>new StrategicContextBuilder().Build(new CommanderContextBuilder().Build(sim,manager),planner);
        private StrategicAIApprovalBridge Bridge(IStrategicAIInterpreter interpreter=null)=>new StrategicAIApprovalBridge(interpreter??new RejectedInterpreter(),planner.IntentIds,Context);
        private static void Prune(CommanderWorkerAuthority authority,int tick=0)
        {var method=typeof(CommanderWorkerAuthority).GetMethod("PruneUnavailableWorkers");method.Invoke(authority,method.GetParameters().Length==0?null:new object[]{tick});}
        private UnitData Unit(bool villager=true)
        {var unit=sim.UnitRegistry.CreateUnit(0,FixedVector3.Zero,Fixed32.One,Fixed32.One,Fixed32.One);unit.IsVillager=villager;unit.UnitType=villager?0:1;unit.CurrentHealth=unit.MaxHealth=100;unit.State=UnitState.Idle;return unit;}

        [Test]public void InvalidOwnedRequestConstruction_DoesNotLeakUnboundIdentity()
        {
            Assert.Throws<ArgumentNullException>(()=>new StrategicAIRequest("fixture",null,planner.IntentIds));
            Assert.That(Store(planner.IntentIds,"owners"),Is.Zero);
        }
        [Test]public void RejectedIntentChurn_DoesNotOutliveBoundedPlannerHistory()
        {
            for(int i=0;i<128;i++)
            {var intent=planner.CreateIntent((StrategicObjectiveType)999);Assert.That(planner.SubmitIntent(intent).CreatedPlan,Is.False);}
            Assert.That(planner.Intents.Count,Is.LessThanOrEqualTo(100));Assert.That(Store(planner.IntentIds,"owners"),Is.Zero);
        }
        [Test]public async Task FailedTranslationChurn_ReleasesOwnedRequestContexts()
        {
            using var bridge=Bridge();for(int i=0;i<64;i++)Assert.That((await bridge.TranslateAsync("expand economy")).Success,Is.False);
            Assert.That(Store(planner.IntentIds,"owners"),Is.Zero);
        }
        [Test]public void DismissedPendingRecommendations_RetireTheirClaims()
        {
            using var bridge=Bridge();
            for(int i=0;i<64;i++)
            {
                var result=bridge.StageValidatedSemanticObjective(StrategicObjectiveType.EconomicExpansion,0,bridge.Generation);
                Assert.That(result.Success,Is.True);var pending=result.Intent;bridge.ClearPending();
                Assert.That(planner.IntentIds.Owns(pending),Is.False);
            }
            Assert.That(Store(planner.IntentIds,"owners"),Is.Zero);
        }
        [Test]public void ConfirmationTransfer_PreservesNewOwnerButDismissesOldObject()
        {
            using var bridge=Bridge();var result=bridge.StageValidatedSemanticObjective(StrategicObjectiveType.EconomicExpansion,0,bridge.Generation);
            var confirmed=bridge.Confirm(result.Intent.IntentId);Assert.That(confirmed,Is.Not.Null);
            Assert.That(planner.IntentIds.Owns(result.Intent),Is.False);Assert.That(planner.IntentIds.Owns(confirmed),Is.True);
        }
        [Test]public async Task ResetDuringIgnoringTranslation_ReleasesClaimAndLateResultCannotBind()
        {
            var interpreter=new DeferredInterpreter();using var bridge=Bridge(interpreter);
            var task=bridge.TranslateAsync("expand economy");Assert.That(Store(planner.IntentIds,"owners"),Is.EqualTo(1));
            bridge.Reset();await task;Assert.That(Store(planner.IntentIds,"owners"),Is.Zero);
            var late=new StrategicIntent(interpreter.Request.IntentId,0,StrategicObjectiveType.EconomicExpansion,interpreter.Request.Context.SnapshotTick);
            Assert.That(interpreter.Request.BindIntent(late),Is.False);
            interpreter.Pending.SetResult(StrategicAIProviderResult.Rejected("fixture late result"));
        }
        [Test]public void RetiredIdentity_CannotRegisterSameOrReplacementObjectAgain()
        {
            var intent=planner.CreateIntent(StrategicObjectiveType.EconomicExpansion);
            var method=typeof(StrategicIntentIdProvider).GetMethod("Retire",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null,"Owned identity retirement is missing.");
            method.Invoke(planner.IntentIds,new object[]{intent.IntentId,intent});
            Assert.That(planner.IntentIds.TryRegister(intent),Is.False);
            Assert.That(planner.IntentIds.TryRegister(new StrategicIntent(intent.IntentId,0,intent.ObjectiveType,0)),Is.False);
            Assert.That(planner.IntentIds.Owns(intent),Is.False);Assert.That(planner.CanCommitIntent(intent,out _),Is.False);
        }
        [Test]public void DisposedPlanner_ReleasesIdentityClaimsAndHistory()
        {
            for(int i=0;i<16;i++)planner.CreateIntent(StrategicObjectiveType.EconomicExpansion);
            var ids=planner.IntentIds;planner.Dispose();
            Assert.That(Store(ids,"owners"),Is.Zero);Assert.That(planner.Intents,Is.Empty);
            Assert.Throws<ObjectDisposedException>(()=>ids.Allocate());
        }
        [Test]public void DeadAndRemovedUnits_PruneAllFiveAuthorityStores()
        {
            var authority=new CommanderWorkerAuthority(sim,0);var human=Unit();var controlled=Unit();var reserved=Unit();var military=Unit(false);
            authority.ObserveEnqueuedCommand(new MoveCommand(0,new[]{human.Id},FixedVector3.Zero),CommandEnqueueSource.Human,0);
            authority.ObserveEnqueuedCommand(new GatherCommand(0,new[]{controlled.Id},0),CommandEnqueueSource.Commander,0);
            Assert.That(authority.TryReserve(reserved.Id,1,CommanderWorkerReservationType.Gatherer,0),Is.True);
            var goal=new EnsureUnitCountGoal(0,1,1){GoalId=2};Assert.That(authority.TryReserveCommand(goal,new MoveCommand(0,new[]{military.Id},FixedVector3.Zero),0),Is.True);
            foreach(var unit in new[]{human,controlled,reserved,military})sim.UnitRegistry.RemoveUnit(unit.Id);
            Prune(authority);
            foreach(string field in new[]{"humanProtectedUntilTick","commanderControlledWorkers","commanderGatherUntilTick","reservations","commanderUnitReservations"})Assert.That(Store(authority,field),Is.Zero,field);
        }
        [Test]public void BulkExpiry_PrunesTemporaryLeasesButKeepsLiveCommanderControl()
        {
            var authority=new CommanderWorkerAuthority(sim,0);var human=Unit();var controlled=Unit();
            authority.ObserveEnqueuedCommand(new MoveCommand(0,new[]{human.Id},FixedVector3.Zero),CommandEnqueueSource.Human,0);
            authority.ObserveEnqueuedCommand(new GatherCommand(0,new[]{controlled.Id},0),CommandEnqueueSource.Commander,0);
            Prune(authority,1000);
            Assert.That(Store(authority,"humanProtectedUntilTick"),Is.Zero);Assert.That(Store(authority,"commanderGatherUntilTick"),Is.Zero);
            Assert.That(authority.IsCommanderControlled(controlled.Id),Is.True);
        }
        [Test]public void GarrisonedLiveIdentity_KeepsProtectionWhileReservationsRelease()
        {
            var authority=new CommanderWorkerAuthority(sim,0);var human=Unit();
            authority.ObserveEnqueuedCommand(new MoveCommand(0,new[]{human.Id},FixedVector3.Zero),CommandEnqueueSource.Human,0);
            sim.UnitRegistry.GarrisonUnit(human.Id);Prune(authority,1);
            Assert.That(authority.ObserveHumanProtection(human.Id,1),Is.True);Assert.That(authority.GetReservation(human.Id),Is.Null);
            sim.UnitRegistry.RestoreUnit(human.Id);Assert.That(authority.IsHumanProtected(human.Id,1),Is.True);
        }
        [Test]public void ConstructorFailureInTrustedFactory_ReleasesUnboundSlot()
        {
            Assert.Throws<ArgumentException>(()=>planner.CreateIntent(StrategicObjectiveType.EconomicExpansion,new Dictionary<string,string>{{" ","fixture"}}));
            Assert.That(Store(planner.IntentIds,"owners"),Is.Zero);
        }
        [TestCase(false)][TestCase(true)]public void DeniedPipelineDecisions_RetireIncomingOwnedClaims(bool approved)
        {
            using var pipeline=new StrategicPipeline(planner,()=>new CommanderContextBuilder().Build(sim,manager),evaluator:new EmptyEvaluator(),decisionPolicy:new DenyPolicy());
            for(int i=0;i<32;i++)
            {
                var intent=planner.CreateIntent(StrategicObjectiveType.EconomicExpansion);
                if(approved)pipeline.EvaluateApprovedIntentNow(StrategicApprovalResult.Accept(intent,StrategicPlanAuthority.Normal));
                else pipeline.EvaluatePlayerIntentNow(intent);
                Assert.That(planner.IntentIds.Owns(intent),Is.False);
            }
            Assert.That(Store(planner.IntentIds,"owners"),Is.Zero);Assert.That(planner.ActivePlans,Is.Empty);
        }
        [Test]public void GarrisonedReservedWorker_RetainsClaimUntilGoalRelease()
        {
            var authority=new CommanderWorkerAuthority(sim,0);var worker=Unit();
            Assert.That(authority.TryReserve(worker.Id,7,CommanderWorkerReservationType.Builder,0),Is.True);
            sim.UnitRegistry.GarrisonUnit(worker.Id);Prune(authority,1);
            Assert.That(authority.GetReservation(worker.Id)?.GoalId,Is.EqualTo(7));
            sim.UnitRegistry.RestoreUnit(worker.Id);
            Assert.That(authority.TryReserve(worker.Id,8,CommanderWorkerReservationType.Gatherer,1),Is.False);
            authority.ReleaseGoal(7);Assert.That(authority.TryReserve(worker.Id,8,CommanderWorkerReservationType.Gatherer,1),Is.True);
        }
        [Test]public void DisposedGoalManager_ClearsHistoryAndWorkerControlStores()
        {
            var worker=Unit();var goal=manager.SubmitEnsureUnitCount(1,4);
            sim.CommandBuffer.EnqueueCommand(new GatherCommand(0,new[]{worker.Id},0),CommandEnqueueSource.Commander);
            var authority=(CommanderWorkerAuthority)typeof(CommanderGoalManager).GetField("workerAuthority",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager);
            Assert.That(authority.IsCommanderControlled(worker.Id),Is.True);manager.Dispose();
            Assert.That(manager.Goals,Is.Empty);Assert.That(Store(authority,"commanderControlledWorkers"),Is.Zero);Assert.That(Store(authority,"commanderGatherUntilTick"),Is.Zero);
            Assert.That(goal.RuntimeOwner,Is.Null);
        }
        private sealed class EmptyEvaluator:IStrategicEvaluator
        {public IReadOnlyList<StrategicRecommendation> Evaluate(StrategicContext context)=>Array.Empty<StrategicRecommendation>();}
        private sealed class DenyPolicy:IStrategicDecisionPolicy,IStrategicApprovedDecisionPolicy
        {
            public StrategicIntent SelectIntent(StrategicContext context,IReadOnlyList<StrategicRecommendation> recommendations)=>null;
            public StrategicIntent SelectIntent(StrategicContext context,IReadOnlyList<StrategicRecommendation> recommendations,StrategicIntent player)=>null;
            public StrategicDecisionResult Decide(StrategicContext context,IReadOnlyList<StrategicRecommendation> recommendations)=>StrategicDecisionResult.Rejected(context.SnapshotTick,"fixture denial");
            public StrategicDecisionResult Decide(StrategicContext context,IReadOnlyList<StrategicRecommendation> recommendations,StrategicIntent player)=>Decide(context,recommendations);
            public StrategicDecisionResult DecideApproved(StrategicContext context,IReadOnlyList<StrategicRecommendation> recommendations,StrategicApprovalResult approval)=>Decide(context,recommendations);
        }
        private sealed class RejectedInterpreter:IStrategicAIInterpreter
        {public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(StrategicAIRequest request,CancellationToken token)=>Task.FromResult(StrategicAIProviderResult.Rejected("fixture rejection"));}
        private sealed class DeferredInterpreter:IStrategicAIInterpreter
        {public StrategicAIRequest Request;public readonly TaskCompletionSource<StrategicAIProviderResult> Pending=new TaskCompletionSource<StrategicAIProviderResult>();public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(StrategicAIRequest request,CancellationToken token){Request=request;return Pending.Task;}}
    }
}
