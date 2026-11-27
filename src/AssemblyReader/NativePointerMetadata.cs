using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class NativePointerMetadata
    {
        internal static bool IsPointerConstructor(byte[] image, int[] heaps, int[] methods, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (!ReadPlatformPointerMember(image, heaps, methods, row, reference, ".ctor"))
                return false;
            uint blob = (uint)reference[5];
            if (!Fits(blob, 6, (uint)heaps[1])) return false;
            uint at = (uint)heaps[0] + blob;
            // Exact instance .ctor(void*) signature. Other constructors perform numeric conversions.
            return U32(image, at) == 0x01012005 && U16(image, at + 4) == 0x010f;
        }

        internal static bool IsPointerAccessor(byte[] image, int[] heaps, int[] methods, int row, bool member)
        {
            uint blob;
            if (member)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (!ReadPlatformPointerMember(image, heaps, methods, row, reference, "ToPointer")) return false;
                blob = (uint)reference[5];
            }
            else
            {
                if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, methods, row, 3), "ToPointer") ||
                    (MethodDefinitions.ReadColumn(image, methods, row, 2) & 0x57) != 6) return false;
                int owner = ObjectLayouts.Owner(image, heaps, methods, row, false);
                if (!NativeStorage.IsIntegerElement(ArrayTypeRefs.PrimitiveElement(image, heaps, methods, owner << 2)))
                    return false;
                blob = (uint)MethodDefinitions.ReadColumn(image, methods, row, 4);
            }
            // Instance void* ToPointer(), with zero explicit arguments.
            return blob != 0 && Fits(blob, 5, (uint)heaps[1]) &&
                U32(image, (uint)heaps[0] + blob) == 0x0f002004 && image[heaps[0] + (int)blob + 4] == 1;
        }

        private static bool ReadPlatformPointerMember(byte[] image, int[] heaps, int[] methods, int row,
            int[] reference, string name)
        {
            if (AssemblyReferences.ReadMember(image, heaps, methods, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[3], "System") ||
                !MatchesAscii(image, heaps, (uint)reference[4], name)) return false;
            uint owner = (uint)reference[2];
            if (!MatchesAscii(image, heaps, owner, "IntPtr") && !MatchesAscii(image, heaps, owner, "UIntPtr"))
                return false;
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, methods, counts, reference[AssemblyReferences.AssemblyRow]);
        }
    }
}
