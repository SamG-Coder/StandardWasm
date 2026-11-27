using PeLibrary;

namespace PortableCompiler
{
    internal static class ComOutputPointer
    {
        // CoCreateInstance writes the interface pointer to its final void**
        // argument during the call. Keep this proof at the raw ImplMap and
        // signature boundary, before the pointer types erase to Wasm i32.
        internal static bool MatchesActivation(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int methods, int row)
        {
            if (row < 1 || row > methods || MethodDefinitions.ReadColumn(image, layout, row, 1) != 0x80) return false;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                NativeImports.Find(image, heaps, layout, counts, "OLE32.dll", "CoCreateInstance", row, 0x101) != row &&
                NativeImports.Find(image, heaps, layout, counts, "ole32.dll", "CoCreateInstance", row, 0x101) != row)
                return false;

            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 5, cursor);
            if (end < 0) return false;
            int result = Word(image, cursor, end, 0x08);
            if (result < 0 || !Take(image, cursor, end, 0x0f)) return false;
            int clsid = ValueType(image, cursor, end);
            if (clsid < 1 || !Take(image, cursor, end, 0x0f)) return false;
            int outer = Word(image, cursor, end, 1), context = Word(image, cursor, end, 0x09);
            if (outer < 0 || context < 0 || !Take(image, cursor, end, 0x0f) || ValueType(image, cursor, end) != clsid ||
                !Take(image, cursor, end, 0x0f) || !Take(image, cursor, end, 0x0f) ||
                !Take(image, cursor, end, 1) || cursor[0] != end) return false;
            return MatchesLayout(bundle, image, layout, heaps, owner, clsid, "System", "Guid", 16, true) &&
                (result == 0 || MatchesLayout(bundle, image, layout, heaps, owner, result, "Windows.Win32.Foundation", "HRESULT", 4)) &&
                (outer == 0 || MatchesLayout(bundle, image, layout, heaps, owner, outer, "Windows.Win32.System.Com", "IUnknown", 4)) &&
                (context == 0 || MatchesLayout(bundle, image, layout, heaps, owner, context, "Windows.Win32.System.Com", "CLSCTX", 4));
        }

        private static bool MatchesLayout(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int coded, string space, string name, int size, bool coreLib = false)
        {
            if (coded < 1) return false;
            var identity = new int[2];
            if (AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity) < 0) return false;
            byte[] target = bundle.Get(identity[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            var names = new int[2];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0 ||
                ObjectLayouts.TypeDefinitionNames(target, targetHeaps, targetLayout, identity[1], names) < 0 ||
                !ImageBytes.MatchesAscii(target, targetHeaps, (uint)names[0], name) ||
                !ImageBytes.MatchesAscii(target, targetHeaps, (uint)names[1], space)) return false;
            if (coreLib)
            {
                var counts = new int[45];
                if (MetadataTableRows.ReadCounts(target, targetHeaps[4], targetHeaps[5], counts) < 0 ||
                    !PlatformAssemblyIdentity.IsCoreLibDefinition(target, targetHeaps, targetLayout, counts)) return false;
            }
            var measured = new int[2];
            return NativeValueLayouts.Measure(bundle, identity[0], identity[1], measured) == 0 && measured[0] == size;
        }

        private static int Word(byte[] image, int[] cursor, int end, int scalar) =>
            Take(image, cursor, end, scalar) ? 0 : ValueType(image, cursor, end);

        private static int ValueType(byte[] image, int[] cursor, int end) =>
            Take(image, cursor, end, 0x11) ? ImageBytes.CompressedUnsigned(image, cursor[0], end, cursor) : -1;

        private static bool Take(byte[] image, int[] cursor, int end, int element) =>
            SignatureReader.TakeElement(image, cursor, end, element);
    }
}
