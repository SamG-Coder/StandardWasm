using PeLibrary;

namespace PortableCompiler
{
    internal static class PrimitiveReceivers
    {
        internal static bool IsNominalContext(int[] objects, int context, int owner, int declaring) =>
            context >= ObjectLayouts.FirstClass && context - ObjectLayouts.FirstClass < objects[0] &&
            ObjectLayouts.AssemblyOf(objects, context) == owner && ObjectLayouts.TypeDef(objects, context) == declaring;

        // The CLI this of a primitive value is a managed address. CoreLib's
        // signed Int32 body can use the existing cell without creating a
        // nominal struct or changing the boxed virtual dispatch ABI.
        internal static int DirectInt32(byte[] image, int[] layout, int[] heaps, int declaring, int method)
        {
            int flags = MethodDefinitions.ReadColumn(image, layout, method, 2);
            return (flags & 0x2410) == 0 && MethodDefinitions.ReadColumn(image, layout, method, 0) != 0 &&
                ArrayTypeRefs.PrimitiveElement(image, heaps, layout, declaring << 2) == SignatureReader.Int32
                ? SignatureTypes.Int32Location : 0;
        }
    }
}
