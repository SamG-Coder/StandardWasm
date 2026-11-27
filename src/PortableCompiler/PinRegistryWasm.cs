using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // Stage 149 (decision 0077): the active string pins. The CLR gives every pin of one string
    // the same address while any pin is live, in one frame or nested ones. Module table 1
    // holds pairs [string, i31(address >> 4)] for the live pins, and a count global (an even
    // number) marks its live end. A pinning frame saves the count on entry and restores it on
    // every exit, so a nested frame reuses an outer pin's address and never outlives it.
    // Blocks are 16-byte aligned, so address >> 4 fits an i31 for any 32-bit memory.
    internal static class PinRegistryWasm
    {
        internal const int Table = 1;
        // The table index 0 funcref table is the delegate thunk table; it is always emitted
        // (possibly empty) when this table exists, so both indices stay fixed.
        internal const int GlobalKey = int.MinValue + 4; // after the three shadow-stack globals

        internal static bool Present(int[] globals)
        {
            for (int i = 0; i < globals[0]; i++)
                if (StaticFields.Owner(globals, i) == 0 && StaticFields.Row(globals, i) == GlobalKey) return true;
            return false;
        }

        internal static int Begin(byte[] code, int used, int count, int frame)
        {
            used = Global(code, used, 0x23, count);
            return Local(code, used, 0x21, frame);
        }

        // Drop this frame's pairs (their strings stay traced only while pinned), then restore.
        internal static int Release(byte[] code, int used, int count, int frame)
        {
            used = Local(code, used, 0x20, frame);
            code[used++] = 0xd0; code[used++] = 0x6d; // ref.null eq
            used = Global(code, used, 0x23, count);
            used = Local(code, used, 0x20, frame);
            code[used++] = 0x6b; // i32.sub
            code[used++] = 0xfc; code[used++] = 17; code[used++] = Table; // table.fill
            used = Local(code, used, 0x20, frame);
            return Global(code, used, 0x24, count);
        }

        // A host call starts with no live pins, even after a raw trap skipped a release.
        internal static int Reset(byte[] code, int used, int count)
        {
            used = Const(code, used, 0);
            code[used++] = 0xd0; code[used++] = 0x6d;
            used = Global(code, used, 0x23, count);
            code[used++] = 0xfc; code[used++] = 17; code[used++] = Table;
            used = Const(code, used, 0);
            return Global(code, used, 0x24, count);
        }

        // address = the live pin's address for source, or stays 0. Searches newest first.
        internal static int Find(byte[] code, int used, int count, int source, int index, int address)
        {
            used = Global(code, used, 0x23, count);
            used = Local(code, used, 0x21, index);
            code[used++] = 0x02; code[used++] = 0x40; // block
            code[used++] = 0x03; code[used++] = 0x40; // loop
            used = Local(code, used, 0x20, index);
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x0d; code[used++] = 1; // br_if block: not pinned
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 2);
            code[used++] = 0x6b;
            used = Local(code, used, 0x22, index);
            code[used++] = 0x25; code[used++] = Table; // table.get
            used = Local(code, used, 0x20, source);
            code[used++] = 0xd3; // ref.eq
            code[used++] = 0x04; code[used++] = 0x40;
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            code[used++] = 0x25; code[used++] = Table;
            code[used++] = 0xfb; code[used++] = 0x16; code[used++] = 0x6c; // ref.cast (ref i31)
            code[used++] = 0xfb; code[used++] = 0x1e; // i31.get_u
            used = Const(code, used, 4);
            code[used++] = 0x74; // i32.shl
            used = Local(code, used, 0x21, address);
            code[used++] = 0x0c; code[used++] = 2; // br block
            code[used++] = 0x0b; // if
            code[used++] = 0x0c; code[used++] = 0; // continue loop
            code[used++] = 0x0b; // loop
            code[used++] = 0x0b; // block
            return used;
        }

        // Append [source, address]; the table grows by doubling. Growth failure traps.
        internal static int Register(byte[] code, int used, int count, int source, int address)
        {
            code[used++] = 0xfc; code[used++] = 16; code[used++] = Table; // table.size
            used = Global(code, used, 0x23, count);
            used = Const(code, used, 2);
            code[used++] = 0x6a;
            code[used++] = 0x49; // i32.lt_u: no room for a pair
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0xd0; code[used++] = 0x6d;
            code[used++] = 0xfc; code[used++] = 16; code[used++] = Table;
            used = Const(code, used, 16);
            code[used++] = 0x6a; // grow by size + 16, at least one pair
            code[used++] = 0xfc; code[used++] = 15; code[used++] = Table; // table.grow
            used = Const(code, used, -1);
            code[used++] = 0x46;
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b;
            code[used++] = 0x0b;
            used = Global(code, used, 0x23, count);
            used = Local(code, used, 0x20, source);
            code[used++] = 0x26; code[used++] = Table; // table.set
            used = Global(code, used, 0x23, count);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x20, address);
            used = Const(code, used, 4);
            code[used++] = 0x76; // i32.shr_u
            code[used++] = 0xfb; code[used++] = 0x1c; // ref.i31
            code[used++] = 0x26; code[used++] = Table;
            used = Global(code, used, 0x23, count);
            used = Const(code, used, 2);
            code[used++] = 0x6a;
            return Global(code, used, 0x24, count);
        }

        // No ref parameters: the compiler must compile itself.
        private static int Global(byte[] code, int used, byte instruction, int index)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, index);
        }
    }
}
