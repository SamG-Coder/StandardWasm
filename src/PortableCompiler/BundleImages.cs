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

        internal byte[] Get(int owner) => images[owner].Bytes;

        internal static BundleImages Single(byte[] image)
        {
            var images = new BundleImage[1];
            images[0] = new BundleImage(image);
            return new BundleImages(images, 1, 0);
        }

        internal static BundleImages Unpack(byte[] packed)
        {
            // Validate every range before copying any payload. The repeated
            // pass below uses those same length prefixes without retaining
            // access to the caller's packed buffer.
            int count = CountImages(packed);
            if (count == 0) return null;
            var images = new BundleImage[count];
            int cursor = 0;
            for (int owner = 0; owner < count; owner++)
            {
                int size = ImageLength(packed, cursor);
                if (size == 0) return null;
                cursor += 4;
                var image = new byte[size];
                for (int i = 0; i < image.Length; i++)
                    image[i] = packed[cursor + i];
                images[owner] = new BundleImage(image);
                cursor += image.Length;
            }
            return cursor == packed.Length ? new BundleImages(images, count, packed.Length) : null;
        }

        private static int CountImages(byte[] packed)
        {
            int count = 0, cursor = 0;
            while (cursor < packed.Length)
            {
                int length = ImageLength(packed, cursor);
                if (length == 0) return 0;
                cursor += 4 + length;
                count++;
            }
            return count;
        }

        private static int ImageLength(byte[] packed, int cursor)
        {
            int remaining = packed.Length - cursor;
            if (remaining < 4) return 0;
            uint length = U32(packed, (uint)cursor);
            return length <= (uint)(remaining - 4) ? (int)length : 0;
        }
    }

    // A nominal wrapper keeps each byte array distinct in the image vector.
    internal sealed class BundleImage
    {
        internal readonly byte[] Bytes;

        internal BundleImage(byte[] bytes) { Bytes = bytes; }
    }
}
