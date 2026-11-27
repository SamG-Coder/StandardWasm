using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Array slots share the same stable cell as addressed struct fields. A
    // null slot is a default value, and ordinary stores replace the snapshot.
    internal static class ValueArrayWasm
    {
        internal static int Take(byte[] code, int used, int array, int reference, int index, bool tracksRoots)
        {
            used = Cell(code, used, array, reference, index);
            code[used++] = 0xd1;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Operands(code, used, reference, index);
            code[used++] = 0xd0; code[used++] = 0x6d; // ref.null eq
            used = ManagedValueLocationWasm.CreateCell(code, used, tracksRoots, reference);
            used = Gc(code, used, 0x0e, array);
            code[used++] = 0x0b;
            return Cell(code, used, array, reference, index);
        }

        private static int Read(byte[] code, int used, int array, int type, int reference, int index)
        {
            used = Cell(code, used, array, reference, index);
            code[used++] = 0xd1;
            code[used++] = 0x04;
            used = WasmBinary.ValueType(code, used, type);
            code[used++] = 0xd0;
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
            code[used++] = 0x05;
            used = Cell(code, used, array, reference, index);
            used = ManagedValueLocationWasm.ReadSnapshot(code, used, type);
            code[used++] = 0x0b;
            return used;
        }

        private static int Write(byte[] code, int used, int array, int type, int reference, int index, int source, int sourceType, bool tracksRoots)
        {
            used = Cell(code, used, array, reference, index);
            code[used++] = 0xd1;
            code[used++] = 0x04; code[used++] = 0x40;
            used = Operands(code, used, reference, index);
            used = LoadOperand(code, used, source, sourceType, type);
            used = ManagedValueLocationWasm.CreateCell(code, used, tracksRoots, reference);
            used = Gc(code, used, 0x0e, array);
            code[used++] = 0x05;
            used = Cell(code, used, array, reference, index);
            used = LoadOperand(code, used, source, sourceType, type);
            used = ManagedValueLocationWasm.WriteSnapshot(code, used);
            code[used++] = 0x0b;
            return used;
        }

        internal static int EmitAccess(byte[] code, int used, bool read, int array, int type,
            int state, int top, int[] states, int spills, int maximum, int spillFlags, int[] objects, bool tracksRoots)
        {
            int position = read ? top - 1 : top - 2;
            int reference = Spill(spills, maximum, spillFlags, states[state + position], position);
            int index = Spill(spills, maximum, spillFlags, states[state + position + 1], position + 1);
            used = GuardNull(code, used, reference);
            used = GuardArrayIndex(code, used, reference, index, false);
            if (!PeLibrary.ObjectLayouts.IsValueType(objects, type))
            {
                // The verifier has bound the exact nominal vector. Reference
                // slots store their payload directly and need no value cell.
                used = Operands(code, used, reference, index);
                if (read)
                {
                    used = Gc(code, used, 0x0b, array);
                    return Local(code, used, 0x21, Spill(spills, maximum, spillFlags, type, position));
                }
                int sourceType = states[state + top];
                used = LoadOperand(code, used, Spill(spills, maximum, spillFlags, sourceType, top), sourceType, type);
                return Gc(code, used, 0x0e, array);
            }
            if (!read)
            {
                int sourceType = states[state + top];
                return Write(code, used, array, type, reference, index,
                    Spill(spills, maximum, spillFlags, sourceType, top), sourceType, tracksRoots);
            }
            used = Read(code, used, array, type, reference, index);
            return Local(code, used, 0x21, Spill(spills, maximum, spillFlags, type, position));
        }

        private static int Operands(byte[] code, int used, int reference, int index) =>
            Local(code, Local(code, used, 0x20, reference), 0x20, index);

        private static int Cell(byte[] code, int used, int array, int reference, int index) =>
            Gc(code, Operands(code, used, reference, index), 0x0b, array);
    }
}
