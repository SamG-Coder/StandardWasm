using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Named Win32 leaves share the native import table. They do not consume fixed feature bits.
    internal static class Win32NamedBinding
    {
        internal const int Post = Win32ModuleBinding.Count + 1, Peek = Post + 1,
            WindowRect = Peek + 1, ClientRect = WindowRect + 1;
        private const int ImportOwner = -4;

        internal static int Find(BundleImages bundle, byte[] image, int[] heaps, int[] methods, int[] counts,
            int owner, int row)
        {
            var mapping = new int[3];
            if (NativeImports.ForMethod(image, heaps, methods, counts, row, mapping) != 1 ||
                (mapping[0] & ~0x40) != 0x101) return 0;
            uint library = (uint)mapping[2];
            if (!ImageBytes.MatchesAscii(image, heaps, library, "USER32.dll") &&
                !ImageBytes.MatchesAscii(image, heaps, library, "user32.dll")) return 0;
            uint entry = (uint)mapping[1];
            int member = ImageBytes.MatchesAscii(image, heaps, entry, "PostThreadMessageW") ? Post :
                ImageBytes.MatchesAscii(image, heaps, entry, "PeekMessageW") ? Peek :
                ImageBytes.MatchesAscii(image, heaps, entry, "GetWindowRect") ? WindowRect :
                ImageBytes.MatchesAscii(image, heaps, entry, "GetClientRect") ? ClientRect : 0;
            if (member == 0) return 0;

            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4),
                Arity(member), cursor);
            if (end < 0 || Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x08) < 0) return 0;
            if (member == Post)
                return ReadUnsigned(image, cursor, end) && ReadUnsigned(image, cursor, end) &&
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x19) >= 0 &&
                    Win32ModuleBinding.ReadAbiWord(image, cursor, end, 0x18) >= 0 && cursor[0] == end ? Post : 0;

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

        internal static int Arity(int member) => member == Post ? 4 : member == Peek ? 5 : 2;
        internal static bool NeedsMemory(int member) => member != Post;

        internal static bool MatchesSignature(BundleImages bundle, int[] objects, int member, int[] types, int offset)
        {
            if (!Win32ModuleBinding.IsAbiShape(bundle, objects, types[offset], I32, "BOOL")) return false;
            if (member == Post)
                return types[offset + 1] == I32 && types[offset + 2] == I32 &&
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
            byte[] module = NativeImportTable.Ascii("standardwasm:win32");
            byte[] name = NativeImportTable.Ascii(member == Post ? "postThreadMessageW" : member == Peek ? "peekMessageW" :
                member == WindowRect ? "getWindowRect" : "getClientRect");
            var types = new byte[Arity(member)];
            for (int i = 0; i < types.Length; i++) types[i] = 0x7f;
            return imports.Add(ImportOwner, member, module, module.Length, name, name.Length, types, types.Length, 0x7f);
        }
    }
}
