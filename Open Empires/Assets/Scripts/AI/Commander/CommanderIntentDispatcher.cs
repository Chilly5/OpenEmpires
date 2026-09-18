using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    public enum CommanderSubmissionState
    {
        Idle, Submitting, WaitingForInterpretation, Resolving, Executing, Completed, Failed, Cancelled
    }

    public sealed class CommanderIntentSubmission
    {
        public CommanderIntentInterpretation Interpretation { get; }
        public CommanderIntentResolution Resolution { get; }
        public StrategicIntentSubmission StrategicSubmission { get; }
        public string Response { get; }
        public bool CreatedGoal => Resolution != null && Resolution.CreatedGoal;
        public bool CreatedPlan => StrategicSubmission != null && StrategicSubmission.CreatedPlan;

        public CommanderIntentSubmission(CommanderIntentInterpretation interpretation,
            CommanderIntentResolution resolution, string response)
            : this(interpretation, resolution, null, response)
        {
        }

        public CommanderIntentSubmission(CommanderIntentInterpretation interpretation,
            CommanderIntentResolution resolution, StrategicIntentSubmission strategicSubmission,
            string response)
        {
            Interpretation = interpretation;
            Resolution = resolution;
            StrategicSubmission = strategicSubmission;
            Response = response ?? string.Empty;
        }
    }

    public sealed class CommanderIntentDispatcher : IDisposable
    {
        private readonly ICommanderIntentInterpreter interpreter;
        private readonly CommanderIntentResolver resolver;
        private readonly CommanderResponseGenerator responseGenerator;
        private readonly IntentRouter intentRouter;
        private readonly GameSimulation simulation;
        private readonly CommanderGoalManager goalManager;
        private readonly Dictionary<int, CommanderIntent> intentsByGoalId =
            new Dictionary<int, CommanderIntent>();
        private bool disposed;
        private readonly int ownerThreadId = Thread.CurrentThread.ManagedThreadId;
        private CancellationTokenSource pendingRequest;
        private int displayedGoalId = -1;
        public CommanderSubmissionState State { get; private set; } = CommanderSubmissionState.Idle;
        public bool IsInterpreting => pendingRequest != null;
        public event Action<CommanderSubmissionState> StateChanged;

        public event Action<string> ResponseGenerated;

        public CommanderIntentDispatcher(GameSimulation simulation,
            CommanderGoalManager goalManager,
            ICommanderIntentInterpreter interpreter = null,
            CommanderIntentResolver resolver = null,
            CommanderResponseGenerator responseGenerator = null,
            StrategicPlanner strategicPlanner = null,
            IntentRouter intentRouter = null)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.goalManager = goalManager ?? throw new ArgumentNullException(nameof(goalManager));
            this.interpreter = interpreter ?? new SimpleTextIntentParser();
            this.resolver = resolver ?? new CommanderIntentResolver();
            this.responseGenerator = responseGenerator ?? new CommanderResponseGenerator();
            this.intentRouter = intentRouter ?? new IntentRouter(goalManager.PlayerId,
                SubmitTacticalIntent, strategicPlanner);
            goalManager.GoalEventPublished += HandleGoalEvent;
        }

        public CommanderIntentSubmission SubmitText(string playerInput)
        {
            ThrowIfDisposed();
            CheckOwnerThread();
            if (pendingRequest != null) return RejectBusy();
            if (!(interpreter is ISynchronousCommanderIntentInterpreter immediate))
                throw new InvalidOperationException("This interpreter is asynchronous. Use SubmitTextAsync.");
            CommanderIntentInterpretation interpretation = immediate.Interpret(
                playerInput, goalManager.PlayerId);
            if (!interpretation.Success)
            {
                string rejected = responseGenerator.GenerateInterpretationRejection(interpretation);
                Notify(ResponseGenerated, rejected);
                return new CommanderIntentSubmission(interpretation, null, rejected);
            }

            return SubmitInterpretedIntent(interpretation);
        }

        public CommanderIntentSubmission SubmitIntent(CommanderIntent intent)
        {
            ThrowIfDisposed();
            CheckOwnerThread();
            if (pendingRequest != null) return RejectBusy();
            var interpretation = CommanderIntentInterpretation.Accepted(intent);
            return SubmitInterpretedIntent(interpretation);
        }

        public CommanderIntentSubmission SubmitIntent(StrategicIntent intent)
        {
            ThrowIfDisposed();
            CheckOwnerThread();
            if (pendingRequest != null) return RejectBusy();
            return SubmitInterpretedIntent(
                CommanderIntentInterpretation.AcceptedStrategic(intent));
        }

        public async Task<CommanderIntentSubmission> SubmitTextAsync(string playerInput,
            CancellationToken cancellationToken = default, int timeoutMilliseconds = 10000)
        {
            ThrowIfDisposed();
            CheckOwnerThread();
            if (pendingRequest != null) return RejectBusy();
            if (SynchronizationContext.Current == null)
                throw new InvalidOperationException("Async submission requires the Unity main-thread synchronization context.");
            if (timeoutMilliseconds < 1 || timeoutMilliseconds > 120000)
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = request.Token;
            pendingRequest = request;
            displayedGoalId = -1;
            try
            {
                SetState(CommanderSubmissionState.Submitting);
                request.Token.ThrowIfCancellationRequested();
                var context = new CommanderContextBuilder().Build(simulation, goalManager);
                SetState(CommanderSubmissionState.WaitingForInterpretation);
                request.Token.ThrowIfCancellationRequested();
                // Even a provider's synchronous prefix only sees detached data on a worker thread.
                var interpretationTask = Task.Run(() => interpreter.InterpretAsync(playerInput, context, token), token);
                // Observe late failures even if a non-cooperative provider ignores cancellation/timeout.
                _ = interpretationTask.ContinueWith(task => { var ignored = task.Exception; },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                var timeout = Task.Delay(timeoutMilliseconds, request.Token);
                var finished = await Task.WhenAny(interpretationTask, timeout);
                CheckOwnerThread();
                request.Token.ThrowIfCancellationRequested();
                if (finished != interpretationTask)
                {
                    CancelSafely(request);
                    return RejectAsync(CommanderIntentErrorCode.TimedOut, "Intent interpretation timed out.", CommanderSubmissionState.Failed);
                }
                var interpretation = await interpretationTask;
                request.Token.ThrowIfCancellationRequested();
                if (disposed) throw new OperationCanceledException();
                if (interpretation == null)
                    return RejectAsync(CommanderIntentErrorCode.ProviderFailure, "Interpreter returned no result.", CommanderSubmissionState.Failed);
                if (!interpretation.Success)
                {
                    SetState(CommanderSubmissionState.Failed);
                    return PublishRejection(interpretation);
                }
                SetState(CommanderSubmissionState.Resolving);
                // A cancellation raised by a state observer must win before goal creation.
                request.Token.ThrowIfCancellationRequested();
                var result = SubmitInterpretedIntent(interpretation, trackLifecycle: true);
                return result;
            }
            catch (OperationCanceledException)
            { return RejectAsync(CommanderIntentErrorCode.Cancelled, "Intent submission cancelled; no goal created.", CommanderSubmissionState.Cancelled); }
            catch (Exception)
            { return RejectAsync(CommanderIntentErrorCode.ProviderFailure, "Intent interpretation failed safely.", CommanderSubmissionState.Failed); }
            finally
            {
                CancelSafely(request);
                if (ReferenceEquals(pendingRequest, request)) pendingRequest = null;
                request.Dispose();
            }
        }

        // Cancels interpretation only. Already accepted goals remain owned by the goal manager.
        public void CancelPendingSubmission()
        {
            CheckOwnerThread();
            CancelSafely(pendingRequest);
        }

        private CommanderIntentSubmission RejectAsync(CommanderIntentErrorCode code, string reason, CommanderSubmissionState state)
        {
            if (!disposed) SetState(state);
            return PublishRejection(CommanderIntentInterpretation.Rejected(code, reason));
        }

        private CommanderIntentSubmission RejectBusy() => new CommanderIntentSubmission(
            CommanderIntentInterpretation.Rejected(CommanderIntentErrorCode.SubmissionInProgress, "A submission is already being interpreted."),
            null, "A submission is already being interpreted.");

        private CommanderIntentSubmission PublishRejection(CommanderIntentInterpretation interpretation)
        {
            string response = responseGenerator.GenerateInterpretationRejection(interpretation);
            if (!disposed) Notify(ResponseGenerated, response);
            return new CommanderIntentSubmission(interpretation, null, response);
        }

        private void SetState(CommanderSubmissionState state)
        {
            if (disposed) return;
            State = state;
            Notify(StateChanged, state);
        }

        // UI observers and provider cancellation callbacks must not alter a committed result.
        private static void Notify<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try { handler(value); }
                catch (Exception error) { UnityEngine.Debug.LogWarning("[Commander] Notification callback failed: " + error.GetType().Name); }
            }
        }

        private static void CancelSafely(CancellationTokenSource request)
        {
            if (request == null) return;
            try { request.Cancel(); }
            catch (AggregateException) { UnityEngine.Debug.LogWarning("[Commander] Provider cancellation callback failed; submission cleanup continues."); }
        }

        private static CommanderSubmissionState GoalState(CommanderGoal goal) =>
            goal.Status == CommanderGoalStatus.Completed ? CommanderSubmissionState.Completed :
            goal.Status == CommanderGoalStatus.Failed ? CommanderSubmissionState.Failed :
            goal.Status == CommanderGoalStatus.Cancelled ? CommanderSubmissionState.Cancelled : CommanderSubmissionState.Executing;

        private void CheckOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThreadId)
                throw new InvalidOperationException("Commander dispatch must run on its owning Unity thread.");
        }

        public void Dispose()
        {
            if (disposed) return;
            CheckOwnerThread();
            disposed = true;
            if (pendingRequest != null) State = CommanderSubmissionState.Cancelled;
            CancelSafely(pendingRequest);
            goalManager.GoalEventPublished -= HandleGoalEvent;
            intentsByGoalId.Clear();
            StateChanged = null;
            ResponseGenerated = null;
        }

        private CommanderIntentSubmission SubmitInterpretedIntent(
            CommanderIntentInterpretation interpretation, bool trackLifecycle = false)
        {
            IntentRouteResult route = intentRouter.Route(interpretation.Request,
                strategicIsPlayerOverride: interpretation.StrategicIntent != null);
            CommanderIntentSubmission submission;
            if (route.TacticalSubmission != null)
            {
                submission = route.TacticalSubmission;
            }
            else
            {
                StrategicIntentSubmission strategic = route.StrategicSubmission;
                string response = strategic != null && strategic.CreatedPlan
                    ? $"Strategic plan #{strategic.Plan.StrategicPlanId} started: {strategic.Plan.PlanType}."
                    : route.Reason;
                if (strategic?.CreatedPlan == true && !string.IsNullOrEmpty(strategic.Plan.OutcomeMessage))
                    response += " " + strategic.Plan.OutcomeMessage;
                submission = new CommanderIntentSubmission(interpretation, null,
                    strategic, response);
                if (!disposed) Notify(ResponseGenerated, response);
            }

            if (trackLifecycle)
            {
                displayedGoalId = submission.CreatedGoal ? submission.Resolution.Goal.GoalId : -1;
                SetState(submission.CreatedGoal
                    ? GoalState(submission.Resolution.Goal)
                    : submission.CreatedPlan
                        ? CommanderSubmissionState.Executing
                        : CommanderSubmissionState.Failed);
            }
            return submission;
        }

        private CommanderIntentSubmission SubmitTacticalIntent(CommanderIntent intent)
        {
            CommanderIntentInterpretation interpretation =
                CommanderIntentInterpretation.Accepted(intent);
            CommanderIntentResolution resolution = resolver.Resolve(
                intent, simulation, goalManager);
            if (resolution.CreatedGoal)
                intentsByGoalId[resolution.Goal.GoalId] = resolution.Intent;
            string response = responseGenerator.GenerateResolutionResponse(resolution);
            if (!disposed) Notify(ResponseGenerated, response);
            return new CommanderIntentSubmission(interpretation, resolution, response);
        }

        private void HandleGoalEvent(CommanderGoalEvent goalEvent)
        {
            if (!intentsByGoalId.TryGetValue(goalEvent.Goal.GoalId,
                out CommanderIntent intent)) return;

            if (goalEvent.Goal.GoalId == displayedGoalId && goalEvent.Goal.IsTerminal)
                SetState(GoalState(goalEvent.Goal));

            string response = responseGenerator.GenerateGoalResponse(goalEvent, intent);
            if (!string.IsNullOrEmpty(response)) Notify(ResponseGenerated, response);
            if (goalEvent.Goal.IsTerminal) intentsByGoalId.Remove(goalEvent.Goal.GoalId);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CommanderIntentDispatcher));
        }
    }
}
