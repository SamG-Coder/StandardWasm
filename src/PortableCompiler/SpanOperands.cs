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
            int fieldCaller = 0)
        {
            bool field = fieldCaller != 0;
            int call = records[address + 2] * Width;
            if (call >= records.Length || ValueOperands.Entry(records, call / Width, clauses)) return 0;
            if ((spanType == SignatureTypes.CharacterSpan || spanType == SignatureTypes.WritableCharacterSpan) &&
                ToStringCall(bundle, image, layout, heaps, owner, context, methodArguments, records, call, spanType, objects))
            {
                // The exact CoreLib character override copies the view into an
                // immutable string. The descriptor itself is only read.
                records[address] = field ? GetField : Load;
                records[call] = StringFromSpan;
                records[call + 3] = spanType;
                return 1;
            }
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
                call = AfterArgumentRead(image, layout, heaps, records, call, clauses, fieldCaller);
                if (call < 0) return 0;
                // Argument reads cannot replace the original span before the call.
                // The descriptor snapshot still shares its backing storage.
                bool explicitLength = records[call] == Load || records[call] == Constant;
                int slice = explicitLength ? AfterArgumentRead(image, layout, heaps, records, call, clauses, fieldCaller) : call;
                int arity = explicitLength ? 2 : 1;
                if (slice >= 0 && IsDirectMemberCall(records, slice) &&
                    SpanMetadata.IsSlice(bundle, image, layout, heaps, owner, records[slice + 1],
                        context, methodArguments, spanType, objects, arity))
                {
                    records[address] = field ? GetField : Load;
                    records[slice] = SpanSlice;
                    records[slice + 1] = arity;
                    records[slice + 3] = spanType;
                    return 1;
                }
            }
            if (!IsDirectMemberCall(records, call)) return 0;
            if (!field && indexed && lengthCall < 0 &&
                (spanType == SignatureTypes.WritableCharacterSpan || spanType == SignatureTypes.ReadOnlyByteSpan))
            {
                int span = SpanMetadata.ClassifySpanCall(bundle, image, layout, heaps, owner,
                    records[call + 1], context, methodArguments, objects, constructor: true);
                int expected = spanType == SignatureTypes.ReadOnlyByteSpan ? ByteArrayAsSpan : CharacterArrayAsSpan;
                if (span == expected)
                {
                    records[address] = Nop;
                    records[call] = span;
                    records[call + 3] = records[address + 1] + 1;
                    return 1;
                }
            }

            int accessor = SpanMetadata.Accessor(bundle, image, layout, heaps, owner,
                records[call + 1], context, methodArguments, spanType, objects);
            int element = spanType == SignatureTypes.WritableCharacterSpan ? SpanAddress : SpanRead;
            if (indexed ? accessor != element : accessor != SpanLength && accessor != SpanIsEmpty) return 0;

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

        private static bool ToStringCall(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int context, int[] methodArguments, int[] records, int call, int spanType, int[] objects)
        {
            if (IsDirectMemberCall(records, call))
                return SpanMetadata.IsToString(bundle, image, layout, heaps, owner, records[call + 1],
                    context, methodArguments, spanType, objects);
            if (records[call] != ConstrainedInstance) return false;
            int type = records[call + 1];
            if (type >> 24 != 0x1b || ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                owner, type & 0xffffff, context, objects, methodArguments) != spanType) return false;

            var declaration = new int[2];
            if (ConstrainedValueCalls.ResolveObjectDeclaration(bundle, image, layout, heaps, owner, records[call + 3],
                context, methodArguments, objects, declaration) < 0) return false;
            byte[] core = bundle.Get(declaration[0]);
            var methods = new int[4];
            var streams = new int[8];
            return CompressedTables.FindWithHeaps(core, methods, streams) >= 0 &&
                RuntimeTypeMetadata.IsCoreObjectToString(core, streams, methods, declaration[1]);
        }

        private static int Next(int[] records, int slot, int[] clauses)
        {
            int next = records[slot + 2];
            return next < records.Length / Width && !ValueOperands.Entry(records, next, clauses) ? next * Width : -1;
        }

        private static int AfterArgumentRead(byte[] image, int[] layout, int[] heaps, int[] records, int read, int[] clauses, int caller)
        {
            int next = Next(records, read, clauses);
            // A receiver load followed by a field read supplies one argument,
            // for example this._pos in Span<T>.Slice(this._pos).
            if (next >= 0 && records[read] == Load && (records[next] == GetField ||
                caller != 0 && records[next] == Call && records[next + 3] == 0 &&
                ScopedFieldReaders.IsInt32(image, layout, heaps, caller, records[next + 1])))
                next = Next(records, next, clauses);
            return next;
        }

        private static bool IsDirectMemberCall(int[] records, int slot) =>
            records[slot] == MemberCall && records[slot + 3] == 0x28;
    }
}
