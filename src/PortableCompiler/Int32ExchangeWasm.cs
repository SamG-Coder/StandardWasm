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
            if ((arity != 2 && arity != 3) || signatures[signature] != I32 ||
                signatures[signature + 1] != Int32Location || signatures[signature + 2] != I32 ||
                arity == 3 && signatures[signature + 3] != I32) return used;
            bool compare = arity == 3;
            string name = compare ? "CompareExchange" : "Exchange";
            if (!ImageBytes.MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name))
                return used;
            byte[] methodSignature = compare ? new byte[] { 0, 3, 8, 0x10, 8, 8, 8 } :
                new byte[] { 0, 2, 8, 0x10, 8, 8 };
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System.Threading", "Interlocked",
                    name, methodSignature, true))
                return used;
            if (code.Length - used < 96) return -3;

            // The location is traced storage local to this Wasm agent.
            // Neither access invokes host code or suspends execution.
            const int location = 0, replacement = 1, comparand = 2;
            int previous = arity;
            code[used++] = 1;
            code[used++] = 1;
            code[used++] = 0x7f; // previous value
            used = ValueFieldProjectionWasm.Read(code, used, 0, location, Ints, Int32Location, projections);
            used = Local(code, used, 0x21, previous);
            if (compare)
            {
                used = Local(code, used, 0x20, previous);
                used = Local(code, used, 0x20, comparand);
                code[used++] = 0x46; // i32.eq
                code[used++] = 0x04; code[used++] = 0x40; // if (void)
            }
            used = ValueFieldProjectionWasm.Write(code, used, 0, location, replacement, Ints, Int32Location, projections);
            if (compare) code[used++] = 0x0b;
            used = Local(code, used, 0x20, previous);
            code[used++] = 0x0b;
            return used;
        }
    }
}
