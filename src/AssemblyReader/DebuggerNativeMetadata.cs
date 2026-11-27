namespace PeLibrary
{
    internal static class DebuggerNativeMetadata
    {
        internal static bool IsAttachmentQuery(byte[] image, int[] heaps, int[] methods, int row, int implementation)
        {
            if (implementation != 0x80) return false; // PreserveSig
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, methods, counts)) return false;
            if (NativeImports.Find(image, heaps, methods, counts, "QCall", "DebugDebugger_IsManagedDebuggerAttached", row, 0x101) != row)
                return false;
            int declaring = ObjectLayouts.Owner(image, heaps, methods, row, false);
            return declaring > 0 && ObjectLayouts.NamedDefinition(image, heaps, methods, counts, first,
                declaring, "System.Diagnostics", "Debugger");
        }
    }
}
