using static PortableCompiler.ArrayCopyWasm;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class AggregateArrayCopyWasm
    {
        // The source payload is an immutable value snapshot. Copying the
        // mutable Wasm cell would alias two CLR slots and rebind destination
        // byrefs. Read the payload and update the destination cell instead.
        internal static int Emit(byte[] code, int used, int vector, int source, int sourceIndex, int destination,
            int destinationIndex, int length, int cursor, int direction, int snapshot, bool tracksRoots)
        {
            used = Local(code, Const(code, used, 0), 0x21, cursor);
            used = Local(code, Const(code, used, 1), 0x21, direction);
            if (sourceIndex >= 0)
            {
                // For rightward overlap in the same vector, copy from the
                // end so a write cannot change a source we have yet to read.
                used = Local(code, used, 0x20, source);
                used = Local(code, used, 0x20, destination);
                code[used++] = 0xd3; // ref.eq
                used = LoadIndex(code, used, destinationIndex);
                used = LoadIndex(code, used, sourceIndex);
                code[used++] = 0x4b; // i32.gt_u
                code[used++] = 0x71; // i32.and
                used = LoadIndex(code, used, destinationIndex);
                used = LoadIndex(code, used, sourceIndex);
                used = Local(code, used, 0x20, length);
                code[used++] = 0x6a; // i32.add
                code[used++] = 0x49; // i32.lt_u
                code[used++] = 0x71; // i32.and
                code[used++] = 0x04; code[used++] = 0x40; // if backwards
                used = Local(code, used, 0x20, length);
                used = Const(code, used, 1);
                code[used++] = 0x6b; // i32.sub
                used = Local(code, used, 0x21, cursor);
                used = Local(code, Const(code, used, -1), 0x21, direction);
                code[used++] = 0x0b;
            }

            code[used++] = 0x02; code[used++] = 0x40; // block
            code[used++] = 0x03; code[used++] = 0x40; // loop
            used = Local(code, used, 0x20, cursor);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x4f; // i32.ge_u: also catches -1 after a backwards loop
            code[used++] = 0x0d; code[used++] = 1; // leave block

            used = Cell(code, used, vector, source, sourceIndex, cursor);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; code[used++] = 0x6d; // if result eqref
            code[used++] = 0xd0; code[used++] = 0x6d; // absent source cell means default snapshot
            code[used++] = 0x05; // else
            used = Cell(code, used, vector, source, sourceIndex, cursor);
            used = ManagedValueLocationWasm.ReadRawSnapshot(code, used);
            code[used++] = 0x0b;
            used = Local(code, used, 0x21, snapshot);

            used = Cell(code, used, vector, destination, destinationIndex, cursor);
            code[used++] = 0xd1;
            code[used++] = 0x04; code[used++] = 0x40; // if no destination cell
            used = Local(code, used, 0x20, snapshot);
            code[used++] = 0xd1; code[used++] = 0x45; // snapshot is not default
            code[used++] = 0x04; code[used++] = 0x40;
            used = Element(code, used, destination, destinationIndex, cursor);
            used = Local(code, used, 0x20, snapshot);
            used = ManagedValueLocationWasm.CreateCell(code, used, tracksRoots, destination);
            used = Gc(code, used, 0x0e, vector); // array.set
            code[used++] = 0x0b;
            code[used++] = 0x05; // else
            used = Cell(code, used, vector, destination, destinationIndex, cursor);
            used = Local(code, used, 0x20, snapshot);
            used = ManagedValueLocationWasm.WriteSnapshot(code, used);
            code[used++] = 0x0b;

            used = Local(code, used, 0x20, cursor);
            used = Local(code, used, 0x20, direction);
            code[used++] = 0x6a; // i32.add
            used = Local(code, used, 0x21, cursor);
            code[used++] = 0x0c; code[used++] = 0; // continue loop
            code[used++] = 0x0b; code[used++] = 0x0b;
            return used;
        }

        private static int Cell(byte[] code, int used, int vector, int array, int index, int cursor) =>
            Gc(code, Element(code, used, array, index, cursor), 0x0b, vector); // array.get

        private static int Element(byte[] code, int used, int array, int index, int cursor)
        {
            used = Local(code, used, 0x20, array);
            used = Local(code, used, 0x20, cursor);
            if (index >= 0)
            {
                used = LoadIndex(code, used, index);
                code[used++] = 0x6a; // i32.add
            }
            return used;
        }
    }
}
