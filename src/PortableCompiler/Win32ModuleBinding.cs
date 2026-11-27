using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class Win32ModuleBinding
    {
        internal const int Module = 1, CreateContext = 2;

        internal static int FindImport(byte[] image, int[] heaps, int[] methods, int row, int implementation)
        {
            if (implementation != 0x80) return 0; // PreserveSig
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return 0;
            if (NativeImports.Find(image, heaps, methods, counts, "KERNEL32.dll", "GetModuleHandleW", row, 0x101) == row)
                return Module;
            if (NativeImports.Find(image, heaps, methods, counts, "KERNEL32.dll", "CreateActCtxW", row, 0x101) == row)
                return CreateContext;
            return 0;
        }

        internal static bool MatchesSignature(BundleImages bundle, int[] objects, int import, int input, int result)
        {
            // ACTCTXW* is passed as a native linear-memory address. The SDK's
            // HANDLE return wrapper has one native field, like HMODULE.
            if (import == CreateContext)
                return input == Native && IsAbiShape(bundle, objects, result, "HANDLE");
            return IsAbiShape(bundle, objects, input, "PCWSTR") && IsAbiShape(bundle, objects, result, "HMODULE");
        }

        internal static bool IsAbiShape(BundleImages bundle, int[] objects, int type, string name)
        {
            if (type == Native) return true;
            if (!ObjectLayouts.IsValueType(objects, type) || ObjectLayouts.IsExplicitValue(objects, type) ||
                ObjectLayouts.IsAutoValue(objects, type) || ObjectLayouts.IsByRefLikeValue(objects, type) ||
                ObjectLayouts.FieldCount(objects, type) != 1 || ObjectLayouts.FieldTypeAt(objects, type, 0) != Native)
                return false;
            int owner = ObjectLayouts.AssemblyOf(objects, type);
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return false;
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first,
                ObjectLayouts.TypeDef(objects, type), "Windows.Win32.Foundation", name);
        }

    }
}
