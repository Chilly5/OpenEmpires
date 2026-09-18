using System;

namespace OpenEmpires
{
    public enum StrategicEvaluationTriggerType
    {
        PlayerRequest,
        StrategicEvent,
        WorldStateChange,
        MilestoneCompleted,
        PlanFailed,
        Emergency
    }

    public sealed class StrategicEvaluationTrigger
    {
        public const int DefaultCooldownTicks = 900; // 30 seconds at 30 Hz

        public int CooldownTicks { get; set; }
        public int LastEvaluationTick { get; private set; } = -1;
        public bool HasPendingTrigger { get; private set; }
        public StrategicEvaluationTriggerType PendingTriggerType { get; private set; }
        public string PendingReason { get; private set; } = string.Empty;

        public event Action<StrategicEvaluationTriggerType, string> TriggerFired;

        public StrategicEvaluationTrigger(int cooldownTicks = DefaultCooldownTicks)
        {
            CooldownTicks = Math.Max(0, cooldownTicks);
        }

        public void FireTrigger(StrategicEvaluationTriggerType triggerType, string reason = null)
        {
            if (HasPendingTrigger
                && PendingTriggerType == StrategicEvaluationTriggerType.Emergency
                && triggerType != StrategicEvaluationTriggerType.Emergency)
                return;
            HasPendingTrigger = true;
            PendingTriggerType = triggerType;
            PendingReason = reason ?? triggerType.ToString();
            TriggerFired?.Invoke(triggerType, PendingReason);
        }

        public bool ShouldEvaluate(int currentTick, out StrategicEvaluationTriggerType triggerType, out string reason)
        {
            if (!HasPendingTrigger)
            {
                triggerType = default;
                reason = null;
                return false;
            }

            bool isEmergency = PendingTriggerType == StrategicEvaluationTriggerType.Emergency;
            bool cooldownElapsed = LastEvaluationTick < 0 || currentTick - LastEvaluationTick >= CooldownTicks;

            if (isEmergency || cooldownElapsed)
            {
                triggerType = PendingTriggerType;
                reason = PendingReason;
                return true;
            }

            triggerType = default;
            reason = null;
            return false;
        }

        public void MarkEvaluated(int currentTick)
        {
            LastEvaluationTick = currentTick;
            HasPendingTrigger = false;
            PendingReason = string.Empty;
        }

        public void Reset()
        {
            LastEvaluationTick = -1;
            HasPendingTrigger = false;
            PendingReason = string.Empty;
        }
    }
}
