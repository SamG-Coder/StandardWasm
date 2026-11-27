using PeLibrary;

namespace PortableCompiler
{
    internal static class ScopedFieldReaders
    {
        // A direct getter on the caller's type can supply a span argument when
        // its entire body only reads an instance i32 field. The getter itself
        // remains a call, preserving exceptions from its own field read.
        internal static bool IsInt32(byte[] image, int[] layout, int[] heaps, int caller, int getter)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                getter < 1 || getter > counts[6]) return false;
            int flags = MethodDefinitions.ReadColumn(image, layout, getter, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, getter, 1);
            // Managed IL instance method with no external, forwarded or synchronized implementation.
            if ((flags & 0x2410) != 0 || (implementation & 0x1037) != 0) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, layout, getter, 4), 0, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Int32) ||
                cursor[0] != end) return false;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, getter, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 7 || body[3] != 0 || body[5] != 0)
                return false;
            int start = body[0];
            // ldarg.0; ldfld <FieldDef>; ret
            if (image[start] != 2 || image[start + 1] != 0x7b || image[start + 6] != 0x2a) return false;
            uint token = ImageBytes.U32(image, (uint)(start + 2));
            int field = (int)(token & 0xffffff);
            if (token >> 24 != 4 || field < 1 || field > counts[4]) return false;
            int type = ObjectLayouts.Owner(image, heaps, layout, getter, false);
            if (type < 1 || type != ObjectLayouts.Owner(image, heaps, layout, caller, false) ||
                type != ObjectLayouts.Owner(image, heaps, layout, field, true)) return false;

            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return false;
            var definition = new int[3];
            AssemblyReferences.ReadField(image, layout, table, field, definition);
            int blob = definition[2];
            if ((definition[0] & 0x10) != 0 || blob < 1 || blob >= heaps[1]) return false;
            int limit = heaps[0] + heaps[1];
            int size = ImageBytes.CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int signature = cursor[0];
            return size == 2 && size <= limit - signature && image[signature] == 6 &&
                image[signature + 1] == SignatureReader.Int32;
        }
    }
}
