using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ResourceNativeMetadata
    {
        // Locate the native stub and the managed marshalling wrapper in a
        // selected CoreLib. Do not use SDK-specific names or MethodDef rows.
        // On success, result receives [native MethodDef, wrapper MethodDef].
        internal static int Identify(byte[] image, int[] heaps, int[] methods, int[] result)
        {
            if (result.Length < 2) return -3;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return first;
            if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, methods, counts)) return 0;
            int native = NativeImports.Find(image, heaps, methods, counts, "QCall", "AssemblyNative_GetResource");
            if (native <= 0) return native;
            int owner = ObjectLayouts.Owner(image, heaps, methods, native, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, owner, "System.Reflection", "RuntimeAssembly") ||
                !MatchesSignature(image, heaps, methods, counts, first, native, true))
                return -2;
            var range = new int[2];
            if (ObjectLayouts.MethodRange(image, heaps, methods, owner, range) < 0) return -1;
            int wrapper = 0;
            for (int row = range[0]; row < range[1]; row++)
            {
                uint name = unchecked((uint)MethodDefinitions.ReadColumn(image, methods, row, 3));
                if (!MatchesAscii(image, heaps, name, "GetResource") ||
                    !MatchesSignature(image, heaps, methods, counts, first, row, false))
                    continue;
                if (wrapper != 0 || !CallsNativeStub(image, heaps, methods, counts, first, row, native)) return -2;
                wrapper = row;
            }
            if (wrapper == 0) return -2;
            result[0] = native;
            result[1] = wrapper;
            return 1;
        }

        private static bool MatchesSignature(byte[] image, int[] heaps, int[] methods, int[] counts, int first,
            int row, bool native)
        {
            if ((MethodDefinitions.ReadColumn(image, methods, row, 2) & 0x10) == 0) return false;
            int blob = MethodDefinitions.ReadColumn(image, methods, row, 4);
            if (blob <= 0 || blob >= heaps[1]) return false;
            int end = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, end, next);
            int start = next[0];
            // DEFAULT, three arguments, byte*, valuetype QCallAssembly.
            if (size < 9 || size > end - start || image[start] != 0 || image[start + 1] != 3 ||
                image[start + 2] != 0x0f || image[start + 3] != 5 || image[start + 4] != 0x11)
                return false;
            end = start + size;
            int qcall = CompressedUnsigned(image, start + 5, end, next);
            if (qcall <= 0 || (qcall & 3) != 0 || !ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, qcall >> 2,
                "System.Runtime.CompilerServices", "QCallAssembly")) return false;
            int tail = next[0];
            return native
                ? end - tail == 4 && U32(image, (uint)tail) == 0x090f070f // ushort*, uint*
                : end - tail == 3 && image[tail] == 0x0e && U16(image, (uint)tail + 1) == 0x0910; // string, uint&
        }

        private static bool CallsNativeStub(byte[] image, int[] heaps, int[] methods, int[] counts, int first, int wrapper, int native)
        {
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, methods, wrapper, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 33 || body[5] != 0)
                return false;
            // The selected stub marshals a pinned UTF-16 name and length.
            // Validate all instructions around the two symbolic call operands
            // so an unsupported stub shape is left for regular IL lowering.
            uint at = (uint)body[0];
            return U32(image, at) == 0x04541604 && U32(image, at + 4) == 0x0ae0070b &&
                U16(image, at + 8) == 0x2803 &&
                IsPinnableReference(image, heaps, methods, counts, first, U32(image, at + 10)) &&
                U32(image, at + 14) == 0x0ce0090d && U32(image, at + 18) == 0x28060802 &&
                U32(image, at + 22) == (uint)(0x06000000 | native) &&
                U32(image, at + 26) == 0x160de016 && U16(image, at + 30) == 0x0be0 && image[at + 32] == 0x2a;
        }

        private static bool IsPinnableReference(byte[] image, int[] heaps, int[] methods, int[] counts, int first, uint token)
        {
            int row = (int)(token & 0xffffff);
            if (token >> 24 != 6 || row < 1 || row > counts[6]) return false;
            uint name = unchecked((uint)MethodDefinitions.ReadColumn(image, methods, row, 3));
            if (!MatchesAscii(image, heaps, name, "GetPinnableReference") ||
                (MethodDefinitions.ReadColumn(image, methods, row, 2) & 0x10) == 0)
                return false;
            int owner = ObjectLayouts.Owner(image, heaps, methods, row, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, owner,
                "System.Runtime.InteropServices.Marshalling", "Utf16StringMarshaller")) return false;

            int blob = MethodDefinitions.ReadColumn(image, methods, row, 4);
            if (blob <= 0 || blob >= heaps[1]) return false;
            int end = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, end, next);
            int start = next[0];
            if (size < 7 || size > end - start || U16(image, (uint)start) != 0x0100 || image[start + 2] != 0x1f)
                return false;
            end = start + size;
            int modifier = CompressedUnsigned(image, start + 3, end, next);
            int tail = next[0];
            return modifier > 0 && (modifier & 3) == 0 &&
                ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, modifier >> 2,
                    "System.Runtime.InteropServices", "InAttribute") &&
                end - tail == 3 && U16(image, (uint)tail) == 0x0310 && image[tail + 2] == 0x0e;
        }
    }
}
