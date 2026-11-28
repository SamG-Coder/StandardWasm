using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ReadOnlyExplicitOverlay
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct Overlay
    {
        [FieldOffset(0)] internal long Bits;
        [FieldOffset(0)] internal int Signed;
        [FieldOffset(0)] internal uint Unsigned;
        [FieldOffset(0)] internal float Single;
        [FieldOffset(4)] internal int High;
    }

    internal readonly struct TaggedValue
    {
        private readonly Overlay _value;
        internal readonly object Tag;

        internal TaggedValue(long bits, object tag)
        {
            _value = default;
            _value.Bits = bits;
            Tag = tag;
        }

        internal void Read<T>(out T result) => result = Unsafe.As<Overlay, T>(ref Unsafe.AsRef(in _value));

        internal uint ReadUnsigned() => Unsafe.As<Overlay, uint>(ref Unsafe.AsRef(in _value));

        internal float ReadSingle() => Unsafe.As<Overlay, float>(ref Unsafe.AsRef(in _value));

        internal long ReadBits() => _value.Bits;

        internal int ReadHigh() => _value.High;

        internal static ref int ReturnAlias(in TaggedValue value) => ref Unsafe.As<Overlay, int>(ref Unsafe.AsRef(in value._value));
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct MissingZeroView
    {
        [FieldOffset(0)] internal long Bits;
        [FieldOffset(4)] internal int Upper;
    }

    internal readonly struct UnsupportedValue
    {
        private readonly MissingZeroView _value;

        internal UnsupportedValue(long bits)
        {
            _value = default;
            _value.Bits = bits;
        }

        internal int ReadUndeclaredView() => Unsafe.As<MissingZeroView, int>(ref Unsafe.AsRef(in _value));
    }

    public static class Probe
    {
        public static int ClosedGenericOut()
        {
            var tag = new object();
            var value = new TaggedValue(unchecked((long)0x12345678fffffff7UL), tag);
            value.Read(out int scalar);
            return scalar == -9 && value.ReadHigh() == 0x12345678 && ReferenceEquals(tag, value.Tag) ? 101 : -1;
        }

        public static int DirectUnsigned()
        {
            var value = new TaggedValue(unchecked((long)0x87654321fedcba98UL), null);
            return value.ReadUnsigned() == 0xfedcba98U ? 103 : -1;
        }

        public static int FloatBits()
        {
            var value = new TaggedValue(0x11223344c0400000L, null);
            float scalar = value.ReadSingle();
            return scalar == -3.0f && value.ReadBits() == 0x11223344c0400000L ? 107 : -1;
        }

        public static int DefaultAndIndependentSnapshots()
        {
            var value = new TaggedValue(0x1234567887654321L, null);
            var saved = value;
            value = default;
            saved.Read(out int first);
            value.Read(out int second);
            return first == unchecked((int)0x87654321) && second == 0 && saved.ReadHigh() == 0x12345678 ? 109 : -1;
        }

        public static int NonzeroOffsetOnly()
        {
            var value = new UnsupportedValue(0x1122334455667788L);
            return value.ReadUndeclaredView();
        }

        public static int EscapingAddress()
        {
            var value = new TaggedValue(0x1122334455667788L, null);
            ref int alias = ref TaggedValue.ReturnAlias(in value);
            return alias;
        }
    }
}
