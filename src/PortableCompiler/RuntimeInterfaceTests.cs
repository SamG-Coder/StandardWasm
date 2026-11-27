using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal sealed class RuntimeInterfaceTests
    {
        private int[] heads;

        internal static bool UsesClassLayouts(BundleImages bundle, int[] objects, int type)
        {
            // CoreLib interfaces can also be implemented by split representations
            // such as strings, arrays and primitive boxes.
            int owner = AssemblyOf(objects, type);
            if (owner < 0 || owner >= bundle.Count) return false;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            return CompressedTables.FindWithHeaps(image, layout, heaps) >= 0 &&
                MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts);
        }

        internal int Call(byte[] code, int used, int bodyStart, int type)
        {
            if (heads == null) heads = new int[MaxClasses];
            code[used++] = 0x10;
            return WasmIndexFixups.WriteAt(code, used, bodyStart, heads, type - FirstClass);
        }

        internal int Write(byte[] code, int[] position, int[] objects, int[] offsets, int[] signatures,
            int[] sizes, int[] bodyOffsets, int[] arities, int functions, int hostImports)
        {
            if (heads == null) return functions;
            int used = position[0];
            for (int index = 0; index < objects[0]; index++)
            {
                if (heads[index] == 0) continue;
                if (functions == arities.Length) return -3;
                int signature = MethodCompiler.NextSignature(offsets, arities, functions);
                if (signature >= signatures.Length - 2) return -3;
                signatures[signature] = I32;
                signatures[signature + 1] = ObjectReference;
                offsets[functions] = signature;
                arities[functions] = 1;
                bodyOffsets[functions] = used;
                int end = WriteTest(code, used, objects, FirstClass + index);
                if (end < 0) return end;
                WasmIndexFixups.Resolve(code, heads[index], hostImports + functions);
                sizes[functions++] = end - used;
                used = end;
            }
            position[0] = used;
            return functions;
        }

        private static int WriteTest(byte[] code, int used, int[] objects, int target)
        {
            if (code.Length - used < 5) return -3;
            code[used++] = 0; // no additional locals
            // Scan the final registry. A known implementing base covers every
            // subtype, including layouts discovered after the original cast site.
            for (int type = FirstClass; type - FirstClass < objects[0]; type++)
            {
                if (IsInterface(objects, type) || IsValueType(objects, type) ||
                    ArrayElement(objects, type) != 0 || !Implements(objects, type, target) ||
                    Implements(objects, Parent(objects, type), target)) continue;
                if (code.Length - used < 21) return -3;
                used = Local(code, used, 0x20, 0);
                code[used++] = 0xfb;
                code[used++] = 0x14; // ref.test non-null class
                used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
                code[used++] = 0x04; // if
                code[used++] = 0x40;
                used = Const(code, used, 1);
                code[used++] = 0x0f; // return true
                code[used++] = 0x0b;
            }
            // Null succeeds for castclass and remains null for isinst.
            used = Local(code, used, 0x20, 0);
            code[used++] = 0xd1;
            code[used++] = 0x0b;
            return used;
        }
    }
}
