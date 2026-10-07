using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OpenEmpires
{
    /// <summary>
    /// Local, runtime-only correlation between a sent command batch and its relay replay.
    /// This never changes a command, a wire payload, or simulation ordering.
    /// </summary>
    internal sealed class CommanderCommandOriginLedger
    {
        private const int MaxFutureTicks = 32;
        private const int MaxBatchEntries = 256;

        private readonly Dictionary<int, Batch> batches = new Dictionary<int, Batch>();

        internal void Record(int tick, int owner, IReadOnlyList<ICommand> originals,
            IReadOnlyList<ICommand> stamped, GameSimulation simulation)
        {
            if (batches.TryGetValue(tick, out Batch previous))
            {
                batches.Remove(tick);
                Discard(previous, simulation);
            }

            if (originals == null || stamped == null || simulation == null
                || originals.Count != stamped.Count || originals.Count > MaxBatchEntries)
            {
                Discard(originals, simulation);
                return;
            }

            // Empty sends have no candidate origin. NetworkManager still sends its Noop.
            if (originals.Count == 0) return;

            var savedOriginals = new ICommand[originals.Count];
            var fingerprints = new Fingerprint[stamped.Count];
            for (int i = 0; i < stamped.Count; i++)
            {
                savedOriginals[i] = originals[i];
                if (savedOriginals[i] == null || !TryFingerprint(stamped[i], out fingerprints[i])
                    || fingerprints[i].Owner != owner)
                {
                    Discard(originals, simulation);
                    return;
                }
            }

            batches.Add(tick, new Batch(simulation, owner, savedOriginals, fingerprints));
            while (batches.Count > MaxFutureTicks)
            {
                int oldest = int.MaxValue;
                foreach (int pendingTick in batches.Keys)
                    if (pendingTick < oldest) oldest = pendingTick;
                Batch abandoned = batches[oldest];
                batches.Remove(oldest);
                Discard(abandoned, simulation);
            }
        }

        internal IReadOnlyDictionary<ICommand, ICommand> Consume(int tick, int owner,
            IReadOnlyList<ICommand> received, GameSimulation simulation)
        {
            PruneBefore(tick, simulation);
            var verified = new Dictionary<ICommand, ICommand>(CommandReferenceComparer.Instance);
            if (!batches.TryGetValue(tick, out Batch batch)) return verified;
            batches.Remove(tick);

            if (simulation == null || !ReferenceEquals(batch.Runtime, simulation)
                || batch.Owner != owner || received == null)
            {
                Discard(batch, simulation);
                return verified;
            }

            // SendCommands appends exactly one local Noop after the real batch.
            // Keep every command in received; this slice exists only to verify origins.
            var local = new List<ICommand>();
            for (int i = 0; i < received.Count; i++)
            {
                ICommand command = received[i];
                if (command != null && command.PlayerId == owner) local.Add(command);
            }

            if (local.Count != batch.Originals.Length + 1
                || !(local[local.Count - 1] is NoopCommand))
            {
                Discard(batch, simulation);
                return verified;
            }

            for (int i = 0; i < batch.Originals.Length; i++)
            {
                if (local[i] is NoopCommand || !TryFingerprint(local[i], out Fingerprint replayed)
                    || !batch.Stamped[i].Equals(replayed))
                {
                    Discard(batch, simulation);
                    return verified;
                }
            }

            // Without an explicit relay FIFO contract, equal payloads from distinct
            // original boxes cannot be assigned to their true issuers by ordinal.
            for (int i = 0; i < batch.Stamped.Length; i++)
            {
                for (int j = i + 1; j < batch.Stamped.Length; j++)
                {
                    if (batch.Stamped[i].Equals(batch.Stamped[j])
                        && !ReferenceEquals(batch.Originals[i], batch.Originals[j])
                        && (batch.Originals[i] is PlaceBuildingCommand
                            || batch.Originals[j] is PlaceBuildingCommand
                            || simulation.HasPendingTrainingOrigin(batch.Originals[i])
                            || simulation.HasPendingTrainingOrigin(batch.Originals[j])))
                    {
                        Discard(batch, simulation);
                        return verified;
                    }
                }
            }

            for (int i = 0; i < batch.Originals.Length; i++)
            {
                ICommand original = batch.Originals[i];
                if (!(original is PlaceBuildingCommand)
                    && (!(original is TrainUnitCommand) || !simulation.HasPendingTrainingOrigin(original)))
                    continue;
                // A replay box must identify only one original box in this tick.
                if (verified.ContainsKey(local[i]))
                {
                    verified.Clear();
                    Discard(batch, simulation);
                    return verified;
                }
                verified.Add(local[i], original);
            }
            return verified;
        }

        internal void PruneBefore(int tick, GameSimulation simulation)
        {
            var abandonedTicks = new List<int>();
            foreach (int pendingTick in batches.Keys)
                if (pendingTick < tick) abandonedTicks.Add(pendingTick);
            foreach (int abandonedTick in abandonedTicks)
            {
                Batch abandoned = batches[abandonedTick];
                batches.Remove(abandonedTick);
                Discard(abandoned, simulation);
            }
        }

        internal void Clear(GameSimulation simulation)
        {
            foreach (Batch batch in batches.Values)
                Discard(batch, simulation);
            batches.Clear();
        }

        private static bool TryFingerprint(ICommand command, out Fingerprint fingerprint)
        {
            fingerprint = default;
            if (command == null) return false;
            try
            {
                var serialized = CommandSerializer.ToJson(command);
                fingerprint = new Fingerprint(command.GetType(), command.Type,
                    command.PlayerId, serialized.commandType, serialized.payload);
                return true;
            }
            catch (Exception)
            {
                // Unsupported or malformed commands can still follow the existing
                // gameplay/network path, but they cannot convey a local origin.
                return false;
            }
        }

        private static void Discard(Batch batch, GameSimulation fallback)
            => Discard(batch.Originals, batch.Runtime ?? fallback);

        private static void Discard(IReadOnlyList<ICommand> originals, GameSimulation simulation)
        {
            if (originals == null || simulation == null) return;
            for (int i = 0; i < originals.Count; i++)
                simulation.LoseTrainingOrigin(originals[i]);
        }

        private sealed class Batch
        {
            internal readonly GameSimulation Runtime;
            internal readonly int Owner;
            internal readonly ICommand[] Originals;
            internal readonly Fingerprint[] Stamped;

            internal Batch(GameSimulation runtime, int owner, ICommand[] originals, Fingerprint[] stamped)
            {
                Runtime = runtime;
                Owner = owner;
                Originals = originals;
                Stamped = stamped;
            }
        }

        private readonly struct Fingerprint : IEquatable<Fingerprint>
        {
            internal readonly Type RuntimeType;
            internal readonly CommandType Kind;
            internal readonly int Owner;
            internal readonly string SerializedType;
            internal readonly string Payload;

            internal Fingerprint(Type runtimeType, CommandType kind, int owner,
                string serializedType, string payload)
            {
                RuntimeType = runtimeType;
                Kind = kind;
                Owner = owner;
                SerializedType = serializedType;
                Payload = payload;
            }

            public bool Equals(Fingerprint other)
                => RuntimeType == other.RuntimeType && Kind == other.Kind && Owner == other.Owner
                    && string.Equals(SerializedType, other.SerializedType, StringComparison.Ordinal)
                    && string.Equals(Payload, other.Payload, StringComparison.Ordinal);
        }

        private sealed class CommandReferenceComparer : IEqualityComparer<ICommand>
        {
            internal static readonly CommandReferenceComparer Instance = new CommandReferenceComparer();
            public bool Equals(ICommand x, ICommand y) => ReferenceEquals(x, y);
            public int GetHashCode(ICommand command) => RuntimeHelpers.GetHashCode(command);
        }
    }
}
