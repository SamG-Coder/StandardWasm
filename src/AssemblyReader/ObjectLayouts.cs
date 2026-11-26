using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class ObjectLayouts
    {
        internal const byte ObjectReference = 12, EventArgsReference = 13, EventHandlerReference = 14;
        internal const int FirstClass = ConsoleStorage.Writer + 1, MaxClasses = 256 - FirstClass;
        private const int Header = 4, Width = 13, Assembly = 0, TypeRow = 1, FirstField = 2, Fields = 3, Storage = 4;
        private const int Base = 5, ShapeFlags = 6, FieldTable = 7, Filling = 8, Interfaces = 9;
        private const int Arguments = 10, Arity = 11, Element = 12;
        private const int ValueShape = 0x40000000, NullableShape = 0x20000000;
        private const int FieldStorageBits = 9, FieldStorageMask = (1 << FieldStorageBits) - 1;
        private const int EnumHead = 3, EnumKeyBase = 0x300;
        internal const int FieldMapStart = Header + MaxClasses * Width;

        // The header stores the type count, occupied field and auxiliary slots,
        // and the enum identity list head. Field and auxiliary data grow inward.
        internal static int RegisterEnum(int[] registry, int assembly, int row, int storage)
        {
            for (int entry = registry[EnumHead]; entry != 0; entry = registry[entry + 3])
            {
                if (registry[entry] != assembly || registry[entry + 1] != row) continue;
                return entry + EnumKeyBase;
            }

            int start = registry.Length - registry[2] - 4;
            if (start < FieldMapStart + registry[1]) return -3;
            registry[start] = assembly;
            registry[start + 1] = row;
            registry[start + 2] = storage;
            registry[start + 3] = registry[EnumHead];
            registry[EnumHead] = start;
            registry[2] = registry[2] + 4;
            return start + EnumKeyBase;
        }

        // Enum keys never become heap types or evaluation-stack tags. Their
        // defining identity survives substitution until storage is requested.
        internal static bool IsEnumKey(int key) => key >= EnumKeyBase;

        internal static int SignatureStorage(int[] registry, int key) =>
            IsEnumKey(key) ? registry[key - EnumKeyBase + 2] : GenericScalar.Storage(key);

        internal static int Find(int[] registry, int assembly, int row, int[] arguments)
        {
            for (int i = 0; i < registry[0]; i++)
            {
                int candidate = Slot(FirstClass + i);
                if (registry[candidate + Assembly] != assembly || registry[candidate + TypeRow] != row ||
                    registry[candidate + Arity] != arguments.Length) continue;
                int start = registry[candidate + Arguments];
                int j = 0;
                while (j < arguments.Length && registry[start + j] == arguments[j]) j++;
                if (j == arguments.Length) return FirstClass + i;
            }
            return 0;
        }

        // Aggregate shapes fill the descriptor. An enum instead returns its
        // scalar storage tag and never reserves a GC aggregate.
        internal static int ReadShape(byte[] image, int[] streams, int[] methods, int row, int[] shape, bool valueType = false)
        {
            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (start < 0 || row < 1 || row > counts[2]) return -1;
            int strings = methods[2];
            int refs = TypeRefStart(image, streams, counts, start, strings);
            int refWidth = ResolutionWidth(counts) + 2 * strings;
            int defs = refs + counts[1] * refWidth;
            int extends = CodedWidth(counts[2], counts[1], counts[27]);
            int fieldIndex = IndexWidth(counts[4]), methodIndex = IndexWidth(counts[6]);
            int fieldColumn = 4 + 2 * strings + extends;
            int defWidth = fieldColumn + fieldIndex + methodIndex;
            uint def = (uint)(defs + (row - 1) * defWidth);
            uint flags = U32(image, def);
            bool isInterface = (flags & 0x20) != 0;
            uint baseType = ReadIndex(image, def + (uint)(4 + 2 * strings), extends);
            if (isInterface ? baseType != 0 : baseType == 0 || (baseType & 3) > 1) return -2;
            int valueBase = baseType == 0 ? 0 : ValueBase(image, streams, methods, counts, refs, baseType);
            uint first = ReadIndex(image, def + (uint)fieldColumn, fieldIndex);
            uint end = row == counts[2] ? (uint)(counts[4] + 1) :
                ReadIndex(image, def + (uint)(defWidth + fieldColumn), fieldIndex);
            if (first < 1 || end < first || end > (uint)(counts[4] + 1)) return -1;
            int fields = defs + counts[2] * defWidth;
            if (valueBase == 2)
                return valueType && (flags & 0x138) == 0x100
                    ? EnumStorage.Read(image, streams, methods, fields, (int)first, (int)end) : -2;
            uint layout = flags & 0x18;
            if (valueType ? layout == 0x18 : layout != 0) return -2;
            if (valueType)
            {
                // Sequential values use declared managed fields. Explicit
                // field offsets are validated separately before lowering.
                if ((flags & 0x120) != 0x100 || valueBase != 1)
                    return -2;
                int attributed = ValueTypeAttributes.CheckSupported(image, streams, methods, counts,
                    row, refs, refWidth, defs, defWidth);
                if (attributed < 0) return attributed;
                if (layout != 0x10)
                {
                    int supported = DefaultPacking(image, streams, methods, counts, row, IndexWidth(counts[2]));
                    if (supported < 0) return supported;
                }
                baseType = 0;
                flags |= ValueShape;
                if (IsPlatformNullable(image, streams, methods, counts, def)) flags |= NullableShape;
            }
            else if (valueBase != 0)
                return -2; // A value definition cannot enter a class slot.
            else if ((baseType & 3) == 1 && IsObjectRef(image, streams, methods, counts, refs, (int)(baseType >> 2)))
                baseType = 0;

            if (isInterface) end = first; // Interfaces have no instance fields.
            shape[0] = fields;
            shape[1] = (int)first;
            shape[2] = (int)(end - first);
            shape[3] = (int)baseType;
            shape[4] = (int)flags;
            return 0;
        }

        internal static int Reserve(int[] registry, int assembly, int row, int[] shape, int parent, int[] arguments)
        {
            if (registry[0] >= MaxClasses) return -2;
            int count = shape[2], storage = FieldMapStart + registry[1];
            int argumentStart = registry.Length - registry[2] - arguments.Length;
            if (count > argumentStart - storage) return -3;
            // Reserve the class and its whole field slice before following
            // recursive references. Their registration may append other slices.
            int index = registry[0];
            registry[0] = index + 1;
            registry[1] = registry[1] + count;
            int slot = Slot(FirstClass + index);
            registry[slot + Assembly] = assembly;
            registry[slot + TypeRow] = row;
            registry[slot + FirstField] = shape[1];
            registry[slot + Fields] = count;
            registry[slot + Storage] = storage;
            registry[slot + Base] = parent;
            registry[slot + ShapeFlags] = shape[4];
            registry[slot + FieldTable] = shape[0];
            registry[slot + Arguments] = argumentStart;
            registry[slot + Arity] = arguments.Length;
            for (int i = 0; i < arguments.Length; i++) registry[argumentStart + i] = arguments[i];
            registry[2] = registry[2] + arguments.Length;
            return FirstClass + index;
        }

        internal static int RegisterArray(int[] registry, int element)
        {
            for (int i = 0; i < registry[0]; i++)
                if (registry[Slot(FirstClass + i) + Element] == element) return FirstClass + i;
            if (registry[0] >= MaxClasses) return -2;
            int type = FirstClass + registry[0];
            registry[0] = registry[0] + 1;
            registry[Slot(type) + Element] = element;
            return type;
        }

        internal static int ArrayElement(int[] registry, int type) =>
            type >= FirstClass && type - FirstClass < registry[0] ? GenericScalar.Storage(registry[Slot(type) + Element]) : 0;

        internal static int Argument(int[] registry, int type, int index)
        {
            if (type < FirstClass || type - FirstClass >= registry[0]) return -2;
            int slot = Slot(type);
            return index < 0 || index >= registry[slot + Arity] ? -2 : registry[registry[slot + Arguments] + index];
        }

        // Return storage directly for known types. A class signature yields
        // its image-local TypeDefOrRef code for the linker to resolve.
        internal static int ReadStorage(byte[] image, int[] streams, int[] methods, int table, int row, int[] reference)
        {
            reference[0] = 0;
            reference[1] = 0;
            int strings = methods[2], blobs = methods[3];
            uint at = (uint)(table + (row - 1) * (2 + strings + blobs));
            uint flags = U16(image, at);
            if ((flags & 0x10) != 0) return 0; // Static fields have no instance storage.
            uint sig = ReadIndex(image, at + (uint)(2 + strings), blobs);
            if (sig == 0 || sig >= (uint)streams[1]) return -1;
            int limit = streams[0] + streams[1];
            int size = CompressedUnsigned(image, (int)((uint)streams[0] + sig), limit, reference);
            int blob = reference[0], end = blob + size;
            reference[0] = 0;
            if (size < 2 || size > limit - blob) return -1;
            if (image[blob] != 6) return -2;
            reference[0] = blob + 1;
            int modifier = ReadFieldModifier(image, streams, methods, reference, end);
            if (modifier < 0) return modifier;
            blob = reference[0] - 1;
            size = end - blob;
            reference[0] = 0;
            byte element = image[blob + 1];
            if (size == 2 && (element == 8 || element == 9)) return 1;
            if (size == 2 && element >= 2 && element <= 7) return NarrowStorage.FromElement(element);
            if (size == 2 && element == 0x0e) return 6;
            if (size == 2 && element == 0x1c) return ObjectReference;
            if (size == 2 && WideStorage.IsIntegerElement(element)) return WideStorage.Scalar;
            if (size == 2 && NativeStorage.IsIntegerElement(element)) return NativeStorage.Scalar;
            byte floating = FloatStorage.FromElement(element);
            if (size == 2 && floating != 0) return floating;
            if (size == 3 && element == 0x1d)
            {
                byte type = ArrayTypeRefs.Storage(image[blob + 2]);
                if (type != 0) return type;
            }
            if (element == 0x15 || element == 0x13 || element == 0x1d || element == PointerStorage.Element)
            {
                reference[0] = -blob - 1;
                reference[1] = end;
                return 0;
            }
            if (element != 0x12 && element != 0x11) return -2;
            int coded = CompressedUnsigned(image, blob + 2, end, reference);
            if (coded <= 0 || reference[0] != end) return -2;
            reference[0] = coded;
            if (element == 0x11) reference[1] = 1;
            return 0;
        }

        // A required modifier participates in linkage even when the storage
        // bits are unchanged. Volatile ordering belongs to the IL prefix.
        internal static int ReadFieldModifier(byte[] image, int[] heaps, int[] methods, int[] cursor, int end)
        {
            int at = cursor[0];
            if (at >= end) return -1;
            if (image[at] == 0x20) return -2; // Optional modifiers need their own binding policy.
            if (image[at] != 0x1f) return 0;
            int coded = CompressedUnsigned(image, at + 1, end, cursor);
            if (coded <= 0 || cursor[0] >= end) return -1;
            return IsVolatileMarker(image, heaps, methods, coded) ? 1 : -2;
        }

        private static bool IsVolatileMarker(byte[] image, int[] heaps, int[] methods, int coded)
        {
            int tag = coded & 3, row = coded >> 2;
            if (tag > 1 || row < 1) return false;
            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (start < 0) return false;
            int strings = methods[2], scope = ResolutionWidth(counts);
            int refs = TypeRefStart(image, heaps, counts, start, strings);
            int refWidth = scope + 2 * strings;
            uint name, space;
            if (tag == 1)
            {
                if (row > counts[1]) return false;
                uint at = (uint)(refs + (row - 1) * refWidth);
                uint resolution = ReadIndex(image, at, scope);
                // The other recognized facades forward IsVolatile, while
                // System.Runtime.Extensions does not export the marker.
                if ((resolution & 3) != 2 ||
                    !PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, methods, counts,
                        (int)(resolution >> 2), includeRuntimeExtensions: false))
                    return false;
                name = ReadIndex(image, at + (uint)scope, strings);
                space = ReadIndex(image, at + (uint)(scope + strings), strings);
            }
            else
            {
                if (row > counts[2] || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, methods, counts))
                    return false;
                int width = 4 + 2 * strings + CodedWidth(counts[2], counts[1], counts[27]) +
                    IndexWidth(counts[4]) + IndexWidth(counts[6]);
                uint at = (uint)(refs + counts[1] * refWidth + (row - 1) * width);
                if ((U32(image, at) & 7) != 1) return false; // The CoreLib marker is top-level and public.
                name = ReadIndex(image, at + 4, strings);
                space = ReadIndex(image, at + (uint)(4 + strings), strings);
            }
            if (!Fits(name, 11, (uint)heaps[3]) || !Fits(space, 32, (uint)heaps[3])) return false;
            uint n = (uint)heaps[2] + name, s = (uint)heaps[2] + space;
            // IsVolatile, System.Runtime.CompilerServices.
            return U32(image, n) == 0x6f567349 && U32(image, n + 4) == 0x6974616c &&
                U16(image, n + 8) == 0x656c && image[n + 10] == 0 &&
                U32(image, s) == 0x74737953 && U32(image, s + 4) == 0x522e6d65 &&
                U32(image, s + 8) == 0x69746e75 && U32(image, s + 12) == 0x432e656d &&
                U32(image, s + 16) == 0x69706d6f && U32(image, s + 20) == 0x5372656c &&
                U32(image, s + 24) == 0x69767265 && U32(image, s + 28) == 0x00736563;
        }

        internal static int Owner(byte[] image, int[] streams, int[] methods, int member, bool field)
        {
            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            int limit = field ? counts[4] : counts[6];
            if (start < 0 || member < 1 || member > limit) return -1;
            int strings = methods[2];
            int refs = TypeRefStart(image, streams, counts, start, strings);
            int defs = refs + counts[1] * (ResolutionWidth(counts) + 2 * strings);
            int fieldIndex = IndexWidth(counts[4]), methodIndex = IndexWidth(counts[6]);
            int column = 4 + 2 * strings + CodedWidth(counts[2], counts[1], counts[27]);
            int width = column + fieldIndex + methodIndex;
            if (!field) column += fieldIndex;
            int indexWidth = field ? fieldIndex : methodIndex;

            int owner = -1;
            uint previous = 1;
            for (int i = 0; i < counts[2]; i++)
            {
                uint index = ReadIndex(image, (uint)(defs + i * width + column), indexWidth);
                if (index < previous || index > (uint)(limit + 1)) return -1;
                if (index <= (uint)member) owner = i + 1;
                previous = index;
            }
            return owner;
        }

        private static int Slot(int type) => Header + (type - FirstClass) * Width;

        internal static int Parent(int[] registry, int type) => registry[Slot(type) + Base];
        internal static bool IsSealed(int[] registry, int type) => (registry[Slot(type) + ShapeFlags] & 0x100) != 0;
        internal static bool IsAbstract(int[] registry, int type) => (registry[Slot(type) + ShapeFlags] & 0x80) != 0;
        internal static bool IsInterface(int[] registry, int type) =>
            type >= FirstClass && type - FirstClass < registry[0] && (registry[Slot(type) + ShapeFlags] & 0x20) != 0;
        internal static bool IsValueType(int[] registry, int type) =>
            type >= FirstClass && type - FirstClass < registry[0] && (registry[Slot(type) + ShapeFlags] & ValueShape) != 0;
        internal static bool IsExplicitValue(int[] registry, int type) =>
            IsValueType(registry, type) && (registry[Slot(type) + ShapeFlags] & 0x18) == 0x10;

        internal static bool IsAutoValue(int[] registry, int type) =>
            IsValueType(registry, type) && (registry[Slot(type) + ShapeFlags] & 0x18) == 0;

        internal static bool IsNullableValueType(int[] registry, int type) =>
            IsValueType(registry, type) && (registry[Slot(type) + ShapeFlags] & NullableShape) != 0;

        internal static int Representation(int[] registry, int type) => IsInterface(registry, type) ? ObjectReference : type;
        internal static int AssemblyOf(int[] registry, int type) => registry[Slot(type) + Assembly];
        internal static int TypeDef(int[] registry, int type) => registry[Slot(type) + TypeRow];
        internal static int FirstRow(int[] registry, int type) => registry[Slot(type) + FirstField];
        internal static int TableOf(int[] registry, int type) => registry[Slot(type) + FieldTable];

        internal static bool BeginFields(int[] registry, int type)
        {
            int slot = Slot(type) + Filling;
            if (registry[slot] != 0) return false;
            registry[slot] = 1;
            return true;
        }

        internal static bool Inherits(int[] registry, int source, int target)
        {
            if (source < FirstClass || target < FirstClass) return false;
            for (int type = Parent(registry, source); type != 0; type = Parent(registry, type))
                if (type == target) return true;
            return false;
        }

        internal static bool AddInterface(int[] registry, int source, int target)
        {
            int edge = registry.Length - registry[2] - 2;
            if (edge < FieldMapStart + registry[1]) return false;
            int slot = Slot(source) + Interfaces;
            registry[edge] = target;
            registry[edge + 1] = registry[slot];
            registry[slot] = edge;
            registry[2] = registry[2] + 2;
            return true;
        }

        internal static bool Implements(int[] registry, int source, int target)
        {
            if (source < FirstClass || !IsInterface(registry, target)) return false;
            return FindInterface(registry, source, target, new byte[registry[0]]);
        }

        private static bool FindInterface(int[] registry, int source, int target, byte[] seen)
        {
            // Interface inheritance can form diamonds. Visit each type once,
            // including every class base whose InterfaceImpl rows apply.
            for (int type = source; type != 0; type = Parent(registry, type))
            {
                int index = type - FirstClass;
                if (seen[index] != 0) break;
                seen[index] = 1;
                for (int edge = registry[Slot(type) + Interfaces]; edge != 0; edge = registry[edge + 1])
                {
                    int next = registry[edge];
                    if (next == target || FindInterface(registry, next, target, seen)) return true;
                }
            }
            return false;
        }

        internal static int LayoutFieldCount(int[] registry, int type)
        {
            if (IsExplicitValue(registry, type)) return 1;
            int count = 0;
            for (int current = type; current != 0; current = Parent(registry, current))
                count += FieldCount(registry, current);
            return count;
        }

        internal static int LayoutFieldType(int[] registry, int type, int index)
        {
            // The GC type stores shared bytes in one immutable i64. Its CLI
            // field metadata and bit offsets remain independently addressable.
            if (IsExplicitValue(registry, type)) return WideStorage.Scalar;
            // Base fields are the prefix required by Wasm struct subtyping.
            int current = type;
            while (true)
            {
                int parent = Parent(registry, current);
                int prefix = LayoutFieldCount(registry, parent);
                if (index >= prefix) return FieldTypeAt(registry, current, index - prefix);
                current = parent;
            }
        }

        internal static int FieldCount(int[] registry, int type) => registry[Slot(type) + Fields];

        internal static int FieldTypeAt(int[] registry, int type, int offset) =>
            registry[registry[Slot(type) + Storage] + offset] & FieldStorageMask;

        internal static void SetFieldType(int[] registry, int type, int offset, int fieldType) =>
            registry[registry[Slot(type) + Storage] + offset] = fieldType;

        internal static void SetExplicitField(int[] registry, int type, int index, int storage, int offset) =>
            SetFieldType(registry, type, index, storage | ((offset + 1) << FieldStorageBits));

        internal static int FieldType(int[] registry, int type, int row) =>
            FieldTypeAt(registry, type, row - registry[Slot(type) + FirstField]);

        internal static int FieldOrdinal(int[] registry, int type, int field)
        {
            int slot = Slot(type);
            int index = field - registry[slot + FirstField], storage = registry[slot + Storage];
            if (index < 0 || index >= registry[slot + Fields] || registry[storage + index] == 0)
                return -1;
            if (IsExplicitValue(registry, type))
                return (registry[storage + index] >> FieldStorageBits) - 1;
            int ordinal = 0;
            for (int parent = Parent(registry, type); parent != 0; parent = Parent(registry, parent))
                for (int i = 0; i < FieldCount(registry, parent); i++)
                    if (FieldTypeAt(registry, parent, i) != 0) ordinal++;
            for (int i = 0; i < index; i++)
                if (registry[storage + i] != 0) ordinal++;
            return ordinal;
        }

        internal static bool IsConstructor(byte[] image, int[] streams, int[] methods, int row)
        {
            int name = MethodDefinitions.ReadColumn(image, methods, row, 3);
            if (name <= 0 || (uint)name > (uint)streams[3] || !Fits((uint)name, 6, (uint)streams[3]))
                return false;
            uint at = (uint)(streams[2] + name);
            return U32(image, at) == 0x6f74632e && U16(image, at + 4) == 0x72;
        }

        internal static bool IsObjectConstructor(byte[] image, int[] streams, int[] methods, int row)
        {
            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (start < 0 || row < 1 || row > counts[10]) return false;
            int strings = methods[2], blobs = methods[3];
            int cursor = methods[0] + counts[6] * methods[1];
            int limit = streams[4] + streams[5];
            for (int table = 8; table <= 10; table++)
            {
                int width = StandaloneSignatures.Width(counts, table, strings, blobs);
                int skip = table == 10 ? row : counts[table];
                if (skip > (limit - cursor) / width) return false;
                cursor += skip * width;
            }
            int memberWidth = StandaloneSignatures.Width(counts, 10, strings, blobs);
            uint at = (uint)(cursor - memberWidth);
            int parentWidth = memberWidth - strings - blobs;
            uint parent = ReadIndex(image, at, parentWidth);
            if ((parent & 7) != 1 ||
                !IsObjectRef(image, streams, methods, counts, TypeRefStart(image, streams, counts, start, strings), (int)(parent >> 3)))
                return false;

            uint name = ReadIndex(image, at + (uint)parentWidth, strings);
            uint sig = ReadIndex(image, at + (uint)(parentWidth + strings), blobs);
            if (!Fits(name, 6, (uint)streams[3]) || !Fits(sig, 4, (uint)streams[1])) return false;
            uint nameAt = (uint)streams[2] + name, blob = (uint)streams[0] + sig;
            return U32(image, nameAt) == 0x6f74632e && U16(image, nameAt + 4) == 0x72 &&
                U32(image, blob) == 0x01002003; // .ctor, HASTHIS, zero args, void
        }

        internal static bool IsCoreObjectType(byte[] image, int[] streams, int[] methods, int coded)
        {
            int tag = coded & 3, row = coded >> 2;
            if (tag > 1 || row < 1) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0) return false;
            int strings = methods[2];
            int refs = TypeRefStart(image, streams, counts, first, strings);
            if (tag == 1) return IsObjectRef(image, streams, methods, counts, refs, row);
            if (row > counts[2]) return false;
            int defs = refs + counts[1] * (ResolutionWidth(counts) + 2 * strings);
            int extends = CodedWidth(counts[2], counts[1], counts[27]);
            int fieldWidth = IndexWidth(counts[4]);
            int fieldColumn = 4 + 2 * strings + extends;
            int width = fieldColumn + fieldWidth + IndexWidth(counts[6]);
            uint at = (uint)(defs + (row - 1) * width);
            uint startField = ReadIndex(image, at + (uint)fieldColumn, fieldWidth);
            uint endField = row == counts[2] ? (uint)(counts[4] + 1) :
                ReadIndex(image, at + (uint)(width + fieldColumn), fieldWidth);
            return (U32(image, at) & 0x1bf) == 1 &&
                ReadIndex(image, at + (uint)(4 + 2 * strings), extends) == 0 &&
                startField != 0 && startField == endField &&
                IsObjectName(image, streams, ReadIndex(image, at + 4, strings),
                    ReadIndex(image, at + (uint)(4 + strings), strings)) &&
                PlatformAssemblyIdentity.IsCoreLibDefinition(image, streams, methods, counts);
        }

        internal static bool IsCoreObjectConstructor(byte[] image, int[] streams, int[] methods, int row)
        {
            if (!IsConstructor(image, streams, methods, row)) return false;
            int owner = Owner(image, streams, methods, row, false);
            if (owner < 1 || !IsCoreObjectType(image, streams, methods, owner << 2)) return false;
            int sig = MethodDefinitions.ReadColumn(image, methods, row, 4);
            if (sig <= 0 || sig > streams[1] - 4 || U32(image, (uint)(streams[0] + sig)) != 0x01002003)
                return false;
            // Same-image method definitions can have arbitrary bodies. Only
            // the actual empty CoreLib constructor can be lowered as a no-op.
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, methods, row, 0));
            var body = new int[6];
            return MethodBodies.Read(image, rva, body) == 0 && body[1] == 1 && body[5] == 0 && image[body[0]] == 0x2a;
        }

        private static bool IsObjectRef(byte[] image, int[] streams, int[] methods, int[] counts, int refs, int row)
        {
            if (row < 1 || row > counts[1]) return false;
            int strings = methods[2];
            int scopeWidth = ResolutionWidth(counts);
            uint start = (uint)(refs + (row - 1) * (scopeWidth + 2 * strings));
            uint scope = ReadIndex(image, start, scopeWidth);
            if ((scope & 3) != 2) return false;
            uint name = ReadIndex(image, start + (uint)scopeWidth, strings);
            uint ns = ReadIndex(image, start + (uint)(scopeWidth + strings), strings);
            return IsObjectName(image, streams, name, ns) &&
                PlatformAssemblyIdentity.IsPlatformAssembly(image, streams, methods, counts, (int)(scope >> 2));
        }

        private static bool IsObjectName(byte[] image, int[] streams, uint name, uint ns)
        {
            if (!Fits(name, 7, (uint)streams[3]) || !Fits(ns, 7, (uint)streams[3])) return false;
            uint n = (uint)streams[2] + name, s = (uint)streams[2] + ns;
            return U32(image, n) == 0x656a624f && U16(image, n + 4) == 0x7463 && image[n + 6] == 0 &&
                U32(image, s) == 0x74737953 && U16(image, s + 4) == 0x6d65 && image[s + 6] == 0;
        }

        internal static bool IsPlatformValueType(byte[] image, int[] streams, int[] methods, int[] counts, int start, int coded) =>
            coded > 0 && (coded & 3) <= 1 &&
            ValueBase(image, streams, methods, counts, TypeRefStart(image, streams, counts, start, methods[2]), (uint)coded) == 1;

        private static bool IsPlatformNullable(byte[] image, int[] streams, int[] methods, int[] counts, uint definition)
        {
            int strings = methods[2];
            uint name = ReadIndex(image, definition + 4, strings);
            uint space = ReadIndex(image, definition + (uint)(4 + strings), strings);
            if (!Fits(name, 11, (uint)streams[3]) || !Fits(space, 7, (uint)streams[3])) return false;
            uint n = (uint)streams[2] + name, s = (uint)streams[2] + space;
            return U32(image, n) == 0x6c6c754e && U32(image, n + 4) == 0x656c6261 &&
                U16(image, n + 8) == 0x3160 && image[n + 10] == 0 &&
                U32(image, s) == 0x74737953 && U16(image, s + 4) == 0x6d65 && image[s + 6] == 0 &&
                PlatformAssemblyIdentity.IsCoreLibDefinition(image, streams, methods, counts);
        }

        private static int ValueBase(byte[] image, int[] streams, int[] methods, int[] counts, int refs, uint coded)
        {
            int row = (int)(coded >> 2);
            int tag = (int)(coded & 3), strings = methods[2], scope = ResolutionWidth(counts);
            int refWidth = scope + 2 * strings;
            if (row < 1 || row > counts[tag == 1 ? 1 : 2]) return 0;
            uint name, space, resolution = 0;
            if (tag == 1)
            {
                uint at = (uint)(refs + (row - 1) * refWidth);
                resolution = ReadIndex(image, at, scope);
                if ((resolution & 3) != 2) return 0;
                name = ReadIndex(image, at + (uint)scope, strings);
                space = ReadIndex(image, at + (uint)(scope + strings), strings);
            }
            else
            {
                int width = 4 + 2 * strings + CodedWidth(counts[2], counts[1], counts[27]) +
                    IndexWidth(counts[4]) + IndexWidth(counts[6]);
                uint at = (uint)(refs + counts[1] * refWidth + (row - 1) * width);
                name = ReadIndex(image, at + 4, strings);
                space = ReadIndex(image, at + (uint)(4 + strings), strings);
            }
            if (!Fits(name, 5, (uint)streams[3]) || !Fits(space, 7, (uint)streams[3])) return 0;
            uint n = (uint)streams[2] + name, s = (uint)streams[2] + space;
            if (U32(image, s) != 0x74737953 || U16(image, s + 4) != 0x6d65 || image[s + 6] != 0) return 0;
            int kind = 0;
            if (U32(image, n) == 0x6d756e45 && image[n + 4] == 0) kind = 2; // Enum
            else if (Fits(name, 10, (uint)streams[3]) && U32(image, n) == 0x756c6156 &&
                U32(image, n + 4) == 0x70795465 && U16(image, n + 8) == 0x65) kind = 1; // ValueType
            if (kind == 0) return 0;
            bool platform = tag == 1
                ? PlatformAssemblyIdentity.IsPlatformAssembly(image, streams, methods, counts,
                    (int)(resolution >> 2), includeRuntimeExtensions: false)
                : PlatformAssemblyIdentity.IsCoreLibDefinition(image, streams, methods, counts);
            return platform ? kind : 0;
        }

        private static int DefaultPacking(byte[] image, int[] heaps, int[] methods, int[] counts, int row, int typeIndex)
        {
            if (counts[15] == 0) return 0;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 15);
            if (table < 0) return -1;
            int width = 6 + typeIndex;
            for (int i = 0; i < counts[15]; i++)
            {
                uint at = (uint)(table + i * width);
                if (ReadIndex(image, at + 6, typeIndex) == (uint)row &&
                    (U16(image, at) != 0 || U32(image, at + 2) != 0)) return -2;
            }
            return 0;
        }

        private static int TypeRefStart(byte[] image, int[] streams, int[] counts, int first, int strings) =>
            first + counts[0] * (2 + strings + 3 * ((image[streams[4] + 6] & 2) == 0 ? 2 : 4));

        private static int ResolutionWidth(int[] counts) =>
            CodedWidth(counts[0], counts[26], counts[35]) == 4 || counts[1] >= 16384 ? 4 : 2;

        private static int CodedWidth(int a, int b, int c) => a < 16384 && b < 16384 && c < 16384 ? 2 : 4;
        private static int IndexWidth(int count) => count < 65536 ? 2 : 4;

        private static uint ReadIndex(byte[] image, uint offset, int width) =>
            width == 2 ? U16(image, offset) : U32(image, offset);
    }
}
