using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class WasmGlobals
    {
        internal static int Size(int[] globals, int[] objects)
        {
            int count = globals[0];
            if (count == 0) return 0;
            int size = WasmBinary.UnsignedSize(count);
            for (int index = 0; index < count; index++)
            {
                int type = ObjectLayouts.Representation(objects, StaticFields.Storage(globals, index));
                int width = WasmBinary.ValueWidth(type);
                int init = StaticFields.Owner(globals, index) == StaticFields.EventOwner ? 3 :
                    type == F32 ? 5 : type == F64 ? 9 : IsScalar(type) || type == ObjectLayouts.ObjectReference ? 2 : width;
                int entry = width + init + 2; // mutability and end opcode
                if (entry > int.MaxValue - size) return int.MaxValue;
                size += entry;
            }
            return size;
        }

        internal static int Write(byte[] output, int cursor, int[] globals, int[] objects)
        {
            int count = globals[0];
            cursor = WasmBinary.Unsigned(output, cursor, count);
            for (int index = 0; index < count; index++)
            {
                int type = ObjectLayouts.Representation(objects, StaticFields.Storage(globals, index));
                bool eventArgs = StaticFields.Owner(globals, index) == StaticFields.EventOwner;
                cursor = WasmBinary.ValueType(output, cursor, type);
                output[cursor++] = eventArgs ? (byte)0 : (byte)1;
                if (eventArgs)
                {
                    output[cursor++] = 0xfb;
                    output[cursor++] = 1; // struct.new_default
                    cursor = WasmBinary.Unsigned(output, cursor, WasmBinary.HeapType(type));
                }
                else if (IsScalar(type))
                {
                    output[cursor++] = type == I64 ? (byte)0x42 : type == F32 ? (byte)0x43 :
                        type == F64 ? (byte)0x44 : (byte)0x41;
                    int bytes = type == F32 ? 4 : type == F64 ? 8 : 1;
                    for (int i = 0; i < bytes; i++) output[cursor++] = 0;
                }
                else
                {
                    output[cursor++] = 0xd0; // ref.null
                    int heap = type == ObjectLayouts.ObjectReference ? -19 : WasmBinary.HeapType(type);
                    cursor = WasmBinary.Signed(output, cursor, heap);
                }
                output[cursor++] = 0x0b;
            }
            return cursor;
        }
    }
}
