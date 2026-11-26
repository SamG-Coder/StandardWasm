using PeLibrary;

namespace PortableCompiler
{
    internal static class RuntimeTypeTokens
    {
        // Static field owners are nonnegative. EventArgs.Empty has owner -1.
        // Use a separate key for each defining module and TypeDef row.
        internal static bool IsDescriptorOwner(int owner) => owner <= -2;
        internal static int ModuleSlot(int owner) => -owner - 2;

        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int coded, int[] globals)
        {
            if ((coded & 3) == 0 && !RuntimeTypeMetadata.ValidDefinition(image, heaps, coded >> 2)) return -1;
            var identity = new int[2];
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity);
            if (status < 0) return status;
            // Module slot zero is the canonical platform System.Object.
            return StaticFields.Register(globals, -3 - identity[0], identity[1], ObjectLayouts.ObjectReference);
        }
    }
}
