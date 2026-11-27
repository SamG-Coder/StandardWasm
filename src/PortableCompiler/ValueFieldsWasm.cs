using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class ValueFieldsWasm
    {
        internal static bool CheckLayouts(int[] objects)
        {
            var visited = new byte[objects[0]];
            for (int type = FirstClass; type < FirstClass + objects[0]; type++)
                if (IsValueType(objects, type) && !Visit(objects, type, visited)) return false;
            return true;
        }

        private static bool Visit(int[] objects, int type, byte[] visited)
        {
            int index = type - FirstClass;
            if (visited[index] != 0) return visited[index] == 2;
            visited[index] = 1;
            for (int i = 0; i < FieldCount(objects, type); i++)
            {
                int field = FieldTypeAt(objects, type, i);
                if (IsValueType(objects, field) && !Visit(objects, field, visited)) return false;
            }
            visited[index] = 2;
            return true;
        }

        internal static int Clear(byte[] code, int used, int type, int local)
        {
            code[used++] = 0xd0;
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
            return Local(code, used, 0x21, local);
        }

        internal static int Materialize(byte[] code, int used, int type, int local)
        {
            // A null representation denotes a default value. Materialize
            // a zero aggregate only when a physical struct load needs it.
            used = Local(code, used, 0x20, local);
            code[used++] = 0xd1;
            code[used++] = 0x04;
            used = WasmBinary.ValueType(code, used, type);
            used = Gc(code, used, 1, type);
            code[used++] = 0x05;
            used = Local(code, used, 0x20, local);
            code[used++] = 0x0b;
            return used;
        }

        internal static int Update(byte[] code, int used, int[] objects, int type, int local, int temporary,
            int field, int replacement, int replacementType, bool location = false)
        {
            if (location)
            {
                used = ManagedValueLocationWasm.ReadLocation(code, used, local, type);
                used = Local(code, used, 0x21, temporary);
                used = LoadOperand(code, used, local, SignatureTypes.ValueLocation(type), SignatureTypes.ValueLocation(type));
                local = temporary;
            }
            used = Materialize(code, used, type, local);
            used = Local(code, used, 0x21, temporary);
            int ordinal = 0;
            for (int i = 0; i < FieldCount(objects, type); i++)
            {
                int storage = FieldTypeAt(objects, type, i);
                if (storage == 0) continue;
                if (ordinal == field)
                    used = LoadOperand(code, used, replacement, replacementType, storage);
                else
                {
                    used = Local(code, used, 0x20, temporary);
                    used = Gc(code, used, FieldAccess.LoadInstruction(storage), type);
                    used = WasmBinary.Unsigned(code, used, ordinal);
                }
                ordinal++;
            }
            // Publish a fresh immutable aggregate to the addressed local.
            // Existing copies keep the original value.
            used = Gc(code, used, 0, type);
            return location ? ManagedValueLocationWasm.WriteSnapshot(code, used) : Local(code, used, 0x21, local);
        }
    }
}
