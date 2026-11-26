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
            return CompileProgram(BundleImages.Single(image), entries, output, location, 0);
        }

        internal static int CompileExports(byte[] image, byte[] entries, byte[] output) =>
            CompileProgram(BundleImages.Single(image), entries, output, new int[0], 0);

        internal static int CompileModuleDetailed(byte[] image, byte[] entries, byte[] output, int[] location)
        {
            if (location.Length < CompileLocation.Size) return -3;
            return CompileProgram(BundleImages.Single(image), entries, output, location, 0);
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
            BundleImages images = BundleImages.Unpack(bundle);
            return images == null ? -1 : CompileProgram(images, entries, output, location, 0);
        }

        // Imported functions precede defined functions. Discovery can add
        // a host feature, so re-emit with its function indices reserved.
        private static int CompileProgram(BundleImages bundle, byte[] entries, byte[] output, int[] location,
            int hostFeatures)
        {
            int hostImports = WasmImports.FunctionCount(hostFeatures);
            CompileLocation.Clear(location);
            byte[] image = bundle.Get(0);
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
            int mainInitialization = TypeInitializers.CheckModuleInitializer(image, layout, heaps, count);
            if (mainInitialization < 0) return mainInitialization;

            // A MethodDef row is local to its image. Give every row a disjoint
            // range in the global symbol index and record its owner on the queue.
            int modules = bundle.Count;
            var bases = new int[modules + 1];
            bases[1] = count;
            var interfaceTable = new int[3];
            int interfaces = InterfaceImplementations.ReadTable(image, heaps, layout, interfaceTable);
            if (interfaces < 0) return interfaces;
            for (int module = 1; module < modules; module++)
            {
                CompileLocation.Set(location, module, 0, -1);
                byte[] dependency = bundle.Get(module);
                var dependencyLayout = new int[4];
                var dependencyHeaps = new int[8];
                int size = CompressedTables.FindWithHeaps(dependency, dependencyLayout, dependencyHeaps);
                if (size < 0) return size == MethodDefinitions.Unsupported ? -2 : -1;
                int dependencyInterfaces = InterfaceImplementations.ReadTable(dependency, dependencyHeaps, dependencyLayout, interfaceTable);
                if (dependencyInterfaces < 0) return dependencyInterfaces;
                interfaces += dependencyInterfaces;
                int dependencyInitialization = TypeInitializers.CheckModuleInitializer(dependency, dependencyLayout, dependencyHeaps, size);
                if (dependencyInitialization < 0) return dependencyInitialization;
                if (size >= int.MaxValue - bases[module]) return -2;
                bases[module + 1] = bases[module] + size;
            }
            int symbols = bases[modules];
            // Field rows occupy at least six bytes. Each InterfaceImpl row
            // reserves up to two extra cells for the nominal edge.
            int imageBytes = bundle.Length == 0 ? image.Length : bundle.Length;
            // Closed instantiations have distinct field slices and argument
            // vectors even when they share a CLI definition.
            var objects = new int[ObjectLayouts.FieldMapStart + imageBytes / 6 + interfaces * 2 + output.Length / 4];
            // One MethodDef can have a body per closed declaring instantiation.
            // Reserve as many specialized bodies as definitions; exceeding that
            // is a reported capacity failure, never a shared approximation.
            if (symbols > int.MaxValue / 2 - 1) return -3;
            int functionCapacity = symbols * 2;
            var owners = new int[functionCapacity];
            var rows = new int[functionCapacity];
            var contexts = new int[functionCapacity];
            var instantiations = new MethodInstantiations();
            var indices = new int[symbols + 1];
            var arities = new int[functionCapacity];
            var offsets = new int[functionCapacity];
            var sizes = new int[functionCapacity];
            var bodyOffsets = new int[functionCapacity];
            var thunks = new int[functionCapacity + 1];
            var signatures = new int[output.Length];
            var frame = new int[output.Length];
            var virtualTypes = new int[output.Length];
            var virtualUsed = new int[1];
            var moduleCode = new byte[output.Length];
            var literals = new byte[output.Length];
            var literalSize = new int[1];
            var remainderFixup = new int[1];
            var reflection = new RuntimeReflectionPlan();
            var hostUse = new int[1];
            int functions = 0, length = 0;
            bool externalObjects = false;
            // Any reachable ldind/stind needs the host-lent memory 0 (decision 0060).
            bool linearMemory = false;
            var dispatch = new VirtualDispatch(output.Length);
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
                    functions = Register(bundle, image, layout, heaps, 0, (int)row, 0, owners, rows, contexts, bases, indices,
                        arities, offsets, signatures, objects, functions);
                    if (functions < 0) return functions;
                    index = functions - 1;
                }
                // Wasm sees interface parameters as eqref. Keep the host
                // from bypassing the linker's nominal assignment checks.
                for (int parameter = 1; parameter <= arities[index]; parameter++)
                {
                    int type = signatures[offsets[index] + parameter];
                    if (CanExchangeClass(objects, type)) externalObjects = true;
                    if (type == NullPointer || IsManagedLocation(type) || IsSpan(type) || RuntimeTypeMetadata.IsStorage(type) || ObjectLayouts.IsInterface(objects, type) || ObjectLayouts.IsValueType(objects, type))
                        return -2;
                }
                int result = signatures[offsets[index]];
                // A foreign Wasm caller can mutate an escaped class or array
                // that still has an alias inside the linked application.
                if (CanExchangeClass(objects, result)) externalObjects = true;
                if (result == NullPointer || IsSpan(result) || RuntimeTypeMetadata.IsStorage(result) || ObjectLayouts.IsValueType(objects, result)) return -2;
                exports[export] = index;
            }
            var body = new int[6];
            var frameShape = new int[20];
            var shadowStack = new int[3]; // $sp, base, limit globals + 1
            // Each emitted global needs at least five bytes in the destination.
            var globals = new int[1 + output.Length / 5 * 3];
            var initializers = new TypeInitializerPlan(functionCapacity, output.Length / 5 + 1);
            var initializerSlot = new int[1];
            int exceptionTags = 0;
            var noCatch = new int[0];
            var sizingBuffer = new byte[0];

            // Width evidence is read lazily, so metadata-only use of an
            // incompatible CoreLib stays available until one of its bodies is lowered.
            var coreLibWidths = new int[modules];
            int activeOwner = 0;
            for (int current = 0; ; current++)
            {
                if (current == functions)
                {
                    // Newly selected overrides can allocate additional runtime
                    // classes. Compile them and revisit only the new allocations.
                    int expanded = dispatch.Expand(bundle, objects, owners, rows, contexts, bases, indices,
                        arities, offsets, signatures, functions, location);
                    if (expanded < 0) return expanded;
                    if (expanded == functions) break;
                    functions = expanded;
                }
                if (rows[current] == 0) continue; // Synthetic dispatcher, emitted after discovery.
                int owner = owners[current], source = rows[current], context = contexts[current];
                if (context < 0) context = ~context;
                int[] methodArguments = instantiations.Arguments(current);
                CompileLocation.Set(location, owner, source, -1);
                if (owner != activeOwner)
                {
                    image = bundle.Get(owner);
                    layout = new int[4];
                    heaps = new int[8];
                    count = CompressedTables.FindWithHeaps(image, layout, heaps);
                    if (count < 0) return -1;
                    activeOwner = owner;
                }
                int compatibility = FrameworkPacks.CheckBodyOwner(image, layout, heaps, coreLibWidths, owner);
                if (compatibility < 0) return compatibility;
                // Stage 129 (decision 0057): an initializer body runs behind its guard. A
                // static method or constructor of a precise (non-BeforeFieldInit) type runs
                // the type's initializer on entry, after the caller evaluated the arguments.
                int initializerGuard = -1, initializerPrologue = -1;
                if (MethodNames.IsInitializer(image, layout, heaps, source))
                {
                    initializerGuard = initializers.Guard(current);
                    if (initializerGuard < 0) return -1;
                }
                else
                {
                    int declaringType = ObjectLayouts.Owner(image, heaps, layout, source, false);
                    if (declaringType < 1) return -1;
                    var initializerFlags = new int[1];
                    int initializer = TypeInitializers.Find(image, layout, heaps, declaringType, initializerFlags);
                    if (initializer < 0) return initializer;
                    int memberFlags = MethodDefinitions.ReadColumn(image, layout, source, 2);
                    if (initializer != 0 && initializerFlags[0] == 0 &&
                        ((memberFlags & 0x10) != 0 || ObjectLayouts.IsConstructor(image, heaps, layout, source)))
                    {
                        functions = initializers.Ensure(bundle, owner, declaringType, initializer, owners, rows, contexts,
                            bases, indices, arities, offsets, signatures, objects, globals, functions, initializerSlot);
                        if (functions < 0) return functions;
                        initializerPrologue = initializerSlot[0];
                    }
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
                        // Fixed-tag bits, plus the object tag and caught class when the
                        // clause type is a linked class (decision 0054).
                        int mask = ManagedExceptionClauses.ClauseMask(bundle, image, layout, heaps, owner,
                            catchRegion[clause + 5], objects);
                        if (mask < 0) return mask;
                        if (mask == 0) return -2;
                        catchRegion[clause + 5] = mask;
                    }
                    if (location.Length != 0) location[2] = -1;
                    // Every protected region also catches the object tag, so a finally
                    // cannot be skipped by a thrown managed object (decision 0054).
                    exceptionTags |= ExceptionTypes.Supported | ExceptionTypes.Object;
                }

                int parameters = arities[current];
                int signature = offsets[current];
                int result = signatures[signature];
                bool resourceOpen = reflection.ResourceOwner == owner && reflection.ResourceOpen == source;
                // The Wasm result of a value constructor is its receiver's final
                // value. Its IL returns void, so stack analysis checks void.
                bool valueConstructor = IsValueConstructor(image, layout, heaps, source, signatures, signature, parameters, objects);
                if (valueConstructor) result = Void;
                bool locationReceiver = parameters > 0 && IsValueLocation(signatures[signature + 1]);
                // Snapshot receivers retain the existing read-only ABI.
                bool valueMethod = !valueConstructor && !locationReceiver &&
                    IsValueMethod(image, layout, heaps, source, signatures, signature, parameters, objects);
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
                if (resourceOpen)
                {
                    locals = ResourceOpenIl.Frame(frame, parameters);
                    if (locals < 0) return locals;
                }
                else if (body[3] != 0)
                {
                    int localBlob = StandaloneSignatures.BlobIndex(image, heaps, layout, count, body[3]);
                    if (localBlob < 0)
                        return -1;
                    locals = SignatureTypes.Read(bundle, image, heaps, localBlob, frame, parameters, -1, layout, objects, owner, context, methodArguments);
                    if (locals < 0)
                        return locals;
                    // CorILMethod_InitLocals (body[4]) is deliberately not required. Without it
                    // ECMA-335 II.25.4.4 leaves local contents unspecified, and the CLR still zeroes
                    // GC references. Every Wasm local starts at zero or null, and value-type locals
                    // use null as their zeroed aggregate, so [SkipLocalsInit] bodies get zeroed locals
                    // too: a valid refinement of "unspecified". localloc (stackalloc) memory is not a
                    // Wasm local; it is not decoded and stays an explicit diagnostic (decision 0051).
                }
                if (body[1] > (int.MaxValue - 32) / 64)
                    return -2;

                var code = new byte[body[1] * 4 + 2];
                int used = scalar && locals == 0 && catchRegion.Length == 0 && initializerPrologue < 0
                    ? StraightLineIl.Translate(image, body, parameters, code) : -2;
                if (used < 0)
                {
                    var records = new int[body[1] * BranchIl.Width];
                    status = resourceOpen
                        ? ResourceOpenIl.Decode(image, body, records, location, parameters)
                        : BranchIl.Decode(image, body[0], body[1], parameters, locals, records, location, catchRegion.Length != 0);
                    if (status < 0)
                        return status;
                    status = ArrayInitializers.Lower(image, heaps, layout, records, literals, literalSize, location);
                    if (status < 0)
                        return status;
                    virtualUsed[0] = 0;
                    var inlineLocations = new int[parameters + locals];
                    functions = LinkOperands(bundle, image, layout, heaps, owner, source, context,
                        parameters == 0 ? 0 : signatures[signature + 1], valueConstructor || valueMethod || locationReceiver, valueMethod, valueConstructor, records, owners, rows, contexts,
                        bases, indices, arities, offsets, signatures, objects, thunks, globals, literals, literalSize, functions, location, frame, catchRegion, hostUse, virtualTypes, virtualUsed,
                        initializers, reflection, instantiations, methodArguments, inlineLocations, parameters);
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
                    var allocatedStates = new byte[virtualUsed[0] != 0 ? states.Length : 0];
                    int exceptionLocals = 0;
                    if (catchRegion.Length != 0)
                    {
                        exceptionLocals = ExceptionClauses.Validate(records, catchRegion, location);
                        if (exceptionLocals < 0) return exceptionLocals;
                    }
                    status = StackDepths.Analyze(records, depths, states, maximum, frame,
                        result, signatures, offsets, arities, location, catchRegion, outgoingNulls, objects, virtualTypes, allocatedStates);
                    if (status < 0)
                        return resourceOpen && status == -1 ? -2 : status;
                    if (resourceOpen)
                    {
                        hostUse[0] = hostUse[0] | WasmImports.Resources;
                        exceptionTags |= ExceptionTypes.Argument | ExceptionTypes.ArgumentNull;
                    }
                    if (virtualUsed[0] != 0)
                    {
                        functions = LinkVirtualCalls(bundle, records, depths, states, allocatedStates, maximum, virtualTypes,
                            owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions, location,
                            dispatch, externalObjects, owner, source);
                        if (functions < 0) return functions;
                    }

                    if (location.Length != 0) location[2] = -1;
                    int capacity = body[1] * 64 + 32;
                    for (int local = parameters; local < inlineLocations.Length; local++)
                    {
                        if (inlineLocations[local] == 0) continue;
                        if (capacity > int.MaxValue - 24) return -3;
                        capacity += 24;
                    }
                    if (catchRegion.Length != 0)
                    {
                        // One dispatch per fixed tag plus the object dispatch, whose clause arms add a
                        // nominal ref.test and clear the other kind's saved reference (decision 0054).
                        int handlerBytes = 32 + (ExceptionTagsWasm.Count + 1) * 20 +
                            catchRegion.Length / ExceptionClauses.Width * (48 * (ExceptionTagsWasm.Count + 1));
                        if (capacity > int.MaxValue - handlerBytes) return -3;
                        capacity += handlerBytes;
                    }
                    if (locals > (int.MaxValue - capacity) / 3)
                        return -3;
                    capacity += locals * 3;
                    if (narrowParameters > (int.MaxValue - capacity) / 16)
                        return -3;
                    capacity += narrowParameters * 16;
                    if (initializerGuard >= 0 || initializerPrologue >= 0)
                        capacity += TypeInitializerWasm.Capacity;
                    if (resourceOpen)
                    {
                        if (capacity > int.MaxValue - 320) return -3;
                        capacity += 320;
                    }
                    bool allocates = false;
                    int classBytes = objects[0] * 12;
                    if (classBytes > int.MaxValue - capacity) return -3;
                    capacity += classBytes;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        int kind = records[slot];
                        if (kind == BranchIl.ConsoleWrite && depths[slot / BranchIl.Width] != 0)
                        {
                            if (capacity > int.MaxValue - ConsoleWasm.WriteCapacity) return -3;
                            capacity += ConsoleWasm.WriteCapacity;
                        }
                        if (kind == BranchIl.RemoveHandler && depths[slot / BranchIl.Width] != 0)
                        {
                            // The structured search and copy expand a single IL call.
                            if (capacity > int.MaxValue - 320) return -3;
                            capacity += 320;
                        }
                        int pc = slot / BranchIl.Width;
                        int operation = records[slot + 1];
                        if ((kind == BranchIl.LoadIndirect || kind == BranchIl.StoreIndirect) && depths[pc] != 0)
                        {
                            int address = pc * maximum + depths[pc] - (kind == BranchIl.LoadIndirect ? 2 : 3);
                            if (IsNativeInteger(states[address])) linearMemory = true;
                        }
                        // Stage 143 (decision 0071): p->F loads and stores also address memory 0.
                        bool fieldStore = kind == BranchIl.SetField || kind == BranchIl.UpdateConstructorField;
                        bool nativeField = (kind == BranchIl.GetField || fieldStore) && depths[pc] != 0 &&
                            records.Length > slot + 2 * BranchIl.Width + 3 && records[slot + 2 * BranchIl.Width + 2] != 0 &&
                            IsNativeInteger(states[pc * maximum + depths[pc] - 1 - (fieldStore ? 2 : 1)]);
                        if (nativeField) linearMemory = true;
                        if (kind == BranchIl.LocalAllocate && depths[pc] != 0)
                        {
                            // An initializer's guard and a shadow-stack frame both wrap the
                            // body; combining them is not supported yet.
                            if (initializerGuard >= 0)
                            {
                                if (location.Length != 0) location[2] = pc;
                                return -2;
                            }
                            linearMemory = true;
                            allocates = true;
                            if (shadowStack[0] == 0)
                                for (int g = 0; g < 3; g++)
                                {
                                    int global = StaticFields.Register(globals, 0, int.MinValue + 1 + g, I32);
                                    if (global < 0) return global;
                                    shadowStack[g] = global + 1;
                                }
                            if (capacity > int.MaxValue - StackAllocationWasm.Capacity) return -3;
                            capacity += StackAllocationWasm.Capacity;
                        }
                        if (kind == BranchIl.LoadStaticField || kind == BranchIl.StoreStaticField)
                        {
                            if (capacity > int.MaxValue - TypeInitializerWasm.TriggerCapacity) return -3;
                            capacity += TypeInitializerWasm.TriggerCapacity;
                        }
                        if (kind == BranchIl.NewObject && depths[pc] != 0)
                        {
                            int allocated = signatures[offsets[operation] + 1];
                            if (allocated >= ObjectLayouts.FirstClass && !ObjectLayouts.IsValueType(objects, allocated))
                                dispatch.AddAllocation(allocated);
                        }
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
                            if (kind == BranchIl.UpdateValue || kind == BranchIl.UpdateConstructorField)
                            {
                                int fields = ObjectLayouts.FieldCount(objects, FieldAccess.Owner(records[slot + 3]));
                                if (fields > (int.MaxValue - capacity) / 16) return -3;
                                capacity += fields * 16;
                            }
                            if (kind == BranchIl.ReadReflection)
                            {
                                int bytes = reflection.BodyCapacity(int.MaxValue - capacity);
                                if (bytes < 0) return bytes;
                                capacity += bytes;
                            }
                            int inlineLocal = kind == BranchIl.Load || kind == BranchIl.Store ? operation :
                                kind == BranchIl.ConstructValue ? records[slot + 3] : -1;
                            if (inlineLocal >= 0 && inlineLocations[inlineLocal] != 0)
                            {
                                int cells = ObjectLayouts.FieldCount(objects, frame[inlineLocal]);
                                if (cells > (int.MaxValue - capacity) / 40) return -3;
                                capacity += cells * 40;
                            }
                            int tags = 0;
                            if (kind == BranchIl.CheckedInteger || kind == BranchIl.CheckedConversion || kind == BranchIl.NewArray ||
                                (kind == BranchIl.MathCall && MathWasm.CanOverflow(operation)))
                                tags = ExceptionTypes.Overflow;
                            else if (kind == BranchIl.NewDelegate)
                                tags = ExceptionTypes.Argument;
                            else if (kind == BranchIl.UnboxValue)
                                tags = ExceptionTypes.InvalidCast | ExceptionTypes.NullReference;
                            else if (kind == BranchIl.CastClass)
                                tags = ExceptionTypes.InvalidCast;
                            else if (kind == BranchIl.SpanRead || kind == BranchIl.SpanAddress)
                                tags = ExceptionTypes.IndexOutOfRange;
                            else if (kind == BranchIl.ArrayGet || kind == BranchIl.ArraySet || kind == BranchIl.StringCharacter ||
                                kind == BranchIl.ObjectElementAddress)
                                tags = ExceptionTypes.IndexOutOfRange | ExceptionTypes.NullReference;
                            else if (kind == BranchIl.ArrayLength || kind == BranchIl.GetField ||
                                kind == BranchIl.SetField || nativeField || kind == BranchIl.StringLength || kind == BranchIl.InvokeDelegate ||
                                kind == BranchIl.ThrowNull || kind == BranchIl.ReadReflection ||
                                kind == BranchIl.LoadIndirect || kind == BranchIl.StoreIndirect || kind == BranchIl.TakeValueFieldAddress ||
                                kind == BranchIl.LoadObjectIndirect || kind == BranchIl.StoreObjectIndirect ||
                                kind == BranchIl.ExchangeHandlerField || (kind == BranchIl.Call && records[slot + 3] != 0))
                                tags = ExceptionTypes.NullReference;
                            else if (kind == BranchIl.Binary)
                            {
                                int type = states[pc * maximum + depths[pc] - 2];
                                if (BranchIl.IsIntegerDivision(operation, type))
                                    tags = BranchIl.IsSignedIntegerDivision(operation, type)
                                        ? ExceptionTypes.Arithmetic : ExceptionTypes.DivideByZero;
                            }
                            // A throw may raise a managed object on the object tag (decision 0054).
                            if (kind == BranchIl.ThrowNull) tags |= ExceptionTypes.Object;
                            if (tags != 0)
                            {
                                if (capacity > int.MaxValue - 64) return -3;
                                capacity += 64;
                                exceptionTags |= tags;
                            }
                        }
                        if ((kind != BranchIl.Call && kind != BranchIl.NewObject && kind != BranchIl.ConstructValue) ||
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
                    frameShape[10] = hostImports;
                    frameShape[11] = valueConstructor ? 1 : 0;
                    frameShape[12] = initializerPrologue + 1;
                    frameShape[13] = initializerGuard + 1;
                    frameShape[14] = initializers.Epoch + 1;
                    frameShape[15] = initializers.Active + 1;
                    frameShape[16] = allocates ? shadowStack[0] : 0;
                    frameShape[17] = allocates ? shadowStack[1] : 0;
                    frameShape[18] = allocates ? shadowStack[2] : 0;
                    frameShape[19] = resourceOpen ? ((hostFeatures & WasmImports.Console) != 0 ? ConsoleWasm.ImportCount + 1 : 1) : 0;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        int kind = records[slot];
                        if (kind == BranchIl.JoinStrings || kind == BranchIl.StringFromChars || kind == BranchIl.StringEquals ||
                            kind == BranchIl.ConsoleWrite) frameShape[5] = 1;
                        if (kind == BranchIl.CombineHandler || kind == BranchIl.RemoveHandler ||
                            (kind == BranchIl.InvokeDelegate && records[slot + 1] == EventHandlerReference))
                            frameShape[6] = 1;
                    }
                    code = new byte[capacity];
                    used = BranchWasm.Emit(records, depths, states, frameShape, frame, signatures, offsets,
                        arities, code, remainderFixup, catchRegion, outgoingNulls, objects, initializers.FieldInitializers, reflection, inlineLocations);
                }

                if (used < 0) return used;
                if (used > moduleCode.Length - length)
                    return -3;
                for (int i = 0; i < used; i++)
                    moduleCode[length + i] = code[i];
                sizes[current] = used;
                bodyOffsets[current] = length;
                length += used;
            }
            if ((hostUse[0] & ~hostFeatures) != 0)
                return CompileProgram(bundle, entries, output, location, hostFeatures | hostUse[0]);
            length = dispatch.Write(moduleCode, bodyOffsets, sizes, arities, offsets, signatures, length, hostImports, location);
            if (length < 0) return length;
            if (initializers.Epoch >= 0 || shadowStack[0] != 0)
            {
                // Stage 129 (decision 0057): every host export enters through a wrapper
                // that retires initializers interrupted by an earlier raw trap. Internal
                // calls keep the original bodies, so recursion stays in the same epoch.
                for (int export = 0; export < exportCount; export++)
                {
                    if (functions >= owners.Length) return -3;
                    int target = exports[export], arity = arities[target];
                    int offset = NextSignature(offsets, arities, functions);
                    if (arity >= signatures.Length - offset) return -3;
                    for (int i = 0; i <= arity; i++) signatures[offset + i] = signatures[offsets[target] + i];
                    owners[functions] = owners[target];
                    rows[functions] = 0; // synthetic
                    contexts[functions] = contexts[target];
                    arities[functions] = arity;
                    offsets[functions] = offset;
                    bodyOffsets[functions] = length;
                    length = TypeInitializerWasm.ExportEntry(moduleCode, length, initializers.Epoch, initializers.Active,
                        hostImports + target, arity, shadowStack[0] - 1, shadowStack[2] - 1);
                    if (length < 0) return length;
                    sizes[functions] = length - bodyOffsets[functions];
                    exports[export] = functions++;
                }
            }
            CompileLocation.Clear(location);
            if (linearMemory) hostFeatures |= WasmImports.Memory;
            return WasmBinary.WriteModule(output, arities, offsets, signatures, sizes, bodyOffsets, functions,
                moduleCode, entries, entryOffsets, exports, objects, thunks, literals, literalSize[0], globals, remainderFixup[0], exceptionTags, hostFeatures, reflection.HeapFixups[0]);
        }

        private static int LinkOperands(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int callerRow, int context, int callerReceiver, bool valueReceiver, bool readOnlyReceiver, bool valueConstructor, int[] records, int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures, int[] objects, int[] thunks,
            int[] globals, byte[] literals, int[] literalSize, int functions, int[] location, int[] frame, int[] clauses, int[] hostUse, int[] virtualTypes, int[] virtualUsed,
            TypeInitializerPlan initializers, RuntimeReflectionPlan reflection, MethodInstantiations instantiations, int[] methodArguments,
            int[] inlineLocations, int parameterCount)
        {
            int callerType = 0; // The caller's TypeDef, read once when a member needs access checks.
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
            {
                int kind = records[slot];
                if (kind == 0) continue;
                if (location.Length != 0) location[2] = slot / BranchIl.Width;
                if (kind == BranchIl.LoadTypeToken)
                {
                    int global = RuntimeTypeTokens.Bind(bundle, image, layout, heaps, owner, records[slot + 1], globals);
                    if (global < 0) return global;
                    records[slot + 1] = global;
                    continue;
                }
                if (kind == BranchIl.NewArray)
                {
                    uint token = unchecked((uint)records[slot + 1]);
                    int type = ArrayTypeRefs.ElementType(image, heaps, layout, token);
                    if (type == -2)
                    {
                        uint table = token >> 24;
                        if (table != 1 && table != 2) return -2;
                        int coded = (int)((token & 0xffffff) << 2) | (table == 1 ? 1 : 0);
                        // A String vector holds references to the immutable String struct.
                        int element = PlatformTypeReferences.IsString(image, heaps, layout, coded) ? String :
                            BundleLayouts.RegisterReference(bundle, image, layout, heaps, owner, coded, objects);
                        if (element < 0) return element;
                        type = ObjectLayouts.RegisterArray(objects, element);
                    }
                    if (type < 0) return type;
                    records[slot + 1] = type;
                }
                if (kind == BranchIl.BoxValue || kind == BranchIl.UnboxValue || kind == BranchIl.TestClass)
                {
                    int operand = records[slot + 1];
                    if (kind == BranchIl.TestClass)
                    {
                        int table = (operand & 3) == 1 ? 1 : 2;
                        operand = table << 24 | operand >> 2;
                    }
                    int element = PrimitiveBoxing.Element(bundle, image, layout, heaps,
                        owner, operand, context, methodArguments, objects);
                    if (element > 0)
                    {
                        int boxing = PrimitiveBoxing.Layout(objects);
                        if (boxing < 0) return boxing;
                        if (kind == BranchIl.TestClass) records[slot] = BranchIl.TestBox;
                        records[slot + 1] = element;
                        records[slot + 3] = boxing;
                        continue;
                    }
                    if (kind != BranchIl.TestClass || element != -2) return element;
                }
                if (kind == BranchIl.ObjectElementAddress || kind == BranchIl.LoadObjectIndirect || kind == BranchIl.StoreObjectIndirect)
                {
                    if (kind == BranchIl.ObjectElementAddress && records[slot + 3] == BranchIl.BoundObjectElement)
                        continue;
                    if (kind == BranchIl.ObjectElementAddress &&
                        BundleLayouts.RegisterReference(bundle, image, layout, heaps, owner, records[slot + 1], objects) != ObjectReference)
                        return -2;
                    int array = ObjectLayouts.RegisterArray(objects, ObjectReference);
                    if (array < 0) return array;
                    records[slot + 1] = array;
                    continue;
                }
                if (kind == BranchIl.CastClass || kind == BranchIl.TestClass)
                {
                    // A descriptor is a Type on the CLR, even after widening to Object.
                    // These targets need reflection-aware casts before ordinary layout matching applies.
                    if (RuntimeTypeMetadata.RequiresReflectionCast(image, heaps, layout, records[slot + 1])) return -2;
                    int coded = records[slot + 1];
                    int type = PlatformTypeReferences.IsString(image, heaps, layout, coded) ? String :
                        BundleLayouts.RegisterReference(bundle, image, layout, heaps, owner, coded, objects);
                    if (type < 0) return type;
                    if (kind == BranchIl.CastClass && type == EventHandlerReference)
                        records[slot] = BranchIl.CastHandler;
                    else if (type != String && (type < ObjectLayouts.FirstClass || ObjectLayouts.IsInterface(objects, type) ||
                        ObjectLayouts.ArrayElement(objects, type) != 0)) return -2;
                    records[slot + 1] = type;
                    continue;
                }
                if (valueReceiver && (kind == BranchIl.Load || kind == BranchIl.Store) && records[slot + 1] == 0)
                {
                    // starg.0 would replace the address itself.
                    if (kind == BranchIl.Store) return -2;
                    int lowered = ValueOperands.LowerReceiver(bundle, image, layout, heaps,
                        owner, bases[owner + 1] - bases[owner], records, slot, frame, objects, clauses, readOnlyReceiver);
                    if (lowered == -2 && valueConstructor)
                        records[slot] = BranchIl.LoadConstructorAddress;
                    else if (lowered < 0)
                        return lowered;
                    continue;
                }
                if (kind == BranchIl.FrameAddress)
                {
                    // The value-type this argument is already a managed address.
                    // ldarga.0 would instead take the address of that argument slot.
                    if (valueReceiver && records[slot + 1] == 0) return -2;
                    int type = frame[records[slot + 1]];
                    if (IsSpan(type))
                    {
                        int span = SpanOperands.Bind(bundle, image, layout, heaps, owner, context, methodArguments,
                            records, slot, type, objects, clauses);
                        if (span < 0) return span;
                        if (span != 0) continue;
                    }
                    if (records[slot + 1] >= parameterCount)
                    {
                        int stable = InlineObjectAddresses.Bind(bundle, image, layout, heaps, owner, context,
                            methodArguments, records, slot, frame, inlineLocations, objects, clauses);
                        if (stable < 0) return stable;
                        if (stable != 0) continue;
                    }
                    int lowered = ValueOperands.LowerFrame(bundle, image, layout, heaps,
                        owner, bases[owner + 1] - bases[owner], records, slot, frame, objects, clauses);
                    if (lowered < 0) return lowered;
                    continue;
                }
                if (kind == BranchIl.ConstrainedStatic)
                {
                    var symbol = new int[3];
                    int status = StaticInterfaceSlots.Resolve(bundle, image, layout, heaps, owner, records[slot + 1],
                        records[slot + 3], context, methodArguments, objects, symbol);
                    if (status < 0) return status;
                    int targetOwner = symbol[0], row = symbol[1], targetContext = symbol[2];
                    int function = Lookup(owners, rows, contexts, bases, indices, functions, targetOwner, row, targetContext);
                    if (function < 0)
                    {
                        byte[] implementationImage = bundle.Get(targetOwner);
                        var targetLayout = new int[4];
                        var targetHeaps = new int[8];
                        if (CompressedTables.FindWithHeaps(implementationImage, targetLayout, targetHeaps) < 0) return -1;
                        functions = Register(bundle, implementationImage, targetLayout, targetHeaps, targetOwner, row, targetContext,
                            owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions);
                        if (functions < 0) return functions;
                        function = functions - 1;
                    }
                    records[slot] = BranchIl.Call;
                    records[slot + 1] = function;
                    records[slot + 3] = 0;
                    continue;
                }
                if (kind == BranchIl.MethodSpecCall)
                {
                    bool checkReceiver = (records[slot + 3] & BranchIl.MethodSpecCallvirt) != 0;
                    var counts = new int[45];
                    if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
                    var spec = new int[MethodSpecifications.Width];
                    int status = MethodSpecifications.Read(image, heaps, layout, counts, records[slot + 1], spec);
                    if (status < 0) return status;
                    int method = spec[MethodSpecifications.Target];
                    int row = method >> 1, targetOwner = owner, targetContext = 0;
                    byte[] targetImage = image;
                    int[] targetLayout = layout, targetHeaps = heaps;
                    if ((method & 1) != 0)
                    {
                        if (bundle.Length == 0 || (checkReceiver && !MemberHasThis(image, heaps, layout, row))) return -2;
                        if (callerType == 0) callerType = ObjectLayouts.Owner(image, heaps, layout, callerRow, false);
                        if (callerType < 0) return -1;
                        var symbol = new int[5];
                        status = AssemblyBundle.Resolve(bundle, image, layout, heaps, owner, row, checkReceiver ? 0x6f : 0x28, false, objects, symbol,
                            context, callerType, methodArity: spec[MethodSpecifications.Arity], callerMethodArguments: methodArguments);
                        if (status < 0) return status;
                        targetOwner = symbol[0];
                        row = symbol[1];
                        targetContext = symbol[3];
                        targetImage = bundle.Get(targetOwner);
                        targetLayout = new int[4];
                        targetHeaps = new int[8];
                        if (CompressedTables.FindWithHeaps(targetImage, targetLayout, targetHeaps) < 0) return -1;
                    }
                    else
                    {
                        int targetType = ObjectLayouts.Owner(image, heaps, layout, row, false);
                        if (targetType < 0) return targetType;
                        if (context != 0 && targetType == ObjectLayouts.TypeDef(objects, context)) targetContext = context;
                    }
                    int methodFlags = MethodDefinitions.ReadColumn(targetImage, targetLayout, row, 2);
                    bool instance = (methodFlags & 0x10) == 0;
                    // Generic virtual slots and value receivers need their own dispatch or location ABI.
                    if ((checkReceiver && !instance) || (methodFlags & 0x40) != 0) return -2;
                    var types = new int[spec[MethodSpecifications.Arity]];
                    status = MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner,
                        context, objects, methodArguments, spec, types);
                    if (status < 0) return status;
                    var deferred = new int[8];
                    int constraints = GenericParameters.Check(targetImage, targetHeaps, targetLayout, row,
                        types, objects, deferred, method: true);
                    if (constraints < 0) return constraints;
                    if (constraints != 0)
                    {
                        constraints = BundleConstraints.Check(bundle, targetImage, targetLayout, targetHeaps,
                            targetOwner, objects, types, deferred, method: true, context: targetContext);
                        if (constraints < 0) return constraints;
                    }

                    int function = instantiations.Find(owners, rows, contexts, functions, targetOwner, row, targetContext, types);
                    if (function < 0)
                    {
                        functions = Register(bundle, targetImage, targetLayout, targetHeaps, targetOwner, row, targetContext,
                            owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions, methodArguments: types);
                        if (functions < 0) return functions;
                        function = functions - 1;
                        if (instance)
                        {
                            int receiver = signatures[offsets[function] + 1];
                            if (!IsObjectReference(receiver) || ObjectLayouts.IsValueType(objects, receiver)) return -2;
                        }
                        // Reserve before decoding the body so self-recursion reuses the slot.
                        instantiations.Record(function, owners.Length, types);
                    }
                    records[slot] = BranchIl.Call;
                    records[slot + 1] = function;
                    // A nonvirtual callvirt still tests the receiver after argument evaluation.
                    records[slot + 3] = checkReceiver ? 1 : 0;
                    continue;
                }
                if (kind == BranchIl.FieldToken || kind == BranchIl.InitializeValue)
                    return -2;
                if (kind == BranchIl.GetField || kind == BranchIl.SetField || kind == BranchIl.FieldAddress)
                {
                    int field = records[slot + 1];
                    bool linked = records[slot + 3] != 0;
                    if (linked)
                    {
                        if (bundle.Length == 0) return -2;
                        var linkedField = new int[2];
                        if (callerType == 0) callerType = ObjectLayouts.Owner(image, heaps, layout, callerRow, false);
                        if (callerType < 0) return -1;
                        int status = AssemblyBundle.ResolveField(bundle, image, layout, heaps, owner,
                            field, kind == BranchIl.SetField, false, objects, globals, linkedField, context, callerType, methodArguments);
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
                            type = BundleLayouts.Register(bundle, image, layout, heaps, owner, fieldOwner, objects);
                        // Stage 143 (decision 0071): a struct reached only through S* has no class layout.
                        if (type == -2)
                            type = BundleLayouts.RegisterValue(bundle, image, layout, heaps, owner, fieldOwner, objects);
                        if (type < 0) return type;
                        int fieldIndex = ObjectLayouts.FieldOrdinal(objects, type, field);
                        if (fieldIndex < 0) return -1;
                        records[slot + 1] = fieldIndex;
                        records[slot + 3] = FieldAccess.Encode(type, ObjectLayouts.FieldType(objects, type, field));
                        if (ObjectLayouts.IsValueType(objects, type) && records.Length > slot + 2 * BranchIl.Width + 3)
                        {
                            // Stage 143 (decision 0071): the same field through an S* address in memory 0.
                            var native = new int[2];
                            if (NativeValueLayouts.Field(bundle, owner, fieldOwner, field, native) == 0)
                            {
                                records[slot + 2 * BranchIl.Width + 2] = native[0] + 1;
                                records[slot + 2 * BranchIl.Width + 3] = native[1];
                            }
                        }
                    }
                    int descriptor = records[slot + 3];
                    // Stage 143 (decision 0071): a recorded native offset means an S* receiver may reach this field.
                    bool pointerField = records.Length > slot + 2 * BranchIl.Width + 3 && records[slot + 2 * BranchIl.Width + 2] != 0;
                    if (kind == BranchIl.SetField)
                    {
                        int local = records[slot + BranchIl.Width + 1] - 1;
                        if (ObjectLayouts.IsValueType(objects, FieldAccess.Owner(descriptor)))
                        {
                            if (local < 0)
                            {
                                // A constructor can also write through an ordinary pointer.
                                // StackDepths distinguishes that receiver from the managed this.
                                if (valueConstructor && frame[0] == FieldAccess.Owner(descriptor))
                                    records[slot] = BranchIl.UpdateConstructorField;
                                else if (!pointerField)
                                    return -2;
                                continue;
                            }
                            int locationType = frame[local];
                            if (IsValueLocation(locationType))
                            {
                                if (ObjectLayouts.IsExplicitValue(objects, LocatedValue(locationType))) return -2;
                                locationType = LocatedValue(locationType);
                            }
                            if (locationType != FieldAccess.Owner(descriptor)) return -2;
                            records[slot] = BranchIl.UpdateValue;
                        }
                        else if (local >= 0) return -2;
                    }
                    if (kind == BranchIl.FieldAddress)
                    {
                        int next = records[slot + 2];
                        if (pointerField && !ObjectLayouts.IsValueType(objects, FieldAccess.Storage(descriptor)))
                            continue; // &p->F of a primitive field: native address arithmetic
                        if (ObjectLayouts.IsValueType(objects, FieldAccess.Storage(descriptor)) &&
                            next < records.Length / BranchIl.Width && records[next * BranchIl.Width] == BranchIl.GetField &&
                            !ValueOperands.Entry(records, next, clauses))
                            records[slot] = BranchIl.GetField;
                        else if (ManagedValueLocationWasm.UsesCell(objects, FieldAccess.Owner(descriptor), FieldAccess.Storage(descriptor)))
                        {
                            int status = ValueOperands.LowerClassField(image, layout, heaps, bases[owner + 1] - bases[owner],
                                records, slot, FieldAccess.Storage(descriptor), clauses);
                            if (status < 0) return status;
                            records[slot] = BranchIl.TakeValueFieldAddress;
                        }
                        else if (linked || !LinkExchange(image, heaps, layout, records, slot)) return -2;
                    }
                    continue;
                }
                if (kind == BranchIl.SizeOf)
                {
                    uint token = unchecked((uint)records[slot + 1]);
                    uint table = token >> 24;
                    int row = (int)(token & 0xffffff);
                    if (table != 1 && table != 2) return -2; // TypeSpec, including generic T
                    var identity = new int[2];
                    int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner,
                        row << 2 | (table == 1 ? 1 : 0), identity);
                    if (status < 0) return status;
                    var measured = new int[2];
                    status = NativeValueLayouts.Measure(bundle, identity[0], identity[1], measured);
                    if (status < 0) return status;
                    records[slot] = BranchIl.Constant;
                    records[slot + 1] = measured[0];
                    continue;
                }
                if (kind == BranchIl.LoadStaticField || kind == BranchIl.StoreStaticField)
                {
                    var symbol = new int[5];
                    int field = records[slot + 1];
                    bool write = kind == BranchIl.StoreStaticField;
                    bool linked = records[slot + 3] != 0;
                    if (!write && linked && EventMetadata.Member(image, heaps, layout, field) == EventMetadata.Empty)
                    {
                        int eventGlobal = StaticFields.Register(globals, StaticFields.EventOwner, 0, EventArgsReference);
                        if (eventGlobal < 0) return eventGlobal;
                        records[slot] = BranchIl.LoadEventArgsEmpty;
                        records[slot + 1] = eventGlobal;
                        continue;
                    }

                    if (linked ? StringMetadata.Member(image, heaps, layout, field) == StringMetadata.Empty :
                        StringMetadata.IsEmptyDefinition(image, heaps, layout, field))
                    {
                        if (write) return -2;
                        int empty = StringLiterals.LowerEmpty(records, slot, globals);
                        if (empty < 0) return empty;
                        continue;
                    }

                    if (linked && bundle.Length == 0) return -2;
                    int status = linked ? AssemblyBundle.ResolveField(bundle, image, layout, heaps, owner,
                        field, write, true, objects, globals, symbol, context, callerType, methodArguments) :
                        StaticFields.ResolveLocal(bundle, image, layout, heaps, owner, field, write, objects, globals, symbol, callerRow);
                    if (status < 0) return status;
                    if (symbol[3] != 0)
                    {
                        // Bound declaring owner, so an inherited field initializes only its base.
                        var initializer = new int[1];
                        functions = initializers.Ensure(bundle, symbol[2], symbol[4], symbol[3], owners, rows, contexts,
                            bases, indices, arities, offsets, signatures, objects, globals, functions, initializer);
                        if (functions < 0) return functions;
                        initializers.SetFieldInitializer(symbol[0], initializer[0]);
                    }
                    records[slot + 1] = symbol[0];
                    records[slot + 3] = symbol[1];
                    continue;
                }
                if (kind == BranchIl.LoadString)
                {
                    int status = StringLiterals.Lower(image, heaps, records, slot, literals, literalSize, globals);
                    if (status < 0) return status;
                    continue;
                }
                if ((kind == BranchIl.Call || kind == BranchIl.MemberCall) && records[slot + 3] ==
                    (kind == BranchIl.Call ? 0 : 0x28) && records[slot + BranchIl.Width + 1] == 0 &&
                    (kind == BranchIl.MemberCall || (uint)(records[slot + 1] - 1) < (uint)(bases[owner + 1] - bases[owner])) &&
                    SpanMetadata.StringAsSpan(image, layout, heaps, records[slot + 1], kind == BranchIl.MemberCall))
                {
                    records[slot] = BranchIl.StringAsSpan;
                    continue;
                }
                if (kind == BranchIl.MemberCall)
                {
                    int member = records[slot + 1], opcode = records[slot + 3];
                    // Bind addressed receivers through the member metadata.
                    // Platform shortcuts do not consume a managed location.
                    int address = records[slot + BranchIl.Width + 1];
                    bool addressed = address != 0;
                    if (!addressed && (opcode == 0x73 || opcode == 0x28))
                    {
                        int span = SpanMetadata.ClassifyCharacterSpanCall(bundle, image, layout, heaps,
                            owner, member, context, methodArguments, objects);
                        if (span == BranchIl.CharacterArrayAsSpan && opcode == 0x73 ||
                            span == BranchIl.SpanAsReadOnly && opcode == 0x28)
                        {
                            records[slot] = span;
                            records[slot + 3] = 0;
                            continue;
                        }
                    }
                    // A static member would receive the address as a byref argument.
                    if (addressed && (opcode != 0x28 || bundle.Length == 0 || !MemberHasThis(image, heaps, layout, member)))
                        return -2;
                    if (opcode == 0x28 && !addressed && RuntimeTypeMetadata.IsGetTypeFromHandle(image, heaps, layout, member))
                    {
                        records[slot] = BranchIl.TypeFromHandle;
                        continue;
                    }
                    int reflectionMember = !addressed && (opcode == 0x28 || opcode == 0x6f)
                        ? ReflectionMembers.Identify(image, heaps, layout, member, opcode == 0x28) : 0;
                    if (reflectionMember == ReflectionMembers.ExecutingAssembly)
                    {
                        int status = reflection.Bind(bundle, globals, literals, literalSize);
                        if (status < 0) return status;
                        // The executing method retains the assembly of its authored IL.
                        // Slot zero is reserved for symbolic types without an image.
                        records[slot] = BranchIl.LoadStaticField;
                        records[slot + 1] = reflection.Slots[(owner + 1) * RuntimeReflectionPlan.Width + RuntimeReflectionPlan.Assembly];
                        records[slot + 3] = RuntimeTypeMetadata.Assembly;
                        continue;
                    }
                    if (reflectionMember == ReflectionMembers.ResourceStream)
                    {
                        int status = reflection.BindResource(bundle);
                        if (status < 0) return status;
                        status = reflection.Bind(bundle, globals, literals, literalSize);
                        if (status < 0) return status;
                        int targetOwner = reflection.ResourceOwner;
                        int row = reflection.ResourceOpen;
                        int linked = Lookup(owners, rows, contexts, bases, indices, functions, targetOwner, row, 0);
                        if (linked < 0)
                        {
                            byte[] targetImage = bundle.Get(targetOwner);
                            var targetLayout = new int[4];
                            var targetHeaps = new int[8];
                            if (CompressedTables.FindWithHeaps(targetImage, targetLayout, targetHeaps) < 0) return -1;
                            functions = Register(bundle, targetImage, targetLayout, targetHeaps, targetOwner, row, 0,
                                owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions,
                                allowVirtual: true, receiverOverride: RuntimeTypeMetadata.Assembly);
                            if (functions < 0) return functions;
                            linked = functions - 1;
                        }
                        if (signatures[offsets[linked] + 1] != RuntimeTypeMetadata.Assembly) return -2;
                        records[slot] = BranchIl.Call;
                        records[slot + 1] = linked;
                        records[slot + 3] = 1; // callvirt receiver null check
                        continue;
                    }
                    if (reflectionMember != 0)
                    {
                        int status = reflection.Bind(bundle, globals, literals, literalSize);
                        if (status < 0) return status;
                        records[slot] = BranchIl.ReadReflection;
                        records[slot + 1] = reflectionMember;
                        continue;
                    }
                    // Exact System.Math intrinsics, identified by platform assembly and signature (decision 0003).
                    int intrinsic = opcode == 0x28 && !addressed ? PlatformMembers.MathIntrinsic(image, heaps, layout, member) : 0;
                    if (intrinsic != 0)
                    {
                        records[slot] = BranchIl.MathCall;
                        records[slot + 1] = intrinsic;
                        continue;
                    }
                    int constructorType = Delegate;
                    // Platform event members are bound by identity in every bundled assembly.
                    if (!addressed)
                    {
                        int eventMember = EventMetadata.Member(image, heaps, layout, member);
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
                    int consoleMember = addressed ? 0 : ConsoleMetadata.Member(image, heaps, layout, member);
                    if (consoleMember == ConsoleMetadata.Unsupported) return -2;
                    if (consoleMember != 0)
                    {
                        // Accessors are static calls. Writes must dispatch virtually:
                        // a non-virtual call would bypass the console's synchronized writer.
                        bool accessor = consoleMember == ConsoleMetadata.StandardOutput ||
                            consoleMember == ConsoleMetadata.StandardError;
                        if (opcode != (accessor ? 0x28 : 0x6f)) return -2;
                        records[slot] = accessor ? BranchIl.ConsoleWriter : BranchIl.ConsoleWrite;
                        records[slot + 1] = accessor ? ConsoleWasm.Stream(consoleMember) : consoleMember;
                        hostUse[0] = hostUse[0] | WasmImports.Console;
                        continue;
                    }
                    // call Object::.ctor runs the empty base constructor; newobj allocates a
                    // fresh plain object of the canonical root (decision 0057).
                    if ((opcode == 0x28 || opcode == 0x73) && !addressed &&
                        ObjectLayouts.IsObjectConstructor(image, heaps, layout, member))
                    {
                        records[slot] = opcode == 0x73 ? BranchIl.NewRootObject : BranchIl.BaseCtor;
                        continue;
                    }
                    int stringMember = addressed ? 0 : StringMetadata.Member(image, heaps, layout, member);
                    if (stringMember == StringMetadata.OrdinalEquals && opcode == 0x28)
                    {
                        records[slot] = BranchIl.StringEquals;
                        continue;
                    }
                    bool joinStrings = stringMember == StringMetadata.Concat && opcode == 0x28;
                    if (joinStrings || stringMember == StringMetadata.FromChars && opcode == 0x73)
                    {
                        int empty = StringLiterals.Empty(globals);
                        if (empty < 0) return empty;
                        records[slot] = joinStrings ? BranchIl.JoinStrings : BranchIl.StringFromChars;
                        records[slot + 1] = empty;
                        continue;
                    }
                    if ((stringMember == StringMetadata.Length || stringMember == StringMetadata.Character) &&
                        (opcode == 0x6f || opcode == 0x28))
                    {
                        records[slot] = stringMember == StringMetadata.Length
                            ? BranchIl.StringLength : BranchIl.StringCharacter;
                        continue;
                    }
                    int delegateMember = addressed ? 0 : DelegateMetadata.Member(image, heaps, layout, member);
                    if (delegateMember == DelegateMetadata.Invoke && opcode == 0x6f)
                    {
                        records[slot] = BranchIl.InvokeDelegate;
                        records[slot + 1] = Delegate;
                        continue;
                    }
                    if (delegateMember == 0 && stringMember == 0 && constructorType == Delegate && bundle.Length != 0)
                    {
                        var symbol = new int[5];
                        bool baseCall = opcode == 0x28 && callerReceiver >= ObjectLayouts.FirstClass &&
                            ObjectLayouts.IsConstructor(image, heaps, layout, callerRow);
                        if (callerType == 0) callerType = ObjectLayouts.Owner(image, heaps, layout, callerRow, false);
                        if (callerType < 0) return -1;
                        int resolved = AssemblyBundle.Resolve(bundle, image, layout, heaps, owner, member, opcode, baseCall, objects, symbol,
                            context, callerType, allowVirtual: !addressed, valueAddress: addressed, callerMethodArguments: methodArguments);
                        if (resolved < 0) return resolved;
                        int targetOwner = symbol[0], targetRow = symbol[1], targetContext = symbol[3];
                        if (address < 0) targetContext = ~targetContext;
                        int linked = Lookup(owners, rows, contexts, bases, indices, functions, targetOwner, targetRow, targetContext);
                        bool virtualMethod = symbol[4] != 0;
                        bool deferred = opcode == 0x6f && virtualMethod;
                        if (deferred && targetContext != 0) return -2;
                        if (deferred || linked < 0)
                        {
                            byte[] targetImage = bundle.Get(targetOwner);
                            var targetLayout = new int[4];
                            var targetHeaps = new int[8];
                            if (CompressedTables.FindWithHeaps(targetImage, targetLayout, targetHeaps) < 0) return -1;
                            if (deferred)
                            {
                                int staged = StageVirtualCall(bundle, targetImage, targetLayout, targetHeaps,
                                    targetOwner, targetRow, objects, records, slot, virtualTypes, virtualUsed);
                                if (staged < 0) return staged;
                                continue;
                            }
                            functions = Register(bundle, targetImage, targetLayout, targetHeaps, targetOwner, targetRow,
                                targetContext, owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions, allowVirtual: virtualMethod);
                            if (functions < 0) return functions;
                            linked = functions - 1;
                        }
                        if (!IsSharedBundleSignature(signatures, offsets[linked], arities[linked]))
                            return -2;
                        if (address < 0)
                        {
                            if (!BindLocationCall(records, slot, linked, -address, arities, offsets, signatures)) return -2;
                            continue;
                        }
                        bool linkedValue = symbol[2] != 0 &&
                            ObjectLayouts.IsValueType(objects, signatures[offsets[linked] + 1]) &&
                            signatures[offsets[linked]] == signatures[offsets[linked] + 1] &&
                            IsLinkedConstructor(bundle, targetOwner, targetRow);
                        if (addressed || linkedValue)
                        {
                            bool linkedMethod = !linkedValue && addressed && opcode == 0x28 &&
                                IsLinkedValueMethod(bundle, targetOwner, targetRow, signatures, offsets[linked],
                                    arities[linked], objects);
                            int lowered = LinkValueConstruction(records, slot, opcode == 0x73, linked, linkedValue, linkedMethod,
                                readOnlyReceiver, arities, offsets, signatures, frame, clauses);
                            if (lowered < 0) return lowered;
                            continue;
                        }
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
                if (kind == BranchIl.Call)
                {
                    int accessor = StringMetadata.AccessorDefinition(image, heaps, layout, target);
                    if (accessor != 0)
                    {
                        // CoreLib's accessors read the desktop layout. Other
                        // String methods retain their managed IL and use the GC string ABI.
                        records[slot] = accessor == StringMetadata.Length ? BranchIl.StringLength : BranchIl.StringCharacter;
                        continue;
                    }
                }
                if ((kind == BranchIl.NewObject || kind == BranchIl.Call && records[slot + 3] == 0) &&
                    ObjectLayouts.IsCoreObjectConstructor(image, heaps, layout, target))
                {
                    records[slot] = kind == BranchIl.NewObject ? BranchIl.NewRootObject : BranchIl.BaseCtor;
                    continue;
                }
                bool virtualMethodLocal = (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x40) != 0;
                if (virtualMethodLocal && kind == BranchIl.LoadFunction) return -2;
                if (virtualMethodLocal && kind == BranchIl.Call && records[slot + 3] != 0)
                {
                    int staged = StageVirtualCall(bundle, image, layout, heaps, owner, target,
                        objects, records, slot, virtualTypes, virtualUsed);
                    if (staged < 0) return staged;
                    continue;
                }
                int fieldReceiver = kind == BranchIl.Call ? records[slot + BranchIl.Width + 1] : 0;
                int key = fieldReceiver < 0 ? -1 : 0;
                int index = Lookup(owners, rows, contexts, bases, indices, functions, owner, target, key);
                if (index < 0)
                {
                    functions = Register(bundle, image, layout, heaps, owner, target, key, owners, rows, contexts, bases, indices,
                        arities, offsets, signatures, objects, functions, allowVirtual: virtualMethodLocal && kind == BranchIl.Call);
                    if (functions < 0) return functions;
                    index = functions - 1;
                }
                if (fieldReceiver < 0)
                {
                    if (!BindLocationCall(records, slot, index, -fieldReceiver, arities, offsets, signatures)) return -2;
                    continue;
                }
                int signature = offsets[index];
                bool hasReceiver = arities[index] > 0 && StackType(signatures[signature + 1]) >= ObjectLayouts.FirstClass;
                bool localValue = kind != BranchIl.LoadFunction &&
                    IsValueConstructor(image, layout, heaps, target, signatures, signature, arities[index], objects);
                bool addressedCall = kind == BranchIl.Call && records[slot + BranchIl.Width + 1] != 0;
                if (localValue || addressedCall)
                {
                    if (kind == BranchIl.Call && records[slot + 3] != 0) return -2; // callvirt on a constructor
                    bool localMethod = !localValue && addressedCall && records[slot + 3] == 0 &&
                        IsValueMethod(image, layout, heaps, target, signatures, signature, arities[index], objects);
                    int lowered = LinkValueConstruction(records, slot, kind == BranchIl.NewObject, index, localValue, localMethod,
                        readOnlyReceiver, arities, offsets, signatures, frame, clauses);
                    if (lowered < 0) return lowered;
                    continue;
                }
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

        private static bool BindLocationCall(int[] records, int slot, int function, int storage,
            int[] arities, int[] offsets, int[] signatures)
        {
            int argumentCount = records[slot + 2 * BranchIl.Width + 1];
            if (argumentCount != arities[function] || signatures[offsets[function] + 1] != ValueLocation(storage)) return false;
            records[slot] = BranchIl.Call;
            records[slot + 1] = function;
            records[slot + 3] = 0;
            return true;
        }

        // Value-type constructors use the receiver-in, value-out ABI of
        // decision 0042. newobj supplies the zero default; call on a
        // location's address supplies that location's current value.
        private static int LinkValueConstruction(int[] records, int slot, bool newObject, int function, bool valueConstructor,
            bool valueMethod, bool readOnlyReceiver, int[] arities, int[] offsets, int[] signatures, int[] frame, int[] clauses)
        {
            int local = records[slot + BranchIl.Width + 1] - 1;
            if (newObject)
            {
                // [arguments] -> newobj -> [S] on a fresh unaliased value.
                if (!valueConstructor || local >= 0) return -2;
                records[slot] = BranchIl.NewObject;
                records[slot + 1] = function;
                records[slot + 3] = 0;
                return 0;
            }
            // Any other call through a value address (a byref receiver or a
            // virtual method) would need general managed byrefs.
            if (local < 0 || !valueConstructor && !valueMethod) return -2;
            int arguments = records[slot + 2 * BranchIl.Width + 1] - 1;
            if (arities[function] != arguments + 1 || frame[local] != signatures[offsets[function] + 1]) return -2;
            if (valueMethod)
            {
                // [&value, arguments] -> call S::M -> [result]. The callee only
                // reads its receiver (decision 0049), and the address does not
                // escape this frame, so passing the location's current aggregate
                // observes exactly what the CLR's byref would. The address scan
                // already replaced ldloca with a load of the local.
                records[slot] = BranchIl.Call;
                records[slot + 1] = function;
                records[slot + 3] = 0;
                return 0;
            }
            // A read-only method cannot construct its own receiver in place.
            if (readOnlyReceiver && local == 0) return -2;
            // The callee's writes reach the location only when it returns. If
            // the constructor throws inside a protected range, a handler or
            // finally in this frame could observe the CLR's partly constructed
            // location, which this ABI cannot show. (Roslyn emits newobj there.)
            int pc = slot / BranchIl.Width;
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
                if (pc >= clauses[clause + ExceptionClauses.TryStart] &&
                    pc < clauses[clause + ExceptionClauses.TryStart] + clauses[clause + ExceptionClauses.TryLength]) return -2;
            records[slot] = BranchIl.ConstructValue;
            records[slot + 1] = function;
            records[slot + 3] = local;
            return 0;
        }

        // An instance .ctor on a value type, after MethodShape gave it the value result.
        private static bool IsValueConstructor(byte[] image, int[] layout, int[] heaps, int row, int[] signatures, int signature,
            int arity, int[] objects) =>
            arity > 0 && ObjectLayouts.IsValueType(objects, signatures[signature + 1]) &&
            signatures[signature] == signatures[signature + 1] &&
            (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 &&
            ObjectLayouts.IsConstructor(image, heaps, layout, row);

        // The MemberRef signature declares HASTHIS (ECMA-335 II.23.2.1).
        private static bool MemberHasThis(byte[] image, int[] heaps, int[] layout, int member)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, member, reference, allowClosedParent: true) < 0) return false;
            int index = reference[5];
            if (index < 1 || index >= heaps[1]) return false;
            var next = new int[1];
            int end = heaps[0] + heaps[1];
            int size = ImageBytes.CompressedUnsigned(image, heaps[0] + index, end, next);
            return size > 0 && size <= end - next[0] && (image[next[0]] & 0x20) != 0;
        }

        // A non-constructor instance method whose receiver is a value type.
        private static bool IsValueMethod(byte[] image, int[] layout, int[] heaps, int row, int[] signatures, int signature,
            int arity, int[] objects) =>
            arity > 0 && ObjectLayouts.IsValueType(objects, signatures[signature + 1]) &&
            (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0 &&
            !ObjectLayouts.IsConstructor(image, heaps, layout, row);

        private static bool IsLinkedValueMethod(BundleImages bundle, int owner, int row,
            int[] signatures, int signature, int arity, int[] objects)
        {
            byte[] targetImage = bundle.Get(owner);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(targetImage, targetLayout, targetHeaps) < 0) return false;
            return IsValueMethod(targetImage, targetLayout, targetHeaps, row, signatures, signature, arity, objects);
        }

        private static bool IsLinkedConstructor(BundleImages bundle, int owner, int row)
        {
            byte[] targetImage = bundle.Get(owner);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(targetImage, targetLayout, targetHeaps) < 0) return false;
            return ObjectLayouts.IsConstructor(targetImage, targetHeaps, targetLayout, row);
        }

        private static int StageVirtualCall(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int row, int[] objects, int[] records, int slot,
            int[] types, int[] used)
        {
            int declaring = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (declaring < 1) return -1;
            int receiver = BundleLayouts.Register(bundle, image, layout, heaps, owner, declaring, objects);
            if (receiver < 0) return receiver;
            if (ObjectLayouts.IsValueType(objects, receiver)) return -2;
            int shape = used[0];
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int arity = SignatureTypes.Read(bundle, image, heaps, blob, types, shape,
                receiver, layout, objects, owner, 0);
            if (arity < 1) return arity < 0 ? arity : -1;
            if (!IsSharedBundleSignature(types, shape, arity)) return -2;
            used[0] = shape + arity + 1;
            records[slot] = BranchIl.VirtualCall;
            records[slot + 1] = shape;
            records[slot + 3] = arity;
            // The following four IL bytes are the token operand. Their
            // record kinds remain zero, so no control-flow edge enters them.
            records[slot + BranchIl.Width + 1] = owner;
            records[slot + BranchIl.Width + 3] = row;
            return 0;
        }

        private static int LinkVirtualCalls(BundleImages bundle, int[] records,
            int[] depths, int[] states, byte[] allocatedStates, int maximum, int[] types, int[] owners, int[] rows, int[] contexts, int[] bases,
            int[] indices, int[] arities, int[] offsets, int[] signatures, int[] objects, int functions, int[] location,
            VirtualDispatch dispatch, bool externalObjects, int callerOwner, int callerRow)
        {
            for (int pc = 0; pc < depths.Length; pc++)
            {
                int slot = pc * BranchIl.Width;
                if (records[slot] != BranchIl.VirtualCall || depths[pc] == 0) continue;
                if (location.Length != 0) location[2] = pc;
                int shape = records[slot + 1], arity = records[slot + 3];
                int receiver = pc * maximum + depths[pc] - 1 - arity;
                int actual = states[receiver], declared = types[shape + 1];
                int bound = actual == NullValue ? declared : actual;
                int owner = records[slot + BranchIl.Width + 1], row = records[slot + BranchIl.Width + 3];
                bool allocated = allocatedStates[receiver] != 0;
                int function;
                if (allocated || ObjectLayouts.IsSealed(objects, bound))
                {
                    function = BindVirtualBody(bundle, objects, actual, allocated, declared, owner, row,
                        owners, rows, contexts, bases, indices, arities, offsets, signatures, functions);
                    if (function < 0) return function;
                    if (!VirtualSignaturesMatch(arities, offsets, signatures, function, types, shape, arity, actual, objects)) return -2;
                }
                else
                {
                    if (externalObjects || bound < ObjectLayouts.FirstClass ||
                        ObjectLayouts.IsValueType(objects, bound) || !Assignable(bound, declared, objects)) return -2;
                    function = dispatch.Add(functions, owner, row, bound, declared, callerOwner, callerRow, pc);
                    if (function < 0) return function;
                    if (function == functions)
                    {
                        if (functions >= rows.Length) return -3;
                        int offset = NextSignature(offsets, arities, functions);
                        if (arity >= signatures.Length - offset) return -3;
                        arities[function] = arity;
                        offsets[function] = offset;
                        for (int i = 0; i <= arity; i++) signatures[offset + i] = types[shape + i];
                        signatures[offset + 1] = bound;
                    }
                }

                if (function == functions) functions++;
                records[slot] = BranchIl.Call;
                records[slot + 1] = function;
                records[slot + 3] = 1; // Preserve the callvirt null check.
            }
            return functions;
        }

        internal static int BindVirtualBody(BundleImages bundle, int[] objects, int actual, bool allocated,
            int declared, int owner, int row, int[] owners, int[] rows, int[] contexts, int[] bases,
            int[] indices, int[] arities, int[] offsets, int[] signatures, int functions)
        {
            var symbol = new int[2];
            int status = VirtualSlots.SelectVirtualBody(bundle, objects, actual, allocated, declared, owner, row, symbol);
            if (status < 0) return status;
            owner = symbol[0];
            row = symbol[1];
            int function = indices[bases[owner] + row] - 1;
            if (function >= 0) return function;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x400) != 0) return -2;
            status = Register(bundle, image, layout, heaps, owner, row, 0, owners, rows, contexts, bases,
                indices, arities, offsets, signatures, objects, functions, allowVirtual: true);
            return status < 0 ? status : functions;
        }

        internal static bool VirtualSignaturesMatch(int[] arities, int[] offsets, int[] signatures, int function,
            int[] types, int shape, int arity, int actual, int[] objects)
        {
            // The original body may narrow the receiver. Every other part of
            // the declared slot signature must match.
            int bound = offsets[function];
            if (arities[function] != arity || signatures[bound] != types[shape] ||
                !Assignable(actual, signatures[bound + 1], objects)) return false;
            for (int arg = 2; arg <= arity; arg++)
                if (signatures[bound + arg] != types[shape + arg]) return false;
            return true;
        }

        private static bool CanExchangeClass(int[] objects, int type)
        {
            while (type >= ObjectLayouts.FirstClass)
            {
                int element = ObjectLayouts.ArrayElement(objects, type);
                if (element == 0) return !ObjectLayouts.IsValueType(objects, type);
                type = element;
            }
            return type == ObjectLayouts.ObjectReference || type == Delegate || type == EventHandlerReference;
        }

        private static int NextSignature(int[] offsets, int[] arities, int functions) =>
            functions == 0 ? 0 : offsets[functions - 1] + arities[functions - 1] + 1;

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

        // A function is identified by (assembly, MethodDef, closed declaring
        // layout). Nongeneric definitions use context zero and the dense
        // per-row index; specialized bodies are found by their full key.
        private static int Lookup(int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int functions,
            int owner, int row, int context)
        {
            if (context == 0) return indices[bases[owner] + row] - 1;
            for (int function = 0; function < functions; function++)
                if (contexts[function] == context && rows[function] == row && owners[function] == owner) return function;
            return -1;
        }

        internal static int Register(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner, int row,
            int context, int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures,
            int[] objects, int functions, bool allowVirtual = false, bool initializer = false, int receiverOverride = 0,
            int[] methodArguments = null)
        {
            if (functions >= owners.Length) return -3;
            int offset = NextSignature(offsets, arities, functions);
            // A complemented context is the same method specialized for a traced receiver.
            int arity = MethodShape(bundle, image, layout, heaps, owner, row, context < 0 ? ~context : context,
                signatures, offset, objects, allowVirtual, initializer, receiverOverride, methodArguments);
            if (arity < 0) return arity;
            if (context < 0)
            {
                if (!IsValueMethod(image, layout, heaps, row, signatures, offset, arity, objects)) return -2;
                signatures[offset + 1] = ValueLocation(signatures[offset + 1]);
            }
            owners[functions] = owner;
            rows[functions] = row;
            contexts[functions] = context;
            if (context == 0 && methodArguments == null) indices[bases[owner] + row] = functions + 1;
            arities[functions] = arity;
            offsets[functions] = offset;
            return functions + 1;
        }

        // Class slots have already resolved to an assembly-qualified layout.
        private static bool IsSharedBundleType(int type) =>
            IsScalar(type) || type == String || type == ObjectReference || type == NullPointer ||
            RuntimeTypeMetadata.IsStorage(type) || IsSpan(type) || IsArray(StackType(type)) || IsValueLocation(type) ||
            // The platform EventArgs and EventHandler representations are
            // compiler-owned and bound by identity, so every assembly shares them.
            type == EventArgsReference || type == EventHandlerReference ||
            (type >= ObjectLayouts.FirstClass && type - ObjectLayouts.FirstClass < ObjectLayouts.MaxClasses);

        private static bool IsSharedBundleSignature(int[] types, int offset, int arity)
        {
            if (!IsSharedBundleType(types[offset]) && types[offset] != Void) return false;
            for (int index = 1; index <= arity; index++)
                if (!IsSharedBundleType(types[offset + index])) return false;
            return true;
        }

        private static int MethodShape(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int assembly, int row,
            int context, int[] types, int offset, int[] objects, bool allowVirtual, bool initializer, int receiverOverride,
            int[] methodArguments)
        {
            int attributes = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            if ((attributes & 0x2400) != 0 || (!allowVirtual && (attributes & 0x40) != 0) || (implementation & 7) != 0)
                return -2;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 0) return -1;
            bool isStatic = (attributes & 0x10) != 0;
            // The initializer itself is reached only through its guard (decision 0057).
            if (isStatic && MethodNames.IsInitializer(image, layout, heaps, row) != initializer) return -2;
            var initializerFlags = new int[1];
            int initialization = TypeInitializers.Find(image, layout, heaps, owner, initializerFlags);
            if (initialization < 0) return initialization;
            // A generic type has one initializer per closed instantiation, which needs
            // per-instantiation guards. Only a precise type runs it on a method call; a
            // beforefieldinit generic type waits for a static field access, and generic
            // static fields are still diagnosed (Stage 108). A precise value type also
            // triggers on instance methods. Both stay explicit diagnostics.
            if (initialization != 0 && initializerFlags[0] == 0 && (context != 0 ||
                GenericParameters.Check(image, heaps, layout, owner, new int[0], objects, new int[8]) != 0))
                return -2;
            if (context != 0)
            {
                // The closed layout must instantiate this method's own definition.
                if (ObjectLayouts.AssemblyOf(objects, context) != assembly || ObjectLayouts.TypeDef(objects, context) != owner)
                    return -1;
            }
            else
            {
                // An open generic body needs a closed declaring instantiation.
                int open = GenericParameters.Check(image, heaps, layout, owner, new int[0], objects, new int[8]);
                if (open != 0) return open < 0 ? open : -2;
            }
            // Compiler-owned representations can execute trusted managed bodies
            // without registering the desktop object's physical layout.
            int receiver = isStatic ? 0 : receiverOverride != 0 ? receiverOverride : context;
            if (!isStatic && receiver == 0 && PlatformTypeReferences.IsString(image, heaps, layout, owner << 2))
                receiver = String;
            if (!isStatic && receiver == 0)
            {
                receiver = BundleLayouts.Register(bundle, image, layout, heaps, assembly, owner, objects);
                // Constructors (decision 0042) and non-virtual instance methods
                // (decision 0049) take a value-type receiver. A method body that
                // writes through its receiver is diagnosed when it is compiled.
                if (receiver == -2 && (ObjectLayouts.IsConstructor(image, heaps, layout, row) || (attributes & 0x40) == 0))
                    receiver = BundleLayouts.RegisterValue(bundle, image, layout, heaps, assembly, owner, objects);
                if (receiver < 0) return receiver;
                if (ObjectLayouts.IsInterface(objects, receiver)) return -2;
                if (initialization != 0 && initializerFlags[0] == 0 && ObjectLayouts.IsValueType(objects, receiver) &&
                    !ObjectLayouts.IsConstructor(image, heaps, layout, row)) return -2;
            }
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int arity = SignatureTypes.Read(bundle, image, heaps, signature, types, offset, receiver, layout, objects, assembly, context, methodArguments);
            if (arity > 0 && !isStatic && ObjectLayouts.IsValueType(objects, receiver) &&
                ObjectLayouts.IsConstructor(image, heaps, layout, row))
            {
                // CLI: void .ctor(S& this, ...). Wasm: (S this, ...) -> S. The
                // receiver is the location's current aggregate on entry and the
                // constructed aggregate on return (decision 0042).
                if (types[offset] != Void) return -1;
                types[offset] = receiver;
            }
            return arity;
        }
    }
}
