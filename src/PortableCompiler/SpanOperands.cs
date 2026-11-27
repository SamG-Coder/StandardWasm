using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class SpanOperands
    {
        // Neither accessor mutates the descriptor itself. A load can supply
        // its current value. A constructor initializes the destination slot.
        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] records, int address, int spanType, int[] objects, int[] clauses,
            bool field = false)
        {
            int call = records[address + 2] * Width;
            if (call >= records.Length || ValueOperands.Entry(records, call / Width, clauses)) return 0;
            int lengthCall = -1;
            bool indexed = records[call] == Load || records[call] == Constant;
            if (records[call] == Dup)
            {
                // Both copies observe the same descriptor. Only a length read and
                // integer arithmetic intervene before the element accessor.
                lengthCall = Next(records, call, clauses);
                if (lengthCall < 0 || !IsDirectMemberCall(records, lengthCall) ||
                    SpanMetadata.Accessor(bundle, image, layout, heaps, owner, records[lengthCall + 1],
                        context, methodArguments, spanType, objects) != SpanLength) return 0;
                int offset = Next(records, lengthCall, clauses);
                if (offset < 0 || records[offset] != Constant) return 0;
                int arithmetic = Next(records, offset, clauses);
                if (arithmetic < 0 || records[arithmetic] != Binary ||
                    (records[arithmetic + 1] != 0x6a && records[arithmetic + 1] != 0x6b)) return 0;
                call = Next(records, arithmetic, clauses);
                if (call < 0) return 0;
                indexed = true;
            }
            else if (indexed)
            {
                call = Next(records, call, clauses);
                if (call < 0) return 0;
            }
            if (!IsDirectMemberCall(records, call)) return 0;
            if (!field && indexed && lengthCall < 0 && spanType == SignatureTypes.WritableCharacterSpan &&
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
                int load = Next(records, call, clauses);
                if (load < 0) return 0;
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
            // A verified accessor can read the field's descriptor directly.
            // Its backing storage remains shared, including writes through get_Item.
            records[address] = field ? GetField : Load;
            records[call] = accessor;
            records[call + 3] = spanType;
            if (lengthCall >= 0)
            {
                records[lengthCall] = SpanLength;
                records[lengthCall + 3] = spanType;
            }
            return 1;
        }

        private static int Next(int[] records, int slot, int[] clauses)
        {
            int next = records[slot + 2];
            return next < records.Length / Width && !ValueOperands.Entry(records, next, clauses) ? next * Width : -1;
        }

        private static bool IsDirectMemberCall(int[] records, int slot) =>
            records[slot] == MemberCall && records[slot + 3] == 0x28;
    }
}
