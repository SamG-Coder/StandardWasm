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
            if (arity < 1 || arity > 3 || signatures[signature] != I32 ||
                signatures[signature + 1] != Int32Location ||
                (arity > 1 && signatures[signature + 2] != I32) ||
                (arity == 3 && signatures[signature + 3] != I32)) return used;
            bool increment = arity == 1, compare = arity == 3;
            string name = increment ? "Increment" : compare ? "CompareExchange" : "Exchange";
            if (!ImageBytes.MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name))
                return used;
            // The original CoreCLR Increment has managed IL. Only the
            // exact Mono InternalCall needs its method body supplied here.
            if (increment && (MethodDefinitions.ReadColumn(image, layout, row, 0) != 0 ||
                MethodDefinitions.ReadColumn(image, layout, row, 1) != 0x1000)) return used;
            byte[] methodSignature = increment ? new byte[] { 0, 1, 8, 0x10, 8 } :
                compare ? new byte[] { 0, 3, 8, 0x10, 8, 8, 8 } : new byte[] { 0, 2, 8, 0x10, 8, 8 };
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
            int value = arity;
            code[used++] = 1;
            code[used++] = 1;
            code[used++] = 0x7f; // return value
            used = ValueFieldProjectionWasm.Read(code, used, 0, location, Ints, Int32Location, projections);
            if (increment)
            {
                used = Const(code, used, 1);
                code[used++] = 0x6a; // i32.add wraps and returns the new value.
            }
            used = Local(code, used, 0x21, value);
            if (compare)
            {
                used = Local(code, used, 0x20, value);
                used = Local(code, used, 0x20, comparand);
                code[used++] = 0x46; // i32.eq
                code[used++] = 0x04; code[used++] = 0x40; // if (void)
            }
            used = ValueFieldProjectionWasm.Write(code, used, 0, location, increment ? value : replacement, Ints, Int32Location, projections);
            if (compare) code[used++] = 0x0b;
            used = Local(code, used, 0x20, value);
            code[used++] = 0x0b;
            return used;
        }
    }
}
