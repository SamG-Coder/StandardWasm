using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ArrayAllocations
    {
        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] records, int slot)
        {
            uint token = unchecked((uint)records[slot + 1]);
            int primitive = ArrayTypeRefs.TokenElement(image, heaps, layout, token);
            int type = primitive;
            if (primitive == -2)
            {
                uint table = token >> 24;
                if (table != 1 && table != 2 && table != 0x1b) return -2;
                int coded = (int)((token & 0xffffff) << 2) | (table == 1 ? 1 : table == 2 ? 0 : 2);
                int element = TokenIdentity(bundle, image, layout, heaps, owner, context, methodArguments, objects, coded);
                if (element < 0) return element;
                if (element == Vector128Word) return -2;
                // Use the same representation as a closed T[] signature.
                if (GenericScalar.IsScalar(element)) primitive = GenericScalar.Element(element);
                else type = ObjectLayouts.RegisterArray(objects, element);
            }
            if (primitive > 0)
            {
                type = ArrayTypeRefs.Storage(primitive);
                records[slot + Width + 1] = primitive;
            }
            if (type <= 0) return type == 0 ? -2 : type;
            records[slot + 1] = type;
            return type;
        }

        internal static int TokenIdentity(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int coded)
        {
            if ((coded & 3) == 2)
                return ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps, owner,
                    coded >> 2, context, objects, methodArguments);
            if (PlatformTypeReferences.IsString(image, heaps, layout, coded)) return String;
            int type = BundleLayouts.RegisterReference(bundle, image, layout, heaps, owner, coded, objects);
            return type == -2 ? BundleLayouts.RegisterType(bundle, image, layout, heaps, owner, coded, objects, true, true) : type;
        }
    }
}
