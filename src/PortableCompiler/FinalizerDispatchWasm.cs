using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class FinalizerDispatchWasm
    {
        internal static int Write(byte[] code, int used, int[] objects, FinalizerSlots finalizers,
            int first, int imports, int roots)
        {
            const int receiver = 0, frame = 1;
            if (code.Length - used < 192) return -3;
            code[used++] = 2;
            code[used++] = 1; code[used++] = 0x6d; // eqref receiver
            code[used++] = 1; code[used++] = 0x7f; // root stack base
            used = ReachabilityWasm.Call(code, used, first + FinalizerQueueWasm.Pop);
            used = Local(code, used, 0x22, receiver);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04; code[used++] = 0x40;
            used = Const(code, used, 0);
            code[used++] = 0x0f;
            code[used++] = 0x0b;

            // The receiver is no longer pending. It is an ordinary active root
            // for the duration of its managed callback, including reentrant marking.
            used = RootStackWasm.Enter(code, used, roots, frame, 1);
            used = RootStackWasm.Store(code, used, frame, 0, receiver);
            code[used++] = 0x02; code[used++] = ExceptionDispatchWasm.ExceptionReference;
            code[used++] = 0x1f; code[used++] = 0x40; code[used++] = 1;
            code[used++] = 0x03; code[used++] = 0; // catch_all_ref
            for (int type = FirstClass + objects[0] - 1; type >= FirstClass; type--)
            {
                int target = finalizers.Callback(type);
                if (target < 0) continue;
                if (code.Length - used < 112) return -3;
                used = Local(code, used, 0x20, receiver);
                used = RefType(code, used, 0x14, type); // most derived receiver first
                code[used++] = 0x04; code[used++] = 0x40;
                used = Local(code, used, 0x20, receiver);
                used = RefType(code, used, 0x16, type);
                used = ReachabilityWasm.Call(code, used, imports + target);
                used = RootStackWasm.Leave(code, used, roots, frame);
                used = Const(code, used, 1);
                code[used++] = 0x0f;
                code[used++] = 0x0b;
            }
            // Only metadata-registered types have callbacks. An arbitrary probe
            // registration may still be dequeued through the diagnostic pop export.
            used = RootStackWasm.Leave(code, used, roots, frame);
            used = Const(code, used, -1);
            code[used++] = 0x0f;
            code[used++] = 0x0b; // try_table
            code[used++] = 0x00;
            code[used++] = 0x0b; // exnref on the stack
            used = RootStackWasm.Leave(code, used, roots, frame);
            code[used++] = 0x0a; // throw_ref
            code[used++] = 0x0b;
            return used;
        }

        private static int RefType(byte[] code, int used, byte instruction, int type)
        {
            code[used++] = 0xfb; code[used++] = instruction;
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
        }
    }
}
