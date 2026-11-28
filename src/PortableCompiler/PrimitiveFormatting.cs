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

        // The managed location a receiver of `element` must have on the evaluation stack.
        // Int32Location and Int64Location carry no signedness; the declaring type supplies it.
        internal static int ReceiverLocation(int element) => element <= 9 ? Int32Location : Int64Location;
    }
}
