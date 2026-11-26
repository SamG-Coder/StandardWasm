namespace PortableCompiler
{
    internal static class FieldAccess
    {
        // A linked field operand contains a byte-sized receiver type and a
        // storage type that may include a narrow tag above the byte range.
        private const int StorageBits = 9;

        internal static int Encode(int owner, int storage) => (owner << StorageBits) | storage;
        internal static byte Owner(int descriptor) => (byte)(descriptor >> StorageBits);
        internal static int Storage(int descriptor) => descriptor & ((1 << StorageBits) - 1);

        internal static int LoadInstruction(int storage) =>
            storage == SignatureTypes.Signed8 || storage == SignatureTypes.Signed16 ? 3 :
            SignatureTypes.IsNarrow(storage) ? 4 : 2;
    }
}
