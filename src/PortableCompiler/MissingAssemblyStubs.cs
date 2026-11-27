using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Issue #299 (opt in): methods whose bodies need an assembly missing from the bundle. The
    // CLR's JIT fails such a method, and the call site throws
    //   System.IO.FileNotFoundException: Could not load file or assembly '<display name>'.
    //   The system cannot find the file specified.
    // on every call; handlers inside the method never run (CLR probes 1-3 on #299). A stub's
    // body is the synthetic sequence ldstr message; ldstr fileName; newobj
    // FileNotFoundException::.ctor(string, string); throw, using the bundled CoreLib's own
    // exception class. Without a bindable CoreLib the located -4 diagnostic remains.
    internal sealed class MissingAssemblyStubs
    {
        internal int Count;
        internal int[] Owners = new int[8], Rows = new int[8];
        // The missing assembly's display name per stub, as UTF-16 code units in one pool.
        internal int[] NameStart = new int[8], NameLength = new int[8];
        internal int[] Units = new int[256];
        internal int UnitCount;

        internal int Find(int owner, int row)
        {
            for (int i = 0; i < Count; i++)
                if (Owners[i] == owner && Rows[i] == row) return i;
            return -1;
        }

        // Records the stub for (owner, row) and the display name of bundle's latest missing
        // AssemblyRef. Returns false when that name cannot be formatted exactly (a full public
        // key would need its SHA-1 token), so the caller keeps the strict diagnostic.
        internal bool Add(int owner, int row, BundleImages bundle)
        {
            byte[] image = bundle.MissingImage;
            int[] reference = bundle.MissingReference;
            if (image == null || reference == null || reference[AssemblyReferences.FullKey] != 0) return false;
            if (Count == Owners.Length)
            {
                Owners = Grow(Owners); Rows = Grow(Rows); NameStart = Grow(NameStart); NameLength = Grow(NameLength);
            }
            int start = UnitCount;
            int[] heaps = bundle.MissingHeaps;
            // Name
            uint name = (uint)reference[1];
            uint at = (uint)heaps[2] + name;
            while (at < image.Length && image[at] != 0) Append(image[at++]);
            Text(", Version=");
            uint version = (uint)reference[0];
            for (int part = 0; part < 4; part++)
            {
                if (part > 0) Append('.');
                Number((int)U16(image, version + (uint)(2 * part)));
            }
            Text(", Culture=neutral, PublicKeyToken=");
            int keyLength = reference[AssemblyReferences.KeyLength];
            if (keyLength == 0) Text("null");
            else
            {
                const string digits = "0123456789abcdef";
                int key = reference[AssemblyReferences.KeyStart];
                for (int i = 0; i < keyLength; i++)
                {
                    Append(digits[image[key + i] >> 4]);
                    Append(digits[image[key + i] & 15]);
                }
            }
            Owners[Count] = owner;
            Rows[Count] = row;
            NameStart[Count] = start;
            NameLength[Count] = UnitCount - start;
            Count++;
            return true;
        }

        // Writes the CLR's message (prefix + name + suffix) or only the name (the FileName
        // argument) into the module literal pool. Returns the code-unit count, or -3.
        internal int WriteLiteral(int stub, bool message, byte[] literals, int start)
        {
            const string prefix = "Could not load file or assembly '";
            const string suffix = "'. The system cannot find the file specified.";
            int length = NameLength[stub] + (message ? prefix.Length + suffix.Length : 0);
            if (start < 0 || length > (literals.Length - start) / 2) return -3;
            int used = start;
            if (message)
                for (int i = 0; i < prefix.Length; i++) used = WriteUnit(literals, used, prefix[i]);
            for (int i = 0; i < NameLength[stub]; i++) used = WriteUnit(literals, used, Units[NameStart[stub] + i]);
            if (message)
                for (int i = 0; i < suffix.Length; i++) used = WriteUnit(literals, used, suffix[i]);
            return length;
        }

        // The bundle index and MethodDef row of CoreLib's FileNotFoundException(string, string),
        // or (-1, 0) when the bundle has no such CoreLib.
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
                int type = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System.IO", "FileNotFoundException");
                if (type == 0) return 0;
                var range = new int[2];
                if (ObjectLayouts.MethodRange(image, heaps, layout, type, range) < 0) return 0;
                for (int method = range[0]; method < range[1]; method++)
                {
                    if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), ".ctor")) continue;
                    // instance void .ctor(string message, string fileName)
                    int blob = MethodDefinitions.ReadColumn(image, layout, method, 4);
                    if (blob <= 0 || blob >= heaps[1]) continue;
                    int at = heaps[0] + blob;
                    if (image[at] == 5 && image[at + 1] == 0x20 && image[at + 2] == 2 && image[at + 3] == 1 &&
                        image[at + 4] == 0x0e && image[at + 5] == 0x0e)
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

        // Leave a non-instruction slot after each string load for its
        // intern-cache index, just as the operand bytes do for ordinary IL.
        internal static void Records(int[] records, int constructor)
        {
            int w = BranchIl.Width;
            records[0] = BranchIl.LoadString;
            records[2] = 2;
            records[2 * w] = BranchIl.LoadString;
            records[2 * w + 2] = 4;
            records[4 * w] = BranchIl.NewObject;
            records[4 * w + 1] = constructor;
            records[4 * w + 2] = 5;
            records[5 * w] = BranchIl.ThrowNull;
            records[5 * w + 2] = 6;
        }

        private void Text(string text)
        {
            for (int i = 0; i < text.Length; i++) Append(text[i]);
        }

        private void Number(int value)
        {
            if (value >= 10) Number(value / 10);
            Append('0' + value % 10);
        }

        private void Append(int unit)
        {
            if (UnitCount == Units.Length) Units = Grow(Units);
            Units[UnitCount++] = unit;
        }

        private static int WriteUnit(byte[] literals, int used, int unit)
        {
            literals[used] = (byte)unit;
            literals[used + 1] = (byte)(unit >> 8);
            return used + 2;
        }

        private static int[] Grow(int[] values)
        {
            var grown = new int[values.Length * 2];
            for (int i = 0; i < values.Length; i++) grown[i] = values[i];
            return grown;
        }
    }
}
