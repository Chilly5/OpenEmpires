using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // One owner per Commander runtime. Allocate before starting asynchronous work.
    public sealed class StrategicIntentIdProvider
    {
        private readonly object sync = new object();
        private long next = 1;
        private static readonly object Unbound = new object();
        // Lifetime claims are independent of the planner's bounded UI/history collections.
        private readonly Dictionary<int, object> owners = new Dictionary<int, object>();

        public int Allocate()
        {
            lock (sync)
            {
                if (next > int.MaxValue)
                    throw new InvalidOperationException("Strategic intent identity space is exhausted.");
                int id = (int)next++;
                owners.Add(id, Unbound);
                return id;
            }
        }

        internal void Observe(int id)
        {
            if (id < 1) throw new ArgumentOutOfRangeException(nameof(id));
            lock (sync) next = Math.Max(next, (long)id + 1);
        }

        internal void BindAllocated(int id, object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            lock (sync)
            {
                if (!owners.TryGetValue(id, out object current) || !ReferenceEquals(current, Unbound))
                    throw new InvalidOperationException("Strategic identity was not allocated for this request.");
                owners[id] = owner;
            }
        }

        internal bool Transfer(int id, object expectedOwner, object newOwner)
        {
            if (expectedOwner == null || newOwner == null) return false;
            lock (sync)
            {
                if (!owners.TryGetValue(id, out object current) || !ReferenceEquals(current, expectedOwner)) return false;
                owners[id] = newOwner;
                return true;
            }
        }

        internal bool TryRegister(StrategicIntent intent)
        {
            lock (sync)
            {
                if (owners.TryGetValue(intent.IntentId, out object owner)) return ReferenceEquals(owner, intent);
                owners.Add(intent.IntentId, intent);
                next = Math.Max(next, (long)intent.IntentId + 1);
                return true;
            }
        }

        internal bool Owns(StrategicIntent intent)
        {
            lock (sync) return owners.TryGetValue(intent.IntentId, out object owner) && ReferenceEquals(owner, intent);
        }
    }
}
