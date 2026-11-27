using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Named Win32 leaves share the native import table. They do not consume fixed feature bits.
    internal static class Win32NamedBinding
    {
        internal const int Post = Win32ModuleBinding.Count + 1, Peek = Post + 1,
            WindowRect = Peek + 1, ClientRect = WindowRect + 1, DeleteFile = ClientRect + 1, PostWindow = DeleteFile + 1,
            NotifyWinEvent = PostWindow + 1, SendMessageTimeout = NotifyWinEvent + 1,
            BeginPaint = SendMessageTimeout + 1, EndPaint = BeginPaint + 1,
            UpdateRect = EndPaint + 1, ValidateRect = UpdateRect + 1, CoCreateInstance = ValidateRect + 1;
        private const int ImportOwner = -4;

        internal static int Find(BundleImages bundle, byte[] image, int[] heaps, int[] methods, int[] counts,
            int owner, int row)
        {
            var mapping = new int[3];
            if (NativeImports.ForMethod(image, heaps, methods, counts, row, mapping) != 1 ||
                (mapping[0] & ~0x40) != 0x101) return 0;
            if (ImageBytes.MatchesAscii(image, heaps, (uint)mapping[1], "CoCreateInstance"))
                return bundle != null && ComOutputPointer.MatchesActivation(bundle, image, methods, heaps, owner, counts[6], row)
                    ? CoCreateInstance : 0;
            int member = FindEntry(image, heaps, (uint)mapping[2], (uint)mapping[1]);
            if (member == 0) return 0;

            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4),
                Arity(member), cursor);
            if (member == SendMessageTimeout)
            {
                return end >= 0 && Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 &&
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 && ReadUnsigned(image, cursor, end) &&
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x19) >= 0 &&
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 &&
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x09) >= 0 && ReadUnsigned(image, cursor, end) &&
                    SignatureReader.TakeElement(image, cursor, end, 0x0f) &&
                    SignatureReader.TakeElement(image, cursor, end, 0x19) && cursor[0] == end ? member : 0;
            }
            if (member == NotifyWinEvent)
            {
                return mapping[0] == 0x101 && end >= 0 &&
                    SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void) &&
                    ReadUnsigned(image, cursor, end) &&
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 &&
                    SignatureReader.TakeElement(image, cursor, end, 0x08) &&
                    SignatureReader.TakeElement(image, cursor, end, 0x08) && cursor[0] == end ? member : 0;
            }
            // BeginPaint returns an HDC word. The other leaves return a 32-bit BOOL.
            if (end < 0 || Win32ModuleBinding.ReadAbiWord(image, cursor, end, member == BeginPaint ? 0x18 : 0x08) < 0) return 0;
            if (IsPaint(member)) return FindPaint(bundle, image, heaps, methods, owner, member, cursor, end);
            if (member == DeleteFile)
                return SignatureReader.TakeElement(image, cursor, end, 0x0f) &&
                    (SignatureReader.TakeElement(image, cursor, end, 0x07) ||
                    SignatureReader.TakeElement(image, cursor, end, 0x03)) && cursor[0] == end ? DeleteFile : 0;
            if (member == Post || member == PostWindow)
                return (member == Post ? ReadUnsigned(image, cursor, end) :
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0) && ReadUnsigned(image, cursor, end) &&
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x19) >= 0 &&
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 && cursor[0] == end ? member : 0;

            if (member != Peek && Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) < 0) return 0;
            if (!SignatureReader.TakeElement(image, cursor, end, 0x0f) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return 0;
            int pointee = ImageBytes.CompressedUnsigned(image, cursor[0], end, cursor);
            if (pointee < 1) return 0;
            if (member != Peek)
                return cursor[0] == end && Win32ModuleBinding.MatchesRect(bundle, image, heaps, methods, owner, pointee)
                    ? member : 0;
            return Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 &&
                ReadUnsigned(image, cursor, end) && ReadUnsigned(image, cursor, end) &&
                Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x09) >= 0 && cursor[0] == end &&
                MatchesMessage(bundle, image, heaps, methods, owner, pointee) ? Peek : 0;
        }

        private static bool IsPaint(int member) =>
            member == BeginPaint || member == EndPaint || member == UpdateRect || member == ValidateRect;

        // (HWND, PAINTSTRUCT*) for BeginPaint and EndPaint; (HWND, RECT*[, BOOL]) for the region queries.
        private static int FindPaint(BundleImages bundle, byte[] image, int[] heaps, int[] methods, int owner, int member,
            int[] cursor, int end)
        {
            if (Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) < 0 ||
                !SignatureReader.TakeElement(image, cursor, end, 0x0f) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return 0;
            int pointee = ImageBytes.CompressedUnsigned(image, cursor[0], end, cursor);
            if (pointee < 1 || (member == UpdateRect && Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x08) < 0) ||
                cursor[0] != end) return 0;
            bool matches = member == BeginPaint || member == EndPaint
                ? MatchesPaintStruct(bundle, image, heaps, methods, owner, pointee)
                : Win32ModuleBinding.MatchesRect(bundle, image, heaps, methods, owner, pointee);
            return matches ? member : 0;
        }

        private static bool MatchesPaintStruct(BundleImages bundle, byte[] image, int[] heaps, int[] methods, int owner, int pointee)
        {
            // The host writes the whole 64-byte x86 PAINTSTRUCT. Resolve the declared
            // pointee so an int* or a differently laid out struct is diagnosed instead.
            // rgbReserved is a C# fixed buffer (decision 0095).
            var identity = new int[2];
            if (bundle == null || AssemblyBundle.ClassIdentity(bundle, image, methods, heaps, owner, pointee, identity) < 0)
                return false;
            byte[] target = bundle.Get(identity[0]);
            var layout = new int[4];
            var targetHeaps = new int[8];
            var names = new int[2];
            if (CompressedTables.FindWithHeaps(target, layout, targetHeaps) < 0 ||
                ObjectLayouts.TypeDefinitionNames(target, targetHeaps, layout, identity[1], names) < 0 ||
                !ImageBytes.MatchesAscii(target, targetHeaps, (uint)names[0], "PAINTSTRUCT") ||
                !ImageBytes.MatchesAscii(target, targetHeaps, (uint)names[1], "Windows.Win32.Graphics.Gdi"))
                return false;
            var size = new int[2];
            if (NativeValueLayouts.Measure(bundle, identity[0], identity[1], size) != 0 || size[0] != 64 || size[1] != 4)
                return false;
            var shape = new int[6];
            if (ObjectLayouts.ReadShape(target, targetHeaps, layout, identity[1], shape, true) != 0 || shape[2] != 6)
                return false;
            var definition = new int[3];
            var field = new int[2];
            for (int index = 0; index < 6; index++)
            {
                int row = shape[1] + index;
                AssemblyReferences.ReadField(target, layout, shape[0], row, definition);
                string name = index == 0 ? "hdc" : index == 1 ? "fErase" : index == 2 ? "rcPaint" :
                    index == 3 ? "fRestore" : index == 4 ? "fIncUpdate" : "rgbReserved";
                int offset = index == 0 ? 0 : index == 1 ? 4 : index == 2 ? 8 : index == 3 ? 24 : index == 4 ? 28 : 32;
                if ((definition[0] & 0x10) != 0 || !ImageBytes.MatchesAscii(target, targetHeaps, (uint)definition[1], name) ||
                    NativeValueLayouts.Field(bundle, identity[0], identity[1], row, field) != 0 || field[0] != offset)
                    return false;
            }
            return true;
        }

        private static int FindEntry(byte[] image, int[] heaps, uint library, uint entry)
        {
            if (ImageBytes.MatchesAscii(image, heaps, library, "kernel32.dll") ||
                ImageBytes.MatchesAscii(image, heaps, library, "KERNEL32.dll"))
                return ImageBytes.MatchesAscii(image, heaps, entry, "DeleteFileW") ? DeleteFile : 0;
            if (!ImageBytes.MatchesAscii(image, heaps, library, "USER32.dll") &&
                !ImageBytes.MatchesAscii(image, heaps, library, "user32.dll")) return 0;
            return ImageBytes.MatchesAscii(image, heaps, entry, "PostThreadMessageW") ? Post :
                ImageBytes.MatchesAscii(image, heaps, entry, "PostMessageW") ? PostWindow :
                ImageBytes.MatchesAscii(image, heaps, entry, "NotifyWinEvent") ? NotifyWinEvent :
                ImageBytes.MatchesAscii(image, heaps, entry, "SendMessageTimeoutW") ? SendMessageTimeout :
                ImageBytes.MatchesAscii(image, heaps, entry, "PeekMessageW") ? Peek :
                ImageBytes.MatchesAscii(image, heaps, entry, "GetWindowRect") ? WindowRect :
                ImageBytes.MatchesAscii(image, heaps, entry, "GetClientRect") ? ClientRect :
                ImageBytes.MatchesAscii(image, heaps, entry, "BeginPaint") ? BeginPaint :
                ImageBytes.MatchesAscii(image, heaps, entry, "EndPaint") ? EndPaint :
                ImageBytes.MatchesAscii(image, heaps, entry, "GetUpdateRect") ? UpdateRect :
                ImageBytes.MatchesAscii(image, heaps, entry, "ValidateRect") ? ValidateRect : 0;
        }

        private static bool ReadUnsigned(byte[] image, int[] cursor, int end) =>
            SignatureReader.TakeElement(image, cursor, end, 0x09);

        private static bool MatchesMessage(BundleImages bundle, byte[] image, int[] heaps, int[] methods, int owner, int pointee)
        {
            // The Wasm parameter is just i32. Resolve the declared pointee before the host
            // can write a native MSG into the caller's linear memory.
            var identity = new int[2];
            if (bundle == null || AssemblyBundle.ClassIdentity(bundle, image, methods, heaps, owner, pointee, identity) < 0)
                return false;
            byte[] target = bundle.Get(identity[0]);
            var layout = new int[4];
            var targetHeaps = new int[8];
            var names = new int[2];
            if (CompressedTables.FindWithHeaps(target, layout, targetHeaps) < 0 ||
                ObjectLayouts.TypeDefinitionNames(target, targetHeaps, layout, identity[1], names) < 0 ||
                !ImageBytes.MatchesAscii(target, targetHeaps, (uint)names[0], "MSG") ||
                !ImageBytes.MatchesAscii(target, targetHeaps, (uint)names[1], "Windows.Win32.UI.WindowsAndMessaging"))
                return false;
            var size = new int[2];
            if (NativeValueLayouts.Measure(bundle, identity[0], identity[1], size) != 0 || size[0] != 28 || size[1] != 4)
                return false;
            var shape = new int[6];
            if (ObjectLayouts.ReadShape(target, targetHeaps, layout, identity[1], shape, true) != 0 || shape[2] != 6)
                return false;
            var definition = new int[3];
            var field = new int[2];
            for (int index = 0; index < 6; index++)
            {
                int row = shape[1] + index;
                AssemblyReferences.ReadField(target, layout, shape[0], row, definition);
                string name = index == 0 ? "hwnd" : index == 1 ? "message" : index == 2 ? "wParam" :
                    index == 3 ? "lParam" : index == 4 ? "time" : "pt";
                if ((definition[0] & 0x10) != 0 || !ImageBytes.MatchesAscii(target, targetHeaps, (uint)definition[1], name) ||
                    NativeValueLayouts.Field(bundle, identity[0], identity[1], row, field) != 0 || field[0] != index * 4)
                    return false;
                if (index == 1 || index == 4)
                {
                    if (field[1] != 0x4b) return false; // unsigned 32-bit field
                }
                else if (index == 5 ? field[1] != 0 : field[1] != 0 && field[1] != 0x4d)
                    return false; // point aggregate or pointer-sized word
            }
            return true;
        }

        internal static int Arity(int member) => member == Post || member == PostWindow || member == NotifyWinEvent ? 4 :
            member == SendMessageTimeout ? 7 : member == Peek || member == CoCreateInstance ? 5 :
            member == DeleteFile ? 1 : member == UpdateRect ? 3 : 2;
        internal static bool NeedsMemory(int member) => member != Post && member != PostWindow && member != NotifyWinEvent;

        internal static bool MatchesSignature(BundleImages bundle, int[] objects, int member, int[] types, int offset)
        {
            if (member == CoCreateInstance)
                return Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset], I32, "HRESULT") &&
                    types[offset + 1] == Native && types[offset + 2] == Native &&
                    Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 3], I32, "CLSCTX", "Windows.Win32.System.Com") &&
                    types[offset + 4] == Native && types[offset + 5] == Native;
            if (member == SendMessageTimeout)
                return Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset], Native, "LRESULT") &&
                    Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == I32 &&
                    Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 3], Native, "WPARAM") &&
                    Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 4], Native, "LPARAM") &&
                    types[offset + 5] == I32 && types[offset + 6] == I32 && types[offset + 7] == Native;
            if (member == NotifyWinEvent)
                return types[offset] == Void && types[offset + 1] == I32 &&
                    Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 2], Native, "HWND") &&
                    types[offset + 3] == I32 && types[offset + 4] == I32;
            if (member == BeginPaint)
                return Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset], Native, "HDC", "Windows.Win32.Graphics.Gdi") &&
                    Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") && types[offset + 2] == Native;
            if (!Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset], I32, "BOOL")) return false;
            if (member == UpdateRect)
                return Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == Native && Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 3], I32, "BOOL");
            if (member == DeleteFile) return types[offset + 1] == Native;
            if (member == Post || member == PostWindow)
                return (member == Post ? types[offset + 1] == I32 :
                    Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND")) && types[offset + 2] == I32 &&
                    Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 3], Native, "WPARAM") &&
                    Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 4], Native, "LPARAM");
            if (member != Peek)
                return Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 1], Native, "HWND") &&
                    types[offset + 2] == Native;
            return types[offset + 1] == Native &&
                Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset + 2], Native, "HWND") &&
                types[offset + 3] == I32 && types[offset + 4] == I32 && types[offset + 5] == I32;
        }

        internal static bool CapturesLastError(byte[] image, int[] heaps, int[] methods, int row)
        {
            var counts = new int[45];
            var mapping = new int[3];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                NativeImports.ForMethod(image, heaps, methods, counts, row, mapping) == 1 && (mapping[0] & 0x40) != 0;
        }

        internal static int Register(NativeImportTable imports, int member)
        {
            int existing = imports.Find(ImportOwner, member);
            if (existing >= 0) return existing;
            byte[] module = NativeImportTable.Ascii(member == CoCreateInstance ? "standardwasm:com" : "standardwasm:win32");
            byte[] name = NativeImportTable.Ascii(member == Post ? "postThreadMessageW" :
                member == PostWindow ? "postMessageW" : member == Peek ? "peekMessageW" :
                member == NotifyWinEvent ? "notifyWinEvent" :
                member == SendMessageTimeout ? "sendMessageTimeoutW" :
                member == WindowRect ? "getWindowRect" : member == ClientRect ? "getClientRect" :
                member == BeginPaint ? "beginPaint" : member == EndPaint ? "endPaint" :
                member == UpdateRect ? "getUpdateRect" : member == ValidateRect ? "validateRect" :
                member == CoCreateInstance ? "coCreateInstance" : "deleteFileW");
            var types = new byte[Arity(member)];
            for (int i = 0; i < types.Length; i++) types[i] = 0x7f;
            return imports.Add(ImportOwner, member, module, module.Length, name, name.Length, types, types.Length,
                member == NotifyWinEvent ? (byte)0 : (byte)0x7f);
        }
    }
}
