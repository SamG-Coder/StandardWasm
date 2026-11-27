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

        // constrained. int32 callvirt Object::Equals(object) or Object::ToString().
        // Int32 overrides both, so ECMA-335 III.2.1 calls the override on the
        // receiver address without boxing. Any other Object slot returns -2 and
        // keeps the caller's ordinary value-receiver handling or diagnostic.
        internal static int ResolveConstrainedInt32ObjectSlot(BundleImages bundle, int[] objects,
            int owner, int declaration, int[] target)
        {
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int name = MethodDefinitions.ReadColumn(image, layout, declaration, 3);
            int signature = MethodDefinitions.ReadColumn(image, layout, declaration, 4);
            bool equals = MatchesAscii(image, heaps, (uint)name, "Equals") && signature > 0 &&
                Fits((uint)signature, 5, (uint)heaps[1]) &&
                U32(image, (uint)(heaps[0] + signature)) == 0x02012004 &&
                image[heaps[0] + signature + 4] == 0x1c; // instance bool (object)
            if (!equals && !RuntimeTypeMetadata.IsCoreObjectToString(image, heaps, layout, declaration)) return -2;

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

        internal static int ResolveConstrainedInt32Compare(BundleImages bundle, int[] objects, int declared,
            int owner, int declaration, int[] target)
        {
            int int32 = GenericScalar.Key(SignatureReader.Int32);
            if (ArgumentCount(objects, declared) != 1 || Argument(objects, declared, 0) != int32 ||
                AssemblyOf(objects, declared) != owner) return -2;

            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return -1;
            int declaring = TypeDef(objects, declared);
            if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !NamedDefinition(image, heaps, layout, counts, first, declaring, "System", "IComparable`1") ||
                Owner(image, heaps, layout, declaration, false) != declaring ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, declaration, 3), "CompareTo"))
                return -2;

            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps,
                MethodDefinitions.ReadColumn(image, layout, declaration, 4), 1, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x13) || // VAR 0
                CompressedUnsigned(image, cursor[0], end, cursor) != 0 || cursor[0] != end) return -2;

            // Prove the closed InterfaceImpl with the primitive's original TypeDef.
            // Its nominal layout selects the real slot while the call uses an Int32 cell.
            int implements = BundleConstraints.Implements(bundle, objects, int32, new[] { owner, declaring, int32 }, 0);
            if (implements <= 0) return implements < 0 ? implements : -2;
            int definition = ArrayTypeRefs.CoreDefinition(image, heaps, layout, SignatureReader.Int32);
            if (definition <= 0) return definition < 0 ? definition : -2;
            int nominal = BundleLayouts.RegisterValue(bundle, image, layout, heaps, owner, definition, objects);
            if (nominal < 0) return nominal;
            int selected = InterfaceSlots.SelectBody(bundle, objects, nominal, declared, owner, declaration, target,
                primitiveMap: true);
            if (selected < 0) return selected;
            if (target[0] != owner || Owner(image, heaps, layout, target[1], false) != definition ||
                DirectInt32(image, layout, heaps, definition, target[1]) != Int32Location) return -2;
            target[2] = 0;
            return Int32Location;
        }
    }
}
