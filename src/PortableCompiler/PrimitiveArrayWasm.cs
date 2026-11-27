using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal sealed class PrimitiveArrayWasm
    {
        internal const int FirstElement = 2, EndElement = 26;
        internal const int BaseTypeOverhead = 7 * 2;
        private readonly int[] heads = new int[EndElement];
        private readonly byte[] families = new byte[WasmBinary.NativeArrayHeap + 1];
        internal int EntryCount { get; private set; }
        internal int TypeBytes { get; private set; }

        internal int WriteHeap(byte[] code, int position, int bodyStart, int element) =>
            WasmIndexFixups.WriteAt(code, position, bodyStart, heads, element);

        internal void Prepare()
        {
            int reached = 0;
            for (int element = FirstElement; element < EndElement; element++)
            {
                if (heads[element] != 0)
                    reached |= 1 << WasmBinary.HeapType(ArrayTypeRefs.Storage(element));
            }
            for (int element = FirstElement; element < EndElement; element++)
            {
                int storage = ArrayTypeRefs.Storage(element);
                if (storage == 0) continue;
                int heap = WasmBinary.HeapType(storage);
                if ((reached & (1 << heap)) != 0)
                    families[heap] = (byte)(families[heap] + 1);
            }
            for (int heap = 0; heap < families.Length; heap++)
            {
                int count = families[heap];
                if (count == 0) continue;
                EntryCount++;
                TypeBytes += 6 * count + (count > 1 ? 2 : 0);
            }
        }

        internal static int WriteBase(byte[] output, int cursor, int storage)
        {
            output[cursor++] = 0x50; // non-final, no supertypes
            output[cursor++] = 0;
            return WriteLayout(output, cursor, storage);
        }

        internal int WriteIdentities(byte[] output, int cursor, byte[] bodies, int type)
        {
            for (int heap = 0; heap < families.Length; heap++)
            {
                int count = families[heap];
                if (count == 0) continue;
                // A storage family has a stable recursive shape across modules.
                // Its positions distinguish CLI types sharing packed storage.
                if (count > 1)
                {
                    output[cursor++] = 0x4e;
                    output[cursor++] = (byte)count;
                }
                for (int element = FirstElement; element < EndElement; element++)
                {
                    int storage = ArrayTypeRefs.Storage(element);
                    if (storage == 0 || WasmBinary.HeapType(storage) != heap) continue;
                    WasmIndexFixups.Resolve(bodies, heads[element], type++);
                    output[cursor++] = 0x4f; // final subtype of the storage heap
                    output[cursor++] = 1;
                    output[cursor++] = (byte)heap;
                    cursor = WriteLayout(output, cursor, storage);
                }
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
