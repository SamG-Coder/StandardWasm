using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ManagedReferenceSignatures
    {
        internal static int Read(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout, int[] heaps,
            int owner, int[] next, int end, int context, int[] objects, int depth)
        {
            int at = next[0];
            if (at >= end) return -1;
            if (depth >= ObjectLayouts.MaxClasses) return -2;
            int element = image[at];
            next[0] = at + 1;
            if (element == 0x0e) return String;
            if (element == 0x1c) return ObjectLayouts.ObjectReference;
            if (element == 0x12 || element == 0x13)
            {
                int token = CompressedUnsigned(image, at + 1, end, next);
                if (token < 0) return -1;
                return element == 0x13 ? ObjectLayouts.Argument(objects, context, token) :
                    BundleLayouts.RegisterReference(main, bundle, ranges, image, layout, heaps, owner, token, objects);
            }
            if (element == 0x1d)
            {
                int item = Read(main, bundle, ranges, image, layout, heaps, owner, next, end, context, objects, depth + 1);
                return item < 0 ? item : ObjectLayouts.RegisterArray(objects, item);
            }
            if (element != 0x15 || at + 1 >= end || image[at + 1] != 0x12) return -2;
            int definition = CompressedUnsigned(image, at + 2, end, next);
            if (definition <= 0) return -1;
            int count = CompressedUnsigned(image, next[0], end, next);
            if (count <= 0 || count > end - next[0]) return -1;
            var arguments = new int[count];
            for (int i = 0; i < count; i++)
            {
                int argument = Read(main, bundle, ranges, image, layout, heaps, owner, next, end, context, objects, depth + 1);
                if (argument < 0) return argument;
                arguments[i] = argument;
            }
            return BundleLayouts.RegisterInstantiation(main, bundle, ranges, image, layout, heaps, owner, definition, objects, arguments);
        }
    }
}
