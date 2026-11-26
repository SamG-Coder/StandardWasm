using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class RuntimeTypeMetadata
    {
        internal const int Type = NativeStorage.Vector + 1, Handle = Type + 1, Module = Handle + 1, Assembly = Module + 1;

        internal static bool IsStorage(int type) => type == Handle || IsReference(type);
        internal static bool IsReference(int type) => type == Type || type == Module || type == Assembly;

        internal static int Storage(byte[] image, int[] heaps, int[] layout, int coded, bool valueType)
        {
            int kind = Kind(image, heaps, layout, coded);
            if (valueType) return kind == PlatformTypeReferences.TypeHandle ? Handle : 0;
            if (kind == PlatformTypeReferences.Type) return Type;
            if (kind == PlatformTypeReferences.Module) return Module;
            return kind == PlatformTypeReferences.Assembly ? Assembly : 0;
        }

        internal static bool RequiresReflectionCast(byte[] image, int[] heaps, int[] layout, int coded)
        {
            int kind = Kind(image, heaps, layout, coded);
            return kind == PlatformTypeReferences.Type || kind == PlatformTypeReferences.TypeBase ||
                kind == PlatformTypeReferences.Module || kind == PlatformTypeReferences.Assembly;
        }

        private static int Kind(byte[] image, int[] heaps, int[] layout, int coded)
        {
            if ((coded & 3) != 1) return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first < 0 ? 0 : PlatformTypeReferences.Identify(image, heaps, layout, counts, first, coded >> 2);
        }

        internal static bool ValidDefinition(byte[] image, int[] heaps, int row)
        {
            // TypeDef row 1 is the module pseudo-type and has no runtime Type.
            if (row < 2) return false;
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 && row <= counts[2];
        }

        internal static bool IsGetTypeFromHandle(byte[] image, int[] heaps, int[] layout, int row)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 || member[AssemblyReferences.Depth] != 0 ||
                !PlatformTypeReferences.Named17(image, (uint)heaps[2], (uint)heaps[3], (uint)member[4],
                    0x54746547, 0x46657079, 0x486d6f72, 0x6c646e61, 0x65)) // GetTypeFromHandle
                return false;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || PlatformTypeReferences.Identify(image, heaps, layout, counts, first, member[AssemblyReferences.Leaf]) != PlatformTypeReferences.Type)
                return false;

            int blob = member[5], limit = heaps[0] + heaps[1];
            if (blob <= 0 || blob >= heaps[1]) return false;
            var next = new int[1];
            int bytes = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (bytes < 6 || bytes > limit - start || image[start] != 0 || image[start + 1] != 1 || image[start + 2] != 0x12)
                return false;
            int end = start + bytes;
            int result = CompressedUnsigned(image, start + 3, end, next);
            int cursor = next[0];
            if ((result & 3) != 1 || cursor >= end || image[cursor] != 0x11 ||
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, result >> 2) != PlatformTypeReferences.Type)
                return false;
            int parameter = CompressedUnsigned(image, cursor + 1, end, next);
            return next[0] == end && (parameter & 3) == 1 &&
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, parameter >> 2) == PlatformTypeReferences.TypeHandle;
        }
    }
}
