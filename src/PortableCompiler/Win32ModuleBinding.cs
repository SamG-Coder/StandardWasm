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
            BCryptGenRandom = 19, GetProcessAwareness = 20, LoadLibrary = 21, GetProcedure = 22,
            CallWindowProcedure = 23, RegisterClass = 24, GetClassInfo = 25,
            SetWindowLong = 26, SetWindowLongPtr = 27, CreateWindow = 28,
            GetFullPathName = 29, RedrawWindow = 30, RegisterWindowMessage = 31,
            GetWindowThreadProcessId = 32, Count = GetWindowThreadProcessId;

        internal static string EntryName(int member) => member == Module ? "GetModuleHandleW" :
            member == CreateContext ? "CreateActCtxW" : member == CurrentContext ? "GetCurrentActCtx" :
            member == ActivateContext ? "ActivateActCtx" : member == DeactivateContext ? "DeactivateActCtx" :
            member == ReleaseContext ? "ReleaseActCtx" : member == IsValidDpiContext ? "IsValidDpiAwarenessContext" :
            member == SetProcessDpiContext ? "SetProcessDpiAwarenessContext" : member == SetLegacyDpi ? "SetProcessDPIAware" :
            member == SetProcessAwareness ? "SetProcessDpiAwareness" : member == GetThreadDpiContext ? "GetThreadDpiAwarenessContext" :
            member == EqualDpiContexts ? "AreDpiAwarenessContextsEqual" : member == GetDpiAwareness ? "GetAwarenessFromDpiAwarenessContext" :
            member == IsProcessDpiAware ? "IsProcessDPIAware" :
            member == IsWindowEnabled ? "IsWindowEnabled" : member == EnableWindow ? "EnableWindow" :
            member == GetWindowLong ? "GetWindowLongW" : member == GetWindowLongPtr ? "GetWindowLongPtrW" :
            member == GetProcessAwareness ? "GetProcessDpiAwareness" :
            member == LoadLibrary ? "LoadLibraryExW" : member == GetProcedure ? "GetProcAddress" :
            member == CallWindowProcedure ? "CallWindowProcW" : member == RegisterClass ? "RegisterClassW" :
            member == GetClassInfo ? "GetClassInfoW" : member == SetWindowLong ? "SetWindowLongW" :
            member == SetWindowLongPtr ? "SetWindowLongPtrW" :
            member == CreateWindow ? "CreateWindowExW" :
            member == GetFullPathName ? "GetFullPathNameW" : member == RedrawWindow ? "RedrawWindow" :
            member == RegisterWindowMessage ? "RegisterWindowMessageW" :
            member == GetWindowThreadProcessId ? "GetWindowThreadProcessId" : "BCryptGenRandom";

        private static string Library(int member) => member == LoadLibrary || member == GetFullPathName ? "kernel32.dll" :
            member <= ReleaseContext || member == GetProcedure ? "KERNEL32.dll" :
            member == SetProcessAwareness ? "api-ms-win-shcore-scaling-l1-1-1.dll" :
            member == BCryptGenRandom ? "BCrypt.dll" : "USER32.dll";

        internal static int Arity(int member) =>
            member == CreateWindow ? 12 : member == CallWindowProcedure ? 5 :
            member == BCryptGenRandom || member == GetFullPathName || member == RedrawWindow ? 4 :
            member == LoadLibrary || member == GetClassInfo || IsWindowLongSetter(member) ? 3 :
            member == SetLegacyDpi || member == GetThreadDpiContext || member == IsProcessDpiAware ? 0 :
            member == GetProcedure || member == ActivateContext || member == DeactivateContext || member == EqualDpiContexts ||
            member == EnableWindow || member == GetWindowThreadProcessId ||
            UsesLastError(member) ? 2 : 1;

        internal static bool UsesLastError(int member) =>
            member == GetWindowLong || member == GetWindowLongPtr || IsWindowLongSetter(member);

        private static bool IsWindowLongSetter(int member) => member == SetWindowLong || member == SetWindowLongPtr;

        internal static int FindImport(byte[] image, int[] heaps, int[] methods, int row, int implementation)
        {
            if (implementation != 0x80) return 0; // PreserveSig
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return 0;
            for (int member = 1; member <= Count; member++)
                if (member != GetProcessAwareness && (UsesLastError(member) ? IsWindowLongImport(image, heaps, methods, counts, row, member) :
                    NativeImports.Find(image, heaps, methods, counts, Library(member), EntryName(member), row, 0x101) == row))
                    return member != GetWindowThreadProcessId || IsWindowOwnerSignature(image, heaps, methods, row) ? member : 0;
            return 0;
        }

        private static bool IsWindowOwnerSignature(byte[] image, int[] heaps, int[] methods, int row)
        {
            const int unsignedInt32 = 0x09, valueType = 0x11, pointer = 0x0f;
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4), 2, cursor);
            // Signature storage reduces native addresses and signed/unsigned
            // i32 values. Confirm the SDK's DWORD and DWORD* before reduction.
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, unsignedInt32) &&
                SignatureReader.TakeElement(image, cursor, end, valueType) &&
                ImageBytes.CompressedUnsigned(image, cursor[0], end, cursor) > 0 &&
                SignatureReader.TakeElement(image, cursor, end, pointer) &&
                SignatureReader.TakeElement(image, cursor, end, unsignedInt32) && cursor[0] == end;
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
            int result = types[offset];
            if (member == GetProcessAwareness)
                return arity == 2 && IsAbiShape(bundle, objects, types[offset + 1], Native, "HANDLE") &&
                    types[offset + 2] == Int32Location && IsAbiShape(bundle, objects, result, I32, "HRESULT");
            if (arity != Arity(member)) return false;
            // CoreLib's generated P/Invoke is the raw UTF-16 form. Its
            // managed wrapper pins the string and captures last error.
            if (member == LoadLibrary)
                return result == Native && types[offset + 1] == Native &&
                    types[offset + 2] == Native && types[offset + 3] == I32;
            if (member == GetProcedure)
                return IsAbiShape(bundle, objects, types[offset + 1], Native, "HMODULE") &&
                    IsAbiShape(bundle, objects, types[offset + 2], Native, "PCSTR") &&
                    IsAbiShape(bundle, objects, result, Native, "FARPROC");
            // The Windows SDK entry passes a native code address and three
            // pointer-sized Foundation values. The host validates the token ABI.
            if (member == CallWindowProcedure)
                return types[offset + 1] == Native && types[offset + 3] == I32 &&
                    IsAbiShape(bundle, objects, types[offset + 2], Native, "HWND") &&
                    IsAbiShape(bundle, objects, types[offset + 4], Native, "WPARAM") &&
                    IsAbiShape(bundle, objects, types[offset + 5], Native, "LPARAM") &&
                    IsAbiShape(bundle, objects, result, Native, "LRESULT");
            // The source-generated wrappers retain pinning and manual error capture.
            if (member == RegisterClass) return result == Unsigned16 && types[offset + 1] == Native;
            if (member == GetClassInfo)
                return IsAbiShape(bundle, objects, result, I32, "BOOL") &&
                    IsAbiShape(bundle, objects, types[offset + 1], Native, "HINSTANCE") &&
                    IsAbiShape(bundle, objects, types[offset + 2], Native, "PCWSTR") &&
                    types[offset + 3] == Native;
            if (member == CreateWindow)
                return MatchesCreateWindowSignature(bundle, objects, types, offset);
            // The CoreLib wrapper pins both UTF-16 addresses and owns the
            // system-error snapshot. This leaf accepts linear addresses only.
            if (member == GetFullPathName)
                return result == I32 && types[offset + 1] == Native && types[offset + 2] == I32 &&
                    types[offset + 3] == Native && types[offset + 4] == Native;
            if (member == RegisterWindowMessage)
                return result == I32 && IsAbiShape(bundle, objects, types[offset + 1], Native, "PCWSTR");
            if (member == GetWindowThreadProcessId)
                return result == I32 && IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == Native;
            if (member == RedrawWindow)
                return IsAbiShape(bundle, objects, result, I32, "BOOL") &&
                    IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == Native &&
                    IsAbiShape(bundle, objects, types[offset + 3], Native, "HRGN", "Windows.Win32.Graphics.Gdi") &&
                    types[offset + 4] == I32;
            if (IsWindowLongSetter(member))
                return IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == I32 && types[offset + 3] == Native && result == Native;
            if (member == BCryptGenRandom)
                return types[offset + 1] == Native && types[offset + 2] == Native &&
                    types[offset + 3] == I32 && types[offset + 4] == I32 && result == I32;
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

        private static bool MatchesCreateWindowSignature(BundleImages bundle, int[] objects, int[] types, int offset)
        {
            // Styles are uint-backed SDK enums. The original managed wrapper still
            // owns pinning and the transition from system to saved P/Invoke error.
            return IsAbiShape(bundle, objects, types[offset], Native, "HWND") &&
                types[offset + 1] == I32 &&
                IsAbiShape(bundle, objects, types[offset + 2], Native, "PCWSTR") &&
                IsAbiShape(bundle, objects, types[offset + 3], Native, "PCWSTR") &&
                types[offset + 4] == I32 && types[offset + 5] == I32 && types[offset + 6] == I32 &&
                types[offset + 7] == I32 && types[offset + 8] == I32 &&
                IsAbiShape(bundle, objects, types[offset + 9], Native, "HWND") &&
                IsAbiShape(bundle, objects, types[offset + 10], Native, "HMENU", "Windows.Win32.UI.WindowsAndMessaging") &&
                IsAbiShape(bundle, objects, types[offset + 11], Native, "HINSTANCE") &&
                types[offset + 12] == Native;
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
