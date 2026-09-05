using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3BPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private int x, z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            x = sim.MapData.BasePositions[0].x; z = sim.MapData.BasePositions[0].y;
            sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
            var enemy = sim.MapData.BasePositions[1];
            sim.CreateBuilding(1, BuildingType.TownCenter, enemy.x, enemy.y, false, true).AutoProduceVillagers = false;
            manager = new CommanderGoalManager(sim, 0);
        }
        [TearDown]
        public void TearDown() { dispatcher?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }

        [UnityTest]
        public IEnumerator Interpreter_DoesNotBlockSimulation()
        {
            dispatcher = new CommanderIntentDispatcher(sim, manager, new MockLlmIntentInterpreter(500));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            Assert.That(task.IsCompleted, Is.False);
            int frames = 0;
            while (!task.IsCompleted && watch.ElapsedMilliseconds < 10000)
            {
                Assert.That(manager.Goals, Is.Empty);
                sim.Tick(); manager.Tick(sim.CurrentTick); frames++;
                yield return null;
            }
            Assert.That(task.IsCompleted, Is.True, "Async request did not finish.");
            Assert.That(task.GetAwaiter().GetResult().CreatedGoal, Is.True);
            Assert.That(frames, Is.GreaterThan(1));
            Assert.That(sim.CurrentTick, Is.EqualTo(frames));
            Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(450));
            Debug.Log($"[Phase3B Runtime] Nonblocking mock: frames={frames}, simulationTicks={sim.CurrentTick}, elapsedMs={watch.ElapsedMilliseconds}.");
        }

        [UnityTest]
        public IEnumerator Interpreter_CancellationStopsSubmission()
        {
            dispatcher = new CommanderIntentDispatcher(sim, manager, new MockLlmIntentInterpreter(500));
            using (var cancel = new CancellationTokenSource())
            {
                var task = dispatcher.SubmitTextAsync("make 10 spearmen", cancel.Token);
                cancel.Cancel();
                yield return Await(task);
                Assert.That(task.Result.Interpretation.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.Cancelled));
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(dispatcher.State, Is.EqualTo(CommanderSubmissionState.Cancelled));
            }
        }

        [UnityTest]
        public IEnumerator Interpreter_InvalidResponseFailsSafely()
        {
            dispatcher = new CommanderIntentDispatcher(sim, manager, new MockLlmIntentInterpreter(500, "{broken JSON"));
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            yield return Await(task);
            Assert.That(task.Result.CreatedGoal, Is.False);
            Assert.That(task.Result.Interpretation.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.InvalidJson));
            Assert.That(task.Result.Interpretation.ErrorField, Is.EqualTo("response"));
            Assert.That(manager.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator SubmittingState_WaitingState_ResolvingState_ExecutingState_CompletedState()
        {
            dispatcher = new CommanderIntentDispatcher(sim, manager, new MockLlmIntentInterpreter(500));
            var states = new List<CommanderSubmissionState>();
            var callbackThreads = new List<int>();
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            dispatcher.StateChanged += state => { callbackThreads.Add(Thread.CurrentThread.ManagedThreadId); states.Add(state); };
            Assert.That(dispatcher.State, Is.EqualTo(CommanderSubmissionState.Idle));
            // Satisfy target explicitly: this test isolates state reporting, demo below trains normally.
            for (int i = 0; i < 10; i++)
            {
                var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 7, z), Fixed32.One, Fixed32.One, Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
            }
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            Assert.That(states, Is.EqualTo(new[] { CommanderSubmissionState.Submitting, CommanderSubmissionState.WaitingForInterpretation }));
            yield return Await(task);
            Assert.That(task.Result.CreatedGoal, Is.True);
            manager.Tick(sim.CurrentTick);
            Assert.That(states, Is.EqualTo(new[] { CommanderSubmissionState.Submitting, CommanderSubmissionState.WaitingForInterpretation,
                CommanderSubmissionState.Resolving, CommanderSubmissionState.Executing, CommanderSubmissionState.Completed }));
            Assert.That(callbackThreads.All(thread => thread == mainThread), Is.True);
        }

        [UnityTest]
        public IEnumerator FailedState_ProviderExceptionAndTimeout()
        {
            dispatcher = new CommanderIntentDispatcher(sim, manager, new ThrowingInterpreter());
            var failed = dispatcher.SubmitTextAsync("anything");
            yield return Await(failed);
            Assert.That(failed.Result.Interpretation.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.ProviderFailure));
            Assert.That(dispatcher.State, Is.EqualTo(CommanderSubmissionState.Failed));
            dispatcher.Dispose();
            var provider = new ControlledInterpreter();
            dispatcher = new CommanderIntentDispatcher(sim, manager, provider);
            var timedOut = dispatcher.SubmitTextAsync("make 10 spearmen", timeoutMilliseconds: 50);
            yield return Await(timedOut);
            Assert.That(timedOut.Result.Interpretation.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.TimedOut));
            provider.Complete();
            yield return null; yield return null;
            Assert.That(manager.Goals, Is.Empty, "Late response after timeout must never resolve.");
        }

        [UnityTest]
        public IEnumerator CancelledState_DisposeAndLateProviderResponse()
        {
            var provider = new ControlledInterpreter();
            dispatcher = new CommanderIntentDispatcher(sim, manager, provider);
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            dispatcher.Dispose();
            yield return Await(task);
            provider.Complete();
            yield return null; yield return null;
            Assert.That(task.Result.Interpretation.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.Cancelled));
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(dispatcher.State, Is.EqualTo(CommanderSubmissionState.Cancelled));
        }

        [UnityTest]
        public IEnumerator CancellationAtResolvingCreatesNoGoal_AndRetryWorks()
        {
            dispatcher = new CommanderIntentDispatcher(sim, manager, new MockLlmIntentInterpreter(500));
            Action<CommanderSubmissionState> cancel = state => { if (state == CommanderSubmissionState.Resolving) dispatcher.CancelPendingSubmission(); };
            dispatcher.StateChanged += cancel;
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            yield return Await(task);
            Assert.That(task.Result.CreatedGoal, Is.False);
            Assert.That(manager.Goals, Is.Empty);
            dispatcher.StateChanged -= cancel;
            var retry = dispatcher.SubmitTextAsync("make 10 spearmen");
            yield return Await(retry);
            Assert.That(retry.Result.CreatedGoal, Is.True);
            Assert.That(manager.Goals, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SingleFlight_OneSnapshot_OneInterpretation_NoTickInvocations()
        {
            var provider = new ControlledInterpreter();
            dispatcher = new CommanderIntentDispatcher(sim, manager, provider);
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = 123;
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            var duplicate = dispatcher.SubmitTextAsync("make 2 archers");
            Assert.That(duplicate.Result.Interpretation.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.SubmissionInProgress));
            resources.Food = 999;
            for (int i = 0; i < 10; i++) { sim.Tick(); manager.Tick(sim.CurrentTick); yield return null; }
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(provider.Context.Resources.Food, Is.EqualTo(123));
            Assert.That(provider.Context.SnapshotTick, Is.Zero);
            provider.Complete();
            yield return Await(task);
            for (int i = 0; i < 10; i++) { sim.Tick(); manager.Tick(sim.CurrentTick); yield return null; }
            Assert.That(provider.Calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AsyncResult_StillPassesOwnershipValidation()
        {
            var provider = new ControlledInterpreter();
            dispatcher = new CommanderIntentDispatcher(sim, manager, provider);
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            provider.Complete(1);
            yield return Await(task);
            Assert.That(task.Result.CreatedGoal, Is.False);
            Assert.That(task.Result.Resolution.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.PlayerMismatch));
            Assert.That(manager.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Demo_Make10Spearmen_AsyncDtoToNormalCommandExecution()
        {
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = 10000;
            sim.CreateBuilding(0, BuildingType.Barracks, x + 8, z, false);
            for (int a = x - 5; a <= x + 15; a++)
                for (int b = z - 5; b <= z + 10; b++) sim.FogOfWar.SetVisible(0, a, b);
            var commands = new List<ICommand>();
            sim.CommandBuffer.CommandEnqueued += (command, source) => { if (source == CommandEnqueueSource.Commander) commands.Add(command); };
            dispatcher = new CommanderIntentDispatcher(sim, manager, new MockLlmIntentInterpreter(500));
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            yield return Await(task);
            Assert.That(task.Result.CreatedGoal, Is.True, task.Result.Response);
            var goal = task.Result.Resolution.Goal;
            for (int i = 0; i < 15000 && !goal.IsTerminal; i++)
            {
                manager.Tick(sim.CurrentTick); sim.Tick();
                if (i % 30 == 0) yield return null;
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(dispatcher.State, Is.EqualTo(CommanderSubmissionState.Completed));
            Assert.That(sim.UnitRegistry.GetAllUnits().Count(unit => unit.PlayerId == 0 && unit.UnitType == 1 && unit.CurrentHealth > 0), Is.EqualTo(10));
            Assert.That(commands.OfType<TrainUnitCommand>().Count(), Is.EqualTo(10));
            Assert.That(commands.All(command => command is TrainUnitCommand), Is.True);
            Debug.Log($"[Phase3B Runtime] PASS make 10 spearmen: text -> delayed mock -> JSON DTO -> validated intent -> goal -> {commands.Count} normal TrainUnitCommands -> 10 live spearmen; tick={sim.CurrentTick}; state={dispatcher.State}.");
        }

        [UnityTest]
        public IEnumerator ObserverAndCancellationCallbackFailures_DoNotChangeCommittedSubmission()
        {
            dispatcher = new CommanderIntentDispatcher(sim, manager, new ThrowingCancellationInterpreter());
            dispatcher.ResponseGenerated += response => throw new OperationCanceledException("Observer failure");
            dispatcher.StateChanged += state => { if (state == CommanderSubmissionState.Executing) throw new InvalidOperationException("Observer failure"); };
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            yield return Await(task);
            Assert.That(task.Result.CreatedGoal, Is.True);
            Assert.That(dispatcher.State, Is.EqualTo(CommanderSubmissionState.Executing));
            Assert.That(dispatcher.IsInterpreting, Is.False);
            Assert.That(manager.Goals, Has.Count.EqualTo(1));
            var retry = dispatcher.SubmitTextAsync("make 10 spearmen");
            yield return Await(retry);
            Assert.That(retry.Result.CreatedGoal, Is.True);
        }

        [UnityTest]
        public IEnumerator AcceptanceObserverCancelsGoal_TerminalStateIsNotLost()
        {
            dispatcher = new CommanderIntentDispatcher(sim, manager, new MockLlmIntentInterpreter(500));
            bool cancelled = false;
            dispatcher.ResponseGenerated += response =>
            {
                if (cancelled || manager.Goals.Count == 0) return;
                cancelled = true;
                manager.CancelGoal(manager.Goals[0].GoalId);
            };
            var task = dispatcher.SubmitTextAsync("make 10 spearmen");
            yield return Await(task);
            Assert.That(task.Result.CreatedGoal, Is.True, "Cancellation is of an accepted goal, not of interpretation.");
            Assert.That(dispatcher.State, Is.EqualTo(CommanderSubmissionState.Cancelled));
        }

        private sealed class ThrowingCancellationInterpreter : ICommanderIntentInterpreter
        {
            public Task<CommanderIntentInterpretation> InterpretAsync(string input, CommanderContext context, CancellationToken cancellationToken)
            {
                cancellationToken.Register(() => throw new InvalidOperationException("Cancellation callback failure"));
                return Task.FromResult(CommanderIntentInterpretation.Accepted(new EnsureUnitCountIntent(context.PlayerId, 1, 10)));
            }
        }

        private static IEnumerator Await(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Task exceeded test deadline.");
            if (task.IsFaulted) throw task.Exception;
        }

        private sealed class ControlledInterpreter : ICommanderIntentInterpreter
        {
            private readonly TaskCompletionSource<CommanderIntentInterpretation> completion = new TaskCompletionSource<CommanderIntentInterpretation>(TaskCreationOptions.RunContinuationsAsynchronously);
            public CommanderContext Context;
            public int Calls;
            public Task<CommanderIntentInterpretation> InterpretAsync(string input, CommanderContext context, CancellationToken cancellationToken)
            { Context = context; Interlocked.Increment(ref Calls); return completion.Task; }
            public void Complete(int player = 0) => completion.TrySetResult(CommanderIntentInterpretation.Accepted(new EnsureUnitCountIntent(player, 1, 10)));
        }
        private sealed class ThrowingInterpreter : ICommanderIntentInterpreter
        {
            public Task<CommanderIntentInterpretation> InterpretAsync(string input, CommanderContext context, CancellationToken cancellationToken)
            { throw new InvalidOperationException("Mock provider failure."); }
        }
    }
}
