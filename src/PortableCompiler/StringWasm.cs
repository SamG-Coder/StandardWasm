namespace PortableCompiler
{
    internal static class StringWasm
    {
        // Three private Char[] scratch locals followed by two lengths. The
        // published String only exposes its immutable reference to a fresh copy.
        internal static int Copy(byte[] code, int used, int left, int right, int scratch, bool join)
        {
            int a = scratch, b = scratch + 1, output = scratch + 2;
            int aLength = scratch + 3, bLength = scratch + 4;
            used = ArrayOrEmpty(code, used, left, join);
            used = Local(code, used, 0x21, a);
            used = Length(code, used, a);
            used = Local(code, used, 0x21, aLength);
            if (join)
            {
                used = ArrayOrEmpty(code, used, right, true);
                used = Local(code, used, 0x21, b);
                used = Length(code, used, b);
                used = Local(code, used, 0x21, bLength);
            }

            used = Local(code, used, 0x20, aLength);
            if (join)
            {
                used = Local(code, used, 0x20, bLength);
                code[used++] = 0x6a;
            }
            code[used++] = 0xfb;
            code[used++] = 7; // array.new_default Char[]
            code[used++] = 4;
            used = Local(code, used, 0x21, output);
            used = CopyArray(code, used, output, -1, a, aLength);
            if (join)
                used = CopyArray(code, used, output, aLength, b, bLength);
            used = Local(code, used, 0x20, output);
            return Wrap(code, used);
        }

        internal static int Wrap(byte[] code, int used)
        {
            code[used++] = 0xfb;
            code[used++] = 0;
            code[used++] = 5; // struct.new String
            return used;
        }

        internal static int Unwrap(byte[] code, int used)
        {
            code[used++] = 0xfb;
            code[used++] = 2;
            code[used++] = 5;
            code[used++] = 0;
            return used;
        }

        private static int ArrayOrEmpty(byte[] code, int used, int input, bool wrapped)
        {
            used = Local(code, used, 0x20, input);
            code[used++] = 0xd1; // ref.is_null
            code[used++] = 0x04;
            code[used++] = 0x63; // if result (ref null Char[])
            code[used++] = 4;
            code[used++] = 0xfb;
            code[used++] = 8; // empty array.new_fixed
            code[used++] = 4;
            code[used++] = 0;
            code[used++] = 0x05;
            used = Local(code, used, 0x20, input);
            if (wrapped) used = Unwrap(code, used);
            code[used++] = 0x0b;
            return used;
        }

        private static int Length(byte[] code, int used, int local)
        {
            used = Local(code, used, 0x20, local);
            code[used++] = 0xfb;
            code[used++] = 0x0f;
            return used;
        }

        private static int CopyArray(byte[] code, int used, int destination, int offset, int source, int length)
        {
            used = Local(code, used, 0x20, destination);
            used = offset < 0 ? Zero(code, used) : Local(code, used, 0x20, offset);
            used = Local(code, used, 0x20, source);
            used = Zero(code, used);
            used = Local(code, used, 0x20, length);
            code[used++] = 0xfb;
            code[used++] = 0x11; // array.copy
            code[used++] = 4;
            code[used++] = 4;
            return used;
        }

        private static int Zero(byte[] code, int used)
        {
            code[used++] = 0x41;
            code[used++] = 0;
            return used;
        }

        private static int Local(byte[] code, int used, byte instruction, int local)
        {
            code[used++] = instruction;
            return WasmBinary.Unsigned(code, used, local);
        }
    }
}
