using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // An invocation owns the unpacked images. Readers share the same bounded
    // byte array for each owner, including during a host-import retry.
    internal sealed class BundleImages
    {
        private readonly BundleImage[] images;
        internal readonly int Count;
        // Generic arguments can re-enter reservation through ordinary type
        // binding. The shared depth bounds that path and direct inheritance.
        internal int LayoutDepth;
        // Zero retains the standalone compilation policy. A one-image
        // packed bundle still uses the bundle linking policy.
        internal readonly int Length;

        private BundleImages(BundleImage[] images, int count, int length)
        {
            this.images = images;
            Count = count;
            Length = length;
        }

        // A zero image length reserves the optional versioned prefix without
        // changing the original sequence of [length, image] records.
        private const uint ModuleOrigins = 0x314f4d53; // SMO1

        internal byte[] Get(int owner) => images[owner].Bytes;

        // A supplied basename is UTF-16 little endian. A null name denotes a
        // module loaded from bytes, with no physical file path.
        internal byte[] FileName(int owner) => images[owner].FileName;

        internal static BundleImages Single(byte[] image)
        {
            var images = new BundleImage[1];
            images[0] = new BundleImage(image, null);
            return new BundleImages(images, 1, 0);
        }

        internal static BundleImages Unpack(byte[] packed)
        {
            bool origins = packed.Length >= 8 && U32(packed, 0) == 0 && U32(packed, 4) == ModuleOrigins;
            int start = origins ? 8 : 0;
            int header = origins ? 8 : 4;
            // Validate every range and basename before copying any payload.
            int count = CountImages(packed, start, header, origins);
            if (count == 0) return null;
            var images = new BundleImage[count];
            int cursor = start;
            for (int owner = 0; owner < count; owner++)
            {
                int size = (int)U32(packed, (uint)cursor);
                int nameSize = origins ? (int)U32(packed, (uint)cursor + 4) : 0;
                cursor += header;
                byte[] image = Copy(packed, cursor, size);
                cursor += size;
                byte[] name = nameSize == 0 ? null : Copy(packed, cursor, nameSize);
                images[owner] = new BundleImage(image, name);
                cursor += nameSize;
            }
            return new BundleImages(images, count, packed.Length);
        }

        private static byte[] Copy(byte[] source, int start, int size)
        {
            var copy = new byte[size];
            for (int i = 0; i < size; i++) copy[i] = source[start + i];
            return copy;
        }

        private static int CountImages(byte[] packed, int cursor, int header, bool origins)
        {
            int count = 0;
            uint limit = (uint)packed.Length;
            while (cursor < packed.Length)
            {
                if (!Fits((uint)cursor, (uint)header, limit)) return 0;
                uint size = U32(packed, (uint)cursor);
                uint nameSize = origins ? U32(packed, (uint)cursor + 4) : 0;
                uint start = (uint)(cursor + header);
                if (size == 0 || !Fits(start, size, limit) || !Fits(start + size, nameSize, limit))
                    return 0;
                if (nameSize != 0 && !ValidFileName(packed, start + size, nameSize)) return 0;
                cursor = (int)(start + size + nameSize);
                count++;
            }
            return count;
        }

        private static bool ValidFileName(byte[] packed, uint start, uint size)
        {
            if ((size & 1) != 0) return false;
            if (size == 2 && U16(packed, start) == '.' ||
                size == 4 && U32(packed, start) == 0x002e002e) return false;
            for (uint offset = 0; offset < size; offset += 2)
            {
                uint unit = U16(packed, start + offset);
                if (unit == 0 || unit == '/' || unit == '\\' || unit == ':') return false;
            }
            return true;
        }
    }

    // A nominal wrapper keeps each byte array distinct in the image vector.
    internal sealed class BundleImage
    {
        internal readonly byte[] Bytes;
        internal readonly byte[] FileName;

        internal BundleImage(byte[] bytes, byte[] fileName)
        {
            Bytes = bytes;
            FileName = fileName;
        }
    }
}
