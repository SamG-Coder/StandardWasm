using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

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

        internal static int ResolveConstrainedInt32Equals(BundleImages bundle, int[] objects,
            int owner, int declaration, int[] target)
        {
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int name = MethodDefinitions.ReadColumn(image, layout, declaration, 3);
            int signature = MethodDefinitions.ReadColumn(image, layout, declaration, 4);
            if (!MatchesAscii(image, heaps, (uint)name, "Equals") || signature <= 0 ||
                !Fits((uint)signature, 5, (uint)heaps[1]) ||
                U32(image, (uint)(heaps[0] + signature)) != 0x02012004 ||
                image[heaps[0] + signature + 4] != 0x1c) return -2; // instance bool (object)

            int definition = ArrayTypeRefs.CoreDefinition(image, heaps, layout, SignatureReader.Int32);
            if (definition <= 0) return definition < 0 ? definition : -2;
            // The nominal layout is used only to resolve the real Object slot.
            // The original override executes on the live scalar location.
            int nominal = BundleLayouts.RegisterValue(bundle, image, layout, heaps, owner, definition, objects);
            if (nominal < 0) return nominal;
            int selected = VirtualSlots.SelectVirtualBody(bundle, objects, nominal, true, ObjectReference,
                owner, declaration, target, exactValue: true);
            if (selected < 0) return selected;
            if (target[0] != owner || Owner(image, heaps, layout, target[1], false) != definition ||
                DirectInt32(image, layout, heaps, definition, target[1]) != Int32Location) return -2;
            target[2] = 0; // Share the existing direct scalar ABI, separate from the boxed nominal ABI.
            return Int32Location;
        }
    }
}
