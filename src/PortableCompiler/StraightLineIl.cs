using PeLibrary;

namespace PortableCompiler
{
    internal static class StraightLineIl
    {
        internal const int Invalid = -1, Unsupported = -2;

        // The output begins with the body local declaration vector. Each accepted IL
        // instruction has the same evaluation order in Wasm, so no value spill is needed.
        internal static int Translate(byte[] image, int[] body, int parameters, byte[] code)
        {
            int cursor = body[0], end = cursor + body[1], depth = 0, used = 0;
            code[used++] = 0;
            while (cursor < end)
            {
                int op = image[cursor++];
                if (op == 0x2a)
                {
                    if (depth != 1 || cursor != end)
                        return Invalid;
                    code[used++] = 0x0b;
                    return used;
                }

                if (op == 0)
                    continue;
                if (op >= 2 && op <= 5 || op == 0x0e)
                {
                    int index = op - 2;
                    if (op == 0x0e)
                    {
                        if (cursor == end) return Invalid;
                        index = image[cursor++];
                    }
                    if (index >= parameters)
                        return Invalid;
                    code[used++] = 0x20;
                    used = WasmBinary.Unsigned(code, used, index);
                    depth++;
                }
                else if (op >= 0x15 && op <= 0x20)
                {
                    int value = op - 0x16;
                    if (op == 0x1f)
                    {
                        if (cursor == end) return Invalid;
                        value = image[cursor++];
                        value = (value ^ 128) - 128;
                    }
                    else if (op == 0x20)
                    {
                        if (end - cursor < 4) return Invalid;
                        value = unchecked((int)ImageBytes.U32(image, (uint)cursor));
                        cursor += 4;
                    }
                    code[used++] = 0x41;
                    used = WasmBinary.Signed(code, used, value);
                    depth++;
                }
                else if (op == 0x65 || op == 0x66)
                {
                    if (depth < 1) return Invalid;
                    code[used++] = 0x41;
                    code[used++] = 0x7f;
                    code[used++] = op == 0x65 ? (byte)0x6c : (byte)0x73;
                }
                else
                {
                    if (op == 0x5b) return Unsupported; // Signed division needs the overflow guard.
                    int lowered = Binary(op);
                    if (lowered < 0) return Unsupported;
                    if (depth < 2) return Invalid;
                    code[used++] = (byte)lowered;
                    depth--;
                }

                if (depth > body[2])
                    return Invalid;
            }
            return Invalid;
        }

        internal static int Binary(int op)
        {
            if (op >= 0x58 && op <= 0x5c) return 0x6a + op - 0x58;
            if (op == 0x5e) return 0x70;
            if (op >= 0x5f && op <= 0x64) return 0x71 + op - 0x5f;
            return -1;
        }
    }
}
