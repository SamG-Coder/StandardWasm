using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

// Stage 366 (decision 0104): the platform members that benchmark-style console programs use,
// such as the How-fast-is-C# drivers. Every line printed here must be identical on the x86 CLR,
// in Node and in Edge. Values that legitimately differ by host (timings, hardware support) are
// only printed as properties that hold everywhere.
internal static unsafe class Program
{
    private readonly struct Result
    {
        public readonly ulong Ops, Checksum;
        public Result(ulong ops, ulong checksum) { Ops = ops; Checksum = checksum; }
    }

    private static int Main(string[] args)
    {
        string which = args.Length > 0 ? args[0] : "all";
        if (which != "all" && which != "exit") return 2;
        Formatting();
        Strings();
        Whitespace();
        Clock();
        Arithmetic();
        Arrays();
        Native();
        Unsafes();
        Features();
        Exceptions();
        Collector();
        Console.Out.Flush();
        Console.Error.WriteLine("stderr line");
        Console.Error.Flush();
        if (which == "exit")
        {
            try { Environment.Exit(7); }
            finally { Console.Out.Write("finally must not run\n"); }
        }
        return 3;
    }

    // Int32/UInt32/Int64/UInt64/SByte/Byte/Int16/UInt16 ToString through ldloca, ldarga,
    // ldflda of init-only fields and ldelema, including both extremes.
    private static void Formatting()
    {
        long min = long.MinValue; ulong max = ulong.MaxValue; int imin = int.MinValue; uint umax = uint.MaxValue;
        sbyte s = -128; byte b = 255; short sh = -32768; ushort us = 65535; ulong zero = 0;
        var r = new Result(42, 18446744073709551000UL);
        byte[] bytes = { 0, 7, 200 };
        Line("format " + min + " " + max + " " + imin + " " + umax + " " + s + " " + b + " " + sh + " " + us + " " + zero +
            " " + r.Ops + " " + r.Checksum + " " + bytes[2].ToString() + " " + Argument(-5) + " " + Argument(int.MaxValue));
    }

    private static string Argument(int value) => value.ToString();

    // == and != over literals, equal contents, empty and null; Concat of 3, 4 and an array,
    // including the CLR's reference identity results.
    private static void Strings()
    {
        string a = "abc", c = null, e = "";
        string built = new string(new[] { 'a', 'b', 'c' });
        int flags = (a == built ? 1 : 0) | (a != built ? 2 : 0) | (c != null ? 4 : 0) | (e != "" ? 8 : 0) | (a != "abd" ? 16 : 0);
        string three = string.Concat(e, a, c);
        string four = string.Concat(a, e, "x", c);
        string[] parts = { null, "" };
        int identity = ((object)three == a ? 1 : 0) | ((object)four == a ? 2 : 0) |
            ((object)string.Concat(parts) == "" ? 4 : 0) | ((object)string.Concat(new[] { a }) == a ? 8 : 0);
        Line("strings " + flags + " " + three + " " + four + " " + identity + " " + string.Concat(new[] { "p", null, "q", "", "r" }));
    }

    // Char.IsWhiteSpace over every UTF-16 code unit.
    private static void Whitespace()
    {
        ulong hash = 14695981039346656037UL; int count = 0;
        for (int unit = 0; unit <= 0xffff; unit++)
            if (char.IsWhiteSpace((char)unit)) { count++; hash = (hash ^ (ulong)unit) * 1099511628211UL; }
        Line("whitespace " + count + " " + hash);
    }

    // Stopwatch: a monotonic timestamp and a positive frequency; the exact values are host-specific.
    private static void Clock()
    {
        long frequency = Stopwatch.Frequency;
        long first = Stopwatch.GetTimestamp();
        long second = Stopwatch.GetTimestamp();
        ulong ns = TicksToNs(second - first, frequency);
        Line("clock " + (frequency > 0 ? "positive" : "bad") + " " + (second >= first ? "monotonic" : "bad") + " " +
            (ns < 60_000_000_000UL ? "bounded" : "bad"));
    }

    private static ulong TicksToNs(long ticks, long frequency) =>
        (ulong)((UInt128)(ulong)ticks * 1_000_000_000UL / (ulong)frequency);

    // UInt128 multiply-divide, Math.BigMul and BitOperations over 32- and 64-bit operands.
    private static void Arithmetic()
    {
        ulong hash = 0, x = 0x9E3779B97F4A7C15UL;
        for (int i = 0; i < 4000; i++)
        {
            x ^= x << 13; x ^= x >> 7; x ^= x << 17;
            ulong divisor = (x >> (i % 64)) | 1;
            ulong high = Math.BigMul(x, x ^ 0xE7037ED1A0B428DBUL, out ulong low);
            ulong v = i % 97 == 0 ? 0 : x >> (i % 64);
            uint w = (uint)v;
            hash = (hash ^ TicksToNs((long)(x >> 1), (long)(divisor >> 1) | 1) ^ high ^ low) * 1099511628211UL +
                (ulong)(BitOperations.LeadingZeroCount(v) + BitOperations.LeadingZeroCount(w) * 3 +
                BitOperations.TrailingZeroCount(v) * 5 + BitOperations.TrailingZeroCount(w) * 7 +
                BitOperations.TrailingZeroCount((long)v) * 11 + BitOperations.TrailingZeroCount((int)w) * 13 +
                BitOperations.PopCount(v) * 17 + BitOperations.PopCount(w) * 19);
        }
        ulong maxHigh = Math.BigMul(ulong.MaxValue, ulong.MaxValue, out ulong maxLow);
        int divide = 0;
        try { TicksToNs(1, 0); } catch (DivideByZeroException) { divide = 1; }
        Line("arithmetic " + hash + " " + maxHigh + " " + maxLow + " " + divide + " " + IntPtr.Size + " " + (ulong)nuint.MaxValue);
    }

    // Array.Sort<T> for int/uint/long/ulong, element addresses of uint/long/ulong/byte elements,
    // and MemoryMarshal.GetArrayDataReference with Unsafe.Add.
    private static void Arrays()
    {
        ulong hash = 0, x = 88172645463325252UL;
        foreach (int n in new[] { 0, 1, 2, 7, 1000 })
        {
            long[] a = new long[n]; ulong[] b = new ulong[n]; int[] c = new int[n]; uint[] d = new uint[n];
            for (int i = 0; i < n; i++)
            {
                x ^= x << 13; x ^= x >> 7; x ^= x << 17;
                a[i] = (long)x % 1000; b[i] = x; c[i] = (int)x; d[i] = (uint)(x >> 7) % 50;
            }
            Array.Sort(a); Array.Sort(b); Array.Sort(c); Array.Sort(d);
            for (int i = 0; i < n; i++) hash = hash * 1099511628211UL ^ (ulong)a[i] ^ b[i] * 3 ^ (ulong)c[i] * 5 ^ d[i] * 7;
        }
        uint[] u = new uint[8]; long[] l = new long[8]; ulong[] q = new ulong[8]; byte[] bytes = new byte[8];
        for (int i = 0; i < 100; i++)
        {
            u[i & 7] += (uint)i * 2654435761u; l[i & 7] -= i; Bump(ref q[i & 7], (ulong)i * 3); bytes[i & 7] += (byte)i;
        }
        ref long first = ref MemoryMarshal.GetArrayDataReference(l);
        ref uint start = ref MemoryMarshal.GetArrayDataReference(u);
        for (int i = 0; i < 8; i++) { Unsafe.Add(ref first, i) += 1000; Unsafe.Add(ref start, i) ^= 0x55; }
        for (int i = 0; i < 8; i++) hash = hash * 31 + u[i] + (ulong)l[i] + q[i] + bytes[i];
        int empty = 0;
        ref long none = ref MemoryMarshal.GetArrayDataReference(new long[0]);
        try { MemoryMarshal.GetArrayDataReference((long[])null); } catch (NullReferenceException) { empty = 1; }
        try { u[8] += 1; } catch (IndexOutOfRangeException) { empty += 2; }
        try { Array.Sort((int[])null); } catch (ArgumentNullException) { empty += 4; }
        Line("arrays " + hash + " " + empty);
    }

    private static void Bump(ref ulong value, ulong delta) => value += delta;

    // NativeMemory allocation, reallocation, alignment, zeroing, Fill/Clear/Copy and Buffer-free use.
    private static void Native()
    {
        long* a = (long*)NativeMemory.Alloc((nuint)(8 * 1000));
        for (int i = 0; i < 1000; i++) a[i] = i * 3L;
        a = (long*)NativeMemory.Realloc(a, (nuint)(8 * 100000));
        for (int i = 1000; i < 100000; i++) a[i] = i * 3L;
        long sum = 0;
        for (int i = 0; i < 100000; i++) sum += a[i];
        int* z = (int*)NativeMemory.AllocZeroed(1000, 4);
        int zeros = 0;
        for (int i = 0; i < 1000; i++) zeros += z[i];
        byte* aligned = (byte*)NativeMemory.AlignedAlloc(4096, 128);
        int isAligned = (nuint)aligned % 128 == 0 ? 1 : 0;
        NativeMemory.Fill(aligned, 4096, 7);
        NativeMemory.Clear(aligned, 100);
        int filled = 0;
        for (int i = 0; i < 4096; i++) filled += aligned[i];
        aligned = (byte*)NativeMemory.AlignedRealloc(aligned, 1 << 20, 128);
        int kept = aligned[200] + aligned[4095] + ((nuint)aligned % 128 == 0 ? 1 : 0);
        NativeMemory.Copy(a, a + 1, 8 * 10);
        long moved = a[1] + a[10];
        NativeMemory.Free(a);
        NativeMemory.Free(z);
        NativeMemory.AlignedFree(aligned);
        NativeMemory.Free(null);
        int alignment = 0;
        try { NativeMemory.AlignedAlloc(16, 3); } catch (ArgumentException) { alignment = 1; }
        void* one = NativeMemory.Alloc(0);
        NativeMemory.Free(one);
        Line("native " + sum + " " + zeros + " " + isAligned + " " + filled + " " + kept + " " + moved + " " + alignment +
            " " + (one != null ? "nonnull" : "null"));
    }

    // Unsafe.SkipInit on a local and an out parameter, and Unsafe.BitCast between equal-size primitives.
    private static void Unsafes()
    {
        long skipped;
        Unsafe.SkipInit(out skipped);
        skipped = 5;
        Skip(out int other);
        int hash = 0;
        for (int i = -300; i < 300; i += 7)
        {
            hash = hash * 31 + Unsafe.BitCast<bool, byte>(i > 0) + Unsafe.BitCast<sbyte, byte>((sbyte)i) +
                Unsafe.BitCast<ushort, short>((ushort)(i * 300));
            hash ^= Unsafe.BitCast<float, int>(i * 1.5f) ^ (int)Unsafe.BitCast<double, long>(i * 2.25) ^
                (int)Unsafe.BitCast<ulong, long>((ulong)i);
        }
        Line("unsafe " + skipped + " " + other + " " + hash);
    }

    private static void Skip(out int value)
    {
        Unsafe.SkipInit(out value);
        value = 9;
    }

    // Hardware feature queries pick a path; both paths must give the same result. The queries
    // themselves differ by host, so only the result is printed.
    private static void Features()
    {
        int[] data = new int[64];
        for (int i = 0; i < data.Length; i++) data[i] = i * 7 % 23;
        int vectorSum = 0;
        if (Vector128.IsHardwareAccelerated && Sse2.IsSupported)
        {
            Vector128<int> acc = Vector128<int>.Zero;
            for (int i = 0; i < data.Length; i += 4) acc += Vector128.Create(data[i], data[i + 1], data[i + 2], data[i + 3]);
            vectorSum = Vector128.Sum(acc);
        }
        else
        {
            for (int i = 0; i < data.Length; i++) vectorSum += data[i];
        }
        Line("features " + vectorSum);
    }

    // newobj of platform exceptions followed by throw, caught by type.
    private static void Exceptions()
    {
        int caught = 0;
        try { throw new ArgumentOutOfRangeException("limit"); } catch (ArgumentOutOfRangeException) { caught |= 1; }
        try { throw new ArgumentException("message", "name"); } catch (ArgumentException) { caught |= 2; }
        try { throw new ArgumentNullException("name"); } catch (ArgumentNullException) { caught |= 4; }
        try { throw new ArgumentOutOfRangeException("limit"); } catch (ArgumentException) { caught |= 8; }
        Line("exceptions " + caught);
    }

    // GC members return normally; this program has no finalizers to observe.
    private static void Collector()
    {
        object kept = new object();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.KeepAlive(kept);
        Line("collector done");
    }

    private static void Line(string text) => Console.Out.Write(text + "\n");
}
