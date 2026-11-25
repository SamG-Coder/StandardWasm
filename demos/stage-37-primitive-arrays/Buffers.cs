namespace PrimitiveArrays
{
    // Ordinary array code over every primitive element width. Element updates are written as a[i] = a[i] + x:
    // the compound form a[i] += x compiles to ldelema (a managed pointer), which is a separate, later capability.
    public static class Buffers
    {
        // Sieve of Eratosthenes over bool[] (packed i8): number of primes <= limit.
        public static int CountPrimes(int limit)
        {
            if (limit < 2) return 0;
            var composite = new bool[limit + 1];
            int count = 0;
            for (int i = 2; i <= limit; i++)
            {
                if (composite[i]) continue;
                count++;
                for (long j = (long)i * i; j <= limit; j += i)
                    composite[(int)j] = true;
            }
            return count;
        }

        // long[] prefix sums of cubes, returning the element at index.
        public static long PrefixCube(int length, int index)
        {
            var sums = new long[length];
            long running = 0;
            for (int i = 0; i < sums.Length; i++)
            {
                running += (long)i * i * i;
                sums[i] = running;
            }
            return sums[index];
        }

        // double[] mean and population variance of a generated series; which selects the result.
        public static double Statistic(int length, double scale, int which)
        {
            var samples = new double[length];
            for (int i = 0; i < length; i++)
                samples[i] = (i % 7 - 3) * scale + i * 0.25;
            double sum = 0;
            for (int i = 0; i < samples.Length; i++) sum += samples[i];
            double mean = sum / samples.Length;
            if (which == 0) return mean;
            double squares = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double delta = samples[i] - mean;
                squares += delta * delta;
            }
            return squares / samples.Length;
        }

        // float[] dot product, accumulated in single precision.
        public static float Dot(int length, float a, float b)
        {
            var x = new float[length];
            var y = new float[length];
            for (int i = 0; i < length; i++)
            {
                x[i] = a * i;
                y[i] = b / (i + 1);
            }
            float total = 0;
            for (int i = 0; i < length; i++) total += x[i] * y[i];
            return total;
        }

        // char[] Caesar shift of "Hello, Wasm" by shift, returning a checksum of the result.
        public static int CaesarChecksum(int shift)
        {
            // Filled element by element: a constant initializer ({ 'H', ... }) compiles to ldtoken plus
            // RuntimeHelpers.InitializeArray over static data, which this compiler does not lower yet.
            var text = new char[11];
            text[0] = 'H'; text[1] = 'e'; text[2] = 'l'; text[3] = 'l'; text[4] = 'o'; text[5] = ',';
            text[6] = ' '; text[7] = 'W'; text[8] = 'a'; text[9] = 's'; text[10] = 'm';
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 'a' && c <= 'z') text[i] = (char)('a' + ((c - 'a' + shift) % 26 + 26) % 26);
                else if (c >= 'A' && c <= 'Z') text[i] = (char)('A' + ((c - 'A' + shift) % 26 + 26) % 26);
            }
            int checksum = 0;
            for (int i = 0; i < text.Length; i++) checksum = checksum * 31 + text[i];
            return checksum;
        }

        // Packed-cell wraparound: the value read back after storing into each element type.
        public static int StoreShort(int value) { var a = new short[1]; a[0] = (short)value; return a[0]; }
        public static int StoreUShort(int value) { var a = new ushort[1]; a[0] = (ushort)value; return a[0]; }
        public static int StoreChar(int value) { var a = new char[1]; a[0] = (char)value; return a[0]; }
        public static int StoreSByte(int value) { var a = new sbyte[1]; a[0] = (sbyte)value; return a[0]; }
        public static int StoreBool(int value) { var a = new bool[1]; a[0] = value != 0; return a[0] ? 1 : 0; }

        // Small scalar locals and returns normalize like the CLR, without arrays.
        public static short ToShort(int value) => (short)value;
        public static ushort ToUShort(int value) => (ushort)value;
        public static sbyte ToSByte(int value) => (sbyte)value;
        public static char ToChar(int value) => (char)value;
        public static int DoubleToShort(double value) => (short)value;
        public static int DoubleToChar(double value) => (char)value;
        public static int LongToSByte(long value) => (sbyte)value;

        // Small-typed parameters. The CLR can only pass in-range values. A JS caller can pass any Number, and the
        // module normalizes it on entry like a CLR store would (test.mjs checks this host boundary directly).
        public static int ShortParameter(short value) => value;
        public static int CharParameter(char value) => value;
        public static int SByteParameter(sbyte value) => value;

        // A short local that overflows during a loop wraps at 16 bits.
        public static int ShortCounter(int steps)
        {
            short counter = 0;
            for (int i = 0; i < steps; i++) counter += 1000;
            return counter;
        }

        // uint[] keeps unsigned order: the largest element by unsigned comparison.
        public static int UnsignedMax(int a, int b, int c)
        {
            var values = new uint[] { (uint)a, (uint)b, (uint)c };
            uint best = 0;
            for (int i = 0; i < values.Length; i++)
                if (values[i] > best) best = values[i];
            return (int)best;
        }

        // Lengths and failures: element read at index from a new array of length (traps when out of range).
        public static long ReadLong(int length, int index) => (new long[length])[index];
        public static double ReadDouble(int length, int index) => (new double[length])[index];
        public static int ReadChar(int length, int index) => (new char[length])[index];
        public static int LengthOfShorts(int length) => new short[length].Length;
    }

    // Negative fixture: compound assignment to an element compiles to ldelema and must be diagnosed.
    public static class CompoundElement
    {
        public static int Increment(int index)
        {
            var counts = new int[4];
            counts[index] += 1;
            return counts[index];
        }
    }
}
