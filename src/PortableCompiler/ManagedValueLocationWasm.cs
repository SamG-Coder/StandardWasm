using static PortableCompiler.BranchWasm;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    internal static class ManagedValueLocationWasm
    {
        internal static int TypeBytes(bool tracksRoots) => tracksRoots ? 6 : 4;

        // A class field owns the cell. Ordinary field loads return its immutable
        // value snapshot, so only addresses can share the mutable location.
        internal static bool UsesCell(int[] objects, int owner, int storage) =>
            !IsValueType(objects, owner) && IsValueType(objects, storage);

        internal static int WriteType(byte[] output, int cursor, bool tracksRoots)
        {
            output[cursor++] = 0x5f; // struct
            output[cursor++] = tracksRoots ? (byte)2 : (byte)1;
            output[cursor++] = 0x6d; // nullable eqref, null denotes the default snapshot
            output[cursor++] = 1; // mutable
            if (tracksRoots)
            {
                output[cursor++] = 0x6d; // containing class or array, absent for frame and projection cells
                output[cursor++] = 0; // immutable: snapshot replacement must preserve the owner
            }
            return cursor;
        }

        internal static int WriteFieldType(byte[] output, int cursor)
        {
            output[cursor++] = 0x63; // ref null
            return WasmBinary.Signed(output, cursor, WasmBinary.ValueLocationHeap);
        }

        internal static int Read(byte[] code, int used, int owner, int reference, int field, int type)
        {
            used = Cell(code, used, owner, reference, field);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; // if
            used = WasmBinary.ValueType(code, used, type);
            code[used++] = 0xd0; // an untouched class field reads as the default value
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
            code[used++] = 0x05; // else
            used = Cell(code, used, owner, reference, field);
            used = CellInstruction(code, used, 2); // struct.get
            code[used++] = 0;
            code[used++] = 0xfb;
            code[used++] = 0x17; // ref.cast null
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
            code[used++] = 0x0b;
            return used;
        }

        internal static int Write(byte[] code, int used, int owner, int reference, int field,
            int source, int sourceType, int type, bool tracksRoots)
        {
            used = Cell(code, used, owner, reference, field);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; code[used++] = 0x40;
            // Allocate the cell on the first write. Later writes replace only the snapshot.
            used = Local(code, used, 0x20, reference);
            used = LoadOperand(code, used, source, sourceType, type);
            used = CreateCell(code, used, tracksRoots, reference);
            used = Gc(code, used, 5, owner);
            used = WasmBinary.Unsigned(code, used, field);
            code[used++] = 0x05;
            used = Cell(code, used, owner, reference, field);
            used = LoadOperand(code, used, source, sourceType, type);
            used = CellInstruction(code, used, 5); // struct.set
            code[used++] = 0;
            code[used++] = 0x0b;
            return used;
        }

        internal static int Take(byte[] code, int used, int owner, int reference, int field, bool tracksRoots)
        {
            used = Cell(code, used, owner, reference, field);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, reference);
            code[used++] = 0xd0; code[used++] = 0x6d; // ref.null eq
            used = CreateCell(code, used, tracksRoots, reference);
            used = Gc(code, used, 5, owner);
            used = WasmBinary.Unsigned(code, used, field);
            code[used++] = 0x0b;
            return Cell(code, used, owner, reference, field);
        }

        // The value is already on the stack. Leave the cell, with its original
        // owner attached so an interior alias keeps the entire object alive.
        internal static int CreateCell(byte[] code, int used, bool tracksRoots, int owner = -1)
        {
            if (tracksRoots)
            {
                if (owner >= 0) used = Local(code, used, 0x20, owner);
                else
                {
                    code[used++] = 0xd0; // ref.null eq
                    code[used++] = 0x6d;
                }
            }
            return CellInstruction(code, used, 0); // struct.new
        }

        internal static int ClearFrameCell(byte[] code, int used, int cell)
        {
            used = Local(code, used, 0x20, cell);
            code[used++] = 0xd0; // ref.null eq is the default snapshot
            code[used++] = 0x6d;
            return WriteSnapshot(code, used);
        }

        internal static int ReadLocation(byte[] code, int used, int local, int type, ValueFieldProjections projections = null, int start = 0)
        {
            // Evaluation-stack locations can spill as eqref. Recover the
            // cell's heap type before reading its snapshot.
            used = LoadOperand(code, used, local, SignatureTypes.ValueLocation(type), SignatureTypes.ValueLocation(type));
            return ReadSnapshot(code, used, type, projections, start);
        }

        internal static int ReadRawSnapshot(byte[] code, int used)
        {
            used = CellInstruction(code, used, 2);
            code[used++] = 0;
            return used;
        }

        internal static int ReadSnapshot(byte[] code, int used, int type, ValueFieldProjections projections = null, int start = 0)
        {
            used = projections != null && projections.Aggregates
                ? ValueFieldProjectionWasm.CallSnapshot(code, used, start, false, projections)
                : ReadRawSnapshot(code, used);
            code[used++] = 0xfb;
            code[used++] = 0x17; // ref.cast null
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
        }

        internal static int WriteSnapshot(byte[] code, int used, ValueFieldProjections projections = null, int start = 0)
        {
            if (projections != null && projections.Aggregates)
                return ValueFieldProjectionWasm.CallSnapshot(code, used, start, true, projections);
            used = CellInstruction(code, used, 5);
            code[used++] = 0;
            return used;
        }

        private static int Cell(byte[] code, int used, int owner, int reference, int field)
        {
            used = Local(code, used, 0x20, reference);
            used = Gc(code, used, 2, owner);
            return WasmBinary.Unsigned(code, used, field);
        }

        private static int CellInstruction(byte[] code, int used, byte instruction)
        {
            code[used++] = 0xfb;
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, WasmBinary.ValueLocationHeap);
        }
    }
}
