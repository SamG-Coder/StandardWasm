using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // A direct field read from a declared blittable T* parameter needs only
    // the native layout. It must not allocate a parallel managed value layout.
    internal static class NativePointerFields
    {
        internal static bool Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int method, int parameters, int[] records, int at, int[] clauses)
        {
            int kind = records[at];
            if ((kind != GetField && kind != FieldAddress) || records[at + 3] != 0 ||
                records.Length <= at + 2 * Width + 3) return false;
            int previous = PreviousLoad(records, at);
            bool nested = previous < 0;
            if (nested)
            {
                // FieldDef and MemberRef field IL operands have fixed width.
                previous = at - 5 * Width;
                if (previous < 0 || records[previous] != FieldAddress || records[previous + 2] * Width != at ||
                    records[previous + 2 * Width + 2] <= 0 || records[previous + 2 * Width + 3] >= 0) return false;
            }

            int root = nested ? PreviousLoad(records, previous) : previous;
            if (root < 0) return false;
            int declared = FirstValuePointer(image, layout, heaps, method, parameters, records[root + 1]);
            if (declared <= 0 || ValueOperands.Entry(records, at / Width, clauses)) return false;
            int type = nested ? -records[previous + 2 * Width + 3] : declared;
            int row = records[at + 1];
            if (ObjectLayouts.Owner(image, heaps, layout, row, true) != type) return false;
            var field = new int[2];
            if (NativeValueLayouts.Field(bundle, owner, type, row, field) != 0) return false;
            int opcode = field[1];
            if (kind == GetField && opcode == 0) return false;
            if (opcode == 0)
            {
                int backing = NestedFieldType(image, layout, heaps, row);
                if (nested || backing <= 0) return false;
                opcode = -backing;
            }
            int storage = kind == GetField ? StackDepths.IndirectType(opcode) : SignatureTypes.Native;
            if (storage <= 0) return false;
            records[at + 3] = FieldAccess.Encode(0, storage);
            records[at + 2 * Width + 2] = field[0] + 1;
            records[at + 2 * Width + 3] = opcode;
            return true;
        }

        private static int PreviousLoad(int[] records, int at)
        {
            int previous = at - Width;
            if (previous >= 0 && records[previous] == Load && records[previous + 2] * Width == at) return previous;
            previous -= Width;
            return previous >= 0 && records[previous] == Load && records[previous + 2] * Width == at ? previous : -1;
        }

        private static int FirstValuePointer(byte[] image, int[] layout, int[] heaps, int method, int parameters, int argument)
        {
            bool instance = (MethodDefinitions.ReadColumn(image, layout, method, 2) & 0x10) == 0;
            int first = instance ? 1 : 0;
            if (argument != first || first >= parameters) return -2;
            var cursor = new int[1];
            int blob = MethodDefinitions.ReadColumn(image, layout, method, 4);
            int end = instance ? SignatureReader.ReadInstance(image, heaps, blob, parameters - 1, cursor) :
                SignatureReader.ReadStatic(image, heaps, blob, parameters, cursor);
            if (end <= cursor[0]) return -2;
            int result = image[cursor[0]++];
            if (result != 1 && NativeValueLayouts.PrimitiveSize(result) < 1 || result == 0x0f ||
                !SignatureReader.TakeElement(image, cursor, end, 0x0f) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return -2;
            int coded = CompressedUnsigned(image, cursor[0], end, cursor);
            return coded > 0 && (coded & 3) == 0 ? coded >> 2 : -2;
        }

        private static int NestedFieldType(byte[] image, int[] layout, int[] heaps, int row)
        {
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return table;
            var field = new int[3];
            AssemblyReferences.ReadField(image, layout, table, row, field);
            int blob = field[2];
            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            if (blob < 1 || blob >= heaps[1]) return -2;
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            if (size < 3 || size > limit - cursor[0]) return -2;
            int end = cursor[0] + size;
            if (!SignatureReader.TakeElement(image, cursor, end, 0x06) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return -2;
            int coded = CompressedUnsigned(image, cursor[0], end, cursor);
            return coded > 0 && (coded & 3) == 0 && cursor[0] == end ? coded >> 2 : -2;
        }
    }
}
