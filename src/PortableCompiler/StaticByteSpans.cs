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
            var backingSize = new int[1];
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != StaticFieldAddress || records[slot + 3] != 0) continue;
                int length = records[slot + 2] * Width;
                if (length >= records.Length || records[length] != Constant) continue;
                int call = records[length + 2] * Width;
                if (call >= records.Length || records[call] != MemberCall ||
                    !SpanMetadata.IsBytePointerConstructor(bundle, image, layout, heaps, owner, records[call + 1],
                        context, methodArguments, objects)) continue;
                int address = -1;
                if (records[call + 3] == 0x28)
                {
                    for (int before = slot - Width; before >= 0; before -= Width)
                    {
                        if (records[before] == 0) continue;
                        if (records[before] == FrameAddress && records[before + 2] * Width == slot) address = before;
                        break;
                    }
                    if (address < 0 || ValueOperands.Entry(records, slot / Width, clauses)) continue;
                }
                else if (records[call + 3] != 0x73) continue;
                if (ValueOperands.Entry(records, length / Width, clauses) || ValueOperands.Entry(records, call / Width, clauses))
                    continue;

                // The original static address never escapes. The verified
                // constructor consumes a bounded range of immutable PE bytes.
                // A later managed reference can read beyond the view while
                // remaining inside the original field, so retain that field.
                if (location.Length != 0) location[2] = slot / Width;
                int count = records[length + 1], offset = size[0];
                if (count < 0) return -2;
                int source = ArrayInitializerMetadata.FindStaticData(image, heaps, layout, records[slot + 1], count, backingSize);
                if (source < 0) return source;
                int backing = backingSize[0];
                if (backing > bytes.Length - offset) return -3;
                for (int i = 0; i < backing; i++) bytes[offset + i] = image[source + i];
                size[0] = offset + backing;
                records[slot] = LoadStaticByteSpan;
                records[slot + 1] = offset;
                records[slot + 2] = address < 0 ? records[call + 2] : call / Width;
                records[slot + 3] = count;
                records[slot + Width + 1] = backing; // Reserved operand record inside ldsflda.
                records[length] = 0;
                if (address < 0) records[call] = 0;
                else
                {
                    // An instance constructor initializes its original frame slot.
                    records[address] = Nop;
                    records[call] = Store;
                    records[call + 1] = records[address + 1];
                    records[call + 3] = 0;
                }
            }
            return 0;
        }
    }
}
