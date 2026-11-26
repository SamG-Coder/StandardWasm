namespace PeLibrary
{
    internal static class PointerStorage
    {
        internal const byte VoidPointer = NativeStorage.Scalar + 1;
        internal const byte NativeZero = VoidPointer + 1;
        internal const int Element = 0x0f;

        internal static bool IsVoidPointer(byte[] image, int position, int end) =>
            position < end && image[position] == 1; // ELEMENT_TYPE_VOID is the pointee.
    }
}
