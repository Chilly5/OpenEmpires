using System;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager
    {
        private void HandleNativeMoveProcessed(NativeMoveProcessingReceipt receipt)
        {
            if (disposed || receipt == null || !ReferenceEquals(receipt.Runtime, simulation)) return;
            for (int g = 0; g < activeGoals.Count; g++)
            {
                if (!(activeGoals[g] is CommanderCapabilityGoal goal) || goal.IsTerminal
                    || !goal.CommandIssued || !ReferenceEquals(goal.RuntimeOwner, this)
                    || !(goal.IssuedCommand is MoveCommand move) || move.UnitIds == null) continue;
                int index = Array.IndexOf(move.UnitIds, receipt.UnitId);
                if (index < 0 || goal.NativeMovementProcessed == null
                    || index >= goal.NativeMovementProcessed.Length) continue;
                if (!ReferenceEquals(receipt.OriginalCommand, goal.IssuedCommand))
                {
                    if (goal.NativeMovementProcessed[index]) goal.NativeMovementReplaced = true;
                    continue;
                }
                if (receipt.Tick < goal.CommandIssuedSimulationTick
                    || goal.NativeMovementProcessed[index])
                { goal.NativeMovementReplaced = true; continue; }
                goal.NativeMovementProcessed[index] = true;
                goal.NativeMovementEndpoints[index] = receipt.Endpoint;
                goal.NativeMovementResults[index] = receipt.Result;
            }
        }

        private void HandleNativeUnitCommandProcessed(NativeUnitCommandObservation observation)
        {
            if (disposed || observation == null || !ReferenceEquals(observation.Runtime, simulation)) return;
            int[] subjects = CommanderWorkerAuthority.GetSubjectUnitIds(observation.ExecutingCommand);
            if (subjects == null || subjects.Length == 0) return;
            for (int g = 0; g < activeGoals.Count; g++)
            {
                if (!(activeGoals[g] is CommanderCapabilityGoal goal) || goal.IsTerminal
                    || !goal.CommandIssued || !ReferenceEquals(goal.RuntimeOwner, this)
                    || ReferenceEquals(observation.OriginalCommand, goal.IssuedCommand)
                    || !(goal.IssuedCommand is MoveCommand move) || move.UnitIds == null
                    || goal.NativeMovementProcessed == null) continue;
                for (int i = 0; i < move.UnitIds.Length; i++)
                    if (goal.NativeMovementProcessed[i] && Array.IndexOf(subjects, move.UnitIds[i]) >= 0)
                    { goal.NativeMovementReplaced = true; break; }
            }
        }
    }
}
