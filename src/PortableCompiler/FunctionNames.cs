using PeLibrary;

namespace PortableCompiler
{
    // An opt-in Wasm "name" custom section (function names as "Type::Method") for profiling the
    // generated code. Off by default, so compiled modules stay byte-identical; a profiling tool
    // sets EmitNames before compiling. Names carry no semantics.
    internal static class FunctionNames
    {
        internal static bool EmitNames;

        // Appends the section after the module's `size` bytes; returns the new size, or -3.
        internal static int Append(byte[] output, int size, BundleImages bundle, int[] owners, int[] rows,
            int functions, int imported)
        {
            var body = new byte[1 << 22];
            int used = 0, count = 0;
            var counts = new int[45];
            var names = new int[2];
            var layout = new int[4];
            var heaps = new int[8];
            int active = -1;
            byte[] image = null;
            int entries = 0;
            for (int f = 0; f < functions; f++)
                if (rows[f] > 0 && owners[f] >= 0 && owners[f] < bundle.Count) entries++;
            used = WasmBinary.Unsigned(body, used, entries);
            for (int f = 0; f < functions; f++)
            {
                int owner = owners[f], row = rows[f];
                if (row <= 0 || owner < 0 || owner >= bundle.Count) continue;
                if (owner != active)
                {
                    image = bundle.Get(owner);
                    if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return size;
                    if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return size;
                    active = owner;
                }
                if (used > body.Length - 600) return size;
                used = WasmBinary.Unsigned(body, used, imported + f);
                int start = used + 2; // room for a two-byte length
                int at = start;
                int type = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (type > 0 && ObjectLayouts.TypeDefinitionNames(image, heaps, layout, type, names) >= 0)
                    at = Text(body, at, image, heaps, names[0]);
                body[at++] = (byte)':';
                body[at++] = (byte)':';
                at = Text(body, at, image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 3));
                int length = at - start;
                if (length > 0x3fff) return size;
                body[used] = (byte)(0x80 | length & 0x7f);
                body[used + 1] = (byte)(length >> 7);
                used = at;
                count++;
            }
            // custom section: id 0, size, "name", subsection 1 (function names), size, map
            int subsection = used;
            int total = 1 + 4 + 1 + WasmBinary.UnsignedSize(subsection) + subsection;
            if (output.Length - size < 1 + WasmBinary.UnsignedSize(total) + total) return -3;
            int cursor = size;
            output[cursor++] = 0;
            cursor = WasmBinary.Unsigned(output, cursor, total);
            output[cursor++] = 4;
            output[cursor++] = (byte)'n'; output[cursor++] = (byte)'a'; output[cursor++] = (byte)'m'; output[cursor++] = (byte)'e';
            output[cursor++] = 1;
            cursor = WasmBinary.Unsigned(output, cursor, subsection);
            for (int i = 0; i < used; i++) output[cursor++] = body[i];
            return cursor;
        }

        // Copies the #Strings entry at `index` (up to 200 bytes).
        private static int Text(byte[] body, int at, byte[] image, int[] heaps, int index)
        {
            if (index <= 0 || index >= heaps[3]) return at;
            int from = heaps[2] + index;
            for (int i = 0; i < 200 && image[from + i] != 0; i++) body[at++] = image[from + i];
            return at;
        }
    }
}
