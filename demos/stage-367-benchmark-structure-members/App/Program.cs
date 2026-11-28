// Stage 367 (decision 0105): the data-structure members the "How fast is C#?" benchmarks use
// beyond their drivers. Every result is printed, and every exception is caught and named, so the
// x86 CLR, Node and Edge can be compared byte for byte.
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

internal static unsafe class Program
{
    private static int Main(string[] args)
    {
        RankTwo();
        Clamps();
        MemoryCopies();
        ExplicitNodes();
        ArrayPins();
        SpanPins();
        ArrayMembers();
        PointerSpans();
        BooleanElements();
        new Pools().Run();
        Vectors();
        Tuples();
        Write("done");
        return 0;
    }

    private static void Write(string text) => Console.Out.Write(text + "\n");

    private static string Flag(bool value) => value ? "T" : "F";

    // ---- rank-2 arrays (static readonly initializer) and Math.Clamp ----------------------------

    private static readonly long[,] Table = { { 1, -2, 3 }, { 4, 5, -6 } };
    private static readonly int[,] Small = { { 7 }, { 8 } };

    private static void RankTwo()
    {
        long sum = 0;
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 3; j++) sum = sum * 7 + Table[i, j];
        Write("rank2 " + sum.ToString() + " " + (Small[0, 0] + Small[1, 0]).ToString());
        try { Write(Table[2, 0].ToString()); } catch (IndexOutOfRangeException) { Write("rank2 row " + "IndexOutOfRangeException"); }
        try { Write(Table[0, 3].ToString()); } catch (IndexOutOfRangeException) { Write("rank2 column " + "IndexOutOfRangeException"); }
        try { Write(Table[-1, 0].ToString()); } catch (IndexOutOfRangeException) { Write("rank2 negative " + "IndexOutOfRangeException"); }
    }

    private static void Clamps()
    {
        Write("clamp " + Math.Clamp(5, 1, 3).ToString() + " " + Math.Clamp(-5, -3, 3).ToString() + " " +
            Math.Clamp(2u, 3u, 4u).ToString() + " " + Math.Clamp(long.MaxValue, 0L, 9L).ToString() + " " +
            Math.Clamp(ulong.MaxValue, 1UL, ulong.MaxValue - 1).ToString());
        try { Write(Math.Clamp(1, 5, 4).ToString()); } catch (ArgumentException) { Write("clamp " + "ArgumentException"); }
        try { Write(Math.Clamp(1UL, 5UL, 4UL).ToString()); } catch (ArgumentException) { Write("clamp " + "ArgumentException"); }
    }

    // ---- Buffer.MemoryCopy ---------------------------------------------------------------------

    private static void MemoryCopies()
    {
        byte* block = (byte*)NativeMemory.Alloc(32);
        for (int i = 0; i < 32; i++) block[i] = (byte)i;
        Buffer.MemoryCopy(block, block + 4, 28L, 8L);   // overlapping, forward
        Buffer.MemoryCopy(block + 10, block + 8, 24UL, 6UL); // overlapping, backward
        long sum = 0;
        for (int i = 0; i < 32; i++) sum = sum * 3 + block[i];
        Write("memorycopy " + sum.ToString());
        try { Buffer.MemoryCopy(block, block + 16, 4L, 5L); }
        catch (ArgumentOutOfRangeException) { Write("memorycopy " + "ArgumentOutOfRangeException"); }
        NativeMemory.Free(block);
    }

    // ---- explicit layouts with fixed buffers through pointers -----------------------------------

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct Node
    {
        [FieldOffset(56)] public int Count;
        [FieldOffset(0)] public fixed long Keys[7];
        [FieldOffset(60)] public int Tag;
    }

    private static void ExplicitNodes()
    {
        Node* nodes = (Node*)NativeMemory.AllocZeroed(3, (nuint)sizeof(Node));
        for (int n = 0; n < 3; n++)
        {
            Node* node = nodes + n;
            node->Count = n + 1;
            node->Tag = 100 * n;
            for (int k = 0; k < 7; k++) node->Keys[k] = n * 10 + k;
        }
        long sum = 0;
        long* raw = (long*)nodes;
        for (int i = 0; i < 3 * 8; i++) sum = sum * 31 + raw[i];
        Write("explicit " + sizeof(Node).ToString() + " " + sum.ToString() + " " + nodes[2].Count.ToString());
        NativeMemory.Free(nodes);
    }

    // ---- pinned arrays and spans (alias-free copies) ------------------------------------------

    private static void ArrayPins()
    {
        long[] values = { 1, 2, 3, 4 };
        fixed (long* p = values)
        {
            long* q = p;
            q[0] = 10;
            q++;           // pointer arithmetic on the fixed variable does not move the pin
            q[2] = 30;
            fixed (long* r = values) r[3] += p[0]; // the same array pinned twice shares the block
        }
        Write("pin " + values[0].ToString() + " " + values[1].ToString() + " " + values[2].ToString() + " " + values[3].ToString());
        int[] counts = { 5, 6 };
        try { Bump(counts); } catch (InvalidCastException) { Write("pin unwind " + "InvalidCastException" + " " + counts[0].ToString()); }
        int[] empty = new int[0];
        bool none;
        fixed (int* e = empty) none = e == null; // a virtual call (Console) inside the pin would fail its proof
        Write("pin empty " + Flag(none));
    }

    private static void Bump(int[] counts)
    {
        fixed (int* p = counts)
        {
            p[0] = 50;
            Fail();       // the exception leaves the frame: the copy is written back first
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Fail() => throw new InvalidCastException();

    private static void SpanPins()
    {
        byte[] bytes = { 9, 8, 7, 6, 5, 4 };
        Write("span " + SumPinned(new ReadOnlySpan<byte>(bytes, 1, 3)).ToString() + " " +
            SumPinned(new ReadOnlySpan<byte>(bytes, 6, 0)).ToString());
        try { SumPinned(new ReadOnlySpan<byte>(bytes, 4, 3)); }
        catch (ArgumentOutOfRangeException) { Write("span " + "ArgumentOutOfRangeException"); }
    }

    private static int SumPinned(ReadOnlySpan<byte> key)
    {
        int length = key.Length, sum = 0;
        fixed (byte* p = key)
        {
            if (p == null) return -1; // an empty span pins to null
            for (int i = 0; i < length; i++) sum = sum * 10 + p[i];
        }
        return sum;
    }

    // ---- Array.Fill / Resize / Copy / Clone / BinarySearch ---------------------------------------

    private sealed class Buffer8
    {
        public byte[] Data = new byte[2];
        public int[] Offsets = { 1, 2, 3 };

        public void Grow(int size) => Array.Resize(ref Data, size);
        public void Shrink(int size) => Array.Resize(ref Offsets, size);
    }

    private static void ArrayMembers()
    {
        int[] filled = new int[6];
        Array.Fill(filled, 7);
        Array.Fill(filled, -1, 2, 3);
        long[] wide = new long[3];
        Array.Fill(wide, long.MinValue, 1, 2);
        Write("fill " + string.Concat(Text(filled)) + " " + wide[0].ToString() + " " + wide[2].ToString());
        try { Array.Fill(filled, 0, 5, 2); } catch (ArgumentOutOfRangeException) { Write("fill " + "ArgumentOutOfRangeException"); }
        try { Array.Fill((int[])null, 0); } catch (ArgumentNullException) { Write("fill " + "ArgumentNullException"); }

        var buffer = new Buffer8();
        buffer.Data[1] = 42;
        byte[] before = buffer.Data;
        buffer.Grow(2);
        bool same = (object)before == (object)buffer.Data;
        buffer.Grow(5);
        buffer.Shrink(2);
        Write("resize " + Flag(same) + " " + buffer.Data.Length.ToString() + " " + buffer.Data[1].ToString() + " " +
            buffer.Offsets.Length.ToString() + " " + buffer.Offsets[1].ToString());
        try { buffer.Grow(-1); } catch (ArgumentOutOfRangeException) { Write("resize " + "ArgumentOutOfRangeException"); }

        Array.Copy(CopySource, CopyTarget, 3);
        Write("copy " + CopyTarget[0].ToString() + CopyTarget[2].ToString() + CopyTarget[3].ToString());

        int[] original = { 3, 1, 2 };
        int[] clone = (int[])original.Clone();
        clone[0] = 9;
        Write("clone " + original[0].ToString() + " " + clone[0].ToString() + " " + clone.Length.ToString() + " " +
            Flag((object)original == (object)clone));

        int[] sorted = { 1, 3, 3, 3, 5, 8 };
        Write("search " + Array.BinarySearch(sorted, 3).ToString() + " " + Array.BinarySearch(sorted, 4).ToString() + " " +
            Array.BinarySearch(sorted, 0).ToString() + " " + Array.BinarySearch(sorted, 9).ToString() + " " +
            Array.BinarySearch(new long[] { -5, 0, 7 }, 7L).ToString());
        try { Array.BinarySearch((int[])null, 1); } catch (ArgumentNullException) { Write("search " + "ArgumentNullException"); }
    }

    private static readonly byte[] CopySource = { 1, 2, 3, 4 };
    private static readonly byte[] CopyTarget = new byte[4];

    private static string[] Text(int[] values)
    {
        var text = new string[values.Length];
        for (int i = 0; i < values.Length; i++) text[i] = values[i].ToString() + ",";
        return text;
    }

    private static void PointerSpans()
    {
        uint* words = (uint*)NativeMemory.Alloc(8 * sizeof(uint));
        new Span<uint>(words, 8).Fill(uint.MaxValue);
        new Span<uint>(words + 2, 3).Fill(0x01020304);
        new Span<uint>(words + 6, 2).Clear();
        ulong sum = 0;
        for (int i = 0; i < 8; i++) sum = sum * 1000003 + words[i];
        Write("pointerspan " + sum.ToString());
        try { new Span<uint>(words, -1).Fill(1); } catch (ArgumentOutOfRangeException) { Write("pointerspan " + "ArgumentOutOfRangeException"); }
        NativeMemory.Free(words);
    }

    private static void BooleanElements()
    {
        bool[] flags = new bool[4];
        flags[1] |= true;
        flags[2] ^= true;
        flags[2] &= false;
        flags[3] |= flags[1];
        Write("bool " + Flag(flags[0]) + Flag(flags[1]) + Flag(flags[2]) + Flag(flags[3]));
    }

    // ---- inline-array pools ------------------------------------------------------------------

    private struct PoolState
    {
        public uint Head;
        public uint Cursor;
        public uint Limit;
    }

    [InlineArray(4)]
    private struct PoolArray
    {
        private PoolState _first;
    }

    private sealed class Pools
    {
        private PoolArray _pools;

        private uint Take(int pool)
        {
            ref PoolState p = ref Unsafe.Add(ref Unsafe.As<PoolArray, PoolState>(ref _pools), pool);
            uint value = p.Cursor;
            p.Cursor = value + (uint)pool + 1;
            p.Head = p.Head ^ value; // an address of an element field (^=) stays diagnosed
            if (p.Cursor > p.Limit) p.Limit = p.Cursor;
            return value;
        }

        private uint Read(int pool)
        {
            ref PoolState p = ref Unsafe.Add(ref Unsafe.As<PoolArray, PoolState>(ref _pools), pool);
            return p.Head * 7 + p.Cursor * 3 + p.Limit;
        }

        public void Run()
        {
            uint sum = 0;
            for (int i = 0; i < 20; i++) sum = sum * 5 + Take(i % 4);
            Write("pools " + sum.ToString() + " " + Read(0).ToString() + " " + Read(3).ToString());
        }
    }

    // ---- SIMD vectors ------------------------------------------------------------------------

    private static string Lanes(Vector512<int> v)
    {
        int* buffer = stackalloc int[16];
        Vector512.Store(v, buffer);
        string text = "";
        for (int i = 0; i < 16; i++)
        {
            int lane = buffer[i];
            text = text + lane.ToString() + (i == 15 ? "" : ",");
        }
        return text;
    }

    private static string Lanes(Vector512<long> v)
    {
        long* buffer = stackalloc long[8];
        Vector512.Store(v, buffer);
        string text = "";
        for (int i = 0; i < 8; i++)
        {
            long lane = buffer[i];
            text = text + lane.ToString() + (i == 7 ? "" : ",");
        }
        return text;
    }

    private static (Vector512<long> Lo, Vector512<long> Hi) Products(Vector512<int> a, Vector512<int> b)
    {
        var (alo, ahi) = Vector512.Widen(a);
        var (blo, bhi) = Vector512.Widen(b);
        return (alo * blo, ahi * bhi);
    }

    private static void Vectors()
    {
        int* data = stackalloc int[16];
        for (int i = 0; i < 16; i++) data[i] = (i * 37 % 23) - 11;
        var a = Vector512.Load(data);
        var b = Vector512.Create(3) - Vector512<int>.Indices;
        Write("v512 load " + Lanes(a));
        Write("v512 min " + Lanes(Vector512.Min(a, b)) + " max " + Lanes(Vector512.Max(a, b)));
        var less = Vector512.LessThan(a, b);
        Write("v512 lt " + Lanes(less) + " msb " + Vector512.ExtractMostSignificantBits(less).ToString());
        Write("v512 select " + Lanes(Vector512.ConditionalSelect(less, a, b & Vector512.Create(6))));
        Write("v512 cmp " + Vector512.ExtractMostSignificantBits(Vector512.Equals(a, b)).ToString() + " " +
            Vector512.ExtractMostSignificantBits(Vector512.GreaterThanOrEqual(a, b)).ToString() + " " +
            Vector512.ExtractMostSignificantBits(Vector512.LessThanOrEqual(a, b)).ToString() + " " +
            Vector512.ExtractMostSignificantBits(Vector512.GreaterThan(a, b)).ToString());
        var (lo, hi) = Products(a, Vector512.Create(int.MaxValue));
        Write("v512 widen " + Lanes(lo) + " | " + Lanes(hi));
        var longs = Vector512.Create(5L, -3L, long.MaxValue, long.MinValue, 0L, 9L, -9L, 1L);
        var indices = Vector512.Create(7L, 0L, 8L, -1L, 3L, 3L, 2L, 1L);
        Write("v512 shuffle " + Lanes(Vector512.Shuffle(longs, indices)));
        Write("v512 long " + Lanes(Vector512.Min(longs, Vector512<long>.Indices)) + " " +
            Lanes(Vector512.Max(longs, Vector512<long>.Zero)) + " " +
            Lanes(Vector512.Abs(longs)) + " " + Lanes((longs << 1) + Vector512<long>.One) + " " +
            Lanes(Vector512.ShiftRightLogical(longs, 60)) + " " +
            Vector512.ExtractMostSignificantBits(Vector512.LessThan(longs, Vector512<long>.Zero)).ToString() + " " +
            Vector512.ToScalar(longs | Vector512.Create(16L)).ToString());
        Write("v512 as " + Lanes(Vector512.AsInt32(longs)) + " " + Lanes(Vector512.AsInt64(a)));
        var quarter = Vector512.GetLower(a).GetUpper();
        var small = Vector128.Create(4, -2, 9, 0);
        Write("v128 " + Vector128.Min(quarter, small).GetElement(0).ToString() + " " +
            Vector128.Max(quarter, small).ToScalar().ToString() + " " +
            Vector128.Shuffle(small, Vector128.Create(3, 2, 7, -1)).GetElement(1).ToString() + " " +
            Vector128.ConditionalSelect(Vector128.Create(-1, 0, -1, 0), small, quarter).GetElement(3).ToString() + " " +
            Vector256.Max(Vector512.GetUpper(a), Vector512.GetLower(a)).GetLower().GetElement(2).ToString());
        try { Write(small.GetElement(4).ToString()); } catch (ArgumentOutOfRangeException) { Write("v128 " + "ArgumentOutOfRangeException"); }
        Vector128<int>* boxes = stackalloc Vector128<int>[2];
        boxes[0] = small;
        boxes[1] = Vector128.Max(boxes[0], quarter);
        int fifth = ((int*)boxes)[5];
        Write("v128 memory " + sizeof(Vector128<int>).ToString() + " " + boxes[1].GetElement(2).ToString() + " " +
            fifth.ToString());
        // A guarded accelerated path is pruned; both paths compute the same value.
        uint bits = Bmi2.IsSupported ? Bmi2.ParallelBitDeposit(5u, 0xF0u) : (5u & 1u) << 4 | (5u & 4u) << 4;
        Write("isa " + bits.ToString());
    }

    // ---- UInt128 tuples ----------------------------------------------------------------------

    private static void Tuples()
    {
        ulong[] xs = { ulong.MaxValue, 3, 0, ulong.MaxValue, 1UL << 40, 7 };
        var all = new (UInt128 Distance, int Id)[xs.Length + 1];
        int n = 0;
        for (int i = 0; i < xs.Length; i++)
            all[n++] = ((UInt128)xs[i] * xs[i] + (UInt128)xs[(i + 1) % xs.Length] * 2UL, 10 - i);
        Array.Sort(all, 0, n);
        string order = "";
        for (int i = 0; i < 7; i++) order = order + all[i].Id.ToString() + " ";
        Write("tuples " + order);
        try { Array.Sort(all, 2, 9); } catch (ArgumentException) { Write("tuples " + "ArgumentException"); }
        try { Array.Sort(all, -1, 2); } catch (ArgumentOutOfRangeException) { Write("tuples " + "ArgumentOutOfRangeException"); }
        try { Write(all[9].Id.ToString()); } catch (IndexOutOfRangeException) { Write("tuples " + "IndexOutOfRangeException"); }
    }
}
