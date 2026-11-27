using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ArrayInitializers
    {
        internal static int Lower(BundleImages bundle, byte[] image, int[] heaps, int[] methods, int owner,
            int context, int[] arguments, int[] objects, int[] records, int[] clauses, byte[] data,
            int[] dataSize, int[] location)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != Constant) continue;
                int next = records[slot + 2] * Width;
                if (next >= records.Length || records[next] != NewArray) continue;
                int dup = records[next + 2] * Width;
                if (dup >= records.Length || records[dup] != Dup) continue;
                int token = records[dup + 2] * Width;
                if (token >= records.Length || records[token] != FieldToken) continue;
                int call = records[token + 2] * Width;
                if (call >= records.Length || records[call] != MemberCall || records[call + 3] != 0x28)
                    continue;

                if (location.Length != 0) location[2] = token / Width;
                // Only the leading count can be an external branch target.
                if (HasInteriorTarget(records, clauses, next / Width, call / Width))
                    return -2;
                int length = records[slot + 1];
                int type = ArrayAllocations.Bind(bundle, image, methods, heaps, owner, context, arguments, objects, records, next);
                if (type < 0) return type;
                // The enum array keeps its nominal heap. Its underlying scalar
                // determines the byte width of each element in the RVA data.
                int storage = ObjectLayouts.IsEnumArray(objects, type) ? EnumArrayStorage(objects, type) : type;
                int width = storage == Bytes ? 1 : storage == Chars ? 2 : storage == Ints || storage == Singles ? 4 :
                    storage == Longs || storage == Doubles ? 8 : 0;
                // Native-width and reference-backed elements have no matching data encoding.
                if (width == 0) return -2;
                if (length <= 0 || length > int.MaxValue / width) return -2;
                int bytes = length * width;
                int source = ArrayInitializerMetadata.FindData(image, heaps, methods, records[call + 1], records[token + 1], bytes);
                if (source < 0) return source;
                int destination = dataSize[0];
                if (bytes > data.Length - destination) return -3;
                for (int i = 0; i < bytes; i++)
                    data[destination + i] = image[source + i];
                dataSize[0] = destination + bytes;

                // The fused instruction consumes the original count and leaves one fresh array.
                records[next] = InitializedArray;
                records[next + 2] = records[call + 2];
                records[next + 3] = destination;
                records[dup] = 0;
                records[token] = 0;
                records[call] = 0;
            }
            return 0;
        }

        private static bool HasInteriorTarget(int[] records, int[] clauses, int first, int last)
        {
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
            {
                int start = clauses[clause + 1], handler = clauses[clause + 3];
                if (Inside(start, first, last) || Inside(start + clauses[clause + 2], first, last) ||
                    Inside(handler, first, last) || Inside(handler + clauses[clause + 4], first, last))
                    return true;
            }
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot], operand = records[slot + 1];
                if ((kind == Branch || kind == Conditional || kind == Leave) && Inside(operand, first, last))
                    return true;
                if (kind != Switch) continue;
                for (int i = 0; i < records[slot + 3]; i++)
                {
                    int target = SwitchTarget(records, operand, i);
                    if (Inside(target, first, last)) return true;
                }
            }
            return false;
        }

        private static bool Inside(int entry, int first, int last) => entry >= first && entry <= last;
    }
}
