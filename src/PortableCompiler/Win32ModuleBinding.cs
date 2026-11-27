using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class Win32ModuleBinding
    {
        internal const int Module = 1, CreateContext = 2, CurrentContext = 3,
            ActivateContext = 4, DeactivateContext = 5, ReleaseContext = 6, Count = ReleaseContext;

        internal static string EntryName(int member) => member == Module ? "GetModuleHandleW" :
            member == CreateContext ? "CreateActCtxW" : member == CurrentContext ? "GetCurrentActCtx" :
            member == ActivateContext ? "ActivateActCtx" : member == DeactivateContext ? "DeactivateActCtx" : "ReleaseActCtx";

        internal static int Arity(int member) => member == ActivateContext || member == DeactivateContext ? 2 : 1;

        internal static int FindImport(byte[] image, int[] heaps, int[] methods, int row, int implementation)
        {
            if (implementation != 0x80) return 0; // PreserveSig
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return 0;
            for (int member = 1; member <= Count; member++)
                if (NativeImports.Find(image, heaps, methods, counts, "KERNEL32.dll", EntryName(member), row, 0x101) == row)
                    return member;
            return 0;
        }

        internal static bool MatchesSignature(BundleImages bundle, int[] objects, int member, int[] types, int offset, int arity)
        {
            if (arity != Arity(member)) return false;
            int result = types[offset], input = types[offset + 1];
            if (member == Module)
                return IsAbiShape(bundle, objects, input, Native, "PCWSTR") && IsAbiShape(bundle, objects, result, Native, "HMODULE");
            if (member == CreateContext)
                return input == Native && IsAbiShape(bundle, objects, result, Native, "HANDLE");
            if (member == ReleaseContext)
                return IsAbiShape(bundle, objects, input, Native, "HANDLE") && result == Void;

            if (!IsAbiShape(bundle, objects, result, I32, "BOOL")) return false;
            if (member == CurrentContext) return input == Native;
            if (types[offset + 2] != Native) return false;
            return member == ActivateContext ? IsAbiShape(bundle, objects, input, Native, "HANDLE") : input == I32;
        }

        private static bool IsAbiShape(BundleImages bundle, int[] objects, int type, int field, string name)
        {
            if (type == field) return true;
            if (!ObjectLayouts.IsValueType(objects, type) || ObjectLayouts.IsExplicitValue(objects, type) ||
                ObjectLayouts.IsAutoValue(objects, type) || ObjectLayouts.IsByRefLikeValue(objects, type) ||
                ObjectLayouts.FieldCount(objects, type) != 1 || ObjectLayouts.FieldTypeAt(objects, type, 0) != field)
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
