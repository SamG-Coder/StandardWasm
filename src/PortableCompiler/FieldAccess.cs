namespace PortableCompiler
{
    internal static class FieldAccess
    {
        // Both halves hold managed layout IDs or storage tags.
        private const int StorageBits = 16;

        internal static int Encode(int owner, int storage) => (owner << StorageBits) | storage;
        internal static int Owner(int descriptor) => (int)((uint)descriptor >> StorageBits);
        internal static int Storage(int descriptor) => descriptor & ((1 << StorageBits) - 1);

        internal static int LoadInstruction(int storage) =>
            storage == SignatureTypes.Signed8 || storage == SignatureTypes.Signed16 ? 3 :
            SignatureTypes.IsNarrow(storage) ? 4 : 2;
    }
}
