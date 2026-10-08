using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager
    {
        private readonly List<ICommand> pendingTrainingOrigins = new List<ICommand>();

        private void ReleaseGoalTrainingOrigins(CommanderGoal goal)
        {
            simulation.DiscardTrainingOriginsForIssuer(goal);
            pendingTrainingOrigins.RemoveAll(c => !simulation.HasPendingTrainingOrigin(c));
        }

        private void HandleTrainingOriginLost(ICommand command, object issuer)
        {
            if (!disposed && issuer is EnsureUnitCountGoal goal && !goal.IsTerminal
                && ReferenceEquals(goal.RuntimeOwner, this) && ReferenceEquals(GetGoal(goal.GoalId), goal))
                goal.TrainingAttributionUnavailable = true;
            pendingTrainingOrigins.RemoveAll(c => !simulation.HasPendingTrainingOrigin(c));
        }

        private void TrackIssuedTraining(CommanderGoal goal, ICommand command)
        {
            if (!(goal is EnsureUnitCountGoal units)
                || !(command is TrainUnitCommand train)) return;
            pendingTrainingOrigins.RemoveAll(c => !simulation.HasPendingTrainingOrigin(c));
            if (pendingTrainingOrigins.Count >= 512)
                throw new InvalidOperationException("Training observation origin bound reached.");
            simulation.RegisterTrainingOrigin(command, units,
                () => !disposed && !units.IsTerminal && ReferenceEquals(units.RuntimeOwner, this)
                    && ReferenceEquals(GetGoal(units.GoalId), units));
            pendingTrainingOrigins.Add(command);
        }

        private void HandleTrainingAccepted(TrainingOrderReceipt receipt)
        {
            if (!OwnedTrainingReceipt(receipt, out var goal)) return;
            if ((goal.RequiredProducerGoal != null
                    && !System.Linq.Enumerable.Contains(goal.RequiredProducerGoal.ResultBuildingIds, receipt.ProducerId))
                || (goal.BoundProducerBuildingIds != null
                    && !System.Linq.Enumerable.Contains(goal.BoundProducerBuildingIds, receipt.ProducerId))) return;
            if (goal.TrackedTrainingOrders.Contains(receipt)) return;
            goal.TrackedTrainingOrders.Add(receipt);
            // Dispatched origins are no longer needed. The receipt now owns queue identity.
            pendingTrainingOrigins.RemoveAll(c => !simulation.HasPendingTrainingOrigin(c));
        }

        private bool OwnedTrainingReceipt(TrainingOrderReceipt receipt, out EnsureUnitCountGoal goal)
        {
            goal = receipt?.Issuer as EnsureUnitCountGoal;
            return !disposed && goal != null && !goal.IsTerminal
                && ReferenceEquals(receipt.Runtime, simulation) && ReferenceEquals(goal.RuntimeOwner, this)
                && ReferenceEquals(GetGoal(goal.GoalId), goal) && receipt.PlayerId == playerId
                && receipt.ResolvedUnitType == simulation.ResolveCivUnitType(playerId, goal.RequestedUnitType);
        }

        private void HandleTrackedUnitProduced(TrainingOrderReceipt receipt, int unitId)
        {
            if (!OwnedTrainingReceipt(receipt, out var goal) || receipt.IsCancelled || !receipt.IsCompleted)
                return;
            var unit = simulation.UnitRegistry.GetUnit(unitId);
            if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
                || unit.State == UnitState.Dead || unit.UnitType != receipt.ResolvedUnitType
                || goal.AttributedUnitIds.Contains(unitId)) return;
            goal.AttributedUnitIds.Add(unitId);
        }

        private void ReleaseTrainingObservations()
        {
            foreach (var command in pendingTrainingOrigins) simulation.DiscardTrainingOrigin(command);
            pendingTrainingOrigins.Clear();
            simulation.TrainingOrderAccepted -= HandleTrainingAccepted;
            simulation.TrackedUnitProduced -= HandleTrackedUnitProduced;
            simulation.TrainingOriginLost -= HandleTrainingOriginLost;
        }
    }
}
