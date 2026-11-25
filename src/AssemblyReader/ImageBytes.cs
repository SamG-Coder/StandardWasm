namespace PeLibrary
{
    internal static class ImageBytes
    {
        internal static bool Fits(uint offset, uint length, uint limit) =>
            offset <= limit && length <= limit - offset;

        internal static uint U16(byte[] bytes, uint offset) =>
            (uint)(bytes[offset] | (bytes[offset + 1] << 8));

        internal static uint U32(byte[] bytes, uint offset) =>
            U16(bytes, offset) | (U16(bytes, offset + 2) << 16);
    }
}
