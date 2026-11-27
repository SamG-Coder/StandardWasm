using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class WasmGlobals
    {
        internal static int Size(int[] globals, int[] objects, int descriptorHeap, bool tracksRoots)
        {
            int count = globals[0];
            if (count == 0) return 0;
            int size = WasmBinary.UnsignedSize(count);
            for (int index = 0; index < count; index++)
            {
                int storage = StaticFields.Storage(globals, index);
                bool cell = storage <= AddressedFields.Int32Cell;
                int type = cell ? StaticFields.CellVector(storage) : ObjectLayouts.Representation(objects, storage);
                int width = WasmBinary.ValueWidth(type);
                int owner = StaticFields.Owner(globals, index);
                int init = IsValueLocation(storage) ? ManagedValueLocationWasm.DefaultCellSize(tracksRoots) : cell ? 4 + WasmBinary.UnsignedSize(WasmBinary.HeapType(type)) : RuntimeTypeTokens.IsDescriptorOwner(owner)
                    ? RuntimeTypeWasm.InitializerSize(descriptorHeap, RuntimeTypeTokens.ModuleSlot(owner), StaticFields.Row(globals, index))
                    : owner == StaticFields.EventOwner ? 3 :
                        type == F32 ? 5 : type == F64 ? 9 : IsScalar(type) || type == ObjectLayouts.ObjectReference || RuntimeTypeMetadata.IsStorage(type) ? 2 : width;
                int entry = width + init + 2; // mutability and end opcode
                if (entry > int.MaxValue - size) return int.MaxValue;
                size += entry;
            }
            return size;
        }

        internal static int Write(byte[] output, int cursor, int[] globals, int[] objects, int descriptorHeap, bool tracksRoots)
        {
            int count = globals[0];
            cursor = WasmBinary.Unsigned(output, cursor, count);
            for (int index = 0; index < count; index++)
            {
                int storage = StaticFields.Storage(globals, index);
                bool cell = storage <= AddressedFields.Int32Cell;
                int type = cell ? StaticFields.CellVector(storage) : ObjectLayouts.Representation(objects, storage);
                int owner = StaticFields.Owner(globals, index);
                bool eventArgs = owner == StaticFields.EventOwner;
                bool descriptor = RuntimeTypeTokens.IsDescriptorOwner(owner);
                cursor = WasmBinary.ValueType(output, cursor, type);
                output[cursor++] = eventArgs || descriptor || cell || IsValueLocation(storage) ? (byte)0 : (byte)1;
                if (descriptor)
                    cursor = RuntimeTypeWasm.WriteInitializer(output, cursor, descriptorHeap, RuntimeTypeTokens.ModuleSlot(owner), StaticFields.Row(globals, index));
                else if (eventArgs)
                {
                    output[cursor++] = 0xfb;
                    output[cursor++] = 1; // struct.new_default
                    cursor = WasmBinary.Unsigned(output, cursor, WasmBinary.HeapType(type));
                }
                else if (IsValueLocation(storage))
                    cursor = ManagedValueLocationWasm.CreateDefaultCell(output, cursor, tracksRoots);
                else if (cell)
                {
                    cursor = BranchWasm.Const(output, cursor, 1);
                    cursor = BranchWasm.Gc(output, cursor, 7, type); // array.new_default
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
                    int heap = type == ObjectLayouts.ObjectReference || RuntimeTypeMetadata.IsStorage(type) ? -19 : WasmBinary.HeapType(type);
                    cursor = WasmBinary.Signed(output, cursor, heap);
                }
                output[cursor++] = 0x0b;
            }
            return cursor;
        }
    }
}
