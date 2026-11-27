using System;

namespace InlineSpanFieldDefaults
{
    public static class Probe
    {
        public static int WritableField()
        {
            char[] data = Make();
            var owner = new Holder(new Span<char>(data));
            owner.ClearWritable();
            return owner.WriteLength() == 0 && owner.ReadLength() == 3 && owner.Marker == 73 && data[1] == 'b' ? 1 : 0;
        }

        public static int ReadOnlyField()
        {
            char[] data = Make();
            var owner = new Holder(new Span<char>(data));
            owner.ClearReadOnly();
            owner.Write('z');
            return owner.ReadLength() == 0 && owner.WriteLength() == 3 && owner.Marker == 73 && data[0] == 'z' ? 1 : 0;
        }

        public static int PriorSnapshot()
        {
            char[] data = Make();
            var owner = new Holder(new Span<char>(data));
            Holder snapshot = owner;
            owner.ClearWritable();
            owner.ClearReadOnly();
            snapshot.Write('x');
            return owner.WriteLength() == 0 && owner.ReadLength() == 0 && owner.Marker == 73 &&
                snapshot.WriteLength() == 3 && snapshot.ReadLength() == 3 && data[0] == 'x' ? 1 : 0;
        }

        public static int LiveAlias()
        {
            char[] data = Make();
            var owner = new Holder(new Span<char>(data));
            return ClearThroughAlias(ref owner, ref owner);
        }

        public static int StaticInt32Field()
        {
            var owner = new IntHolder(Table);
            owner.Clear();
            return owner.Length() == 0 && owner.Marker == 89 ? 1 : 0;
        }

        private static ReadOnlySpan<int> Table => [3, 5, 7];

        private static int ClearThroughAlias(ref Holder target, ref Holder observer)
        {
            target.ClearWritable();
            return observer.WriteLength() == 0 && observer.ReadLength() == 3 && observer.Marker == 73 ? 1 : 0;
        }

        private static char[] Make()
        {
            var data = new char[3];
            data[0] = 'a';
            data[1] = 'b';
            data[2] = 'c';
            return data;
        }

        private ref struct Holder
        {
            private Span<char> writable;
            private ReadOnlySpan<char> readOnly;
            internal int Marker;

            internal Holder(Span<char> span)
            {
                writable = span;
                readOnly = span;
                Marker = 73;
            }

            internal void ClearWritable() => writable = default;
            internal void ClearReadOnly() => readOnly = default;
            internal int WriteLength() => writable.Length;
            internal int ReadLength() => readOnly.Length;
            internal void Write(char value) => writable[0] = value;
        }

        private ref struct IntHolder
        {
            private ReadOnlySpan<int> table;
            internal int Marker;

            internal IntHolder(ReadOnlySpan<int> values)
            {
                table = values;
                Marker = 89;
            }

            internal void Clear() => table = default;
            internal int Length() => table.Length;
        }
    }
}
