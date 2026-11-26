namespace UnmanagedPointerFixture
{
    // The shape of System.IO.UnmanagedMemoryStream's pointer core: a byte* field,
    // a length and capacity, and ReadByte through ldind.u1. No pinning, no
    // stackalloc: the memory belongs to the host (Marshal.AllocHGlobal on the
    // CLR, the lent linear memory in Wasm), and both sides use the same offsets.
    public sealed unsafe class Reader
    {
        private byte* memory;
        private long length;
        private long capacity;
        private long position;

        public void Initialize(byte* pointer, long count, long limit)
        {
            if (pointer == null) throw new System.ArgumentNullException(nameof(pointer));
            if (count < 0 || limit < 0 || count > limit) throw new System.ArgumentOutOfRangeException(nameof(count));
            memory = pointer;
            length = count;
            capacity = limit;
            position = 0;
        }

        public int ReadByte()
        {
            if (position >= length) return -1;
            return memory[position++];
        }

        public void WriteByte(byte value)
        {
            if (position >= capacity) throw new System.NotSupportedException();
            memory[position++] = value;
            if (position > length) length = position;
        }

        public long Length => length;
    }

    public static unsafe class Kernels
    {
        // Sum count bytes through a Reader: ldind.u1 via a byte* field.
        public static int SumBytes(byte* data, int count)
        {
            var reader = new Reader();
            reader.Initialize(data, count, count);
            int sum = 0, value;
            while ((value = reader.ReadByte()) >= 0) sum = unchecked(sum * 31 + value);
            return sum;
        }

        // Write through the reader, then read back with direct pointer arithmetic.
        public static int WriteThenRead(byte* data, int count)
        {
            var reader = new Reader();
            reader.Initialize(data, 0, count);
            for (int i = 0; i < count; i++) reader.WriteByte(unchecked((byte)(i * 7 + 3)));
            int sum = 0;
            for (byte* cursor = data, end = data + count; cursor < end; cursor++) sum += *cursor;
            return unchecked(sum * 100 + (int)reader.Length);
        }

        // Typed loads and stores: sign and zero extension, and wider elements.
        public static int SignedBytes(sbyte* data, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++) sum += data[i];
            return sum;
        }

        public static int Words(ushort* unsigned, short* signed, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++) sum += unsigned[i] - signed[i];
            return sum;
        }

        public static long Integers(int* values, long* wide, int count)
        {
            long sum = 0;
            for (int i = 0; i < count; i++)
            {
                wide[i] = (long)values[i] * 1000003L;
                sum += wide[i];
            }
            return sum;
        }

        public static double Floats(float* singles, double* doubles, int count)
        {
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                doubles[i] = singles[i] * 0.5;
                sum += doubles[i];
            }
            return sum;
        }

        // Pointer difference and comparison, including a pointer returned from a helper.
        private static byte* Find(byte* start, byte* end, byte value)
        {
            for (byte* cursor = start; cursor < end; cursor++)
                if (*cursor == value) return cursor;
            return null;
        }

        public static int IndexOf(byte* data, int count, int value)
        {
            byte* found = Find(data, data + count, unchecked((byte)value));
            return found == null ? -1 : (int)(found - data);
        }

        // A null pointer read raises NullReferenceException on the CLR.
        public static int ReadNull()
        {
            byte* none = null;
            try { return *none; }
            catch (System.NullReferenceException) { return -7; }
        }

        // The reader rejects a null pointer with ArgumentNullException.
        public static int InitializeNull()
        {
            try { new Reader().Initialize(null, 0, 0); return 0; }
            catch (System.ArgumentNullException) { return -9; }
        }
    }
}
