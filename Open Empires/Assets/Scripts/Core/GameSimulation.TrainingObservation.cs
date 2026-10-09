using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OpenEmpires
{
    // Local observation only; never serialized or consulted by simulation decisions.
    internal sealed class ProducerBirthObservation
    {
        internal GameSimulation Runtime { get; }
        internal BuildingData Producer { get; }
        internal int ProducerId { get; }
        internal int PlayerId { get; }
        internal int UnitType { get; }
        internal int UnitId { get; }
        internal int Tick { get; }
        internal long BirthOrdinal { get; }

        internal ProducerBirthObservation(GameSimulation runtime, BuildingData producer,
            int playerId, int unitType, int unitId, int tick, long birthOrdinal = 0)
        {
            Runtime = runtime;
            Producer = producer;
            ProducerId = producer?.Id ?? -1;
            PlayerId = playerId;
            UnitType = unitType;
            UnitId = unitId;
            Tick = tick;
            BirthOrdinal = birthOrdinal;
        }
    }

    public partial class GameSimulation
    {
        internal event Action<ProducerBirthObservation> ProducerUnitProduced;
        internal event Action<TrainingOrderReceipt> TrainingOrderAccepted;
        internal event Action<TrainingOrderReceipt, int> TrackedUnitProduced;
        internal event Action<ICommand, object> TrainingOriginLost;
        internal event Action<ICommand> LocalActionCommandProcessed;

        private readonly Dictionary<ICommand, TrainingOrigin> trainingOrigins =
            new Dictionary<ICommand, TrainingOrigin>(CommandObjectComparer.Instance);
        private readonly HashSet<ICommand> suppressedLocalActions =
            new HashSet<ICommand>(CommandObjectComparer.Instance);
        private long lastProducerBirthOrdinal;
        internal long LastProducerBirthOrdinal => lastProducerBirthOrdinal;
        private long nextTrainingOrderOrdinal = 1;

        private void PublishProducerUnitProduced(BuildingData producer, int playerId,
            int unitType, int unitId)
        {
            // Local observation revision, not simulation authority or serialized state.
            // Saturation fails closed: later births cannot reuse a fresh ordinal.
            if (lastProducerBirthOrdinal < long.MaxValue) lastProducerBirthOrdinal++;
            var handlers = ProducerUnitProduced;
            if (handlers == null) return;
            var observation = new ProducerBirthObservation(this, producer, playerId,
                unitType, unitId, CurrentTick, lastProducerBirthOrdinal);
            foreach (Action<ProducerBirthObservation> observer in handlers.GetInvocationList())
            {
                try { observer(observation); }
                catch (Exception error) { ReportTrainingObservationError(error); }
            }
        }

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

        // Read-only game-owned in-flight orders. Count before native acceptance so
        // lockstep/input delay cannot spend the same result/queue capacity twice.
        internal int CountPendingTrainingOrders(object issuer = null, int producerId = -1,
            int playerId = -1, int resolvedType = -1)
        {
            int count = 0;
            foreach (var origin in trainingOrigins.Values)
                if ((issuer == null || ReferenceEquals(origin.Issuer, issuer))
                    && (producerId < 0 || origin.ProducerId == producerId)
                    && (playerId < 0 || origin.PlayerId == playerId)
                    && (resolvedType < 0 || ResolveCivUnitType(origin.PlayerId, origin.RequestedUnitType) == resolvedType)) count++;
            return count;
        }

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

        private void PublishLocalActionCommandProcessed(ICommand command)
        {
            var handlers = LocalActionCommandProcessed;
            if (handlers == null) return;
            foreach (Action<ICommand> observer in handlers.GetInvocationList())
            {
                try { observer(command); }
                catch (Exception error) { ReportTrainingObservationError(error); }
            }
        }

        internal void SuppressUnprocessedLocalAction(ICommand command)
        {
            if (command != null) suppressedLocalActions.Add(command);
        }

        private bool ConsumeSuppressedLocalAction(ICommand command)
            => command != null && suppressedLocalActions.Remove(command);

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
