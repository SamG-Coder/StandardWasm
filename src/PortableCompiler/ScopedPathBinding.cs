using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Recognize the same source-generated pinning sequence in CoreLib and in
    // ordinary SDK builds. Each call token is resolved within its metadata.
    internal static class ScopedPathBinding
    {
        internal static int FindWrapper(byte[] image, int[] layout, int[] heaps, int row)
        {
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            bool withFilePart = MatchesAscii(image, heaps, name, "GetFullPathNameW");
            if ((!withFilePart && !MatchesAscii(image, heaps, name, "GetLongPathNameW")) ||
                MethodDefinitions.ReadColumn(image, layout, row, 1) != 0 ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2010) != 0x10 ||
                !MatchesSignature(image, layout, heaps, row, 0x10, withFilePart)) return 0;

            int arity = withFilePart ? 4 : 3;
            int member = withFilePart ? Win32ModuleBinding.GetFullPathName : Win32ModuleBinding.GetLongPathName;
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 36 + arity ||
                body[2] < arity || body[5] != 0) return 0;
            uint start = (uint)body[0];
            // Pin output, pin input, clear system error, call, snapshot system
            // error, clear both pinned locals, publish the saved error, return.
            uint nativeCall = start + 15;
            uint errorCall = nativeCall + (uint)arity + 4;
            if (U32(image, start) != (withFilePart ? 0xe0070b04u : 0xe0070b03u) ||
                U32(image, start + 4) != 0x080c020a ||
                image[start + 8] != 0xe0 || image[start + 9] != 0x16 || image[start + 10] != 0x28 ||
                (withFilePart ? U32(image, nativeCall) != 0x28050603 :
                    U16(image, nativeCall) != 0x0406 || image[nativeCall + 2] != 0x28) ||
                image[errorCall] != 0x28 || U32(image, errorCall + 5) != 0x160ce016 ||
                image[errorCall + 9] != 0xe0 || image[errorCall + 10] != 0x0b ||
                image[errorCall + 11] != 0x28 || image[errorCall + 16] != 0x2a) return 0;

            uint call = U32(image, nativeCall + (uint)arity);
            int leaf = (int)(call & 0xffffff);
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || call >> 24 != 6 || leaf < 1 || leaf > counts[6] ||
                Win32ModuleBinding.FindImport(image, heaps, layout, leaf,
                    MethodDefinitions.ReadColumn(image, layout, leaf, 1)) != member ||
                !MatchesSignature(image, layout, heaps, leaf, 0x0f, withFilePart)) return 0;
            int localBlob = StandaloneSignatures.BlobIndex(image, heaps, layout, counts[6], body[3]);
            // local sig: char*, pinned char&, pinned char&
            if (!Fits((uint)localBlob, 11, (uint)heaps[1])) return 0;
            uint locals = (uint)(heaps[0] + localBlob);
            if (image[locals] != 10 || U32(image, locals + 1) != 0x030f0307 ||
                U32(image, locals + 5) != 0x45031045 || U16(image, locals + 9) != 0x0310) return 0;
            return IsErrorCall(image, layout, heaps, counts, first, U32(image, start + 11), MarshalErrorMetadata.SetSystem) &&
                IsErrorCall(image, layout, heaps, counts, first, U32(image, errorCall + 1), MarshalErrorMetadata.GetSystem) &&
                IsErrorCall(image, layout, heaps, counts, first, U32(image, errorCall + 12), MarshalErrorMetadata.SetPInvoke) ? member : 0;
        }

        internal static bool MatchesSignature(byte[] image, int[] layout, int[] heaps, int row, byte reference, bool withFilePart)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4),
                withFilePart ? 4 : 3, cursor);
            if (end < 0 || end - cursor[0] != (withFilePart ? 7 : 6)) return false;
            int at = cursor[0];
            if (image[at] != 9 || image[at + 1] != reference || image[at + 2] != 3) return false;
            return withFilePart
                ? image[at + 3] == 9 && image[at + 4] == reference && image[at + 5] == 3 && image[at + 6] == 0x18
                : image[at + 3] == reference && image[at + 4] == 3 && image[at + 5] == 9;
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
