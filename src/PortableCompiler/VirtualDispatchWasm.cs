namespace PortableCompiler
{
    internal static class VirtualDispatchWasm
    {
        internal static int Arm(byte[] code, int used, int type, int targetReceiver, int target, int arity)
        {
            used = BranchWasm.Local(code, used, 0x20, 0);
            code[used++] = 0xfb;
            code[used++] = 20; // ref.test (ref T)
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
            code[used++] = 0x04; // if
            code[used++] = 0x40; // empty block
            used = BranchWasm.Local(code, used, 0x20, 0);
            if (targetReceiver != PeLibrary.ObjectLayouts.ObjectReference)
            {
                code[used++] = 0xfb;
                code[used++] = 22; // ref.cast (ref T)
                used = WasmBinary.Signed(code, used, WasmBinary.HeapType(targetReceiver));
            }
            for (int argument = 1; argument < arity; argument++)
                used = BranchWasm.Local(code, used, 0x20, argument);
            code[used++] = 0x12; // return_call
            used = WasmBinary.Unsigned(code, used, target);
            code[used++] = 0x0b; // end if
            return used;
        }
    }
}
