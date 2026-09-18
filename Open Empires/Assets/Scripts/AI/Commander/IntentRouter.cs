using System;

namespace OpenEmpires
{
    public enum IntentRouteStatus
    {
        Routed,
        Rejected
    }

    public sealed class IntentRouteResult
    {
        public IntentRouteStatus Status { get; }
        public bool Success => Status == IntentRouteStatus.Routed;
        public CommanderIntentLayer Layer { get; }
        public string Reason { get; }
        public CommanderIntentSubmission TacticalSubmission { get; }
        public StrategicIntentSubmission StrategicSubmission { get; }

        private IntentRouteResult(IntentRouteStatus status, CommanderIntentLayer layer, string reason,
            CommanderIntentSubmission tacticalSubmission, StrategicIntentSubmission strategicSubmission)
        {
            Status = status;
            Layer = layer;
            Reason = reason ?? string.Empty;
            TacticalSubmission = tacticalSubmission;
            StrategicSubmission = strategicSubmission;
        }

        public static IntentRouteResult Tactical(CommanderIntentSubmission submission) =>
            new IntentRouteResult(submission != null && submission.CreatedGoal
                    ? IntentRouteStatus.Routed : IntentRouteStatus.Rejected,
                CommanderIntentLayer.Tactical, submission?.Response, submission, null);

        public static IntentRouteResult Strategic(StrategicIntentSubmission submission) =>
            new IntentRouteResult(submission != null && submission.IsAccepted ? IntentRouteStatus.Routed : IntentRouteStatus.Rejected,
                CommanderIntentLayer.Strategic, submission?.Reason, null, submission);

        public static IntentRouteResult Rejected(CommanderIntentLayer layer, string reason) =>
            new IntentRouteResult(IntentRouteStatus.Rejected, layer, reason, null, null);
    }

    public sealed class IntentRouter
    {
        private readonly Func<CommanderIntent, CommanderIntentSubmission> tacticalSubmitter;
        public int PlayerId { get; }
        public CommanderIntentDispatcher TacticalDispatcher { get; }
        public StrategicPlanner StrategicPlanner { get; }

        public IntentRouter(int playerId,
            CommanderIntentDispatcher tacticalDispatcher = null,
            StrategicPlanner strategicPlanner = null)
        {
            if (playerId < 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            PlayerId = playerId;
            TacticalDispatcher = tacticalDispatcher;
            StrategicPlanner = strategicPlanner;
            tacticalSubmitter = tacticalDispatcher == null
                ? null
                : new Func<CommanderIntent, CommanderIntentSubmission>(tacticalDispatcher.SubmitIntent);
        }

        internal IntentRouter(int playerId,
            Func<CommanderIntent, CommanderIntentSubmission> tacticalSubmitter,
            StrategicPlanner strategicPlanner)
        {
            if (playerId < 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            PlayerId = playerId;
            this.tacticalSubmitter = tacticalSubmitter;
            StrategicPlanner = strategicPlanner;
        }

        public IntentRouteResult Route(ICommanderIntentRequest request)
        {
            return Route(request, strategicIsPlayerOverride: false);
        }

        internal IntentRouteResult Route(ICommanderIntentRequest request,
            bool strategicIsPlayerOverride)
        {
            if (request == null)
                return IntentRouteResult.Rejected(CommanderIntentLayer.Tactical, "No intent request supplied.");

            switch (request)
            {
                case CommanderIntent tactical:
                    if (tacticalSubmitter == null)
                        return IntentRouteResult.Rejected(CommanderIntentLayer.Tactical, "No tactical dispatcher configured.");
                    CommanderIntentSubmission tacticalResult = tacticalSubmitter(tactical);
                    return IntentRouteResult.Tactical(tacticalResult);

                case StrategicIntent strategic:
                    if (StrategicPlanner == null)
                        return IntentRouteResult.Rejected(CommanderIntentLayer.Strategic, "No strategic planner configured.");
                    if (strategic.PlayerId != PlayerId || StrategicPlanner.PlayerId != PlayerId)
                        return IntentRouteResult.Rejected(CommanderIntentLayer.Strategic,
                            "Strategic intent player mismatch.");
                    StrategicDecisionRecord evaluation = StrategicPlanner.GetOrCreatePipeline()
                        .EvaluatePlayerIntentNow(strategic);
                    return evaluation.Submission != null
                        ? IntentRouteResult.Strategic(evaluation.Submission)
                        : IntentRouteResult.Rejected(CommanderIntentLayer.Strategic, evaluation.Outcome);

                default:
                    return IntentRouteResult.Rejected(request.IntentLayer,
                        $"Unknown intent request type: {request.GetType().Name}");
            }
        }
    }
}
