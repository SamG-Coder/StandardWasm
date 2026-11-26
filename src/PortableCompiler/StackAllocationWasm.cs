namespace PortableCompiler
{
    // Stage 137 (decision 0065). localloc on a module-owned shadow stack in memory 0.
    // The region is 16 pages (1 MiB) taken with memory.grow on a frame's first use, so it
    // never overlaps host-owned pages such as Stage 131's resources. $sp grows down from
    // the limit. A frame that allocates saves $sp on entry and restores it on every exit:
    // each ret, and an outer catch_all_ref that rethrows. Exhaustion is an engine trap:
    // the CLR raises StackOverflowException, which ends the process.
    internal static class StackAllocationWasm
    {
        internal const int Pages = 16, Capacity = 160;

        // Function prologue: initialize the region once, then save $sp.
        internal static int Begin(byte[] code, int used, int sp, int @base, int limit, int frame, int scratch)
        {
            used = Global(code, used, 0x23, sp);
            code[used++] = 0x45; // i32.eqz
            code[used++] = 0x04; code[used++] = 0x40;
            used = BranchWasm.Const(code, used, Pages);
            code[used++] = 0x40; code[used++] = 0; // memory.grow 0
            used = BranchWasm.Local(code, used, 0x22, scratch);
            used = BranchWasm.Const(code, used, -1);
            code[used++] = 0x46;
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b; // grow failed: trap
            used = BranchWasm.Local(code, used, 0x20, scratch);
            used = BranchWasm.Const(code, used, 16);
            code[used++] = 0x74; // i32.shl: pages to bytes
            used = Global(code, used, 0x24, @base);
            used = Global(code, used, 0x23, @base);
            used = BranchWasm.Const(code, used, Pages << 16);
            code[used++] = 0x6a;
            used = Global(code, used, 0x24, limit);
            used = Global(code, used, 0x23, limit);
            used = Global(code, used, 0x24, sp);
            code[used++] = 0x0b;
            used = BranchWasm.Const(code, used, 0);
            used = BranchWasm.Local(code, used, 0x21, scratch);
            used = Global(code, used, 0x23, sp);
            return BranchWasm.Local(code, used, 0x21, frame);
        }

        // [size] -> [address]. Traps on exhaustion (the CLR raises StackOverflowException,
        // which ends the process). The block is zeroed: ECMA-335 requires it with
        // InitLocals, and without it the contents are unspecified (decision 0051).
        internal static int Allocate(byte[] code, int used, int sp, int @base, int size, int result, int scratch)
        {
            used = BranchWasm.Local(code, used, 0x20, size);
            used = Global(code, used, 0x23, sp);
            used = Global(code, used, 0x23, @base);
            code[used++] = 0x6b; // i32.sub
            code[used++] = 0x4b; // i32.gt_u
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b;
            used = Global(code, used, 0x23, sp);
            used = BranchWasm.Local(code, used, 0x20, size);
            used = BranchWasm.Const(code, used, 15);
            code[used++] = 0x6a;
            used = BranchWasm.Const(code, used, -16);
            code[used++] = 0x71; // align up to 16
            code[used++] = 0x6b;
            used = BranchWasm.Local(code, used, 0x22, scratch);
            used = Global(code, used, 0x23, @base);
            code[used++] = 0x49; // i32.lt_u
            code[used++] = 0x04; code[used++] = 0x40; code[used++] = 0x00; code[used++] = 0x0b;
            used = BranchWasm.Local(code, used, 0x20, scratch);
            used = Global(code, used, 0x24, sp);
            used = BranchWasm.Local(code, used, 0x20, scratch);
            used = BranchWasm.Const(code, used, 0);
            used = BranchWasm.Local(code, used, 0x20, size);
            code[used++] = 0xfc; code[used++] = 0x0b; code[used++] = 0; // memory.fill 0
            used = BranchWasm.Local(code, used, 0x20, scratch);
            return BranchWasm.Local(code, used, 0x21, result);
        }

        // Before each ret: release this frame's allocations.
        internal static int Release(byte[] code, int used, int sp, int frame)
        {
            used = BranchWasm.Local(code, used, 0x20, frame);
            used = Global(code, used, 0x24, sp);
            return used;
        }

        // No ref parameters: the compiler must compile itself, and managed byrefs (#273)
        // are not lowered yet.
        private static int Global(byte[] code, int used, byte instruction, int index)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, index);
        }
    }
}
