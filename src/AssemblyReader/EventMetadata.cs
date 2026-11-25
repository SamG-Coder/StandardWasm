using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class EventMetadata
    {
        internal const int Empty = 1, Construct = 2, ConstructHandler = 3, InvokeHandler = 4, CombineHandler = 5, RemoveHandler = 6;
        private const int DelegateType = 256, InterlockedType = 257; // Signature matching only.
        private const int ExchangeMember = 7;

        internal static int Reference(byte[] image, int[] heaps, int[] methods, int coded)
        {
            if ((coded & 3) != 1) return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            int type = first < 0 ? 0 : ReadType(image, heaps, methods, counts, first, coded >> 2);
            return type >= DelegateType ? 0 : type;
        }

        internal static bool IsHandlerExchange(byte[] image, int[] heaps, int[] methods, int row)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || row < 1 || row > counts[43]) return false;
            int specs = MetadataTableRows.TableStart(heaps, methods, counts, 43);
            if (specs < 0) return false;
            int memberWidth = MetadataTableRows.MethodRefWidth(counts), blobs = methods[3];
            uint at = (uint)(specs + (row - 1) * (memberWidth + blobs));
            uint member = Index(image, at, memberWidth);
            if ((member & 1) != 1 || Member(image, heaps, methods, (int)(member >> 1)) != ExchangeMember)
                return false;
            uint blob = Index(image, at + (uint)memberWidth, blobs);
            if (blob == 0 || blob >= (uint)heaps[1]) return false;
            int end = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + (int)blob, end, next);
            int signature = next[0];
            if (size < 4 || size > end - signature || image[signature] != 0x0a ||
                image[signature + 1] != 1 || image[signature + 2] != 0x12)
                return false;
            int type = CompressedUnsigned(image, signature + 3, signature + size, next);
            return next[0] == signature + size && (type & 3) == 1 &&
                ReadType(image, heaps, methods, counts, first, type >> 2) == ObjectLayouts.EventHandlerReference;
        }

        internal static int Member(byte[] image, int[] heaps, int[] methods, int row)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || row < 1 || row > counts[10]) return 0;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 10);
            if (table < 0) return 0;
            int strings = methods[2], blobs = methods[3];
            int width = StandaloneSignatures.Width(counts, 10, strings, blobs);
            int parentWidth = width - strings - blobs;
            uint at = (uint)(table + (row - 1) * width);
            uint parent = Index(image, at, parentWidth);
            if ((parent & 7) != 1) return 0;
            int type = ReadType(image, heaps, methods, counts, first, (int)(parent >> 3));
            if (type == 0) return 0;

            uint name = Index(image, at + (uint)parentWidth, strings);
            uint blob = Index(image, at + (uint)(parentWidth + strings), blobs);
            if (blob == 0 || blob >= (uint)heaps[1]) return 0;
            int end = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + (int)blob, end, next);
            int signature = next[0];
            if (size < 3 || size > end - signature) return 0;
            if (type == InterlockedType)
            {
                // static generic T CompareExchange<T>(ref T, T, T)
                return size == 12 && U32(image, (uint)signature) == 0x1e030110 &&
                    U32(image, (uint)signature + 4) == 0x001e1000 && U32(image, (uint)signature + 8) == 0x001e001e &&
                    Name(image, heaps, name, 15, 0x706d6f43, 0x45657261, 0x61686378, 0x65676e)
                    ? ExchangeMember : 0;
            }
            if (type == DelegateType)
            {
                if (image[signature] != 0 || image[signature + 1] != 2) return 0;
                int member;
                if (Name(image, heaps, name, 7, 0x626d6f43, 0x656e69, 0)) // Combine
                    member = CombineHandler;
                else if (Name(image, heaps, name, 6, 0x6f6d6552, 0x6576, 0)) // Remove
                    member = RemoveHandler;
                else return 0;
                int cursor = signature + 2;
                for (int i = 0; i < 3; i++)
                {
                    if (cursor >= signature + size || image[cursor] != 0x12) return 0;
                    int reference = CompressedUnsigned(image, cursor + 1, signature + size, next);
                    if ((reference & 3) != 1 || ReadType(image, heaps, methods, counts, first, reference >> 2) != DelegateType)
                        return 0;
                    cursor = next[0];
                }
                return cursor == signature + size ? member : 0;
            }
            if (type == ObjectLayouts.EventHandlerReference)
            {
                // HASTHIS, two arguments, void result, object first argument.
                if (size < 5 || U32(image, (uint)signature) != 0x1c010220) return 0;
                if (size == 5 && image[signature + 4] == 0x18 &&
                    Name(image, heaps, name, 5, 0x6f74632e, 0x72, 0)) // .ctor(object, native int)
                    return ConstructHandler;
                if (image[signature + 4] != 0x12 ||
                    !Name(image, heaps, name, 6, 0x6f766e49, 0x656b, 0)) return 0; // Invoke
                int argument = CompressedUnsigned(image, signature + 5, signature + size, next);
                return next[0] == signature + size && IsArgs(image, heaps, methods, counts, first, argument) ? InvokeHandler : 0;
            }
            if (size == 3 && image[signature] == 0x20 && image[signature + 1] == 0 && image[signature + 2] == 1 &&
                Name(image, heaps, name, 5, 0x6f74632e, 0x72, 0)) // .ctor
                return Construct;
            if (image[signature] != 6 || image[signature + 1] != 0x12 ||
                !Name(image, heaps, name, 5, 0x74706d45, 0x79, 0)) // Empty
                return 0;
            int coded = CompressedUnsigned(image, signature + 2, signature + size, next);
            return next[0] == signature + size && IsArgs(image, heaps, methods, counts, first, coded) ? Empty : 0;
        }

        private static bool IsArgs(byte[] image, int[] heaps, int[] methods, int[] counts, int first, int coded) =>
            (coded & 3) == 1 && ReadType(image, heaps, methods, counts, first, coded >> 2) == ObjectLayouts.EventArgsReference;

        private static int ReadType(byte[] image, int[] heaps, int[] methods, int[] counts, int first, int row)
        {
            if (row < 1 || row > counts[1]) return 0;
            int strings = methods[2], blobs = methods[3];
            int scopeWidth = counts[0] < 16384 && counts[1] < 16384 && counts[26] < 16384 && counts[35] < 16384 ? 2 : 4;
            int guid = (image[heaps[4] + 6] & 2) == 0 ? 2 : 4;
            int typeRefs = first + counts[0] * (2 + strings + 3 * guid);
            uint at = (uint)(typeRefs + (row - 1) * (scopeWidth + 2 * strings));
            uint scope = Index(image, at, scopeWidth);
            if ((scope & 3) != 2 || scope >> 2 == 0 || scope >> 2 > (uint)counts[35]) return 0;
            uint name = Index(image, at + (uint)scopeWidth, strings);
            int type;
            if (Name(image, heaps, name, 9, 0x6e657645, 0x67724174, 0x73)) // EventArgs
                type = ObjectLayouts.EventArgsReference;
            else if (Name(image, heaps, name, 12, 0x6e657645, 0x6e614874, 0x72656c64)) // EventHandler
                type = ObjectLayouts.EventHandlerReference;
            else if (Name(image, heaps, name, 8, 0x656c6544, 0x65746167, 0)) // Delegate
                type = DelegateType;
            else if (Name(image, heaps, name, 11, 0x65746e49, 0x636f6c72, 0x64656b)) // Interlocked
                type = InterlockedType;
            else return 0;
            uint ns = Index(image, at + (uint)(scopeWidth + strings), strings);
            if (type == InterlockedType ? !Name(image, heaps, ns, 16, 0x74737953, 0x542e6d65, 0x61657268, 0x676e6964) :
                !Name(image, heaps, ns, 6, 0x74737953, 0x6d65, 0))
                return 0;

            // Read the AssemblyRef public key token and culture as well as its name.
            // Matching a bare TypeRef name would bind user-defined types to the BCL.
            int assemblies = MetadataTableRows.TableStart(heaps, methods, counts, 35);
            if (assemblies < 0) return 0;
            uint assembly = (uint)(assemblies + ((int)(scope >> 2) - 1) * (12 + 2 * strings + 2 * blobs));
            if (U32(image, assembly + 8) != 0 || Index(image, assembly + (uint)(12 + blobs + strings), strings) != 0 ||
                !Name(image, heaps, Index(image, assembly + (uint)(12 + blobs), strings), 11, 0x7374656e, 0x646e6174, 0x647261)) // netstandard
                return 0;
            uint key = Index(image, assembly + 12, blobs);
            if (key == 0 || !Fits(key, 9, (uint)heaps[1])) return 0;
            uint token = (uint)heaps[0] + key;
            return image[token] == 8 && U32(image, token + 1) == 0xff137bcc &&
                U32(image, token + 5) == 0x51dd2dcd ? type : 0;
        }

        // CLR metadata names are UTF-8. These ASCII signatures are packed in
        // little-endian words so the scalar bootstrap can compile the matcher.
        private static bool Name(byte[] image, int[] heaps, uint index, int length, uint first, uint second, uint third,
            uint fourth = 0)
        {
            if (index == 0 || !Fits(index, (uint)length + 1, (uint)heaps[3])) return false;
            uint start = (uint)heaps[2] + index;
            for (int i = 0; i < length; i++)
            {
                uint word = i < 4 ? first : i < 8 ? second : i < 12 ? third : fourth;
                if (image[start + (uint)i] != unchecked((byte)(word >> ((i & 3) * 8)))) return false;
            }
            return image[start + (uint)length] == 0;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
