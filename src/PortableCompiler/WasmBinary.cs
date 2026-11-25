namespace PortableCompiler
{
    internal static class WasmBinary
    {
        internal static int WriteModule(byte[] output, int[] arities, int[] sizes, int functions, byte[] bodies)
        {
            int typeSize = UnsignedSize(functions);
            int functionSize = UnsignedSize(functions);
            int codeSize = UnsignedSize(functions);
            // Use one type per function in discovery order. Accumulate against
            // destination capacity before any writes, guarding size arithmetic.
            for (int i = 0; i < functions; i++)
            {
                int type = 3 + UnsignedSize(arities[i]) + arities[i];
                int code = UnsignedSize(sizes[i]) + sizes[i];
                int index = UnsignedSize(i);
                if (type > output.Length - typeSize || code > output.Length - codeSize ||
                    index > output.Length - functionSize)
                    return -3;
                typeSize += type;
                codeSize += code;
                functionSize += index;
            }
            int remaining = output.Length - 8;
            remaining -= SectionOverhead(typeSize);
            if (remaining < typeSize) return -3;
            remaining -= typeSize + SectionOverhead(functionSize);
            if (remaining < functionSize) return -3;
            remaining -= functionSize + SectionOverhead(7) + 7 + SectionOverhead(codeSize);
            if (remaining < codeSize) return -3;

            output[0] = 0;
            output[1] = 0x61;
            output[2] = 0x73;
            output[3] = 0x6d;
            output[4] = 1;
            output[5] = 0;
            output[6] = 0;
            output[7] = 0;
            int cursor = Section(output, 8, 1, typeSize);
            cursor = Unsigned(output, cursor, functions);
            for (int type = 0; type < functions; type++)
            {
                output[cursor++] = 0x60;
                cursor = Unsigned(output, cursor, arities[type]);
                for (int i = 0; i < arities[type]; i++)
                    output[cursor++] = 0x7f;
                output[cursor++] = 1;
                output[cursor++] = 0x7f;
            }

            cursor = Section(output, cursor, 3, functionSize);
            cursor = Unsigned(output, cursor, functions);
            for (int i = 0; i < functions; i++)
                cursor = Unsigned(output, cursor, i);

            cursor = Section(output, cursor, 7, 7);
            output[cursor++] = 1;
            output[cursor++] = 3;
            output[cursor++] = 0x72;
            output[cursor++] = 0x75;
            output[cursor++] = 0x6e;
            output[cursor++] = 0;
            output[cursor++] = 0;

            cursor = Section(output, cursor, 10, codeSize);
            cursor = Unsigned(output, cursor, functions);
            int start = 0;
            for (int body = 0; body < functions; body++)
            {
                int size = sizes[body];
                cursor = Unsigned(output, cursor, size);
                for (int i = 0; i < size; i++)
                    output[cursor++] = bodies[start + i];
                start += size;
            }
            return cursor;
        }

        private static int SectionOverhead(int payload) => 1 + UnsignedSize(payload);

        private static int Section(byte[] bytes, int cursor, byte id, int payload)
        {
            bytes[cursor++] = id;
            return Unsigned(bytes, cursor, payload);
        }

        private static int UnsignedSize(int value)
        {
            int count = 1;
            while (value >= 128)
            {
                value >>= 7;
                count++;
            }
            return count;
        }

        internal static int Unsigned(byte[] bytes, int cursor, int value)
        {
            while (value >= 128)
            {
                bytes[cursor++] = (byte)((value & 127) | 128);
                value >>= 7;
            }
            bytes[cursor++] = (byte)value;
            return cursor;
        }

        internal static int Signed(byte[] bytes, int cursor, int value)
        {
            while (value < -64 || value > 63)
            {
                bytes[cursor++] = (byte)((value & 127) | 128);
                value >>= 7;
            }
            bytes[cursor++] = (byte)(value & 127);
            return cursor;
        }
    }
}
