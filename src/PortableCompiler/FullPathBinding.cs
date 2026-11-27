using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Recognize the same source-generated pinning sequence in CoreLib and in
    // ordinary SDK builds. Each call token is resolved within its metadata.
    internal static class FullPathBinding
    {
        internal static bool IsWrapper(byte[] image, int[] layout, int[] heaps, int row)
        {
            if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetFullPathNameW") ||
                MethodDefinitions.ReadColumn(image, layout, row, 1) != 0 ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2010) != 0x10 ||
                !MatchesSignature(image, layout, heaps, row, 0x10)) return false;

            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 40 || body[5] != 0) return false;
            uint start = (uint)body[0];
            // Pin output, pin input, clear system error, call, snapshot system
            // error, clear both pinned locals, publish the saved error, return.
            if (U32(image, start) != 0xe0070b04 || U32(image, start + 4) != 0x080c020a ||
                image[start + 8] != 0xe0 || image[start + 9] != 0x16 || image[start + 10] != 0x28 ||
                U32(image, start + 15) != 0x28050603 || image[start + 23] != 0x28 ||
                U32(image, start + 28) != 0x160ce016 || image[start + 32] != 0xe0 ||
                image[start + 33] != 0x0b || image[start + 34] != 0x28 || image[start + 39] != 0x2a)
                return false;

            uint call = U32(image, start + 19);
            int leaf = (int)(call & 0xffffff);
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || call >> 24 != 6 || leaf < 1 || leaf > counts[6] ||
                Win32ModuleBinding.FindImport(image, heaps, layout, leaf,
                    MethodDefinitions.ReadColumn(image, layout, leaf, 1)) != Win32ModuleBinding.GetFullPathName ||
                !MatchesSignature(image, layout, heaps, leaf, 0x0f)) return false;
            int localBlob = StandaloneSignatures.BlobIndex(image, heaps, layout, counts[6], body[3]);
            // local sig: char*, pinned char&, pinned char&
            if (!Fits((uint)localBlob, 11, (uint)heaps[1])) return false;
            uint locals = (uint)(heaps[0] + localBlob);
            if (image[locals] != 10 || U32(image, locals + 1) != 0x030f0307 ||
                U32(image, locals + 5) != 0x45031045 || U16(image, locals + 9) != 0x0310) return false;
            return IsErrorCall(image, layout, heaps, counts, first, U32(image, start + 11), MarshalErrorMetadata.SetSystem) &&
                IsErrorCall(image, layout, heaps, counts, first, U32(image, start + 24), MarshalErrorMetadata.GetSystem) &&
                IsErrorCall(image, layout, heaps, counts, first, U32(image, start + 35), MarshalErrorMetadata.SetPInvoke);
        }

        private static bool MatchesSignature(byte[] image, int[] layout, int[] heaps, int row, byte reference)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 4, cursor);
            if (end < 0 || end - cursor[0] != 7) return false;
            int at = cursor[0];
            // uint (char&/char*, uint, char&/char*, native int)
            return image[at] == 9 && image[at + 1] == reference && image[at + 2] == 3 &&
                image[at + 3] == 9 && image[at + 4] == reference && image[at + 5] == 3 && image[at + 6] == 0x18;
        }

        private static bool IsErrorCall(byte[] image, int[] layout, int[] heaps, int[] counts, int first, uint token, int member)
        {
            int row = (int)(token & 0xffffff);
            if (token >> 24 == 0x0a)
                return MarshalErrorMetadata.Identify(image, heaps, layout, row) == member;
            if (token >> 24 != 6 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            string name = member == MarshalErrorMetadata.SetSystem ? "SetLastSystemError" :
                member == MarshalErrorMetadata.GetSystem ? "GetLastSystemError" : "SetLastPInvokeError";
            byte[] signature = MarshalErrorMetadata.IsSetter(member) ? new byte[] { 0, 1, 1, 8 } : new byte[] { 0, 0, 8 };
            return CoreStringRange.IsMethod(image, layout, heaps, counts, first, row,
                "System.Runtime.InteropServices", "Marshal", name, signature, true);
        }
    }
}
