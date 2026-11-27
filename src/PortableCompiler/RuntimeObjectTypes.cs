using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal sealed class RuntimeObjectTypes
    {
        internal readonly int[] Fixups = new int[1];
        private byte[] receivers;
        private bool anyClass, stringReceiver, interfaceReceiver;
        private int boxedElements, primitiveArrays, primitiveReceivers;

        internal void Include(int[] objects, int type)
        {
            if (type == ObjectReference) anyClass = true;
            if (type == String) stringReceiver = true;
            if (IsArray(type)) primitiveReceivers |= 1 << type;
            if (type < FirstClass) return;
            if (IsInterface(objects, type)) interfaceReceiver = true;
            if (receivers == null) receivers = new byte[MaxClasses];
            receivers[type - FirstClass] = 1;
        }

        internal void IncludeBox(int element) => boxedElements |= 1 << element;
        internal void IncludePrimitiveArray(int element) => primitiveArrays |= 1 << element;

        internal int Write(BundleImages bundle, byte[] code, int position, int[] objects, int[] globals,
            PrimitiveArrayWasm arrayHeaps, AggregateBoxPlan aggregateBoxes)
        {
            if (code.Length - position < 3) return -3;
            code[position++] = 0; // no locals besides the receiver
            // Parents precede children in the nominal GC group. Test the most
            // derived layouts first, even if linked after the GetType call.
            for (int index = objects[0] - 1; index >= 0; index--)
            {
                int type = FirstClass + index;
                if (IsValueType(objects, type) || IsInterface(objects, type) || PrimitiveBoxing.IsLayout(objects, type) ||
                    AggregateBoxing.IsLayout(objects, type) || !Includes(objects, type)) continue;
                int descriptor = RuntimeTypeTokens.BindLayout(bundle, objects, type, globals);
                if (descriptor < 0) return descriptor;
                position = HeapCase(code, position, WasmBinary.HeapType(type), descriptor);
                if (position < 0) return position;
            }
            if (anyClass || stringReceiver)
            {
                var identity = new int[2];
                int status = RuntimeTypeTokens.FindCoreType(bundle, "String", identity);
                if (status < 0 && (status != -6 || stringReceiver)) return status;
                if (status == 0)
                {
                    int descriptor = RuntimeTypeTokens.BindIdentity(globals, identity[0], identity[1]);
                    if (descriptor < 0) return descriptor;
                    position = HeapCase(code, position, WasmBinary.HeapType(String), descriptor);
                    if (position < 0) return position;
                }
            }
            if (anyClass)
            {
                int descriptor = RuntimeTypeTokens.BindIdentity(globals, AssemblyBundle.ObjectOwner, 0);
                if (descriptor < 0) return descriptor;
                position = HeapCase(code, position, WasmBinary.RootObjectHeap, descriptor);
                if (position < 0) return position;
                if (boxedElements != 0)
                {
                    position = BoxCases(bundle, code, position, objects, globals);
                    if (position < 0) return position;
                }
            }
            if ((anyClass || interfaceReceiver) && aggregateBoxes.Count != 0)
            {
                position = WriteAggregateTypes(code, position, objects, aggregateBoxes);
                if (position < 0) return position;
            }
            for (int element = PrimitiveArrayWasm.FirstElement; element < PrimitiveArrayWasm.EndElement; element++)
            {
                if ((primitiveArrays & (1 << element)) == 0 ||
                    (!anyClass && (primitiveReceivers & (1 << ArrayTypeRefs.Storage(element))) == 0)) continue;
                int descriptor = RuntimeTypeTokens.BindIdentity(globals, AssemblyBundle.ObjectOwner,
                    RuntimeTypeTokens.PrimitiveVectorKey + element);
                if (descriptor < 0) return descriptor;
                position = HeapCase(code, position, element, descriptor, arrayHeaps);
                if (position < 0) return position;
            }
            // Representations without exact runtime type identity must fail
            // rather than substituting the static Object type.
            code[position++] = 0x00;
            code[position++] = 0x0b;
            return position;
        }

        private static int WriteAggregateTypes(byte[] code, int position, int[] objects, AggregateBoxPlan boxes)
        {
            if (code.Length - position < 12) return -3;
            int layout = AggregateBoxing.FindLayout(objects);
            position = AggregateBoxing.TestCarrier(code, position, 0, layout);
            code[position++] = 0x04;
            code[position++] = 0x40;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (code.Length - position < 32) return -3;
                int descriptor = boxes.Descriptor(i);
                position = AggregateBoxing.TestDescriptor(code, position, 0, layout, descriptor);
                position = ReturnCase(code, position, descriptor);
            }
            code[position++] = 0x0b;
            return position;
        }

        private bool Includes(int[] objects, int type)
        {
            if (anyClass) return true;
            if (receivers == null) return false;
            for (int candidate = type; candidate != 0; candidate = Parent(objects, candidate))
                if (receivers[candidate - FirstClass] != 0) return true;
            if (interfaceReceiver)
                for (int candidate = 0; candidate < objects[0]; candidate++)
                    if (receivers[candidate] != 0 && Implements(objects, type, FirstClass + candidate)) return true;
            return false;
        }

        private static int HeapCase(byte[] code, int position, int heap, int descriptor, PrimitiveArrayWasm arrayHeaps = null)
        {
            if (code.Length - position < 24) return -3;
            position = Local(code, position, 0x20, 0);
            code[position++] = 0xfb;
            code[position++] = 0x14; // ref.test non-null
            position = arrayHeaps == null ? WasmBinary.Signed(code, position, heap) :
                arrayHeaps.WriteHeap(code, position, 0, heap);
            return ReturnCase(code, position, descriptor);
        }

        private int BoxCases(BundleImages bundle, byte[] code, int position, int[] objects, int[] globals)
        {
            int layout = PrimitiveBoxing.Layout(objects);
            if (layout < 0) return layout;
            var identity = new int[2];
            for (int element = 0; element < 32; element++)
            {
                if ((boxedElements & (1 << element)) == 0) continue;
                int found = BundleConstraints.FindPrimitive(bundle, GenericScalar.Key(element), identity);
                if (found < 0) return found;
                if (found == 0) continue;
                int descriptor = RuntimeTypeTokens.BindIdentity(globals, identity[0], identity[1]);
                if (descriptor < 0) return descriptor;
                if (code.Length - position < 42) return -3;
                position = PrimitiveBoxing.TestType(code, position, 0, layout);
                code[position++] = 0x04;
                code[position++] = 0x40;
                position = PrimitiveBoxing.TestElement(code, position, 0, layout, element);
                position = ReturnCase(code, position, descriptor);
                code[position++] = 0x0b;
            }
            return position;
        }

        private static int ReturnCase(byte[] code, int position, int descriptor)
        {
            code[position++] = 0x04;
            code[position++] = 0x40;
            code[position++] = 0x23; // global.get
            position = WasmBinary.Unsigned(code, position, descriptor);
            code[position++] = 0x0f;
            code[position++] = 0x0b;
            return position;
        }
    }
}
