using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Decision 0104: `call instance string System.{Int32,UInt32,Int64,UInt64}::ToString()`
    // on a managed receiver address formats the value as an invariant decimal string.
    // C# emits this shape for interpolation and `"text" + number`. The receiver is the
    // value's managed location (ldloca, ldarga, ldflda or ldelema), so the lowering reads
    // through it instead of calling the CoreLib body.
    internal static class PrimitiveFormatting
    {
        // Returns the CLI element type of the receiver (8 = I4, 9 = U4, 10 = I8, 11 = U8)
        // when `member` names the parameterless platform primitive ToString, otherwise 0.
        internal static int Identify(byte[] image, int[] heaps, int[] layout, int member)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !ImageBytes.MatchesAscii(image, heaps, (uint)reference[4], "ToString")) return 0;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, reference[5], 0, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, 0x0e) || cursor[0] != end) return 0;
            // PrimitiveElement also proves that the TypeRef resolves to a platform assembly.
            int element = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, reference[AssemblyReferences.Leaf] << 2 | 1);
            return element >= 8 && element <= 11 ? element : 0;
        }

        // Operand 3 of a FormatPrimitive record whose receiver was folded into a plain value load.
        internal const int ValueReceiver = 1;

        // `ldloca x` / `ldflda F` immediately consumed by the primitive ToString call: the address
        // is only read, so the caller loads the value instead (no managed cell, and init-only
        // fields need no address). `call` is the record offset of the call; `storage` is the
        // stack storage of the local or field. Returns true after rewriting the call record.
        internal static bool FoldValueReceiver(byte[] image, int[] heaps, int[] layout, int[] records, int call,
            int[] clauses, int storage)
        {
            if (call + BranchIl.Width + 1 >= records.Length || records[call] != BranchIl.MemberCall ||
                records[call + 3] != 0x28 || records[call + BranchIl.Width + 1] != 0 ||
                ValueOperands.Entry(records, call / BranchIl.Width, clauses)) return false;
            int element = Identify(image, heaps, layout, records[call + 1]);
            if (element == 0 || storage != (element <= 9 ? I32 : I64)) return false;
            records[call] = BranchIl.FormatPrimitive;
            records[call + 1] = element;
            records[call + 3] = ValueReceiver;
            return true;
        }

        // The stack value a folded receiver of `element` must have.
        internal static int ReceiverValue(int element) => element <= 9 ? I32 : I64;

        // The managed location a receiver of `element` must have on the evaluation stack.
        // Int32Location and Int64Location carry no signedness; the declaring type supplies it.
        internal static int ReceiverLocation(int element) => element <= 9 ? Int32Location : Int64Location;
    }
}
