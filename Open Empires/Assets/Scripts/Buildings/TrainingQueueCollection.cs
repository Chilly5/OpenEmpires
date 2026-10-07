using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenEmpires
{
    // Preserves the public integer queue while making every legacy/direct edit
    // observable. Native enqueue/dequeue/cancel keep receipt slots aligned.
    public sealed class TrainingQueueCollection : IList<int>, IReadOnlyList<int>
    {
        private readonly List<int> units = new List<int>();
        private readonly List<TrainingOrderReceipt> receipts = new List<TrainingOrderReceipt>();

        public int Count => units.Count;
        public bool IsReadOnly => false;

        public int this[int index]
        {
            get => units[index];
            set
            {
                InvalidateAttribution();
                units[index] = value;
            }
        }

        public void Add(int item)
        {
            InvalidateAttribution();
            AppendNative(item, null);
        }

        public void AddRange(IEnumerable<int> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var additions = new List<int>(items);
            if (additions.Count == 0) return;
            InvalidateAttribution();
            for (int i = 0; i < additions.Count; i++) AppendNative(additions[i], null);
        }

        public void Clear()
        {
            InvalidateAttribution();
            units.Clear();
            receipts.Clear();
        }

        public bool Contains(int item) => units.Contains(item);
        public void CopyTo(int[] array, int arrayIndex) => units.CopyTo(array, arrayIndex);
        public IEnumerator<int> GetEnumerator() => units.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int IndexOf(int item) => units.IndexOf(item);

        public void Insert(int index, int item)
        {
            if (index < 0 || index > units.Count) throw new ArgumentOutOfRangeException(nameof(index));
            InvalidateAttribution();
            units.Insert(index, item);
            receipts.Insert(index, null);
        }

        public bool Remove(int item)
        {
            int index = units.IndexOf(item);
            if (index < 0) return false;
            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= units.Count) throw new ArgumentOutOfRangeException(nameof(index));
            InvalidateAttribution();
            RemoveAtNative(index, out _);
        }

        internal void AppendNative(int unitType, TrainingOrderReceipt receipt)
        {
            units.Add(unitType);
            receipts.Add(receipt);
        }

        internal int DequeueNative(out TrainingOrderReceipt receipt)
        {
            if (units.Count == 0)
            {
                receipt = null;
                return -1;
            }
            return RemoveAtNative(0, out receipt);
        }

        internal int RemoveAtNative(int index, out TrainingOrderReceipt receipt)
        {
            int unitType = units[index];
            receipt = receipts[index];
            units.RemoveAt(index);
            receipts.RemoveAt(index);
            receipt?.MarkDequeued();
            return unitType;
        }

        private void InvalidateAttribution()
        {
            for (int i = 0; i < receipts.Count; i++)
            {
                receipts[i]?.MarkCancelled();
                receipts[i] = null;
            }
        }
    }
}
