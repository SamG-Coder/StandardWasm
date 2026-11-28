using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Decision 0104: static void System.Environment::Exit(int32).
    // CLR: the process ends with the status; no finally or catch block of the calling thread runs.
    // Wasm: `call standardwasm:process exit(status)` records the status in the host, then
    // `unreachable` traps. A trap is the one unwind that Wasm exception handling (catch_all,
    // which lowers finally) cannot intercept, so no managed handler observes it. The launcher
    // reports the recorded status as the exit code.
    internal static class ProcessExit
    {
        private const string Module = "standardwasm:process", Member = "exit";
        internal const int TypeBytes = 4, Capacity = 16;

        internal static bool IsExit(byte[] image, int[] heaps, int[] layout, int row)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0 ||
                reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[4], "Exit")) return false;
            int blob = reference[5];
            // Length 4: default static, one parameter, returns void, takes Int32.
            if (blob < 1 || !Fits((uint)blob, 5, (uint)heaps[1]) ||
                U32(image, (uint)(heaps[0] + blob)) != 0x01010004 || image[heaps[0] + blob + 4] != 0x08) return false;
            return SpanMetadata.IsPlatformType(image, layout, heaps, reference[AssemblyReferences.ParentType],
                "System", "Environment");
        }

        internal static int EntryBytes(int type) => Module.Length + Member.Length + 3 + WasmBinary.UnsignedSize(type);

        internal static int WriteType(byte[] output, int cursor)
        {
            output[cursor++] = 0x60;
            output[cursor++] = 1;
            output[cursor++] = 0x7f; // exit status
            output[cursor++] = 0;
            return cursor;
        }

        internal static int WriteEntry(byte[] output, int cursor, int type)
        {
            cursor = Win32ErrorWasm.WriteName(output, cursor, Module);
            cursor = Win32ErrorWasm.WriteName(output, cursor, Member);
            output[cursor++] = 0;
            return WasmBinary.Unsigned(output, cursor, type);
        }

        // [status] -> (no return)
        internal static int Emit(byte[] code, int used, int status, int import)
        {
            used = BranchWasm.Local(code, used, 0x20, status);
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, import);
            code[used++] = 0x00; // unreachable
            return used;
        }
    }
}
