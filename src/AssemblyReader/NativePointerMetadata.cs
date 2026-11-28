using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class NativePointerMetadata
    {
        internal const int IdentityConstructor = 1;

        internal static int ConstructorOperation(byte[] image, int[] heaps, int[] methods, int row, bool member)
        {
            if (member)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (!ReadPlatformPointerMember(image, heaps, methods, row, reference, ".ctor")) return 0;
                uint blob = (uint)reference[5];
                uint at = (uint)heaps[0] + blob;
                // An exact void* constructor already has native width.
                if (Fits(blob, 6, (uint)heaps[1]) && U32(image, at) == 0x01012005 && U16(image, at + 4) == 0x010f)
                    return IdentityConstructor;

                int parameter = WideConstructorParameter(image, heaps, blob);
                bool unsigned = MatchesAscii(image, heaps, (uint)reference[2], "UIntPtr");
                return ConversionOperation(parameter, unsigned ? 0x19 : 0x18);
            }

            int wide = WideConstructorParameter(image, heaps, (uint)MethodDefinitions.ReadColumn(image, methods, row, 4));
            if (wide == 0 || MethodDefinitions.ReadColumn(image, methods, row, 1) != 0 ||
                (MethodDefinitions.ReadColumn(image, methods, row, 2) & 0x1810) != 0x1800 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, methods, row, 3), ".ctor"))
                return 0;
            int owner = ObjectLayouts.Owner(image, heaps, methods, row, false);
            if (owner < 1) return 0;
            int element = ArrayTypeRefs.PrimitiveElement(image, heaps, methods, owner << 2);
            int conversion = ConversionOperation(wide, element);
            if (conversion == 0) return 0;

            // The original CoreLib constructor stores only the checked native
            // conversion into its native storage word. Verify the actual body.
            var body = new int[6];
            uint rva = (uint)MethodDefinitions.ReadColumn(image, methods, row, 0);
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 9 || body[2] < 2 || body[3] != 0 || body[5] != 0)
                return 0;
            uint start = (uint)body[0];
            if (U16(image, start) != 0x0302 || image[start + 2] != conversion || image[start + 3] != 0x7d ||
                image[start + 8] != 0x2a) return 0;
            uint fieldToken = U32(image, start + 4);
            int fieldRow = (int)(fieldToken & 0xffffff);
            if (fieldToken >> 24 != 4 || ObjectLayouts.Owner(image, heaps, methods, fieldRow, true) != owner) return 0;
            int table = AssemblyReferences.FieldTable(image, heaps, methods);
            if (table < 0) return 0;
            var field = new int[3];
            AssemblyReferences.ReadField(image, methods, table, fieldRow, field);
            if (field[0] != 0x21 || !MatchesAscii(image, heaps, (uint)field[1], "_value") ||
                !Fits((uint)field[2], 3, (uint)heaps[1])) return 0;
            uint signature = (uint)(heaps[0] + field[2]);
            return U16(image, signature) == 0x0602 && image[signature + 2] == element ? conversion : 0;
        }

        private static int WideConstructorParameter(byte[] image, int[] heaps, uint blob)
        {
            uint at = (uint)heaps[0] + blob;
            if (!Fits(blob, 5, (uint)heaps[1]) || U32(image, at) != 0x01012004) return 0;
            int type = image[at + 4];
            return type == 0x0a || type == 0x0b ? type : 0;
        }

        private static int ConversionOperation(int argument, int element) =>
            argument == 0x0a && element == 0x18 ? 0xd4 : // conv.ovf.i
            argument == 0x0b && element == 0x19 ? 0x8b : // conv.ovf.u.un
            0;

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
