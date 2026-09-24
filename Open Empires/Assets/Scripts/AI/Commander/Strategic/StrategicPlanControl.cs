using System;

namespace OpenEmpires
{
    public enum StrategicPlanControlType { Pause, Resume, Cancel, Status }

    public enum StrategicPlanControlStatus
    {
        Applied,
        NoActivePlan,
        AmbiguousPlan,
        AlreadyPaused,
        AlreadyRunning,
        AlreadyCompleted,
        AlreadyCancelled,
        StalePlan,
        Unauthorized,
        Rejected
    }

    // A copied observation, not a reference to a plan or an authority to submit goals.
    public readonly struct StrategicPlanControlRequest
    {
        public int PlayerId { get; }
        public int PlanId { get; }
        public int CreatedTick { get; }
        public int ObservedRevision { get; }
        public StrategicPlanControlType ControlType { get; }

        internal StrategicPlanControlRequest(int playerId, int planId, int createdTick,
            int observedRevision, StrategicPlanControlType controlType)
        {
            PlayerId = playerId;
            PlanId = planId;
            CreatedTick = createdTick;
            ObservedRevision = observedRevision;
            ControlType = controlType;
        }
    }

    public readonly struct StrategicPlanControlResult
    {
        public StrategicPlanControlStatus Status { get; }
        public int PlayerId { get; }
        public int PlanId { get; }
        public int CreatedTick { get; }
        public int Revision { get; }
        public StrategicPlanStatus PlanStatus { get; }
        public string Message { get; }

        internal StrategicPlanControlResult(StrategicPlanControlStatus status,
            StrategicPlanControlRequest request, StrategicPlan plan, string message)
        {
            Status = status;
            PlayerId = request.PlayerId;
            PlanId = request.PlanId;
            CreatedTick = request.CreatedTick;
            Revision = plan?.Revision ?? request.ObservedRevision;
            PlanStatus = plan?.Status ?? StrategicPlanStatus.Created;
            Message = message ?? string.Empty;
        }
    }
}
