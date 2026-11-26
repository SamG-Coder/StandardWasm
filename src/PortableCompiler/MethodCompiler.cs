using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class MethodCompiler
    {
        internal static int Compile(byte[] image, int row, byte[] output) =>
            CompileSingle(image, row, output, new int[0]);

        internal static int CompileDetailed(byte[] image, int row, byte[] output, int[] location)
        {
            if (location.Length < CompileLocation.Size) return -3;
            return CompileSingle(image, row, output, location);
        }

        private static int CompileSingle(byte[] image, int row, byte[] output, int[] location)
        {
            var entries = new byte[11];
            entries[0] = unchecked((byte)row);
            entries[1] = unchecked((byte)(row >> 8));
            entries[2] = unchecked((byte)(row >> 16));
            entries[3] = unchecked((byte)(row >> 24));
            entries[4] = 3;
            entries[8] = 0x72;
            entries[9] = 0x75;
            entries[10] = 0x6e;
            return CompileProgram(image, new byte[0], new int[0], entries, output, location);
        }

        internal static int CompileExports(byte[] image, byte[] entries, byte[] output) =>
            CompileProgram(image, new byte[0], new int[0], entries, output, new int[0]);

        internal static int CompileModuleDetailed(byte[] image, byte[] entries, byte[] output, int[] location)
        {
            if (location.Length < CompileLocation.Size) return -3;
            return CompileProgram(image, new byte[0], new int[0], entries, output, location);
        }

        internal static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            CompileBundleCore(bundle, entries, output, new int[0]);

        internal static int CompileBundleDetailed(byte[] bundle, byte[] entries, byte[] output, int[] location)
        {
            if (location.Length < CompileLocation.Size) return -3;
            return CompileBundleCore(bundle, entries, output, location);
        }

        private static int CompileBundleCore(byte[] bundle, byte[] entries, byte[] output, int[] location)
        {
            CompileLocation.Clear(location);
            int count = AssemblyBundle.Count(bundle);
            if (count < 0) return count;
            int mainSize = (int)ImageBytes.U32(bundle, 0);
            var ranges = new int[(count - 1) * 2];
            int cursor = 4 + mainSize;
            for (int i = 0; i < ranges.Length; i += 2)
            {
                int size = (int)ImageBytes.U32(bundle, (uint)cursor);
                ranges[i] = cursor + 4;
                ranges[i + 1] = size;
                cursor += 4 + size;
            }
            return CompileProgram(AssemblyBundle.Copy(bundle, 4, mainSize), bundle, ranges, entries, output, location);
        }

        private static int CompileProgram(byte[] main, byte[] bundle, int[] ranges, byte[] entries, byte[] output, int[] location)
        {
            CompileLocation.Clear(location);
            byte[] image = main;
            var entryOffsets = new int[entries.Length / 9];
            int exportCount = EntryPoints.Read(entries, entryOffsets);
            if (exportCount < 0)
                return exportCount;
            var exports = new int[exportCount];
            var layout = new int[4];
            var heaps = new int[8];
            CompileLocation.Set(location, 0, 0, -1);
            int count = CompressedTables.FindWithHeaps(image, layout, heaps);
            if (count < 0)
                return count == MethodDefinitions.Unsupported ? -2 : -1;
            int mainInitialization = TypeInitializers.RejectModuleInitializer(image, layout, heaps);
            if (mainInitialization < 0) return mainInitialization;

            // A MethodDef row is local to its image. Give every row a disjoint
            // range in the global symbol index and record its owner on the queue.
            int modules = ranges.Length / 2 + 1;
            var bases = new int[modules + 1];
            bases[1] = count;
            var interfaceTable = new int[3];
            int interfaces = InterfaceImplementations.ReadTable(image, heaps, layout, interfaceTable);
            if (interfaces < 0) return interfaces;
            for (int module = 1; module < modules; module++)
            {
                CompileLocation.Set(location, module, 0, -1);
                byte[] dependency = AssemblyBundle.Load(main, bundle, ranges, module);
                var dependencyLayout = new int[4];
                var dependencyHeaps = new int[8];
                int size = CompressedTables.FindWithHeaps(dependency, dependencyLayout, dependencyHeaps);
                if (size < 0) return size == MethodDefinitions.Unsupported ? -2 : -1;
                int dependencyInterfaces = InterfaceImplementations.ReadTable(dependency, dependencyHeaps, dependencyLayout, interfaceTable);
                if (dependencyInterfaces < 0) return dependencyInterfaces;
                interfaces += dependencyInterfaces;
                int dependencyInitialization = TypeInitializers.RejectModuleInitializer(dependency, dependencyLayout, dependencyHeaps);
                if (dependencyInitialization < 0) return dependencyInitialization;
                if (size >= int.MaxValue - bases[module]) return -2;
                bases[module + 1] = bases[module] + size;
            }
            int symbols = bases[modules];
            // Field rows occupy at least six bytes. Each InterfaceImpl row
            // reserves up to two extra cells for the nominal edge.
            int imageBytes = bundle.Length == 0 ? main.Length : bundle.Length;
            // Closed instantiations have distinct field slices and argument
            // vectors even when they share a CLI definition.
            var objects = new int[ObjectLayouts.FieldMapStart + imageBytes / 6 + interfaces * 2 + output.Length / 4];
            var owners = new int[symbols];
            var rows = new int[symbols];
            var indices = new int[symbols + 1];
            var arities = new int[symbols];
            var offsets = new int[symbols];
            var sizes = new int[symbols];
            var thunks = new int[symbols + 1];
            var signatures = new int[output.Length];
            var frame = new int[output.Length];
            var moduleCode = new byte[output.Length];
            var literals = new byte[output.Length];
            var literalSize = new int[1];
            var remainderFixup = new int[1];
            int functions = 0, length = 0;
            for (int export = 0; export < exportCount; export++)
            {
                uint row = ImageBytes.U32(entries, (uint)entryOffsets[export]);
                CompileLocation.Set(location, 0, 0, -1);
                if (row == 0 || row > (uint)count)
                    return -1;
                CompileLocation.Set(location, 0, (int)row, -1);
                int index = indices[row] - 1;
                if (index < 0)
                {
                    functions = Register(main, bundle, ranges, image, layout, heaps, 0, (int)row, owners, rows, bases, indices,
                        arities, offsets, signatures, objects, functions);
                    if (functions < 0) return functions;
                    index = functions - 1;
                }
                // Wasm sees interface parameters as eqref. Keep the host
                // from bypassing the linker's nominal assignment checks.
                for (int parameter = 1; parameter <= arities[index]; parameter++)
                {
                    int type = signatures[offsets[index] + parameter];
                    if (type == NullPointer || ObjectLayouts.IsInterface(objects, type) || ObjectLayouts.IsValueType(objects, type))
                        return -2;
                }
                int result = signatures[offsets[index]];
                if (result == NullPointer || ObjectLayouts.IsValueType(objects, result)) return -2;
                exports[export] = index;
            }
            var body = new int[6];
            var frameShape = new int[10];
            bool eventArgsEmpty = false;
            int exceptionTags = 0;
            var noCatch = new int[0];
            var sizingBuffer = new byte[0];

            int activeOwner = 0;
            for (int current = 0; current < functions; current++)
            {
                int owner = owners[current], source = rows[current];
                CompileLocation.Set(location, owner, source, -1);
                if (owner != activeOwner)
                {
                    image = AssemblyBundle.Load(main, bundle, ranges, owner);
                    layout = new int[4];
                    heaps = new int[8];
                    count = CompressedTables.FindWithHeaps(image, layout, heaps);
                    if (count < 0) return -1;
                    activeOwner = owner;
                }
                uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, source, 0));
                int status = MethodBodies.Read(image, rva, body);
                if (status != 0)
                    return status == MethodBodies.Malformed ? -1 : -2;
                int[] catchRegion = noCatch;
                bool hasFinally = false;
                if (body[5] != 0)
                {
                    int regions = ExceptionRegions.Count(image, rva, body);
                    if (regions < 0) return regions;
                    if (regions == 0) return -2;
                    catchRegion = new int[regions * ExceptionClauses.Width];
                    int read = ExceptionRegions.Read(image, rva, body, catchRegion);
                    if (read < 0) return read;
                    for (int clause = 0; clause < catchRegion.Length; clause += ExceptionRegions.Width)
                    {
                        if (location.Length != 0) location[2] = catchRegion[clause + 3];
                        if (catchRegion[clause] == 2)
                        {
                            hasFinally = true;
                            continue; // The reader publishes zero in the union field.
                        }
                        if (catchRegion[clause] != 0) return -2;
                        int mask = ExceptionTypes.CatchMask(image, heaps, layout, catchRegion[clause + 5]);
                        if (mask == 0) return -2;
                        catchRegion[clause + 5] = mask;
                    }
                    if (location.Length != 0) location[2] = -1;
                    exceptionTags |= ExceptionTypes.Supported;
                }

                int parameters = arities[current];
                int signature = offsets[current];
                int result = signatures[signature];
                bool scalar = result == I32;
                int narrowParameters = 0;
                for (int i = 0; i < parameters; i++)
                {
                    int type = signatures[signature + i + 1];
                    frame[i] = type;
                    if (type != I32) scalar = false;
                    if (IsNarrow(type)) narrowParameters++;
                }
                int locals = 0;
                if (body[3] != 0)
                {
                    int localBlob = StandaloneSignatures.BlobIndex(image, heaps, layout, count, body[3]);
                    if (localBlob < 0)
                        return -1;
                    locals = SignatureTypes.Read(main, bundle, ranges, image, heaps, localBlob, frame, parameters, -1, layout, objects, owner);
                    if (locals < 0)
                        return locals;
                    if (locals > 0 && body[4] == 0)
                        return -2;
                }
                if (body[1] > (int.MaxValue - 32) / 64)
                    return -2;

                var code = new byte[body[1] * 4 + 2];
                int used = scalar && locals == 0 && catchRegion.Length == 0
                    ? StraightLineIl.Translate(image, body, parameters, code) : -2;
                if (used < 0)
                {
                    var records = new int[body[1] * BranchIl.Width];
                    status = BranchIl.Decode(image, body[0], body[1], parameters, locals, records, location, catchRegion.Length != 0);
                    if (status < 0)
                        return status;
                    status = ArrayInitializers.Lower(image, heaps, layout, records, literals, literalSize, location);
                    if (status < 0)
                        return status;
                    functions = LinkOperands(main, bundle, ranges, image, layout, heaps, owner, source,
                        parameters == 0 ? 0 : signatures[signature + 1], records, owners, rows,
                        bases, indices, arities, offsets, signatures, objects, thunks, literals, literalSize, functions, location, frame, catchRegion);
                    if (functions < 0)
                        return functions;

                    if (location.Length != 0) location[2] = -1;
                    int maximum = body[2];
                    if (maximum > 0 && body[1] > int.MaxValue / maximum)
                        return -2;
                    var depths = new int[body[1]];
                    var states = new int[body[1] * maximum];
                    bool hasNull = false;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                        if (records[slot] == BranchIl.NullLiteral || records[slot] == BranchIl.ConvertInteger &&
                            records[slot + 3] == BranchIl.ProvenNativeZero) hasNull = true;
                    var outgoingNulls = new byte[hasNull ? states.Length : 0];
                    int exceptionLocals = 0;
                    if (catchRegion.Length != 0)
                    {
                        exceptionLocals = ExceptionClauses.Validate(records, catchRegion, location);
                        if (exceptionLocals < 0) return exceptionLocals;
                    }
                    status = StackDepths.Analyze(records, depths, states, maximum, frame,
                        result, signatures, offsets, arities, location, catchRegion, outgoingNulls, objects);
                    if (status < 0) return status;

                    if (location.Length != 0) location[2] = -1;
                    int capacity = body[1] * 64 + 32;
                    if (catchRegion.Length != 0)
                    {
                        int handlerBytes = 32 + ExceptionTagsWasm.Count * 20 +
                            catchRegion.Length / ExceptionClauses.Width * (40 * ExceptionTagsWasm.Count);
                        if (capacity > int.MaxValue - handlerBytes) return -3;
                        capacity += handlerBytes;
                    }
                    if (locals > (int.MaxValue - capacity) / 3)
                        return -3;
                    capacity += locals * 3;
                    if (narrowParameters > (int.MaxValue - capacity) / 16)
                        return -3;
                    capacity += narrowParameters * 16;
                    int classBytes = objects[0] * 12;
                    if (classBytes > int.MaxValue - capacity) return -3;
                    capacity += classBytes;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        int kind = records[slot];
                        if (kind == BranchIl.RemoveHandler && depths[slot / BranchIl.Width] != 0)
                        {
                            // The structured search and copy expand a single IL call.
                            if (capacity > int.MaxValue - 320) return -3;
                            capacity += 320;
                        }
                        int pc = slot / BranchIl.Width;
                        int operation = records[slot + 1];
                        if (depths[pc] != 0)
                        {
                            if (hasNull)
                            {
                                capacity = NullSpillWasm.Transfer(sizingBuffer, capacity, records, pc,
                                    depths, states, outgoingNulls, maximum, 0);
                                if (capacity == int.MaxValue) return -3;
                            }
                            if (hasFinally && kind == BranchIl.Leave)
                            {
                                int unwindBytes = catchRegion.Length / ExceptionClauses.Width * 32;
                                if (capacity > int.MaxValue - unwindBytes) return -3;
                                capacity += unwindBytes;
                            }
                            if (kind == BranchIl.UpdateValue)
                            {
                                int fields = ObjectLayouts.FieldCount(objects, FieldAccess.Owner(records[slot + 3]));
                                if (fields > (int.MaxValue - capacity) / 16) return -3;
                                capacity += fields * 16;
                            }
                            int tags = 0;
                            if (kind == BranchIl.CheckedInteger || kind == BranchIl.CheckedConversion || kind == BranchIl.NewArray)
                                tags = ExceptionTypes.Overflow;
                            else if (kind == BranchIl.CastClass)
                                tags = ExceptionTypes.InvalidCast;
                            else if (kind == BranchIl.ArrayGet || kind == BranchIl.ArraySet || kind == BranchIl.StringCharacter)
                                tags = ExceptionTypes.IndexOutOfRange | ExceptionTypes.NullReference;
                            else if (kind == BranchIl.ArrayLength || kind == BranchIl.GetField ||
                                kind == BranchIl.SetField || kind == BranchIl.StringLength || kind == BranchIl.InvokeDelegate ||
                                kind == BranchIl.ThrowNull ||
                                kind == BranchIl.ExchangeHandlerField || (kind == BranchIl.Call && records[slot + 3] != 0))
                                tags = ExceptionTypes.NullReference;
                            else if (kind == BranchIl.Binary)
                            {
                                int type = states[pc * maximum + depths[pc] - 2];
                                if (BranchIl.IsIntegerDivision(operation, type))
                                    tags = BranchIl.IsSignedIntegerDivision(operation, type)
                                        ? ExceptionTypes.Arithmetic : ExceptionTypes.DivideByZero;
                            }
                            if (tags != 0)
                            {
                                if (capacity > int.MaxValue - 64) return -3;
                                capacity += 64;
                                exceptionTags |= tags;
                            }
                        }
                        if ((kind != BranchIl.Call && kind != BranchIl.NewObject) ||
                            depths[slot / BranchIl.Width] == 0)
                            continue;
                        int arguments = arities[records[slot + 1]];
                        if (arguments > (int.MaxValue - capacity) / 6)
                            return -3;
                        capacity += arguments * 6;
                    }
                    frameShape[0] = parameters;
                    frameShape[1] = locals;
                    frameShape[2] = maximum;
                    frameShape[3] = result;
                    frameShape[4] = objects[0];
                    frameShape[5] = 0;
                    frameShape[6] = 0;
                    frameShape[7] = length;
                    frameShape[8] = exceptionLocals;
                    frameShape[9] = hasFinally ? catchRegion.Length / ExceptionClauses.Width : 0;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        int kind = records[slot];
                        if (kind == BranchIl.JoinStrings || kind == BranchIl.StringFromChars) frameShape[5] = 1;
                        if (kind == BranchIl.CombineHandler || kind == BranchIl.RemoveHandler ||
                            (kind == BranchIl.InvokeDelegate && records[slot + 1] == EventHandlerReference))
                            frameShape[6] = 1;
                        if (kind == BranchIl.LoadEventArgsEmpty && depths[slot / BranchIl.Width] != 0)
                            eventArgsEmpty = true;
                    }
                    code = new byte[capacity];
                    used = BranchWasm.Emit(records, depths, states, frameShape, frame, signatures, offsets,
                        arities, code, remainderFixup, catchRegion, outgoingNulls, objects);
                }

                if (used > moduleCode.Length - length)
                    return -3;
                for (int i = 0; i < used; i++)
                    moduleCode[length + i] = code[i];
                sizes[current] = used;
                length += used;
            }
            CompileLocation.Clear(location);
            return WasmBinary.WriteModule(output, arities, offsets, signatures, sizes, functions,
                moduleCode, entries, entryOffsets, exports, objects, thunks, literals, literalSize[0], eventArgsEmpty, remainderFixup[0], exceptionTags);
        }

        private static int LinkOperands(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout,
            int[] heaps, int owner, int callerRow, int callerReceiver, int[] records, int[] owners, int[] rows, int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures, int[] objects, int[] thunks,
            byte[] literals, int[] literalSize, int functions, int[] location, int[] frame, int[] clauses)
        {
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
            {
                int kind = records[slot];
                if (kind == 0) continue;
                if (location.Length != 0) location[2] = slot / BranchIl.Width;
                if (kind == BranchIl.NewArray)
                {
                    uint token = unchecked((uint)records[slot + 1]);
                    int type = ArrayTypeRefs.ElementType(image, heaps, layout, token);
                    if (type == -2)
                    {
                        uint table = token >> 24;
                        if (table != 1 && table != 2) return -2;
                        int coded = (int)((token & 0xffffff) << 2) | (table == 1 ? 1 : 0);
                        int element = BundleLayouts.RegisterReference(main, bundle, ranges, image, layout, heaps, owner, coded, objects);
                        if (element < 0) return element;
                        type = ObjectLayouts.RegisterArray(objects, element);
                    }
                    if (type < 0) return type;
                    records[slot + 1] = type;
                }
                if (kind == BranchIl.CastClass || kind == BranchIl.TestClass)
                {
                    int type = BundleLayouts.RegisterReference(main, bundle, ranges, image, layout, heaps, owner,
                        records[slot + 1], objects);
                    if (type < 0) return type;
                    if (kind == BranchIl.CastClass && type == EventHandlerReference)
                        records[slot] = BranchIl.CastHandler;
                    else if (type < ObjectLayouts.FirstClass || ObjectLayouts.IsInterface(objects, type) ||
                        ObjectLayouts.ArrayElement(objects, type) != 0) return -2;
                    records[slot + 1] = type;
                    continue;
                }
                if (kind == BranchIl.LocalAddress)
                {
                    int lowered = ValueOperands.LowerLocal(main, bundle, ranges, image, layout, heaps,
                        owner, records, slot, frame, objects, clauses);
                    if (lowered < 0) return lowered;
                    continue;
                }
                if (kind == BranchIl.MethodSpecCall || kind == BranchIl.FieldToken || kind == BranchIl.InitializeValue)
                    return -2;
                if (kind == BranchIl.GetField || kind == BranchIl.SetField || kind == BranchIl.FieldAddress)
                {
                    int field = records[slot + 1];
                    bool linked = records[slot + 3] != 0;
                    if (linked)
                    {
                        if (bundle.Length == 0) return -2;
                        var linkedField = new int[2];
                        int status = AssemblyBundle.ResolveField(main, bundle, ranges, image, layout, heaps, owner,
                            field, kind == BranchIl.SetField, objects, linkedField);
                        if (status < 0) return status;
                        records[slot + 1] = linkedField[0];
                        records[slot + 3] = linkedField[1];
                    }
                    else
                    {
                        int fieldOwner = ObjectLayouts.Owner(image, heaps, layout, field, true);
                        if (fieldOwner < 0) return -1;
                        int type = ObjectLayouts.Find(objects, owner, fieldOwner, new int[0]);
                        if (type == 0)
                            type = BundleLayouts.Register(main, bundle, ranges, image, layout, heaps, owner, fieldOwner, objects);
                        if (type < 0) return type;
                        int fieldIndex = ObjectLayouts.FieldOrdinal(objects, type, field);
                        if (fieldIndex < 0) return -1;
                        records[slot + 1] = fieldIndex;
                        records[slot + 3] = FieldAccess.Encode(type, ObjectLayouts.FieldType(objects, type, field));
                    }
                    int descriptor = records[slot + 3];
                    if (kind == BranchIl.SetField)
                    {
                        int local = records[slot + BranchIl.Width + 1] - 1;
                        if (ObjectLayouts.IsValueType(objects, FieldAccess.Owner(descriptor)))
                        {
                            if (local < 0 || frame[local] != FieldAccess.Owner(descriptor)) return -2;
                            records[slot] = BranchIl.UpdateValue;
                        }
                        else if (local >= 0) return -2;
                    }
                    if (kind == BranchIl.FieldAddress)
                    {
                        int next = records[slot + 2];
                        if (ObjectLayouts.IsValueType(objects, FieldAccess.Storage(descriptor)) &&
                            next < records.Length / BranchIl.Width && records[next * BranchIl.Width] == BranchIl.GetField &&
                            !ValueOperands.Entry(records, next, clauses))
                            records[slot] = BranchIl.GetField;
                        else if (linked || !LinkExchange(image, heaps, layout, records, slot)) return -2;
                    }
                    continue;
                }
                if (kind == BranchIl.LoadString)
                {
                    int start = literalSize[0];
                    int characters = StringMetadata.CopyLiteral(image, heaps, records[slot + 1], literals, start);
                    if (characters < 0) return characters;
                    records[slot + 1] = start;
                    records[slot + 3] = characters;
                    literalSize[0] = start + characters * 2;
                    continue;
                }
                if (kind == BranchIl.MemberCall)
                {
                    int member = records[slot + 1], opcode = records[slot + 3];
                    int constructorType = Delegate;
                    if (owner == 0)
                    {
                        int eventMember = EventMetadata.Member(image, heaps, layout, member);
                        if (eventMember == EventMetadata.Empty && opcode == 0x7e)
                        {
                            records[slot] = BranchIl.LoadEventArgsEmpty;
                            continue;
                        }
                        if (eventMember == EventMetadata.Construct && opcode == 0x73)
                        {
                            records[slot] = BranchIl.NewEventArgs;
                            continue;
                        }
                        if (eventMember == EventMetadata.InvokeHandler && opcode == 0x6f)
                        {
                            records[slot] = BranchIl.InvokeDelegate;
                            records[slot + 1] = EventHandlerReference;
                            continue;
                        }
                        if (eventMember == EventMetadata.ConstructHandler && opcode == 0x73)
                            constructorType = EventHandlerReference;
                        if ((eventMember == EventMetadata.CombineHandler || eventMember == EventMetadata.RemoveHandler) && opcode == 0x28)
                        {
                            records[slot] = eventMember == EventMetadata.CombineHandler ? BranchIl.CombineHandler : BranchIl.RemoveHandler;
                            continue;
                        }
                    }
                    if (opcode == 0x7e) return -2;
                    if (opcode == 0x28 && ObjectLayouts.IsObjectConstructor(image, heaps, layout, member))
                    {
                        records[slot] = BranchIl.BaseCtor;
                        continue;
                    }
                    int stringMember = StringMetadata.Member(image, heaps, layout, member);
                    if (stringMember == StringMetadata.Concat && opcode == 0x28)
                    {
                        records[slot] = BranchIl.JoinStrings;
                        continue;
                    }
                    if (stringMember == StringMetadata.FromChars && opcode == 0x73)
                    {
                        records[slot] = BranchIl.StringFromChars;
                        continue;
                    }
                    if ((stringMember == StringMetadata.Length || stringMember == StringMetadata.Character) &&
                        (opcode == 0x6f || opcode == 0x28))
                    {
                        records[slot] = stringMember == StringMetadata.Length
                            ? BranchIl.StringLength : BranchIl.StringCharacter;
                        continue;
                    }
                    int delegateMember = DelegateMetadata.Member(image, heaps, layout, member);
                    if (delegateMember == DelegateMetadata.Invoke && opcode == 0x6f)
                    {
                        records[slot] = BranchIl.InvokeDelegate;
                        records[slot + 1] = Delegate;
                        continue;
                    }
                    if (delegateMember == 0 && stringMember == 0 && constructorType == Delegate && bundle.Length != 0)
                    {
                        var symbol = new int[3];
                        bool baseCall = opcode == 0x28 && callerReceiver >= ObjectLayouts.FirstClass &&
                            ObjectLayouts.IsConstructor(image, heaps, layout, callerRow);
                        int resolved = AssemblyBundle.Resolve(main, bundle, ranges, image, layout, heaps, owner, member, opcode, baseCall, objects, symbol);
                        if (resolved < 0) return resolved;
                        int targetOwner = symbol[0], targetRow = symbol[1];
                        int linked = indices[bases[targetOwner] + targetRow] - 1;
                        if (linked < 0)
                        {
                            byte[] targetImage = AssemblyBundle.Load(main, bundle, ranges, targetOwner);
                            var targetLayout = new int[4];
                            var targetHeaps = new int[8];
                            if (CompressedTables.FindWithHeaps(targetImage, targetLayout, targetHeaps) < 0)
                                return -1;
                            functions = Register(main, bundle, ranges, targetImage, targetLayout, targetHeaps, targetOwner, targetRow,
                                owners, rows, bases, indices, arities, offsets, signatures, objects, functions);
                            if (functions < 0) return functions;
                            linked = functions - 1;
                        }
                        if (!IsSharedBundleSignature(signatures, offsets[linked], arities[linked]))
                            return -2;
                        if (symbol[2] != 0)
                        {
                            int receiverType = signatures[offsets[linked] + 1];
                            if (signatures[offsets[linked]] != Void || receiverType < ObjectLayouts.FirstClass ||
                                (opcode == 0x73 ? ObjectLayouts.IsAbstract(objects, receiverType) :
                                    !ObjectLayouts.Inherits(objects, callerReceiver, receiverType) && callerReceiver != receiverType))
                                return -2;
                        }
                        records[slot] = opcode == 0x73 ? BranchIl.NewObject : BranchIl.Call;
                        records[slot + 1] = linked;
                        records[slot + 3] = opcode == 0x6f ? 1 : 0;
                        continue;
                    }
                    int pointer = slot - 6 * BranchIl.Width;
                    if ((constructorType == Delegate && delegateMember != DelegateMetadata.Construct) || opcode != 0x73 || pointer < 0 ||
                        records[pointer] != BranchIl.LoadFunction || records[pointer + 2] != slot / BranchIl.Width)
                        return -2;
                    records[slot] = BranchIl.NewDelegate;
                    records[slot + 1] = constructorType;
                    records[slot + 3] = records[pointer + 3];
                    continue;
                }
                if (kind != BranchIl.Call && kind != BranchIl.NewObject && kind != BranchIl.LoadFunction)
                    continue;
                int target = records[slot + 1];
                if (target < 1 || target > bases[owner + 1] - bases[owner])
                    return -1;
                if (kind == BranchIl.Call && records[slot + 3] == 0 &&
                    ObjectLayouts.IsCoreObjectConstructor(image, heaps, layout, target))
                {
                    records[slot] = BranchIl.BaseCtor;
                    continue;
                }
                int index = indices[bases[owner] + target] - 1;
                if (index < 0)
                {
                    functions = Register(main, bundle, ranges, image, layout, heaps, owner, target, owners, rows, bases, indices,
                        arities, offsets, signatures, objects, functions);
                    if (functions < 0) return functions;
                    index = functions - 1;
                }
                int signature = offsets[index];
                bool hasReceiver = arities[index] > 0 && StackType(signatures[signature + 1]) >= ObjectLayouts.FirstClass;
                if (kind == BranchIl.LoadFunction)
                {
                    if (!hasReceiver ||
                        (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x10) != 0)
                        return -2;
                    int next = records[slot + 2] * BranchIl.Width;
                    if (next >= records.Length || records[next] != BranchIl.MemberCall || records[next + 3] != 0x73)
                        return -2;
                    bool eventHandler = EventMetadata.Member(image, heaps, layout, records[next + 1]) == EventMetadata.ConstructHandler;
                    if (eventHandler)
                    {
                        if (arities[index] != 3 || signatures[signature] != Void ||
                            signatures[signature + 2] != ObjectReference || signatures[signature + 3] != EventArgsReference)
                            return -2;
                    }
                    else if (arities[index] != 1 || signatures[signature] != I32)
                        return -2;
                    int thunk = 0;
                    while (thunk < thunks[0] && thunks[thunk + 1] != index) thunk++;
                    if (thunk == thunks[0])
                    {
                        thunks[thunk + 1] = index;
                        thunks[0] = thunk + 1;
                    }
                    records[slot + 1] = thunk;
                    records[slot + 3] = signatures[signature + 1];
                    continue;
                }
                if (kind == BranchIl.NewObject)
                {
                    if (!hasReceiver || signatures[signature] != Void ||
                        !ObjectLayouts.IsConstructor(image, heaps, layout, target) ||
                        ObjectLayouts.IsAbstract(objects, signatures[signature + 1]))
                        return -2;
                }
                else if (records[slot + 3] != 0)
                {
                    if (!hasReceiver || (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x10) != 0)
                        return -2;
                }
                records[slot + 1] = index;
            }
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
            {
                int kind = records[slot];
                if ((kind == BranchIl.Branch || kind == BranchIl.Conditional) &&
                    records[records[slot + 1] * BranchIl.Width] == BranchIl.NewDelegate)
                {
                    if (location.Length != 0) location[2] = slot / BranchIl.Width;
                    return -1;
                }
            }
            return functions;
        }

        private static bool LinkExchange(byte[] image, int[] heaps, int[] layout, int[] records, int slot)
        {
            // Keep the interior reference confined to this fused operation.
            // Only local/argument loads can occur between ldflda and the call.
            if (FieldAccess.Storage(records[slot + 3]) != EventHandlerReference) return false;
            int replacement = records[slot + 2] * BranchIl.Width;
            if (replacement >= records.Length || records[replacement] != BranchIl.Load || records[replacement + 3] != 0)
                return false;
            int comparand = records[replacement + 2] * BranchIl.Width;
            if (comparand >= records.Length || records[comparand] != BranchIl.Load || records[comparand + 3] != 0)
                return false;
            int call = records[comparand + 2] * BranchIl.Width;
            if (call >= records.Length || records[call] != BranchIl.MethodSpecCall || records[call + 3] != 0 ||
                !EventMetadata.IsHandlerExchange(image, heaps, layout, records[call + 1]))
                return false;

            // The field token's operand bytes have no IR instructions. Keep
            // the two captured frame indices in their unused operand slots.
            records[slot + BranchIl.Width + 1] = records[replacement + 1];
            records[slot + 2 * BranchIl.Width + 1] = records[comparand + 1];
            records[slot] = BranchIl.ExchangeHandlerField;
            records[slot + 2] = records[call + 2];
            records[replacement] = 0;
            records[comparand] = 0;
            records[call] = 0;
            return true;
        }

        private static int Register(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout, int[] heaps, int owner, int row,
            int[] owners, int[] rows, int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures, int[] objects, int functions)
        {
            int offset = functions == 0 ? 0 : offsets[functions - 1] + arities[functions - 1] + 1;
            int arity = MethodShape(main, bundle, ranges, image, layout, heaps, owner, row, signatures, offset, objects);
            if (arity < 0)
                return arity;
            owners[functions] = owner;
            rows[functions] = row;
            indices[bases[owner] + row] = functions + 1;
            arities[functions] = arity;
            offsets[functions] = offset;
            return functions + 1;
        }

        // Class slots have already resolved to an assembly-qualified layout.
        private static bool IsSharedBundleType(int type) =>
            IsScalar(type) || type == String || type == ObjectReference || type == NullPointer || IsArray(StackType(type)) ||
            (type >= ObjectLayouts.FirstClass && type - ObjectLayouts.FirstClass < ObjectLayouts.MaxClasses);

        private static bool IsSharedBundleSignature(int[] types, int offset, int arity)
        {
            if (!IsSharedBundleType(types[offset]) && types[offset] != Void) return false;
            for (int index = 1; index <= arity; index++)
                if (!IsSharedBundleType(types[offset + index])) return false;
            return true;
        }

        private static int MethodShape(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout, int[] heaps, int assembly, int row, int[] types, int offset, int[] objects)
        {
            int attributes = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            if ((attributes & 0x2440) != 0 || (implementation & 7) != 0)
                return -2;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 0) return -1;
            int initialization = TypeInitializers.RejectDeclaringType(image, layout, heaps, owner);
            if (initialization < 0) return initialization;
            int receiver = 0;
            if ((attributes & 0x10) == 0)
            {
                receiver = BundleLayouts.Register(main, bundle, ranges, image, layout, heaps, assembly, owner, objects);
                if (receiver < 0) return receiver;
                if (ObjectLayouts.IsInterface(objects, receiver)) return -2;
            }
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            return SignatureTypes.Read(main, bundle, ranges, image, heaps, signature, types, offset, receiver, layout, objects, assembly);
        }
    }
}
