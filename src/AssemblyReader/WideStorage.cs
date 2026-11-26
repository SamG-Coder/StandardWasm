namespace PeLibrary
{
    internal static class WideStorage
    {
        // Signedness belongs to the IL operation. Int64 and UInt64 share storage.
        internal const byte Scalar = 8, Array = 9;

        internal static bool IsIntegerElement(int element) => element == 0x0a || element == 0x0b;
    }
}
