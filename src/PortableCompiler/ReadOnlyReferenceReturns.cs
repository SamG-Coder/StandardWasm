using PeLibrary;

namespace PortableCompiler
{
    internal static class ReadOnlyReferenceReturns
    {
        // C# emits modreq(InAttribute) before the byref on a readonly
        // interface return. Recognize the real CoreLib modifier before
        // mapping that return onto the existing managed location ABI.
        internal static bool ReadModifier(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] cursor, int end)
        {
            if (!SignatureReader.TakeElement(image, cursor, end, 0x1f)) return false;
            int token = ImageBytes.CompressedUnsigned(image, cursor[0], end, cursor);
            if (token < 1 || cursor[0] >= end || image[cursor[0]] != 0x10) return false;
            var identity = new int[2];
            if (AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, token, identity) < 0) return false;
            byte[] definition = bundle.Get(identity[0]);
            var definitionLayout = new int[4];
            var definitionHeaps = new int[8];
            var names = new int[2];
            var counts = new int[45];
            return CompressedTables.FindWithHeaps(definition, definitionLayout, definitionHeaps) >= 0 &&
                MetadataTableRows.ReadCounts(definition, definitionHeaps[4], definitionHeaps[5], counts) >= 0 &&
                PlatformAssemblyIdentity.IsCoreLibDefinition(definition, definitionHeaps, definitionLayout, counts) &&
                ObjectLayouts.TypeDefinitionNames(definition, definitionHeaps, definitionLayout, identity[1], names) >= 0 &&
                ImageBytes.MatchesAscii(definition, definitionHeaps, (uint)names[0], "InAttribute") &&
                ImageBytes.MatchesAscii(definition, definitionHeaps, (uint)names[1], "System.Runtime.InteropServices");
        }
    }
}
