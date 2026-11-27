using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class RuntimeTypeGenericKind
    {
        internal static bool IsDescriptorReceiver(int type) => type == RuntimeTypeMetadata.Type || type == NullValue;

        internal static int Write(BundleImages bundle, byte[] code, int used, int[] objects, int[] globals)
        {
            if (code.Length - used < 3) return -3;
            code[used++] = 0; // no local declarations
            var layout = new int[4];
            var heaps = new int[8];
            for (int index = 0; index < globals[0]; index++)
            {
                int owner = StaticFields.Owner(globals, index), row = StaticFields.Row(globals, index);
                if (!RuntimeTypeTokens.IsDescriptorOwner(owner) || row < 0) continue;
                int module = RuntimeTypeTokens.ModuleSlot(owner) - 1;
                int definition = Definition(bundle, objects, module, row, layout, heaps);
                if (definition == -1) return -1;
                if (definition < 0) continue;
                if (code.Length - used < 18) return -3;
                used = Local(code, used, 0x20, 0);
                code[used++] = 0x23; // global.get
                used = WasmBinary.Unsigned(code, used, index);
                code[used++] = 0xd3; // ref.eq
                code[used++] = 0x04;
                code[used++] = 0x40;
                used = Const(code, used, definition > 0 ? 1 : 0);
                code[used++] = 0x0f;
                code[used++] = 0x0b;
            }
            code[used++] = 0x00; // An unrepresented descriptor cannot establish a result.
            code[used++] = 0x0b;
            return used;
        }

        internal static int Definition(BundleImages bundle, int[] objects, int owner, int row, int[] layout, int[] heaps)
        {
            if (row >= RuntimeTypeTokens.PrimitiveVectorKey)
                return owner == AssemblyBundle.ObjectOwner && ArrayTypeRefs.Storage(row - RuntimeTypeTokens.PrimitiveVectorKey) != 0 ? 0 : -2;
            if (row >= RuntimeTypeTokens.ClosedTypeKey)
            {
                int type = row - RuntimeTypeTokens.ClosedTypeKey;
                if (type < FirstClass || type - FirstClass >= objects[0]) return -2;
                // An array remains nongeneric even if the element is generic.
                if (ArrayElementKey(objects, type) != 0) return 0;
                if (AssemblyOf(objects, type) != owner || !HasArguments(objects, type)) return -2;
                return TypeDef(objects, type);
            }
            if (owner == AssemblyBundle.ObjectOwner) return row == 0 ? 0 : -2;
            if (owner < 0 || owner >= bundle.Count) return -2;
            byte[] image = bundle.Get(owner);
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 || row < 2 || row > counts[2]) return -1;
            // Generic definitions, including nested types that inherit outer
            // parameters, are identified by GenericParam ownership.
            int[] parameters = GenericParameters.TypeParameterNames(image, heaps, layout, counts, row);
            return parameters == null ? -1 : parameters.Length == 0 ? 0 : row;
        }
    }
}
