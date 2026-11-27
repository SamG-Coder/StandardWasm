using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class SpanOperands
    {
        // Neither accessor mutates the descriptor itself. A load can supply
        // its current value. A constructor initializes the destination slot.
        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] records, int address, int spanType, int[] objects, int[] clauses)
        {
            int call = records[address + 2] * Width;
            if (call >= records.Length || ValueOperands.Entry(records, call / Width, clauses)) return 0;
            bool indexed = records[call] == Load || records[call] == Constant;
            if (indexed)
            {
                call = records[call + 2] * Width;
                if (call >= records.Length || ValueOperands.Entry(records, call / Width, clauses)) return 0;
            }
            if (records[call] != MemberCall || records[call + 3] != 0x28) return 0;
            if (indexed && spanType == SignatureTypes.WritableCharacterSpan &&
                SpanMetadata.ClassifyCharacterSpanCall(bundle, image, layout, heaps, owner,
                    records[call + 1], context, methodArguments, objects) == CharacterArrayAsSpan)
            {
                records[address] = Nop;
                records[call] = CharacterArrayAsSpan;
                records[call + 3] = records[address + 1] + 1;
                return 1;
            }

            int accessor = SpanMetadata.Accessor(bundle, image, layout, heaps, owner,
                records[call + 1], context, methodArguments, spanType, objects);
            int element = spanType == SignatureTypes.WritableCharacterSpan ? SpanAddress : SpanRead;
            if (accessor != (indexed ? element : SpanLength)) return 0;

            if (accessor == SpanRead)
            {
                // ReadOnlySpan<T>.get_Item returns a readonly reference. Fuse
                // its immediate read so it never becomes a writable location.
                int load = records[call + 2] * Width;
                if (load >= records.Length || ValueOperands.Entry(records, load / Width, clauses)) return 0;
                int indirect = spanType == SignatureTypes.CharacterSpan ? 0x49 :
                    spanType == SignatureTypes.ReadOnlyByteSpan ? 0x47 : 0;
                if (indirect != 0 ? records[load] != LoadIndirect || records[load + 1] != indirect :
                    records[load] != LoadObjectIndirect) return 0;
                int vector = spanType == SignatureTypes.CharacterSpan ? SignatureTypes.Chars :
                    spanType == SignatureTypes.ReadOnlyByteSpan ? SignatureTypes.Bytes :
                    ObjectLayouts.RegisterArray(objects, ObjectLayouts.ObjectReference);
                if (vector < 0) return vector;
                records[call + 1] = vector;
                records[call + 2] = records[load + 2];
                records[load] = 0;
            }
            records[address] = Load;
            records[call] = accessor;
            records[call + 3] = spanType;
            return 1;
        }
    }
}
