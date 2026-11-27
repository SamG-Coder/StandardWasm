namespace StaticPointerPinning
{
    public static unsafe class HelperWrite
    {
        private static readonly int* value;
        private static readonly bool wasZero;

        static HelperWrite()
        {
            wasZero = value == null;
            fixed (int** address = &value)
                SetAddress(address);
        }

        private static void SetAddress(int** address) => *address = (int*)1;

        public static int Read() => wasZero && value == (int*)1 ? 101 : -1;
    }

    public static unsafe class InlineWrite
    {
        private static readonly int* value = (int*)2;
        private static readonly bool observedBorrow;

        static InlineWrite()
        {
            fixed (int** address = &value)
                *address = (int*)3;
            observedBorrow = value == (int*)3;
            value = (int*)4;
        }

        public static int Read() => observedBorrow && value == (int*)4 ? 103 : -1;
    }

    public static unsafe class AliasWrite
    {
        private static readonly int* value;
        private static readonly int beforeWrite;

        static AliasWrite()
        {
            fixed (int** address = &value)
            {
                PackedCarrier** alias = (PackedCarrier**)address;
                beforeWrite = Observe();
                SetAddress(alias);
            }
        }

        private static int Observe() => value == null ? 31 : -1;

        private static void SetAddress(PackedCarrier** address) => *address = (PackedCarrier*)5;

        public static int Read() => beforeWrite == 31 && value == (int*)5 ? 109 : -1;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
        private struct PackedCarrier
        {
            internal byte Prefix;
            internal int Value;
        }
    }

    public static unsafe class EscapingAddress
    {
        private static readonly int* value;
        private static readonly int** captured;

        static EscapingAddress()
        {
            fixed (int** address = &value)
                captured = address;
        }

        public static int Read() => captured == null ? -1 : 107;
    }
}
