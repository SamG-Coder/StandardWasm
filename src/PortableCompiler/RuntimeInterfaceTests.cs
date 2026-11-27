using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal sealed class RuntimeInterfaceTests
    {
        private const int BoundPrimitivePlan = 1;
        private int[] heads, boxMatches;

        internal int Bind(BundleImages bundle, int[] objects, int type)
        {
            int owner = AssemblyOf(objects, type);
            if (owner < 0 || owner >= bundle.Count) return -2;
            int index = type - FirstClass;
            if (boxMatches != null && boxMatches[index] != 0) return 0;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0 ||
                MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
            if (HasArguments(objects, type)) return -2;

            int row = TypeDef(objects, type);
            if (!InterfaceImplementations.HasDefinitionReference(image, heaps, layout, row)) return 0;
            var required = new[] { owner, row };
            // These references use dedicated Wasm layouts. Until each has an
            // interface matcher, preserve the diagnostic if it could match.
            if (BundleConstraints.ImplementsCoreReference(bundle, objects, required, "String", "System") != 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "Array", "System") != 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "MulticastDelegate", "System") != 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "RuntimeType", "System") != 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "RuntimeAssembly", "System.Reflection") != 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "RuntimeModule", "System.Reflection") != 0)
                return -2;

            int matches = BoundPrimitivePlan;
            for (int element = 2; element < 32; element++)
            {
                int primitive = GenericScalar.Key(element);
                if (primitive == 0) continue;
                int result = BundleConstraints.Implements(bundle, objects, primitive, required, 0);
                if (result < 0) return result;
                if (result != 0) matches |= 1 << element;
            }
            if (boxMatches == null) boxMatches = new int[MaxClasses];
            boxMatches[index] = matches;
            return 0;
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
                int matches = boxMatches == null ? 0 : boxMatches[index] & ~BoundPrimitivePlan;
                int end = WriteTest(code, used, objects, FirstClass + index, matches);
                if (end < 0) return end;
                WasmIndexFixups.Resolve(code, heads[index], hostImports + functions);
                sizes[functions++] = end - used;
                used = end;
            }
            position[0] = used;
            return functions;
        }

        private static int WriteTest(byte[] code, int used, int[] objects, int target, int boxElements)
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
                used = WriteHeapMatch(code, used, type);
                if (used < 0) return used;
            }
            if (boxElements != 0)
            {
                int layout = PrimitiveBoxing.FindLayout(objects);
                if (layout != 0)
                {
                    used = WriteBoxTest(code, used, layout, boxElements);
                    if (used < 0) return used;
                }
            }
            // Null succeeds for castclass and remains null for isinst.
            if (code.Length - used < 4) return -3;
            used = Local(code, used, 0x20, 0);
            code[used++] = 0xd1;
            code[used++] = 0x0b;
            return used;
        }

        private static int WriteHeapMatch(byte[] code, int used, int type)
        {
            if (code.Length - used < 21) return -3;
            used = Local(code, used, 0x20, 0);
            code[used++] = 0xfb;
            code[used++] = 0x14; // ref.test non-null
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
            code[used++] = 0x04;
            code[used++] = 0x40;
            used = Const(code, used, 1);
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            return used;
        }

        private static int WriteBoxTest(byte[] code, int used, int layout, int elements)
        {
            if (code.Length - used < 12) return -3;
            used = PrimitiveBoxing.TestType(code, used, 0, layout);
            code[used++] = 0x04; // if: the tag field belongs to a primitive box
            code[used++] = 0x40;
            for (int element = 2; element < 32; element++)
            {
                if ((elements & (1 << element)) == 0) continue;
                if (code.Length - used < 32) return -3;
                used = PrimitiveBoxing.TestElement(code, used, 0, layout, element);
                code[used++] = 0x04;
                code[used++] = 0x40;
                used = Const(code, used, 1);
                code[used++] = 0x0f;
                code[used++] = 0x0b;
            }
            code[used++] = 0x0b;
            return used;
        }
    }
}
