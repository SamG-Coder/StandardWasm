using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class Win32ModuleBinding
    {
        internal const int Module = 1, CreateContext = 2, CurrentContext = 3,
            ActivateContext = 4, DeactivateContext = 5, ReleaseContext = 6,
            IsValidDpiContext = 7, SetProcessDpiContext = 8, SetLegacyDpi = 9, SetProcessAwareness = 10,
            GetThreadDpiContext = 11, EqualDpiContexts = 12, GetDpiAwareness = 13, IsProcessDpiAware = 14,
            IsWindowEnabled = 15, EnableWindow = 16, GetWindowLong = 17, GetWindowLongPtr = 18,
            Count = GetWindowLongPtr;

        internal static string EntryName(int member) => member == Module ? "GetModuleHandleW" :
            member == CreateContext ? "CreateActCtxW" : member == CurrentContext ? "GetCurrentActCtx" :
            member == ActivateContext ? "ActivateActCtx" : member == DeactivateContext ? "DeactivateActCtx" :
            member == ReleaseContext ? "ReleaseActCtx" : member == IsValidDpiContext ? "IsValidDpiAwarenessContext" :
            member == SetProcessDpiContext ? "SetProcessDpiAwarenessContext" : member == SetLegacyDpi ? "SetProcessDPIAware" :
            member == SetProcessAwareness ? "SetProcessDpiAwareness" : member == GetThreadDpiContext ? "GetThreadDpiAwarenessContext" :
            member == EqualDpiContexts ? "AreDpiAwarenessContextsEqual" : member == GetDpiAwareness ? "GetAwarenessFromDpiAwarenessContext" :
            member == IsProcessDpiAware ? "IsProcessDPIAware" :
            member == IsWindowEnabled ? "IsWindowEnabled" : member == EnableWindow ? "EnableWindow" :
            member == GetWindowLong ? "GetWindowLongW" : "GetWindowLongPtrW";

        private static string Library(int member) => member <= ReleaseContext ? "KERNEL32.dll" :
            member == SetProcessAwareness ? "api-ms-win-shcore-scaling-l1-1-1.dll" : "USER32.dll";

        internal static int Arity(int member) =>
            member == SetLegacyDpi || member == GetThreadDpiContext || member == IsProcessDpiAware ? 0 :
            member == ActivateContext || member == DeactivateContext || member == EqualDpiContexts || member == EnableWindow ||
            UsesLastError(member) ? 2 : 1;

        internal static bool UsesLastError(int member) => member == GetWindowLong || member == GetWindowLongPtr;

        internal static int FindImport(byte[] image, int[] heaps, int[] methods, int row, int implementation)
        {
            if (implementation != 0x80) return 0; // PreserveSig
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return 0;
            for (int member = 1; member <= Count; member++)
                if (UsesLastError(member) ? IsWindowLongImport(image, heaps, methods, counts, row, member) :
                    NativeImports.Find(image, heaps, methods, counts, Library(member), EntryName(member), row, 0x101) == row)
                    return member;
            return 0;
        }

        private static bool IsWindowLongImport(byte[] image, int[] heaps, int[] methods, int[] counts, int row, int member)
        {
            // WinForms' generated declarations and ordinary exact-spelling P/Invokes
            // use the same API and SetLastError ABI.
            string entry = EntryName(member);
            return NativeImports.Find(image, heaps, methods, counts, "user32.dll", entry, row, 0x140) == row ||
                NativeImports.Find(image, heaps, methods, counts, "user32.dll", entry, row, 0x141) == row ||
                NativeImports.Find(image, heaps, methods, counts, "USER32.dll", entry, row, 0x140) == row ||
                NativeImports.Find(image, heaps, methods, counts, "USER32.dll", entry, row, 0x141) == row;
        }

        internal static bool MatchesSignature(BundleImages bundle, int[] objects, int member, int[] types, int offset, int arity)
        {
            if (arity != Arity(member)) return false;
            int result = types[offset];
            if (UsesLastError(member))
                return IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == I32 && (result == Native || member == GetWindowLong && result == I32);
            if (member == IsWindowEnabled || member == EnableWindow)
                return IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    IsAbiShape(bundle, objects, result, I32, "BOOL") &&
                    (member == IsWindowEnabled || IsAbiShape(bundle, objects, types[offset + 2], I32, "BOOL"));
            if (member >= IsValidDpiContext) return MatchesDpiSignature(bundle, objects, member, types, offset, arity);
            int input = types[offset + 1];
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

        private static bool MatchesDpiSignature(BundleImages bundle, int[] objects, int member, int[] types, int offset, int arity)
        {
            int result = types[offset];
            if (member == GetThreadDpiContext) return IsDpiContext(bundle, objects, result);
            if (member == SetProcessAwareness)
                return types[offset + 1] == I32 && IsAbiShape(bundle, objects, result, I32, "HRESULT");
            if (member == GetDpiAwareness)
                return result == I32 && IsDpiContext(bundle, objects, types[offset + 1]);
            if (!IsAbiShape(bundle, objects, result, I32, "BOOL")) return false;
            for (int argument = 1; argument <= arity; argument++)
                if (!IsDpiContext(bundle, objects, types[offset + argument])) return false;
            return true;
        }

        private static bool IsDpiContext(BundleImages bundle, int[] objects, int type) =>
            IsAbiShape(bundle, objects, type, Native, "DPI_AWARENESS_CONTEXT", "Windows.Win32.UI.HiDpi");

        private static bool IsAbiShape(BundleImages bundle, int[] objects, int type, int field, string name,
            string space = "Windows.Win32.Foundation")
        {
            if (type == field) return true;
            if (!ObjectLayouts.IsValueType(objects, type) || ObjectLayouts.IsExplicitValue(objects, type) ||
                ObjectLayouts.IsAutoValue(objects, type) || ObjectLayouts.IsByRefLikeValue(objects, type) ||
                ObjectLayouts.IsInlineArray(objects, type))
                return false;

            // Layout slots track metadata field rows. Static fields leave empty
            // slots, including the Win32 SDK's constants and singleton values.
            bool foundField = false;
            for (int index = 0; index < ObjectLayouts.FieldCount(objects, type); index++)
            {
                int storage = ObjectLayouts.FieldTypeAt(objects, type, index);
                if (storage == 0) continue;
                if (foundField || storage != field) return false;
                foundField = true;
            }
            if (!foundField) return false;
            int owner = ObjectLayouts.AssemblyOf(objects, type);
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return false;
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first,
                ObjectLayouts.TypeDef(objects, type), space, name);
        }

    }
}
