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
            used = TailCall(code, used, target, arity);
            code[used++] = 0x0b; // end if
            return used;
        }

        internal static int BoxArm(byte[] code, int used, int boxLayout, int element, int valueLayout, int target, int arity)
        {
            used = PrimitiveBoxing.TestType(code, used, 0, boxLayout);
            code[used++] = 0x04;
            code[used++] = 0x40;
            used = PrimitiveBoxing.TestElement(code, used, 0, boxLayout, element);
            code[used++] = 0x04;
            code[used++] = 0x40;
            // Primitive methods use the existing read-only value receiver ABI.
            // The original box remains unchanged and retains its identity.
            used = PrimitiveBoxing.Payload(code, used, 0, element, boxLayout);
            used = BranchWasm.Gc(code, used, 0, valueLayout); // struct.new
            used = TailCall(code, used, target, arity);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int TailCall(byte[] code, int used, int target, int arity)
        {
            for (int argument = 1; argument < arity; argument++)
                used = BranchWasm.Local(code, used, 0x20, argument);
            code[used++] = 0x12; // return_call
            return WasmBinary.Unsigned(code, used, target);
        }
    }
}
