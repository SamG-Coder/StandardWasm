using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Decision 0104: System.Diagnostics.Stopwatch and the constant platform static fields that
    // benchmark-style console programs read.
    //
    // WebAssembly has no time source, so the clock is the one host call. It uses the typed native
    // import table of decision 0083 with the entries CoreLib's own Stopwatch calls on Windows:
    //   native:kernel32.dll QueryPerformanceCounter(LARGE_INTEGER*)   -> BOOL
    //   native:kernel32.dll QueryPerformanceFrequency(LARGE_INTEGER*) -> BOOL
    // The LARGE_INTEGER is written to an 8-byte slot of the module's NativeMemory control block
    // (NativeHeapWasm) and read back. Stopwatch.GetTimestamp() is the counter and
    // Stopwatch.Frequency the frequency, so programs observe ticks and a rate as on Windows; the
    // host decides the rate (the browser host reports nanoseconds).
    internal static class PlatformClock
    {
        internal const int Frequency = 1, IsHighResolution = 2, IsLittleEndian = 3;
        internal const int Capacity = 96;
        // Synthetic owner of the two entries in the native table (MethodDef owners are >= 0).
        private const int EntryOwner = -3;

        // A MemberRef row naming one of the platform static fields, or 0.
        internal static int StaticField(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0) return 0;
            int blob = reference[5];
            if (blob < 1 || !Fits((uint)blob, 3, (uint)heaps[1])) return 0;
            uint signature = (uint)(heaps[0] + blob);
            if (U16(image, signature) != 0x0602) return 0; // length 2, FIELD
            int element = image[signature + 2];
            int parent = reference[AssemblyReferences.ParentType];
            uint name = (uint)reference[4];
            if (SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System.Diagnostics", "Stopwatch"))
            {
                if (element == 0x0a && MatchesAscii(image, heaps, name, "Frequency")) return Frequency;
                if (element == 0x02 && MatchesAscii(image, heaps, name, "IsHighResolution")) return IsHighResolution;
                return 0;
            }
            return element == 0x02 && MatchesAscii(image, heaps, name, "IsLittleEndian") &&
                SpanMetadata.IsPlatformType(image, layout, heaps, parent, "System", "BitConverter") ? IsLittleEndian : 0;
        }

        // static int64 System.Diagnostics.Stopwatch::GetTimestamp()
        internal static bool IsGetTimestamp(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "GetTimestamp")) return false;
            int blob = reference[5];
            // Length 3: default static, no parameters, returns Int64.
            if (blob < 1 || !Fits((uint)blob, 4, (uint)heaps[1]) ||
                U32(image, (uint)(heaps[0] + blob)) != 0x0a000003) return false;
            return SpanMetadata.IsPlatformType(image, layout, heaps, reference[AssemblyReferences.ParentType],
                "System.Diagnostics", "Stopwatch");
        }

        // Registers the kernel32 entry (and the helpers every typed native table imports) and
        // returns its index in the native table.
        internal static int Register(NativeImportTable natives, bool frequency)
        {
            natives.Helper(NativeImportTable.PresentHelper);
            natives.Helper(NativeImportTable.LibraryHelper);
            byte[] module = NativeImportTable.Ascii("native:kernel32.dll");
            byte[] name = NativeImportTable.Ascii(frequency ? "QueryPerformanceFrequency" : "QueryPerformanceCounter");
            var types = new byte[1];
            types[0] = 0x7f; // LARGE_INTEGER*: a wasm32 address in memory 0
            return natives.Add(EntryOwner, frequency ? 2 : 1, module, module.Length, name, name.Length, types, 1, 0x7f);
        }

        // [] -> [i64] through the kernel32 entry `function`, using the heap control block's slot.
        internal static int Emit(byte[] code, int used, int function, int heap)
        {
            used = NativeHeapWasm.EnsureCreated(code, used, heap);
            used = Slot(code, used, heap);
            code[used++] = 0x10; // call the typed native import
            used = WasmBinary.Unsigned(code, used, function);
            code[used++] = 0x1a; // drop the BOOL: both calls always succeed on supported hosts
            used = Slot(code, used, heap);
            code[used++] = 0x29; code[used++] = 3; code[used++] = 0; // i64.load align 8
            return used;
        }

        private static int Slot(byte[] code, int used, int heap)
        {
            code[used++] = 0x23; // global.get
            used = WasmBinary.Unsigned(code, used, heap);
            used = BranchWasm.Const(code, used, NativeHeapWasm.ClockSlot);
            code[used++] = 0x6a;
            return used;
        }
    }
}
