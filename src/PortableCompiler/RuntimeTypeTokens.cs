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
            if ((coded & 3) == 2)
            {
                int type = ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps, owner,
                    coded >> 2, context, objects, methodArguments);
                if (type < 0) return type;
                return BindLayout(objects, type, globals);
            }
            if ((coded & 3) == 0 && !RuntimeTypeMetadata.ValidDefinition(image, heaps, coded >> 2)) return -1;
            var identity = new int[2];
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity);
            if (status < 0) return status;
            // Module slot zero is the canonical platform System.Object.
            return StaticFields.Register(globals, -3 - identity[0], identity[1], ObjectLayouts.ObjectReference);
        }

        internal static int BindLayout(int[] objects, int type, int[] globals)
        {
            if (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0] ||
                ObjectLayouts.ArrayElement(objects, type) != 0) return -2;
            int owner = ObjectLayouts.AssemblyOf(objects, type);
            int definition = ObjectLayouts.TypeDef(objects, type);
            if (owner < 0 || definition <= 0) return -2;
            int key = ObjectLayouts.HasArguments(objects, type) ? ClosedTypeKey + type : definition;
            return StaticFields.Register(globals, -3 - owner, key, ObjectLayouts.ObjectReference);
        }
    }
}
