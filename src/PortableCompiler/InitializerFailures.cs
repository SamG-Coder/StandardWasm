using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Issue #170: a managed exception escaping a type initializer becomes the bundled CoreLib's own
    // TypeInitializationException, built through its internal
    //   .ctor(string fullTypeName, string message, Exception innerException)
    // which avoids CoreLib's resource formatting. The message is the text .NET 10 prints:
    //   The type initializer for '<fullTypeName>' threw an exception.
    // fullTypeName is Type.FullName: Namespace.Outer+Inner. A bundle without CoreLib keeps the
    // Stage 129 trap (decision 0057).
    internal static class InitializerFailures
    {
        // found = (bundle index, MethodDef row) of that constructor, or (-1, 0).
        internal static int FindConstructor(BundleImages bundle, int[] found)
        {
            found[0] = -1;
            found[1] = 0;
            for (int owner = 0; owner < bundle.Count; owner++)
            {
                // Fresh tables per image: ReadCounts leaves absent tables untouched, so counts
                // reused from a previous image would misidentify the CoreLib definition.
                var layout = new int[4];
                var heaps = new int[8];
                var counts = new int[45];
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
                if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) continue;
                int type = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System", "TypeInitializationException");
                if (type == 0) return 0;
                var range = new int[2];
                if (ObjectLayouts.MethodRange(image, heaps, layout, type, range) < 0) return 0;
                for (int method = range[0]; method < range[1]; method++)
                {
                    if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), ".ctor")) continue;
                    // instance void .ctor(string, string, class System.Exception)
                    int blob = MethodDefinitions.ReadColumn(image, layout, method, 4);
                    if (blob <= 0 || blob >= heaps[1]) continue;
                    int at = heaps[0] + blob;
                    if (image[at] >= 7 && image[at + 1] == 0x20 && image[at + 2] == 3 && image[at + 3] == 1 &&
                        image[at + 4] == 0x0e && image[at + 5] == 0x0e && image[at + 6] == 0x12)
                    {
                        found[0] = owner;
                        found[1] = method;
                        return 0;
                    }
                }
                return 0;
            }
            return 0;
        }

        // The MethodDef row of `instance void .ctor()` on TypeDef row type, or 0.
        internal static int FindDefaultConstructor(byte[] image, int[] heaps, int[] layout, int type)
        {
            var range = new int[2];
            if (ObjectLayouts.MethodRange(image, heaps, layout, type, range) < 0) return 0;
            for (int method = range[0]; method < range[1]; method++)
            {
                if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), ".ctor")) continue;
                int blob = MethodDefinitions.ReadColumn(image, layout, method, 4);
                if (blob <= 0 || blob >= heaps[1]) continue;
                int at = heaps[0] + blob;
                if (image[at] == 3 && image[at + 1] == 0x20 && image[at + 2] == 0 && image[at + 3] == 1) return method;
            }
            return 0;
        }

        // Writes the name the CLR gives a failing initializer's type (TypeInitializationException.TypeName,
        // also quoted in its Message) as UTF-16 into literals at start: the TypeDef's own namespace and
        // name, "Namespace.Name", or just "Name" with no namespace. A nested type has no namespace of its
        // own and the CLR does not prefix its enclosing types (Outer+Inner reports "Inner"). Returns the
        // code-unit count, or a negative status.
        internal static int WriteInitializerName(byte[] image, int[] heaps, int[] layout, int typeRow, byte[] literals, int start)
        {
            var names = new int[2];
            if (ObjectLayouts.TypeDefinitionNames(image, heaps, layout, typeRow, names) < 0) return -1;
            int used = start;
            if (names[1] != 0)
            {
                // Copy validates the heap index; a valid empty namespace writes nothing and gets no dot.
                int named = Copy(image, heaps, (uint)names[1], literals, used);
                if (named < 0) return named;
                if (named != used) used = Put(literals, named, '.');
                if (used < 0) return used;
            }
            used = Copy(image, heaps, (uint)names[0], literals, used);
            if (used < 0) return used;
            return (used - start) / 2;
        }

        // Writes prefix + the code units at [nameStart, nameStart + 2 * nameLength) + suffix.
        internal static int WriteMessage(byte[] literals, int start, int nameStart, int nameLength)
        {
            const string prefix = "The type initializer for '";
            const string suffix = "' threw an exception.";
            int used = start;
            for (int i = 0; i < prefix.Length && used >= 0; i++) used = Put(literals, used, prefix[i]);
            for (int i = 0; i < nameLength && used >= 0; i++)
                used = Put(literals, used, literals[nameStart + 2 * i] | (literals[nameStart + 2 * i + 1] << 8));
            for (int i = 0; i < suffix.Length && used >= 0; i++) used = Put(literals, used, suffix[i]);
            return used < 0 ? used : (used - start) / 2;
        }

        // Appends the #Strings entry at index (UTF-8, NUL-terminated within the heap) as UTF-16.
        private static int Copy(byte[] image, int[] heaps, uint index, byte[] literals, int used)
        {
            uint limit = (uint)heaps[3];
            if (index >= limit) return -1;
            int start = heaps[2] + (int)index, end = start;
            while (end < heaps[2] + (int)limit && image[end] != 0) end++;
            if (end == heaps[2] + (int)limit) return -1; // unterminated entry
            int bytes = Utf8Scalars.CopyUtf16(image, start, end - start, literals, used);
            return bytes < 0 ? bytes : used + bytes;
        }

        private static int Put(byte[] literals, int used, int unit)
        {
            if (used < 0 || used > literals.Length - 2) return -3;
            literals[used] = (byte)unit;
            literals[used + 1] = (byte)(unit >> 8);
            return used + 2;
        }
    }
}
