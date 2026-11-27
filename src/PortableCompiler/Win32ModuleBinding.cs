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
            GetWindowThreadProcessId = 32, GetCurrentProcess = 33, GetCurrentThread = 34, GetCurrentThreadId = 35,
            DuplicateHandle = 36, GetExitCodeThread = 37, CloseHandle = 38,
            GetProcessWindowStation = 39, GetUserObjectInformation = 40,
            GetLongPathName = 41, SetClassLong = 42, GetDeviceCaps = 43,
            GetDC = 44, ReleaseDC = 45, InvalidateRect = 46, AdjustWindowRectForDpi = 47, IsWindow = 48,
            Count = IsWindow;

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
            member == GetWindowThreadProcessId ? "GetWindowThreadProcessId" :
            member == GetCurrentProcess ? "GetCurrentProcess" : member == GetCurrentThread ? "GetCurrentThread" :
            member == GetCurrentThreadId ? "GetCurrentThreadId" : member == DuplicateHandle ? "DuplicateHandle" :
            member == GetExitCodeThread ? "GetExitCodeThread" : member == CloseHandle ? "CloseHandle" :
            member == GetProcessWindowStation ? "GetProcessWindowStation" :
            member == GetUserObjectInformation ? "GetUserObjectInformationW" :
            member == GetLongPathName ? "GetLongPathNameW" :
            member == SetClassLong ? "SetClassLongW" : member == GetDeviceCaps ? "GetDeviceCaps" :
            member == GetDC ? "GetDC" : member == ReleaseDC ? "ReleaseDC" :
            member == InvalidateRect ? "InvalidateRect" :
            member == AdjustWindowRectForDpi ? "AdjustWindowRectExForDpi" :
            member == IsWindow ? "IsWindow" : "BCryptGenRandom";

        private static string Library(int member) => member == LoadLibrary || member == GetFullPathName ||
            member == GetLongPathName ? "kernel32.dll" :
            member == SetClassLong ? "user32.dll" : member == GetDeviceCaps ? "GDI32.dll" :
            member <= ReleaseContext || member == GetProcedure || IsThreadImport(member) ? "KERNEL32.dll" :
            member == SetProcessAwareness ? "api-ms-win-shcore-scaling-l1-1-1.dll" :
            member == BCryptGenRandom ? "BCrypt.dll" : "USER32.dll";

        internal static int Arity(int member) =>
            member == CreateWindow ? 12 : member == DuplicateHandle ? 7 :
            member == CallWindowProcedure || member == GetUserObjectInformation || member == AdjustWindowRectForDpi ? 5 :
            member == BCryptGenRandom || member == GetFullPathName || member == RedrawWindow ? 4 :
            member == LoadLibrary || member == GetClassInfo || member == GetLongPathName ||
            member == SetClassLong || member == InvalidateRect || IsWindowLongSetter(member) ? 3 :
            member == SetLegacyDpi || member == GetThreadDpiContext || member == IsProcessDpiAware ||
            member == GetCurrentProcess || member == GetCurrentThread || member == GetCurrentThreadId ||
            member == GetProcessWindowStation ? 0 :
            member == GetProcedure || member == ActivateContext || member == DeactivateContext || member == EqualDpiContexts ||
            member == EnableWindow || member == GetWindowThreadProcessId || member == GetExitCodeThread ||
            member == GetDeviceCaps || member == ReleaseDC ||
            UsesLastError(member) ? 2 : 1;

        internal static bool UsesLastError(int member) =>
            member == GetWindowLong || member == GetWindowLongPtr || IsWindowLongSetter(member);

        private static bool IsThreadImport(int member) => member >= GetCurrentProcess && member <= CloseHandle;

        private static bool IsWindowStationImport(int member) =>
            member == GetProcessWindowStation || member == GetUserObjectInformation;

        private static bool IsWindowLongSetter(int member) => member == SetWindowLong || member == SetWindowLongPtr;

        internal static int FindImport(byte[] image, int[] heaps, int[] methods, int row, int implementation,
            BundleImages bundle = null, int owner = 0)
        {
            if (implementation != 0x80) return 0; // PreserveSig
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return 0;
            for (int member = 1; member <= Count; member++)
                if (member != GetProcessAwareness && (UsesLastError(member) ? IsWindowLongImport(image, heaps, methods, counts, row, member) :
                    NativeImports.Find(image, heaps, methods, counts, Library(member), EntryName(member), row,
                        member == SetClassLong ? 0x100 : 0x101) == row ||
                    // The original wrapper contains both architecture branches.
                    // wasm32 uses one procedure-word ABI for both raw names.
                    member == SetClassLong &&
                    NativeImports.Find(image, heaps, methods, counts, "user32.dll", "SetClassLongPtrW", row, 0x100) == row ||
                    member == GetDeviceCaps &&
                    NativeImports.Find(image, heaps, methods, counts, "gdi32.dll", "GetDeviceCaps", row, 0x101) == row ||
                    (member == GetDC || member == ReleaseDC) &&
                    NativeImports.Find(image, heaps, methods, counts, "user32.dll", EntryName(member), row, 0x101) == row ||
                    IsWindowStationImport(member) &&
                    NativeImports.Find(image, heaps, methods, counts, "user32.dll", EntryName(member), row, 0x101) == row))
                {
                    if (member == GetWindowThreadProcessId && !IsWindowOwnerSignature(image, heaps, methods, row)) return 0;
                    if (member == GetUserObjectInformation && !IsUserObjectInformationSignature(image, heaps, methods, row)) return 0;
                    if (member == GetLongPathName && !ScopedPathBinding.MatchesSignature(image, methods, heaps, row, 0x0f, false))
                        return 0;
                    if ((member == DuplicateHandle || member == GetExitCodeThread) &&
                        !MatchesThreadOutputPointer(image, heaps, methods, row, member)) return 0;
                    if (member >= GetDeviceCaps && member <= ReleaseDC && !MatchesDisplaySignature(image, heaps, methods, row, member))
                        return 0;
                    if (member == InvalidateRect && !MatchesInvalidateRect(bundle, image, heaps, methods, owner, row)) return 0;
                    if (member == AdjustWindowRectForDpi && !MatchesAdjustWindowRect(bundle, image, heaps, methods, owner, row)) return 0;
                    if (member == IsWindow && !MatchesIsWindow(image, heaps, methods, row)) return 0;
                    return member;
                }
            return 0;
        }

        private static bool MatchesIsWindow(byte[] image, int[] heaps, int[] methods, int row)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4), 1, cursor);
            // BOOL is a signed word and HWND is a native word. Check the raw
            // signature before both become Wasm i32. The layout check verifies SDK wrappers.
            return end >= 0 && ReadAbiWord(image, cursor, end, 0x08) >= 0 &&
                ReadAbiWord(image, cursor, end, 0x18) >= 0 && cursor[0] == end;
        }

        private static bool MatchesInvalidateRect(BundleImages bundle, byte[] image, int[] heaps, int[] methods, int owner, int row)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4), 3, cursor);
            if (bundle == null || end < 0 || ReadAbiWord(image, cursor, end, 0x08) < 0 ||
                ReadAbiWord(image, cursor, end, 0x18) < 0 || !SignatureReader.TakeElement(image, cursor, end, 0x0f) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return false;
            int pointee = ImageBytes.CompressedUnsigned(image, cursor[0], end, cursor);
            if (pointee < 1 || ReadAbiWord(image, cursor, end, 0x08) < 0 || cursor[0] != end) return false;

            return MatchesRect(bundle, image, heaps, methods, owner, pointee);
        }

        private static bool MatchesAdjustWindowRect(BundleImages bundle, byte[] image, int[] heaps, int[] methods, int owner, int row)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4), 5, cursor);
            if (bundle == null || end < 0 || ReadAbiWord(image, cursor, end, 0x08) < 0 ||
                !SignatureReader.TakeElement(image, cursor, end, 0x0f) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return false;
            int pointee = ImageBytes.CompressedUnsigned(image, cursor[0], end, cursor);
            return pointee > 0 && ReadAbiWord(image, cursor, end, 0x09) >= 0 &&
                ReadAbiWord(image, cursor, end, 0x08) >= 0 && ReadAbiWord(image, cursor, end, 0x09) >= 0 &&
                SignatureReader.TakeElement(image, cursor, end, 0x09) && cursor[0] == end &&
                MatchesRect(bundle, image, heaps, methods, owner, pointee);
        }

        private static bool MatchesRect(BundleImages bundle, byte[] image, int[] heaps, int[] methods, int owner, int pointee)
        {
            // A native stack tag erases the difference between RECT* and int*.
            // Validate the resolved Win32 declaration and its byte extent.
            var identity = new int[2];
            if (AssemblyBundle.ClassIdentity(bundle, image, methods, heaps, owner, pointee, identity) < 0) return false;
            byte[] target = bundle.Get(identity[0]);
            var layout = new int[4];
            var targetHeaps = new int[8];
            var names = new int[2];
            if (CompressedTables.FindWithHeaps(target, layout, targetHeaps) < 0 ||
                ObjectLayouts.TypeDefinitionNames(target, targetHeaps, layout, identity[1], names) < 0 ||
                !ImageBytes.MatchesAscii(target, targetHeaps, (uint)names[0], "RECT") ||
                !ImageBytes.MatchesAscii(target, targetHeaps, (uint)names[1], "Windows.Win32.Foundation"))
                return false;
            var size = new int[2];
            return NativeValueLayouts.Measure(bundle, identity[0], identity[1], size) == 0 && size[0] == 16 && size[1] == 4;
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

        private static bool IsUserObjectInformationSignature(byte[] image, int[] heaps, int[] methods, int row)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4), 5, cursor);
            // Wasm reduces pointer element types and signedness. Confirm void*,
            // DWORD size and optional DWORD* before accepting the native ABI.
            return end >= 0 && ReadAbiWord(image, cursor, end, 0x08) >= 0 &&
                ReadAbiWord(image, cursor, end, 0x18) >= 0 && ReadAbiWord(image, cursor, end, 0x08) >= 0 &&
                SignatureReader.TakeElement(image, cursor, end, 0x0f) &&
                SignatureReader.TakeElement(image, cursor, end, 0x01) &&
                SignatureReader.TakeElement(image, cursor, end, 0x09) &&
                SignatureReader.TakeElement(image, cursor, end, 0x0f) &&
                SignatureReader.TakeElement(image, cursor, end, 0x09) && cursor[0] == end;
        }

        private static bool MatchesThreadOutputPointer(byte[] image, int[] heaps, int[] methods, int row, int member)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4),
                Arity(member), cursor);
            if (end < 0 || ReadAbiWord(image, cursor, end, 0x08) < 0) return false;
            int handle = ReadAbiWord(image, cursor, end, 0x18);
            if (handle < 0) return false;
            if (member == GetExitCodeThread)
                return SignatureReader.TakeElement(image, cursor, end, 0x0f) &&
                    SignatureReader.TakeElement(image, cursor, end, 0x09) && cursor[0] == end;

            // The duplicate output points to the same HANDLE type as the
            // source process argument. A smaller pointer target cannot hold it.
            return ReadAbiWord(image, cursor, end, 0x18) >= 0 &&
                ReadAbiWord(image, cursor, end, 0x18) >= 0 &&
                SignatureReader.TakeElement(image, cursor, end, 0x0f) &&
                ReadAbiWord(image, cursor, end, 0x18) == handle;
        }

        private static int ReadAbiWord(byte[] image, int[] cursor, int end, int scalar)
        {
            if (SignatureReader.TakeElement(image, cursor, end, scalar)) return 0;
            if (!SignatureReader.TakeElement(image, cursor, end, 0x11)) return -1;
            int token = ImageBytes.CompressedUnsigned(image, cursor[0], end, cursor);
            return token > 0 ? token : -1;
        }

        private static bool MatchesDisplaySignature(byte[] image, int[] heaps, int[] methods, int row, int member)
        {
            const int signedInt = 0x08, nativeInt = 0x18;
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4), Arity(member), cursor);
            if (end < 0) return false;
            // Verify the raw storage before Wasm erases integer signedness.
            // MatchesSignature checks the nominal SDK handle types.
            if (member == GetDC)
                return ReadAbiWord(image, cursor, end, nativeInt) >= 0 &&
                    ReadAbiWord(image, cursor, end, nativeInt) >= 0 && cursor[0] == end;
            if (!SignatureReader.TakeElement(image, cursor, end, signedInt) ||
                ReadAbiWord(image, cursor, end, nativeInt) < 0) return false;
            return (member == GetDeviceCaps ? SignatureReader.TakeElement(image, cursor, end, signedInt) :
                ReadAbiWord(image, cursor, end, nativeInt) >= 0) && cursor[0] == end;
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
            if (member == GetDeviceCaps)
                return result == I32 && IsDisplayContext(bundle, objects, types[offset + 1]) && types[offset + 2] == I32;
            if (member == GetDC)
                return IsDisplayContext(bundle, objects, result) &&
                    IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND");
            if (member == ReleaseDC)
                return result == I32 && IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    IsDisplayContext(bundle, objects, types[offset + 2]);
            if (IsThreadImport(member)) return MatchesThreadSignature(bundle, objects, member, types, offset);
            if (member == GetProcessWindowStation)
                return IsAbiShape(bundle, objects, result, Native, "HWINSTA", "Windows.Win32.System.StationsAndDesktops");
            if (member == GetUserObjectInformation)
                return IsAbiShape(bundle, objects, result, I32, "BOOL") &&
                    IsAbiShape(bundle, objects, types[offset + 1], Native, "HANDLE") &&
                    types[offset + 2] == I32 && types[offset + 3] == Native &&
                    types[offset + 4] == I32 && types[offset + 5] == Native;
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
            if (member == GetLongPathName)
                return result == I32 && types[offset + 1] == Native && types[offset + 2] == Native && types[offset + 3] == I32;
            if (member == RegisterWindowMessage)
                return result == I32 && IsAbiShape(bundle, objects, types[offset + 1], Native, "PCWSTR");
            if (member == GetWindowThreadProcessId)
                return result == I32 && IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == Native;
            if (member == InvalidateRect)
                return IsAbiShape(bundle, objects, result, I32, "BOOL") &&
                    IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") && types[offset + 2] == Native &&
                    IsAbiShape(bundle, objects, types[offset + 3], I32, "BOOL");
            if (member == AdjustWindowRectForDpi)
                return IsAbiShape(bundle, objects, result, I32, "BOOL") && types[offset + 1] == Native &&
                    types[offset + 2] == I32 && IsAbiShape(bundle, objects, types[offset + 3], I32, "BOOL") &&
                    types[offset + 4] == I32 && types[offset + 5] == I32;
            if (member == RedrawWindow)
                return IsAbiShape(bundle, objects, result, I32, "BOOL") &&
                    IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == Native &&
                    IsAbiShape(bundle, objects, types[offset + 3], Native, "HRGN", "Windows.Win32.Graphics.Gdi") &&
                    types[offset + 4] == I32;
            // The window and class setters share the x86 procedure-word shape.
            // Only the window declarations request automatic error capture.
            if (IsWindowLongSetter(member) || member == SetClassLong)
                return IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == I32 && types[offset + 3] == Native && result == Native;
            if (member == BCryptGenRandom)
                return types[offset + 1] == Native && types[offset + 2] == Native &&
                    types[offset + 3] == I32 && types[offset + 4] == I32 && result == I32;
            if (UsesLastError(member))
                return IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == I32 && (result == Native || member == GetWindowLong && result == I32);
            if (member == IsWindow || member == IsWindowEnabled || member == EnableWindow)
                return IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    IsAbiShape(bundle, objects, result, I32, "BOOL") &&
                    (member != EnableWindow || IsAbiShape(bundle, objects, types[offset + 2], I32, "BOOL"));
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

        private static bool MatchesThreadSignature(BundleImages bundle, int[] objects, int member, int[] types, int offset)
        {
            int result = types[offset];
            if (member == GetCurrentProcess || member == GetCurrentThread)
                return IsAbiShape(bundle, objects, result, Native, "HANDLE");
            if (member == GetCurrentThreadId) return result == I32;
            if (!IsAbiShape(bundle, objects, result, I32, "BOOL") ||
                !IsAbiShape(bundle, objects, types[offset + 1], Native, "HANDLE")) return false;
            if (member == CloseHandle) return true;
            if (member == GetExitCodeThread) return types[offset + 2] == Native;
            return IsAbiShape(bundle, objects, types[offset + 2], Native, "HANDLE") &&
                IsAbiShape(bundle, objects, types[offset + 3], Native, "HANDLE") &&
                types[offset + 4] == Native && types[offset + 5] == I32 &&
                IsAbiShape(bundle, objects, types[offset + 6], I32, "BOOL") && types[offset + 7] == I32;
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

        private static bool IsDisplayContext(BundleImages bundle, int[] objects, int type) =>
            IsAbiShape(bundle, objects, type, Native, "HDC", "Windows.Win32.Graphics.Gdi");

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
