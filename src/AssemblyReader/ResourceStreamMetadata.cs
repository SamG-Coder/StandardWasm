using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ResourceStreamMetadata
    {
        // The original public override remains responsible for the null
        // branch and constructing its own managed ManifestResourceStream.
        // Bind only the native prelude after proving the managed shape.
        internal static int FindOpen(byte[] image, int[] heaps, int[] methods, int[] counts,
            int first, int owner, int[] range, int wrapper)
        {
            var cursor = new int[1];
            int open = 0;
            for (int row = range[0]; row < range[1]; row++)
            {
                uint name = (uint)MethodDefinitions.ReadColumn(image, methods, row, 3);
                if (!MatchesAscii(image, heaps, name, "GetManifestResourceStream")) continue;
                int flags = MethodDefinitions.ReadColumn(image, methods, row, 2);
                int end = MethodSignature(image, heaps, methods, row, cursor);
                int start = cursor[0];
                if ((flags & 0x57) != 0x46 || end - start < 5 || U16(image, (uint)start) != 0x0120 ||
                    image[start + 2] != 0x12) continue;
                int stream = CompressedUnsigned(image, start + 3, end, cursor);
                int tail = cursor[0];
                if (stream <= 0 || (stream & 3) != 0 || end - tail != 1 || image[tail] != 0x0e ||
                    !ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, stream >> 2, "System.IO", "Stream"))
                    continue;
                if (open != 0) return -2;
                open = row;
            }
            if (open == 0) return -2;
            int constructor = CheckBody(image, heaps, methods, counts, first, owner, open, wrapper, cursor);
            if (constructor < 0) return constructor;
            range[0] = constructor;
            return open;
        }

        private static int CheckBody(byte[] image, int[] heaps, int[] methods, int[] counts,
            int first, int owner, int open, int wrapper, int[] cursor)
        {
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, methods, open, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 38 || body[5] != 0) return -2;
            int localBlob = StandaloneSignatures.BlobIndex(image, heaps, methods, counts[6], body[3]);
            int end = BlobEnd(image, heaps, localBlob, cursor);
            int start = cursor[0];
            if (end - start < 7 || U16(image, (uint)start) != 0x0307 || image[start + 2] != 0x12 ||
                CompressedUnsigned(image, start + 3, end, cursor) != owner << 2 ||
                end - cursor[0] != 3 || U16(image, (uint)cursor[0]) != 0x050f || image[cursor[0] + 2] != 9)
                return -2;

            uint at = (uint)body[0];
            // ldloca.s of the RuntimeAssembly slot feeds QCallAssembly.
            // ldloca.s of the third local passes the out uint to the wrapper.
            if (U32(image, at) != 0x00120a02 || image[at + 4] != 0x73 ||
                U32(image, at + 9) != 0x28021203 || U32(image, at + 13) != (uint)(0x06000000 | wrapper) ||
                U32(image, at + 17) != 0xe016070b || U32(image, at + 21) != 0x07020d2e ||
                U32(image, at + 25) != 0x6e086e08 || U16(image, at + 29) != 0x7317 ||
                U16(image, at + 35) != 0x142a || image[at + 37] != 0x2a)
                return -2;
            int handle = ConstructorRow(image, heaps, methods, counts, U32(image, at + 5));
            if (handle == 0 || !CheckHandleConstructor(image, heaps, methods, counts, first, owner, handle, cursor))
                return -2;
            int stream = ConstructorRow(image, heaps, methods, counts, U32(image, at + 31));
            if (stream == 0 || !CheckStreamConstructor(image, heaps, methods, counts, first, owner, stream, cursor))
                return -2;
            return stream;
        }

        private static int ConstructorRow(byte[] image, int[] heaps, int[] methods, int[] counts, uint token)
        {
            int row = (int)(token & 0xffffff);
            if (token >> 24 != 6 || row < 1 || row > counts[6]) return 0;
            uint name = (uint)MethodDefinitions.ReadColumn(image, methods, row, 3);
            return MatchesAscii(image, heaps, name, ".ctor") ? row : 0;
        }

        private static bool CheckHandleConstructor(byte[] image, int[] heaps, int[] methods, int[] counts,
            int first, int owner, int row, int[] cursor)
        {
            int type = ObjectLayouts.Owner(image, heaps, methods, row, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, type,
                "System.Runtime.CompilerServices", "QCallAssembly")) return false;
            int end = MethodSignature(image, heaps, methods, row, cursor);
            int start = cursor[0];
            return end - start >= 6 && U32(image, (uint)start) == 0x10010120 && image[start + 4] == 0x12 &&
                CompressedUnsigned(image, start + 5, end, cursor) == owner << 2 && cursor[0] == end;
        }

        private static bool CheckStreamConstructor(byte[] image, int[] heaps, int[] methods, int[] counts,
            int first, int owner, int row, int[] cursor)
        {
            int type = ObjectLayouts.Owner(image, heaps, methods, row, false);
            if (!NestedTypes.IsDirectChild(image, heaps, methods, counts, type, owner) ||
                !ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, type, "", "ManifestResourceStream", nested: true))
                return false;
            int end = MethodSignature(image, heaps, methods, row, cursor);
            int start = cursor[0];
            if (end - start < 11 || U32(image, (uint)start) != 0x12010520 ||
                CompressedUnsigned(image, start + 4, end, cursor) != owner << 2)
                return false;
            int tail = cursor[0];
            if (end - tail < 6 || U32(image, (uint)tail) != 0x0a0a050f || image[tail + 4] != 0x11) return false;
            int access = CompressedUnsigned(image, tail + 5, end, cursor);
            return access > 0 && (access & 3) == 0 && cursor[0] == end &&
                ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first, access >> 2, "System.IO", "FileAccess");
        }

        private static int MethodSignature(byte[] image, int[] heaps, int[] methods, int row, int[] cursor) =>
            BlobEnd(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4), cursor);

        private static int BlobEnd(byte[] image, int[] heaps, int offset, int[] cursor)
        {
            if (offset <= 0 || offset >= heaps[1]) return -1;
            int end = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + offset, end, cursor);
            return length >= 0 && length <= end - cursor[0] ? cursor[0] + length : -1;
        }
    }
}
