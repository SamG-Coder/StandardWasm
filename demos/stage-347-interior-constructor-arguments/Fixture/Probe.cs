namespace InteriorConstructorFixture
{
    public static class Probe
    {
        private struct Payload
        {
            internal byte Byte;
            internal int Word;
        }

        private sealed class Source
        {
            internal Payload Data;
        }

        private struct Slot<T> where T : struct
        {
            internal T Item;
            internal bool Present;

            internal Slot(T item)
            {
                Item = item;
                Present = true;
            }
        }

        public static int ByteArgument() => FromByte(Create(231, 417));

        public static int WordArgument() => FromWord(Create(197, 262157));

        public static int RhsNull()
        {
            try { return FromByte(null); }
            catch (System.NullReferenceException) { return 619; }
        }

        private static int FromByte(Source source)
        {
            Slot<byte> result = new Slot<byte>(source.Data.Byte);
            return result.Present ? result.Item : 0;
        }

        private static int FromWord(Source source)
        {
            Slot<int> result = new Slot<int>(source.Data.Word);
            return result.Present ? result.Item : 0;
        }

        private static Source Create(byte value, int word)
        {
            var source = new Source();
            source.Data = new Payload { Byte = value, Word = word };
            return source;
        }
    }
}
