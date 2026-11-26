using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class ObjectSpanOperands
    {
        // The readonly receiver has no observable address identity. A load can
        // supply its immutable descriptor when followed by the known accessor.
        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] records, int address, int[] objects, int[] clauses)
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
            int accessor = ObjectSpanMetadata.Accessor(bundle, image, layout, heaps, owner,
                records[call + 1], context, methodArguments, objects);
            if (accessor != (indexed ? ObjectSpanRead : ObjectSpanLength)) return 0;

            if (indexed)
            {
                // ReadOnlySpan<T>.get_Item returns a readonly reference. Fuse
                // its immediate read so it never becomes a writable location.
                int load = records[call + 2] * Width;
                if (load >= records.Length || records[load] != LoadObjectIndirect ||
                    ValueOperands.Entry(records, load / Width, clauses)) return 0;
                int vector = ObjectLayouts.RegisterArray(objects, ObjectLayouts.ObjectReference);
                if (vector < 0) return vector;
                records[call + 1] = vector;
                records[call + 2] = records[load + 2];
                records[load] = 0;
            }
            records[address] = Load;
            records[call] = accessor;
            records[call + 3] = 0;
            return 1;
        }
    }
}
