using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    // dotnet/runtime builds System.Private.CoreLib once per target with
    // TARGET_32BIT or TARGET_64BIT, so its method bodies carry the target's
    // native width as IL constants. In the 10.0.10 packs, x64 IntPtr.get_Size is
    // `ldc.i4 8; ret` and x64 IntPtr.get_MaxValue is `ldc.i8 0x7fff...; conv.i`.
    //
    // The PE header doesn't identify that width. The browser-wasm CoreLib is an
    // IL-only PE32 image with the I386 machine value, the same header as any
    // AnyCPU library. A CoreLib's own IntPtr.get_Size body is therefore the
    // evidence for the width that all of its bodies were built for.
    internal static class CoreLibNativeWidth
    {
        internal const int Malformed = -1, Unrecognized = -2, NotCoreLib = 0;

        // Returns 4 or 8 for a System.Private.CoreLib definition, NotCoreLib for
        // any other image, and Unrecognized when the CoreLib has no single public
        // System.IntPtr.get_Size that returns a constant 4 or 8.
        internal static int Read(byte[] image, int[] layout, int[] heaps)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return Malformed;
            if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return NotCoreLib;

            int width = 0;
            for (int row = 1; row <= counts[6]; row++)
            {
                uint name = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 3));
                // get_Size
                if (!PlatformTypeReferences.Named(image, (uint)heaps[2], (uint)heaps[3], name, 8,
                        0x5f746567, 0x657a6953, 0))
                    continue;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1) return Malformed;
                if (!IsSystemIntPtr(image, heaps, layout, counts, first, owner)) continue;
                // A second definition would make the evidence ambiguous.
                if (width != 0) return Unrecognized;
                // Public, static and parameterless with an int32 result.
                int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
                if ((flags & 0x17) != 0x16 || !ReturnsInt32WithoutParameters(image, heaps, layout, row))
                    return Unrecognized;
                width = ConstantBody(image, unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0)));
                if (width < 0) return width;
            }
            return width == 0 ? Unrecognized : width;
        }

        private static bool IsSystemIntPtr(byte[] image, int[] heaps, int[] layout, int[] counts, int first, int row)
        {
            int strings = layout[2];
            int guids = (image[heaps[4] + 6] & 2) == 0 ? 2 : 4;
            int scope = counts[0] < 16384 && counts[26] < 16384 && counts[35] < 16384 && counts[1] < 16384 ? 2 : 4;
            int extends = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4;
            int defs = first + counts[0] * (2 + strings + 3 * guids) + counts[1] * (scope + 2 * strings);
            int width = 4 + 2 * strings + extends + (counts[4] < 65536 ? 2 : 4) + (counts[6] < 65536 ? 2 : 4);
            uint at = (uint)(defs + (row - 1) * width);
            // Visibility 1 is public and top-level, which excludes nested types.
            if ((U32(image, at) & 7) != 1) return false;
            uint name = strings == 2 ? U16(image, at + 4) : U32(image, at + 4);
            uint space = strings == 2 ? U16(image, at + 4 + (uint)strings) : U32(image, at + 4 + (uint)strings);
            uint heap = (uint)heaps[2], size = (uint)heaps[3];
            return PlatformTypeReferences.Named(image, heap, size, name, 6, 0x50746e49, 0x7274, 0) && // IntPtr
                PlatformTypeReferences.Named(image, heap, size, space, 6, 0x74737953, 0x6d65, 0); // System
        }

        // MethodDefSig: DEFAULT (0x00), zero parameters, ELEMENT_TYPE_I4.
        private static bool ReturnsInt32WithoutParameters(byte[] image, int[] heaps, int[] layout, int row)
        {
            uint blob = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 4));
            if (blob == 0 || !Fits(blob, 4, (uint)heaps[1])) return false;
            uint at = (uint)heaps[0] + blob;
            return image[at] == 3 && image[at + 1] == 0 && image[at + 2] == 0 && image[at + 3] == 8;
        }

        // Accept only `ldc.i4.4|ldc.i4.8|ldc.i4.s N|ldc.i4 N; ret` without
        // locals or exception sections, and only for N equal to 4 or 8.
        private static int ConstantBody(byte[] image, uint rva)
        {
            var body = new int[6];
            int status = MethodBodies.Read(image, rva, body);
            if (status == MethodBodies.Malformed) return Malformed;
            if (status != 0 || body[3] != 0 || body[5] != 0) return Unrecognized;
            int code = body[0], length = body[1];
            int value = -1;
            if (length == 2 && image[code] == 0x1a) value = 4;
            else if (length == 2 && image[code] == 0x1e) value = 8;
            else if (length == 3 && image[code] == 0x1f) value = image[code + 1];
            else if (length == 6 && image[code] == 0x20) value = unchecked((int)U32(image, (uint)code + 1));
            if (value < 0 || image[code + length - 1] != 0x2a) return Unrecognized;
            return value == 4 || value == 8 ? value : Unrecognized;
        }
    }
}
