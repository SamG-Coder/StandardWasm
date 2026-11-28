using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Decision 0104: GC.Collect(), GC.WaitForPendingFinalizers() and GC.KeepAlive(object).
    //
    // - KeepAlive only extends the lifetime the CLR's precise collector would otherwise end
    //   early. A Wasm GC reference on the stack is already live, so it lowers to consuming
    //   the argument, exactly.
    // - Collect and WaitForPendingFinalizers ask the CLR to discover unreachable finalizable
    //   objects and run their finalizers. The engine's collector cannot be forced, and modules
    //   compiled without finalizer tracking (decision 0080 and Stage 152) never queue finalizers.
    //   Both therefore return without running any finalizer. CLR-to-Wasm difference:
    //   a finalizer's side effects (for example freeing NativeMemory) do not happen. This is
    //   reported in decision 0104, not silently assumed equivalent.
    internal static class GarbageCollectorMembers
    {
        internal const int Collect = 1, WaitForPendingFinalizers = 2, KeepAlive = 3;

        internal static int Identify(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !SpanMetadata.IsPlatformType(image, layout, heaps, reference[AssemblyReferences.ParentType], "System", "GC"))
                return 0;
            int blob = reference[5];
            if (blob < 1 || !Fits((uint)blob, 4, (uint)heaps[1])) return 0;
            uint signature = (uint)(heaps[0] + blob);
            uint name = (uint)reference[4];
            // static void (): length 3
            if (U32(image, signature) == 0x01000003)
                return MatchesAscii(image, heaps, name, "Collect") ? Collect :
                    MatchesAscii(image, heaps, name, "WaitForPendingFinalizers") ? WaitForPendingFinalizers : 0;
            // static void KeepAlive(object): length 4
            return Fits((uint)blob, 5, (uint)heaps[1]) && U32(image, signature) == 0x01010004 &&
                image[signature + 4] == 0x1c && MatchesAscii(image, heaps, name, "KeepAlive") ? KeepAlive : 0;
        }
    }
}
