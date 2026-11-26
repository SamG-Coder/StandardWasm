using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class EnumStorage
    {
        internal static int Read(byte[] image, int[] heaps, int[] methods, int table, int first, int end)
        {
            int strings = methods[2], blobs = methods[3], width = 2 + strings + blobs;
            int storage = 0;
            for (int row = first; row < end; row++)
            {
                uint field = (uint)(table + (row - 1) * width);
                uint attributes = U16(image, field);
                if ((attributes & 0x10) != 0)
                {
                    if ((attributes & 0x40) == 0) return -2; // Enum statics are literal declarations.
                    continue;
                }
                if (storage != 0 || (attributes & 0x600) != 0x600) return -2;
                uint name = Index(image, field + 2, strings);
                if (!Fits(name, 8, (uint)heaps[3])) return -1;
                uint text = (uint)heaps[2] + name;
                if (U32(image, text) != 0x756c6176 || U32(image, text + 4) != 0x005f5f65) return -2; // value__
                uint signature = Index(image, field + (uint)(2 + strings), blobs);
                if (signature == 0 || !Fits(signature, 3, (uint)heaps[1])) return -1;
                uint blob = (uint)heaps[0] + signature;
                if (image[blob] != 2 || image[blob + 1] != 6) return -2;
                int element = image[blob + 2];
                if (element >= 4 && element <= 7) storage = NarrowStorage.FromElement(element);
                else if (element == 8 || element == 9) storage = 1; // CLI int32
                else if (WideStorage.IsIntegerElement(element)) storage = WideStorage.Scalar;
                else return -2;
            }
            return storage != 0 ? storage : -2;
        }

        private static uint Index(byte[] image, uint at, int size) => size == 2 ? U16(image, at) : U32(image, at);
    }
}
