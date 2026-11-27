using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal sealed class RuntimeInterfaceTests
    {
        private const int BoundPlan = 1, StringMatch = 2;
        internal const int RequiresExactStackType = 1;
        private int[] heads, interfaceMatches;

        internal int Bind(BundleImages bundle, int[] objects, int type)
        {
            int owner = AssemblyOf(objects, type);
            if (owner < 0 || owner >= bundle.Count) return -2;
            int index = type - FirstClass;
            if (interfaceMatches != null && interfaceMatches[index] != 0) return 0;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return -1;
            if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;

            int row = TypeDef(objects, type);
            int arity = ArgumentCount(objects, type);
            if (arity == 0 && !InterfaceImplementations.HasDefinitionReference(image, heaps, layout, row)) return 0;
            var required = new int[2 + arity];
            required[0] = owner;
            required[1] = row;
            for (int argument = 0; argument < arity; argument++) required[argument + 2] = Argument(objects, type, argument);
            if (arity != 0 && !HasExactMembership(image, layout, heaps, counts, first, row, objects, required))
                return RequiresExactStackType;
            int stringMatch = BundleConstraints.ImplementsCoreReference(bundle, objects, required, "String", "System");
            // These other dedicated reference layouts still require their own matchers.
            if (stringMatch < 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "Array", "System") != 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "MulticastDelegate", "System") != 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "RuntimeType", "System") != 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "RuntimeAssembly", "System.Reflection") != 0 ||
                BundleConstraints.ImplementsCoreReference(bundle, objects, required, "RuntimeModule", "System.Reflection") != 0)
                return arity == 0 ? -2 : RequiresExactStackType;

            // Bits zero and one are reserved; primitive element keys start at two.
            int matches = BoundPlan | (stringMatch == 0 ? 0 : StringMatch);
            for (int element = 2; element < 32; element++)
            {
                int primitive = GenericScalar.Key(element);
                if (primitive == 0) continue;
                int result = BundleConstraints.Implements(bundle, objects, primitive, required, 0);
                if (result < 0) return arity != 0 && result == -2 ? RequiresExactStackType : result;
                if (result != 0) matches |= 1 << element;
            }
            if (interfaceMatches == null) interfaceMatches = new int[MaxClasses];
            interfaceMatches[index] = matches;
            return 0;
        }

        private static bool HasExactMembership(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row,
            int[] objects, int[] required)
        {
            if (IsSynthesizedVectorInterface(image, layout, heaps, counts, first, row)) return false;
            var variance = new int[required.Length - 2];
            if (GenericParameters.TypeParameterNames(image, heaps, layout, counts, row, variance) == null) return false;
            for (int index = 0; index < variance.Length; index++)
            {
                int argument = required[index + 2];
                // Boxing and scalar conversion do not participate in generic
                // variance. In a contravariant slot Object has no wider type.
                if (variance[index] == 0 || variance[index] == 2 && argument == ObjectReference ||
                    variance[index] != 3 && (GenericScalar.IsScalar(argument) || IsEnumKey(argument) ||
                        IsValueType(objects, argument))) continue;
                return false;
            }
            return true;
        }

        private static bool IsSynthesizedVectorInterface(byte[] image, int[] layout, int[] heaps, int[] counts, int first, int row)
        {
            const string space = "System.Collections.Generic";
            return NamedDefinition(image, heaps, layout, counts, first, row, space, "IEnumerable`1") ||
                NamedDefinition(image, heaps, layout, counts, first, row, space, "ICollection`1") ||
                NamedDefinition(image, heaps, layout, counts, first, row, space, "IList`1") ||
                NamedDefinition(image, heaps, layout, counts, first, row, space, "IReadOnlyCollection`1") ||
                NamedDefinition(image, heaps, layout, counts, first, row, space, "IReadOnlyList`1");
        }

        internal int Call(byte[] code, int used, int bodyStart, int type)
        {
            if (heads == null) heads = new int[MaxClasses];
            code[used++] = 0x10;
            return WasmIndexFixups.WriteAt(code, used, bodyStart, heads, type - FirstClass);
        }

        internal int Write(BundleImages bundle, byte[] code, int[] position, int[] objects, AggregateBoxPlan aggregateBoxes,
            bool enumBoxes, int[] offsets, int[] signatures,
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
                int matches = interfaceMatches == null ? 0 : interfaceMatches[index];
                int end = WriteTest(bundle, code, used, objects, FirstClass + index, matches, aggregateBoxes, enumBoxes);
                if (end < 0) return end;
                WasmIndexFixups.Resolve(code, heads[index], hostImports + functions);
                sizes[functions++] = end - used;
                used = end;
            }
            position[0] = used;
            return functions;
        }

        private static int WriteTest(BundleImages bundle, byte[] code, int used, int[] objects,
            int target, int matches, AggregateBoxPlan aggregateBoxes, bool enumBoxes)
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
            if ((matches & StringMatch) != 0)
            {
                used = WriteHeapMatch(code, used, String);
                if (used < 0) return used;
            }
            int boxElements = matches & ~(BoundPlan | StringMatch);
            if (boxElements != 0)
            {
                int layout = PrimitiveBoxing.FindLayout(objects);
                if (layout != 0)
                {
                    used = WriteBoxTest(code, used, layout, boxElements);
                    if (used < 0) return used;
                }
            }
            if (aggregateBoxes.Count != 0)
            {
                used = WriteAggregateBoxTest(bundle, code, used, objects, target, aggregateBoxes);
                if (used < 0) return used;
            }
            if (enumBoxes)
            {
                int implements = BoxedEnums.Implements(bundle, objects, target);
                if (implements < 0) return implements;
                if (implements != 0)
                {
                    used = WriteEnumTest(code, used, PrimitiveBoxing.FindLayout(objects));
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

        private static int WriteEnumTest(byte[] code, int used, int layout)
        {
            if (code.Length - used < 32) return -3;
            used = PrimitiveBoxing.TestType(code, used, 0, layout);
            code[used++] = 0x04;
            code[used++] = 0x40;
            used = PrimitiveBoxing.TestEnum(code, used, 0, layout);
            code[used++] = 0x04;
            code[used++] = 0x40;
            used = Const(code, used, 1);
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int WriteAggregateBoxTest(BundleImages bundle, byte[] code, int used, int[] objects,
            int target, AggregateBoxPlan boxes)
        {
            if (code.Length - used < 12) return -3;
            int layout = AggregateBoxing.FindLayout(objects);
            int parameters = ArgumentCount(objects, target);
            var required = new int[2 + parameters];
            required[0] = AssemblyOf(objects, target);
            required[1] = TypeDef(objects, target);
            for (int i = 0; i < parameters; i++) required[i + 2] = Argument(objects, target, i);

            used = AggregateBoxing.TestCarrier(code, used, 0, layout);
            code[used++] = 0x04;
            code[used++] = 0x40;
            for (int i = 0; i < boxes.Count; i++)
            {
                int match = BundleConstraints.Implements(bundle, objects, boxes.Type(i), required, 0);
                if (match < 0) return match;
                if (match == 0) continue;
                if (code.Length - used < 30) return -3;
                used = AggregateBoxing.TestDescriptor(code, used, 0, layout, boxes.Descriptor(i));
                code[used++] = 0x04;
                code[used++] = 0x40;
                used = Const(code, used, 1);
                code[used++] = 0x0f;
                code[used++] = 0x0b;
            }
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
