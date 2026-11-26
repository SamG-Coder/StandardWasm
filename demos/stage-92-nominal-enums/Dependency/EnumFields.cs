using System;

namespace EnumFieldDependency
{
    public enum SignedByte : sbyte { Zero }
    [Flags]
    public enum ByteFlags : byte { None = 0, Low = 1, High = 128 }
    public enum SignedShort : short { Zero }
    public enum UnsignedShort : ushort { Zero }
    [Flags]
    public enum States { None = 0, Visible = 1, Control = 8 }
    public enum OtherStates { None = 0, Control = 8 }
    public enum UnsignedWord : uint { Zero }
    public enum SignedLong : long { Zero }
    public enum UnsignedLong : ulong { Zero }

    public class Frame
    {
        public SignedByte Signed;
        public ByteFlags Flags;
        public SignedShort Short;
        public UnsignedShort UnsignedShort;
        public States State;
        public UnsignedWord Word;
        public SignedLong Long;
        public UnsignedLong UnsignedLong;
    }

    public static class Rules
    {
        public static int Score(States value) => unchecked((int)value + 17);
        public static int Score(OtherStates value) => unchecked((int)value - 29);
        public static ByteFlags Echo(ByteFlags value) => value;
    }
}
