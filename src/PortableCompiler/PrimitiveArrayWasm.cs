using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class PrimitiveArrayWasm
    {
        internal const int MemberCount = 14;
        internal const int TypeBytes = 7 * 2 + 2 + MemberCount * 6;
        internal const int FirstElement = 2, EndElement = 26;

        internal static int Heap(int first, int element) =>
            first + (element < 0x18 ? element - FirstElement : element - 0x18 + 12);

        internal static int WriteBase(byte[] output, int cursor, int storage)
        {
            output[cursor++] = 0x50; // non-final, no supertypes
            output[cursor++] = 0;
            return WriteLayout(output, cursor, storage);
        }

        internal static int WriteIdentities(byte[] output, int cursor)
        {
            // Distinct positions in a single recursive group prevent canonical
            // equivalence when two CLI elements have the same mutable storage.
            output[cursor++] = 0x4e;
            output[cursor++] = MemberCount;
            for (int element = FirstElement; element < EndElement; element++)
            {
                int storage = ArrayTypeRefs.Storage(element);
                if (storage == 0) continue;
                output[cursor++] = 0x4f; // final subtype of the existing storage heap
                output[cursor++] = 1;
                output[cursor++] = (byte)WasmBinary.HeapType(storage);
                cursor = WriteLayout(output, cursor, storage);
            }
            return cursor;
        }

        private static int WriteLayout(byte[] output, int cursor, int storage)
        {
            output[cursor++] = 0x5e;
            output[cursor++] = storage == Bytes ? (byte)0x78 : storage == Chars ? (byte)0x77 :
                storage == Longs ? (byte)0x7e : storage == Singles ? (byte)0x7d :
                storage == Doubles ? (byte)0x7c : (byte)0x7f;
            output[cursor++] = 1;
            return cursor;
        }
    }
}
