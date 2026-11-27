namespace UInt32ByrefFixture
{
    public static class Kernels
    {
        public static uint DirectOut(uint value)
        {
            SetOut(value, out uint result);
            return result;
        }

        public static uint DirectRef(uint value)
        {
            ShiftAndMark(ref value);
            return value;
        }

        public static uint ClosedGenericOut(uint value)
        {
            WriteOut(value, out uint result);
            return result;
        }

        public static uint ClosedGenericRef(uint value)
        {
            uint target = 0;
            Replace(ref target, value);
            return Read(ref target) >> 1;
        }

        public static uint SharedRef(uint value)
        {
            UpdateBoth(ref value, ref value);
            return value;
        }

        public static uint NominalOverload(uint value)
        {
            int signed = unchecked((int)value);
            Pick(ref signed);
            Pick(ref value);
            return value ^ unchecked((uint)signed);
        }

        private static void SetOut(uint source, out uint target) => target = source | 0x80000000u;
        private static void ShiftAndMark(ref uint value) => value = (value >> 1) | 0x100u;
        private static void WriteOut<T>(T source, out T target) => target = source;
        private static void Replace<T>(ref T target, T source) => target = source;
        private static T Read<T>(ref T target) => target;

        private static void UpdateBoth(ref uint first, ref uint second)
        {
            first |= 0x80000000u;
            second >>= 1;
        }

        private static void Pick(ref int value) => value = 0x11;
        private static void Pick(ref uint value) => value = 0x80000022u;
    }
}
