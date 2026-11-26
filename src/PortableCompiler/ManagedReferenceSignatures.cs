using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ManagedReferenceSignatures
    {
        internal static int ReadStorage(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout, int[] heaps,
            int owner, int[] next, int end, int context, int[] objects) =>
            GenericScalar.Storage(Read(main, bundle, ranges, image, layout, heaps, owner, next, end, context, objects, 0));

        internal static int ReadInstantiation(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int blob, int[] objects)
        {
            int limit = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 1 || size > limit - start) return -1;
            if (image[start] != 0x15) return -2;
            int end = start + size;
            int type = Read(main, bundle, ranges, image, layout, heaps, owner, next, end, 0, objects, 0);
            return type < 0 || next[0] == end ? type : -1;
        }

        internal static int Read(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout, int[] heaps,
            int owner, int[] next, int end, int context, int[] objects, int depth)
        {
            int at = next[0];
            if (at >= end) return -1;
            if (depth >= ObjectLayouts.MaxClasses) return -2;
            int element = image[at];
            next[0] = at + 1;
            int scalar = GenericScalar.Key(element);
            if (scalar != 0) return scalar;
            if (element == 0x11)
            {
                int token = CompressedUnsigned(image, at + 1, end, next);
                if (token <= 0) return -1;
                int primitive = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, token);
                return primitive > 0 ? GenericScalar.Key(primitive) : primitive;
            }
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
                if (item < 0) return item;
                return GenericScalar.IsScalar(item) ? GenericScalar.VectorKey(item) : ObjectLayouts.RegisterArray(objects, item);
            }
            if (element != 0x15 || at + 1 >= end) return -2;
            int definitionKind = image[at + 1];
            if (definitionKind != 0x12 && definitionKind != 0x11) return -2;
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
            return BundleLayouts.RegisterInstantiation(main, bundle, ranges, image, layout, heaps, owner, definition, objects, arguments, definitionKind == 0x11);
        }
    }
}
