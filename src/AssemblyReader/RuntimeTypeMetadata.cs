using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class RuntimeTypeMetadata
    {
        internal const int Type = NativeStorage.Vector + 1, Handle = Type + 1, Module = Handle + 1, Assembly = Module + 1;

        internal static bool IsStorage(int type) => type == Handle || IsReference(type);
        internal static bool IsReference(int type) => type == Type || type == Module || type == Assembly;

        internal static int Storage(byte[] image, int[] heaps, int[] layout, int coded, bool valueType)
        {
            int kind = Kind(image, heaps, layout, coded);
            if (valueType) return kind == PlatformTypeReferences.TypeHandle ? Handle : 0;
            if (kind == PlatformTypeReferences.Type) return Type;
            if (kind == PlatformTypeReferences.Module) return Module;
            return kind == PlatformTypeReferences.Assembly ? Assembly : 0;
        }

        internal static bool RequiresReflectionCast(byte[] image, int[] heaps, int[] layout, int coded)
        {
            int kind = Kind(image, heaps, layout, coded);
            return kind == PlatformTypeReferences.Type || kind == PlatformTypeReferences.TypeBase ||
                kind == PlatformTypeReferences.Module || kind == PlatformTypeReferences.Assembly;
        }

        private static int Kind(byte[] image, int[] heaps, int[] layout, int coded)
        {
            if ((coded & 3) != 1) return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first < 0 ? 0 : PlatformTypeReferences.Identify(image, heaps, layout, counts, first, coded >> 2);
        }

        // CoreLib signatures can name either Assembly declaration by TypeDef.
        // Runtime-created instances share the canonical Assembly descriptor.
        internal static bool IsAssemblyDefinition(byte[] image, int[] heaps, int[] layout, int coded, bool includeRuntimeAssembly = true)
        {
            if ((coded & 3) != 0) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                (ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, coded >> 2, "System.Reflection", "Assembly") ||
                    includeRuntimeAssembly && ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, coded >> 2,
                        "System.Reflection", "RuntimeAssembly"));
        }

        internal static bool IsRuntimeTypeDefinition(byte[] image, int[] heaps, int[] layout, int coded)
        {
            if ((coded & 3) != 0) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, coded >> 2, "System", "RuntimeType");
        }

        internal const int GenericKind = 1, GenericDefinition = 2, GenericArguments = 3, EnumKind = 4, ValueKind = 5;

        internal static int CoreTypeDescriptorOperation(byte[] image, int[] heaps, int[] layout, int row)
        {
            int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int access = flags & 0x2057;
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            int operation;
            if (access == 6 && (flags & 0x0800) != 0 && MatchesAscii(image, heaps, name, "get_IsValueType"))
                operation = ValueKind;
            else if (access != 0x0046) return 0;
            else if ((flags & 0x0800) != 0)
                operation = MatchesAscii(image, heaps, name, "get_IsGenericType") ? GenericKind :
                    MatchesAscii(image, heaps, name, "get_IsEnum") ? EnumKind : 0;
            else operation = MatchesAscii(image, heaps, name, "GetGenericTypeDefinition") ? GenericDefinition :
                MatchesAscii(image, heaps, name, "GetGenericArguments") ? GenericArguments : 0;
            if (operation == 0) return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
            int declaring = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (!ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, declaring, "System", "Type")) return 0;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 0, cursor);
            if (end < 0) return 0;
            if (operation == GenericArguments && !SignatureReader.TakeElement(image, cursor, end, 0x1d)) return 0;
            bool result = operation == GenericKind || operation == EnumKind || operation == ValueKind ?
                SignatureReader.TakeElement(image, cursor, end, SignatureReader.Boolean) :
                SignatureReader.ReadClass(image, cursor, end) == declaring << 2;
            return result && cursor[0] == end &&
                (operation != ValueKind || IsValueKindWrapper(image, layout, heaps, counts, declaring, row)) ? operation : 0;
        }

        internal static bool ValidDefinition(byte[] image, int[] heaps, int row)
        {
            // TypeDef row 1 is the module pseudo-type and has no runtime Type.
            if (row < 2) return false;
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 && row <= counts[2];
        }

        // Match the platform Object.GetType or a Type comparison operator.
        internal static int ObjectTypeMember(byte[] image, int[] heaps, int[] layout, int row)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                member[AssemblyReferences.Depth] != 0) return 0;
            int leaf = member[AssemblyReferences.Leaf];
            uint name = (uint)member[4];
            bool getType = MatchesAscii(image, heaps, name, "GetType") &&
                ObjectLayouts.IsCoreObjectType(image, heaps, layout, leaf << 2 | 1);
            bool equality = MatchesAscii(image, heaps, name, "op_Equality");
            bool inequality = MatchesAscii(image, heaps, name, "op_Inequality");
            if (!getType && !equality && !inequality) return 0;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !getType &&
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, leaf) != PlatformTypeReferences.Type) return 0;
            int limit = heaps[0] + heaps[1], blob = member[5];
            if (blob <= 0 || blob >= heaps[1]) return 0;
            var next = new int[1];
            int bytes = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (bytes < 4 || bytes > limit - start) return 0;
            int end = start + bytes, cursor = start + 2;
            if (getType)
            {
                if (image[start] != 0x20 || image[start + 1] != 0) return 0;
            }
            else
            {
                if (image[start] != 0 || image[start + 1] != 2 || image[cursor] != 2) return 0;
                cursor++;
            }
            int types = getType ? 1 : 2;
            for (int index = 0; index < types; index++)
            {
                if (cursor >= end || image[cursor++] != 0x12) return 0;
                int token = CompressedUnsigned(image, cursor, end, next);
                if ((token & 3) != 1 || PlatformTypeReferences.Identify(image, heaps, layout, counts, first, token >> 2) != PlatformTypeReferences.Type)
                    return 0;
                cursor = next[0];
            }
            return cursor != end ? 0 : getType ? 1 : equality ? 2 : 3;
        }

        // CoreLib calls its own Type operators by MethodDef. Match the same
        // canonical type signature as the MemberRef path.
        internal static int CoreTypeComparison(byte[] image, int[] heaps, int[] layout, int row)
        {
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2450) != 0x10) return 0;
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            bool equal = MatchesAscii(image, heaps, name, "op_Equality");
            if (!equal && !MatchesAscii(image, heaps, name, "op_Inequality")) return 0;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "Type")) return 0;

            var cursor = new int[1];
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int end = SignatureReader.ReadStatic(image, heaps, blob, 2, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Boolean)) return 0;
            for (int argument = 0; argument < 2; argument++)
                if (SignatureReader.ReadClass(image, cursor, end) != owner << 2) return 0;
            return cursor[0] == end ? equal ? 2 : 3 : 0;
        }

        // Runtime descriptors dispatch ToString through the canonical Object slot.
        internal static bool IsCoreObjectToString(byte[] image, int[] heaps, int[] layout, int row)
        {
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x50) != 0x40 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "ToString")) return false;
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob < 1 || !Fits((uint)blob, 4, (uint)heaps[1]) ||
                U32(image, (uint)(heaps[0] + blob)) != 0x0e002003) return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            return owner > 0 && ObjectLayouts.IsCoreObjectType(image, heaps, layout, owner << 2);
        }

        // CoreLib itself calls Object.GetType by MethodDef rather than MemberRef.
        internal static bool IsCoreObjectGetType(byte[] image, int[] heaps, int[] layout, int row)
        {
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x50) != 0 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetType")) return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 1 || !ObjectLayouts.IsCoreObjectType(image, heaps, layout, owner << 2)) return false;

            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4), limit = heaps[0] + heaps[1];
            if (blob <= 0 || blob >= heaps[1]) return false;
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 4 || size > limit - start || image[start] != 0x20 ||
                image[start + 1] != 0 || image[start + 2] != 0x12) return false;
            int end = start + size;
            int result = CompressedUnsigned(image, start + 3, end, next);
            if (result <= 0 || (result & 3) != 0 || next[0] != end) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && result >> 2 <= counts[2] &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, result >> 2, "System", "Type");
        }

        internal static bool IsGetTypeFromHandle(byte[] image, int[] heaps, int[] layout, int row)
        {
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 || member[AssemblyReferences.Depth] != 0 ||
                !PlatformTypeReferences.Named17(image, (uint)heaps[2], (uint)heaps[3], (uint)member[4],
                    0x54746547, 0x46657079, 0x486d6f72, 0x6c646e61, 0x65)) // GetTypeFromHandle
                return false;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || PlatformTypeReferences.Identify(image, heaps, layout, counts, first, member[AssemblyReferences.Leaf]) != PlatformTypeReferences.Type)
                return false;

            int blob = member[5], limit = heaps[0] + heaps[1];
            if (blob <= 0 || blob >= heaps[1]) return false;
            var next = new int[1];
            int bytes = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (bytes < 6 || bytes > limit - start || image[start] != 0 || image[start + 1] != 1 || image[start + 2] != 0x12)
                return false;
            int end = start + bytes;
            int result = CompressedUnsigned(image, start + 3, end, next);
            int cursor = next[0];
            if ((result & 3) != 1 || cursor >= end || image[cursor] != 0x11 ||
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, result >> 2) != PlatformTypeReferences.Type)
                return false;
            int parameter = CompressedUnsigned(image, cursor + 1, end, next);
            return next[0] == end && (parameter & 3) == 1 &&
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, parameter >> 2) == PlatformTypeReferences.TypeHandle;
        }
        internal static bool IsCoreGetTypeFromHandle(byte[] image, int[] heaps, int[] layout, int row)
        {
            if (!MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetTypeFromHandle")) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "Type")) return false;

            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4), limit = heaps[0] + heaps[1];
            if (blob <= 0 || blob >= heaps[1]) return false;
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int start = next[0];
            if (size < 6 || size > limit - start || image[start] != 0 || image[start + 1] != 1 || image[start + 2] != 0x12)
                return false;
            int end = start + size;
            int result = CompressedUnsigned(image, start + 3, end, next);
            int cursor = next[0];
            if (result != owner << 2 || cursor >= end || image[cursor] != 0x11) return false;
            int parameter = CompressedUnsigned(image, cursor + 1, end, next);
            return parameter > 0 && (parameter & 3) == 0 && next[0] == end &&
                ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, parameter >> 2, "System", "RuntimeTypeHandle");
        }
        internal static bool IsMakeGenericType(byte[] image, int[] heaps, int[] layout, int row, bool definition)
        {
            int signature, declaring;
            if (definition)
            {
                if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2050) != 0x40 ||
                    !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "MakeGenericType"))
                    return false;
                declaring = ObjectLayouts.Owner(image, heaps, layout, row, false);
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    member[AssemblyReferences.Depth] != 0 ||
                    !MatchesAscii(image, heaps, (uint)member[4], "MakeGenericType")) return false;
                declaring = member[AssemblyReferences.Leaf];
                signature = member[5];
            }

            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, signature, 1, cursor);
            int result = end < 0 ? -1 : SignatureReader.ReadClass(image, cursor, end);
            if (declaring < 1 || result < 1 ||
                !SignatureReader.TakeElement(image, cursor, end, 0x1d)) return false;
            int element = SignatureReader.ReadClass(image, cursor, end);
            int tag = definition ? 0 : 1;
            int declaringToken = declaring << 2 | tag;
            if (result != declaringToken || element != declaringToken || cursor[0] != end) return false;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return false;
            return definition
                ? PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                    ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, declaring, "System", "Type")
                : PlatformTypeReferences.Identify(image, heaps, layout, counts, first, declaring) == PlatformTypeReferences.Type;
        }

        internal static bool IsTypeAssignableFrom(byte[] image, int[] heaps, int[] layout, int row, bool definition) =>
            IsTypeAssignability(image, heaps, layout, row, definition, false);

        internal static bool IsTypeAssignableTo(byte[] image, int[] heaps, int[] layout, int row, bool definition) =>
            IsTypeAssignability(image, heaps, layout, row, definition, true);

        private static bool IsTypeAssignability(byte[] image, int[] heaps, int[] layout, int row, bool definition, bool to)
        {
            int signature, declaring;
            string name = to ? "IsAssignableTo" : "IsAssignableFrom";
            if (definition)
            {
                if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & (to ? 0x2450 : 0x2050)) != (to ? 0 : 0x40) ||
                    !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name))
                    return false;
                declaring = ObjectLayouts.Owner(image, heaps, layout, row, false);
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    member[AssemblyReferences.Depth] != 0 ||
                    !MatchesAscii(image, heaps, (uint)member[4], name)) return false;
                declaring = member[AssemblyReferences.Leaf];
                signature = member[5];
            }

            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, signature, 1, cursor);
            if (declaring < 1 || end < 0 ||
                !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Boolean)) return false;
            int argument = SignatureReader.ReadClass(image, cursor, end);
            if (argument < 1 || cursor[0] != end) return false;

            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return false;
            if (definition)
                return argument == declaring << 2 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                    ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, declaring, "System", "Type");
            return (argument & 3) == 1 &&
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, declaring) == PlatformTypeReferences.Type &&
                PlatformTypeReferences.Identify(image, heaps, layout, counts, first, argument >> 2) == PlatformTypeReferences.Type;
        }

        internal static bool IsTypeHandleGetter(byte[] image, int[] heaps, int[] layout, int row, bool definition)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return false;
            int blob;
            if (definition)
            {
                if (row < 1 || row > counts[6] ||
                    (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2050) != 0x40 ||
                    !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "get_TypeHandle") ||
                    !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "Type"))
                    return false;
                blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                    member[AssemblyReferences.Depth] != 0 ||
                    !MatchesAscii(image, heaps, (uint)member[4], "get_TypeHandle") ||
                    PlatformTypeReferences.Identify(image, heaps, layout, counts, first, member[AssemblyReferences.Leaf]) !=
                        PlatformTypeReferences.Type) return false;
                blob = member[5];
            }

            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, blob, 0, cursor);
            if (end < 0 || !SignatureReader.TakeElement(image, cursor, end, 0x11)) return false;
            int result = CompressedUnsigned(image, cursor[0], end, cursor);
            if (result <= 0 || cursor[0] != end) return false;
            int handle = result >> 2;
            return definition
                ? (result & 3) == 0 && handle <= counts[2] &&
                    ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, handle, "System", "RuntimeTypeHandle")
                : (result & 3) == 1 &&
                    PlatformTypeReferences.Identify(image, heaps, layout, counts, first, handle) == PlatformTypeReferences.TypeHandle;
        }

        private static bool IsValueKindWrapper(byte[] image, int[] layout, int[] heaps, int[] counts, int type, int method)
        {
            // The original nonvirtual getter is only a call to its protected virtual slot.
            var body = new int[6];
            uint rva = (uint)MethodDefinitions.ReadColumn(image, layout, method, 0);
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] != 7 || body[3] != 0 || body[5] != 0) return false;
            int at = body[0];
            if (U16(image, (uint)at) != 0x6f02 || image[at + 6] != 0x2a) return false;
            uint token = U32(image, (uint)(at + 2));
            int row = (int)(token & 0xffffff);
            if ((token >> 24) != 6 || row < 1 || row > counts[6] ||
                ObjectLayouts.Owner(image, heaps, layout, row, false) != type ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2057) != 0x0044 ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "IsValueTypeImpl"))
                return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 4), 0, cursor);
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, SignatureReader.Boolean) && cursor[0] == end;
        }

        internal const int ValueTypeProperty = 1, EnumProperty = 2;

        internal static int ClosedTypeProperty(byte[] image, int[] heaps, int[] layout, int row, bool definition)
        {
            int signature, name;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return 0;
            if (definition)
            {
                if (row < 1 || row > counts[6] || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 1 || !ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, owner, "System", "Type")) return 0;
                name = MethodDefinitions.ReadColumn(image, layout, row, 3);
                signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            }
            else
            {
                var member = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 || member[AssemblyReferences.Depth] != 0 ||
                    PlatformTypeReferences.Identify(image, heaps, layout, counts, first, member[AssemblyReferences.Leaf]) != PlatformTypeReferences.Type)
                    return 0;
                name = member[4];
                signature = member[5];
            }
            if (signature < 1 || !Fits((uint)signature, 4, (uint)heaps[1]) ||
                U32(image, (uint)(heaps[0] + signature)) != 0x02002003) return 0; // instance bool (); length prefix 3
            if (MatchesAscii(image, heaps, (uint)name, "get_IsValueType")) return ValueTypeProperty;
            return MatchesAscii(image, heaps, (uint)name, "get_IsEnum") ? EnumProperty : 0;
        }
    }
}
