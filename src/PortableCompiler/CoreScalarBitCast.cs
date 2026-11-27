using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CoreScalarBitCast
    {
        // Equal-width non-reference primitives satisfy all three guards in
        // Unsafe.BitCast. Reinterpret their Wasm bits with no numeric conversion.
        internal static bool Lower(byte[] image, int[] layout, int[] heaps, int method,
            int[] arguments, int[] body, int[] records)
        {
            if (arguments == null || arguments.Length != 2 || body[1] < 3 || body[5] != 0 ||
                !GenericScalar.IsScalar(arguments[0]) || !GenericScalar.IsScalar(arguments[1])) return false;
            int from = GenericScalar.Storage(arguments[0]), to = GenericScalar.Storage(arguments[1]);
            if (Opcode(from, to) == 0 || !ImageBytes.MatchesAscii(image, heaps,
                (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), "BitCast")) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, method, "System.Runtime.CompilerServices",
                    "Unsafe", "BitCast", new byte[] { 0x10, 2, 1, 0x1e, 1, 0x1e, 0 }, true)) return false;

            records[0] = Load;
            records[2] = 1;
            records[Width] = ScalarBitCast;
            records[Width + 1] = from;
            records[Width + 2] = 2;
            records[Width + 3] = to;
            records[2 * Width] = Return;
            records[2 * Width + 2] = body[1];
            return true;
        }

        internal static int Opcode(int from, int to) =>
            from == F32 && to == I32 ? 0xbc : from == I32 && to == F32 ? 0xbe :
            from == F64 && to == I64 ? 0xbd : from == I64 && to == F64 ? 0xbf : 0;
    }
}
