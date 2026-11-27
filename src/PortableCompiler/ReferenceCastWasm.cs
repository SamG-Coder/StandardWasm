using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class ReferenceCastWasm
    {
        internal static int Emit(byte[] code, int used, int source, int target, int type, bool cast)
        {
            int heap = WasmBinary.HeapType(type);
            // The nullable test accepts a null cast and keeps an isinst result
            // null. Test first so a wrong type can raise a managed exception.
            used = Local(code, used, 0x20, source);
            code[used++] = 0xfb;
            code[used++] = 21; // ref.test (ref null T)
            used = WasmBinary.Signed(code, used, heap);
            if (cast)
            {
                code[used++] = 0x45; // i32.eqz
                used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.InvalidCastTag);
                return Copy(code, used, source, target, heap);
            }

            code[used++] = 0x04; // if
            code[used++] = 0x40;
            used = Copy(code, used, source, target, heap);
            code[used++] = 0x05; // else
            code[used++] = 0xd0; // ref.null T
            used = WasmBinary.Signed(code, used, heap);
            used = Local(code, used, 0x21, target);
            code[used++] = 0x0b;
            return used;
        }

        internal static int EmitInterface(byte[] code, int used, int source, int target, int type, bool cast,
            int bodyStart, RuntimeInterfaceTests tests)
        {
            // Interface locals use eqref. Keep the original reference when
            // the membership test succeeds, including a null cast.
            used = Local(code, used, 0x20, source);
            used = tests.Call(code, used, bodyStart, type);
            if (cast)
            {
                code[used++] = 0x45; // i32.eqz
                used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.InvalidCastTag);
            }
            else
            {
                code[used++] = 0x04; // if (result eqref)
                code[used++] = 0x6d;
            }
            used = Local(code, used, 0x20, source);
            if (!cast)
            {
                code[used++] = 0x05; // else
                code[used++] = 0xd0; // ref.null none
                code[used++] = StackSpillWasm.NullRef;
                code[used++] = 0x0b;
            }
            return Local(code, used, 0x21, target);
        }

        private static int Copy(byte[] code, int used, int source, int target, int heap)
        {
            used = Local(code, used, 0x20, source);
            code[used++] = 0xfb;
            code[used++] = 23; // ref.cast (ref null T)
            used = WasmBinary.Signed(code, used, heap);
            return Local(code, used, 0x21, target);
        }
    }
}
