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
