using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class WasmBinary
    {
        internal static int WriteModule(byte[] output, int[] arities, int[] offsets, byte[] types, int[] sizes, int functions, byte[] bodies,
            byte[] names, int[] entryOffsets, int[] exports)
        {
            int typeSize = UnsignedSize(functions + 2) + 6;
            int functionSize = UnsignedSize(functions);
            int codeSize = UnsignedSize(functions);
            // Use one type per function in discovery order. Accumulate against
            // destination capacity before any writes, guarding size arithmetic.
            for (int i = 0; i < functions; i++)
            {
                int shape = offsets[i];
                byte result = types[shape];
                int type = 2 + UnsignedSize(arities[i]) + ValueWidth(result);
                for (int parameter = 0; parameter < arities[i]; parameter++)
                    type += ValueWidth(types[shape + 1 + parameter]);
                int code = UnsignedSize(sizes[i]) + sizes[i];
                int index = UnsignedSize(i + 2);
                if (type > output.Length - typeSize || code > output.Length - codeSize ||
                    index > output.Length - functionSize)
                    return -3;
                typeSize += type;
                codeSize += code;
                functionSize += index;
            }
            int exportSize = UnsignedSize(exports.Length);
            for (int i = 0; i < exports.Length; i++)
            {
                int name = (int)ImageBytes.U32(names, (uint)entryOffsets[i] + 4);
                int metadata = UnsignedSize(name) + 1 + UnsignedSize(exports[i]);
                if (metadata > output.Length - exportSize || name > output.Length - exportSize - metadata)
                    return -3;
                exportSize += metadata + name;
            }
            int remaining = output.Length - 8;
            remaining -= SectionOverhead(typeSize);
            if (remaining < typeSize) return -3;
            remaining -= typeSize + SectionOverhead(functionSize);
            if (remaining < functionSize) return -3;
            remaining -= functionSize + SectionOverhead(exportSize);
            if (remaining < exportSize) return -3;
            remaining -= exportSize + SectionOverhead(codeSize);
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
            cursor = Unsigned(output, cursor, functions + 2);
            output[cursor++] = 0x5e; // array of packed i8
            output[cursor++] = 0x78;
            output[cursor++] = 1;
            output[cursor++] = 0x5e; // array of i32
            output[cursor++] = 0x7f;
            output[cursor++] = 1;
            for (int type = 0; type < functions; type++)
            {
                int shape = offsets[type];
                output[cursor++] = 0x60;
                cursor = Unsigned(output, cursor, arities[type]);
                for (int i = 0; i < arities[type]; i++)
                    cursor = ValueType(output, cursor, types[shape + i + 1]);
                byte result = types[shape];
                output[cursor++] = result == Void ? (byte)0 : (byte)1;
                if (result != Void)
                    cursor = ValueType(output, cursor, result);
            }

            cursor = Section(output, cursor, 3, functionSize);
            cursor = Unsigned(output, cursor, functions);
            for (int i = 0; i < functions; i++)
                cursor = Unsigned(output, cursor, i + 2);

            cursor = Section(output, cursor, 7, exportSize);
            cursor = Unsigned(output, cursor, exports.Length);
            for (int i = 0; i < exports.Length; i++)
            {
                int source = entryOffsets[i];
                int name = (int)ImageBytes.U32(names, (uint)source + 4);
                cursor = Unsigned(output, cursor, name);
                for (int j = 0; j < name; j++)
                    output[cursor++] = names[source + 8 + j];
                output[cursor++] = 0;
                cursor = Unsigned(output, cursor, exports[i]);
            }

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

        private static int ValueWidth(byte type) =>
            type == Void ? 0 : type == I32 ? 1 : 2;

        internal static int ValueType(byte[] output, int cursor, byte type)
        {
            if (type == I32)
                output[cursor++] = 0x7f;
            else
            {
                output[cursor++] = 0x63; // ref null
                output[cursor++] = (byte)(type - Bytes);
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
