using System.Runtime.CompilerServices;

namespace ScalarBitCastFixture
{
    public static class Probe
    {
        public static int SingleBits() => Unsafe.BitCast<float, int>(-0.0f);

        public static int SingleFromBits() => Unsafe.BitCast<uint, float>(0x3fc00000u) == 1.5f ? 1 : 0;

        public static int SingleNaNPayload() =>
            Unsafe.BitCast<float, int>(Unsafe.BitCast<int, float>(0x7fc12345));

        public static int DoubleBits() =>
            Unsafe.BitCast<double, ulong>(-0.0) == 0x8000000000000000ul ? 1 : 0;

        public static int DoubleFromBits() => Unsafe.BitCast<long, double>(0x3ff8000000000000L) == 1.5 ? 1 : 0;

        public static int DoubleNaNPayload() =>
            Unsafe.BitCast<double, ulong>(Unsafe.BitCast<ulong, double>(0x7ff8123456789abcul)) == 0x7ff8123456789abcul ? 1 : 0;
    }
}
