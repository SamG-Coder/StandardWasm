namespace PeLibrary
{
    internal static class FloatStorage
    {
        internal const byte Single = 10, Double = 11;

        internal static byte FromElement(int element) =>
            element == 0x0c ? Single : element == 0x0d ? Double : (byte)0;
    }
}
