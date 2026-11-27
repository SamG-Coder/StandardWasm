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
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            for (int owner = 0; owner < bundle.Count; owner++)
            {
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

        // Writes Type.FullName of TypeDef row typeRow (Namespace.Outer+Inner) as UTF-16 into
        // literals at start. Returns the code-unit count, or a negative status.
        internal static int WriteFullName(byte[] image, int[] heaps, int[] layout, int typeRow, byte[] literals, int start)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            // Collect the chain from the outermost type inward (nesting is shallow).
            var chain = new int[16];
            int depth = 0;
            for (int row = typeRow; row != 0; row = NestedTypes.Enclosing(image, heaps, layout, counts, row))
            {
                if (depth == chain.Length) return -2;
                chain[depth++] = row;
            }
            var names = new int[2];
            int used = start;
            for (int level = depth - 1; level >= 0; level--)
            {
                if (ObjectLayouts.TypeDefinitionNames(image, heaps, layout, chain[level], names) < 0) return -1;
                if (level == depth - 1 && names[1] != 0)
                {
                    used = Copy(image, heaps, (uint)names[1], literals, used);
                    if (used < 0) return used;
                    used = Put(literals, used, '.');
                }
                else if (level != depth - 1) used = Put(literals, used, '+');
                if (used < 0) return used;
                used = Copy(image, heaps, (uint)names[0], literals, used);
                if (used < 0) return used;
            }
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

        private static int Copy(byte[] image, int[] heaps, uint index, byte[] literals, int used)
        {
            uint at = (uint)heaps[2] + index;
            while (at < image.Length && image[at] != 0)
            {
                // A non-ASCII UTF-8 name is not transcoded here; the guard then keeps its trap.
                if (image[at] >= 0x80) return -2;
                used = Put(literals, used, image[at++]);
                if (used < 0) return used;
            }
            return used;
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
