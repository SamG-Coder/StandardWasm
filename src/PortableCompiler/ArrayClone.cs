using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // Decision 0105: `callvirt object System.Array::Clone()` on a single-dimension array: a new
    // array of the same Wasm vector (and so the same CLI element type) with every element
    // copied, as the CLR's shallow MemberwiseClone. A null receiver raises
    // NullReferenceException. The result's static type is object, so the castclass that C#
    // emits next checks it as usual.
    internal static class ArrayClone
    {
        internal const int Capacity = 64;

        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records)
        {
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != MemberCall || records[slot + 3] != 0x6f) continue;
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, records[slot + 1], member) < 0 ||
                    member[AssemblyReferences.Depth] != 0 || !MatchesAscii(image, heaps, (uint)member[4], "Clone") ||
                    !SpanMetadata.IsPlatformType(image, layout, heaps, member[AssemblyReferences.ParentType], "System", "Array"))
                    continue;
                int blob = member[5];
                // instance object Clone(): 03 20 00 1c
                if (blob < 1 || !Fits((uint)blob, 4, (uint)heaps[1]) ||
                    U32(image, (uint)(heaps[0] + blob)) != 0x1c002003) continue;
                records[slot] = CloneArray;
                records[slot + 1] = 0;
                records[slot + 3] = 0;
            }
        }

        // [array] -> [object]; `length` is an i32 scratch local and `copy` the eqref result spill,
        // which may share the receiver's slot only after the receiver is no longer read.
        internal static int Emit(byte[] code, int used, int vector, int array, int length, int copy)
        {
            used = GuardNull(code, used, array);
            used = Local(code, used, 0x20, array);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x22, length);
            used = Gc(code, used, 0x07, vector); // array.new_default
            used = Local(code, used, 0x21, copy);
            used = Local(code, used, 0x20, copy);
            code[used++] = 0xfb; code[used++] = 0x16; // ref.cast (ref $vector)
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(vector));
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, array);
            used = Const(code, used, 0);
            used = Local(code, used, 0x20, length);
            used = Gc(code, used, 0x11, vector); // array.copy
            return WasmBinary.Unsigned(code, used, WasmBinary.HeapType(vector));
        }
    }
}
