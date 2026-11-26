namespace PeLibrary
{
    internal static class NativeStorage
    {
        // The browser target uses the wasm32 ABI. Keep a separate CLI
        // stack category even though both native int and int32 emit i32.
        internal const byte Scalar = 17;
        internal const byte Vector = ConsoleStorage.Writer + 1;

        internal static bool IsIntegerElement(int element) => element == 0x18 || element == 0x19;
    }
}
