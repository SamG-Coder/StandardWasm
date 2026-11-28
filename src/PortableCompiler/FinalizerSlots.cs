using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    internal sealed class FinalizerSlots
    {
        internal readonly int[] RegistrationFixups = new int[1];
        internal readonly int[] SuppressionFixups = new int[1];
        // Decision 0104: GC.Collect calls Discover and GC.WaitForPendingFinalizers drains RunNext.
        internal readonly int[] DiscoverFixups = new int[1];
        internal readonly int[] RunNextFixups = new int[1];
        // Decision 0104: without a bundled CoreLib, a finalizer is found by its declaration
        // (the most-derived `protected override void Finalize()`) instead of CoreLib's slot.
        private bool identity;
        private readonly byte[] cached = new byte[MaxClasses];
        private readonly int[] callbacks = new int[MaxClasses];
        private readonly int[] root = new int[2];
        private int suppressRow;

        internal bool Registers(int type) => type >= FirstClass && cached[type - FirstClass] == 2;
        internal int Callback(int type) => callbacks[type - FirstClass] - 1;
        internal void BindCallback(int type, int function) => callbacks[type - FirstClass] = function + 1;
        internal bool IsSuppression(int owner, int row) => owner == root[0] && row == suppressRow && row != 0;

        internal int ReadRoot(BundleImages bundle)
        {
            var range = new int[2];
            bool coreLib = false;
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
                coreLib = true;
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
                        suppressRow = FindSuppression(image, heaps, layout, counts, first);
                        return 0;
                    }
                }
                return -2;
            }
            if (coreLib) return -2;
            // A console module compiled against the built-in framework (decision 0080) has no
            // CoreLib, so System.Object.Finalize has no MethodDef to compare overrides with.
            identity = true;
            root[0] = -1;
            root[1] = -1;
            return 0;
        }

        private static int FindSuppression(byte[] image, int[] heaps, int[] layout, int[] counts, int first)
        {
            for (int row = 1; row <= counts[6]; row++)
            {
                if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "SuppressFinalize")) continue;
                int type = Owner(image, heaps, layout, row, false);
                if (!NamedDefinition(image, heaps, layout, counts, first, type, "System", "GC")) continue;
                int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
                int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
                if ((flags & 0x57) != 0x16 || blob <= 0 || !Fits((uint)blob, 5, (uint)heaps[1])) continue;
                int at = heaps[0] + blob;
                if (image[at] == 4 && image[at + 1] == 0 && image[at + 2] == 1 && image[at + 3] == 1 && image[at + 4] == 0x1c)
                    return row;
            }
            return 0;
        }

        internal int Includes(BundleImages bundle, int[] objects, int type, int[] target)
        {
            if (type < FirstClass || IsValueType(objects, type) || IsInterface(objects, type)) return 0;
            int index = type - FirstClass;
            if (cached[index] != 0) return cached[index] - 1;
            int hasFinalizer;
            if (identity)
            {
                int status = DeclaredFinalizer(bundle, objects, type, target);
                if (status < 0) return status;
                hasFinalizer = status;
            }
            else
            {
                int status = VirtualSlots.SelectVirtualBody(bundle, objects, type, true, ObjectReference, root[0], root[1], target, true);
                if (status < 0) return status;
                hasFinalizer = target[0] == root[0] && target[1] == root[1] ? 0 : 1;
            }
            cached[index] = (byte)(hasFinalizer + 1);
            return hasFinalizer;
        }

        // 1 with target = [assembly, MethodDef] of the most-derived `Finalize()` override in the class
        // chain, or 0 when no class in the chain declares one. An override is virtual, reuses its
        // slot (not newslot), takes no arguments and returns void, as System.Object.Finalize.
        private static int DeclaredFinalizer(BundleImages bundle, int[] objects, int type, int[] target)
        {
            var layout = new int[4];
            var heaps = new int[8];
            var range = new int[2];
            for (int current = type; current >= FirstClass; current = Parent(objects, current))
            {
                int owner = AssemblyOf(objects, current);
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                int status = MethodRange(image, heaps, layout, TypeDef(objects, current), range);
                if (status < 0) return status;
                for (int row = range[0]; row < range[1]; row++)
                {
                    if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "Finalize")) continue;
                    int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
                    if ((flags & 0x150) != 0x40 || !Signature(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4)))
                        continue; // static, newslot or another shape: not an override of Object.Finalize
                    target[0] = owner;
                    target[1] = row;
                    return 1;
                }
            }
            return 0;
        }

        internal static bool Signature(byte[] image, int[] heaps, int blob)
        {
            if (blob <= 0 || !Fits((uint)blob, 4, (uint)heaps[1])) return false;
            int at = heaps[0] + blob;
            return image[at] == 3 && image[at + 1] == 0x20 && image[at + 2] == 0 && image[at + 3] == 1;
        }
    }
}
