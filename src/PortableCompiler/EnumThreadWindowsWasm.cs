using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Stage 361: USER32.EnumThreadWindows(DWORD, WNDENUMPROC, LPARAM) -> BOOL.
    // The callback crosses as a decision 0097 token; the host enumerates the launch's
    // windows and calls the guest's entry once per HWND with the unmodified LPARAM.
    internal static class EnumThreadWindowsWasm
    {
        internal const int Capacity = 64;
        internal const int CallbackParameters = 2;

        // BOOL EnumThreadWindows(uint, delegate* unmanaged[Stdcall]<HWND, LPARAM, BOOL>, LPARAM), raw words.
        internal static bool MatchesSignature(byte[] image, int[] heaps, int[] layout, int row)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 3, cursor);
            if (end < 0 || Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x08) < 0 ||
                !SignatureReader.TakeElement(image, cursor, end, 0x09) ||
                !UnmanagedCallbackSignatures.ReadHeader(image, cursor, end, CallbackParameters)) return false;
            return Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x08) >= 0 &&
                Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 &&
                Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 &&
                Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 && cursor[0] == end;
        }

        // The callback's storage types, result first, checked as WNDENUMPROC.
        internal static bool ReadCallbackTypes(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int row, int[] objects, int[] types)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 3, cursor);
            if (end < 0 || Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x08) < 0 ||
                !SignatureReader.TakeElement(image, cursor, end, 0x09) ||
                !UnmanagedCallbackSignatures.ReadHeader(image, cursor, end, CallbackParameters) ||
                !UnmanagedCallbackSignatures.ReadTypes(bundle, image, layout, heaps, owner, cursor, end, objects, types,
                    CallbackParameters)) return false;
            return Win32ModuleBinding.IsAbiShape(bundle, objects, types[0], I32, "BOOL") &&
                Win32ModuleBinding.IsAbiShape(bundle, objects, types[1], Native, "HWND") &&
                Win32ModuleBinding.IsAbiShape(bundle, objects, types[2], Native, "LPARAM");
        }

        // enumThreadWindows(thread: i32, target: i32, entry: funcref, payload: i32) -> BOOL i32
        internal static int Register(NativeImportTable imports, int member)
        {
            byte[] module = NativeImportTable.Ascii("standardwasm:win32");
            byte[] name = NativeImportTable.Ascii("enumThreadWindows");
            return imports.Add(-5, member, module, module.Length, name, name.Length,
                new byte[] { 0x7f, 0x7f, 0x70, 0x7f }, 4, 0x7f);
        }

        // Locals: 0 = thread, 1 = callback slot (Native), 2 = LPARAM. types[shape..] is the P/Invoke signature.
        internal static int WriteBody(byte[] code, int used, int[] types, int shape, int import, int entrySlot, int initializer)
        {
            code[used++] = 0; // no locals
            if (initializer >= 0)
            {
                code[used++] = 0x10;
                used = WasmBinary.Unsigned(code, used, initializer);
            }
            used = Local(code, used, 0x20, 0);
            used = Local(code, used, 0x20, 1);
            used = UnmanagedCallbackWasm.LoadEntry(code, used, entrySlot);
            used = UnmanagedCallbackWasm.ReadWord(code, used, types[shape + 3], 2);
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, import);
            if (types[shape] != I32) used = Gc(code, used, 0, types[shape]); // struct.new BOOL
            code[used++] = 0x0b;
            return used;
        }
    }
}
