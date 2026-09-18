using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    public sealed class CommanderAIChatSubmission
    {
        public CommanderAIProviderResult ProviderResult { get; }
        public CommanderIntentInterpretation Interpretation { get; }
        public CommanderIntentSubmission CommanderSubmission { get; }
        public string DisplayText { get; }
        public bool Success => CommanderSubmission != null
            && CommanderSubmission.Interpretation != null
            && CommanderSubmission.Interpretation.Success
            && CommanderSubmission.CreatedGoal;

        public CommanderAIChatSubmission(CommanderAIProviderResult providerResult,
            CommanderIntentInterpretation interpretation,
            CommanderIntentSubmission commanderSubmission, string displayText)
        {
            ProviderResult = providerResult;
            Interpretation = interpretation;
            CommanderSubmission = commanderSubmission;
            DisplayText = displayText ?? string.Empty;
        }
    }

    public sealed class CommanderAIIntentAdapter
    {
        public static readonly TimeSpan DefaultProviderTimeout = TimeSpan.FromSeconds(15);

        private readonly ICommanderAIProvider provider;
        private readonly GameSimulation simulation;
        private readonly CommanderGoalManager goalManager;
        private readonly CommanderIntentDispatcher dispatcher;
        private readonly TimeSpan providerTimeout;
        private bool isSubmitting;
        private int generation;

        public CommanderConversationHistory History { get; }
        public bool IsSubmitting => isSubmitting;
        public void ResetHistory()
        {
            generation++;
            isSubmitting = false;
            History.Clear();
        }

        public CommanderAIIntentAdapter(ICommanderAIProvider provider,
            GameSimulation simulation, CommanderGoalManager goalManager,
            CommanderIntentDispatcher dispatcher,
            CommanderConversationHistory history = null,
            TimeSpan? providerTimeout = null)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.goalManager = goalManager ?? throw new ArgumentNullException(nameof(goalManager));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.providerTimeout = providerTimeout ?? DefaultProviderTimeout;
            if (this.providerTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(providerTimeout));
            History = history ?? new CommanderConversationHistory();
        }

        public async Task<CommanderAIChatSubmission> SubmitAsync(string playerMessage,
            CancellationToken cancellationToken = default)
        {
            if (isSubmitting)
                return Reject(CommanderIntentErrorCode.SubmissionInProgress,
                    "A Commander translation is already in progress.");
            if (string.IsNullOrWhiteSpace(playerMessage))
                return Reject(CommanderIntentErrorCode.EmptyInput,
                    "Enter a Commander order first.");

            isSubmitting = true;
            int requestGeneration = generation;
            string trimmed = playerMessage.Trim();
            CancellationTokenSource requestCancellation = null;
            try
            {
                CommanderContext context = new CommanderContextBuilder().Build(simulation,
                    goalManager);
                var request = new CommanderAIRequest(trimmed, context, History.Snapshot());
                History.Append(CommanderConversationRole.Player, trimmed);

                requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                requestCancellation.CancelAfter(providerTimeout);
                CommanderAIProviderResult providerResult = await provider.TranslateAsync(
                    request, requestCancellation.Token);
                if (requestGeneration != generation || cancellationToken.IsCancellationRequested)
                    return Reject(CommanderIntentErrorCode.Cancelled,
                        "Commander translation was cancelled.");
                if (requestCancellation.IsCancellationRequested)
                    return RejectAndRemember(CommanderIntentErrorCode.Cancelled,
                        "Commander AI request timed out. Please try again or use offline commands.");
                if (providerResult == null)
                    return RejectAndRemember(CommanderIntentErrorCode.ProviderFailure,
                        "The AI provider returned no result.");
                if (!providerResult.Success || providerResult.IntentDto == null)
                {
                    string text = string.IsNullOrWhiteSpace(providerResult.AIResponseText)
                        ? "I could not translate that into a safe Commander order."
                        : providerResult.AIResponseText;
                    History.Append(CommanderConversationRole.Commander, text);
                    return new CommanderAIChatSubmission(providerResult, null, null, text);
                }

                // This is the mandatory trusted DTO boundary. Provider output cannot
                // reach the dispatcher without being converted and validated here.
                CommanderIntentInterpretation interpretation =
                    CommanderIntentDtoCodec.ValidateAndConvert(providerResult.IntentDto, context);
                if (!interpretation.Success || interpretation.Intent == null
                    || interpretation.StrategicIntent != null)
                {
                    string text = "The translated order was rejected by Commander validation.";
                    History.Append(CommanderConversationRole.Commander, text);
                    return new CommanderAIChatSubmission(providerResult, interpretation, null, text);
                }

                CommanderIntentSubmission submission = dispatcher.SubmitIntent(
                    interpretation.Intent);
                string display = submission.CreatedGoal
                    ? providerResult.AIResponseText
                    : submission.Response;
                if (string.IsNullOrWhiteSpace(display)) display = "Commander order submitted.";
                History.Append(CommanderConversationRole.Commander, display);
                return new CommanderAIChatSubmission(providerResult, interpretation,
                    submission, display);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                && requestGeneration == generation
                && requestCancellation != null && requestCancellation.IsCancellationRequested)
            {
                return RejectAndRemember(CommanderIntentErrorCode.Cancelled,
                    "Commander AI request timed out. Please try again or use offline commands.");
            }
            catch (OperationCanceledException)
            {
                return requestGeneration != generation || cancellationToken.IsCancellationRequested
                    ? Reject(CommanderIntentErrorCode.Cancelled,
                        "Commander translation was cancelled.")
                    : RejectAndRemember(CommanderIntentErrorCode.Cancelled,
                    "Commander translation was cancelled.");
            }
            catch (Exception)
            {
                return requestGeneration != generation || cancellationToken.IsCancellationRequested
                    ? Reject(CommanderIntentErrorCode.Cancelled,
                        "Commander translation was cancelled.")
                    : RejectAndRemember(CommanderIntentErrorCode.ProviderFailure,
                    "Commander translation failed safely; no order was submitted.");
            }
            finally
            {
                requestCancellation?.Dispose();
                if (requestGeneration == generation) isSubmitting = false;
            }
        }

        private CommanderAIChatSubmission RejectAndRemember(
            CommanderIntentErrorCode code, string text)
        {
            History.Append(CommanderConversationRole.Commander, text);
            return Reject(code, text);
        }

        private static CommanderAIChatSubmission Reject(
            CommanderIntentErrorCode code, string text)
        {
            var providerResult = CommanderAIProviderResult.Rejected(code, text, text);
            return new CommanderAIChatSubmission(providerResult,
                CommanderIntentInterpretation.Rejected(code, text), null, text);
        }
    }
}
