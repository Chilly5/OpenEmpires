using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OpenEmpires
{
    public partial class GameSimulation
    {
        internal event Action<TrainingOrderReceipt> TrainingOrderAccepted;
        internal event Action<TrainingOrderReceipt, int> TrackedUnitProduced;
        internal event Action<ICommand, object> TrainingOriginLost;

        private readonly Dictionary<ICommand, TrainingOrigin> trainingOrigins =
            new Dictionary<ICommand, TrainingOrigin>(CommandObjectComparer.Instance);
        private long nextTrainingOrderOrdinal = 1;

        internal void RegisterTrainingOrigin(ICommand original, object issuer, Func<bool> isCurrent)
        {
            if (!(original is TrainUnitCommand train))
                throw new ArgumentException("Only an original boxed TrainUnitCommand may be registered.", nameof(original));
            if (issuer == null) throw new ArgumentNullException(nameof(issuer));
            if (isCurrent == null) throw new ArgumentNullException(nameof(isCurrent));
            if (trainingOrigins.ContainsKey(original))
                throw new InvalidOperationException("This command object already has a training origin.");
            trainingOrigins.Add(original, new TrainingOrigin(train, issuer, isCurrent));
        }

        internal void DiscardTrainingOrigin(ICommand original) => DiscardTrainingOriginCore(original, false);
        internal void LoseTrainingOrigin(ICommand original) => DiscardTrainingOriginCore(original, true);

        private void DiscardTrainingOriginCore(ICommand original, bool attributionLost)
        {
            if (original == null || !trainingOrigins.TryGetValue(original, out var origin)) return;
            trainingOrigins.Remove(original);
            if (!attributionLost || TrainingOriginLost == null) return;
            foreach (Action<ICommand, object> observer in TrainingOriginLost.GetInvocationList())
            {
                try { observer(original, origin.Issuer); }
                catch (Exception error) { ReportTrainingObservationError(error); }
            }
        }

        internal void DiscardTrainingOriginsForIssuer(object issuer)
        {
            var matching = new List<ICommand>();
            foreach (var pair in trainingOrigins)
                if (ReferenceEquals(pair.Value.Issuer, issuer)) matching.Add(pair.Key);
            foreach (var command in matching) DiscardTrainingOrigin(command);
        }

        internal bool HasPendingTrainingOrigin(ICommand original)
            => original != null && trainingOrigins.ContainsKey(original);

        private TrainingOrigin ConsumeTrainingOrigin(ICommand original, TrainUnitCommand executing)
        {
            if (original == null || !trainingOrigins.TryGetValue(original, out TrainingOrigin origin))
                return null;
            trainingOrigins.Remove(original);
            if (origin.PlayerId != executing.PlayerId || origin.ProducerId != executing.BuildingId
                || origin.RequestedUnitType != executing.UnitType)
                return null;
            try { return origin.IsCurrent() ? origin : null; }
            catch (Exception error)
            {
                ReportTrainingObservationError(error);
                return null;
            }
        }

        private TrainingOrderReceipt NewTrainingReceipt(TrainingOrigin origin, int producerId,
            int playerId, int resolvedUnitType)
        {
            if (origin == null || nextTrainingOrderOrdinal == long.MaxValue) return null;
            return new TrainingOrderReceipt(this, origin.Issuer, producerId, playerId,
                resolvedUnitType, nextTrainingOrderOrdinal++);
        }

        private void PublishTrainingAccepted(TrainingOrderReceipt receipt)
        {
            if (receipt == null) return;
            var handlers = TrainingOrderAccepted;
            if (handlers == null) return;
            foreach (Action<TrainingOrderReceipt> observer in handlers.GetInvocationList())
            {
                try { observer(receipt); }
                catch (Exception error) { ReportTrainingObservationError(error); }
            }
        }

        private void PublishTrackedUnitProduced(TrainingOrderReceipt receipt, int unitId)
        {
            if (receipt == null || !receipt.MarkCompleted()) return;
            var handlers = TrackedUnitProduced;
            if (handlers == null) return;
            foreach (Action<TrainingOrderReceipt, int> observer in handlers.GetInvocationList())
            {
                try { observer(receipt, unitId); }
                catch (Exception error) { ReportTrainingObservationError(error); }
            }
        }

        private static void ReportTrainingObservationError(Exception error)
        {
            string typeName = error.GetType().Name;
            if (typeName.Length > 64) typeName = typeName.Substring(0, 64);
            Debug.LogWarning($"[TrainingObservation] Callback failed ({typeName}).");
        }

        private static ICommand VerifiedOriginal(ICommand replayed,
            IReadOnlyDictionary<ICommand, ICommand> originals)
        {
            if (originals == null) return null;
            // A value comparer on boxed structs is not an identity proof. Only
            // the exact replayed object supplied for this tick can map back.
            foreach (var pair in originals)
                if (ReferenceEquals(pair.Key, replayed)) return pair.Value;
            return null;
        }

        private sealed class TrainingOrigin
        {
            internal readonly int PlayerId;
            internal readonly int ProducerId;
            internal readonly int RequestedUnitType;
            internal readonly object Issuer;
            internal readonly Func<bool> IsCurrent;

            internal TrainingOrigin(TrainUnitCommand command, object issuer, Func<bool> isCurrent)
            {
                PlayerId = command.PlayerId;
                ProducerId = command.BuildingId;
                RequestedUnitType = command.UnitType;
                Issuer = issuer;
                IsCurrent = isCurrent;
            }
        }

        private sealed class CommandObjectComparer : IEqualityComparer<ICommand>
        {
            internal static readonly CommandObjectComparer Instance = new CommandObjectComparer();
            public bool Equals(ICommand x, ICommand y) => ReferenceEquals(x, y);
            public int GetHashCode(ICommand command) => RuntimeHelpers.GetHashCode(command);
        }
    }
}
