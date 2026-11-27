using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class Vector128Layouts
    {
        internal static bool ProvesWordStorage(BundleImages bundle)
        {
            if (bundle.Vector128Layout != 0) return bundle.Vector128Layout > 0;
            var core = new int[1];
            if (InitializerFailures.FindCoreLib(bundle, core) < 0 || core[0] < 0)
            {
                bundle.Vector128Layout = -1;
                return false;
            }
            byte[] image = bundle.Get(core[0]);
            bundle.Vector128Layout = ProvesCoreLayout(image) ? 1 : -1;
            return bundle.Vector128Layout > 0;
        }

        // The nominal vector occupies exactly sixteen non-reference bytes.
        // Its two readonly halves each wrap one UInt64 with no padding.
        private static bool ProvesCoreLayout(byte[] image)
        {
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0 ||
                MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return false;
            const string space = "System.Runtime.Intrinsics";
            int vector = ObjectLayouts.FindTypeDefinition(image, heaps, layout, space, "Vector128`1");
            int half = ObjectLayouts.FindTypeDefinition(image, heaps, layout, space, "Vector64`1");
            var shape = new int[5];
            var field = new int[3];
            var declared = new uint[2];
            if (!SizedShape(image, layout, heaps, counts, half, 8, 1, shape, declared) ||
                !BackingField(image, layout, heaps, shape, 0, "_00", field)) return false;
            int blob = field[2];
            if (blob <= 0 || blob > heaps[1] - 3 ||
                U16(image, (uint)(heaps[0] + blob)) != 0x0602 || image[heaps[0] + blob + 2] != 0x0b)
                return false;
            return SizedShape(image, layout, heaps, counts, vector, 16, 2, shape, declared) &&
                HalfField(image, layout, heaps, shape, 0, "_lower", half, field) &&
                HalfField(image, layout, heaps, shape, 1, "_upper", half, field);
        }

        private static bool SizedShape(byte[] image, int[] layout, int[] heaps, int[] counts, int row,
            uint bytes, int fields, int[] shape, uint[] declared)
        {
            declared[0] = 0;
            declared[1] = 0;
            return row > 0 && ExplicitValueLayouts.ReadClassLayout(image, heaps, layout, counts, row, declared) == 0 &&
                declared[0] == 0 && declared[1] == bytes &&
                ObjectLayouts.ReadSizedShape(image, heaps, layout, row, shape, true, bytes) == 0 &&
                (shape[4] & 0x18) == 8 && shape[2] == fields;
        }

        private static bool BackingField(byte[] image, int[] layout, int[] heaps, int[] shape, int offset,
            string name, int[] field)
        {
            AssemblyReferences.ReadField(image, layout, shape[0], shape[1] + offset, field);
            return (field[0] & 0x30) == 0x20 && MatchesAscii(image, heaps, (uint)field[1], name);
        }

        private static bool HalfField(byte[] image, int[] layout, int[] heaps, int[] shape, int offset,
            string name, int half, int[] field)
        {
            if (!BackingField(image, layout, heaps, shape, offset, name, field)) return false;
            int blob = field[2];
            if (blob <= 0 || blob >= heaps[1]) return false;
            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int start = cursor[0];
            if (size <= 0 || size > limit - start) return false;
            int end = start + size;
            return SignatureReader.TakeElement(image, cursor, end, 6) &&
                SignatureReader.ReadGenericValue(image, cursor, end, 0) == half << 2 && cursor[0] == end;
        }
    }
}
