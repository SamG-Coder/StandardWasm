using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class WasmBinary
    {
        internal static int WriteModule(byte[] output, int[] arities, int[] offsets, byte[] types, int[] sizes, int functions, byte[] bodies,
            byte[] names, int[] entryOffsets, int[] exports, int[] objects, int[] thunks,
            byte[] literals, int literalSize)
        {
            int typeOffset = objects[0] + 5;
            int totalFunctions = functions + thunks[0];
            int typeSize = UnsignedSize(functions + typeOffset) + 20;
            for (int i = 0; i < objects[0]; i++)
            {
                int slot = 1 + i * Width;
                int first = objects[slot + 1], fields = objects[slot + 2];
                int size = 1, stored = 0;
                for (int field = 0; field < fields; field++)
                {
                    int type = FieldType(objects, first + field);
                    if (type == 0) continue;
                    size += 1 + ValueWidth((byte)type);
                    stored++;
                }
                size += UnsignedSize(stored);
                if (size > output.Length - typeSize) return -3;
                typeSize += size;
            }
            int functionSize = UnsignedSize(totalFunctions) + thunks[0];
            int codeSize = UnsignedSize(totalFunctions);
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
                int index = UnsignedSize(i + typeOffset);
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
            int tableSize = 3 + UnsignedSize(thunks[0]);
            int elementSize = 5 + UnsignedSize(thunks[0]);
            for (int i = 0; i < thunks[0]; i++)
            {
                int function = thunks[i + 1];
                int receiver = types[offsets[function] + 1];
                int thunkSize = ThunkSize(receiver, function);
                if (1 + thunkSize > output.Length - codeSize ||
                    UnsignedSize(functions + i) > output.Length - elementSize)
                    return -3;
                codeSize += 1 + thunkSize;
                elementSize += UnsignedSize(functions + i);
            }

            int remaining = output.Length - 8;
            remaining = Reserve(remaining, typeSize);
            remaining = Reserve(remaining, functionSize);
            if (thunks[0] != 0)
                remaining = Reserve(remaining, tableSize);
            remaining = Reserve(remaining, exportSize);
            if (thunks[0] != 0)
                remaining = Reserve(remaining, elementSize);
            if (literalSize != 0)
                remaining = Reserve(remaining, 1); // DataCount
            remaining = Reserve(remaining, codeSize);
            int dataOverhead = 2 + UnsignedSize(literalSize);
            if (remaining < 0 || (literalSize != 0 && (dataOverhead > remaining ||
                literalSize > remaining - dataOverhead || Reserve(remaining, dataOverhead + literalSize) < 0)))
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
            cursor = Unsigned(output, cursor, functions + typeOffset);
            output[cursor++] = 0x5e; // array of packed i8
            output[cursor++] = 0x78;
            output[cursor++] = 1;
            output[cursor++] = 0x5e; // array of i32
            output[cursor++] = 0x7f;
            output[cursor++] = 1;
            // Thunk type: (eqref) -> i32. Its closure holds an eqref and a funcref.
            output[cursor++] = 0x60;
            output[cursor++] = 1;
            output[cursor++] = 0x6d;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            output[cursor++] = 0x5f;
            output[cursor++] = 2;
            output[cursor++] = 0x6d;
            output[cursor++] = 0;
            output[cursor++] = 0x70;
            output[cursor++] = 0;
            output[cursor++] = 0x5e; // immutable packed UTF-16 array
            output[cursor++] = 0x77;
            output[cursor++] = 0;
            for (int i = 0; i < objects[0]; i++)
            {
                int slot = 1 + i * Width;
                int first = objects[slot + 1], fields = objects[slot + 2];
                output[cursor++] = 0x5f; // struct
                int stored = 0;
                for (int field = 0; field < fields; field++)
                    if (FieldType(objects, first + field) != 0) stored++;
                cursor = Unsigned(output, cursor, stored);
                for (int field = 0; field < fields; field++)
                {
                    int fieldType = FieldType(objects, first + field);
                    if (fieldType == 0) continue;
                    cursor = ValueType(output, cursor, (byte)fieldType);
                    output[cursor++] = 1;
                }
            }
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
            cursor = Unsigned(output, cursor, totalFunctions);
            for (int i = 0; i < functions; i++)
                cursor = Unsigned(output, cursor, i + typeOffset);
            for (int i = 0; i < thunks[0]; i++)
                output[cursor++] = 2;
            if (thunks[0] != 0)
            {
                cursor = Section(output, cursor, 4, tableSize);
                output[cursor++] = 1;
                output[cursor++] = 0x70;
                output[cursor++] = 0;
                cursor = Unsigned(output, cursor, thunks[0]);
            }

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

            if (thunks[0] != 0)
            {
                cursor = Section(output, cursor, 9, elementSize);
                output[cursor++] = 1; // one segment
                output[cursor++] = 0; // active, table zero
                output[cursor++] = 0x41;
                output[cursor++] = 0;
                output[cursor++] = 0x0b;
                cursor = Unsigned(output, cursor, thunks[0]);
                for (int i = 0; i < thunks[0]; i++)
                    cursor = Unsigned(output, cursor, functions + i);
            }

            if (literalSize != 0)
            {
                cursor = Section(output, cursor, 12, 1);
                output[cursor++] = 1;
            }

            cursor = Section(output, cursor, 10, codeSize);
            cursor = Unsigned(output, cursor, totalFunctions);
            int start = 0;
            for (int body = 0; body < functions; body++)
            {
                int size = sizes[body];
                cursor = Unsigned(output, cursor, size);
                for (int i = 0; i < size; i++)
                    output[cursor++] = bodies[start + i];
                start += size;
            }
            for (int i = 0; i < thunks[0]; i++)
            {
                int function = thunks[i + 1], receiver = types[offsets[function] + 1];
                cursor = Unsigned(output, cursor, ThunkSize(receiver, function));
                output[cursor++] = 0; // local declarations
                output[cursor++] = 0x20;
                output[cursor++] = 0;
                output[cursor++] = 0xfb;
                output[cursor++] = 22; // ref.cast to the target's concrete receiver
                cursor = Signed(output, cursor, receiver - Bytes);
                output[cursor++] = 0x10;
                cursor = Unsigned(output, cursor, function);
                output[cursor++] = 0x0b;
            }
            if (literalSize != 0)
            {
                cursor = Section(output, cursor, 11, dataOverhead + literalSize);
                output[cursor++] = 1; // one passive segment
                output[cursor++] = 1;
                cursor = Unsigned(output, cursor, literalSize);
                for (int i = 0; i < literalSize; i++)
                    output[cursor++] = literals[i];
            }
            return cursor;
        }

        private static int ValueWidth(byte type) =>
            type == Void ? 0 : type == I32 || type == MethodPointer ? 1 :
                type == Delegate || type - Bytes <= 63 ? 2 : 3;

        internal static int ValueType(byte[] output, int cursor, byte type)
        {
            if (type == I32 || type == MethodPointer)
                output[cursor++] = 0x7f;
            else
            {
                output[cursor++] = 0x63; // ref null
                cursor = Signed(output, cursor, type == Delegate ? 3 : type - Bytes);
            }
            return cursor;
        }

        private static int ThunkSize(int receiver, int function) =>
            7 + (receiver - Bytes <= 63 ? 1 : 2) + UnsignedSize(function);

        private static int Reserve(int remaining, int size)
        {
            int overhead = 1 + UnsignedSize(size);
            if (remaining < overhead || size > remaining - overhead) return -1;
            return remaining - overhead - size;
        }

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
