using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class WasmBinary
    {
        internal const int ScalarThunkType = 2, EventThunkType = 8, EventEntryHeap = 9, EventHandlerHeap = 10;
        private const int WideArrayHeap = 6, EventArgsHeap = 7, SingleArrayHeap = 11, DoubleArrayHeap = 12;
        private const int NativeArrayHeap = DoubleArrayHeap + 1;
        // The zero-field companion of the native vector is distinct from every class heap.
        // A plain System.Object allocates it without widening its static eqref (decision 0057).
        internal const int RootObjectHeap = NativeArrayHeap + 1;
        internal const int ValueLocationHeap = RootObjectHeap + 1;
        private const int ObjectLocationHeap = ValueLocationHeap + 1;
        private const int ReadOnlySpanHeap = ObjectLocationHeap + 1;
        private const int FirstClassHeap = ReadOnlySpanHeap + 1;

        internal static int WriteModule(byte[] output, int[] arities, int[] offsets, int[] types, int[] sizes, int[] bodyOffsets, int functions, byte[] bodies,
            byte[] names, int[] entryOffsets, int[] exports, int[] objects, int[] thunks,
            byte[] literals, int literalSize, int[] globals, int remainderFixup, int exceptionTags, int hostFeatures, int reflectionHeapFixup)
        {
            // Imported functions precede defined functions in the Wasm index
            // space. Emitted bodies already include this offset in their calls.
            int hostImports = WasmImports.FunctionCount(hostFeatures);
            int argumentArray = EntryArgumentsWasm.ArgumentArray(exports, arities, offsets, types, objects);
            if (!ValueFieldsWasm.CheckLayouts(objects)) return -2;
            int typeOffset = objects[0] + FirstClassHeap;
            bool hasRemainder = remainderFixup != 0;
            bool overflow = (exceptionTags & ExceptionTypes.Overflow) != 0;
            bool divide = (exceptionTags & ExceptionTypes.DivideByZero) != 0;
            bool bounds = (exceptionTags & ExceptionTypes.IndexOutOfRange) != 0;
            bool nullReference = (exceptionTags & ExceptionTypes.NullReference) != 0;
            bool invalidCast = (exceptionTags & ExceptionTypes.InvalidCast) != 0;
            // Tag indices stay stable even when lower tags are unused.
            bool argumentException = (exceptionTags & ExceptionTypes.Argument) != 0;
            bool argumentNull = (exceptionTags & ExceptionTypes.ArgumentNull) != 0;
            bool synchronizationLock = (exceptionTags & ExceptionTypes.SynchronizationLock) != 0;
            int tagCount = synchronizationLock ? ExceptionTagsWasm.SynchronizationLockTag + 1 :
                argumentNull ? ExceptionTagsWasm.ArgumentNullTag + 1 :
                argumentException ? ExceptionTagsWasm.ArgumentTag + 1 : invalidCast ? ExceptionTagsWasm.InvalidCastTag + 1 :
                nullReference ? ExceptionTagsWasm.NullTag + 1 :
                bounds ? ExceptionTagsWasm.IndexTag + 1 : divide ? ExceptionTagsWasm.DivideTag + 1 : overflow ? 1 : 0;
            bool objectTag = (exceptionTags & ExceptionTypes.Object) != 0;
            if (objectTag && tagCount <= ExceptionTagsWasm.ObjectTag) tagCount = ExceptionTagsWasm.ObjectTag + 1;
            int remainderIndex = hostImports + functions + thunks[0];
            int builderIndex = remainderIndex + (hasRemainder ? 1 : 0);
            int totalFunctions = builderIndex - hostImports + (argumentArray != 0 ? EntryArgumentsWasm.FunctionCount : 0);
            var remainderBody = new byte[hasRemainder ? FloatRemainderWasm.BodyCapacity : 0];
            int remainderSize = hasRemainder ? FloatRemainderWasm.WriteBody(remainderBody) : 0;
            int classBytes = 0;
            // Separate singleton groups with identical structs canonicalize to
            // the same Wasm type. Preserve class identity by group position.
            bool recursive = objects[0] != 0;
            for (int i = 0; i < objects[0]; i++)
            {
                int classType = FirstClass + i, fields = LayoutFieldCount(objects, classType);
                if (PrimitiveBoxing.IsLayout(objects, classType))
                {
                    if (PrimitiveBoxing.TypeBytes > output.Length - classBytes) return -3;
                    classBytes += PrimitiveBoxing.TypeBytes;
                    continue;
                }
                int element = ArrayElement(objects, classType);
                if (element != 0)
                {
                    classBytes += 2 + ValueWidth(Representation(objects, element));
                    if (classBytes > output.Length) return -3;
                    continue;
                }
                int size = 1, stored = 0, parent = Parent(objects, classType);
                if (parent != 0) size += 2 + UnsignedSize(HeapType(parent));
                else if (!IsSealed(objects, classType) && !IsInterface(objects, classType)) size += 2;
                for (int field = 0; field < fields; field++)
                {
                    int type = LayoutFieldType(objects, classType, field);
                    if (type == 0) continue;
                    int width = ManagedValueLocationWasm.UsesCell(objects, classType, type)
                        ? 1 + HeapTypeSize(ValueLocationHeap) : ValueWidth(Representation(objects, type));
                    size += 1 + width;
                    stored++;
                }
                size += UnsignedSize(stored);
                if (size > output.Length - classBytes) return -3;
                classBytes += size;
            }
            // A recursive group is one type-section entry but its member
            // types retain individual indices for fields and function signatures.
            // The fixed recursive groups combine seven types into two entries.
            bool hasDescriptors = RuntimeTypeWasm.HasDescriptors(globals);
            int typeEntries = (recursive ? functions + FirstClassHeap + 1 : functions + typeOffset) - 5;
            if (hasRemainder) typeEntries++;
            if (tagCount != 0) typeEntries++;
            if (objectTag) typeEntries++;
            typeEntries += hostImports;
            if (argumentArray != 0) typeEntries += EntryArgumentsWasm.FunctionCount;
            if (hasDescriptors) typeEntries++;
            int prefix = UnsignedSize(typeEntries) + 70 + ManagedValueLocationWasm.TypeBytes +
                ManagedLocationWasm.TypeBytes + SpanWasm.TypeBytes + (recursive ? 1 + UnsignedSize(objects[0]) : 0);
            if (hasRemainder) prefix += FloatRemainderWasm.TypeSize;
            if (tagCount != 0) prefix += 3; // () -> () exception payload
            if (objectTag) prefix += ExceptionTagsWasm.ObjectTagTypeBytes; // (eqref) -> () managed object payload
            prefix += WasmImports.TypeBytes(hostFeatures);
            if (argumentArray != 0) prefix += EntryArgumentsWasm.TypeBytes(argumentArray);
            if (hasDescriptors) prefix += RuntimeTypeWasm.DescriptorBytes;
            if (classBytes > output.Length - prefix) return -3;
            int typeSize = classBytes + prefix;
            int functionSize = UnsignedSize(totalFunctions) + thunks[0];
            int codeSize = UnsignedSize(totalFunctions);
            if (hasRemainder)
            {
                functionSize += UnsignedSize(typeOffset + functions);
                codeSize += UnsignedSize(remainderSize) + remainderSize;
            }
            int tagType = typeOffset + functions + (hasRemainder ? 1 : 0);
            // Host import and argument builder types follow every existing type.
            int objectTagType = tagType + 1;
            int importType = tagType + (tagCount != 0 ? 1 : 0) + (objectTag ? 1 : 0);
            int builderType = importType + hostImports;
            // Append the optional descriptor type to keep the existing ABI indices stable.
            int descriptorHeap = builderType + (argumentArray != 0 ? EntryArgumentsWasm.FunctionCount : 0);
            if (argumentArray != 0)
            {
                for (int i = 0; i < EntryArgumentsWasm.FunctionCount; i++)
                    functionSize += UnsignedSize(builderType + i);
                codeSize += EntryArgumentsWasm.CodeBytes(argumentArray);
            }
            // Use one type per function in discovery order. Accumulate against
            // destination capacity before any writes, guarding size arithmetic.
            for (int i = 0; i < functions; i++)
            {
                int shape = offsets[i];
                int result = types[shape];
                int type = 2 + UnsignedSize(arities[i]) + ValueWidth(Representation(objects, result));
                for (int parameter = 0; parameter < arities[i]; parameter++)
                    type += ValueWidth(Representation(objects, types[shape + 1 + parameter]));
                int code = UnsignedSize(sizes[i]) + sizes[i];
                int index = UnsignedSize(i + typeOffset);
                if (type > output.Length - typeSize || code > output.Length - codeSize ||
                    index > output.Length - functionSize)
                    return -3;
                typeSize += type;
                codeSize += code;
                functionSize += index;
            }
            int exportCount = exports.Length + (argumentArray != 0 ? EntryArgumentsWasm.FunctionCount : 0) +
                (overflow ? 1 : 0) + (divide ? 1 : 0) + (bounds ? 1 : 0) +
                (nullReference ? 1 : 0) + (invalidCast ? 1 : 0) + (argumentException ? 1 : 0) + (objectTag ? 1 : 0) +
                (argumentNull ? 1 : 0) + (synchronizationLock ? 1 : 0);
            int exportSize = UnsignedSize(exportCount) + (overflow ? ExceptionTagsWasm.OverflowExportSize : 0) +
                (divide ? ExceptionTagsWasm.DivideExportSize : 0) + (bounds ? ExceptionTagsWasm.IndexExportSize : 0) +
                (nullReference ? ExceptionTagsWasm.NullExportSize : 0) + (invalidCast ? ExceptionTagsWasm.InvalidCastExportSize : 0) +
                (argumentException ? ExceptionTagsWasm.ArgumentExportSize : 0) + (objectTag ? ExceptionTagsWasm.ObjectExportSize : 0) +
                (argumentNull ? ExceptionTagsWasm.ArgumentNullExportSize : 0) +
                (synchronizationLock ? ExceptionTagsWasm.SynchronizationLockExportSize : 0);
            if (argumentArray != 0) exportSize += EntryArgumentsWasm.ExportBytes(builderIndex);
            int tagSize = 1 + (tagCount - (objectTag ? 1 : 0)) * (1 + UnsignedSize(tagType)) +
                (objectTag ? 1 + UnsignedSize(objectTagType) : 0);
            for (int i = 0; i < exports.Length; i++)
            {
                int name = (int)ImageBytes.U32(names, (uint)entryOffsets[i] + 4);
                int metadata = UnsignedSize(name) + 1 + UnsignedSize(hostImports + exports[i]);
                if (metadata > output.Length - exportSize || name > output.Length - exportSize - metadata)
                    return -3;
                exportSize += metadata + name;
            }
            int tableSize = 3 + UnsignedSize(thunks[0]);
            int elementSize = 5 + UnsignedSize(thunks[0]);
            for (int i = 0; i < thunks[0]; i++)
            {
                int function = thunks[i + 1];
                int receiver = types[offsets[function] + 1];
                int thunkSize = ThunkSize(receiver, hostImports + function, arities[function]);
                if (1 + thunkSize > output.Length - codeSize ||
                    UnsignedSize(hostImports + functions + i) > output.Length - elementSize)
                    return -3;
                codeSize += 1 + thunkSize;
                elementSize += UnsignedSize(hostImports + functions + i);
            }

            int remaining = output.Length - 8;
            remaining = Reserve(remaining, typeSize);
            int importSize = WasmImports.Size(hostFeatures, importType);
            if (hostFeatures != 0)
                remaining = Reserve(remaining, importSize);
            remaining = Reserve(remaining, functionSize);
            if (thunks[0] != 0)
                remaining = Reserve(remaining, tableSize);
            if (tagCount != 0)
                remaining = Reserve(remaining, tagSize);
            int globalSize = WasmGlobals.Size(globals, objects, descriptorHeap);
            if (globalSize != 0)
                remaining = Reserve(remaining, globalSize);
            remaining = Reserve(remaining, exportSize);
            if (thunks[0] != 0)
                remaining = Reserve(remaining, elementSize);
            if (literalSize != 0)
                remaining = Reserve(remaining, 1); // DataCount
            remaining = Reserve(remaining, codeSize);
            int dataOverhead = 2 + UnsignedSize(literalSize);
            if (remaining < 0 || (literalSize != 0 && (dataOverhead > remaining ||
                literalSize > remaining - dataOverhead || Reserve(remaining, dataOverhead + literalSize) < 0)))
                return -3;
            if (reflectionHeapFixup != 0) WasmIndexFixups.Resolve(bodies, reflectionHeapFixup, descriptorHeap);
            if (hasRemainder) WasmIndexFixups.Resolve(bodies, remainderFixup, remainderIndex);

            output[0] = 0;
            output[1] = 0x61;
            output[2] = 0x73;
            output[3] = 0x6d;
            output[4] = 1;
            output[5] = 0;
            output[6] = 0;
            output[7] = 0;
            int cursor = Section(output, 8, 1, typeSize);
            cursor = Unsigned(output, cursor, typeEntries);
            output[cursor++] = 0x5e; // array of packed i8
            output[cursor++] = 0x78;
            output[cursor++] = 1;
            output[cursor++] = 0x5e; // array of i32
            output[cursor++] = 0x7f;
            output[cursor++] = 1;
            // Thunk type: (eqref) -> i32. Its closure holds an eqref and a funcref.
            output[cursor++] = 0x60;
            output[cursor++] = 1;
            output[cursor++] = 0x6d;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            output[cursor++] = 0x5f;
            output[cursor++] = 2;
            output[cursor++] = 0x6d;
            output[cursor++] = 0;
            output[cursor++] = 0x70;
            output[cursor++] = 0;
            output[cursor++] = 0x5e; // Char[] backing store
            output[cursor++] = 0x77;
            output[cursor++] = 1;
            output[cursor++] = 0x5f; // String: immutable reference to private Char[] storage
            output[cursor++] = 1;
            output[cursor++] = 0x63;
            output[cursor++] = 4;
            output[cursor++] = 0;
            output[cursor++] = 0x5e; // array of i64
            output[cursor++] = 0x7e;
            output[cursor++] = 1;
            // The immutable marker gives EventArgs a distinct GC shape from user classes.
            output[cursor++] = 0x5f;
            output[cursor++] = 1;
            output[cursor++] = 0x7f;
            output[cursor++] = 0;
            // Event thunk: (target eqref, sender eqref, EventArgs?) -> void.
            output[cursor++] = 0x60;
            output[cursor++] = 3;
            output[cursor++] = 0x6d;
            output[cursor++] = 0x6d;
            cursor = ValueType(output, cursor, EventArgsReference);
            output[cursor++] = 0;
            // Entry and invocation list refer to each other. The back-reference
            // is set once at construction and retains the original singleton.
            output[cursor++] = 0x4e;
            output[cursor++] = 2;
            output[cursor++] = 0x5f;
            output[cursor++] = 4;
            output[cursor++] = 0x6d;
            output[cursor++] = 0;
            output[cursor++] = 0x63;
            output[cursor++] = EventThunkType;
            output[cursor++] = 0;
            output[cursor++] = 0x7f; // method identity, independent of the receiver
            output[cursor++] = 0;
            output[cursor++] = 0x63;
            output[cursor++] = EventHandlerHeap;
            output[cursor++] = 1;
            // Private array of callbacks. Generated code writes only fresh arrays.
            output[cursor++] = 0x5e;
            output[cursor++] = 0x63;
            output[cursor++] = EventEntryHeap;
            output[cursor++] = 1;
            output[cursor++] = 0x5e; // f32 array
            output[cursor++] = 0x7d;
            output[cursor++] = 1;
            output[cursor++] = 0x5e; // f64 array
            output[cursor++] = 0x7c;
            output[cursor++] = 1;
            // Give the native vector, root object and managed views private
            // type identities by placing them in their own recursive group.
            output[cursor++] = 0x4e;
            output[cursor++] = 5;
            output[cursor++] = 0x5e;
            output[cursor++] = 0x7f;
            output[cursor++] = 1;
            output[cursor++] = 0x5f;
            output[cursor++] = 0;
            cursor = ManagedValueLocationWasm.WriteType(output, cursor);
            cursor = ManagedLocationWasm.WriteType(output, cursor);
            cursor = SpanWasm.WriteType(output, cursor);
            if (recursive)
            {
                output[cursor++] = 0x4e;
                cursor = Unsigned(output, cursor, objects[0]);
            }
            for (int i = 0; i < objects[0]; i++)
            {
                int classType = FirstClass + i, fields = LayoutFieldCount(objects, classType);
                if (PrimitiveBoxing.IsLayout(objects, classType))
                {
                    cursor = PrimitiveBoxing.WriteType(output, cursor);
                    continue;
                }
                int element = ArrayElement(objects, classType);
                if (element != 0)
                {
                    output[cursor++] = 0x5e; // mutable reference vector
                    cursor = ValueType(output, cursor, Representation(objects, element));
                    output[cursor++] = 1;
                    continue;
                }
                int parent = Parent(objects, classType);
                // Interface IDs reserve an empty type entry to keep heap
                // indices stable. Interface values use eqref and never
                // allocate instances of this placeholder.
                bool final = IsSealed(objects, classType) || IsInterface(objects, classType);
                if (parent != 0 || !final)
                {
                    output[cursor++] = final ? (byte)0x4f : (byte)0x50;
                    output[cursor++] = parent == 0 ? (byte)0 : (byte)1;
                    if (parent != 0) cursor = Unsigned(output, cursor, HeapType(parent));
                }
                output[cursor++] = 0x5f; // struct
                int stored = 0;
                for (int field = 0; field < fields; field++)
                    if (LayoutFieldType(objects, classType, field) != 0) stored++;
                cursor = Unsigned(output, cursor, stored);
                for (int field = 0; field < fields; field++)
                {
                    int fieldType = LayoutFieldType(objects, classType, field);
                    if (fieldType == 0) continue;
                    if (ManagedValueLocationWasm.UsesCell(objects, classType, fieldType))
                        cursor = ManagedValueLocationWasm.WriteFieldType(output, cursor);
                    else if (IsNarrow(fieldType))
                        output[cursor++] = fieldType == Signed8 || fieldType == Unsigned8 ? (byte)0x78 : (byte)0x77;
                    else
                        cursor = ValueType(output, cursor, Representation(objects, fieldType));
                    output[cursor++] = IsValueType(objects, classType) ? (byte)0 : (byte)1;
                }
            }
            for (int type = 0; type < functions; type++)
            {
                int shape = offsets[type];
                output[cursor++] = 0x60;
                cursor = Unsigned(output, cursor, arities[type]);
                for (int i = 0; i < arities[type]; i++)
                    cursor = ValueType(output, cursor, Representation(objects, types[shape + i + 1]));
                int result = types[shape];
                output[cursor++] = result == Void ? (byte)0 : (byte)1;
                if (result != Void)
                    cursor = ValueType(output, cursor, Representation(objects, result));
            }
            if (hasRemainder) cursor = FloatRemainderWasm.WriteType(output, cursor);
            if (tagCount != 0)
            {
                output[cursor++] = 0x60;
                output[cursor++] = 0;
                output[cursor++] = 0;
            }
            if (objectTag) cursor = ExceptionTagsWasm.WriteObjectTagType(output, cursor);
            cursor = WasmImports.WriteTypes(output, cursor, hostFeatures);
            if (argumentArray != 0) cursor = EntryArgumentsWasm.WriteTypes(output, cursor, argumentArray);
            if (hasDescriptors) cursor = RuntimeTypeWasm.WriteDescriptor(output, cursor);
            if (hostFeatures != 0)
            {
                cursor = Section(output, cursor, 2, importSize);
                cursor = WasmImports.WriteEntries(output, cursor, hostFeatures, importType);
            }

            cursor = Section(output, cursor, 3, functionSize);
            cursor = Unsigned(output, cursor, totalFunctions);
            for (int i = 0; i < functions; i++)
                cursor = Unsigned(output, cursor, i + typeOffset);
            for (int i = 0; i < thunks[0]; i++)
                output[cursor++] = arities[thunks[i + 1]] == 3 ? (byte)EventThunkType : (byte)ScalarThunkType;
            if (hasRemainder) cursor = Unsigned(output, cursor, typeOffset + functions);
            if (argumentArray != 0)
                for (int i = 0; i < EntryArgumentsWasm.FunctionCount; i++)
                    cursor = Unsigned(output, cursor, builderType + i);
            if (thunks[0] != 0)
            {
                cursor = Section(output, cursor, 4, tableSize);
                output[cursor++] = 1;
                output[cursor++] = 0x70;
                output[cursor++] = 0;
                cursor = Unsigned(output, cursor, thunks[0]);
            }
            if (tagCount != 0)
            {
                // Tag sections precede globals and exports despite section ID 13.
                cursor = Section(output, cursor, 13, tagSize);
                output[cursor++] = (byte)tagCount;
                for (int tag = 0; tag < tagCount; tag++)
                {
                    output[cursor++] = 0; // exception attribute
                    cursor = Unsigned(output, cursor, objectTag && tag == ExceptionTagsWasm.ObjectTag ? objectTagType : tagType);
                }
            }

            if (globalSize != 0)
            {
                cursor = Section(output, cursor, 6, globalSize);
                cursor = WasmGlobals.Write(output, cursor, globals, objects, descriptorHeap);
            }
            cursor = Section(output, cursor, 7, exportSize);
            cursor = Unsigned(output, cursor, exportCount);
            for (int i = 0; i < exports.Length; i++)
            {
                int source = entryOffsets[i];
                int name = (int)ImageBytes.U32(names, (uint)source + 4);
                cursor = Unsigned(output, cursor, name);
                for (int j = 0; j < name; j++)
                    output[cursor++] = names[source + 8 + j];
                output[cursor++] = 0;
                cursor = Unsigned(output, cursor, hostImports + exports[i]);
            }
            if (argumentArray != 0) cursor = EntryArgumentsWasm.WriteExports(output, cursor, builderIndex);
            if (overflow) cursor = ExceptionTagsWasm.WriteOverflowExport(output, cursor);
            if (divide) cursor = ExceptionTagsWasm.WriteDivideExport(output, cursor);
            if (bounds) cursor = ExceptionTagsWasm.WriteIndexExport(output, cursor);
            if (nullReference) cursor = ExceptionTagsWasm.WriteNullExport(output, cursor);
            if (invalidCast) cursor = ExceptionTagsWasm.WriteInvalidCastExport(output, cursor);
            if (argumentException) cursor = ExceptionTagsWasm.WriteArgumentExport(output, cursor);
            if (argumentNull) cursor = ExceptionTagsWasm.WriteArgumentNullExport(output, cursor);
            if (synchronizationLock) cursor = ExceptionTagsWasm.WriteSynchronizationLockExport(output, cursor);
            if (objectTag) cursor = ExceptionTagsWasm.WriteObjectExport(output, cursor);

            if (thunks[0] != 0)
            {
                cursor = Section(output, cursor, 9, elementSize);
                output[cursor++] = 1; // one segment
                output[cursor++] = 0; // active, table zero
                output[cursor++] = 0x41;
                output[cursor++] = 0;
                output[cursor++] = 0x0b;
                cursor = Unsigned(output, cursor, thunks[0]);
                for (int i = 0; i < thunks[0]; i++)
                    cursor = Unsigned(output, cursor, hostImports + functions + i);
            }

            if (literalSize != 0)
            {
                cursor = Section(output, cursor, 12, 1);
                output[cursor++] = 1;
            }

            cursor = Section(output, cursor, 10, codeSize);
            cursor = Unsigned(output, cursor, totalFunctions);
            for (int body = 0; body < functions; body++)
            {
                int size = sizes[body], start = bodyOffsets[body];
                cursor = Unsigned(output, cursor, size);
                for (int i = 0; i < size; i++)
                    output[cursor++] = bodies[start + i];
            }
            for (int i = 0; i < thunks[0]; i++)
            {
                int function = thunks[i + 1], receiver = types[offsets[function] + 1];
                int arity = arities[function];
                cursor = Unsigned(output, cursor, ThunkSize(receiver, hostImports + function, arity));
                output[cursor++] = 0; // local declarations
                output[cursor++] = 0x20;
                output[cursor++] = 0;
                output[cursor++] = 0xfb;
                output[cursor++] = 22; // ref.cast to the target's concrete receiver
                cursor = Signed(output, cursor, HeapType(receiver));
                for (int argument = 1; argument < arity; argument++)
                {
                    output[cursor++] = 0x20;
                    output[cursor++] = (byte)argument;
                }
                output[cursor++] = 0x10;
                cursor = Unsigned(output, cursor, hostImports + function);
                output[cursor++] = 0x0b;
            }
            if (hasRemainder)
            {
                cursor = Unsigned(output, cursor, remainderSize);
                for (int i = 0; i < remainderSize; i++) output[cursor++] = remainderBody[i];
            }
            if (argumentArray != 0) cursor = EntryArgumentsWasm.WriteBodies(output, cursor, argumentArray);
            if (literalSize != 0)
            {
                cursor = Section(output, cursor, 11, dataOverhead + literalSize);
                output[cursor++] = 1; // one passive segment
                output[cursor++] = 1;
                cursor = Unsigned(output, cursor, literalSize);
                for (int i = 0; i < literalSize; i++)
                    output[cursor++] = literals[i];
            }
            return cursor;
        }

        internal static int ValueWidth(int type) =>
            type == Void ? 0 : IsScalar(type) || type == MethodPointer || type == ObjectReference || type == NullPointer ||
                type == ConsoleStorage.Writer || RuntimeTypeMetadata.IsStorage(type) ? 1 :
                1 + HeapTypeSize(HeapType(StackType(type)));

        internal static int ValueType(byte[] output, int cursor, int slot)
        {
            int type = StackType(slot);
            if (type == I32 || type == MethodPointer || IsNativeInteger(type) || type == ConsoleStorage.Writer)
                output[cursor++] = 0x7f; // Console writers store their host stream number.
            else if (type == NullPointer)
                output[cursor++] = NullSpillWasm.NullRef; // Only the null pointer inhabits the browser storage type.
            else if (type == I64)
                output[cursor++] = 0x7e;
            else if (IsFloat(type))
                output[cursor++] = type == F32 ? (byte)0x7d : (byte)0x7c;
            else if (type == ObjectReference || RuntimeTypeMetadata.IsStorage(type))
                output[cursor++] = 0x6d; // eqref, the supertype of the managed class structs
            else
            {
                output[cursor++] = 0x63; // ref null
                cursor = Signed(output, cursor, HeapType(type));
            }
            return cursor;
        }

        internal static int HeapType(int type) =>
            IsValueLocation(type) ? ValueLocationHeap : type == Delegate ? 3 : type == Chars ? 4 : type == String ? 5 : type == Longs ? WideArrayHeap :
                type == EventArgsReference ? EventArgsHeap :
                type == EventHandlerReference || type == DelegateReference ? EventHandlerHeap :
                type == Singles ? SingleArrayHeap : type == Doubles ? DoubleArrayHeap :
                type == NativeVector ? NativeArrayHeap : IsManagedLocation(type) ? ObjectLocationHeap : IsSpan(type) ? ReadOnlySpanHeap :
                type < FirstClass ? type - Bytes : FirstClassHeap + type - FirstClass;

        // Positive heap indices are encoded as signed LEB values. The sign
        // bit reserves one additional bit compared with an unsigned index.
        internal static int HeapTypeSize(int index) => UnsignedSize(index * 2);

        private static int ThunkSize(int receiver, int function, int arity) =>
            7 + HeapTypeSize(HeapType(receiver)) + 2 * (arity - 1) + UnsignedSize(function);

        private static int Reserve(int remaining, int size)
        {
            int overhead = 1 + UnsignedSize(size);
            if (remaining < overhead || size > remaining - overhead) return -1;
            return remaining - overhead - size;
        }

        private static int Section(byte[] bytes, int cursor, byte id, int payload)
        {
            bytes[cursor++] = id;
            return Unsigned(bytes, cursor, payload);
        }

        internal static int UnsignedSize(int value)
        {
            int count = 1;
            while (value >= 128)
            {
                value >>= 7;
                count++;
            }
            return count;
        }

        internal static int Unsigned(byte[] bytes, int cursor, int value)
        {
            while (value >= 128)
            {
                bytes[cursor++] = (byte)((value & 127) | 128);
                value >>= 7;
            }
            bytes[cursor++] = (byte)value;
            return cursor;
        }

        // Keep the compiler bootstrappable with an i32-only seed. An arithmetic
        // shift of the two words preserves the sign and carries bits across the seam.
        internal static int Signed64(byte[] bytes, int cursor, int low, int high)
        {
            while (!(high == 0 && low >= 0 && low <= 63 || high == -1 && low >= -64 && low < 0))
            {
                bytes[cursor++] = (byte)((low & 127) | 128);
                low = unchecked((int)((uint)low >> 7)) | (high << 25);
                high >>= 7;
            }
            bytes[cursor++] = (byte)(low & 127);
            return cursor;
        }

        internal static int Signed(byte[] bytes, int cursor, int value)
        {
            while (value < -64 || value > 63)
            {
                bytes[cursor++] = (byte)((value & 127) | 128);
                value >>= 7;
            }
            bytes[cursor++] = (byte)(value & 127);
            return cursor;
        }
    }
}
