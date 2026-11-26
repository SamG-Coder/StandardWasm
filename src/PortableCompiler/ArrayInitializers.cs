using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ArrayInitializers
    {
        internal static int Lower(byte[] image, int[] heaps, int[] methods, int[] records, byte[] data,
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
                if (HasInteriorTarget(records, next / Width, call / Width))
                    return -2;
                int length = records[slot + 1];
                int type = ArrayTypeRefs.ElementType(image, heaps, methods, unchecked((uint)records[next + 1]));
                if (type < 0) return type;
                int width = type == Bytes ? 1 : type == Chars ? 2 : type == Longs || type == Doubles ? 8 : 4;
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
                records[next + 1] = type;
                records[next + 2] = records[call + 2];
                records[next + 3] = destination;
                records[dup] = 0;
                records[token] = 0;
                records[call] = 0;
            }
            return 0;
        }

        private static bool HasInteriorTarget(int[] records, int first, int last)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot], operand = records[slot + 1];
                if ((kind == Branch || kind == Conditional || kind == Leave) && operand >= first && operand <= last)
                    return true;
                if (kind != Switch) continue;
                for (int i = 0; i < records[slot + 3]; i++)
                {
                    int target = SwitchTarget(records, operand, i);
                    if (target >= first && target <= last) return true;
                }
            }
            return false;
        }
    }
}
