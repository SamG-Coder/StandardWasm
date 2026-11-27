using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ReferenceExchangeWasm
    {
        // The ref argument is a traced location with an invariant CLI type.
        // Its GC array belongs to one Wasm agent. No host call or yield occurs
        // between reading, comparing and conditionally replacing its payload.
        internal static int WriteBody(byte[] output, int cursor, byte[] image,
            int[] layout, int[] heaps, int row, int[] objects, int[] signatures, int offset, int arity, ValueFieldProjections projections)
        {
            if (arity != 3 || !IsReferenceLocation(signatures[offset + 1])) return cursor;
            int type = CellValue(signatures[offset + 1]);
            if (signatures[offset] != type || signatures[offset + 2] != type || signatures[offset + 3] != type ||
                !IsCoreLibOperation(image, layout, heaps, row, type == ObjectLayouts.ObjectReference))
                return cursor;
            if (output.Length - cursor < 128) return -3;
            int vector = ObjectLayouts.RegisterArray(objects, ObjectLayouts.ObjectReference);
            if (vector < 0) return vector;
            return Emit(output, cursor, vector, ObjectLayouts.Representation(objects, type), signatures[offset + 1], projections);
        }

        private static bool IsCoreLibOperation(byte[] image, int[] layout, int[] heaps, int row, bool objectCell)
        {
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            bool native = implementation == 0x1000;
            if (native ? !objectCell || MethodDefinitions.ReadColumn(image, layout, row, 0) != 0 :
                implementation != 0x100) return false;
            int name = MethodDefinitions.ReadColumn(image, layout, row, 3);
            if (!ImageBytes.MatchesAscii(image, heaps, (uint)name, native ? "CompareExchangeObject" : "CompareExchange"))
                return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            // The public object overload only checks the ref for null and
            // forwards to CompareExchangeObject. AccessElement emits that check.
            if (objectCell && CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System.Threading",
                "Interlocked", native ? "CompareExchangeObject" : "CompareExchange",
                new byte[] { 0, 3, 0x1c, 0x10, 0x1c, 0x1c, 0x1c }, true))
                return true;
            return !native && CoreStringRange.IsMethod(image, layout, heaps, counts, first, row, "System.Threading",
                "Interlocked", "CompareExchange", new byte[] { 0x10, 1, 3, 0x1e, 0, 0x10, 0x1e, 0, 0x1e, 0, 0x1e, 0 }, true);
        }

        private static int Emit(byte[] output, int cursor, int vector, int representation, int locationType, ValueFieldProjections projections)
        {
            const int location = 0, replacement = 1, comparand = 2, previous = 3;
            output[cursor++] = 1; // one local group
            output[cursor++] = 1; // previous value
            cursor = WasmBinary.ValueType(output, cursor, representation);
            cursor = ValueFieldProjectionWasm.Read(output, cursor, 0, location, vector, locationType, projections);
            cursor = ManagedLocationWasm.CastCellValue(output, cursor, representation);
            cursor = Local(output, cursor, 0x22, previous);
            cursor = Local(output, cursor, 0x20, comparand);
            output[cursor++] = 0xd3; // ref.eq
            output[cursor++] = 0x04; output[cursor++] = 0x40;
            cursor = ValueFieldProjectionWasm.Write(output, cursor, 0, location, replacement, vector, locationType, projections);
            output[cursor++] = 0x0b;
            cursor = Local(output, cursor, 0x20, previous);
            output[cursor++] = 0x0b;
            return cursor;
        }
    }
}
