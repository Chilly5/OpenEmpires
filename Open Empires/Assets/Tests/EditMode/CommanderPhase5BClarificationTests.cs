using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        // Removing amount from the pending state or local count completion must fail this end-to-end contract.
        [TestCase("4")]
        [TestCase("four")]
        [Category("CommanderPhase5B")]
        public async Task ResourceAmountClarification_RetainsObjectiveThroughLocalCountReply(string reply)
        {
            string pending = Draft().Replace("\"workers\":", "\"resourceAmount\":400,\"resourceAmountMode\":\"Stockpile\",\"workers\":");
            using (var f = new ChatFixture(new SequenceSemanticProvider(pending)))
            {
                await f.Chat.SubmitMessageAsync("gather 400 food from sheep with idle villagers");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, "The resource objective must survive worker clarification.");
                Assert.That(Read(f.Chat.PendingClarification.Draft, "ResourceAmount"), Is.EqualTo(400));
                Assert.That(f.Chat.PendingClarification.OriginalText, Does.Contain("400 food"));
                var result = await f.Chat.SubmitMessageAsync(reply);
                Assert.That(result?.Success, Is.True);
                var allocation = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(allocation.Count, Is.EqualTo(4));
                Assert.That(Read(allocation, "ResourceAmount"), Is.EqualTo(400));
                Assert.That(Read(allocation, "ResourceAmountMode").ToString(), Is.EqualTo("Stockpile"));
                Assert.That(allocation.Destination.Resource, Is.EqualTo(ResourceType.Food));
                Assert.That(allocation.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                Assert.That(allocation.Workers.State, Is.EqualTo(CommanderWorkerState.Idle));
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Null);
            }
        }

        [Test]
        [Category("CommanderPhase5B")]
        public async Task ResourceAmountClarification_RejectsProviderDroppingResolvedAmount()
        {
            string pending = Draft().Replace("\"workers\":", "\"resourceAmount\":400,\"resourceAmountMode\":\"AdditionalGathered\",\"workers\":");
            using (var f = new ChatFixture(new SequenceSemanticProvider(pending, Draft())))
            {
                await f.Chat.SubmitMessageAsync("gather 400 additional food from sheep");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("idle workers");
                Assert.That(Read(f.Chat.PendingClarification.Draft, "ResourceAmount"), Is.EqualTo(400));
                Assert.That(Read(f.Chat.PendingClarification.Draft, "ResourceAmountMode").ToString(), Is.EqualTo("AdditionalGathered"));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        [Category("CommanderPhase5B")]
        public void ResourceObjective_DoesNotCompleteWhenWorkersAreAssigned()
        {
            using (var f = new EconomyFixture())
            {
                var source = f.Node(ResourceType.Food, 8);
                var worker = f.Worker(0);
                string node = AllocationNode(new[] { "SelectedCount", "Exact", "1", "Idle", "", "Food", "Any" });
                node = node.Replace("\"workers\":", "\"resourceAmount\":800,\"resourceAmountMode\":\"Stockpile\",\"workers\":");
                var parsed = CommanderSemanticJson.Parse(Request(node));
                Assert.That(parsed.IsValid, Is.True);
                var goal = f.Manager.SubmitWorkerAllocation(parsed.Nodes[0].WorkerAllocation);
                f.Manager.Tick(0);
                f.Simulation.Tick(f.Commands());
                f.Assigned(worker, source);
                f.Manager.Tick(15);
                Assert.That(goal.IsTerminal, Is.False, "An assigned worker is not 800 food.");
                f.Simulation.ResourceManager.GetPlayerResources(0).Food = 800;
                f.Manager.Tick(30);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            }
        }

        [Test]
        [Category("CommanderPhase5B")]
        public async Task ExplicitAmountCorrection_ChangesOnlyAmountAndKeepsWorkerAndSourceCriteria()
        {
            string pending = Draft().Replace("\"workers\":", "\"resourceAmount\":400,\"resourceAmountMode\":\"Stockpile\",\"workers\":");
            using (var f = new ChatFixture(new SequenceSemanticProvider(pending, pending.Replace("400", "500"))))
            {
                await f.Chat.SubmitMessageAsync("gather 400 food from sheep with idle villagers");
                await f.Chat.SubmitMessageAsync("actually 500 food");
                Assert.That(Read(f.Chat.PendingClarification.Draft, "ResourceAmount"), Is.EqualTo(500));
                Assert.That(f.Chat.PendingClarification.Draft.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                await f.Chat.SubmitMessageAsync("four");
                var allocation = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(allocation.Count, Is.EqualTo(4));
                Assert.That(allocation.ResourceAmount, Is.EqualTo(500));
                Assert.That(allocation.Workers.State, Is.EqualTo(CommanderWorkerState.Idle));
            }
        }

        [Test]
        [Category("CommanderPhase5B")]
        public void AdditionalResourceObjective_TracksDepositsDespiteSpendingAndIgnoresRefunds()
        {
            using (var f = new EconomyFixture())
            {
                var source = f.Node(ResourceType.Food, 8);
                var worker = f.Worker(0);
                f.Simulation.ResourceManager.CreditGatheredIncome(0, ResourceType.Food, 10); // Pre-activation income excluded.
                var allocation = new CommanderWorkerAllocation(CommanderWorkerAllocationMode.SelectedCount,
                    CommanderWorkerCountMode.Exact, 1, new CommanderWorkerSelector(CommanderWorkerState.Idle),
                    new CommanderResourceDestination(ResourceType.Food), 40, CommanderResourceAmountMode.AdditionalGathered);
                var goal = f.Manager.SubmitWorkerAllocation(allocation);
                f.Manager.Tick(0); f.Simulation.Tick(f.Commands()); f.Assigned(worker, source);
                f.Simulation.ResourceManager.AddResource(0, ResourceType.Food, 1000); // Refund/bonus is not gathering.
                f.Manager.Tick(15); Assert.That(goal.IsTerminal, Is.False); Assert.That(goal.ResourceProgress, Is.Zero);
                f.Simulation.ResourceManager.CreditGatheredIncome(0, ResourceType.Food, 40);
                f.Simulation.ResourceManager.GetPlayerResources(0).Food = 0; // Spending cannot erase gathered income.
                f.Manager.Tick(30);
                Assert.That(goal.ResourceProgress, Is.EqualTo(40));
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            }
        }

        [Test]
        [Category("CommanderPhase5B")]
        public async Task FutureOrderRequiresExplicitPreviewApproval_AndConversationResetRemovesWatcher()
        {
            string watch = Request("{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":2,\"producer\":\"TownCenter\",\"action\":\"Gather\",\"resource\":\"Wood\"}");
            using (var f = new ChatFixture(new SequenceSemanticProvider(watch)))
            {
                var producer = f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                    f.Economy.Base.x, f.Economy.Base.y, false, true);
                producer.AutoProduceVillagers = false;
                await f.Chat.SubmitMessageAsync("send the next two villagers from my Town Center to wood");
                Assert.That(f.Economy.Manager.Goals, Is.Empty, "Translation alone cannot authorize a future policy.");
                await f.Chat.SubmitMessageAsync("4");
                Assert.That(f.Economy.Manager.Goals, Is.Empty, "A number is not plan approval.");
                await f.Chat.SubmitMessageAsync("send the next two villagers from my Town Center to wood");
                await f.Chat.SubmitMessageAsync("approve plan");
                var goal = f.Economy.Manager.Goals.Single();
                Assert.That(goal.GoalType.ToString(), Is.EqualTo("WatchFutureUnits"));
                f.Chat.ResetConversation();
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled), "Reset must remove the future birth subscription.");
            }
        }

        [TestCase("reach Age 3", "{\"type\":\"ReachAge\",\"targetAge\":\"Castle\"}")]
        [TestCase("send the next two villagers from my Town Center to wood", "{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":2,\"producer\":\"TownCenter\",\"action\":\"Gather\",\"resource\":\"Wood\"}")]
        [Category("CommanderPhase5B")]
        public async Task NewIndependentCommand_SupersedesPendingWorkerQuestion(string message, string node)
        {
            using (var f = new ChatFixture(new SequenceSemanticProvider(Draft(), Request(node))))
            {
                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true).AutoProduceVillagers = false;
                await f.Chat.SubmitMessageAsync("gather food");
                await f.Chat.SubmitMessageAsync(message);
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Provider.LastRequest.SerializedPendingClarification, Is.Empty, "New commands must receive a new request scope.");
                Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>(), Is.Empty);
            }
        }

        [Test]
        [Category("CommanderPhase5B")]
        public async Task WorkerCountCorrection_WhileDestinationPending_ChangesOnlyExplicitCount()
        {
            string pending = Draft(destination: false).Replace("\"workers\":", "\"count\":4,\"workers\":")
                .Replace("[\"Count\",\"Destination\"]", "[\"Destination\"]");
            string corrected = Request(AllocationNode(new[] { "SelectedCount", "Exact", "5", "Idle", "", "Wood", "Any" }));
            using (var f = new ChatFixture(new SequenceSemanticProvider(pending, corrected)))
            {
                await f.Chat.SubmitMessageAsync("assign four idle villagers");
                await f.Chat.SubmitMessageAsync("actually five villagers to wood");
                var goal = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single();
                Assert.That(goal.Allocation.Count, Is.EqualTo(5));
                Assert.That(goal.Allocation.Workers.State, Is.EqualTo(CommanderWorkerState.Idle));
                Assert.That(goal.Allocation.Destination.Resource, Is.EqualTo(ResourceType.Wood));
            }
        }
    }
}
