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
                if (call >= records.Length) continue;
                int initializer = records[call + 1];
                // Within CoreLib the same helper is called by MethodDef instead of MemberRef.
                bool definition = records[call] == Call && records[call + 3] == 0;
                if (definition)
                {
                    if (!ArrayInitializerMetadata.IsInitializeArrayDefinition(image, heaps, methods, initializer)) continue;
                }
                else if (records[call] != MemberCall || records[call + 3] != 0x28) continue;

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
                int source = definition ?
                    ArrayInitializerMetadata.FindStaticData(image, heaps, methods, records[token + 1], bytes, null) :
                    ArrayInitializerMetadata.FindData(image, heaps, methods, initializer, records[token + 1], bytes);
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

        // Decision 0105: `ldc rows; ldc cols; newobj T[,]::.ctor(int32, int32); dup; ldtoken; call
        // InitializeArray` builds the MdArrays layout from the module data segment: rows, cols, then
        // the RVA elements. The two counts fold into one InitializedArray of rows * cols + 2 elements.
        internal static int LowerRankTwo(byte[] image, int[] heaps, int[] methods, int[] records, int[] clauses,
            byte[] data, int[] dataSize, int[] location)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != Constant) continue;
                int columns = records[slot + 2] * Width;
                if (columns >= records.Length || records[columns] != Constant) continue;
                int create = records[columns + 2] * Width;
                if (create >= records.Length || records[create] != MemberCall || records[create + 3] != 0x73) continue;
                int vector = MdArrays.MemberVector(image, heaps, methods, records[create + 1]);
                if (vector == 0 || MdArrays.Member(image, heaps, methods, records[create + 1]) != 1) continue;
                int dup = records[create + 2] * Width;
                if (dup >= records.Length || records[dup] != Dup) continue;
                int token = records[dup + 2] * Width;
                if (token >= records.Length || records[token] != FieldToken) continue;
                int call = records[token + 2] * Width;
                if (call >= records.Length || records[call] != MemberCall || records[call + 3] != 0x28) continue;
                if (location.Length != 0) location[2] = token / Width;
                if (HasInteriorTarget(records, clauses, columns / Width, call / Width)) return -2;
                int rows = records[slot + 1], cols = records[columns + 1];
                int width = vector == Longs ? 8 : 4;
                if (rows < 0 || cols < 0 || (long)rows * cols > (int.MaxValue - 2) / width) return -2;
                int bytes = rows * cols * width;
                int source = ArrayInitializerMetadata.FindData(image, heaps, methods, records[call + 1], records[token + 1], bytes);
                if (source < 0) return source;
                int destination = dataSize[0];
                if (bytes + 2 * width > data.Length - destination) return -3;
                Put(data, destination, rows, width);
                Put(data, destination + width, cols, width);
                for (int i = 0; i < bytes; i++) data[destination + 2 * width + i] = image[source + i];
                dataSize[0] = destination + 2 * width + bytes;

                records[slot] = Nop;
                records[columns + 1] = rows * cols + 2;
                records[create] = InitializedArray;
                records[create + 1] = vector;
                records[create + 2] = records[call + 2];
                records[create + 3] = destination;
                records[dup] = 0;
                records[dup + 1] = 0; // no exact element: the vector's own heap type
                records[token] = 0;
                records[call] = 0;
            }
            return 0;
        }

        // A little-endian Int32 or Int64 element.
        private static void Put(byte[] data, int at, int value, int width)
        {
            for (int i = 0; i < width; i++) data[at + i] = i < 4 ? unchecked((byte)(value >> (8 * i))) : (byte)(value < 0 ? 0xff : 0);
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
