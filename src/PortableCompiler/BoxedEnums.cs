using PeLibrary;

namespace PortableCompiler
{
    internal static class BoxedEnums
    {
        private const int ElementBits = 5;
        internal const int ElementMask = (1 << ElementBits) - 1;

        internal static bool IsTag(int tag) => tag > ElementMask;

        internal static int Descriptor(int tag) => (tag >> ElementBits) - 1;

        internal static int Implements(BundleImages bundle, int[] objects, int type)
        {
            if (!ObjectLayouts.IsInterface(objects, type) || ObjectLayouts.HasArguments(objects, type)) return 0;
            var required = new[] { ObjectLayouts.AssemblyOf(objects, type), ObjectLayouts.TypeDef(objects, type) };
            return BundleConstraints.ImplementsCoreEnum(bundle, objects, required);
        }

        internal static int Bind(BundleImages bundle, int[] objects, int type, int[] globals)
        {
            var identity = new int[2];
            ObjectLayouts.EnumIdentity(objects, type, identity);
            byte[] image = bundle.Get(identity[0]);
            var layout = new int[4];
            var heaps = new int[8];
            var shape = new int[5];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int status = ObjectLayouts.ReadShape(image, heaps, layout, identity[1], shape, true);
            if (status <= 0) return status < 0 ? status : -2;
            int element = EnumStorage.ReadElement(image, heaps, layout, shape[0], shape[1], shape[1] + shape[2]);
            if (element < 0) return element;
            int descriptor = RuntimeTypeTokens.BindIdentity(globals, identity[0], identity[1]);
            if (descriptor < 0) return descriptor;
            if (descriptor >= (int.MaxValue >> ElementBits)) return -3;
            // Store the signed CLR element as well as the original type. The
            // scalar stack storage by itself cannot distinguish Int32 from UInt32.
            return ((descriptor + 1) << ElementBits) | element;
        }
    }
}
