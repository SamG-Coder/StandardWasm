using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    internal sealed class FinalizerSlots
    {
        internal readonly int[] RegistrationFixups = new int[1];
        private readonly byte[] cached = new byte[MaxClasses];
        private readonly int[] callbacks = new int[MaxClasses];
        private readonly int[] root = new int[2];

        internal bool Registers(int type) => type >= FirstClass && cached[type - FirstClass] == 2;
        internal int Callback(int type) => callbacks[type - FirstClass] - 1;
        internal void BindCallback(int type, int function) => callbacks[type - FirstClass] = function + 1;

        internal int ReadRoot(BundleImages bundle)
        {
            var range = new int[2];
            for (int owner = 0; owner < bundle.Count; owner++)
            {
                var layout = new int[4];
                var heaps = new int[8];
                var counts = new int[45];
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
                if (first < 0) return -1;
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) continue;
                for (int type = 1; type <= counts[2]; type++)
                {
                    if (!NamedDefinition(image, heaps, layout, counts, first, type, "System", "Object")) continue;
                    if (VirtualSlotMetadata.ReadMethodRange(image, heaps, layout, type, range) < 0) return -1;
                    for (int row = range[0]; row < range[1]; row++)
                    {
                        uint name = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 3));
                        if (!MatchesAscii(image, heaps, name, "Finalize")) continue;
                        int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
                        int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
                        if ((flags & 0x157) != 0x144 || !Signature(image, heaps, blob)) return -2;
                        root[0] = owner;
                        root[1] = row;
                        return 0;
                    }
                }
                return -2;
            }
            return -2;
        }

        internal int Includes(BundleImages bundle, int[] objects, int type, int[] target)
        {
            if (type < FirstClass || IsValueType(objects, type) || IsInterface(objects, type)) return 0;
            int index = type - FirstClass;
            if (cached[index] != 0) return cached[index] - 1;
            int status = VirtualSlots.SelectVirtualBody(bundle, objects, type, true, ObjectReference, root[0], root[1], target, true);
            if (status < 0) return status;
            int hasFinalizer = target[0] == root[0] && target[1] == root[1] ? 0 : 1;
            cached[index] = (byte)(hasFinalizer + 1);
            return hasFinalizer;
        }

        internal static bool Signature(byte[] image, int[] heaps, int blob)
        {
            if (blob <= 0 || !Fits((uint)blob, 4, (uint)heaps[1])) return false;
            int at = heaps[0] + blob;
            return image[at] == 3 && image[at + 1] == 0x20 && image[at + 2] == 0 && image[at + 3] == 1;
        }
    }
}
