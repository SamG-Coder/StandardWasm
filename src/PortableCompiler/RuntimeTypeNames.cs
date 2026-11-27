using PeLibrary;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    // Runtime Type descriptors are canonical globals. Read the actual name
    // from linked metadata and cache the formatted String for each descriptor.
    internal static class RuntimeTypeNames
    {
        internal const int Display = 0, Simple = 1, Namespace = 2;
        private const int CacheOwner = int.MaxValue - 1;

        internal static int Write(BundleImages bundle, byte[] code, int used, int[] objects,
            int[] globals, byte[] literals, int[] literalSize, int form)
        {
            if (code.Length - used < 3) return -3;
            code[used++] = 0; // no locals besides the descriptor parameter
            int descriptors = globals[0];
            for (int index = 0; index < descriptors; index++)
            {
                int owner = StaticFields.Owner(globals, index);
                int row = StaticFields.Row(globals, index);
                if (!RuntimeTypeTokens.IsDescriptorOwner(owner) || row < 0) continue;
                int start = literalSize[0];
                int module = RuntimeTypeTokens.ModuleSlot(owner) - 1;
                int end = form == Namespace ? AppendNamespace(bundle, objects, module, row, literals, start, 0) :
                    AppendIdentity(bundle, objects, module, row, literals, start, 0, form == Simple);
                if (end < 0) return end;
                if (code.Length - used < 76) return -3;
                used = Local(code, used, 0x20, 0);
                used = Global(code, used, 0x23, index);
                code[used++] = 0xd3; // ref.eq with the canonical descriptor
                code[used++] = 0x04;
                code[used++] = 0x40;
                if (form == Namespace && end == start)
                {
                    code[used++] = 0xd0; // ref.null
                    code[used++] = StackSpillWasm.NullRef;
                    code[used++] = 0x0f;
                    code[used++] = 0x0b;
                    continue;
                }
                int cache = StaticFields.Register(globals, CacheOwner, index, SignatureTypes.String, form);
                if (cache < 0) return cache;
                used = Global(code, used, 0x23, cache);
                code[used++] = 0xd1; // ref.is_null
                code[used++] = 0x04;
                code[used++] = 0x40;
                used = Const(code, used, start);
                used = Const(code, used, (end - start) / 2);
                code[used++] = 0xfb;
                code[used++] = 9; // array.new_data from the passive UTF-16 section
                code[used++] = 4;
                code[used++] = 0;
                used = StringWasm.Wrap(code, used);
                used = Global(code, used, 0x24, cache);
                code[used++] = 0x0b;
                used = Global(code, used, 0x23, cache);
                code[used++] = 0x0f;
                code[used++] = 0x0b;
                literalSize[0] = end;
            }
            code[used++] = 0; // only linker-owned Type descriptors can use the helper
            code[used++] = 0x0b;
            return used;
        }

        private static int Global(byte[] code, int used, byte op, int slot)
        {
            code[used++] = op;
            return WasmBinary.Unsigned(code, used, slot);
        }

        private static int AppendNamespace(BundleImages bundle, int[] objects, int owner, int row,
            byte[] data, int used, int depth)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            if (row >= RuntimeTypeTokens.PrimitiveVectorKey || owner == AssemblyBundle.ObjectOwner && row == 0)
                return Text(data, used, "System");
            if (row >= RuntimeTypeTokens.ClosedTypeKey)
            {
                int type = row - RuntimeTypeTokens.ClosedTypeKey;
                int element = ObjectLayouts.ArrayElementKey(objects, type);
                if (element != 0)
                {
                    if (GenericScalar.IsScalar(element) || element == SignatureTypes.String)
                        return Text(data, used, "System");
                    var identity = new int[2];
                    int status = RuntimeTypeTokens.ReadClosedIdentity(bundle, objects, element, identity);
                    return status < 0 ? status :
                        AppendNamespace(bundle, objects, identity[0], identity[1], data, used, depth + 1);
                }
                row = ObjectLayouts.TypeDef(objects, type);
            }
            return AppendDefinition(bundle, owner, row, data, used, false, false, true);
        }

        private static int AppendIdentity(BundleImages bundle, int[] objects, int owner, int row,
            byte[] data, int used, int depth, bool simple)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            if (row >= RuntimeTypeTokens.PrimitiveVectorKey)
            {
                used = Primitive(row - RuntimeTypeTokens.PrimitiveVectorKey, data, used, simple);
                return Text(data, used, "[]");
            }
            if (row >= RuntimeTypeTokens.ClosedTypeKey)
            {
                int type = row - RuntimeTypeTokens.ClosedTypeKey;
                int element = ObjectLayouts.ArrayElementKey(objects, type);
                if (element != 0)
                {
                    used = AppendType(bundle, objects, element, data, used, depth + 1, simple);
                    return Text(data, used, "[]");
                }
                used = AppendDefinition(bundle, owner, ObjectLayouts.TypeDef(objects, type), data, used, false, simple);
                if (simple) return used;
                used = Text(data, used, "[");
                for (int index = 0; used >= 0; index++)
                {
                    int argument = ObjectLayouts.Argument(objects, type, index);
                    if (argument < 0) break;
                    if (index != 0) used = Text(data, used, ",");
                    used = AppendType(bundle, objects, argument, data, used, depth + 1, simple);
                }
                return Text(data, used, "]");
            }
            return owner == AssemblyBundle.ObjectOwner && row == 0
                ? Text(data, used, simple ? "Object" : "System.Object") : AppendDefinition(bundle, owner, row, data, used, true, simple);
        }

        private static int AppendType(BundleImages bundle, int[] objects, int type, byte[] data, int used, int depth, bool simple)
        {
            if (used < 0) return used;
            if (GenericScalar.IsScalar(type)) return Primitive(GenericScalar.Element(type), data, used, simple);
            if (type == SignatureTypes.String) return Text(data, used, simple ? "String" : "System.String");
            var identity = new int[2];
            int status = RuntimeTypeTokens.ReadClosedIdentity(bundle, objects, type, identity);
            return status < 0 ? status : AppendIdentity(bundle, objects, identity[0], identity[1], data, used, depth, simple);
        }

        private static int AppendDefinition(BundleImages bundle, int owner, int row, byte[] data, int used, bool open, bool simple,
            bool namespaceOnly = false)
        {
            if (used < 0) return used;
            if (owner < 0 || owner >= bundle.Count) return -2;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 || row < 2 || row > counts[2]) return -1;
            if (namespaceOnly)
            {
                // CLR namespace comes from the outer TypeDef, even if an
                // inner TypeDef has its own encoded namespace.
                for (int depth = 0; depth < ObjectLayouts.MaxTypeDepth; depth++)
                {
                    int parent = NestedTypes.Enclosing(image, heaps, layout, counts, row);
                    if (parent < 0) return parent;
                    if (parent == 0)
                    {
                        var names = new int[2];
                        if (ObjectLayouts.TypeDefinitionNames(image, heaps, layout, row, names) < 0) return -1;
                        return Metadata(image, heaps, names[1], data, used, false);
                    }
                    row = parent;
                }
                return -2;
            }
            if (simple)
            {
                var names = new int[2];
                if (ObjectLayouts.TypeDefinitionNames(image, heaps, layout, row, names) < 0) return -1;
                return Metadata(image, heaps, names[0], data, used);
            }
            used = AppendNested(image, layout, heaps, counts, row, data, used, 0);
            if (used < 0 || !open) return used;
            int[] parameters = GenericParameters.TypeParameterNames(image, heaps, layout, counts, row);
            if (parameters == null) return -1;
            if (parameters.Length == 0) return used;
            used = Text(data, used, "[");
            for (int i = 0; i < parameters.Length && used >= 0; i++)
            {
                if (i != 0) used = Text(data, used, ",");
                used = Metadata(image, heaps, parameters[i], data, used);
            }
            return Text(data, used, "]");
        }

        private static int AppendNested(byte[] image, int[] layout, int[] heaps, int[] counts,
            int row, byte[] data, int used, int depth)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            int parent = NestedTypes.Enclosing(image, heaps, layout, counts, row);
            if (parent < 0) return parent;
            var names = new int[2];
            if (ObjectLayouts.TypeDefinitionNames(image, heaps, layout, row, names) < 0) return -1;
            if (parent != 0)
                used = Text(data, AppendNested(image, layout, heaps, counts, parent, data, used, depth + 1), "+");
            else if (names[1] != 0)
            {
                int end = Metadata(image, heaps, names[1], data, used);
                used = end == used ? used : Text(data, end, ".");
            }
            return Metadata(image, heaps, names[0], data, used);
        }

        private static int Metadata(byte[] image, int[] heaps, int index, byte[] data, int used, bool escape = true)
        {
            if (used < 0) return used;
            if (index < 0 || index >= heaps[3]) return -1;
            int cursor = heaps[2] + index, end = cursor, limit = heaps[2] + heaps[3];
            while (end < limit && image[end] != 0) end++;
            if (end == limit) return -1;
            var next = new int[1];
            while (cursor < end && used >= 0)
            {
                int scalar = Utf8Scalars.Read(image, cursor, end, next);
                if (scalar < 0) return scalar;
                cursor = next[0];
                if (escape && (scalar == '\\' || scalar == '+' || scalar == ',' || scalar == '[' ||
                    scalar == ']' || scalar == '&' || scalar == '*')) used = Put(data, used, '\\');
                if (scalar <= 0xffff) used = Put(data, used, scalar);
                else
                {
                    scalar -= 0x10000;
                    used = Put(data, used, 0xd800 + (scalar >> 10));
                    used = Put(data, used, 0xdc00 + (scalar & 0x3ff));
                }
            }
            return used;
        }

        private static int Primitive(int element, byte[] data, int used, bool simple)
        {
            string name;
            switch (element)
            {
                case 2: name = "Boolean"; break;
                case 3: name = "Char"; break;
                case 4: name = "SByte"; break;
                case 5: name = "Byte"; break;
                case 6: name = "Int16"; break;
                case 7: name = "UInt16"; break;
                case 8: name = "Int32"; break;
                case 9: name = "UInt32"; break;
                case 10: name = "Int64"; break;
                case 11: name = "UInt64"; break;
                case 12: name = "Single"; break;
                case 13: name = "Double"; break;
                case 24: name = "IntPtr"; break;
                case 25: name = "UIntPtr"; break;
                default: return -2;
            }
            return Text(data, simple ? used : Text(data, used, "System."), name);
        }

        private static int Text(byte[] data, int used, string text)
        {
            for (int index = 0; index < text.Length && used >= 0; index++)
                used = Put(data, used, text[index]);
            return used;
        }

        private static int Put(byte[] data, int used, int unit)
        {
            if (used < 0) return used;
            if (used > data.Length - 2) return -3;
            data[used] = unchecked((byte)unit);
            data[used + 1] = unchecked((byte)(unit >> 8));
            return used + 2;
        }
    }
}
