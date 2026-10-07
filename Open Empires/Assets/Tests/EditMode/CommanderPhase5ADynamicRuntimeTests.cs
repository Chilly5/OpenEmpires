using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public sealed class CommanderPhase5ADynamicRuntimeTests
    {
        [Test]
        public void OptInRequestTrace_CorrelatesApprovalAdmissionAndDispatchWithoutOriginalText()
        {
            using var f = new Fixture(1);
            var logs = new System.Collections.Generic.List<string>();
            void Capture(string message, string stack, LogType type)
            { if (message.StartsWith("[Commander] request=", StringComparison.Ordinal)) logs.Add(message); }
            Application.logMessageReceived += Capture;
            try
            {
                f.Manager.RequestTracingEnabled = true;
                var ticket = f.Manager.BeginSemanticRequest("private original text", 1, () => true);
                var parsed = CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Farms.Replace("\"count\":4", "\"count\":1"));
                var candidate = f.Manager.PrepareActionPlan(parsed, ticket.OriginalInput, 1, requestTicket: ticket);
                f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
                f.Manager.Tick(0);
                Assert.That(logs.Any(l => l.Contains("stage=approved")), Is.True);
                Assert.That(logs.Any(l => l.Contains("stage=goal-admitted")), Is.True);
                Assert.That(logs.Any(l => l.Contains("dispatch=PlaceBuildingCommand")), Is.True);
                Assert.That(logs.All(l => l.Contains("request=" + ticket.Id + ";")), Is.True);
                Assert.That(logs.Any(l => l.Contains(ticket.OriginalInput)), Is.False);
            }
            finally { Application.logMessageReceived -= Capture; }
        }

        [Test]
        public void CanonicalUnitSelection_UsesAvailableCivilizationIdsAndRejectsReplacedBaseId()
        {
            using var f = new Fixture(1);
            f.Sim.SetPlayerCivilizations(new[] { Civilization.HolyRomanEmpire });
            var replacement = f.Sim.UnitRegistry.CreateUnit(0, f.Workers[0].SimPosition,
                Fixed32.One, Fixed32.One, Fixed32.One);
            replacement.UnitType = 12;
            replacement.CurrentHealth = replacement.MaxHealth = 100;
            replacement.State = UnitState.Idle;
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"owned\",\"mechanic\":\"select-units\",\"parameters\":{\"unit\":\"unit:1\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"build\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"owned\"]}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.Throws<ArgumentException>(() => f.Manager.PrepareActionPlan(parsed,
                "select my spear and make a farm", 1), "The replaced base ID is unavailable in canonical HRE data.");
            parsed = CommanderSemanticJson.Parse(parsedJsonForReplacement());
            var candidate = f.Manager.PrepareActionPlan(parsed, "select my Landsknecht and make a farm", 1);
            Assert.That(f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1)).Count, Is.EqualTo(1));
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty,
                "Selection is preflight-only and must not issue a new dynamic military action.");
            var context = new CommanderContextBuilder().Build(f.Sim, f.Manager);
            Assert.That(context.CanonicalUnitIds, Does.Contain("unit:12"));
            var production = CommanderSemanticJson.Parse(OneNewSpear.Replace("unit:1", "unit:12"));
            var productionCandidate = f.Manager.PrepareActionPlan(production, "one new Landsknecht", 2);
            Assert.That(productionCandidate.Preview, Does.Contain("Landsknecht"));
            Assert.That(productionCandidate.Preview, Does.Not.Contain("Spearman"),
                "The approved effect label must describe the canonical replacement, not its internal base request.");

            string parsedJsonForReplacement() => "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"owned\",\"mechanic\":\"select-units\",\"parameters\":{\"unit\":\"unit:12\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"build\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"owned\"]}]}";
        }

        [Test]
        public void RestrictedAgePreparation_PreservesGoldWorkersAddedAfterInitialBlocker()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var sheep = f.Sim.UnitRegistry.GetAllUnits().Single(u => u.IsSheep);
            sheep.CurrentHealth = 0;
            sheep.State = UnitState.Dead;
            var resources = f.Sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Gold = 0;
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"ReachAge\",\"targetAge\":\"Next\",\"constraints\":["
                + "{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":\"Sheep\"}]}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(
                f.Manager.PrepareActionPlan(parsed, "next age using food only from sheep", 1), 1));
            f.Manager.Tick(0);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            var gold = f.Sim.MapData.AddResourceNode(ResourceType.Gold,
                f.Sim.MapData.TileToWorldFixed(x - 10, z + 2), 1000);
            f.Workers[0].TargetResourceNodeId = gold.Id;
            f.Workers[0].State = UnitState.Gathering;
            sheep.CurrentHealth = 100;
            sheep.State = UnitState.Idle;
            f.Manager.Tick(180);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty,
                "Cached Food preparation must not reclaim the newly needed Gold worker.");
        }

        [Test]
        public void PreparationSourceDtoAndHumanTakeover_PreserveRestrictionWithoutReclaim()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            f.Sim.ResourceManager.GetPlayerResources(0).Food = 0;
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":1,\"constraints\":["
                + "{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":\"Sheep\"}]}]}");
            Assert.That(parsed.IsValid, Is.True);
            var context = new CommanderContextBuilder().Build(f.Sim, f.Manager);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], context, out var intent, out _), Is.True);
            var dto = CommanderIntentDtoCodec.FromIntent(intent);
            Assert.That(dto.constraints[0].sourceKind, Is.EqualTo("Sheep"));
            var roundTrip = CommanderIntentDtoCodec.InterpretJson(CommanderIntentDtoCodec.ToJson(dto), context);
            Assert.That(roundTrip.Success, Is.True);
            Assert.That(((ResourceSourceConstraint)roundTrip.Intent.Constraints[0]).SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
            var goal = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(
                f.Manager.PrepareActionPlan(parsed, "one spear with food only from sheep", 1), 1))[0];
            f.Manager.Tick(0);
            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<SlaughterSheepCommand>().Count(), Is.EqualTo(1));
            f.Sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { f.Workers[0].Id }));
            f.Sim.Tick();
            f.Manager.Tick(1005);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
        }

        [TestCase("{\"type\":\"ResourceSource\",\"resource\":\"Wood\",\"sourceKind\":\"Sheep\"}")]
        [TestCase("{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":0}")]
        [TestCase("{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":\"Sheep\",\"approved\":true}")]
        public void MalformedPreparationSource_RejectsWholePlan(string constraint)
        {
            using var f = new Fixture(1);
            string json = OneNewSpear.Replace("\"version\":1,", "\"version\":1,\"constraints\":[" + constraint + "],");
            Assert.That(CommanderSemanticJson.Parse(json).IsValid, Is.False);
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProductionFoodSourceRestriction_UsesOnlySheepOrReportsBlocker(bool sheepAvailable)
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            f.Sim.ResourceManager.GetPlayerResources(0).Food = 0;
            f.Sim.MapData.AddResourceNode(ResourceType.Food, f.Sim.MapData.TileToWorldFixed(x - 11, z + 2), 1000);
            foreach (var sheep in f.Sim.UnitRegistry.GetAllUnits().Where(u => u.IsSheep))
                if (!sheepAvailable) { sheep.CurrentHealth = 0; sheep.State = UnitState.Dead; }
            string json = OneNewSpear.Replace("\"version\":1,", "\"version\":1,\"constraints\":["
                + "{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":\"Sheep\"}],");
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "one new spear using food only from sheep", 1);
            Assert.That(candidate.Preview, Does.Contain("Sheep"));
            var goal = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            var commands = f.Sim.CommandBuffer.FlushCommands();
            Assert.That(commands.OfType<GatherCommand>().All(c => c.SourceKind == ResourceSourceKind.Sheep), Is.True);
            Assert.That(commands.OfType<TrainUnitCommand>(), Is.Empty);
            if (sheepAvailable)
            {
                var command = commands.OfType<SlaughterSheepCommand>().Single();
                Assert.That(command.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                Assert.That(command.VillagerIds, Is.EqualTo(new[] { f.Workers[0].Id }));
            }
            else
            {
                Assert.That(commands, Is.Empty, "Visible berries cannot silently substitute for the requested food source.");
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            }
        }

        private const string OneNewSpear = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"spear\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:1\",\"count\":1,\"quantityMode\":\"New\"},\"inputs\":{},\"dependsOn\":[]}]}";

        [Test]
        public void AmbiguousRelayTrainingOrigin_BlocksReplacementOfPotentiallyExecutedExactOrder()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var barracks = f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(OneNewSpear), "one new spear", 1);
            var goal = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            ICommand own = f.Sim.CommandBuffer.FlushCommands().Single(c => c is TrainUnitCommand);
            ICommand human = new TrainUnitCommand(0, barracks.Id, 1);
            var ledger = new CommanderCommandOriginLedger();
            var originals = new[] { own, human };
            var replayed = new ICommand[] { new TrainUnitCommand(0, barracks.Id, 1), new TrainUnitCommand(0, barracks.Id, 1), new NoopCommand(0) };
            ledger.Record(0, 0, originals, originals, f.Sim);
            var map = ledger.Consume(0, 0, replayed, f.Sim);
            Assert.That(map, Is.Empty);
            f.Sim.Tick(replayed.ToList(), map);
            Assert.That(barracks.TrainingQueue.Count, Is.EqualTo(2), "Attribution rejection cannot suppress ordinary gameplay.");
            f.Manager.Tick(15);
            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<TrainUnitCommand>(), Is.Empty,
                "Unknown attribution is not proof of rejection and cannot authorize extra production.");
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
        }

        [Test]
        public void CancelRequest_ReleasesItsPendingTrainingOriginsOnly()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var barracks = f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(OneNewSpear), "one new spear", 1);
            f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
            f.Manager.Tick(0);
            ICommand own = f.Sim.CommandBuffer.FlushCommands().Single(c => c is TrainUnitCommand);
            ICommand unrelated = new TrainUnitCommand(0, barracks.Id, 1);
            f.Sim.RegisterTrainingOrigin(unrelated, new object(), () => true);
            Assert.That(f.Sim.HasPendingTrainingOrigin(own), Is.True);
            Assert.That(f.Manager.CancelActionPlan(candidate), Is.True);
            Assert.That(f.Sim.HasPendingTrainingOrigin(own), Is.False);
            Assert.That(f.Sim.HasPendingTrainingOrigin(unrelated), Is.True);
            f.Sim.DiscardTrainingOrigin(unrelated);
        }

        [Test]
        public void UnsupportedNearGap_RejectsBeforeOfferingApproval()
        {
            using var f = new Fixture(1);
            var parsed = CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Mill.Replace("\"clearGapTiles\":1", "\"clearGapTiles\":2"));
            Assert.That(parsed.IsValid, Is.True, "Structural data does not yet establish a supported placement mechanic.");
            Assert.Throws<ArgumentException>(() => f.Manager.PrepareActionPlan(parsed, "near with two clear tiles", 1));
            Assert.That(f.Manager.Goals, Is.Empty);
        }

        [Test]
        public void CanonicalCostMutation_FreshProjectionAndOrdinaryPlacementUseExistingVirtualGameData()
        {
            using var f = new Fixture(1, mutableCosts: true);
            var mutable = (MutableCostConfig)f.Config;
            mutable.BarracksCost = 333;
            var oldRecord = GameKnowledgeCatalog.Build(f.Sim).FindBuilding("building:Barracks");
            Assert.That(oldRecord.Cost.Wood, Is.EqualTo(333));
            mutable.BarracksCost = 444;
            var freshRecord = GameKnowledgeCatalog.Build(f.Sim).FindBuilding("building:Barracks");
            Assert.That(freshRecord.Cost.Wood, Is.EqualTo(444));
            Assert.That(oldRecord.Cost.Wood, Is.EqualTo(333), "Detached observations must not alias later gameplay mutation.");
            var json = CommanderPhase5ADynamicPlanTests.Farms.Replace("building:Farm", "building:Barracks")
                .Replace("\"count\":4", "\"count\":1");
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(json), "one barracks", 1);
            f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
            int before = f.Sim.ResourceManager.GetPlayerResources(0).Wood;
            f.Manager.Tick(0);
            f.Sim.Tick();
            Assert.That(f.Sim.BuildingRegistry.GetAllBuildings().Count(b => b.Type == BuildingType.Barracks), Is.EqualTo(1));
            Assert.That(f.Sim.ResourceManager.GetPlayerResources(0).Wood, Is.EqualTo(before - 444));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DynamicGlobalNoConstruction_ControlsProductionPrerequisites(bool producerExists)
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            if (producerExists) f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"constraints\":["
                + "{\"type\":\"NoConstruction\"},{\"type\":\"MaximumQueue\",\"amount\":1}],\"nodes\":["
                + "{\"id\":\"spears\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:1\",\"count\":1,\"quantityMode\":\"New\"},"
                + "\"inputs\":{},\"dependsOn\":[]}]}";
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "one new spear without any construction", 1);
            Assert.That(candidate.Preview, Does.Contain("construction forbidden"));
            var goal = (EnsureUnitCountGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            Assert.That(goal.ConstructionForbidden, Is.True);
            Assert.That(goal.MaxQueueDepth, Is.EqualTo(1));
            f.Manager.Tick(0);
            var commands = f.Sim.CommandBuffer.FlushCommands();
            Assert.That(commands.OfType<PlaceBuildingCommand>(), Is.Empty);
            Assert.That(commands.OfType<ConstructBuildingCommand>(), Is.Empty);
            Assert.That(commands.OfType<TrainUnitCommand>().Count(), Is.EqualTo(producerExists ? 1 : 0));
        }

        [Test]
        public void DynamicGlobalNoConstruction_RejectsContradictoryBuildBeforeAdmission()
        {
            using var f = new Fixture(1);
            string json = CommanderPhase5ADynamicPlanTests.Farms.Replace("\"version\":1,",
                "\"version\":1,\"constraints\":[{\"type\":\"NoConstruction\"}],");
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True);
            Assert.Throws<ArgumentException>(() => f.Manager.PrepareActionPlan(parsed, "no construction", 1));
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase("{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}")]
        [TestCase("{\"type\":\"ProtectedResource\",\"resource\":\"Wood\",\"amount\":1}")]
        public void DynamicGlobalWorkerConstraints_RejectEntireUnsafeSharedSelection(string constraint)
        {
            using var f = new Fixture(3);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var wood = f.Sim.MapData.AddResourceNode(ResourceType.Wood, f.Sim.MapData.TileToWorldFixed(x - 12, z), 1000);
            for (int i = 0; i < 2; i++)
            {
                f.Workers[i].State = UnitState.Gathering;
                f.Workers[i].TargetResourceNodeId = wood.Id;
            }
            string json = CommanderPhase5ADynamicCompilerTests.SharedWorkers
                .Replace("\"version\":1,", "\"version\":1,\"constraints\":[" + constraint + "],")
                .Replace("\"state\":\"Idle\"", "\"state\":\"Any\"");
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True);
            var plan = f.Manager.ApproveActionPlan(f.Manager.PrepareActionPlan(parsed, "shared workers with protection", 1), 1);
            Assert.Throws<InvalidOperationException>(() => f.Manager.SubmitSemanticGraph(plan));
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(f.Workers.All(w => !f.Manager.GetWorkerReservation(w.Id).HasValue), Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ResourceLocation_BuildsDistinctExactMillsNearTheFrozenVisibleBerries(int count)
        {
            using var f = new Fixture(1);
            foreach (var resource in f.Sim.MapData.GetAllResourceNodes()) resource.RemainingAmount = 0;
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var berries = f.Sim.MapData.AddResourceNode(ResourceType.Food,
                f.Sim.MapData.TileToWorldFixed(x + 15, z + 5), 1000);
            string json = CommanderPhase5ADynamicPlanTests.Mill.Replace(
                "\"building\":\"building:Mill\",\"count\":1", "\"building\":\"building:Mill\",\"count\":" + count);
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(json),
                "build Mills near the visible berries", 1);
            var build = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            // Native Mill placement queues a gather waypoint. Preserve that behavior
            // and allow the existing 150-tick blocked retry to observe its completion.
            for (int step = 0; step < 20; step++)
            {
                f.Manager.Tick(step * 15);
                f.Sim.Tick();
                foreach (var mill in f.Sim.BuildingRegistry.GetAllBuildings().Where(b =>
                    b.PlayerId == 0 && b.Type == BuildingType.Mill && b.IsUnderConstruction))
                    mill.IsUnderConstruction = false;
                f.Workers[0].State = UnitState.Idle;
            }
            Assert.That(build.Status, Is.EqualTo(CommanderGoalStatus.Completed), build.StatusReason
                + "; worker state=" + f.Workers[0].State + "; queue=" + f.Workers[0].CommandQueue.Count
                + "; reservation=" + f.Manager.GetWorkerReservation(f.Workers[0].Id)?.GoalId
                + "; health=" + f.Workers[0].CurrentHealth + "; villager=" + f.Workers[0].IsVillager
                + "; registry=" + (f.Sim.UnitRegistry.GetUnit(f.Workers[0].Id) != null)
                + "; frozen=" + (build.FrozenWorkerIds?.Count.ToString() ?? "none"));
            Assert.That(build.ResultBuildingIds.Count, Is.EqualTo(count));
            var results = build.ResultBuildingIds.Select(id => f.Sim.BuildingRegistry.GetBuilding(id)).ToArray();
            Assert.That(results.Select(b => new Vector2Int(b.OriginTileX, b.OriginTileZ)).Distinct().Count(), Is.EqualTo(count));
            foreach (var mill in results)
            {
                int dx = Math.Max(berries.TileX - mill.OriginTileX - mill.TileFootprintWidth,
                    mill.OriginTileX - berries.TileX - berries.FootprintWidth);
                int dz = Math.Max(berries.TileZ - mill.OriginTileZ - mill.TileFootprintHeight,
                    mill.OriginTileZ - berries.TileZ - berries.FootprintHeight);
                Assert.That(Math.Max(dx, dz), Is.InRange(0, 3), "The semantic target cannot become an unrelated base location.");
            }
        }

        [Test]
        public void SharedWorkerHumanTakeover_IsStickyAndDoesNotReplaceTheBoundBuilder()
        {
            using var f = new Fixture(4);
            var candidate = f.Manager.PrepareActionPlan(
                CommanderSemanticJson.Parse(CommanderPhase5ADynamicCompilerTests.SharedWorkers), "shared worker request", 1);
            var goals = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
            f.Sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { f.Workers[2].Id }));
            f.Sim.Tick();
            for (int step = 0; step < 3; step++)
            {
                f.Manager.Tick(1005 + step * 15); // Aligned planning ticks beyond temporary protection.
                f.Sim.Tick(); // Earlier runnable allocation yields before the builder is checked.
            }
            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<PlaceBuildingCommand>(), Is.Empty);
            Assert.That(goals[1].Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[3].Id).HasValue, Is.False);
        }

        [Test]
        public void SharedThreeWorkerSelection_UnavailableFullSetRejectsWithoutAnyAdmission()
        {
            using var f = new Fixture(2);
            var candidate = f.Manager.PrepareActionPlan(
                CommanderSemanticJson.Parse(CommanderPhase5ADynamicCompilerTests.SharedWorkers),
                "take three idle workers, two on sheep and the third on a Mill", 1);
            var plan = f.Manager.ApproveActionPlan(candidate, 1);
            Assert.Throws<InvalidOperationException>(() => f.Manager.SubmitSemanticGraph(plan));
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(f.Workers.All(w => !f.Manager.GetWorkerReservation(w.Id).HasValue), Is.True);
        }

        [Test]
        public void SharedThreeWorkerSelection_FreezesDisjointRolesAndIgnoresFourthWorker()
        {
            using var f = new Fixture(4);
            var candidate = f.Manager.PrepareActionPlan(
                CommanderSemanticJson.Parse(CommanderPhase5ADynamicCompilerTests.SharedWorkers),
                "take three idle workers, two on sheep and the third on a Mill", 1);
            Assert.That(f.Workers.All(w => !f.Manager.GetWorkerReservation(w.Id).HasValue), Is.True,
                "A pending preview cannot reserve workers.");
            var goals = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[0].Id)?.GoalId, Is.EqualTo(goals[0].GoalId));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[1].Id)?.GoalId, Is.EqualTo(goals[0].GoalId));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[2].Id)?.GoalId, Is.EqualTo(goals[1].GoalId));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[3].Id).HasValue, Is.False);
            f.Manager.Tick(0);
            var first = f.Sim.CommandBuffer.FlushCommands();
            var gather = first.OfType<SlaughterSheepCommand>().Single();
            Assert.That(gather.VillagerIds, Is.EquivalentTo(new[] { f.Workers[0].Id, f.Workers[1].Id }));
            f.Sim.Tick(first);
            f.Manager.Tick(15);
            var build = f.Sim.CommandBuffer.FlushCommands().OfType<PlaceBuildingCommand>().Single();
            Assert.That(build.BuildingType, Is.EqualTo(BuildingType.Mill));
            Assert.That(build.VillagerUnitIds, Is.EqualTo(new[] { f.Workers[2].Id }));
        }

        private sealed class MutableCostConfig : SimulationConfig
        {
            public int BarracksCost;
            public override int BarracksWoodCost => BarracksCost;
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly SimulationConfig Config;
            internal readonly GameSimulation Sim;
            internal readonly CommanderGoalManager Manager;
            internal readonly UnitData[] Workers;
            internal Fixture(int count, bool mutableCosts = false)
            {
                Config = mutableCosts ? ScriptableObject.CreateInstance<MutableCostConfig>()
                    : ScriptableObject.CreateInstance<SimulationConfig>();
                Sim = new GameSimulation(Config, 1, new[] { 0 }, Array.Empty<int>());
                int x = Sim.MapData.Width / 2, z = Sim.MapData.Height / 2;
                for (int tx = x - 25; tx <= x + 25; tx++)
                    for (int tz = z - 20; tz <= z + 20; tz++)
                    {
                        Sim.MapData.Tiles[tx, tz] = TileType.Grass;
                        Sim.MapData.ForestDensity[tx, tz] = 0;
                        Sim.MapData.FoundationCount[tx, tz] = 0;
                        Sim.FogOfWar.SetVisible(0, tx, tz);
                    }
                Sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                var resources = Sim.ResourceManager.GetPlayerResources(0);
                resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
                Workers = new UnitData[count];
                for (int i = 0; i < count; i++)
                {
                    var worker = Sim.UnitRegistry.CreateUnit(0, Sim.MapData.TileToWorldFixed(x - 4 - i, z),
                        Fixed32.One, Fixed32.One, Fixed32.One);
                    worker.IsVillager = true;
                    worker.CurrentHealth = worker.MaxHealth = 100;
                    worker.State = UnitState.Idle;
                    Workers[i] = worker;
                }
                var sheep = Sim.UnitRegistry.CreateUnit(0, Sim.MapData.TileToWorldFixed(x - 8, z - 2),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                sheep.IsSheep = true;
                sheep.CurrentHealth = sheep.MaxHealth = 100;
                sheep.State = UnitState.Idle;
                Manager = new CommanderGoalManager(Sim, 0);
            }
            public void Dispose() { Manager.Dispose(); UnityEngine.Object.DestroyImmediate(Config); }
        }
    }
}
