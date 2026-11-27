using static PortableCompiler.BranchWasm;
using static PortableCompiler.WasmBinary;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    // Stage 221 (decision 0092): castclass and isinst to EventHandler or a general delegate key.
    // Every such delegate is a nonempty EventHandler-heap invocation list whose entries share one
    // nominal delegate type (Combine rejects a mismatch), so entry 0's type field decides the cast.
    //
    //   null                         -> null (castclass and isinst both pass it)
    //   not an invocation list       -> InvalidCastException / null (another reference type, or a
    //                                   Func<int> or SpanAction closure on heap 3)
    //   list of another nominal type -> InvalidCastException / null
    //   list of this nominal type    -> the same reference, typed as the list
    internal static class DelegateCastWasm
    {
        internal static int Emit(byte[] code, int used, int source, int target, int nominal, bool cast)
        {
            used = Local(code, used, 0x20, source);
            code[used++] = 0xd1; // ref.is_null
            used = IfList(code, used);
            code[used++] = 0xd0; // ref.null EventHandler
            code[used++] = EventHandlerHeap;
            code[used++] = 0x05; // else
            used = Local(code, used, 0x20, source);
            code[used++] = 0xfb; code[used++] = 0x14; code[used++] = EventHandlerHeap; // ref.test (ref EventHandler)
            used = IfList(code, used);
            used = Local(code, used, 0x20, source);
            code[used++] = 0xfb; code[used++] = 0x16; code[used++] = EventHandlerHeap; // ref.cast (ref EventHandler)
            used = Const(code, used, 0);
            code[used++] = 0xfb; code[used++] = 0x0b; code[used++] = EventHandlerHeap; // array.get entry 0
            code[used++] = 0xfb; code[used++] = 0x02; code[used++] = EventEntryHeap; // struct.get
            code[used++] = EventWasm.EntryDelegateType;
            used = Const(code, used, nominal);
            code[used++] = 0x46; // i32.eq
            used = IfList(code, used);
            used = Local(code, used, 0x20, source);
            code[used++] = 0xfb; code[used++] = 0x17; code[used++] = EventHandlerHeap; // ref.cast (ref null EventHandler)
            code[used++] = 0x05;
            used = Fail(code, used, cast);
            code[used++] = 0x0b;
            code[used++] = 0x05;
            used = Fail(code, used, cast);
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return Local(code, used, 0x21, target);
        }

        private static int Fail(byte[] code, int used, bool cast)
        {
            if (cast) return ExceptionTagsWasm.Throw(code, used, ExceptionTagsWasm.InvalidCastTag);
            code[used++] = 0xd0;
            code[used++] = EventHandlerHeap;
            return used;
        }

        private static int IfList(byte[] code, int used)
        {
            code[used++] = 0x04;
            return ValueType(code, used, EventHandlerReference);
        }

        // The nominal delegate type an entry records: 0 for EventHandler, ordinal + 1 for a key.
        internal static int Nominal(int type) =>
            PeLibrary.DelegateShapes.IsKey(type) ? type - PeLibrary.DelegateShapes.KeyBase + 1 : 0;
    }
}
