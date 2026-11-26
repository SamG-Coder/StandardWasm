using System.Runtime.InteropServices;

namespace StructPointerFixture
{
    // Stage 143 (decision 0071): IL sizeof and pointers to blittable sequential structs in
    // memory 0. The x86 CLR is the oracle: its 4-byte native size is the wasm32 width.
    [StructLayout(LayoutKind.Sequential)] public struct ByteLong { public byte B; public long L; }
    public struct ByteDouble { public byte B; public double D; }
    public struct ByteInt { public byte B; public int I; }
    public struct ShortByte { public short S; public byte B; }
    public struct Nested { public byte B; public ByteInt Inner; public short S; }
    public unsafe struct WithPtr { public byte B; public void* P; public byte* Q; }
    public struct CharBool { public char C; public bool B; }
    public struct Empty { }
    public struct LongByte { public long L; public byte B; }
    public struct Rect { public int Left, Top, Right, Bottom; }
    public enum Small : byte { A }
    public struct WithEnum { public Small E; public short S; }
    public struct WithStatic { public static long Shared; public byte B; }
    public struct Mixed { public byte B; public long L; public short S; public double D; }

    public unsafe struct ConstructorPair
    {
        public int Value;

        public ConstructorPair(ConstructorPair* other, int value)
        {
            other->Value = value + 2;
            Value = value + 5;
        }
    }

    // Pack and Size change the CLR byte layout: sizeof stays a located diagnostic (x86 CLR: 5, 12).
    [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct Packed { public byte B; public int I; }
    [StructLayout(LayoutKind.Sequential, Size = 12)] public struct Sized { public int I; }

    public static unsafe class Kernels
    {
        public static int PackedSize() => sizeof(Packed);
        public static int SizedSize() => sizeof(Sized);
        public static int ByteLongSize() => sizeof(ByteLong);
        public static int ByteDoubleSize() => sizeof(ByteDouble);
        public static int ByteIntSize() => sizeof(ByteInt);
        public static int ShortByteSize() => sizeof(ShortByte);
        public static int NestedSize() => sizeof(Nested);
        public static int WithPtrSize() => sizeof(WithPtr);
        public static int CharBoolSize() => sizeof(CharBool);
        public static int EmptySize() => sizeof(Empty);
        public static int LongByteSize() => sizeof(LongByte);
        public static int RectSize() => sizeof(Rect);
        public static int WithEnumSize() => sizeof(WithEnum);
        public static int WithStaticSize() => sizeof(WithStatic);

        public static int Area(Rect* r) => (r->Right - r->Left) * (r->Bottom - r->Top);
        public static int Grow(Rect* r, int by) { r->Left -= by; r->Top -= by; r->Right += by; r->Bottom += by; return r->Right; }
        public static long MixedSum(Mixed* m) { m->S = 7; m->D = 2.5; return m->B + m->L + m->S + (long)(m->D * 4); }
        public static int FieldAddress(Rect* r) { int* p = &r->Bottom; *p = 99; return (int)((byte*)p - (byte*)r) * 1000 + r->Bottom; }
        public static int NullRect() { try { return ((Rect*)null)->Top; } catch (System.NullReferenceException) { return -5; } }
        public static int Walk(Rect* rs, int n) { int s = 0; for (int i = 0; i < n; i++) s += rs[i].Left * 3 + rs[i].Bottom; return s; }

        public static int ConstructorFieldStores(ConstructorPair* other, int value)
        {
            var current = new ConstructorPair(other, value);
            return current.Value * 100 + other->Value;
        }
    }
}
