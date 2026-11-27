using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class Int32ExchangeWasm
    {
        internal static int WriteBody(byte[] code, int used, byte[] image, int[] layout, int[] heaps, int row,
            int[] signatures, int signature, int arity, ValueFieldProjections projections)
        {
            if (arity != 2 || signatures[signature] != I32 || signatures[signature + 1] != Int32Location ||
                signatures[signature + 2] != I32 ||
                !ImageBytes.MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "Exchange"))
                return used;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System.Threading", "Interlocked",
                    "Exchange", new byte[] { 0, 2, 8, 0x10, 8, 8 }, true))
                return used;
            if (code.Length - used < 96) return -3;

            // The location is traced storage local to this Wasm agent.
            // Neither access invokes host code or suspends execution.
            const int location = 0, replacement = 1, previous = 2;
            code[used++] = 1;
            code[used++] = 1;
            code[used++] = 0x7f; // previous value
            used = ValueFieldProjectionWasm.Read(code, used, 0, location, Ints, Int32Location, projections);
            used = Local(code, used, 0x21, previous);
            used = ValueFieldProjectionWasm.Write(code, used, 0, location, replacement, Ints, Int32Location, projections);
            used = Local(code, used, 0x20, previous);
            code[used++] = 0x0b;
            return used;
        }
    }
}
