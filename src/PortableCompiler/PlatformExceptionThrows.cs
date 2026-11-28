using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // Decision 0104: `newobj <platform exception>::.ctor(...)` immediately followed by `throw`.
    //
    // The constructor arguments are still evaluated in order. The newobj becomes a nop and the
    // throw becomes the fixed typed throw of decision 0054's tags, discarding the arguments:
    //   ArgumentException -> Argument, ArgumentNullException -> ArgumentNull,
    //   ArgumentOutOfRangeException -> ArgumentOutOfRange.
    // The message and parameter name are not carried. A typed catch observes the same type,
    // but not Message or ParamName.
    //
    // InvalidOperationException and OutOfMemoryException have no fixed tag. The throw traps
    // (`unreachable`). The CLR terminates the process on an unhandled exception without running
    // finally blocks, so this is the same outcome unless a caller would catch it; such a catch is
    // the documented CLR-to-Wasm difference of this lowering.
    internal static class PlatformExceptionThrows
    {
        internal const int Trap = -1;
        // Operand 3 of the ThrowFrameworkArgument record: the evaluation stack holds the
        // constructor arguments, which the throw discards instead of an int32 operand.
        internal const int DiscardsArguments = 1;

        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != MemberCall || records[slot + 3] != 0x73) continue;
                int next = records[slot + 2] * Width;
                if (next >= records.Length || records[next] != ThrowNull ||
                    ValueOperands.Entry(records, next / Width, clauses)) continue;
                int tag = Tag(image, layout, heaps, records[slot + 1]);
                if (tag == 0) continue;
                // [arguments...] stays on the stack, never deeper than before the newobj (so the
                // body's maxstack still holds); ThrowFrameworkArgument ends the block and drops it.
                records[slot] = Nop;
                records[slot + 3] = 0;
                records[next] = ThrowFrameworkArgument;
                records[next + 1] = tag;
                records[next + 3] = DiscardsArguments;
            }
        }

        // The fixed tag for the platform exception constructor, Trap, or 0 for any other member.
        private static int Tag(byte[] image, int[] layout, int[] heaps, int member)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !ImageBytes.MatchesAscii(image, heaps, (uint)reference[4], ".ctor")) return 0;
            int parent = reference[AssemblyReferences.ParentType];
            if (SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "ArgumentException"))
                return ExceptionTagsWasm.ArgumentTag;
            if (SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "ArgumentNullException"))
                return ExceptionTagsWasm.ArgumentNullTag;
            if (SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "ArgumentOutOfRangeException"))
                return ExceptionTagsWasm.ArgumentOutOfRangeTag;
            return SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "InvalidOperationException") ||
                SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "OutOfMemoryException") ? Trap : 0;
        }
    }
}
