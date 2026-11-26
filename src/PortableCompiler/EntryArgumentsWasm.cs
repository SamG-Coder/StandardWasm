using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Exported constructors that let a host build the managed string[] for
    // Main(string[] args). Wasm GC objects are opaque to JavaScript, so the
    // host allocates through the module and passes the finished array to the
    // entry. The builders exist only when an export takes a String vector.
    //   "\0newArguments"(count i32) -> String[]
    //   "\0newString"(length i32) -> Char[]
    //   "\0setStringCodeUnit"(Char[], index i32, codeUnit i32) -> ()
    //   "\0setArgument"(String[], index i32, Char[]) -> ()
    // The last call wraps the Char[] as an immutable String; the host keeps
    // no other reference that could later observe or modify its storage.
    // Indices are host-supplied and checked by the Wasm array instructions,
    // which trap rather than raise a managed exception. No managed code runs
    // during construction, so no CLR-visible behavior depends on that trap.
    internal static class EntryArgumentsWasm
    {
        internal const int FunctionCount = 4;

        // Returns the String[] class type used by an export, or zero.
        internal static int ArgumentArray(int[] exports, int[] arities, int[] offsets, int[] types, int[] objects)
        {
            for (int i = 0; i < exports.Length; i++)
            {
                int function = exports[i];
                for (int parameter = 1; parameter <= arities[function]; parameter++)
                {
                    int type = types[offsets[function] + parameter];
                    if (type >= FirstClass && ArrayElement(objects, type) == String) return type;
                }
            }
            return 0;
        }

        // Value types encode the heap type as s33; instructions encode a u32 type index.
        private static int ValueHeapWidth(int array) => WasmBinary.HeapTypeSize(WasmBinary.HeapType(array));
        private static int IndexWidth(int array) => WasmBinary.UnsignedSize(WasmBinary.HeapType(array));

        internal static int TypeBytes(int array) =>
            // (i32) -> ref null array; (i32) -> ref null Char[]; (Char[], i32, i32) -> (); (array, i32, Char[]) -> ()
            (5 + ValueHeapWidth(array)) + 6 + 7 + (7 + ValueHeapWidth(array));

        internal static int WriteTypes(byte[] output, int cursor, int array)
        {
            output[cursor++] = 0x60;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            output[cursor++] = 1;
            cursor = WasmBinary.ValueType(output, cursor, array);
            output[cursor++] = 0x60;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            output[cursor++] = 1;
            cursor = WasmBinary.ValueType(output, cursor, Chars);
            output[cursor++] = 0x60;
            output[cursor++] = 3;
            cursor = WasmBinary.ValueType(output, cursor, Chars);
            output[cursor++] = 0x7f;
            output[cursor++] = 0x7f;
            output[cursor++] = 0;
            output[cursor++] = 0x60;
            output[cursor++] = 3;
            cursor = WasmBinary.ValueType(output, cursor, array);
            output[cursor++] = 0x7f;
            cursor = WasmBinary.ValueType(output, cursor, Chars);
            output[cursor++] = 0;
            return cursor;
        }

        // Code section entries including each body's size prefix.
        internal static int CodeBytes(int array) =>
            (1 + 6 + IndexWidth(array)) + (1 + 7) + (1 + 11) + (1 + 13 + IndexWidth(array));

        internal static int WriteBodies(byte[] output, int cursor, int array)
        {
            int heap = WasmBinary.HeapType(array);
            cursor = WasmBinary.Unsigned(output, cursor, 6 + IndexWidth(array));
            cursor = Allocate(output, cursor, heap);
            cursor = WasmBinary.Unsigned(output, cursor, 7);
            cursor = Allocate(output, cursor, WasmBinary.HeapType(Chars));
            output[cursor++] = 11;
            output[cursor++] = 0; // no locals
            cursor = Arguments(output, cursor, 3);
            output[cursor++] = 0xfb;
            output[cursor++] = 0x0e; // array.set Char[]
            output[cursor++] = 4;
            output[cursor++] = 0x0b;
            cursor = WasmBinary.Unsigned(output, cursor, 13 + IndexWidth(array));
            output[cursor++] = 0;
            cursor = Arguments(output, cursor, 3);
            cursor = StringWasm.Wrap(output, cursor);
            output[cursor++] = 0xfb;
            output[cursor++] = 0x0e; // array.set String[]
            cursor = WasmBinary.Unsigned(output, cursor, heap);
            output[cursor++] = 0x0b;
            return cursor;
        }

        private static int Allocate(byte[] output, int cursor, int heap)
        {
            output[cursor++] = 0; // no locals
            output[cursor++] = 0x20;
            output[cursor++] = 0;
            output[cursor++] = 0xfb;
            output[cursor++] = 7; // array.new_default: null Strings or zero code units
            cursor = WasmBinary.Unsigned(output, cursor, heap);
            output[cursor++] = 0x0b;
            return cursor;
        }

        private static int Arguments(byte[] output, int cursor, int count)
        {
            for (int i = 0; i < count; i++)
            {
                output[cursor++] = 0x20;
                output[cursor++] = (byte)i;
            }
            return cursor;
        }

        internal static int ExportBytes(int firstFunction)
        {
            int size = 4 * 2 + 13 + 10 + 18 + 12; // name lengths, names and export kinds
            for (int i = 0; i < FunctionCount; i++) size += WasmBinary.UnsignedSize(firstFunction + i);
            return size;
        }

        internal static int WriteExports(byte[] output, int cursor, int firstFunction)
        {
            // "\0newArguments", "\0newString", "\0setStringCodeUnit", "\0setArgument"
            cursor = Name(output, cursor, 13, 0x77656e00, 0x75677241, 0x746e656d, 0x73);
            cursor = Export(output, cursor, firstFunction);
            cursor = Name(output, cursor, 10, 0x77656e00, 0x69727453, 0x676e, 0);
            cursor = Export(output, cursor, firstFunction + 1);
            cursor = Name(output, cursor, 18, 0x74657300, 0x69727453, 0x6f43676e, 0x6e556564);
            output[cursor++] = 0x69; // continuation of "setStringCodeUnit"
            output[cursor++] = 0x74;
            cursor = Export(output, cursor, firstFunction + 2);
            cursor = Name(output, cursor, 12, 0x74657300, 0x75677241, 0x746e656d, 0);
            return Export(output, cursor, firstFunction + 3);
        }

        // Writes the length and at most sixteen bytes of a packed name.
        private static int Name(byte[] output, int cursor, int length, uint a, uint b, uint c, uint d)
        {
            output[cursor++] = (byte)length;
            int written = length < 16 ? length : 16;
            for (int i = 0; i < written; i++)
            {
                uint word = i < 4 ? a : i < 8 ? b : i < 12 ? c : d;
                output[cursor++] = unchecked((byte)(word >> ((i & 3) * 8)));
            }
            return cursor;
        }

        private static int Export(byte[] output, int cursor, int function)
        {
            output[cursor++] = 0; // function export
            return WasmBinary.Unsigned(output, cursor, function);
        }
    }
}
