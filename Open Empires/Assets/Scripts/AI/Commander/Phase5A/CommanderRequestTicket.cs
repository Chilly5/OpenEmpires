using System;

namespace OpenEmpires
{
    // Local correlation exists before interpretation, but grants no execution authority.
    // The ticket can bind one candidate only; no provider field can supply this object.
    internal sealed class CommanderRequestTicket
    {
        internal long Id { get; }
        internal CommanderGoalManager Owner { get; }
        internal GameSimulation Runtime { get; }
        internal string OriginalInput { get; }
        internal int Generation { get; }
        private Func<bool> current;
        private bool claimed;

        internal CommanderRequestTicket(long id, CommanderGoalManager owner, string input,
            int generation, Func<bool> current)
        {
            Id = id; Owner = owner; Runtime = owner.Simulation;
            OriginalInput = input; Generation = generation; this.current = current;
        }

        internal bool IsCurrent()
        {
            try { return !Owner.IsDisposed && ReferenceEquals(Owner.Simulation, Runtime)
                && (current?.Invoke() ?? true); }
            catch (Exception) { return false; }
        }

        // A retained local clarification is a new UI turn of the same player
        // request. Its old turn-revision predicate must not authorize admission,
        // but the ticket's identity and original utterance must remain stable.
        internal bool BindLocalContinuation(CommanderGoalManager owner, int generation,
            Func<bool> currentTurn)
        {
            if (claimed || !ReferenceEquals(owner, Owner) || generation != Generation
                || Owner.IsDisposed || !ReferenceEquals(Runtime, owner.Simulation)
                || currentTurn == null) return false;
            try { if (!currentTurn()) return false; }
            catch (Exception) { return false; }
            current = currentTurn;
            return true;
        }

        internal bool Claim(CommanderGoalManager owner, string input, int generation)
        {
            if (claimed || !ReferenceEquals(owner, Owner) || generation != Generation
                || input != OriginalInput || !IsCurrent()) return false;
            claimed = true;
            return true;
        }
    }
}
