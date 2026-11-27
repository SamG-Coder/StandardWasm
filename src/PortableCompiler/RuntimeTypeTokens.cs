using PeLibrary;

namespace PortableCompiler
{
    internal static class RuntimeTypeTokens
    {
        // Static field owners are nonnegative. EventArgs.Empty has owner -1.
        // Nominal TypeDef rows and closed layout keys occupy disjoint ranges.
        private const int ClosedTypeKey = 0x1000000;
        internal static bool IsDescriptorOwner(int owner) => owner <= -2;
        internal static int ModuleSlot(int owner) => -owner - 2;

        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int coded, int[] globals, int context, int[] objects, int[] methodArguments)
        {
            var identity = new int[2];
            int status = ReadIdentity(bundle, image, layout, heaps, owner, coded, context, objects, methodArguments, identity);
            if (status < 0) return status;
            // Module slot zero is the canonical platform System.Object.
            return BindIdentity(globals, identity[0], identity[1]);
        }

        internal static int ReadIdentity(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int coded, int context, int[] objects, int[] methodArguments, int[] identity)
        {
            if ((coded & 3) == 2)
            {
                int type = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps, owner,
                    coded >> 2, context, objects, methodArguments);
                if (type < 0) return type;
                if (type == SignatureTypes.String) return FindCoreType(bundle, "String", identity);
                if (type == ObjectLayouts.ObjectReference)
                {
                    identity[0] = AssemblyBundle.ObjectOwner;
                    identity[1] = 0;
                    return 0;
                }
                if (GenericScalar.IsScalar(type))
                {
                    // Scalar storage is shared, but each primitive keeps its
                    // own nominal descriptor from the bundled CoreLib.
                    int found = BundleConstraints.FindPrimitive(bundle, type, identity);
                    return found == 0 ? -2 : found < 0 ? found : 0;
                }
                return ReadLayoutIdentity(objects, type, identity);
            }
            if (PlatformTypeReferences.IsString(image, heaps, layout, coded))
                return FindCoreType(bundle, "String", identity);
            if ((coded & 3) == 0 && !RuntimeTypeMetadata.ValidDefinition(image, heaps, coded >> 2)) return -1;
            return AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity);
        }

        internal static int BindLayout(int[] objects, int type, int[] globals)
        {
            var identity = new int[2];
            int status = ReadLayoutIdentity(objects, type, identity);
            return status < 0 ? status : BindIdentity(globals, identity[0], identity[1]);
        }

        internal static int BindIdentity(int[] globals, int owner, int row) =>
            StaticFields.Register(globals, -3 - owner, row, ObjectLayouts.ObjectReference);

        internal static int FindCoreType(BundleImages bundle, string name, int[] identity)
        {
            int found = -1;
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            for (int owner = 0; owner < bundle.Count; owner++)
            {
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                if (first < 0) return -1;
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) continue;
                for (int row = 1; row <= counts[2]; row++)
                {
                    if (!ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, row, "System", name)) continue;
                    if (found >= 0) return -5;
                    found = owner;
                    identity[0] = owner;
                    identity[1] = row;
                }
            }
            return found < 0 ? -6 : 0;
        }

        internal static int ReadLayoutIdentity(int[] objects, int type, int[] identity)
        {
            if (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0] ||
                ObjectLayouts.ArrayElement(objects, type) != 0) return -2;
            int owner = ObjectLayouts.AssemblyOf(objects, type);
            int definition = ObjectLayouts.TypeDef(objects, type);
            if (owner < 0 || definition <= 0) return -2;
            identity[0] = owner;
            identity[1] = ObjectLayouts.HasArguments(objects, type) ? ClosedTypeKey + type : definition;
            return 0;
        }
    }
}
