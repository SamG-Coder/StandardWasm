using System;

namespace EnumBoxes
{
    public enum Signed { Five = 5 }
    public enum Other { Five = 5 }
    public enum Unsigned : uint { Five = 5 }
    public enum Small : sbyte { Negative = -7 }
    public enum Wide : long { Value = -1234567890123 }

    public static class Probe
    {
        private static object Box(int value) => (Signed)value;
        private static object BoxUnsigned(uint value) => (Unsigned)value;
        private static object BoxGeneric<T>(T value) => value;

        public static int NominalType()
        {
            object box = Box(5);
            return box is Signed && !(box is Other) && !(box is int) && box.GetType() == typeof(Signed) ? 181 : -1;
        }

        public static int AllocationIdentity()
        {
            object a = Box(5);
            object b = Box(5);
            return ReferenceEquals(a, a) && !ReferenceEquals(a, b) ? 191 : -1;
        }

        public static int CompatibleUnbox()
        {
            object box = Box(5);
            object integer = 5;
            return (int)box == 5 && (Other)box == Other.Five && (Signed)integer == Signed.Five ? 193 : -1;
        }

        public static int IncompatibleUnbox()
        {
            object box = BoxUnsigned(5);
            try { return (int)(Signed)box; }
            catch (InvalidCastException) { return 197; }
        }

        public static int NarrowAndWide()
        {
            object small = BoxGeneric(Small.Negative);
            object wide = BoxGeneric(Wide.Value);
            return (sbyte)small == -7 && (long)wide == (long)Wide.Value ? 199 : -1;
        }

        public static int CoreInterface()
        {
            object box = Box(5);
            return box is IFormattable && !(box is IDisposable) ? 211 : -1;
        }

        public static int NullUnbox()
        {
            object box = null;
            try { return (int)(Signed)box; }
            catch (NullReferenceException) { return 223; }
        }

        public static int VirtualDiagnostic()
        {
            object box = Box(5);
            return box.ToString().Length;
        }
    }
}
