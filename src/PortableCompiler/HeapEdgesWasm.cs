using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Typed Wasm reads derive from the same layouts as ordinary field access.
    // Shape queries are separate from reads so a null child never ends enumeration.
    internal static class HeapEdgesWasm
    {
        internal static int Write(byte[] code, int used, int[] objects, bool read)
        {
            if (code.Length - used < 228) return -3;
            code[used++] = 0;
            // A value location retains its current snapshot (or projection)
            // and its original owner. Other wrappers have only a single edge.
            used = Wrapper(code, used, ValueLocation(FirstClass), read, 2);
            used = Wrapper(code, used, ObjectLocation, read);
            used = Wrapper(code, used, ObjectSpan, read);
            used = Wrapper(code, used, CharacterSpanCallback, read);
            for (int index = objects[0] - 1; index >= 0; index--)
            {
                int type = FirstClass + index;
                if (IsInterface(objects, type) || PrimitiveBoxing.IsLayout(objects, type)) continue;
                int element = ArrayElement(objects, type);
                if (element != 0 && !RootStackWasm.IsTraced(element)) continue;
                int edges = element == 0 ? CountFields(objects, type) : 0;
                if (code.Length - used < 64 || edges > (code.Length - used - 64) / 28) return -3;

                // A derived struct must win over its parent, including when it has no edges.
                used = Local(code, used, 0x20, 0);
                used = RefType(code, used, 0x14, type); // ref.test, excluding null
                code[used++] = 0x04; code[used++] = 0x40;
                if (element != 0) used = Array(code, used, type, read);
                else if (read) used = Fields(code, used, objects, type);
                else used = Const(code, used, edges);
                code[used++] = 0x0f;
                code[used++] = 0x0b;
            }
            used = read ? Null(code, used) : Const(code, used, 0);
            code[used++] = 0x0b;
            return used;
        }

        private static int CountFields(int[] objects, int type)
        {
            int count = 0, fields = LayoutFieldCount(objects, type);
            for (int i = 0; i < fields; i++)
                if (RootStackWasm.IsTraced(LayoutFieldType(objects, type, i))) count++;
            return count;
        }

        private static int Wrapper(byte[] code, int used, int type, bool read, int edges = 1)
        {
            used = Local(code, used, 0x20, 0);
            used = RefType(code, used, 0x14, type);
            code[used++] = 0x04; code[used++] = 0x40;
            if (read)
            {
                for (int field = 0; field < edges; field++)
                {
                    used = Local(code, used, 0x20, 1);
                    used = Const(code, used, field);
                    code[used++] = 0x46; // i32.eq
                    code[used++] = 0x04; code[used++] = 0x40;
                    used = Receiver(code, used, type);
                    used = Gc(code, used, 2, type);
                    code[used++] = (byte)field;
                    code[used++] = 0x0f;
                    code[used++] = 0x0b;
                }
                used = Null(code, used);
            }
            else used = Const(code, used, edges);
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            return used;
        }

        private static int Fields(byte[] code, int used, int[] objects, int type)
        {
            int ordinal = 0, edge = 0, fields = LayoutFieldCount(objects, type);
            for (int i = 0; i < fields; i++)
            {
                int field = LayoutFieldType(objects, type, i);
                if (field == 0) continue;
                if (RootStackWasm.IsTraced(field))
                {
                    used = Local(code, used, 0x20, 1);
                    used = Const(code, used, edge++);
                    code[used++] = 0x46; // i32.eq
                    code[used++] = 0x04; code[used++] = 0x40;
                    used = Receiver(code, used, type);
                    used = Gc(code, used, 2, type); // struct.get
                    used = WasmBinary.Unsigned(code, used, ordinal);
                    code[used++] = 0x0f;
                    code[used++] = 0x0b;
                }
                ordinal++;
            }
            return Null(code, used);
        }

        private static int Array(byte[] code, int used, int type, bool read)
        {
            if (read) used = Local(code, used, 0x20, 1);
            used = Receiver(code, used, type);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            if (!read) return used;
            code[used++] = 0x49; // i32.lt_u, also rejects negative indices
            code[used++] = 0x04; code[used++] = 0x40;
            used = Receiver(code, used, type);
            used = Local(code, used, 0x20, 1);
            used = Gc(code, used, 0x0b, type); // array.get
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            return Null(code, used);
        }

        private static int Receiver(byte[] code, int used, int type)
        {
            used = Local(code, used, 0x20, 0);
            return RefType(code, used, 0x16, type); // ref.cast non-null
        }

        private static int RefType(byte[] code, int used, byte operation, int type)
        {
            code[used++] = 0xfb;
            code[used++] = operation;
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
        }

        private static int Null(byte[] code, int used)
        {
            code[used++] = 0xd0; code[used++] = 0x6d;
            return used;
        }
    }
}
