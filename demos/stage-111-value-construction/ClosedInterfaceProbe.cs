using System;

namespace ValueConstructionDemo
{
    public static class ClosedInterfaceProbe
    {
        public interface IClosedReader<T>
        {
            int Read(T value);
        }

        private sealed class IntegerReader : IClosedReader<int>
        {
            public int Read(int value) => unchecked(value * 3 + 11);
        }

        private sealed class OffsetReader<T> : IClosedReader<T>
        {
            public int Read(T value) => 19;
        }

        private sealed class BothReaders : IClosedReader<int>, IClosedReader<uint>
        {
            int IClosedReader<int>.Read(int value) => unchecked(value * 11 + 17);
            int IClosedReader<uint>.Read(uint value) => unchecked((int)value * 7 - 13);
        }

        private static int ReadInt(IClosedReader<int> reader, int value) => reader.Read(value);
        private static int ReadUnsigned(IClosedReader<uint> reader, uint value) => reader.Read(value);

        public static int CheckClosedInterface(int value)
        {
            IClosedReader<int> reader = value < 0 ? (IClosedReader<int>)new IntegerReader() : new OffsetReader<int>();
            var both = new BothReaders();
            return unchecked(ReadInt(reader, value) + ReadInt(both, value) * 5 + ReadUnsigned(both, (uint)value) * 3);
        }

        public static int CheckClosedInterfaceNull(int value)
        {
            IClosedReader<int> reader = value < 0 ? null : new IntegerReader();
            try { return ReadInt(reader, value); }
            catch (NullReferenceException) { return -37; }
        }
    }
}
