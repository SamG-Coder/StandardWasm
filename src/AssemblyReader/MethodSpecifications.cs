using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class MethodSpecifications
    {
        internal const int Target = 0, Arguments = 1, End = 2, Arity = 3, Width = 4;

        // Read the MethodDefOrRef and the instantiation envelope. The caller
        // resolves the target and decodes each argument in its generic context.
        internal static int Read(byte[] image, int[] heaps, int[] methods, int[] counts, int row, int[] signature)
        {
            if (row < 1 || row > counts[43] || signature.Length < Width) return -1;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 43);
            if (table < 0) return -1;
            int targetWidth = MetadataTableRows.MethodRefWidth(counts);
            int blobWidth = methods[3];
            uint at = (uint)(table + (row - 1) * (targetWidth + blobWidth));
            uint target = targetWidth == 2 ? U16(image, at) : U32(image, at);
            uint targetRow = target >> 1;
            int limit = counts[(target & 1) == 0 ? 6 : 10];
            if (targetRow == 0 || targetRow > (uint)limit) return -1;

            at += (uint)targetWidth;
            uint blob = blobWidth == 2 ? U16(image, at) : U32(image, at);
            if (blob == 0 || blob >= (uint)heaps[1]) return -1;
            int heapEnd = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + (int)blob, heapEnd, next);
            int start = next[0];
            if (size < 3 || size > heapEnd - start || image[start] != 0x0a) return -1;
            int end = start + size;
            int arity = CompressedUnsigned(image, start + 1, end, next);
            if (arity < 1 || arity > end - next[0]) return -1;

            signature[Target] = (int)target;
            signature[Arguments] = next[0];
            signature[End] = end;
            signature[Arity] = arity;
            return 0;
        }
    }
}
