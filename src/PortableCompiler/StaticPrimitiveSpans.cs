using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class StaticPrimitiveSpans
    {
        internal static int Lower(byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses,
            byte[] data, int[] used, int[] location)
        {
            int[] counts = null, spec = null, backing = null;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != FieldToken) continue;
                int call = records[slot + 2] * Width;
                if (call >= records.Length || records[call] != MethodSpecCall || records[call + 3] != 0 ||
                    ValueOperands.Entry(records, call / Width, clauses)) continue;
                if (counts == null)
                {
                    counts = new int[45];
                    int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                    if (first < 0) return first;
                    spec = new int[MethodSpecifications.Width];
                    backing = new int[1];
                }
                int element = StaticSpanMetadata.CreateSpanElement(image, layout, heaps, counts, records[call + 1], spec);
                if (element != 8) continue;

                // The original field handle is consumed by the intrinsic.
                // Keep the descriptor scoped to immutable, naturally aligned RVA data.
                const int elementBytes = 4;
                if (location.Length != 0) location[2] = slot / Width;
                int source = ArrayInitializerMetadata.FindStaticData(image, heaps, layout, records[slot + 1], 0,
                    backing, alignment: elementBytes);
                if (source < 0) return source;
                int count = backing[0] / elementBytes;
                int bytes = count * elementBytes, offset = used[0];
                if (bytes > data.Length - offset) return -3;
                for (int i = 0; i < bytes; i++) data[offset + i] = image[source + i];
                used[0] = offset + bytes;

                records[slot] = LoadStaticPrimitiveSpan;
                records[slot + 1] = offset;
                records[slot + 2] = records[call + 2];
                records[slot + 3] = ReadOnlyInt32Span;
                // These records are inside the field token operand.
                records[slot + Width + 1] = count;
                records[slot + Width + 2] = Ints;
                records[call] = 0;
            }
            return 0;
        }
    }
}
