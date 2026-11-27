using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class RuntimeTypeGenericKind
    {
        internal static bool IsDescriptorReceiver(int type) => type == RuntimeTypeMetadata.Type || type == NullValue;

        internal static int Definition(BundleImages bundle, int[] objects, int owner, int row, int[] layout, int[] heaps)
        {
            if (row >= RuntimeTypeTokens.PrimitiveVectorKey)
                return owner == AssemblyBundle.ObjectOwner && ArrayTypeRefs.Storage(row - RuntimeTypeTokens.PrimitiveVectorKey) != 0 ? 0 : -2;
            if (row >= RuntimeTypeTokens.ClosedTypeKey)
            {
                int type = row - RuntimeTypeTokens.ClosedTypeKey;
                if (type < FirstClass || type - FirstClass >= objects[0]) return -2;
                // An array remains nongeneric even if the element is generic.
                if (ArrayElementKey(objects, type) != 0) return 0;
                if (AssemblyOf(objects, type) != owner || !HasArguments(objects, type)) return -2;
                return TypeDef(objects, type);
            }
            if (owner == AssemblyBundle.ObjectOwner) return row == 0 ? 0 : -2;
            if (owner < 0 || owner >= bundle.Count) return -2;
            byte[] image = bundle.Get(owner);
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 || row < 2 || row > counts[2]) return -1;
            // Generic definitions, including nested types that inherit outer
            // parameters, are identified by GenericParam ownership.
            int[] parameters = GenericParameters.TypeParameterNames(image, heaps, layout, counts, row);
            return parameters == null ? -1 : parameters.Length == 0 ? 0 : row;
        }
    }
}
