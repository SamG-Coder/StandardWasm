using System.Runtime.CompilerServices;

namespace InteriorWitness
{
    internal struct Entry<T>
    {
        internal int Key;
        internal T Value;
    }

    internal sealed class Holder<T>
    {
        internal Entry<T> Entry;
    }

    internal struct Payload
    {
        internal Leaf Leaf;
        internal string Text;
        internal int Neighbor;
    }

    internal struct Leaf
    {
        internal int Count;
    }

    internal struct ObjectAndNumber
    {
        internal object Token;
        internal int Number;
    }

    public static class Probe
    {
        public static int ScalarAlias()
        {
            var entries = new Entry<int>[1];
            ref Entry<int> entry = ref entries[0];
            ref int alias = ref entry.Value;
            alias = 23;
            Entry<int> copy = entry;
            entry = default;
            int replacement = alias;
            alias = 59;
            return replacement * 10000 + entries[0].Value * 100 + copy.Value + entry.Key;
        }

        public static int ReferenceAlias()
        {
            var entries = new Entry<string>[1];
            ref Entry<string> entry = ref entries[0];
            ref string alias = ref entry.Value;
            alias = "old";
            Entry<string> copy = entry;
            entry = default;
            int replacement = alias == null ? 1 : 0;
            alias = "fresh";
            return replacement * 10000 + entries[0].Value.Length * 100 + copy.Value.Length + entry.Key;
        }

        public static int GenericCallAndArrayAlias()
        {
            var entries = new Entry<int>[1];
            var scalars = new int[1];
            ref int projected = ref ValueRef(ref entries[0]);
            ref int arrayElement = ref scalars[0];
            int first = Swap(ref projected, -2147483648);
            int second = Swap(ref arrayElement, 149);
            int third = Swap(ref projected, 71);
            return first + second + (third == -2147483648 ? 1000 : 0) + scalars[0] + entries[0].Value;
        }

        public static int GenericCallAndClassAlias()
        {
            var holder = new Holder<string>();
            ref string projected = ref ValueRef(ref holder.Entry);
            ref string second = ref ValueRef(ref holder.Entry);
            projected = "retained";
            Entry<string> copy = holder.Entry;
            holder.Entry = default;
            int prior = projected == null ? 1 : 0;
            projected = "new";
            return prior * 10000 + second.Length * 100 + copy.Value.Length;
        }

        public static int NestedAggregateAlias()
        {
            var entries = new Entry<Payload>[1];
            ref Payload payload = ref ValueRef(ref entries[0]);
            ref Leaf leaf = ref payload.Leaf;
            ref int count = ref leaf.Count;
            count = 23;
            Payload first = payload;
            payload = new Payload { Leaf = new Leaf { Count = 31 }, Neighbor = 17 };
            int afterPayloadStore = count;
            leaf = new Leaf { Count = 41 };
            int afterLeafStore = count;
            count = 47;
            int beforeWholeStore = entries[0].Value.Leaf.Count + payload.Neighbor;
            entries[0] = default;
            int afterWholeStore = count;
            count = 59;
            return first.Leaf.Count + afterPayloadStore * 100 + afterLeafStore * 1000 +
                beforeWholeStore + afterWholeStore * 10000 + payload.Leaf.Count;
        }

        public static int AggregateReferenceAlias()
        {
            var holder = new Holder<Payload>();
            ref Payload payload = ref ValueRef(ref holder.Entry);
            ref string text = ref payload.Text;
            text = "first";
            Payload old = payload;
            payload = new Payload { Text = "replacement", Neighbor = 17 };
            int observed = text.Length;
            text = "updated";
            int current = holder.Entry.Value.Text.Length + payload.Neighbor;
            holder.Entry = default;
            int cleared = text == null ? 1 : 0;
            text = "again";
            return old.Text.Length + observed * 100 + current + cleared * 10000 + payload.Text.Length;
        }

        public static int DescriptorShapedSnapshot()
        {
            var entries = new Entry<ObjectAndNumber>[1];
            ref ObjectAndNumber value = ref ValueRef(ref entries[0]);
            var token = new object();
            value = new ObjectAndNumber { Token = token, Number = 47 };
            ObjectAndNumber copy = value;
            value = default;
            if (value.Token != null || value.Number != 0 || copy.Token != token) return 0;
            return copy.Number;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T ValueRef<T>(ref Entry<T> entry) => ref entry.Value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Swap(ref int value, int replacement)
        {
            int previous = value;
            value = replacement;
            return previous;
        }
    }
}
