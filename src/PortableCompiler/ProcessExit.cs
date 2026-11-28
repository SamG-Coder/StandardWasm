using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    // Decision 0104: static void System.Environment::Exit(int32).
    // CLR: the process ends with the status; no finally or catch block of the calling thread runs.
    // Wasm: the status is stored in the module's exported i64 global "\0exitStatus" as
    // (1 << 32) | (uint)status, so zero still means "no exit", and then `unreachable` traps. A trap
    // is the one unwind that Wasm exception handling (catch_all, which lowers finally) cannot
    // intercept, so no managed handler observes it. The launcher reads the export after the trap.
    internal static class ProcessExit
    {
        // The exit-status global in the StaticFields registry.
        internal const int StatusOwner = int.MaxValue - 4;
        internal const string ExportName = "\0exitStatus";

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

        // The registry index of the exit-status global, or -1 when no reachable code exits.
        internal static int StatusGlobal(int[] globals)
        {
            for (int index = 0; index < globals[0]; index++)
                if (StaticFields.Owner(globals, index) == StatusOwner) return index;
            return -1;
        }

        internal static int ExportBytes(int global) =>
            WasmBinary.UnsignedSize(ExportName.Length) + ExportName.Length + 1 + WasmBinary.UnsignedSize(global);

        internal static int WriteExport(byte[] output, int cursor, int global)
        {
            cursor = WasmBinary.Unsigned(output, cursor, ExportName.Length);
            for (int i = 0; i < ExportName.Length; i++) output[cursor++] = (byte)ExportName[i];
            output[cursor++] = 3; // global
            return WasmBinary.Unsigned(output, cursor, global);
        }

        // [status] -> (no return)
        internal static int Emit(byte[] code, int used, int status, int global)
        {
            used = BranchWasm.Local(code, used, 0x20, status);
            code[used++] = 0xad; // i64.extend_i32_u
            code[used++] = 0x42; code[used++] = 0x80; code[used++] = 0x80; code[used++] = 0x80;
            code[used++] = 0x80; code[used++] = 0x10; // i64.const 1 << 32
            code[used++] = 0x84; // i64.or
            code[used++] = 0x24; // global.set
            used = WasmBinary.Unsigned(code, used, global);
            code[used++] = 0x00; // unreachable
            return used;
        }
    }
}
