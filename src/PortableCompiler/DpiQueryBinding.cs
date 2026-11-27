using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Recognize the generated SDK wrapper that pins a single enum cell for
    // GetProcessDpiAwareness. Other pointer and pinning bodies use normal IL lowering.
    internal static class DpiQueryBinding
    {
        internal static bool IsWrapper(byte[] image, int[] heaps, int[] methods, int row)
        {
            if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, methods, row, 3),
                "GetProcessDpiAwareness") || MethodDefinitions.ReadColumn(image, methods, row, 1) != 0 ||
                (MethodDefinitions.ReadColumn(image, methods, row, 2) & 0x2010) != 0x10) return false;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, methods, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 13 || body[5] != 0) return false;
            uint start = (uint)body[0];
            // ldarg.1; stloc.1; ldloc.1; conv.u; stloc.0; ldarg.0;
            // ldloc.0; call <exact native leaf>; ret.
            if (U32(image, start) != 0xe0070b03 || U32(image, start + 4) != 0x2806020a ||
                image[start + 12] != 0x2a) return false;
            uint call = U32(image, start + 8);
            if (call >> 24 != 6) return false;
            int target = (int)(call & 0xffffff);
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                target < 1 || target > counts[6] ||
                MethodDefinitions.ReadColumn(image, methods, target, 1) != 0x80 ||
                NativeImports.Find(image, heaps, methods, counts, "api-ms-win-shcore-scaling-l1-1-1.dll",
                    "GetProcessDpiAwareness", target, 0x101) != target) return false;

            var shape = new int[3];
            var native = new int[3];
            if (!ReadSignature(image, heaps, methods, row, 0x10, shape) ||
                !ReadSignature(image, heaps, methods, target, 0x0f, native) ||
                shape[0] != native[0] || shape[1] != native[1] || shape[2] != native[2]) return false;

            int blob = StandaloneSignatures.BlobIndex(image, heaps, methods, counts[6], body[3]);
            if (blob < 1 || blob >= heaps[1]) return false;
            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int end = cursor[0] + length;
            return length > 0 && end <= limit &&
                SignatureReader.TakeElement(image, cursor, end, 7) &&
                SignatureReader.TakeElement(image, cursor, end, 2) &&
                SignatureReader.TakeElement(image, cursor, end, 0x0f) &&
                ReadValueType(image, cursor, end) == shape[2] &&
                SignatureReader.TakeElement(image, cursor, end, 0x45) &&
                SignatureReader.TakeElement(image, cursor, end, 0x10) &&
                ReadValueType(image, cursor, end) == shape[2] && cursor[0] == end;
        }

        private static bool ReadSignature(byte[] image, int[] heaps, int[] methods,
            int row, int address, int[] shape)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, methods, row, 4), 2, cursor);
            if (end < 0) return false;
            shape[0] = ReadValueType(image, cursor, end); // HRESULT
            shape[1] = ReadValueType(image, cursor, end); // HANDLE
            if (shape[0] < 1 || shape[1] < 1 || !SignatureReader.TakeElement(image, cursor, end, address)) return false;
            shape[2] = ReadValueType(image, cursor, end); // PROCESS_DPI_AWARENESS
            return shape[2] > 0 && cursor[0] == end;
        }

        private static int ReadValueType(byte[] image, int[] cursor, int end) =>
            SignatureReader.TakeElement(image, cursor, end, 0x11)
                ? CompressedUnsigned(image, cursor[0], end, cursor) : -1;
    }
}
