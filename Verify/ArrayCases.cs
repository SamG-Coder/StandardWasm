using System.Globalization;
using PrimitiveArrays;

// CLR oracle for stage 37. expected == null means the CLR threw IndexOutOfRange or Overflow (negative length), and
// Wasm must trap. 64-bit and floating values use the {"i64"} and {"f64"} encodings.
internal static class ArrayCases
{
    private static object Encode(object value) => value switch
    {
        double v => new Dictionary<string, string> { ["f64"] = v.ToString("G17", CultureInfo.InvariantCulture) },
        float v => new Dictionary<string, string> { ["f64"] = ((double)v).ToString("G17", CultureInfo.InvariantCulture) },
        long v => new Dictionary<string, string> { ["i64"] = v.ToString(CultureInfo.InvariantCulture) },
        // Small CLR integer results cross the Wasm boundary as i32 numbers.
        short v => (int)v,
        ushort v => (int)v,
        sbyte v => (int)v,
        char v => (int)v,
        int v => v,
        _ => throw new ArgumentException($"Unsupported oracle value {value.GetType()}")
    };

    internal static void Add(List<object> cases)
    {
        void Record(string method, object[] inputs, Func<object> run)
        {
            object expected = null;
            string exception = null;
            try { expected = Encode(run()); }
            catch (Exception e) when (e is IndexOutOfRangeException or OverflowException)
            {
                exception = e.GetType().FullName;
            }
            cases.Add(new { module = "PrimitiveArrays", method, inputs = Array.ConvertAll(inputs, Encode), expected, exception });
        }

        foreach (int limit in new[] { -5, 0, 1, 2, 3, 10, 100, 1000, 30000 })
            Record("CountPrimes", [limit], () => Buffers.CountPrimes(limit));
        foreach (int index in new[] { -1, 0, 3, 4 })
            Record("IncrementElement", [index], () => Buffers.IncrementElement(index));
        foreach (var (length, index) in new[] { (1, 0), (10, 9), (2000, 1999), (0, 0), (5, 5), (5, -1), (-1, 0) })
        {
            Record("PrefixCube", [length, index], () => Buffers.PrefixCube(length, index));
            Record("ReadLong", [length, index], () => Buffers.ReadLong(length, index));
            Record("ReadDouble", [length, index], () => Buffers.ReadDouble(length, index));
            Record("ReadChar", [length, index], () => Buffers.ReadChar(length, index));
            Record("LengthOfShorts", [length], () => Buffers.LengthOfShorts(length));
        }
        foreach (int length in new[] { 1, 2, 10, 1000 })
            foreach (double scale in new[] { 0.0, 1.5, -1e10, double.NaN })
                for (int which = 0; which < 2; which++)
                    Record("Statistic", [length, scale, which], () => Buffers.Statistic(length, scale, which));
        foreach (int length in new[] { 0, 1, 5, 1000 })
            foreach (var (a, b) in new[] { (1.5f, 2f), (0.1f, 3f), (float.MaxValue, 2f), (-0f, 1f) })
                Record("Dot", [length, a, b], () => Buffers.Dot(length, a, b));
        foreach (int shift in new[] { -27, -1, 0, 1, 3, 13, 25, 26, 1000 })
            Record("CaesarChecksum", [shift], () => Buffers.CaesarChecksum(shift));

        int[] ints = [int.MinValue, -65537, -65536, -32769, -32768, -129, -128, -1, 0, 1, 127, 128, 200, 255, 256,
                      32767, 32768, 40000, 65535, 65536, 0x1F600, int.MaxValue];
        foreach (int v in ints)
        {
            Record("StoreShort", [v], () => Buffers.StoreShort(v));
            Record("StoreUShort", [v], () => Buffers.StoreUShort(v));
            Record("StoreChar", [v], () => Buffers.StoreChar(v));
            Record("StoreSByte", [v], () => Buffers.StoreSByte(v));
            Record("StoreBool", [v], () => Buffers.StoreBool(v));
            Record("ToShort", [v], () => Buffers.ToShort(v));
            Record("ToUShort", [v], () => Buffers.ToUShort(v));
            Record("ToSByte", [v], () => Buffers.ToSByte(v));
            Record("ToChar", [v], () => Buffers.ToChar(v));
            Record("LongToSByte", [(long)v * 3], () => Buffers.LongToSByte((long)v * 3));
            foreach (int w in new[] { 0, -1, int.MaxValue })
                Record("UnsignedMax", [v, w, 7], () => Buffers.UnsignedMax(v, w, 7));
        }
        // Saturating float-to-small-int conversions (.NET 9+): NaN, infinities and out-of-range values.
        foreach (double d in new[] { 0.0, -0.0, 0.9, -0.9, 127.5, 128.0, 255.9, 32767.5, 32768.0, 40000.0, 65535.9,
                                    65536.0, -1.0, -32769.0, 1e10, -1e10, double.PositiveInfinity, double.NegativeInfinity, double.NaN })
        {
            Record("DoubleToShort", [d], () => Buffers.DoubleToShort(d));
            Record("DoubleToChar", [d], () => Buffers.DoubleToChar(d));
        }
        foreach (int steps in new[] { 0, 1, 32, 33, 66, 1000 })
            Record("ShortCounter", [steps], () => Buffers.ShortCounter(steps));

        var random = new Random(3737);
        for (int i = 0; i < 300; i++)
        {
            int v = random.Next(int.MinValue, int.MaxValue);
            double d = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
            Record("StoreShort", [v], () => Buffers.StoreShort(v));
            Record("StoreChar", [v], () => Buffers.StoreChar(v));
            Record("ToSByte", [v], () => Buffers.ToSByte(v));
            Record("DoubleToShort", [d], () => Buffers.DoubleToShort(d));
            int w = random.Next(int.MinValue, int.MaxValue), x = random.Next();
            Record("UnsignedMax", [v, w, x], () => Buffers.UnsignedMax(v, w, x));
        }
    }
}
