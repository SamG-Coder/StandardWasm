using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class CoreExceptionTextWasm
    {
        internal const int BodyCapacity = 128;

        internal static int WriteBody(byte[] code, int used, byte[] data, int[] dataSize, int empty, int initializer)
        {
            code[used++] = 0; // No locals. The kind is the sole argument.
            if (initializer >= 0)
            {
                code[used++] = 0x10;
                used = WasmBinary.Unsigned(code, used, initializer);
            }
            for (int kind = 1; kind <= 3; kind++)
            {
                // CoreCLR's native fallback messages. Browser modules do not depend on
                // a Windows native resource satellite.
                string message = kind == 1 ? "Thread was being aborted." :
                    kind == 2 ? "Thread was interrupted from a waiting state." :
                    "Insufficient memory to continue the execution of the program.";
                int offset = dataSize[0];
                if (message.Length * 2 > data.Length - offset) return -3;
                for (int index = 0; index < message.Length; index++)
                {
                    int unit = message[index];
                    data[offset + index * 2] = unchecked((byte)unit);
                    data[offset + index * 2 + 1] = (byte)(unit >> 8);
                }
                dataSize[0] = offset + message.Length * 2;
                used = Local(code, used, 0x20, 0);
                used = Const(code, used, kind);
                code[used++] = 0x46; // i32.eq
                code[used++] = 0x04; code[used++] = 0x40; // if
                // A nonempty native result is newly allocated rather than interned.
                used = StringWasm.AllocateLiteral(code, used, offset, message.Length);
                code[used++] = 0x0f; // return
                code[used++] = 0x0b;
            }
            // CoreCLR's release build returns the canonical empty string for an unknown kind.
            used = StringWasm.LoadLiteral(code, used, empty, 0, 0);
            code[used++] = 0x0b;
            return used;
        }
    }
}
