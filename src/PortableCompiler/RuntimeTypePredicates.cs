using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class RuntimeTypePredicates
    {
        internal static int Write(BundleImages bundle, byte[] code, int used, int[] objects, int[] globals, int operation)
        {
            if (code.Length - used < 3) return -3;
            code[used++] = 0; // no local declarations
            var enumIdentity = new int[2];
            var valueIdentity = new int[2];
            if (operation != BranchIl.RuntimeTypeIsGeneric)
            {
                int status = RuntimeTypeTokens.FindCoreType(bundle, "Enum", enumIdentity);
                if (status < 0) return status;
                if (operation == BranchIl.RuntimeTypeIsValue)
                {
                    status = RuntimeTypeTokens.FindCoreType(bundle, "ValueType", valueIdentity);
                    if (status < 0) return status;
                }
            }
            var layout = new int[4];
            var heaps = new int[8];
            var parent = new int[2];
            for (int index = 0; index < globals[0]; index++)
            {
                int owner = StaticFields.Owner(globals, index), row = StaticFields.Row(globals, index);
                if (!RuntimeTypeTokens.IsDescriptorOwner(owner) || row < 0) continue;
                int module = RuntimeTypeTokens.ModuleSlot(owner) - 1;
                int result = operation == BranchIl.RuntimeTypeIsGeneric ?
                    RuntimeTypeGenericKind.Definition(bundle, objects, module, row, layout, heaps) :
                    ValueKind(bundle, objects, module, row, layout, heaps, enumIdentity, valueIdentity, parent, operation);
                if (result == -1) return -1;
                if (result < 0) continue;
                if (code.Length - used < 18) return -3;
                used = Local(code, used, 0x20, 0);
                code[used++] = 0x23; // global.get
                used = WasmBinary.Unsigned(code, used, index);
                code[used++] = 0xd3; // ref.eq
                code[used++] = 0x04;
                code[used++] = 0x40;
                used = Const(code, used, result > 0 ? 1 : 0);
                code[used++] = 0x0f;
                code[used++] = 0x0b;
            }
            code[used++] = 0x00; // An unrepresented descriptor cannot establish a result.
            code[used++] = 0x0b;
            return used;
        }

        private static int ValueKind(BundleImages bundle, int[] objects, int owner, int row, int[] layout, int[] heaps,
            int[] enumIdentity, int[] valueIdentity, int[] parent, int operation)
        {
            if (row >= RuntimeTypeTokens.PrimitiveVectorKey)
                return owner == AssemblyBundle.ObjectOwner &&
                    ArrayTypeRefs.Storage(row - RuntimeTypeTokens.PrimitiveVectorKey) != 0 ? 0 : -2;
            if (row >= RuntimeTypeTokens.ClosedTypeKey)
            {
                int type = row - RuntimeTypeTokens.ClosedTypeKey;
                if (type < FirstClass || type - FirstClass >= objects[0]) return -2;
                if (ArrayElementKey(objects, type) != 0) return 0;
                if (AssemblyOf(objects, type) != owner) return -2;
                row = TypeDef(objects, type);
            }
            if (owner == AssemblyBundle.ObjectOwner) return row == 0 ? 0 : -2;
            if (owner < 0 || owner >= bundle.Count) return -2;
            byte[] image = bundle.Get(owner);
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int baseType = TypeDefinitionBase(image, heaps, layout, row);
            if (baseType <= 0) return baseType;

            // Actual value types extend genuine ValueType or Enum. Enum itself
            // is a reference type even though its parent is ValueType.
            bool constructed = (baseType & 3) == 2;
            if (constructed)
            {
                baseType = GenericBase(image, layout, heaps, baseType >> 2);
                if (baseType < 0) return baseType;
            }
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, baseType, parent);
            if (status < 0) return status;
            if (parent[0] == enumIdentity[0] && parent[1] == enumIdentity[1]) return constructed ? -2 : 1;
            if (operation == BranchIl.RuntimeTypeIsEnum ||
                parent[0] != valueIdentity[0] || parent[1] != valueIdentity[1] ||
                owner == enumIdentity[0] && row == enumIdentity[1]) return 0;
            return constructed ? -2 : 1;
        }

        private static int GenericBase(byte[] image, int[] layout, int[] heaps, int row)
        {
            int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, row);
            if (blob < 0) return blob;
            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = ImageBytes.CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int start = cursor[0];
            if (size < 4 || size > limit - start || image[start] != 0x15 || image[start + 1] != 0x12)
                return -2;
            int end = start + size;
            cursor[0] = start + 1;
            var head = new int[2];
            if (SignatureReader.ReadGenericHeader(image, cursor, end, head) < 1 ||
                SignatureExtents.Type(image, start, end) != end) return -1;
            int target = head[0], tag = target & 3;
            if (tag > 1) return -2;
            return tag == 0 && !RuntimeTypeMetadata.ValidDefinition(image, heaps, target >> 2) ? -1 : target;
        }
    }
}
