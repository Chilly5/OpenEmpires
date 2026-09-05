using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderIntentParserTests
    {
        private SimpleTextIntentParser parser;

        [SetUp]
        public void SetUp()
        {
            parser = new SimpleTextIntentParser();
        }

        [TestCase("make 5 spearmen", 1, 5)]
        [TestCase("create 10 archers", 2, 10)]
        [TestCase("train 20 knights", 7, 20)]
        [TestCase("  MAKE   10 Spearmen. ", 1, 10)]
        public void Parse_UnitCommands_ReturnTypedEnsureUnitCount(
            string text, int expectedUnitType, int expectedAmount)
        {
            CommanderIntentInterpretation result = parser.Interpret(text, 3);

            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(result.Intent, Is.TypeOf<EnsureUnitCountIntent>());
            var intent = (EnsureUnitCountIntent)result.Intent;
            Assert.That(intent.PlayerId, Is.EqualTo(3));
            Assert.That(intent.UnitType, Is.EqualTo(expectedUnitType));
            Assert.That(intent.TargetTotal, Is.EqualTo(expectedAmount));
        }

        [TestCase("put 8 villagers on wood", ResourceType.Wood, 8)]
        [TestCase("move 5 workers to food", ResourceType.Food, 5)]
        public void Parse_ResourceCommands_ReturnTypedExactAllocation(
            string text, ResourceType resource, int count)
        {
            CommanderIntentInterpretation result = parser.Interpret(text, 0);

            Assert.That(result.Success, Is.True, result.Reason);
            var intent = (SetResourceAllocationIntent)result.Intent;
            Assert.That(intent.Resource, Is.EqualTo(resource));
            Assert.That(intent.Mode, Is.EqualTo(ResourceAllocationMode.SetExact));
            Assert.That(intent.WorkerCount, Is.EqualTo(count));
        }

        [TestCase("more gold workers")]
        [TestCase("increase gold workers")]
        public void Parse_MoreGoldWorkers_RepresentsUnspecifiedIncreaseWithoutInventingCount(string text)
        {
            CommanderIntentInterpretation result = parser.Interpret(text, 0);

            Assert.That(result.Success, Is.True, result.Reason);
            var intent = (SetResourceAllocationIntent)result.Intent;
            Assert.That(intent.Resource, Is.EqualTo(ResourceType.Gold));
            Assert.That(intent.Mode, Is.EqualTo(ResourceAllocationMode.Increase));
            Assert.That(intent.WorkerCount, Is.Null);
        }

        [TestCase("build barracks", BuildingType.Barracks)]
        [TestCase("create house", BuildingType.House)]
        [TestCase("make a stable", BuildingType.Stables)]
        public void Parse_BuildingCommands_ReturnTypedBuildStructure(
            string text, BuildingType expected)
        {
            CommanderIntentInterpretation result = parser.Interpret(text, 0);

            Assert.That(result.Success, Is.True, result.Reason);
            var intent = (BuildStructureIntent)result.Intent;
            Assert.That(intent.StructureType, Is.EqualTo(expected));
            Assert.That(intent.Count, Is.EqualTo(1));
        }

        [Test]
        public void Parse_Constraints_AreStronglyTypedAndKeptOutsideCommandNoun()
        {
            CommanderIntentInterpretation result = parser.Interpret(
                "make 10 spearmen but don't touch gold max queue 5", 0);

            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(result.Intent.Constraints, Has.Count.EqualTo(2));
            Assert.That(result.Intent.Constraints[0], Is.TypeOf<ProtectedResourceConstraint>());
            Assert.That(((ProtectedResourceConstraint)result.Intent.Constraints[0]).Resource,
                Is.EqualTo(ResourceType.Gold));
            Assert.That(((MaximumQueueConstraint)result.Intent.Constraints[1]).MaximumQueue,
                Is.EqualTo(5));
        }

        [Test]
        public void Parse_IdleOnlyConstraint_IsStronglyTyped()
        {
            CommanderIntentInterpretation result = parser.Interpret(
                "make 10 spearmen and use idle villagers only", 0);

            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(result.Intent.Constraints, Has.Count.EqualTo(1));
            Assert.That(result.Intent.Constraints[0], Is.TypeOf<PreferredWorkersConstraint>());
            Assert.That(((PreferredWorkersConstraint)result.Intent.Constraints[0]).WorkerSource,
                Is.EqualTo(CommanderPreferredWorkerSource.IdleOnly));
        }

        [Test]
        public void Parse_DoNotQueueMoreThanFive_IsMaximumQueueConstraint()
        {
            var result = parser.Interpret("make 10 archers do not queue more than 5", 0);
            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(((MaximumQueueConstraint)result.Intent.Constraints[0]).MaximumQueue, Is.EqualTo(5));
        }

        [TestCase("", CommanderIntentErrorCode.EmptyInput)]
        [TestCase("dance around the town center", CommanderIntentErrorCode.UnknownCommand)]
        [TestCase("make 10 banana warriors", CommanderIntentErrorCode.UnknownUnit)]
        [TestCase("put 8 villagers on crystal", CommanderIntentErrorCode.UnknownResource)]
        [TestCase("build dragon keep", CommanderIntentErrorCode.UnknownStructure)]
        [TestCase("make -5 spearmen", CommanderIntentErrorCode.InvalidAmount)]
        [TestCase("make 999999999999999999999 spearmen", CommanderIntentErrorCode.InvalidAmount)]
        public void Parse_InvalidInput_ReturnsStructuredRejection(
            string text, CommanderIntentErrorCode expectedCode)
        {
            CommanderIntentInterpretation result = parser.Interpret(text, 0);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(expectedCode));
            Assert.That(result.Intent, Is.Null);
        }
    }

    public class CommanderIntentValidationTests : CommanderIntentSimulationFixture
    {
        [Test]
        public void Validate_EnsureUnitCountAbovePopulationMaximum_IsRejectedWithoutClamping()
        {
            var validator = new CommanderIntentValidator();
            var intent = new EnsureUnitCountIntent(0, 1, Config.MaxPopulation + 1);

            CommanderIntentValidationResult result = validator.Validate(intent, Simulation, 0);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.AmountOutOfRange));
            Assert.That(intent.TargetTotal, Is.EqualTo(Config.MaxPopulation + 1));
        }

        [Test]
        public void Validate_UnknownUnitAndPlayerMismatch_AreRejected()
        {
            var validator = new CommanderIntentValidator();

            CommanderIntentValidationResult unknown = validator.Validate(
                new EnsureUnitCountIntent(0, 999, 10), Simulation, 0);
            CommanderIntentValidationResult mismatch = validator.Validate(
                new EnsureUnitCountIntent(1, 1, 10), Simulation, 0);

            Assert.That(unknown.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.UnknownUnit));
            Assert.That(mismatch.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.PlayerMismatch));
        }

        [Test]
        public void Validate_MaximumQueuePolicyAndDuplicateConstraints_AreRejected()
        {
            var validator = new CommanderIntentValidator();
            var zeroQueue = new EnsureUnitCountIntent(0, 1, 10,
                new CommanderConstraint[] { new MaximumQueueConstraint(0) });
            var duplicate = new EnsureUnitCountIntent(0, 1, 10,
                new CommanderConstraint[]
                {
                    new ProtectedResourceConstraint(ResourceType.Gold),
                    new ProtectedResourceConstraint(ResourceType.Wood)
                });

            Assert.That(validator.Validate(zeroQueue, Simulation, 0).IsValid, Is.False);
            Assert.That(validator.Validate(duplicate, Simulation, 0).ErrorCode,
                Is.EqualTo(CommanderIntentErrorCode.UnsupportedConstraint));
        }
    }

    public class CommanderIntentResolverTests : CommanderIntentSimulationFixture
    {
        [Test]
        public void Resolve_EnsureUnitCount_SubmitsExistingGoalAndStartedEvent()
        {
            var manager = new CommanderGoalManager(Simulation, 0);
            var events = new List<CommanderGoalEvent>();
            manager.GoalEventPublished += events.Add;
            var resolver = new CommanderIntentResolver();

            CommanderIntentResolution result = resolver.Resolve(
                new EnsureUnitCountIntent(0, 1, 10), Simulation, manager);

            Assert.That(result.Status, Is.EqualTo(CommanderIntentResolutionStatus.GoalCreated));
            Assert.That(result.Goal, Is.SameAs(manager.ActiveGoal));
            var goal = (EnsureUnitCountGoal)result.Goal;
            Assert.That(goal.RequestedUnitType, Is.EqualTo(1));
            Assert.That(goal.TargetTotal, Is.EqualTo(10));
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].EventType, Is.EqualTo(CommanderGoalEventType.GoalStarted));
        }

        [Test]
        public void Resolve_MaximumQueueConstraint_MapsToExistingGoalField()
        {
            var manager = new CommanderGoalManager(Simulation, 0);
            var resolver = new CommanderIntentResolver();
            var intent = new EnsureUnitCountIntent(0, 1, 10,
                new CommanderConstraint[] { new MaximumQueueConstraint(5) });

            CommanderIntentResolution result = resolver.Resolve(intent, Simulation, manager);

            Assert.That(result.CreatedGoal, Is.True, result.Reason);
            Assert.That(((EnsureUnitCountGoal)result.Goal).MaxQueueDepth, Is.EqualTo(5));
        }

        [TestCase(2)]
        [TestCase(7)]
        public void Resolve_RecognizedNonSpearmanIntent_CreatesProductionGoal(int unitType)
        {
            var manager = new CommanderGoalManager(Simulation, 0);
            var resolver = new CommanderIntentResolver();

            CommanderIntentResolution result = resolver.Resolve(
                new EnsureUnitCountIntent(0, unitType, 10), Simulation, manager);

            Assert.That(result.Status,
                Is.EqualTo(CommanderIntentResolutionStatus.GoalCreated));
            Assert.That(result.ErrorCode,
                Is.EqualTo(CommanderIntentErrorCode.None));
            Assert.That(((EnsureUnitCountGoal)result.Goal).RequestedUnitType, Is.EqualTo(unitType));
        }

        [Test]
        public void Resolve_ResourceAndBuildingIntents_CreateTypedGoals()
        {
            var manager = new CommanderGoalManager(Simulation, 0);
            var resolver = new CommanderIntentResolver();

            CommanderIntentResolution resource = resolver.Resolve(
                new SetResourceAllocationIntent(0, ResourceType.Wood,
                    ResourceAllocationMode.SetExact, 8), Simulation, manager);
            CommanderIntentResolution building = resolver.Resolve(
                new BuildStructureIntent(0, BuildingType.Barracks), Simulation, manager);

            Assert.That(resource.Status,
                Is.EqualTo(CommanderIntentResolutionStatus.GoalCreated));
            Assert.That(building.Status,
                Is.EqualTo(CommanderIntentResolutionStatus.GoalCreated));
            Assert.That(resource.Goal, Is.TypeOf<ResourceAllocationGoal>());
            Assert.That(building.Goal, Is.TypeOf<BuildStructureGoal>());
        }

        [Test]
        public void Resolve_IdleOnlyConstraint_ConfiguresGoalPolicy()
        {
            var manager = new CommanderGoalManager(Simulation, 0);
            var resolver = new CommanderIntentResolver();
            var intent = new EnsureUnitCountIntent(0, 1, 10,
                new CommanderConstraint[]
                {
                    new PreferredWorkersConstraint(CommanderPreferredWorkerSource.IdleOnly)
                });

            CommanderIntentResolution result = resolver.Resolve(intent, Simulation, manager);

            Assert.That(result.Status,
                Is.EqualTo(CommanderIntentResolutionStatus.GoalCreated));
            Assert.That(result.Goal.UseIdleWorkersOnly, Is.True);
        }
    }

    public class CommanderIntentIntegrationTests : CommanderIntentSimulationFixture
    {
        [Test]
        public void SubmitText_MakeTenSpearmen_CreatesActiveExistingGoal()
        {
            var manager = new CommanderGoalManager(Simulation, 0);
            using var dispatcher = new CommanderIntentDispatcher(Simulation, manager);

            CommanderIntentSubmission result = dispatcher.SubmitText("make 10 spearmen");

            Assert.That(result.CreatedGoal, Is.True, result.Response);
            Assert.That(manager.ActiveGoal, Is.TypeOf<EnsureUnitCountGoal>());
            Assert.That(((EnsureUnitCountGoal)manager.ActiveGoal).TargetTotal, Is.EqualTo(10));
        }

        [Test]
        public void SubmitText_UsesExistingCommanderExecutionAndNormalGameplayCommand()
        {
            CreateBarracks();
            GiveResources(food: 1000, wood: 1000);
            var manager = new CommanderGoalManager(Simulation, 0);
            using var dispatcher = new CommanderIntentDispatcher(Simulation, manager);

            dispatcher.SubmitText("make 10 spearmen");
            manager.Tick(0);
            List<ICommand> commands = Simulation.CommandBuffer.FlushCommands();

            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<TrainUnitCommand>());
            var train = (TrainUnitCommand)commands[0];
            Assert.That(train.PlayerId, Is.EqualTo(0));
            Assert.That(train.UnitType, Is.EqualTo(1));
        }

        [Test]
        public void SubmitText_InvalidIntentRejects_BuildIntentCreatesGoalWithoutImmediateCommand()
        {
            var manager = new CommanderGoalManager(Simulation, 0);
            using var dispatcher = new CommanderIntentDispatcher(Simulation, manager);

            CommanderIntentSubmission invalid = dispatcher.SubmitText("make banana warriors");
            CommanderIntentSubmission future = dispatcher.SubmitText("build barracks");

            Assert.That(invalid.CreatedGoal, Is.False);
            Assert.That(future.Resolution.Status,
                Is.EqualTo(CommanderIntentResolutionStatus.GoalCreated));
            Assert.That(manager.Goals, Has.Count.EqualTo(1));
            Assert.That(Simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void SubmitText_OversizedUnitTarget_IsRejectedBeforeGoalCreation()
        {
            var manager = new CommanderGoalManager(Simulation, 0);
            using var dispatcher = new CommanderIntentDispatcher(Simulation, manager);

            CommanderIntentSubmission result = dispatcher.SubmitText(
                "make 999999999 spearmen");

            Assert.That(result.CreatedGoal, Is.False);
            Assert.That(result.Resolution.Status,
                Is.EqualTo(CommanderIntentResolutionStatus.Rejected));
            Assert.That(result.Resolution.ErrorCode,
                Is.EqualTo(CommanderIntentErrorCode.AmountOutOfRange));
            Assert.That(manager.Goals, Is.Empty);
        }

        [Test]
        public void InjectedInterpreter_RunsOnlyOnSubmissionNotCommanderTicks()
        {
            var fake = new CountingInterpreter(
                new EnsureUnitCountIntent(0, CommanderIntentCatalog.SpearmanUnitType, 10));
            var manager = new CommanderGoalManager(Simulation, 0);
            using var dispatcher = new CommanderIntentDispatcher(Simulation, manager, fake);

            dispatcher.SubmitText("provider-specific text");
            manager.Tick(0);
            manager.Tick(15);
            manager.Tick(30);

            Assert.That(fake.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void GoalCompletionEvent_ProducesDeterministicReadyResponse()
        {
            for (int i = 0; i < 10; i++) CreateUnit(0, 1, i + 1);
            var manager = new CommanderGoalManager(Simulation, 0);
            using var dispatcher = new CommanderIntentDispatcher(Simulation, manager);
            var responses = new List<string>();
            dispatcher.ResponseGenerated += responses.Add;

            dispatcher.SubmitText("make 10 spearmen");
            manager.Tick(0);

            Assert.That(responses[0], Is.EqualTo("Understood.\nPreparing 10 spearmen."));
            Assert.That(responses[1], Is.EqualTo("Your 10 spearmen are ready."));
        }

        private sealed class CountingInterpreter : ISynchronousCommanderIntentInterpreter
        {
            public System.Threading.Tasks.Task<CommanderIntentInterpretation> InterpretAsync(string input,
                CommanderContext context, System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return System.Threading.Tasks.Task.FromResult(Interpret(input, context.PlayerId));
            }

            private readonly CommanderIntent intent;
            public int CallCount { get; private set; }

            public CountingInterpreter(CommanderIntent intent)
            {
                this.intent = intent;
            }

            public CommanderIntentInterpretation Interpret(string playerInput, int playerId)
            {
                CallCount++;
                return CommanderIntentInterpretation.Accepted(intent);
            }
        }
    }

    public class CommanderResponseGeneratorTests
    {
        [Test]
        public void Generate_UnsupportedFutureIntent_DoesNotClaimExecution()
        {
            var generator = new CommanderResponseGenerator();
            var intent = new BuildStructureIntent(0, BuildingType.Barracks);
            var resolution = new CommanderIntentResolution(
                CommanderIntentResolutionStatus.ExecutionNotImplemented, intent, null,
                CommanderIntentErrorCode.UnsupportedIntentExecution,
                "Build-structure goals are planned for Phase 3.");

            string response = generator.GenerateResolutionResponse(resolution);

            Assert.That(response, Does.Contain("understood"));
            Assert.That(response, Does.Contain("cannot execute it yet"));
            Assert.That(response, Does.Not.Contain("Building"));
        }

        [TestCase(CommanderGoalEventType.GoalBlocked, "I am blocked.")]
        [TestCase(CommanderGoalEventType.GoalFailed, "I could not complete the request.")]
        [TestCase(CommanderGoalEventType.GoalCancelled, "The Commander request was cancelled.")]
        public void Generate_TerminalOrBlockedGoalEvent_ReturnsDeterministicTemplate(
            CommanderGoalEventType eventType, string expectedPrefix)
        {
            var generator = new CommanderResponseGenerator();
            var intent = new EnsureUnitCountIntent(0, 1, 10);
            var goal = new EnsureUnitCountGoal(0, 1, 10);

            string response = generator.GenerateGoalResponse(
                new CommanderGoalEvent(eventType, 30, goal), intent);

            Assert.That(response, Does.StartWith(expectedPrefix));
        }
    }

    public abstract class CommanderIntentSimulationFixture
    {
        protected SimulationConfig Config { get; private set; }
        protected GameSimulation Simulation { get; private set; }
        protected Vector2Int BaseTile { get; private set; }

        [SetUp]
        public void SetUpSimulation()
        {
            Config = ScriptableObject.CreateInstance<SimulationConfig>();
            Simulation = new GameSimulation(Config, 2, new[] { 0, 1 }, Array.Empty<int>());
            BaseTile = Simulation.MapData.BasePositions != null
                && Simulation.MapData.BasePositions.Length > 0
                ? Simulation.MapData.BasePositions[0]
                : new Vector2Int(Simulation.MapData.Width / 2, Simulation.MapData.Height / 2);
            Simulation.CreateBuilding(0, BuildingType.TownCenter, BaseTile.x, BaseTile.y,
                underConstruction: false, isMainTownCenter: true);
        }

        [TearDown]
        public void TearDownSimulation()
        {
            UnityEngine.Object.DestroyImmediate(Config);
        }

        protected void CreateBarracks()
        {
            Simulation.CreateBuilding(0, BuildingType.Barracks,
                BaseTile.x + 8, BaseTile.y, underConstruction: false);
        }

        protected UnitData CreateUnit(int playerId, int unitType, int offset)
        {
            UnitData unit = Simulation.UnitRegistry.CreateUnit(playerId,
                Simulation.MapData.TileToWorldFixed(BaseTile.x + offset, BaseTile.y + 2),
                Fixed32.One, Fixed32.FromFloat(0.4f), Fixed32.One);
            unit.UnitType = unitType;
            unit.MaxHealth = 100;
            unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        protected void GiveResources(int food, int wood)
        {
            Simulation.ResourceManager.AddResource(0, ResourceType.Food, food);
            Simulation.ResourceManager.AddResource(0, ResourceType.Wood, wood);
        }
    }
}
