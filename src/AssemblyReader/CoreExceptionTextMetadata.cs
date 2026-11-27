using static PeLibrary.ImageBytes;
using static PeLibrary.SignatureReader;

namespace PeLibrary
{
    internal static class CoreExceptionTextMetadata
    {
        private const string Name = "GetMessageFromNativeResources";

        internal static bool IsLookup(byte[] image, int[] heaps, int[] methods, int row)
        {
            if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, methods, row, 3), Name)) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, methods, counts) ||
                (MethodDefinitions.ReadColumn(image, methods, row, 2) & 0x2010) != 0x10) return false;
            int declaring = ObjectLayouts.Owner(image, heaps, methods, row, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, declaring, "System", "Exception"))
                return false;

            var cursor = new int[1];
            int end = ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4), 1, cursor);
            if (end < 0 || !TakeElement(image, cursor, end, 0x0e) || !TakeElement(image, cursor, end, 0x11)) return false;
            int kind = CompressedUnsigned(image, cursor[0], end, cursor);
            if (kind < 1 || (kind & 3) != 0 || cursor[0] != end ||
                !ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, kind >> 2, "", "ExceptionMessageKind", true))
                return false;

            // The managed adapter creates a single stack-scoped string handle, gives it to
            // the native resource lookup, and immediately returns the written string.
            var body = new int[6];
            if (MethodBodies.Read(image, (uint)MethodDefinitions.ReadColumn(image, methods, row, 0), body) != 0 ||
                body[1] != 17 || body[5] != 0) return false;
            int local = StandaloneSignatures.BlobIndex(image, heaps, methods, counts[6], body[3]);
            if (local < 1 || local > heaps[1] - 4 || U32(image, (uint)(heaps[0] + local)) != 0x0e010703) return false;
            uint code = (uint)body[0];
            if (U32(image, code) != 0x12020a14 || U16(image, code + 4) != 0x7300 ||
                image[code + 10] != 0x28 || U16(image, code + 15) != 0x2a06) return false;
            int constructor = MethodRow(U32(image, code + 6), counts[6]);
            int native = MethodRow(U32(image, code + 11), counts[6]);
            if (constructor == 0 || native == 0 ||
                NativeImports.Find(image, heaps, methods, counts, "QCall", "ExceptionNative_GetMessageFromNativeResources",
                    native, 0x101) != native ||
                ObjectLayouts.Owner(image, heaps, methods, native, false) != declaring) return false;

            int handle = ObjectLayouts.Owner(image, heaps, methods, constructor, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, handle,
                "System.Runtime.CompilerServices", "StringHandleOnStack") ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, methods, constructor, 3), ".ctor"))
                return false;
            end = ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, methods, constructor, 4), 1, cursor);
            if (end < 0 || !TakeElement(image, cursor, end, Void) || !TakeElement(image, cursor, end, 0x10) ||
                !TakeElement(image, cursor, end, 0x0e) || cursor[0] != end) return false;

            end = ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, native, 4), 2, cursor);
            return end >= 0 && TakeElement(image, cursor, end, Void) && TakeElement(image, cursor, end, 0x11) &&
                CompressedUnsigned(image, cursor[0], end, cursor) == kind && TakeElement(image, cursor, end, 0x11) &&
                CompressedUnsigned(image, cursor[0], end, cursor) == (handle << 2) && cursor[0] == end;
        }

        private static int MethodRow(uint token, int count)
        {
            int row = (int)(token & 0xffffff);
            return token >> 24 == 6 && row > 0 && row <= count ? row : 0;
        }
    }
}
