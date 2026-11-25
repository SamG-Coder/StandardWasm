namespace PortableCompiler
{
    internal static class WasmBinary
    {
        internal static int Write(byte[] output, int parameters, byte[] body, int bodyLength)
        {
            int typeSize = 4 + UnsignedSize(parameters) + parameters;
            int codeSize = 1 + UnsignedSize(bodyLength) + bodyLength;
            int required = 8 + SectionSize(typeSize) + SectionSize(2) + SectionSize(7) + SectionSize(codeSize);
            if (output.Length < required)
                return -3;
            output[0] = 0;
            output[1] = 0x61;
            output[2] = 0x73;
            output[3] = 0x6d;
            output[4] = 1;
            output[5] = 0;
            output[6] = 0;
            output[7] = 0;
            int cursor = Section(output, 8, 1, typeSize);
            output[cursor++] = 1;
            output[cursor++] = 0x60;
            cursor = Unsigned(output, cursor, parameters);
            for (int i = 0; i < parameters; i++)
                output[cursor++] = 0x7f;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;

            cursor = Section(output, cursor, 3, 2);
            output[cursor++] = 1;
            output[cursor++] = 0;

            cursor = Section(output, cursor, 7, 7);
            output[cursor++] = 1;
            output[cursor++] = 3;
            output[cursor++] = 0x72;
            output[cursor++] = 0x75;
            output[cursor++] = 0x6e;
            output[cursor++] = 0;
            output[cursor++] = 0;

            cursor = Section(output, cursor, 10, codeSize);
            output[cursor++] = 1;
            cursor = Unsigned(output, cursor, bodyLength);
            for (int i = 0; i < bodyLength; i++)
                output[cursor++] = body[i];
            return cursor;
        }

        private static int SectionSize(int payload) => 1 + UnsignedSize(payload) + payload;

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
