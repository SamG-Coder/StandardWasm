using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class StaticByteSpans
    {
        internal static int Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int context, int[] methodArguments, int[] records, int[] objects, int[] clauses,
            byte[] bytes, int[] size, int[] location)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != StaticFieldAddress || records[slot + 3] != 0) continue;
                int length = records[slot + 2] * Width;
                if (length >= records.Length || records[length] != Constant) continue;
                int call = records[length + 2] * Width;
                if (call >= records.Length || records[call] != MemberCall || records[call + 3] != 0x73 ||
                    !SpanMetadata.IsBytePointerConstructor(bundle, image, layout, heaps, owner, records[call + 1],
                        context, methodArguments, objects)) continue;
                if (ValueOperands.Entry(records, length / Width, clauses) || ValueOperands.Entry(records, call / Width, clauses))
                    continue;

                // The original static address never escapes. The verified
                // constructor consumes a bounded range of immutable PE bytes.
                if (location.Length != 0) location[2] = slot / Width;
                int count = records[length + 1], offset = size[0];
                if (count < 0) return -2;
                int source = ArrayInitializerMetadata.FindStaticData(image, heaps, layout, records[slot + 1], count);
                if (source < 0) return source;
                if (count > bytes.Length - offset) return -3;
                for (int i = 0; i < count; i++) bytes[offset + i] = image[source + i];
                size[0] = offset + count;
                records[slot] = LoadStaticByteSpan;
                records[slot + 1] = offset;
                records[slot + 2] = records[call + 2];
                records[slot + 3] = count;
                records[length] = 0;
                records[call] = 0;
            }
            return 0;
        }
    }
}
