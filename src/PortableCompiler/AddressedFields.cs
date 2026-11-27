namespace PortableCompiler
{
    // An addressed field keeps its backing choice across emission retries.
    // Native aggregates and borrowed pointer words use linear memory. Managed aggregates,
    // Int32 and references use cells.
    internal sealed class AddressedFields
    {
        internal const int Int32Cell = -2, ReferenceCell = -3, ValueCell = -4;
        private const int PageBytes = 65536, FieldWidth = 3;
        private int[] fields = new int[12];
        internal int Count;
        private int bytes;

        internal int Find(int owner, int row)
        {
            for (int index = 0; index < Count; index++)
            {
                int at = index * FieldWidth;
                if (fields[at] == owner && fields[at + 1] == row) return fields[at + 2];
            }
            return -1;
        }

        internal int Add(int owner, int row, int size)
        {
            // The native layout model caps field alignment at eight bytes.
            if (size < 1 || bytes > int.MaxValue - size - 7) return -3;
            int offset = (bytes + 7) & -8;
            bytes = offset + size;
            return Record(owner, row, offset);
        }

        internal void AddInt32Cell(int owner, int row) => Record(owner, row, Int32Cell);

        internal void AddReferenceCell(int owner, int row) => Record(owner, row, ReferenceCell);

        internal void AddValueCell(int owner, int row) => Record(owner, row, ValueCell);

        // FieldDef row IDs are positive. Negative keys keep the instance-cell
        // entries distinct from the static backing map.
        internal bool HasInstanceReference(int owner, int row) => Find(owner, -row) == ReferenceCell;

        internal void AddInstanceReference(int owner, int row) => Record(owner, -row, ReferenceCell);

        private int Record(int owner, int row, int offset)
        {
            int at = Count * FieldWidth;
            if (at == fields.Length)
            {
                var grown = new int[at * 2];
                for (int i = 0; i < at; i++) grown[i] = fields[i];
                fields = grown;
            }
            fields[at] = owner;
            fields[at + 1] = row;
            fields[at + 2] = offset;
            Count++;
            return offset;
        }

        internal int Pages() => (bytes >> 16) + ((bytes & (PageBytes - 1)) == 0 ? 0 : 1);
    }

    internal static class NativeStaticWasm
    {
        // A zero plan denotes a wasm32 pointer word. Aggregate plan indices start at one.
        internal const int PointerWordPlan = 0, PointerWordBytes = 4;

        internal static int LoadData(byte[] code, int used, int region, int pages, int cache,
            int offset, int literal, int count, int address)
        {
            code[used++] = 0x23; // global.get cache
            used = WasmBinary.Unsigned(code, used, cache);
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x04; code[used++] = 0x40;
            used = Address(code, used, region, pages, offset, address);
            used = BranchWasm.Local(code, used, 0x20, address);
            used = BranchWasm.Const(code, used, literal);
            used = BranchWasm.Const(code, used, count);
            code[used++] = 0xfc; code[used++] = 8; code[used++] = 0; code[used++] = 0; // memory.init 0 0
            used = BranchWasm.Local(code, used, 0x20, address);
            code[used++] = 0x24; // global.set cache
            used = WasmBinary.Unsigned(code, used, cache);
            code[used++] = 0x0b;
            code[used++] = 0x23;
            return WasmBinary.Unsigned(code, used, cache);
        }

        internal static int Address(byte[] code, int used, int region, int pages, int offset, int address)
        {
            code[used++] = 0x23; // global.get region
            used = WasmBinary.Unsigned(code, used, region);
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x04; code[used++] = 0x40;
            used = BranchWasm.Const(code, used, pages);
            code[used++] = 0x40; code[used++] = 0; // memory.grow 0
            used = BranchWasm.Local(code, used, 0x22, address);
            used = BranchWasm.Const(code, used, -1);
            code[used++] = 0x46; // i32.eq
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0x00; // allocation failed
            code[used++] = 0x0b;
            used = BranchWasm.Local(code, used, 0x20, address);
            used = BranchWasm.Const(code, used, 16);
            code[used++] = 0x74; // page index to byte address
            code[used++] = 0x24;
            used = WasmBinary.Unsigned(code, used, region);
            code[used++] = 0x0b;
            code[used++] = 0x23;
            used = WasmBinary.Unsigned(code, used, region);
            used = BranchWasm.Const(code, used, offset);
            code[used++] = 0x6a;
            return BranchWasm.Local(code, used, 0x21, address);
        }
    }
}
