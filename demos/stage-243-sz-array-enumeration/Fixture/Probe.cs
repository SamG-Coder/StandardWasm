using System;
using System.Collections;
using System.Collections.Generic;

namespace ArrayEnumeration
{
    public sealed class Item
    {
        public readonly int Value;
        public Item(int value) => Value = value;
    }

    public sealed class SingleItem : IEnumerable<Item>, IEnumerator<Item>
    {
        private readonly Item item;
        private bool moved;

        public SingleItem(Item item) => this.item = item;

        public IEnumerator<Item> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;
        public Item Current => item;
        object IEnumerator.Current => Current;
        public bool MoveNext()
        {
            if (moved) return false;
            moved = true;
            return true;
        }
        public void Dispose() { }
        public void Reset() => moved = false;
    }

    public static class Probe
    {
        private static IEnumerator<Item> Open(IEnumerable<Item> source) => source.GetEnumerator();

        public static int NonEmpty()
        {
            var first = new Item(17);
            var last = new Item(19);
            var values = new[] { first, null, last };
            var sequence = Open(values);
            bool good = sequence.MoveNext() && ReferenceEquals(sequence.Current, first) &&
                sequence.MoveNext() && sequence.Current == null;
            var replacement = new Item(23);
            values[2] = replacement;
            good = good && sequence.MoveNext() && ReferenceEquals(sequence.Current, replacement) &&
                !sequence.MoveNext() && !sequence.MoveNext();
            sequence.Dispose();
            return good ? 1009 + replacement.Value : 0;
        }

        public static int Empty()
        {
            var values = new Item[0];
            var first = Open(values);
            var second = Open(values);
            bool good = first != null && ReferenceEquals(first, second) && !first.MoveNext() && !second.MoveNext();
            first.Dispose();
            return good ? 1031 : 0;
        }

        public static int IndependentEnumerators()
        {
            var item = new Item(31);
            var first = Open(new[] { item, new Item(43) });
            var second = Open(new[] { item, new Item(47) });
            bool good = !ReferenceEquals(first, second) && first.MoveNext() && first.MoveNext() &&
                first.Current.Value == 43 && second.MoveNext() && ReferenceEquals(second.Current, item);
            return good ? 1049 : 0;
        }

        public static int InvalidCurrent()
        {
            var sequence = Open(new[] { new Item(53) });
            int count = 0;
            try { _ = sequence.Current; }
            catch (InvalidOperationException) { count++; }
            if (!(sequence.MoveNext() && !sequence.MoveNext())) return 0;
            try { _ = sequence.Current; }
            catch (InvalidOperationException) { count++; }
            return count == 2 ? 1051 : 0;
        }

        public static int DirectArray()
        {
            var item = new Item(71);
            var sequence = ((IEnumerable<Item>)new[] { item }).GetEnumerator();
            return sequence.MoveNext() && ReferenceEquals(sequence.Current, item) &&
                !sequence.MoveNext() ? 1079 : 0;
        }

        public static int MixedDispatch()
        {
            var direct = new Item(61);
            var custom = new Item(67);
            var fromArray = Open(new[] { direct });
            var fromClass = Open(new SingleItem(custom));
            return fromArray.MoveNext() && ReferenceEquals(fromArray.Current, direct) &&
                fromClass.MoveNext() && ReferenceEquals(fromClass.Current, custom) &&
                !fromArray.MoveNext() && !fromClass.MoveNext() ? 1067 : 0;
        }

        private static bool HasAnyObject(IEnumerable<object> source) => source.GetEnumerator().MoveNext();

        public static int CovariantTargetDiagnostic() => HasAnyObject(new[] { new Item(59) }) ? 1069 : 0;

        public static int NullReceiver()
        {
            try { _ = Open(null); }
            catch (NullReferenceException) { return 1061; }
            return 0;
        }
    }
}
