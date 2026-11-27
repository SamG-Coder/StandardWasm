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
            return CompileProgram(BundleImages.Single(image), entries, output, location, 0, new MissingAssemblyStubs());
        }

        internal static int CompileExports(byte[] image, byte[] entries, byte[] output) =>
            CompileProgram(BundleImages.Single(image), entries, output, new int[0], 0, new MissingAssemblyStubs());

        internal static int CompileModuleDetailed(byte[] image, byte[] entries, byte[] output, int[] location)
        {
            if (location.Length < CompileLocation.Size) return -3;
            return CompileProgram(BundleImages.Single(image), entries, output, location, 0, new MissingAssemblyStubs());
        }

        internal static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            CompileBundleCore(bundle, entries, output, new int[0]);

        internal static int CompileBundleDetailed(byte[] bundle, byte[] entries, byte[] output, int[] location)
        {
            if (location.Length < CompileLocation.Size) return -3;
            return CompileBundleCore(bundle, entries, output, location);
        }

        // Internal experiment. Normal application compilation does not expose the root table.
        internal static int CompileBundleWithRootProbe(byte[] bundle, byte[] entries, byte[] output, int[] location)
        {
            if (location.Length < CompileLocation.Size) return -3;
            CompileLocation.Clear(location);
            BundleImages images = BundleImages.Unpack(bundle);
            return images == null ? -1 : CompileProgram(images, entries, output, location, 0, new MissingAssemblyStubs(), true);
        }

        private static int CompileBundleCore(byte[] bundle, byte[] entries, byte[] output, int[] location)
        {
            CompileLocation.Clear(location);
            BundleImages images = BundleImages.Unpack(bundle);
            return images == null ? -1 : CompileProgram(images, entries, output, location, 0, new MissingAssemblyStubs());
        }

        // Issue #299 (opt in): a method whose body needs an assembly missing from the bundle (-4)
        // compiles to a stub that fails on entry, as the CLR's JIT fails that method and the
        // call site throws FileNotFoundException. Each -4 names the method being compiled; it is
        // recorded and the program is recompiled. warnings receives [count, then
        // (assembly, MethodDef row, IL offset) per stubbed method].
        internal static int CompileBundleDeferred(byte[] bundle, byte[] entries, byte[] output, int[] location, int[] warnings)
        {
            if (location.Length < CompileLocation.Size || warnings.Length < 1) return -3;
            BundleImages images = BundleImages.Unpack(bundle);
            if (images == null) return -1;
            images.DeferMissing = true;
            int capacity = (warnings.Length - 1) / 3;
            var stubs = new MissingAssemblyStubs();
            warnings[0] = 0;
            while (true)
            {
                images.MissingReference = null;
                int status = CompileProgram(images, entries, output, location, 0, stubs);
                if (status != -4 || location[1] <= 0) return status;
                // No progress (a stub could not be lowered), no exact display name, or no room:
                // report the strict located diagnostic.
                if (stubs.Find(location[0], location[1]) >= 0 || stubs.Count == capacity) return status;
                int offset = location[2];
                if (!stubs.Add(location[0], location[1], images)) return status;
                int count = stubs.Count - 1;
                warnings[1 + 3 * count] = location[0];
                warnings[2 + 3 * count] = location[1];
                warnings[3 + 3 * count] = offset;
                warnings[0] = stubs.Count;
            }
        }

        // The stub body (decision for #299): ldstr message; ldstr fileName; newobj CoreLib
        // FileNotFoundException::.ctor(string, string); throw. Pre-linked, so it bypasses IL decode.
        private static int LinkStub(BundleImages bundle, MissingAssemblyStubs stubs, int stub, int[] records,
            int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets,
            int[] signatures, int[] objects, int functions, byte[] literals, int[] literalSize, int[] globals)
        {
            var found = new int[2];
            if (MissingAssemblyStubs.FindConstructor(bundle, found) < 0 || found[0] < 0) return -4;
            byte[] coreLib = bundle.Get(found[0]);
            var coreLayout = new int[4];
            var coreHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(coreLib, coreLayout, coreHeaps) < 0) return -1;
            int constructor = indices[bases[found[0]] + found[1]] - 1;
            if (constructor < 0)
            {
                functions = Register(bundle, coreLib, coreLayout, coreHeaps, found[0], found[1], 0, owners, rows, contexts,
                    bases, indices, arities, offsets, signatures, objects, functions);
                if (functions < 0) return functions;
                constructor = functions - 1;
            }
            MissingAssemblyStubs.Records(records, constructor);
            int messageLength = stubs.WriteLiteral(stub, true, literals, literalSize[0] + StringLiterals.HeaderSize);
            if (messageLength < 0) return messageLength;
            int status = StringLiterals.LowerPrepared(records, 0, literals, literalSize, globals, messageLength);
            if (status < 0) return status;
            int nameLength = stubs.WriteLiteral(stub, false, literals, literalSize[0] + StringLiterals.HeaderSize);
            if (nameLength < 0) return nameLength;
            status = StringLiterals.LowerPrepared(records, 2 * BranchIl.Width, literals, literalSize, globals, nameLength);
            return status < 0 ? status : functions;
        }


        // Stage 156 (decision 0083): records the import for a native method, binds CoreLib's
        // DllNotFoundException(string) and EntryPointNotFoundException(string) for a host that
        // lacks the library or the entry, writes their message literals and the thunk records.
        private static int LinkNativeThunk(BundleImages bundle, NativeImportTable natives, int nativeBase, int current,
            int owner, int source, int kind, byte[] module, byte[] name, byte[] types, int[] shape, int description,
            int[] records, int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities,
            int[] offsets, int[] signatures, int[] objects, int functions, byte[] literals, int[] literalSize)
        {
            int moduleLength = description & 0xffff, nameLength = description >> 16;
            if (shape[0] != arities[current]) return -1;
            int entry = natives.Add(owner, source, module, moduleLength, name, nameLength, types, shape[0], shape[1]);
            int present = natives.Helper(NativeImportTable.PresentHelper);
            int library = natives.Helper(NativeImportTable.LibraryHelper);
            int clear = -1, save = -1;
            if (shape[2] != 0)
            {
                clear = nativeBase + natives.Helper(NativeImportTable.ClearErrorHelper);
                save = nativeBase + natives.Helper(NativeImportTable.SaveErrorHelper);
            }
            var found = new int[2];
            if (InitializerFailures.FindCoreLib(bundle, found) < 0) return -1;
            if (found[0] < 0) return -2; // no CoreLib: a missing entry could not raise its exception
            byte[] coreLib = bundle.Get(found[0]);
            var coreLayout = new int[4];
            var coreHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(coreLib, coreLayout, coreHeaps) < 0) return -1;
            var missing = new int[6];
            var message = new byte[160 + moduleLength + nameLength];
            for (int which = 0; which < 2; which++)
            {
                string exceptionName = which == 0 ? "DllNotFoundException" : "EntryPointNotFoundException";
                int type = ObjectLayouts.FindTypeDefinition(coreLib, coreHeaps, coreLayout, "System", exceptionName);
                int row = type == 0 ? 0 : NativeThunks.FindMessageConstructor(coreLib, coreHeaps, coreLayout, type);
                if (row == 0) return -2;
                int constructor = indices[bases[found[0]] + row] - 1;
                if (constructor < 0)
                {
                    functions = Register(bundle, coreLib, coreLayout, coreHeaps, found[0], row, 0, owners, rows, contexts,
                        bases, indices, arities, offsets, signatures, objects, functions);
                    if (functions < 0) return functions;
                    constructor = functions - 1;
                }
                int messageBytes = which == 0
                    ? NativeThunks.LibraryMessage(module, moduleLength, kind, message)
                    : NativeThunks.Message(module, moduleLength, kind, name, nameLength, message);
                int messageStart = literalSize[0];
                int units = Utf8Scalars.CopyUtf16(message, 0, messageBytes, literals, messageStart);
                if (units < 0) return units;
                literalSize[0] = messageStart + units;
                missing[3 * which] = messageStart;
                missing[3 * which + 1] = units / 2;
                missing[3 * which + 2] = constructor;
            }
            NativeThunks.Records(records, current, shape[0], entry, nativeBase + present, nativeBase + library,
                nativeBase + entry, clear, save, missing, kind);
            return functions;
        }

        // Imported functions precede defined functions. Discovery can add
        // a host feature, so re-emit with its function indices reserved.
        internal const int Reemit = -1000;

        private static int CompileProgram(BundleImages bundle, byte[] entries, byte[] output, int[] location,
            long hostFeatures, MissingAssemblyStubs stubs, bool rootProbe = false)
        {
            // Finalization is an internal runtime feature and has no host import indices.
            var features = new long[] { hostFeatures, rootProbe ? 1 : 0, 0 };
            var natives = new NativeImportTable();
            var addressedFields = new AddressedFields();
            var typeCalls = new RuntimeTypeCalls();
            int status = Reemit;
            while (status == Reemit)
                status = CompileProgramPass(bundle, entries, output, location, features, addressedFields, typeCalls, stubs, rootProbe, natives);
            return status;
        }

        private static int CompileProgramPass(BundleImages bundle, byte[] entries, byte[] output, int[] location,
            long[] features, AddressedFields addressedFields, RuntimeTypeCalls typeCalls, MissingAssemblyStubs stubs, bool rootProbe, NativeImportTable natives)
        {
            long hostFeatures = features[0];
            bool tracksFinalizers = features[1] != 0;
            int knownAddressedFields = addressedFields.Count;
            // Native discovery participates in the same fixed point as the fixed host groups.
            int reservedNatives = natives.Count;
            int nativeBase = WasmImports.FixedFunctionCount(hostFeatures);
            int hostImports = WasmImports.FunctionCount(hostFeatures, natives);
            var nativeModule = new byte[512];
            var nativeName = new byte[512];
            var nativeTypes = new byte[64];
            var nativeShape = new int[3];
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
            var projections = new ValueFieldProjections((int)features[2], objects);
            if (projections.ReferenceVector < 0) return projections.ReferenceVector;
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
            // [count, thunk functions..., then GeneralDelegates thunk keys in the second half]
            var thunks = new int[2 * (functionCapacity + 1)];
            // Stage 204: per delegate key, the fixup chain of its thunk type index in invoke helpers.
            var delegateTypeFixups = new int[DelegateShapes.MaxDelegates];
            var signatures = new int[output.Length];
            var frame = new int[output.Length];
            var virtualTypes = new int[output.Length];
            var virtualUsed = new int[1];
            var moduleCode = new byte[output.Length];
            var literals = new byte[output.Length];
            var literalSize = new int[1];
            var remainderFixup = new int[1];
            var primitiveArrays = new PrimitiveArrayWasm();
            var reflection = new RuntimeReflectionPlan();
            var hostUse = new long[1];
            int functions = 0, length = 0, closedSignaturesThrough = 0;
            bool externalObjects = false, externalVectors = false;
            // Any reachable ldind/stind needs the host-lent memory 0 (decision 0060).
            bool linearMemory = false;
            var dispatch = new VirtualDispatch(output.Length, typeCalls);
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
                    if (IsArray(ExchangeLeaf(objects, type))) externalVectors = true;
                    if (externalVectors || CanExchangeClass(objects, type)) externalObjects = true;
                    if (type == NullPointer || IsManagedLocation(type) || IsValueLocation(type) || IsSpan(type) || RuntimeTypeMetadata.IsStorage(type) || ObjectLayouts.IsInterface(objects, type) || ObjectLayouts.IsValueType(objects, type))
                        return -2;
                }
                int result = signatures[offsets[index]];
                // A foreign Wasm caller can mutate an escaped class or array
                // that still has an alias inside the linked application.
                if (CanExchangeClass(objects, result)) externalObjects = true;
                if (result == NullPointer || IsManagedLocation(result) || IsValueLocation(result) || IsSpan(result) ||
                    RuntimeTypeMetadata.IsStorage(result) || ObjectLayouts.IsValueType(objects, result)) return -2;
                exports[export] = index;
            }
            var body = new int[6];
            var frameShape = new int[ExceptionDispatchWasm.FrameSlot + ExceptionDispatchWasm.FrameWidth];
            var shadowStack = new int[3]; // $sp, base, limit globals + 1
            var pinRegistry = new int[1]; // the live-pin count global + 1 (decision 0077)
            int handleSlots = -1;
            // Each emitted global needs at least five bytes in the destination.
            var globals = new int[1 + output.Length / 5 * StaticFields.Width];
            int rootCount = tracksFinalizers ? StaticFields.Register(globals, 0, RootStackWasm.GlobalKey, I32) : -1;
            int markedCount = tracksFinalizers ? StaticFields.Register(globals, 0, ReachabilityWasm.GlobalKey, I32) : -1;
            int registeredCount = tracksFinalizers ? StaticFields.Register(globals, 0, FinalizerQueueWasm.RegisteredKey, I32) : -1;
            int pendingCount = tracksFinalizers ? StaticFields.Register(globals, 0, FinalizerQueueWasm.PendingKey, I32) : -1;
            int suppressedCount = tracksFinalizers ? StaticFields.Register(globals, 0, FinalizerSuppressionWasm.GlobalKey, I32) : -1;
            if (tracksFinalizers && (rootCount < 0 || markedCount < 0 || registeredCount < 0 || pendingCount < 0 || suppressedCount < 0)) return -3;
            FinalizerSlots finalizers = null;
            if (tracksFinalizers || bundle.Count > 1)
            {
                finalizers = new FinalizerSlots();
                int rootStatus = finalizers.ReadRoot(bundle);
                if (rootStatus < 0)
                {
                    if (tracksFinalizers) return rootStatus;
                    finalizers = null;
                }
            }
            var initializers = new TypeInitializerPlan(functionCapacity, output.Length / 5 + 1);
            var nativePlans = new NativeAggregatePlans();
            var pointers = new FunctionPointerPlan();
            var receiverDemand = new ReceiverBodyDemand(functionCapacity);
            // Reuse local pin metadata across methods. Signature decoding resets each slot.
            var pinned = new int[frame.Length];
            var localElements = new byte[frame.Length];
            var initializerSlot = new int[1];
            int exceptionTags = tracksFinalizers ? ExceptionTypes.ArgumentNull : 0;
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
                        arities, offsets, signatures, functions, location, instantiations);
                    if (expanded < 0) return expanded;
                    dispatch.ObserveReceiverDemand(receiverDemand, contexts);
                    for (int i = 0; i < exports.Length; i++) receiverDemand.Require(exports[i], contexts);
                    for (int i = 0; i < thunks[0]; i++) receiverDemand.Require(thunks[i + 1], contexts);
                    for (int i = 0; i < pointers.TargetCount; i++) receiverDemand.Require(pointers.Targets[i], contexts);
                    if (expanded == functions)
                    {
                        // A later caller may retain the traced ABI of an earlier
                        // signature slot. Revisit it before finalizing the module.
                        current = receiverDemand.Revisit(functions, contexts, sizes);
                        if (current < 0) break;
                    }
                    else functions = expanded;
                }
                if (rows[current] == 0 || sizes[current] != 0 || !receiverDemand.Needed(current, contexts)) continue;
                int owner = owners[current], source = rows[current], context = contexts[current];
                context = NativeReceiverCalls.DeclaringContext(context);
                int[] methodArguments = instantiations.Arguments(current);
                // Issue #299: a deferred stub replaces this body with a FileNotFoundException throw.
                int stub = stubs.Find(owner, source);
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
                else if (stub < 0)
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
                            bases, indices, arities, offsets, signatures, objects, globals, functions, initializerSlot, context);
                        if (functions < 0) return functions;
                        initializerPrologue = initializerSlot[0];
                    }
                }
                int status = 0;
                if (stub < 0 && initializerPrologue < 0)
                {
                    int intrinsic = PlatformMembers.CoreMathInternalCall(image, heaps, layout, source);
                    if (intrinsic != 0)
                    {
                        if (moduleCode.Length - length < 96) return -3;
                        bodyOffsets[current] = length;
                        length = MathWasm.WriteBody(moduleCode, length, intrinsic);
                        sizes[current] = length - bodyOffsets[current];
                        if (MathWasm.CanOverflow(intrinsic)) exceptionTags |= ExceptionTypes.Overflow;
                        continue;
                    }
                    status = Int32ExchangeWasm.WriteBody(moduleCode, length, image, layout, heaps, source,
                        signatures, offsets[current], arities[current], projections);
                    if (status == length)
                        status = NativeExchangeWasm.WriteBody(moduleCode, length, image, layout, heaps, source,
                            signatures, offsets[current], arities[current], projections);
                    if (status == length)
                        status = ReferenceExchangeWasm.WriteBody(moduleCode, length, image, layout, heaps, source,
                            objects, signatures, offsets[current], arities[current], projections);
                    if (status < 0) return status;
                    if (status != length)
                    {
                        bodyOffsets[current] = length;
                        sizes[current] = status - length;
                        length = status;
                        exceptionTags |= ExceptionTypes.NullReference;
                        continue;
                    }
                }
                int handleOperation = stub < 0 && initializerPrologue < 0 ? GcHandleBinding.Identify(image, layout, heaps, source) : 0;
                if (handleOperation != 0)
                {
                    if (!GcHandleWasm.Matches(handleOperation, signatures, offsets[current], arities[current])) return -2;
                    if (handleSlots < 0)
                    {
                        handleSlots = StaticFields.Register(globals, 0, GcHandleWasm.GlobalKey, I32);
                        if (handleSlots < 0) return handleSlots;
                    }
                    bool allocatesHandle = handleOperation == GcHandleBinding.Allocate;
                    string name = allocatesHandle ? "ThrowNotSupportedException" : "ThrowInvalidOperationException_HandleIsNotInitialized";
                    int method = GcHandleBinding.FindFailure(image, layout, heaps, name);
                    if (method == 0) return -2;
                    int index = indices[bases[owner] + method] - 1;
                    if (index < 0)
                    {
                        functions = Register(bundle, image, layout, heaps, owner, method, 0, owners, rows, contexts,
                            bases, indices, arities, offsets, signatures, objects, functions);
                        if (functions < 0) return functions;
                        index = functions - 1;
                    }
                    bodyOffsets[current] = length;
                    length = GcHandleWasm.WriteBody(moduleCode, length, handleOperation, handleSlots, hostImports + index);
                    if (length < 0) return length;
                    sizes[current] = length - bodyOffsets[current];
                    continue;
                }
                if (stub < 0 && (MethodDefinitions.ReadColumn(image, layout, source, 2) & 0x2000) != 0)
                {
                    // MethodShape proved the named native leaf and both sides
                    // of the ABI. Keep the SDK's managed caller and error logic.
                    int implementation = MethodDefinitions.ReadColumn(image, layout, source, 1);
                    if (DebuggerNativeMetadata.IsAttachmentQuery(image, heaps, layout, source, implementation))
                    {
                        hostUse[0] = hostUse[0] | WasmImports.Debugger;
                        if (moduleCode.Length - length < RuntimeDebuggerWasm.BodyCapacity) return -3;
                        bodyOffsets[current] = length;
                        length = RuntimeDebuggerWasm.WriteBody(moduleCode, length, WasmImports.DebuggerSlot(hostFeatures), hostImports, initializerPrologue);
                        sizes[current] = length - bodyOffsets[current];
                        continue;
                    }
                    int import = Win32ModuleBinding.FindImport(image, heaps, layout, source, implementation, bundle, owner);
                    if (import != 0)
                    {
                        hostUse[0] = hostUse[0] | WasmImports.Win32NativeFeature(import);
                        if (Win32ModuleBinding.UsesLastError(import)) hostUse[0] = hostUse[0] | WasmImports.Win32Errors;
                        if (moduleCode.Length - length < Win32ModuleWasm.BodyCapacity(import)) return -3;
                        bodyOffsets[current] = length;
                        int shape = offsets[current];
                        length = Win32ModuleWasm.WriteBody(moduleCode, length, signatures, shape, import,
                            WasmImports.Win32NativeSlot(hostFeatures, import), hostImports, initializerPrologue,
                            WasmImports.Win32ErrorBase(hostFeatures), projections);
                        sizes[current] = length - bodyOffsets[current];
                        continue;
                    }
                }
                int scopedPath = stub < 0 ? ScopedPathBinding.FindWrapper(image, layout, heaps, source) : 0;
                if (scopedPath != 0)
                {
                    hostUse[0] = hostUse[0] | WasmImports.Win32NativeFeature(scopedPath) | WasmImports.Win32Errors;
                    int stackStatus = StackAllocationWasm.Register(globals, shadowStack);
                    if (stackStatus < 0) return stackStatus;
                    if (moduleCode.Length - length < ScopedPathWasm.Capacity) return -3;
                    bodyOffsets[current] = length;
                    length = ScopedPathWasm.WriteBody(moduleCode, length, WasmImports.Win32NativeSlot(hostFeatures, scopedPath),
                        WasmImports.Win32ErrorBase(hostFeatures), initializerPrologue, hostImports, shadowStack,
                        scopedPath == Win32ModuleBinding.GetFullPathName);
                    sizes[current] = length - bodyOffsets[current];
                    continue;
                }
                if (stub < 0 && DpiQueryBinding.IsWrapper(image, heaps, layout, source))
                {
                    int member = Win32ModuleBinding.GetProcessAwareness;
                    int shape = offsets[current];
                    if (!Win32ModuleBinding.MatchesSignature(bundle, objects, member, signatures, shape, arities[current]))
                        return -2;
                    hostUse[0] = hostUse[0] | WasmImports.Win32NativeFeature(member);
                    exceptionTags |= ExceptionTypes.NullReference;
                    if (moduleCode.Length - length < Win32ModuleWasm.DpiQueryBodyCapacity) return -3;
                    bodyOffsets[current] = length;
                    length = Win32ModuleWasm.WriteBody(moduleCode, length, signatures, shape, member,
                        WasmImports.Win32NativeSlot(hostFeatures, member), hostImports, initializerPrologue, 0, projections);
                    sizes[current] = length - bodyOffsets[current];
                    continue;
                }
                if (stub < 0 && RuntimeTerminationMetadata.IsMonoFailFast(image, heaps, layout, source))
                {
                    if (moduleCode.Length - length < RuntimeTerminationWasm.BodyCapacity) return -3;
                    int write = nativeBase + RuntimeTerminationWasm.RegisterWrite(natives);
                    int terminate = nativeBase + RuntimeTerminationWasm.RegisterTerminate(natives);
                    int initialize = initializerPrologue < 0 ? -1 : hostImports + initializerPrologue;
                    bodyOffsets[current] = length;
                    length = RuntimeTerminationWasm.WriteBody(moduleCode, length, write, terminate, initialize);
                    sizes[current] = length - bodyOffsets[current];
                    continue;
                }
                uint rva = 0;
                int nativeKind = stub >= 0 ? NativeThunks.None : NativeThunks.Kind(image, heaps, layout, source);
                int nativeDescription = 0;
                if (stub >= 0)
                {
                    // The CLR fails the JIT before the method runs, so a stub triggers no type
                    // initializer. Its body is four synthetic instructions with a stack of two.
                    initializerPrologue = -1;
                    body[0] = 0; body[1] = 6; body[2] = 2; body[3] = 0; body[4] = 0; body[5] = 0;
                }
                else if (nativeKind != NativeThunks.None)
                {
                    // Stage 156 (decision 0083): a host import behind a synthetic thunk. The type
                    // initializer prologue stays: the CLR runs it before a precise type's P/Invoke.
                    nativeDescription = NativeThunks.Describe(image, heaps, layout, source, nativeKind,
                        nativeModule, nativeName, nativeTypes, nativeShape);
                    if (nativeDescription < 0) return nativeDescription;
                    if (nativeKind == NativeThunks.SymbolLookup) hostUse[0] = hostUse[0] | WasmImports.Memory;
                    body[0] = 0;
                    body[1] = NativeThunks.RecordCount(nativeShape[0], nativeShape[2] != 0, nativeKind);
                    body[2] = nativeShape[0] > 1 ? nativeShape[0] : 1;
                    body[3] = 0; body[4] = 0; body[5] = 0;
                }
                else
                {
                    rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, source, 0));
                    status = MethodBodies.Read(image, rva, body);
                    if (status != 0)
                        return status == MethodBodies.Malformed ? -1 : -2;
                }
                int[] catchRegion = noCatch;
                int[] filterEnds = null, filters = noCatch;
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
                        if (catchRegion[clause] == 1)
                        {
                            if (filterEnds == null)
                            {
                                filterEnds = new int[body[1]];
                                filters = new int[regions * ExceptionFilters.Width];
                            }
                            int start = catchRegion[clause + 5], end = catchRegion[clause + 3];
                            int filtered = ExceptionFilters.Bind(bundle, image, layout, heaps, owner,
                                body[0] + start, body[0] + end, objects, filters, clause / ExceptionRegions.Width * ExceptionFilters.Width);
                            if (filtered < 0) return filtered;
                            if (start == 0) return -1;
                            for (int offset = 0; offset < end; offset++)
                                if (filterEnds[offset] > start) return -1;
                            filterEnds[start] = end;
                            catchRegion[clause] = 0;
                            catchRegion[clause + 5] = filtered;
                            continue;
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

                typeCalls.ApplyDescriptorSignatures(bases, indices, offsets, signatures);
                typeCalls.ApplyClosedSignatures(bundle, owners, rows, contexts, instantiations, offsets, signatures, objects, closedSignaturesThrough, functions);
                closedSignaturesThrough = functions;
                int parameters = arities[current];
                int signature = offsets[current];
                int result = signatures[signature];
                bool resourceOpen = stub < 0 && reflection.ResourceOwner == owner && reflection.ResourceOpen == source;
                // The Wasm result of a value constructor is its receiver's final
                // value. Its IL returns void, so stack analysis checks void.
                bool valueConstructor = IsValueConstructor(image, layout, heaps, source, signatures, signature, parameters, objects);
                if (valueConstructor) result = Void;
                bool locationReceiver = parameters > 0 && IsValueLocation(signatures[signature + 1]) &&
                    (MethodDefinitions.ReadColumn(image, layout, source, 2) & 0x10) == 0;
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
                    locals = SignatureTypes.Read(bundle, image, heaps, localBlob, frame, parameters, -1, layout, objects, owner, context, methodArguments, pinned, filters, localElements);
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
                int used = stub < 0 && nativeKind == NativeThunks.None && scalar && locals == 0 && catchRegion.Length == 0 && initializerPrologue < 0
                    ? StraightLineIl.Translate(image, body, parameters, code) : -2;
                if (used < 0)
                {
                    var records = new int[body[1] * BranchIl.Width];
                    if (stub >= 0)
                    {
                        functions = LinkStub(bundle, stubs, stub, records, owners, rows, contexts, bases, indices,
                            arities, offsets, signatures, objects, functions, literals, literalSize, globals);
                        if (functions < 0)
                        {
                            // No bindable CoreLib FileNotFoundException: keep the located diagnostic.
                            location[0] = owner; location[1] = source; location[2] = 0;
                            return functions;
                        }
                    }
                    else if (nativeKind != NativeThunks.None)
                    {
                        functions = LinkNativeThunk(bundle, natives, nativeBase, current, owner, source, nativeKind,
                            nativeModule, nativeName, nativeTypes, nativeShape, nativeDescription, records, owners, rows,
                            contexts, bases, indices, arities, offsets, signatures, objects, functions, literals, literalSize);
                        if (functions < 0) return functions;
                    }
                    else if (resourceOpen) status = ResourceOpenIl.Decode(image, body, records, location, parameters);
                    else if (parameters == 3 && locals == 1 && frame[parameters] == Native &&
                        CoreStringRange.Lower(image, layout, heaps, source, body, records)) status = 0;
                    else if (parameters == 2 && locals == 0 && frame[0] == String && frame[1] == String &&
                        CoreStringEquality.Lower(image, layout, heaps, source, body, records)) status = 0;
                    else if (parameters == 2 && locals == 0 && frame[0] == String &&
                        CoreStringSearch.Lower(image, layout, heaps, source, body, records)) status = 0;
                    else status = BranchIl.Decode(image, body[0], body[1], parameters, locals, records,
                        location, catchRegion.Length != 0, filterEnds: filterEnds);
                    if (status < 0)
                        return status;
                    status = GcHandleBinding.RestrictPinned(image, layout, heaps, source, body, records, catchRegion);
                    if (status < 0) return status;
                    if (parameters == 2 && frame[0] == String && frame[1] == WritableCharacterSpan)
                        CoreStringSpanCopy.Lower(bundle, image, layout, heaps, owner, source, records, objects, catchRegion);
                    CoreStringCreation.Lower(bundle, image, layout, heaps, owner, source, methodArguments,
                        frame, records, objects, catchRegion);
                    RuntimeTypeBranches.FoldClosedTypeBranches(bundle, image, layout, heaps, owner, context,
                        methodArguments, objects, records, catchRegion, count);
                    RuntimeTypeBranches.FoldClosedSizeBranches(bundle, image, layout, heaps, owner,
                        context, methodArguments, objects, records, catchRegion);
                    EnumFlagTests.Lower(bundle, image, layout, heaps, owner, objects, records, catchRegion);
                    ExactManagedReads.FoldExactManagedReads(bundle, image, layout, heaps, owner, context,
                        methodArguments, objects, frame, records, catchRegion);
                    status = CurrentThreadBinding.Lower(image, heaps, layout, owner, source,
                        records, catchRegion, frame, parameters, locals, body[2], objects, globals);
                    if (status < 0) return status;
                    byte[] frameRoots = null;
                    if (tracksFinalizers)
                    {
                        if (location.Length != 0) location[2] = -1;
                        frameRoots = RootStackWasm.MapFrame(records, frame, parameters + locals, catchRegion.Length != 0, parameters, filters);
                        if (frameRoots == null) return -2;
                    }
                    if (filterEnds != null)
                    {
                        status = ExceptionFilters.ValidateLocals(records, filters, parameters, locals, location);
                        if (status < 0) return status;
                    }
                    status = ArrayInitializers.Lower(bundle, image, heaps, layout, owner, context, methodArguments,
                        objects, records, catchRegion, literals, literalSize, location);
                    if (status < 0)
                        return status;
                    status = StaticByteSpans.Lower(bundle, image, layout, heaps, owner, context, methodArguments,
                        records, objects, catchRegion, literals, literalSize, location);
                    if (status < 0) return status;
                    status = StaticPrimitiveSpans.Lower(image, layout, heaps, records, catchRegion, literals, literalSize, location);
                    if (status < 0) return status;
                    if (locals != 0 && !resourceOpen)
                    {
                        PinnedByteSpans.Lower(bundle, image, heaps, layout, owner, context, methodArguments,
                            objects, records, pinned, frame, parameters, locals, catchRegion, literals);
                        status = PinnedStrings.Lower(image, heaps, layout, records, pinned, parameters, locals, location);
                        if (status < 0) return status;
                    }
                    virtualUsed[0] = 0;
                    var inlineLocations = new int[parameters + locals];
                    var frameCells = new int[parameters + locals];
                    int[] scopedCalls = null;
                    int scopedReceiver = 0;
                    if (NativeReceiverCalls.IsNativeKey(contexts[current]))
                    {
                        int type = context;
                        if (type == 0)
                        {
                            int definition = ObjectLayouts.Owner(image, heaps, layout, source, false);
                            type = ObjectLayouts.Find(objects, owner, definition, new int[0]);
                        }
                        if (ObjectLayouts.IsByRefLikeValue(objects, type))
                        {
                            scopedReceiver = type;
                            ScopedNativeReceivers.LowerThisBorrow(bundle, image, layout, heaps, owner, type, context,
                                methodArguments, objects, globals, records, catchRegion);
                        }
                    }
                    if (locals != 0)
                    {
                        NativeAggregateFieldPointers.Lower(bundle, image, layout, heaps, owner, source, context,
                            methodArguments, objects, globals, records, catchRegion, frame, parameters);
                        scopedCalls = ScopedNativeReceivers.FindCalls(bundle, image, layout, heaps, owner, source,
                            context, methodArguments, objects, globals, records, catchRegion, frame, parameters);
                        NativeAggregateConstructors.Lower(bundle, image, layout, heaps, owner, source, context,
                            methodArguments, objects, globals, records, catchRegion, frame, scopedCalls);
                    }
                    status = NativeFrameWasm.BindAddresses(bundle, objects, nativePlans, records, catchRegion,
                        frame, parameters, localElements, frameCells, location, scopedCalls);
                    if (status < 0) return status;
                    if (frameRoots != null) NativeFrameWasm.ExcludeNativeRoots(frameRoots, frameCells);
                    if (stub < 0 && nativeKind == NativeThunks.None)
                    {
                        functions = LinkOperands(bundle, image, layout, heaps, count, owner, source, context,
                            parameters == 0 ? 0 : signatures[signature + 1], valueConstructor || valueMethod || locationReceiver, valueMethod, valueConstructor, records, owners, rows, contexts,
                            bases, indices, arities, offsets, signatures, objects, thunks, globals, literals, literalSize, functions, location, frame, catchRegion, hostUse, virtualTypes, virtualUsed,
                            initializers, reflection, typeCalls, instantiations, methodArguments, inlineLocations, frameCells, parameters, signatures[signature],
                            nativePlans, addressedFields, localElements, pointers, finalizers, tracksFinalizers, scopedReceiver);
                        if (functions == Reemit)
                        {
                            // Allocation can precede the first reachable suppression call.
                            // Re-emit every body so its registration and roots agree.
                            features[0] = hostFeatures | hostUse[0];
                            features[1] = 1;
                            return Reemit;
                        }
                        if (functions < 0)
                            return functions;
                        functions = LinkCallIndirect(bundle, image, heaps, layout, count, owner, context, records, owners, rows,
                            contexts, arities, offsets, signatures, objects, functions, pointers, location, natives, nativeBase);
                        if (functions < 0)
                            return functions;
                    }

                    if (ScopedCharacterPins.Lower(records, frame, pinned, parameters, locals, catchRegion, frameCells, natives, nativeBase) &&
                        frameRoots != null)
                    {
                        frameRoots = RootStackWasm.MapFrame(records, frame, parameters + locals, catchRegion.Length != 0, parameters, filters);
                        if (frameRoots == null) return -2;
                        NativeFrameWasm.ExcludeNativeRoots(frameRoots, frameCells);
                    }
                    for (int local = 0; local < frameCells.Length; local++)
                        if (frameCells[local] != 0 && inlineLocations[local] != 0) return -2;
                    typeCalls.ApplyDescriptorSignatures(bases, indices, offsets, signatures);
                    typeCalls.ApplyClosedSignatures(bundle, owners, rows, contexts, instantiations, offsets, signatures, objects, closedSignaturesThrough, functions);
                    closedSignaturesThrough = functions;
                    RuntimeTypeLocals.Specialize(bundle, records, frame, parameters, locals, objects, catchRegion, signatures, offsets, virtualTypes);
                    status = RuntimeTypeArrays.Specialize(bundle, records, frame, parameters, locals, objects, catchRegion);
                    if (status < 0) return status;
                    if (location.Length != 0) location[2] = -1;
                    int maximum = body[2];
                    if (maximum > 0 && body[1] > int.MaxValue / maximum)
                        return -2;
                    var depths = new int[body[1]];
                    var states = new int[body[1] * maximum];
                    bool hasNull = false, hasBranch = false, hasAggregateBox = false, hasReferenceBox = false;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        int kind = records[slot];
                        if (kind == BranchIl.NullLiteral || kind == BranchIl.ConvertInteger &&
                            records[slot + 3] == BranchIl.ProvenNativeZero) hasNull = true;
                        if (kind == BranchIl.Branch || kind == BranchIl.Conditional || kind == BranchIl.Switch)
                            hasBranch = true;
                        if (kind == BranchIl.BoxAggregate) hasAggregateBox = true;
                        if (kind == BranchIl.WidenReference && records[slot + 1] != ObjectReference) hasReferenceBox = true;
                    }
                    var outgoingNulls = new byte[hasNull ? states.Length : 0];
                    // Without a control-flow edge into a shared successor, the
                    // producer's spill type already matches the next consumer.
                    var outgoingReferences = new int[hasBranch ? states.Length : 0];
                    var allocatedStates = new byte[virtualUsed[0] != 0 ? states.Length : 0];
                    int exceptionLocals = 0;
                    var fixedObjects = new int[ExceptionDispatchWasm.FrameWidth];
                    for (int i = 0; i < fixedObjects.Length; i += 2) fixedObjects[i] = -1;
                    if (catchRegion.Length != 0)
                    {
                        exceptionLocals = ExceptionClauses.Validate(records, catchRegion, location);
                        if (exceptionLocals < 0) return exceptionLocals;
                        functions = BindCaughtExceptions(bundle, catchRegion, location, owners, rows, contexts, bases, indices,
                            arities, offsets, signatures, objects, functions, fixedObjects);
                        if (functions < 0) return functions;
                    }
                    if (hasAggregateBox)
                    {
                        status = AggregateBoxAssignments.Bind(bundle, objects, records, frame, result, catchRegion, location);
                        if (status < 0) return status;
                    }
                    if (hasReferenceBox)
                        ReferenceBoxCalls.Bind(objects, records, catchRegion, arities, offsets, signatures, virtualTypes);
                    var descriptorMismatch = new int[6];
                    status = StackDepths.Analyze(records, depths, states, maximum, frame,
                        result, signatures, offsets, arities, location, catchRegion, outgoingNulls, outgoingReferences, objects, virtualTypes, allocatedStates,
                        descriptorMismatch, contexts);
                    if (status < 0)
                    {
                        int fieldSlot = descriptorMismatch[4] - 1;
                        if (fieldSlot >= 0 && bundle.TypeFields.RequireInstance(bundle, image, heaps, layout,
                            owner, source, objects, records[fieldSlot + 3], records[fieldSlot + 1], false))
                        {
                            features[0] = hostFeatures | hostUse[0];
                            return Reemit;
                        }
                        int fieldRead = descriptorMismatch[5] - 1;
                        if (fieldRead >= 0 && bundle.TypeFields.RequireInstance(bundle, image, heaps, layout,
                            owner, source, objects, records[fieldRead + 3], records[fieldRead + 1], true))
                        {
                            features[0] = hostFeatures | hostUse[0];
                            return Reemit;
                        }
                        int virtualSlot = descriptorMismatch[3] - 1;
                        if (virtualSlot >= 0)
                        {
                            int argument = descriptorMismatch[1];
                            int expected = virtualTypes[records[virtualSlot + 1] + 1 + argument];
                            if (typeCalls.RequireVirtualArgument(bundle, records[virtualSlot + BranchIl.Width + 1],
                                records[virtualSlot + BranchIl.Width + 3], argument, expected, objects))
                            {
                                features[0] = hostFeatures | hostUse[0];
                                return Reemit;
                            }
                        }
                        int resultSource = descriptorMismatch[2] - 1;
                        if (resultSource >= 0 && !IsExported(exports, resultSource) &&
                            typeCalls.RequireResult(bundle, owners[resultSource], rows[resultSource], signatures[offsets[resultSource]],
                                objects, contexts[resultSource] != 0 || instantiations.Arguments(resultSource) != null))
                        {
                            features[0] = hostFeatures | hostUse[0];
                            return Reemit;
                        }
                        int target = descriptorMismatch[0] - 1;
                        if (target >= 0 && contexts[target] == 0 && instantiations.Arguments(target) == null &&
                            !IsExported(exports, target))
                        {
                            int argument = descriptorMismatch[1];
                            if (typeCalls.Require(bundle, owners[target], rows[target], argument,
                                signatures[offsets[target] + 1 + argument], objects))
                            {
                                features[0] = hostFeatures | hostUse[0];
                                return Reemit;
                            }
                        }
                        return resourceOpen && status == -1 ? -2 : status;
                    }
                    if (reflection.AssemblyMetadata.Vector != 0)
                    {
                        dispatch.AddAllocation(reflection.AssemblyMetadata.Vector);
                        exceptionTags |= ExceptionTypes.ArgumentNull;
                    }
                    ValueObjectOperands.LowerManaged(records, depths, states, maximum);
                    status = projections.BindReachable(records, depths, states, maximum, objects, location);
                    if (status < 0) return status;
                    if (resourceOpen)
                    {
                        hostUse[0] = hostUse[0] | WasmImports.Resources;
                        exceptionTags |= ExceptionTypes.Argument | ExceptionTypes.ArgumentNull;
                    }
                    functions = NativeReceiverCalls.Bind(bundle, records, depths, states, maximum, owners, rows,
                        contexts, bases, indices, arities, offsets, signatures, objects, functions, location, instantiations, scopedCalls);
                    if (functions < 0) return functions;
                    if (virtualUsed[0] != 0)
                    {
                        functions = LinkVirtualCalls(bundle, records, depths, states, allocatedStates, maximum, virtualTypes,
                            owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions, location,
                            dispatch, externalObjects, owner, source, instantiations, reflection);
                        if (functions < 0) return functions;
                    }

                    receiverDemand.ObserveCalls(records, depths, contexts);
                    if (location.Length != 0) location[2] = -1;
                    int capacity = body[1] * 64 + 32;
                    if (tracksFinalizers)
                    {
                        var finalizerTarget = new int[2];
                        for (int pc = 0; pc < depths.Length; pc++)
                        {
                            int slot = pc * BranchIl.Width;
                            if (depths[pc] == 0 || records[slot] != BranchIl.NewObject) continue;
                            int constructor = records[slot + 1];
                            int type = signatures[offsets[constructor] + 1];
                            if (type < FirstClass || IsValueType(objects, type) || finalizers.Callback(type) >= 0) continue;
                            status = finalizers.Includes(bundle, objects, type, finalizerTarget);
                            if (status == 0) continue;
                            if (location.Length != 0) location[2] = pc;
                            if (status < 0) return status;
                            int finalizerOwner = finalizerTarget[0], finalizerRow = finalizerTarget[1];
                            byte[] finalizerImage = bundle.Get(finalizerOwner);
                            var finalizerLayout = new int[4];
                            var finalizerHeaps = new int[8];
                            if (CompressedTables.FindWithHeaps(finalizerImage, finalizerLayout, finalizerHeaps) < 0) return -1;
                            int declaringRow = ObjectLayouts.Owner(finalizerImage, finalizerHeaps, finalizerLayout, finalizerRow, false);
                            int declaring = type;
                            while (declaring >= FirstClass && (AssemblyOf(objects, declaring) != finalizerOwner ||
                                TypeDef(objects, declaring) != declaringRow)) declaring = Parent(objects, declaring);
                            if (declaring < FirstClass) return -2;
                            int finalizerContext = HasArguments(objects, declaring) ? declaring : 0;
                            int callback = Lookup(owners, rows, contexts, bases, indices, functions, finalizerOwner, finalizerRow, finalizerContext);
                            if (callback < 0)
                            {
                                functions = Register(bundle, finalizerImage, finalizerLayout, finalizerHeaps, finalizerOwner,
                                    finalizerRow, finalizerContext, owners, rows, contexts, bases, indices, arities, offsets,
                                    signatures, objects, functions, allowVirtual: true);
                                if (functions < 0) return functions;
                                callback = functions - 1;
                            }
                            finalizers.BindCallback(type, callback);
                            if (location.Length != 0) location[2] = -1;
                        }
                        int handlers = catchRegion.Length / ExceptionClauses.Width;
                        long extra = (long)body[1] * (96L + 20L * (parameters + locals + maximum + handlers));
                        if (initializerPrologue >= 0) extra += 112L + 20L * parameters;
                        if (initializerGuard >= 0) extra += 48;
                        if (extra > int.MaxValue - capacity) return -3;
                        capacity += (int)extra;
                    }
                    for (int local = parameters; local < inlineLocations.Length; local++)
                    {
                        if (inlineLocations[local] == 0) continue;
                        if (capacity > int.MaxValue - 24) return -3;
                        capacity += 24;
                    }
                    bool allocates = false, pins = false, readsLine = false;
                    for (int slot = 0; slot < frameCells.Length; slot++)
                    {
                        if (frameCells[slot] == 0) continue;
                        int cellBytes = NativeFrameWasm.IsCell(frameCells[slot]) ? NativeFrameWasm.Capacity : 32;
                        if (capacity > int.MaxValue - cellBytes) return -3;
                        capacity += cellBytes;
                        if (!NativeFrameWasm.IsCell(frameCells[slot])) continue;
                        status = StackAllocationWasm.Register(globals, shadowStack);
                        if (status < 0) return status;
                        allocates = true;
                        linearMemory = true;
                    }
                    if (catchRegion.Length != 0)
                    {
                        // One dispatch per fixed tag plus the object dispatch, whose clause arms add a
                        // nominal ref.test and clear the other kind's saved reference (decision 0054).
                        int handlerBytes = 32 + (ExceptionTagsWasm.Count + 1) * 20 +
                            catchRegion.Length / ExceptionClauses.Width * (48 * (ExceptionTagsWasm.Count + 1));
                        if (tracksFinalizers) handlerBytes += (ExceptionTagsWasm.Count + 1) * 32;
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
                    // Issue #170: an initializer body wraps an escaping managed exception in CoreLib's
                    // TypeInitializationException when the bundle supplies it.
                    int[] failure = null;
                    bool wrapsFailure = false;
                    if (initializerGuard >= 0)
                    {
                        failure = new int[TypeInitializerWasm.FailureWidth];
                        functions = BindInitializerFailure(bundle, image, heaps, layout, owner, source, context, owners, rows, contexts,
                            bases, indices, arities, offsets, signatures, objects, functions, globals, literals, literalSize, failure);
                        if (functions < 0) return functions;
                        wrapsFailure = failure[TypeInitializerWasm.FailureCache] != 0;
                        if (wrapsFailure)
                        {
                            failure[TypeInitializerWasm.FailureCache] = failure[TypeInitializerWasm.FailureCache] - 1; // stored as index + 1
                            failure[TypeInitializerWasm.FailureConstructor] = failure[TypeInitializerWasm.FailureConstructor] + hostImports;
                            for (int i = 0; i < TypeInitializerWasm.FixedFailureCount; i++)
                                if (failure[TypeInitializerWasm.FailureFixed + 2 * i] >= 0)
                                    failure[TypeInitializerWasm.FailureFixed + 2 * i] = failure[TypeInitializerWasm.FailureFixed + 2 * i] + hostImports;
                            // Every tag the guard catches must be declared by the module.
                            exceptionTags |= ExceptionTypes.Object | ExceptionTypes.Supported;
                            if (capacity > int.MaxValue - 192) return -3;
                            capacity += 192;
                        }
                    }
                    if (resourceOpen)
                    {
                        if (capacity > int.MaxValue - 320) return -3;
                        capacity += 320;
                    }
                    frameShape[21] = 0;
                    frameShape[22] = addressedFields.Pages();
                    int classBytes = objects[0] * 12;
                    if (classBytes > int.MaxValue - capacity) return -3;
                    capacity += classBytes;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        int kind = records[slot];
                        if (kind == BranchIl.Monitor && depths[slot / BranchIl.Width] != 0)
                        {
                            if (capacity > int.MaxValue - MonitorWasm.Capacity) return -3;
                            capacity += MonitorWasm.Capacity;
                            exceptionTags |= ExceptionTypes.Argument | ExceptionTypes.ArgumentNull | ExceptionTypes.SynchronizationLock;
                        }
                        if (kind == BranchIl.ConsoleReadLine && depths[slot / BranchIl.Width] != 0)
                        {
                            readsLine = true;
                            if (capacity > int.MaxValue - ConsoleInputWasm.ReadCapacity) return -3;
                            capacity += ConsoleInputWasm.ReadCapacity;
                        }
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
                        if ((kind == BranchIl.SetCurrentThreadHandle || kind == BranchIl.LoadMonoCurrentThread) &&
                            depths[slot / BranchIl.Width] != 0)
                        {
                            if (capacity > int.MaxValue - CurrentThreadWasm.Capacity) return -3;
                            capacity += CurrentThreadWasm.Capacity;
                        }
                        int pc = slot / BranchIl.Width;
                        int operation = records[slot + 1];
                        if ((kind == BranchIl.CallNativeIndirect || kind == BranchIl.CallDynamicNative) && depths[pc] != 0)
                            linearMemory = true;
                        if ((kind == BranchIl.Load || kind == BranchIl.Store) && frameCells[operation] < 0 && depths[pc] != 0)
                        {
                            // Native layout nesting is bounded to 16 levels. Each stored leaf
                            // needs at most one load or store sequence per level.
                            long bytes = 128L + 768L * NativeAggregates.Size(nativePlans.Plans, -frameCells[operation]);
                            if (bytes > int.MaxValue - capacity) return -3;
                            capacity += (int)bytes;
                        }
                        if ((kind == BranchIl.LoadIndirect || kind == BranchIl.StoreIndirect) && depths[pc] != 0)
                        {
                            int address = pc * maximum + depths[pc] - (kind == BranchIl.LoadIndirect ? 2 : 3);
                            if (IsNativeInteger(states[address])) linearMemory = true;
                        }
                        if ((kind == BranchIl.LoadObject || kind == BranchIl.StoreObject || kind == BranchIl.InitializeNative) && depths[pc] != 0)
                        {
                            linearMemory = true;
                            int fields = ObjectLayouts.FieldCount(objects, records[slot + 1]);
                            // A load or store per field, nested structs included (bounded by size).
                            int bytes = 64 + 24 * (fields + NativeAggregates.Size(nativePlans.Plans, records[slot + 3] - 1));
                            if (capacity > int.MaxValue - bytes) return -3;
                            capacity += bytes;
                        }
                        // Stage 143 (decision 0071): p->F loads and stores also address memory 0.
                        bool fieldStore = kind == BranchIl.SetField || kind == BranchIl.UpdateConstructorField;
                        bool nativeField = (kind == BranchIl.GetField || fieldStore) && depths[pc] != 0 &&
                            records.Length > slot + 2 * BranchIl.Width + 3 && records[slot + 2 * BranchIl.Width + 2] != 0 &&
                            IsNativeInteger(states[pc * maximum + depths[pc] - 1 - (fieldStore ? 2 : 1)]);
                        if (nativeField) linearMemory = true;
                        if ((kind == BranchIl.LocalAllocate || kind == BranchIl.PinString || kind == BranchIl.PinStaticBytes || kind == BranchIl.CallPinnedCharacterNative) && depths[pc] != 0)
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
                            status = StackAllocationWasm.Register(globals, shadowStack);
                            if (status < 0) return status;
                            if (kind == BranchIl.PinString)
                            {
                                pins = true;
                                if (pinRegistry[0] == 0)
                                {
                                    int global = StaticFields.Register(globals, 0, PinRegistryWasm.GlobalKey, I32);
                                    if (global < 0) return global;
                                    pinRegistry[0] = global + 1;
                                }
                            }
                            int allocationBytes = kind == BranchIl.PinString ? Utf16MemoryWasm.PinCapacity :
                                kind == BranchIl.CallPinnedCharacterNative ? ScopedCharacterPinWasm.Capacity : StackAllocationWasm.Capacity;
                            if (capacity > int.MaxValue - allocationBytes) return -3;
                            capacity += allocationBytes;
                        }
                        if (depths[pc] != 0 && (kind == BranchIl.LoadNativeStatic || kind == BranchIl.StoreNativeStatic || kind == BranchIl.StaticFieldAddress))
                        {
                            linearMemory = true;
                            int region = StaticFields.Register(globals, 0, int.MinValue + 16, I32);
                            if (region < 0) return region;
                            frameShape[21] = region + 1;
                            int plan = records[slot + BranchIl.Width + 1];
                            int bytes = 160 + 24 * (ObjectLayouts.FieldCount(objects, records[slot + 3]) + NativeAggregates.Size(nativePlans.Plans, plan));
                            if (capacity > int.MaxValue - bytes) return -3;
                            capacity += bytes;
                        }
                        if (kind == BranchIl.LoadStaticField || kind == BranchIl.StoreStaticField ||
                            kind == BranchIl.StaticInt32 || kind == BranchIl.StaticReference || kind == BranchIl.StaticValue)
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
                            if (depths[pc] > 1)
                            {
                                int top = states[pc * maximum + depths[pc] - 2];
                                dispatch.ObserveReceiver(top);
                                // The public Wasm ABI exposes storage supertypes.
                                // A foreign array can lack the managed allocation's type ID.
                                if (externalVectors && kind == BranchIl.GetObjectType && (top == ObjectReference || IsArray(top)))
                                {
                                    CompileLocation.Set(location, owner, source, pc);
                                    return -2;
                                }
                            }
                            if (kind == BranchIl.NewArray || kind == BranchIl.InitializedArray)
                            {
                                int element = records[slot + BranchIl.Width + 1];
                                if (element != 0)
                                {
                                    reflection.ObjectTypes.IncludePrimitiveArray(element);
                                    dispatch.AddPrimitiveArray(element);
                                }
                                else if (ObjectLayouts.ArrayElementKey(objects, operation) != 0)
                                    dispatch.AddAllocation(operation);
                            }
                            if (kind == BranchIl.BoxValue) dispatch.AddBox(operation);
                            if (kind == BranchIl.BoxAggregate || kind == BranchIl.BoxAggregateToInterface)
                                reflection.AggregateBoxes.Include(kind == BranchIl.BoxAggregate ? operation : FieldAccess.Owner(operation), records[slot + 3]);
                            if (hasNull || hasBranch)
                            {
                                capacity = StackSpillWasm.Transfer(sizingBuffer, capacity, records, pc,
                                    depths, states, outgoingNulls, outgoingReferences, maximum, 0);
                                if (capacity == int.MaxValue) return -3;
                            }
                            if (hasFinally && kind == BranchIl.Leave)
                            {
                                int unwindBytes = catchRegion.Length / ExceptionClauses.Width * 32;
                                if (capacity > int.MaxValue - unwindBytes) return -3;
                                capacity += unwindBytes;
                            }
                            if (kind == BranchIl.UpdateValue || kind == BranchIl.UpdateConstructorField || kind == BranchIl.ClearInteriorField ||
                                kind == BranchIl.SetField && IsValueLocation(states[pc * maximum + depths[pc] - 3]))
                            {
                                int fields = ObjectLayouts.FieldCount(objects, FieldAccess.Owner(records[slot + 3]));
                                if (fields > (int.MaxValue - capacity) / 16) return -3;
                                capacity += fields * 16;
                            }
                            if (kind == BranchIl.ReadReflection && !ReflectionMembers.IsTypeText(operation))
                            {
                                int bytes = reflection.BodyCapacity(int.MaxValue - capacity);
                                if (bytes < 0) return bytes;
                                capacity += bytes;
                            }
                            if (kind == BranchIl.FormatEnum)
                            {
                                int names = unchecked((int)PeLibrary.ImageBytes.U32(literals, (uint)operation));
                                if (capacity > int.MaxValue - EnumFormattingWasm.Capacity ||
                                    names > (int.MaxValue - capacity - EnumFormattingWasm.Capacity) / EnumFormattingWasm.NameCapacity) return -3;
                                capacity += EnumFormattingWasm.Capacity + names * EnumFormattingWasm.NameCapacity;
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
                            else if (kind == BranchIl.UnboxValue || kind == BranchIl.UnboxAggregate || kind == BranchIl.UnboxAggregateAddress)
                                tags = ExceptionTypes.InvalidCast | ExceptionTypes.NullReference;
                            else if (kind == BranchIl.CastClass || kind == BranchIl.CastHandler)
                                tags = ExceptionTypes.InvalidCast;
                            else if (kind == BranchIl.CombineHandler || kind == BranchIl.RemoveHandler)
                                tags = ExceptionTypes.Argument; // decision 0092: mismatched nominal types
                            else if (kind == BranchIl.NativeCharacterSpan || kind == BranchIl.SpanSlice ||
                                kind == BranchIl.WritableByteArraySpan ||
                                kind == BranchIl.StringAsSpan && operation != 0)
                                tags = ExceptionTypes.ArgumentOutOfRange;
                            else if (kind == BranchIl.CopyCharacterSpan)
                            {
                                tags = ExceptionTypes.Argument;
                                if ((hostFeatures & WasmImports.NativeCharacterViews) != 0) tags |= ExceptionTypes.NullReference;
                                if (capacity > int.MaxValue - CharacterSpanCopyWasm.Capacity) return -3;
                                capacity += CharacterSpanCopyWasm.Capacity;
                            }
                            else if (kind == BranchIl.FindCharacterInSpan)
                            {
                                if ((hostFeatures & WasmImports.NativeCharacterViews) != 0) tags = ExceptionTypes.NullReference;
                                if (capacity > int.MaxValue - CharacterSpanSearchWasm.Capacity) return -3;
                                capacity += CharacterSpanSearchWasm.Capacity;
                            }
                            else if (kind == BranchIl.EqualCharacterSpans)
                            {
                                if (capacity > int.MaxValue - SpanWasm.EqualityCapacity) return -3;
                                capacity += SpanWasm.EqualityCapacity;
                            }
                            else if (kind == BranchIl.ArrayClear)
                            {
                                tags = ExceptionTypes.ArgumentNull | ExceptionTypes.IndexOutOfRange;
                                if (capacity > int.MaxValue - ArrayClearWasm.Capacity) return -3;
                                capacity += ArrayClearWasm.Capacity;
                            }
                            else if (kind == BranchIl.ArrayCopy)
                            {
                                tags = ExceptionTypes.ArgumentNull | ExceptionTypes.ArgumentOutOfRange | ExceptionTypes.Argument;
                                if (capacity > int.MaxValue - ArrayCopyWasm.Capacity) return -3;
                                capacity += ArrayCopyWasm.Capacity;
                            }
                            else if (kind == BranchIl.ThrowFrameworkArgument)
                                tags = operation == ExceptionTagsWasm.ArgumentNullTag
                                    ? ExceptionTypes.ArgumentNull : ExceptionTypes.ArgumentOutOfRange;
                            else if (kind == BranchIl.StringFromSpan)
                                tags = (hostFeatures & WasmImports.NativeCharacterViews) != 0 ? ExceptionTypes.NullReference : 0;
                            else if (kind == BranchIl.SpanRead || kind == BranchIl.SpanAddress)
                                tags = ExceptionTypes.IndexOutOfRange |
                                    ((hostFeatures & WasmImports.NativeCharacterViews) != 0 ? ExceptionTypes.NullReference : 0);
                            else if (kind == BranchIl.ArrayGet || kind == BranchIl.ArraySet || kind == BranchIl.StringCharacter ||
                                kind == BranchIl.ObjectElementAddress)
                                tags = ExceptionTypes.IndexOutOfRange | ExceptionTypes.NullReference;
                            else if (kind == BranchIl.RuntimeTypeDisplayName || kind == BranchIl.RuntimeTypeIsGeneric || kind == BranchIl.RuntimeTypeIsEnum ||
                                kind == BranchIl.RuntimeTypeGenericDefinition || kind == BranchIl.RuntimeTypeGenericArguments || kind == BranchIl.RuntimeTypeIsValue || kind == BranchIl.ArrayLength || kind == BranchIl.GetField ||
                                kind == BranchIl.SetField || kind == BranchIl.ClearInteriorField || kind == BranchIl.TakeProjectedFieldAddress ||
                                nativeField || kind == BranchIl.StringLength || kind == BranchIl.StringFirstCodeUnit ||
                                kind == BranchIl.GetReferenceField || kind == BranchIl.SetReferenceField || kind == BranchIl.TakeReferenceFieldAddress ||
                                kind == BranchIl.StringRange || kind == BranchIl.StringContainsCodeUnit || kind == BranchIl.InvokeDelegate ||
                                kind == BranchIl.ThrowNull || kind == BranchIl.ReadReflection || kind == BranchIl.GetObjectType ||
                                (kind == BranchIl.EqualRuntimeDescriptors && operation == RuntimeTypeMetadata.Assembly) ||
                                kind == BranchIl.LoadIndirect || kind == BranchIl.ReadStaticUInt16 || kind == BranchIl.StoreIndirect || kind == BranchIl.TakeValueFieldAddress ||
                                kind == BranchIl.LoadManagedCell || kind == BranchIl.StoreManagedCell || kind == BranchIl.ClearManagedCell ||
                                kind == BranchIl.SetCurrentThreadHandle ||
                                kind == BranchIl.LoadObjectIndirect || kind == BranchIl.StoreObjectIndirect ||
                                kind == BranchIl.LoadObject || kind == BranchIl.StoreObject || kind == BranchIl.InitializeNative ||
                                (kind == BranchIl.PinString && records[slot + 3] != 0) ||
                                kind == BranchIl.ExchangeHandlerField || (kind == BranchIl.Call && (records[slot + 3] & 1) != 0))
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
                    frameShape[20] = WasmImports.Win32ErrorBase(hostFeatures);
                    frameShape[23] = WasmImports.MonitorSlot(hostFeatures);
                    frameShape[11] = valueConstructor ? 1 : 0;
                    frameShape[12] = initializerPrologue + 1;
                    frameShape[13] = initializerGuard + 1;
                    frameShape[14] = initializers.Epoch + 1;
                    frameShape[15] = initializers.Active + 1;
                    frameShape[16] = allocates ? shadowStack[0] : 0;
                    frameShape[17] = allocates ? shadowStack[1] : 0;
                    frameShape[18] = allocates ? shadowStack[2] : 0;
                    frameShape[24] = pins ? pinRegistry[0] : 0;
                    frameShape[25] = (hostFeatures & WasmImports.NativeCharacterViews) != 0 ? 1 : 0;
                    // Stage 151 (decision 0079): readLine's function index + 1, when the body reads a line.
                    frameShape[26] = readsLine ? WasmImports.InputFunction(hostFeatures) + 1 : 0;
                    for (int i = 0; i < ExceptionDispatchWasm.FrameWidth; i++)
                        frameShape[ExceptionDispatchWasm.FrameSlot + i] = fixedObjects[i];
                    frameShape[19] = resourceOpen ? ((hostFeatures & WasmImports.Console) != 0 ? ConsoleWasm.ImportCount + 1 : 1) : 0;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        int kind = records[slot];
                        if (kind == BranchIl.JoinStrings || kind == BranchIl.StringFromChars || kind == BranchIl.StringEquals || kind == BranchIl.StringRange ||
                            kind == BranchIl.StringContainsCodeUnit || kind == BranchIl.StringFromSpan || kind == BranchIl.ConsoleWrite ||
                            kind == BranchIl.PinString || kind == BranchIl.ConsoleReadLine || kind == BranchIl.CopyCharacterSpan ||
                            kind == BranchIl.FindCharacterInSpan) frameShape[5] |= 1;
                        if (kind == BranchIl.CallPinnedCharacterNative) frameShape[5] |= 2;
                        if (kind == BranchIl.FormatEnum) frameShape[5] |= 5;
                        if (kind == BranchIl.CombineHandler || kind == BranchIl.RemoveHandler ||
                            (kind == BranchIl.InvokeDelegate && records[slot + 1] == EventHandlerReference))
                            frameShape[6] = 1;
                    }
                    code = new byte[capacity];
                    used = BranchWasm.Emit(records, depths, states, frameShape, frame, signatures, offsets,
                        arities, code, remainderFixup, catchRegion, outgoingNulls, outgoingReferences, objects, initializers.FieldInitializers,
                        reflection, inlineLocations, nativePlans.Plans, filters, frameCells, pointers, rootCount, frameRoots,
                        tracksFinalizers ? finalizers : null, wrapsFailure ? failure : null, primitiveArrays, projections, literals);
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
            // Earlier accesses need the same backing as the address-taking use.
            if ((hostUse[0] & ~hostFeatures) != 0 || addressedFields.Count != knownAddressedFields ||
                natives.Count != reservedNatives || (projections.Kinds & ~projections.KnownKinds) != 0)
            {
                features[0] = hostFeatures | hostUse[0];
                features[2] = features[2] | (uint)projections.Kinds;
                return Reemit;
            }
            length = receiverDemand.WriteCarriers(moduleCode, length, functions, contexts, sizes, bodyOffsets);
            if (length < 0) return length;
            length = dispatch.Write(moduleCode, bodyOffsets, sizes, arities, offsets, signatures, objects, length,
                hostImports, location, reflection, primitiveArrays);
            if (length < 0) return length;
            var interfacePosition = new[] { length };
            functions = reflection.InterfaceTests.Write(bundle, moduleCode, interfacePosition, objects, reflection.AggregateBoxes,
                reflection.ObjectTypes.HasEnumBoxes, offsets, signatures,
                sizes, bodyOffsets, arities, functions, hostImports);
            if (functions < 0) return functions;
            length = interfacePosition[0];
            if (reflection.ObjectTypes.Fixups[0] != 0)
            {
                if (functions >= owners.Length) return -3;
                int offset = NextSignature(offsets, arities, functions);
                if (offset + 1 >= signatures.Length) return -3;
                signatures[offset] = RuntimeTypeMetadata.Type;
                signatures[offset + 1] = ObjectReference;
                arities[functions] = 1;
                offsets[functions] = offset;
                bodyOffsets[functions] = length;
                length = reflection.ObjectTypes.Write(bundle, moduleCode, length, objects, globals, primitiveArrays, reflection.AggregateBoxes);
                if (length < 0) return length;
                WasmIndexFixups.Resolve(moduleCode, reflection.ObjectTypes.Fixups[0], hostImports + functions);
                sizes[functions] = length - bodyOffsets[functions];
                functions++;
            }
            // GetType can publish additional argument descriptors before the factories are written.
            if (reflection.GenericTypes.HasFactories)
            {
                exceptionTags |= ExceptionTypes.ArgumentNull | ExceptionTypes.Argument;
                length = reflection.GenericTypes.Write(bundle, moduleCode, length, objects, globals, bodyOffsets, sizes);
                if (length < 0) return length;
            }
            if (reflection.GenericDefinitions.Fixups[0] != 0)
            {
                exceptionTags |= ExceptionTypes.Object;
                if (functions >= owners.Length) return -3;
                int offset = NextSignature(offsets, arities, functions);
                if (offset + 1 >= signatures.Length) return -3;
                signatures[offset] = RuntimeTypeMetadata.Type;
                signatures[offset + 1] = RuntimeTypeMetadata.Type;
                arities[functions] = 1;
                offsets[functions] = offset;
                bodyOffsets[functions] = length;
                length = reflection.GenericDefinitions.Write(bundle, moduleCode, length, objects, globals, hostImports);
                if (length < 0) return length;
                WasmIndexFixups.Resolve(moduleCode, reflection.GenericDefinitions.Fixups[0], hostImports + functions);
                sizes[functions] = length - bodyOffsets[functions];
                functions++;
            }
            if (reflection.GenericArguments.Fixups[0] != 0)
            {
                if (functions >= owners.Length) return -3;
                int offset = NextSignature(offsets, arities, functions);
                if (offset + 1 >= signatures.Length) return -3;
                signatures[offset] = reflection.GenericArguments.Vector;
                signatures[offset + 1] = RuntimeTypeMetadata.Type;
                arities[functions] = 1;
                offsets[functions] = offset;
                bodyOffsets[functions] = length;
                length = reflection.GenericArguments.Write(bundle, moduleCode, length, objects, globals);
                if (length < 0) return length;
                WasmIndexFixups.Resolve(moduleCode, reflection.GenericArguments.Fixups[0], hostImports + functions);
                sizes[functions] = length - bodyOffsets[functions];
                functions++;
            }
            for (int nameForm = RuntimeTypeNames.Display; nameForm <= RuntimeTypeNames.Namespace; nameForm++)
            {
                int[] fixups = reflection.TypeTextFixups(nameForm);
                if (fixups[0] == 0) continue;
                if (functions >= owners.Length) return -3;
                int offset = NextSignature(offsets, arities, functions);
                if (offset + 1 >= signatures.Length) return -3;
                signatures[offset] = String;
                signatures[offset + 1] = RuntimeTypeMetadata.Type;
                arities[functions] = 1;
                offsets[functions] = offset;
                bodyOffsets[functions] = length;
                length = RuntimeTypeNames.Write(bundle, moduleCode, length, objects, globals, literals, literalSize, nameForm);
                if (length < 0) return length;
                WasmIndexFixups.Resolve(moduleCode, fixups[0], hostImports + functions);
                sizes[functions] = length - bodyOffsets[functions];
                functions++;
            }
            var predicates = new[] { BranchIl.RuntimeTypeIsGeneric, BranchIl.RuntimeTypeIsEnum, BranchIl.RuntimeTypeIsValue };
            for (int index = 0; index < predicates.Length; index++)
            {
                int predicate = predicates[index];
                int[] fixups = reflection.TypePredicateFixups(predicate);
                if (fixups[0] == 0) continue;
                if (functions >= owners.Length) return -3;
                int offset = NextSignature(offsets, arities, functions);
                if (offset + 1 >= signatures.Length) return -3;
                signatures[offset] = I32;
                signatures[offset + 1] = RuntimeTypeMetadata.Type;
                arities[functions] = 1;
                offsets[functions] = offset;
                bodyOffsets[functions] = length;
                length = RuntimeTypePredicates.Write(bundle, moduleCode, length, objects, globals, predicate);
                if (length < 0) return length;
                WasmIndexFixups.Resolve(moduleCode, fixups[0], hostImports + functions);
                sizes[functions] = length - bodyOffsets[functions];
                functions++;
            }
            if (reflection.TypeRelations.HasAssignableTo) exceptionTags |= ExceptionTypes.NullReference;
            length = reflection.TypeRelations.Write(bundle, moduleCode, length, objects, globals, bodyOffsets, sizes);
            if (length < 0) return length;
            length = reflection.TypeRelations.WriteAssignableTo(bundle, moduleCode, length, objects, globals,
                reflection.GenericTypes, bodyOffsets, sizes);
            if (length < 0) return length;
            length = reflection.AssemblyMetadata.Write(moduleCode, length, bodyOffsets, sizes, hostImports, reflection);
            if (length < 0) return length;
            var projectionHelpers = new int[ValueFieldProjectionWasm.HelperCount];
            for (int helper = 0; helper < projectionHelpers.Length; helper++)
            {
                bool snapshot = ValueFieldProjectionWasm.IsSnapshotHelper(helper);
                if (projections.Fixups[helper] == 0 && !(snapshot && projections.Aggregates)) continue;
                int offset = NextSignature(offsets, arities, functions);
                bool write = (helper & 1) != 0;
                int arity = write ? 2 : 1;
                if (functions >= owners.Length || arity >= signatures.Length - offset) return -3;
                int value = ValueFieldProjectionWasm.ValueType(helper);
                signatures[offset] = write ? Void : value;
                signatures[offset + 1] = snapshot ? ValueLocation(FirstClass) : ObjectLocation;
                if (write) signatures[offset + 2] = value;
                arities[functions] = arity;
                offsets[functions] = offset;
                bodyOffsets[functions] = length;
                length = ValueFieldProjectionWasm.WriteHelper(moduleCode, length, objects, projections, helper);
                if (length < 0) return length;
                projectionHelpers[helper] = hostImports + functions;
                sizes[functions] = length - bodyOffsets[functions];
                functions++;
            }
            // An aggregate accessor can call itself and the other accessor.
            // Resolve after all bodies have published their fixup chains.
            for (int helper = 0; helper < projectionHelpers.Length; helper++)
                WasmIndexFixups.Resolve(moduleCode, projections.Fixups[helper], projectionHelpers[helper]);
            for (int ordinal = 0; ordinal < DelegateShapes.EntryCount(objects); ordinal++)
            {
                int key = DelegateShapes.KeyAt(ordinal), helper = DelegateShapes.InvokeHelper(objects, key);
                if (helper < 0) continue;
                bodyOffsets[helper] = length;
                length = GeneralDelegates.WriteInvokeHelper(moduleCode, length, objects, key, delegateTypeFixups);
                if (length < 0) return length;
                sizes[helper] = length - bodyOffsets[helper];
            }
            for (int c = 0; c < pointers.CarrierCount; c++)
            {
                int carrier = pointers.Carriers[c];
                if (moduleCode.Length - length < 3) return -3;
                bodyOffsets[carrier] = length;
                length = FunctionPointerWasm.CarrierBody(moduleCode, length);
                sizes[carrier] = length - bodyOffsets[carrier];
            }
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
                        hostImports + target, arity, shadowStack[0] - 1, shadowStack[2] - 1, pinRegistry[0] - 1);
                    if (length < 0) return length;
                    sizes[functions] = length - bodyOffsets[functions];
                    exports[export] = functions++;
                }
            }
            int rootWalker = -1;
            if (tracksFinalizers)
            {
                rootWalker = hostImports + functions;
                WasmIndexFixups.Resolve(moduleCode, finalizers.RegistrationFixups[0], rootWalker + FinalizerQueueWasm.Register);
                WasmIndexFixups.Resolve(moduleCode, finalizers.SuppressionFixups[0], rootWalker + FinalizerSuppressionWasm.Suppress);
                for (int entry = 0; entry < FinalizerSuppressionWasm.End; entry++)
                {
                    bool read = entry == 1;
                    if (functions >= owners.Length) return -3;
                    int offset = NextSignature(offsets, arities, functions);
                    int arity = read ? 2 : entry == 0 || entry == ReachabilityWasm.Contains || entry == ReachabilityWasm.Add ||
                        entry == FinalizerQueueWasm.Register || entry == FinalizerSuppressionWasm.Suppress || entry == FinalizerSuppressionWasm.Consume ? 1 : 0;
                    if (arity >= signatures.Length - offset) return -3;
                    signatures[offset] = read || entry == FinalizerQueueWasm.Pop ? ObjectReference :
                        entry == ReachabilityWasm.Clear || entry == ReachabilityWasm.Add || entry == FinalizerQueueWasm.Register ||
                        entry == FinalizerSuppressionWasm.Suppress ? Void : I32;
                    if (arity > 0) signatures[offset + 1] = ObjectReference;
                    if (read) signatures[offset + 2] = I32;
                    arities[functions] = arity;
                    offsets[functions] = offset;
                    bodyOffsets[functions] = length;
                    if (entry < ReachabilityWasm.Mark) length = HeapEdgesWasm.Write(moduleCode, length, objects, read);
                    else if (entry < ReachabilityWasm.End)
                        length = ReachabilityWasm.Write(moduleCode, length, entry, rootWalker, rootCount, markedCount, pendingCount,
                            pinRegistry[0] - 1, handleSlots, globals);
                    else if (entry == FinalizerQueueWasm.RunNext)
                        length = FinalizerDispatchWasm.Write(moduleCode, length, objects, finalizers, rootWalker, hostImports, rootCount);
                    else if (entry < FinalizerQueueWasm.End)
                        length = FinalizerQueueWasm.Write(moduleCode, length, entry, rootWalker, registeredCount, pendingCount);
                    else length = FinalizerSuppressionWasm.Write(moduleCode, length, entry, registeredCount, pendingCount, suppressedCount);
                    if (length < 0) return length;
                    sizes[functions] = length - bodyOffsets[functions];
                    functions++;
                }
            }
            CompileLocation.Clear(location);
            if (linearMemory) hostFeatures |= WasmImports.Memory;
            return WasmBinary.WriteModule(output, arities, offsets, signatures, sizes, bodyOffsets, functions,
                moduleCode, entries, entryOffsets, exports, objects, thunks, literals, literalSize[0], globals, remainderFixup[0], exceptionTags, hostFeatures, reflection.HeapFixups[0], pointers, rootCount, rootWalker, rootProbe, natives,
                primitiveArrays, delegateTypeFixups);
        }

        private static bool IsExported(int[] exports, int target)
        {
            for (int i = 0; i < exports.Length; i++)
                if (exports[i] == target) return true;
            return false;
        }

        private static bool FoldCharacterLocalStore(int[] records, int[] clauses, int address, int value, int local,
            int[] frame)
        {
            if (value >= records.Length || ValueOperands.Entry(records, value / BranchIl.Width, clauses)) return false;
            if (records[value] != BranchIl.Constant &&
                (records[value] != BranchIl.Load || StackType(frame[records[value + 1]]) != I32)) return false;

            int store = records[value + 2] * BranchIl.Width;
            if (store >= records.Length || records[store] != BranchIl.StoreIndirect ||
                records[store + 1] != 0x53 || ValueOperands.Entry(records, store / BranchIl.Width, clauses)) return false;
            records[address] = BranchIl.Nop;
            records[store] = BranchIl.Store;
            records[store + 1] = local;
            records[store + 3] = 0;
            return true;
        }

        private static bool CanOwnCharacterCell(int[] frame, int parameters, int result, int[] objects)
        {
            // Local-backed views may be read and passed by value. A method
            // cannot return a byref-like value or publish it via caller storage.
            if (IsSpan(result) || IsManagedLocation(result) || IsValueLocation(result) ||
                ObjectLayouts.IsByRefLikeValue(objects, result)) return false;
            for (int i = 0; i < parameters; i++)
                if (IsManagedLocation(frame[i]) || IsValueLocation(frame[i])) return false;
            return true;
        }

        private static int LinkOperands(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int methodCount, int owner, int callerRow, int context, int callerReceiver, bool valueReceiver, bool readOnlyReceiver, bool valueConstructor, int[] records, int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures, int[] objects, int[] thunks,
            int[] globals, byte[] literals, int[] literalSize, int functions, int[] location, int[] frame, int[] clauses, long[] hostUse, int[] virtualTypes, int[] virtualUsed,
            TypeInitializerPlan initializers, RuntimeReflectionPlan reflection, RuntimeTypeCalls typeCalls,
            MethodInstantiations instantiations, int[] methodArguments,
            int[] inlineLocations, int[] frameCells, int parameterCount, int result, NativeAggregatePlans nativePlans,
            AddressedFields addressedFields, byte[] localElements,
            FunctionPointerPlan pointers, FinalizerSlots finalizers, bool tracksFinalizers, int scopedReceiver)
        {
            int callerType = 0; // The caller's TypeDef, read once when a member needs access checks.
            bool arrayEnumerator = ArrayElementKey(objects, callerReceiver) != 0 &&
                SzArrayEnumeration.IsFactory(image, layout, heaps, callerRow);
            bool classConstructor = context != 0 && callerReceiver == context && !ObjectLayouts.IsValueType(objects, context) &&
                (MethodDefinitions.ReadColumn(image, layout, callerRow, 2) & 0x1850) == 0x1800 &&
                ObjectLayouts.IsConstructor(image, heaps, layout, callerRow);
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
            {
                int kind = records[slot];
                if (kind == 0) continue;
                if (location.Length != 0) location[2] = slot / BranchIl.Width;
                if (kind == BranchIl.BoundScopedFieldLoad || kind == BranchIl.BoundScopedFieldStore)
                {
                    // The earlier field-address record already bound the declaring type and ordinal.
                    records[slot] = kind == BranchIl.BoundScopedFieldLoad ? BranchIl.GetField : BranchIl.SetField;
                    continue;
                }
                // A direct CLI call still contains its original MethodDef row.
                // Validate it before reflection recognizers read its metadata.
                if (kind == BranchIl.Call && (records[slot + 1] < 1 || records[slot + 1] > methodCount))
                    return -1;
                if ((kind == BranchIl.Call || kind == BranchIl.MemberCall) &&
                    RuntimeTypeTokens.LowerExactHandleGetter(image, layout, heaps, records, slot, clauses)) continue;
                if ((kind == BranchIl.Call || kind == BranchIl.MemberCall && records[slot + 3] != 0x73) &&
                    RuntimeTypeMetadata.IsTypeAssignableTo(image, heaps, layout, records[slot + 1], kind == BranchIl.Call))
                {
                    var helper = new[] { -1 };
                    bool callvirt = records[slot + 3] == (kind == BranchIl.Call ? 1 : 0x6f);
                    functions = reflection.TypeRelations.BindTo(bundle, objects, globals, reflection.GenericTypes,
                        records, slot, clauses, owners, arities, offsets, signatures, functions, helper, callvirt);
                    if (functions < 0) return functions;
                    if (helper[0] >= 0)
                    {
                        records[slot] = BranchIl.Call;
                        records[slot + 1] = helper[0];
                        records[slot + 3] = 0;
                        continue;
                    }
                }
                if (kind == BranchIl.Call && records[slot + 3] == 1 ||
                    kind == BranchIl.MemberCall && records[slot + 3] == 0x6f)
                {
                    if (RuntimeTypeMetadata.IsMakeGenericType(image, heaps, layout, records[slot + 1], kind == BranchIl.Call))
                    {
                        var literalArgument = new[] { -1 };
                        int open = RuntimeGenericTypes.OpenDefinition(bundle, objects, records, slot, clauses, literalArgument);
                        if (open >= 0)
                        {
                            var helper = new int[1];
                            functions = reflection.GenericTypes.Bind(bundle, objects, globals, open, literalArgument[0], owners, arities,
                                offsets, signatures, functions, helper);
                            if (functions < 0) return functions;
                            records[slot] = BranchIl.Call;
                            records[slot + 1] = helper[0];
                            records[slot + 3] = 0;
                            continue;
                        }
                    }
                    if (RuntimeTypeMetadata.IsTypeAssignableFrom(image, heaps, layout, records[slot + 1], kind == BranchIl.Call))
                    {
                        var argument = new[] { -1 };
                        int literalReceiver = RuntimeTypeRelations.LiteralReceiver(records, slot, clauses, argument);
                        if (literalReceiver >= 0)
                        {
                            var helper = new int[1];
                            functions = reflection.TypeRelations.Bind(bundle, objects, globals, literalReceiver, argument[0],
                                owners, arities, offsets, signatures, functions, helper);
                            if (functions < 0) return functions;
                            records[slot] = BranchIl.Call;
                            records[slot + 1] = helper[0];
                            records[slot + 3] = 0;
                            continue;
                        }
                    }
                }
                if (kind == BranchIl.LoadTypeToken)
                {
                    int explicitCall = ExactTypeInitialization.MatchCall(image, layout, heaps, records, slot, clauses);
                    if (explicitCall >= 0)
                    {
                        if (location.Length != 0) location[2] = explicitCall / BranchIl.Width;
                        var initializedType = new int[3];
                        int initializerRow = ExactTypeInitialization.Resolve(bundle, image, layout, heaps, owner,
                            records[slot + 1], context, objects, methodArguments, initializedType);
                        if (initializerRow < 0) return initializerRow;
                        int initializerFunction = -1;
                        if (initializerRow > 0)
                        {
                            var functionSlot = new int[1];
                            functions = initializers.Ensure(bundle, initializedType[0], initializedType[1], initializerRow, owners, rows, contexts,
                                bases, indices, arities, offsets, signatures, objects, globals, functions, functionSlot, initializedType[2]);
                            if (functions < 0) return functions;
                            initializerFunction = functionSlot[0];
                        }
                        ExactTypeInitialization.Rewrite(records, slot, explicitCall, initializerFunction);
                        slot = explicitCall;
                        continue;
                    }
                    int global = RuntimeTypeTokens.Bind(bundle, image, layout, heaps, owner, records[slot + 1],
                        globals, context, objects, methodArguments);
                    if (global < 0) return global;
                    records[slot + 1] = global;
                    continue;
                }
                if (kind == BranchIl.NewArray)
                {
                    int array = ArrayAllocations.Bind(bundle, image, layout, heaps, owner, context, methodArguments, objects, records, slot);
                    if (array < 0) return array;
                    continue;
                }
                if (kind == BranchIl.TestClass &&
                    RuntimeTypeMetadata.IsAssemblyDefinition(image, heaps, layout, records[slot + 1]))
                {
                    int entryMember = ReflectionMembers.CoreEntryAssemblyMember(image, heaps, layout, callerRow);
                    if (entryMember == ReflectionMembers.EntryGetter || entryMember == ReflectionMembers.EntrySetter)
                    {
                        int status = reflection.Bind(bundle, globals, literals, literalSize);
                        if (status < 0) return status;
                        records[slot + 1] = RuntimeTypeMetadata.Assembly;
                        continue;
                    }
                }
                if (kind == BranchIl.BoxValue || kind == BranchIl.UnboxValue || kind == BranchIl.UnboxAggregateAddress || kind == BranchIl.TestClass)
                {
                    int operand = records[slot + 1];
                    if (kind == BranchIl.TestClass)
                    {
                        int tag = operand & 3;
                        int table = tag == 1 ? 1 : tag == 0 ? 2 : 0x1b;
                        operand = table << 24 | operand >> 2;
                    }
                    int element = PrimitiveBoxing.Element(bundle, image, layout, heaps,
                        owner, operand, context, methodArguments, objects);
                    if (element > 0)
                    {
                        if (kind == BranchIl.UnboxAggregateAddress) return -2;
                        int boxing = PrimitiveBoxing.Layout(objects);
                        if (boxing < 0) return boxing;
                        if (kind == BranchIl.TestClass) records[slot] = BranchIl.TestBox;
                        records[slot + 1] = element;
                        records[slot + 3] = boxing;
                        continue;
                    }
                    if (element != -2) return element;
                    // Platform reflection descriptors use their own representation.
                    // Keep their cast diagnostic before probing for a value layout.
                    if (kind == BranchIl.TestClass &&
                        RuntimeTypeMetadata.RequiresReflectionCast(image, heaps, layout, records[slot + 1])) return -2;
                    int type = AggregateBoxing.ValueIdentity(bundle, image, layout, heaps, owner, operand, context, methodArguments, objects);
                    if (type < 0 && type != -2) return type;
                    if (kind == BranchIl.BoxValue && (operand >> 24) == 0x1b)
                    {
                        if ((ObjectLayouts.IsEnumKey(type) || ObjectLayouts.IsValueType(objects, type) &&
                            !ObjectLayouts.IsNullableValueType(objects, type) && !ObjectLayouts.IsByRefLikeValue(objects, type)) &&
                            BoxNullTests.TryFoldBoxNullTest(records, slot, clauses))
                            continue;
                        if (IsObjectReference(type) && !ObjectLayouts.IsValueType(objects, type))
                        {
                            // A closed reference T is already a CLR object. box T widens it and preserves identity.
                            records[slot] = BranchIl.WidenReference;
                            records[slot + 1] = type;
                            continue;
                        }
                    }
                    if (ObjectLayouts.IsEnumKey(type))
                    {
                        if (kind == BranchIl.UnboxAggregateAddress) return -2;
                        int tag = BoxedEnums.Bind(bundle, objects, type, globals);
                        if (tag < 0) return tag;
                        int box = PrimitiveBoxing.Layout(objects);
                        if (box < 0) return box;
                        if (kind == BranchIl.TestClass) records[slot] = BranchIl.TestBox;
                        records[slot + 1] = tag;
                        records[slot + 3] = box;
                        continue;
                    }
                    if (ObjectLayouts.IsValueType(objects, type))
                    {
                        if (ObjectLayouts.IsNullableValueType(objects, type) || ObjectLayouts.IsByRefLikeValue(objects, type)) return -2;
                        int box = AggregateBoxing.Layout(objects);
                        if (box < 0) return box;
                        int descriptor = RuntimeTypeTokens.BindLayout(bundle, objects, type, globals);
                        if (descriptor < 0) return descriptor;
                        if (kind != BranchIl.UnboxAggregateAddress)
                            records[slot] = kind == BranchIl.BoxValue ? BranchIl.BoxAggregate :
                                kind == BranchIl.UnboxValue ? BranchIl.UnboxAggregate : BranchIl.TestAggregateBox;
                        records[slot + 1] = type;
                        records[slot + 3] = descriptor;
                        continue;
                    }
                    if (kind == BranchIl.UnboxValue && (operand >> 24) == 0x1b && IsObjectReference(type))
                    {
                        // For a closed reference T, unbox.any T has castclass
                        // semantics, including identity, null and InvalidCastException.
                        if (type == ObjectReference)
                        {
                            records[slot] = BranchIl.WidenReference;
                            records[slot + 1] = type;
                            continue;
                        }
                        kind = BranchIl.CastClass;
                        records[slot] = kind;
                        records[slot + 1] = ((operand & 0xffffff) << 2) | 2; // TypeSpec coded index
                    }
                    else if (kind != BranchIl.TestClass) return -2;
                }
                if ((kind == BranchIl.ArrayGet || kind == BranchIl.ArraySet) && records[slot + 3] != 0)
                {
                    int token = records[slot + 3], table = token >> 24;
                    if (table != 1 && table != 2 && table != 0x1b) return -2;
                    int coded = (token & 0xffffff) << 2 | (table == 1 ? 1 : table == 2 ? 0 : 2);
                    int primitive = table == 0x1b ? -2 : ArrayTypeRefs.PrimitiveElement(image, heaps, layout, coded);
                    int type = primitive > 0 ? GenericScalar.Key(primitive) :
                        ArrayAllocations.TokenIdentity(bundle, image, layout, heaps, owner, context, methodArguments, objects, coded);
                    if (type < 0) return type;
                    if (GenericScalar.IsScalar(type))
                    {
                        int opcode = PrimitiveArrayAccess(GenericScalar.Element(type), kind == BranchIl.ArrayGet);
                        if (opcode == 0) return -2;
                        records[slot + 1] = opcode;
                        records[slot + 3] = 0;
                        continue;
                    }
                    if (ObjectLayouts.IsEnumKey(type) || (!ObjectLayouts.IsValueType(objects, type) && !IsObjectReference(type)))
                        return -2;
                    int vector = ObjectLayouts.RegisterArray(objects, type);
                    if (vector < 0) return vector;
                    records[slot + 1] = vector;
                    records[slot + 3] = type;
                    continue;
                }
                if (kind == BranchIl.ObjectElementAddress || kind == BranchIl.LoadObjectIndirect || kind == BranchIl.StoreObjectIndirect)
                {
                    if (kind == BranchIl.ObjectElementAddress && records[slot + 3] == BranchIl.BoundObjectElement)
                        continue;
                    if (kind == BranchIl.ObjectElementAddress)
                    {
                        int coded = records[slot + 1];
                        int primitive = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, coded);
                        if (primitive == 8)
                        {
                            records[slot + 1] = Ints;
                            continue;
                        }
                        int type = ArrayAllocations.TokenIdentity(bundle, image, layout, heaps, owner, context, methodArguments, objects, coded);
                        if (ObjectLayouts.IsValueType(objects, type) && !ObjectLayouts.IsExplicitValue(objects, type))
                        {
                            int vector = ObjectLayouts.RegisterArray(objects, type);
                            if (vector < 0) return vector;
                            records[slot + 1] = vector;
                            records[slot + 3] = type;
                            continue;
                        }
                        if (type != ObjectReference) return -2;
                    }
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
                    int type = (coded & 3) == 2
                        ? ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps, owner,
                            coded >> 2, context, objects, methodArguments)
                        : PlatformTypeReferences.IsString(image, heaps, layout, coded) ? String :
                            BundleLayouts.RegisterReference(bundle, image, layout, heaps, owner, coded, objects);
                    if (type < 0) return type;
                    if (ObjectLayouts.IsValueType(objects, type)) return -2;
                    if (type == EventHandlerReference || DelegateShapes.IsKey(type))
                    {
                        records[slot] = BranchIl.CastHandler;
                        records[slot + 1] = type;
                        records[slot + 3] = kind == BranchIl.TestClass ? 1 : 0;
                        continue;
                    }
                    else if (ObjectLayouts.IsInterface(objects, type))
                    {
                        int bound = reflection.InterfaceTests.Bind(bundle, objects, type);
                        if (bound == RuntimeInterfaceTests.RequiresExactStackType)
                        {
                            if (kind != BranchIl.CastClass) return -2;
                            records[slot + 3] = ReferenceCastWasm.ProvenInterface;
                        }
                        else
                        {
                            if (bound < 0) return bound;
                            records[slot + 3] = ReferenceCastWasm.CheckedInterface;
                        }
                    }
                    else if (type != String && (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0] ||
                        ObjectLayouts.ArrayElement(objects, type) != 0)) return -2;
                    records[slot + 1] = type;
                    continue;
                }
                if (valueReceiver && (kind == BranchIl.Load || kind == BranchIl.Store) && records[slot + 1] == 0)
                {
                    // starg.0 would replace the address itself.
                    if (kind == BranchIl.Store) return -2;
                    if (valueConstructor)
                    {
                        int inline = InlineObjectAddresses.BindConstructorStore(bundle, image, layout, heaps, owner,
                            context, methodArguments, records, slot, frame[0], objects, clauses);
                        if (inline < 0) return inline;
                        if (inline != 0) continue;
                    }
                    // A traced receiver is already a real managed byref.
                    // Keep it on the evaluation stack for ordinary field operations and calls.
                    if (IsValueLocation(callerReceiver))
                    {
                        int reset = ValueOperands.BindReceiverInitialization(bundle, image, layout, heaps, owner,
                            records, slot, callerReceiver, objects, clauses, context, methodArguments);
                        if (reset < 0) return reset;
                        continue;
                    }
                    int lowered = ValueOperands.LowerReceiver(bundle, image, layout, heaps,
                        owner, bases[owner + 1] - bases[owner], records, slot, frame, objects, clauses, readOnlyReceiver, context, methodArguments);
                    if (lowered == -2 && valueConstructor)
                        records[slot] = BranchIl.LoadConstructorAddress;
                    else if (lowered < 0)
                        return ManagedObjectCopy(records, slot, location, lowered);
                    continue;
                }
                if (kind == BranchIl.FrameAddress)
                {
                    // The value-type this argument is already a managed address.
                    // ldarga.0 would instead take the address of that argument slot.
                    if (valueReceiver && records[slot + 1] == 0) return -2;
                    int local = records[slot + 1];
                    int type = frame[local];
                    int next = records[slot + 2] * BranchIl.Width;
                    if (type == I32 && PrimitiveHashCalls.LowerFrame(bundle, image, layout, heaps, owner,
                        context, methodArguments, objects, records, clauses, slot)) continue;
                    int enumFormatting = EnumFormatting.LowerFrame(bundle, image, layout, heaps, owner,
                        context, methodArguments, objects, records, clauses, slot, type, globals, literals, literalSize);
                    if (enumFormatting != 0)
                    {
                        if (enumFormatting < 0) return enumFormatting;
                        continue;
                    }
                    if (IsObjectReference(type) && !ObjectLayouts.IsValueType(objects, type))
                    {
                        int constrained = ConstrainedReferenceCalls.Lower(bundle, image, layout, heaps, owner, context,
                            objects, methodArguments, records, slot, type, clauses, bases[owner + 1] - bases[owner]);
                        if (constrained != 0)
                        {
                            if (constrained < 0) return constrained;
                            continue;
                        }
                    }
                    int singleSpan = type == ObjectReference ? ObjectSpan :
                        type == Character && local >= parameterCount ? CharacterSpan : 0;
                    if (singleSpan != 0 && next < records.Length &&
                        records[next] == BranchIl.MemberCall && records[next + 3] == 0x73 &&
                        !ValueOperands.Entry(records, next / BranchIl.Width, clauses) &&
                        SpanMetadata.IsSingleElementConstructor(bundle, image, layout, heaps, owner,
                            records[next + 1], context, methodArguments, objects, singleSpan))
                    {
                        if (singleSpan == CharacterSpan &&
                            !CanOwnCharacterCell(frame, parameterCount, result, objects)) return -2;
                        int vector = singleSpan == CharacterSpan ? Chars : ObjectLayouts.RegisterArray(objects, ObjectReference);
                        if (vector < 0) return vector;
                        frameCells[local] = vector;
                        records[slot] = BranchIl.Nop;
                        records[next] = BranchIl.LoadSingleFrameSpan;
                        records[next + 1] = local;
                        records[next + 3] = singleSpan;
                        continue;
                    }
                    // A plain indirect write through a local alias updates the
                    // same cell as stloc. Neither the address nor an alias escapes.
                    if (type == Character && local >= parameterCount &&
                        FoldCharacterLocalStore(records, clauses, slot, next, local, frame)) continue;
                    bool booleanSlot = type == I32 && local >= parameterCount && localElements[local - parameterCount] == SignatureReader.Boolean;
                    if (booleanSlot && next < records.Length &&
                        (records[next] == BranchIl.MemberCall && records[next + 3] == 0x28 ||
                            records[next] == BranchIl.Call && records[next + 3] == 0) &&
                        !ValueOperands.Entry(records, next / BranchIl.Width, clauses) &&
                        MonitorMetadata.Identify(image, heaps, layout, records[next + 1],
                            records[next] == BranchIl.MemberCall) == MonitorMetadata.EnterTaken)
                    {
                        // The call consumes this address. Emission updates the
                        // same Boolean cell if another managed use takes its address.
                        records[slot] = BranchIl.Nop;
                        records[next] = BranchIl.Monitor;
                        records[next + 1] = local + 1;
                        hostUse[0] = hostUse[0] | WasmImports.Monitor;
                        continue;
                    }
                    int referenceLocation = LocationFromIdentity(objects, type);
                    if (type == I32 || type == I64 || type == Native || IsReferenceLocation(referenceLocation))
                    {
                        // Native words still use managed cells when their address
                        // crosses managed calls. Explicit pointers use NativeFrameWasm.
                        int vector = type == Native ? NativeVector : type == I64 ? Longs : type == I32 ? booleanSlot ? Bytes : Ints :
                            ObjectLayouts.RegisterArray(objects, ObjectReference);
                        if (vector < 0) return vector;
                        frameCells[local] = vector;
                        records[slot] = BranchIl.LoadFrameLocation;
                        records[slot + 3] = type == Native ? NativeIntLocation :
                            type == I32 ? booleanSlot ? BooleanLocation : Int32Location : type == I64 ? Int64Location : referenceLocation;
                        continue;
                    }
                    if (IsSpan(type))
                    {
                        int span = SpanOperands.Bind(bundle, image, layout, heaps, owner, context, methodArguments,
                            records, slot, type, objects, clauses, frame, hostUse);
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
                        owner, bases[owner + 1] - bases[owner], records, slot, frame, objects, clauses, context, methodArguments);
                    if (lowered == -2 && ObjectLayouts.IsValueType(objects, type) &&
                        !ObjectLayouts.IsExplicitValue(objects, type))
                    {
                        int locationType = ValueLocation(type);
                        frameCells[local] = locationType;
                        records[slot] = BranchIl.LoadFrameLocation;
                        records[slot + 3] = locationType;
                    }
                    else if (lowered < 0) return ManagedObjectCopy(records, slot, location, lowered);
                    continue;
                }
                if (kind == BranchIl.ConstrainedInstance)
                {
                    if (PrimitiveHashCalls.Identify(bundle, image, layout, heaps, owner, context, methodArguments, objects, records, slot) == 8)
                    {
                        records[slot] = BranchIl.LoadManagedCell;
                        records[slot + 1] = Ints;
                        records[slot + 3] = Int32Location;
                        continue;
                    }
                    var overrideSymbol = new int[3];
                    int receiver = ConstrainedValueCalls.Resolve(bundle, image, layout, heaps, owner,
                        records[slot + 1], records[slot + 3], context, methodArguments, objects, overrideSymbol);
                    if (receiver < 0) return receiver;
                    int targetOwner = overrideSymbol[0], row = overrideSymbol[1], overrideContext = overrideSymbol[2];
                    int function = Lookup(owners, rows, contexts, bases, indices, functions, targetOwner, row, overrideContext);
                    if (function < 0)
                    {
                        byte[] implementation = bundle.Get(targetOwner);
                        var targetLayout = new int[4];
                        var targetHeaps = new int[8];
                        if (CompressedTables.FindWithHeaps(implementation, targetLayout, targetHeaps) < 0) return -1;
                        functions = Register(bundle, implementation, targetLayout, targetHeaps, targetOwner, row, overrideContext,
                            owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions,
                            allowVirtual: true, receiverOverride: IsValueLocation(receiver) ? LocatedValue(receiver) : receiver);
                        if (functions < 0) return functions;
                        function = functions - 1;
                    }
                    if (signatures[offsets[function] + 1] != receiver) return -2;
                    records[slot] = BranchIl.Call;
                    records[slot + 1] = function;
                    records[slot + 3] = 0;
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
                    if (!checkReceiver)
                    {
                        if (arrayEnumerator && spec[MethodSpecifications.Arity] == 1 &&
                            SzArrayEnumeration.IsArrayCast(image, layout, heaps, counts, spec[MethodSpecifications.Target]))
                        {
                            var cast = new int[1];
                            status = MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner,
                                context, objects, methodArguments, spec, cast);
                            if (status < 0) return status;
                            if (cast[0] != callerReceiver || slot < BranchIl.Width ||
                                records[slot - BranchIl.Width] != BranchIl.Load || records[slot - BranchIl.Width + 1] != 0 ||
                                ValueOperands.Entry(records, slot / BranchIl.Width, clauses)) return -2;
                            records[slot] = BranchIl.Nop;
                            continue;
                        }
                        int addressIntrinsic = NativeAddressIntrinsics.Identify(image, layout, heaps, counts, spec);
                        if (addressIntrinsic != 0)
                        {
                            var element = new int[1];
                            status = MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner,
                                context, objects, methodArguments, spec, element);
                            if (status < 0) return status;
                            int reference = ManagedReferenceSignatures.ElementLocation(objects, element[0]);
                            if (addressIntrinsic == BranchIl.NullManagedLocation && reference == 0) return -2;
                            records[slot] = addressIntrinsic;
                            records[slot + 3] = reference;
                            continue;
                        }
                        if (spec[MethodSpecifications.Arity] == 2 &&
                            ExactManagedReads.IsPlatformAs(image, heaps, layout, counts, spec[MethodSpecifications.Target]))
                        {
                            var elements = new int[2];
                            status = MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner,
                                context, objects, methodArguments, spec, elements);
                            if (status < 0) return status;
                            int sourceLocation = ManagedReferenceSignatures.ElementLocation(objects, elements[0]);
                            int targetLocation = ManagedReferenceSignatures.ElementLocation(objects, elements[1]);
                            if (IsPacked16Location(sourceLocation) && IsPacked16Location(targetLocation))
                            {
                                records[slot] = BranchIl.ReinterpretManagedLocation;
                                records[slot + 1] = sourceLocation;
                                records[slot + 3] = targetLocation;
                                continue;
                            }
                            int alias = BooleanStorageAliases.Lower(bundle, owner, objects, elements[0], elements[1], records, slot, clauses);
                            if (alias < 0) return alias;
                            if (alias > 0) continue;
                        }
                        int searchArity = spec[MethodSpecifications.Arity] == 1
                            ? SpanMetadata.ReadOnlySearchArity(image, layout, heaps, counts, spec[MethodSpecifications.Target]) : 0;
                        if (searchArity != 0)
                        {
                            var element = new int[1];
                            status = MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner,
                                context, objects, methodArguments, spec, element);
                            if (status < 0) return status;
                            if (element[0] == GenericScalar.Key(3))
                            {
                                records[slot] = BranchIl.FindCharacterInSpan;
                                records[slot + 1] = searchArity;
                                continue;
                            }
                        }
                        int intrinsic = StaticByteIntrinsics.Identify(image, layout, heaps, counts, spec, methodArguments);
                        if (intrinsic != 0)
                        {
                            if (intrinsic == BranchIl.OffsetCharacterLocation &&
                                !CharacterOffsetProof.IsAligned(records, slot, clauses)) return -2;
                            records[slot] = intrinsic;
                            continue;
                        }
                    }
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
                            context, callerType, valueAddress: !checkReceiver, methodArity: spec[MethodSpecifications.Arity],
                            callerMethodArguments: methodArguments);
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
                    // Generic virtual slots still need their own dispatch plan.
                    if ((checkReceiver && !instance) || (methodFlags & 0x40) != 0) return -2;
                    var types = new int[spec[MethodSpecifications.Arity]];
                    status = MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner,
                        context, objects, methodArguments, spec, types);
                    if (status < 0) return status;
                    bool assemblyMetadata = !instance && !checkReceiver && types.Length == 1 &&
                        RuntimeAssemblyMetadata.IsQuery(targetImage, targetLayout, targetHeaps, targetOwner, row, objects, types[0]);
                    var deferred = new int[8];
                    var constructors = new int[types.Length];
                    int constraints = GenericParameters.Check(targetImage, targetHeaps, targetLayout, row,
                        types, objects, deferred, method: true, constructors: constructors);
                    if (constraints < 0) return constraints;
                    status = GenericConstructorConstraints.Check(bundle, objects, types, constructors);
                    if (status < 0) return status;
                    if (constraints != 0)
                    {
                        constraints = assemblyMetadata ? RuntimeAssemblyMetadata.CheckConstraint(targetImage, targetLayout,
                            targetHeaps, targetOwner, objects, types[0], deferred) :
                            BundleConstraints.Check(bundle, targetImage, targetLayout, targetHeaps,
                                targetOwner, objects, types, deferred, method: true, context: targetContext);
                        if (constraints < 0) return constraints;
                    }
                    else if (assemblyMetadata) return -2;

                    if (assemblyMetadata)
                    {
                        var query = new int[1];
                        functions = reflection.AssemblyMetadata.Bind(bundle, targetImage, targetLayout, targetHeaps,
                            targetOwner, row, types[0], objects, globals, literals, literalSize, reflection,
                            owners, rows, contexts, bases, indices, arities, offsets, signatures, functions, query);
                        if (functions < 0) return functions;
                        records[slot] = BranchIl.Call;
                        records[slot + 1] = query[0];
                        records[slot + 3] = 0;
                        continue;
                    }

                    int implementationContext = targetContext;
                    if (instance && !checkReceiver)
                    {
                        int declaring = ObjectLayouts.Owner(targetImage, targetHeaps, targetLayout, row, false);
                        if (declaring < 1) return -1;
                        int value = targetContext != 0 ? targetContext : BundleLayouts.RegisterValue(bundle, targetImage,
                            targetLayout, targetHeaps, targetOwner, declaring, objects);
                        if (value < 0 && value != -2) return value;
                        if (ObjectLayouts.IsValueType(objects, value))
                            implementationContext = ~targetContext;
                    }
                    int function = instantiations.Find(owners, rows, contexts, functions, targetOwner, row, implementationContext, types);
                    if (function < 0)
                    {
                        functions = Register(bundle, targetImage, targetLayout, targetHeaps, targetOwner, row, implementationContext,
                            owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions, methodArguments: types);
                        if (functions < 0) return functions;
                        function = functions - 1;
                        if (instance)
                        {
                            int receiver = signatures[offsets[function] + 1];
                            if (implementationContext < 0 ? !IsValueLocation(receiver) :
                                !IsObjectReference(receiver) || ObjectLayouts.IsValueType(objects, receiver)) return -2;
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
                if (kind == BranchIl.LoadObject || kind == BranchIl.StoreObject || kind == BranchIl.InitializeValue)
                {
                    // Stage 145 (decision 0073): a blittable struct through a native address.
                    // initobj on a managed local was lowered by ValueOperands already.
                    uint token = unchecked((uint)records[slot + 1]);
                    uint table = token >> 24;
                    int row = (int)(token & 0xffffff);
                    if (table == 0x1b)
                    {
                        int locationType = ManagedReferenceSignatures.TypeSpecLocation(bundle, image, layout, heaps,
                            owner, row, context, objects, methodArguments);
                        if (locationType < 0) return locationType;
                        if (IsPacked16Location(locationType) && kind != BranchIl.InitializeValue)
                        {
                            // Closed packed 16-bit ldobj/stobj uses the nominal signedness.
                            bool load = kind == BranchIl.LoadObject;
                            records[slot] = load ? BranchIl.LoadIndirect : BranchIl.StoreIndirect;
                            records[slot + 1] = load ? locationType == Int16Location ? 0x48 : 0x49 : 0x53;
                            continue;
                        }
                        // Closed Int32 and UInt32 operations share the same
                        // managed cell, including the numeric zero of initobj.
                        bool int32Access = locationType == Int32Location;
                        bool int64Access = locationType == Int64Location;
                        if (!IsValueLocation(locationType) && locationType != BooleanLocation &&
                            !IsReferenceLocation(locationType) && !int32Access && !int64Access) return -2;
                        int vector = IsValueLocation(locationType) ? 0 :
                            locationType == BooleanLocation ? Bytes : int32Access ? Ints : int64Access ? Longs :
                            ObjectLayouts.RegisterArray(objects, ObjectReference);
                        if (vector < 0) return vector;
                        records[slot] = kind == BranchIl.LoadObject ? BranchIl.LoadManagedCell :
                            kind == BranchIl.StoreObject ? BranchIl.StoreManagedCell : BranchIl.ClearManagedCell;
                        records[slot + 1] = vector;
                        records[slot + 3] = locationType;
                        continue;
                    }
                    if (table != 1 && table != 2) return -2;
                    int coded = row << 2 | (table == 1 ? 1 : 0);
                    int type = BundleLayouts.RegisterType(bundle, image, layout, heaps, owner, coded, objects, true);
                    if (type < 0) return type;
                    if (!ObjectLayouts.IsValueType(objects, type)) return -2;
                    int plan = NativeAggregates.Plan(bundle, objects, type, nativePlans);
                    if (plan < 0 && plan != -2) return plan;
                    // The verifier selects a managed cell or native layout
                    // from the actual address, including for named types.
                    if (kind == BranchIl.InitializeValue) records[slot] = BranchIl.InitializeNative;
                    records[slot + 1] = type;
                    records[slot + 3] = plan + 1;
                    continue;
                }
                if (kind == BranchIl.FieldToken)
                    return -2;
                if (kind == BranchIl.GetField || kind == BranchIl.SetField || kind == BranchIl.FieldAddress)
                {
                    int field = records[slot + 1];
                    bool readOnlyAddress = false;
                    bool linked = records[slot + 3] != 0;
                    int nextSlot = records[slot + 2] * BranchIl.Width;
                    bool initializesField = kind == BranchIl.FieldAddress && nextSlot < records.Length &&
                        records[nextSlot] == BranchIl.InitializeValue && !ValueOperands.Entry(records, nextSlot / BranchIl.Width, clauses);
                    bool initializesConstructorField = initializesField && valueConstructor && slot >= BranchIl.Width &&
                        records[slot - BranchIl.Width] == BranchIl.LoadConstructorAddress &&
                        !ValueOperands.Entry(records, slot / BranchIl.Width, clauses);
                    if (!linked && kind == BranchIl.GetField && StringMetadata.IsFirstCodeUnitDefinition(image, heaps, layout, field))
                    {
                        records[slot] = BranchIl.StringFirstCodeUnit;
                        continue;
                    }
                    if (linked)
                    {
                        if (bundle.Length == 0) return -2;
                        var linkedField = new int[4];
                        if (callerType == 0) callerType = ObjectLayouts.Owner(image, heaps, layout, callerRow, false);
                        if (callerType < 0) return -1;
                        bool constructorReceiver = initializesConstructorField || kind == BranchIl.SetField &&
                            (classConstructor || valueConstructor && records[slot + BranchIl.Width + 1] == 1);
                        int status = AssemblyBundle.ResolveField(bundle, image, layout, heaps, owner,
                            field, kind == BranchIl.SetField || initializesField, false, objects, globals, linkedField, context, callerType,
                            methodArguments, constructorReceiver);
                        if (status < 0) return status;
                        records[slot + 1] = linkedField[0];
                        records[slot + 3] = linkedField[1];
                        field = linkedField[2];
                        readOnlyAddress = (linkedField[3] & 0x20) != 0;
                        if (kind == BranchIl.GetField && scopedReceiver != 0 &&
                            FieldAccess.Owner(linkedField[1]) == scopedReceiver && FieldAccess.Storage(linkedField[1]) == Native)
                            RecordScopedNativeField(bundle, objects, records, slot, scopedReceiver, field);
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
                        if (initializesField && !ValueOperands.IsWritableInteriorField(image, layout, heaps, field,
                            initializesConstructorField && frame[0] == type)) return -2;
                        records[slot + 1] = fieldIndex;
                        records[slot + 3] = FieldAccess.Encode(type, ObjectLayouts.FieldType(objects, type, field));
                        if (ObjectLayouts.IsValueType(objects, type) && records.Length > slot + 2 * BranchIl.Width + 3)
                        {
                            // Stage 143 (decision 0071): the same field through an S* address in memory 0.
                            var native = new int[2];
                            if (NativeValueLayouts.Field(bundle, owner, fieldOwner, field, native, scopedFrame: type == scopedReceiver) == 0)
                            {
                                records[slot + 2 * BranchIl.Width + 2] = native[0] + 1;
                                records[slot + 2 * BranchIl.Width + 3] = native[1];
                            }
                        }
                    }
                    int descriptor = records[slot + 3];
                    int declaring = FieldAccess.Owner(descriptor);
                    if (kind == BranchIl.GetField && ObjectLayouts.HasExplicitSlots(objects, declaring) &&
                        ObjectLayouts.IsInterface(objects, FieldAccess.Storage(descriptor)) &&
                        ObjectLayouts.LayoutFieldType(objects, declaring, records[slot + 1] / ObjectLayouts.ExplicitSlotWidth) == ObjectReference)
                        return -2; // A shared reference slot has no nominal interface heap to cast to.
                    int storage = FieldAccess.Storage(descriptor);
                    if (storage == RuntimeTypeMetadata.Type && kind == BranchIl.FieldAddress) return -2;
                    if (storage == RuntimeTypeMetadata.Type && kind == BranchIl.SetField &&
                        !RuntimeTypeFields.IsDeclaringConstructor(image, heaps, layout, owner, callerRow, objects, declaring)) return -2;
                    if (!ObjectLayouts.IsValueType(objects, declaring) && IsReferenceLocation(LocationFromIdentity(objects, storage)))
                    {
                        int assembly = ObjectLayouts.AssemblyOf(objects, declaring);
                        bool inCell = addressedFields.HasInstanceReference(assembly, field);
                        if (kind == BranchIl.FieldAddress)
                        {
                            if (linked ? readOnlyAddress : !ValueOperands.IsWritableInteriorField(image, layout, heaps, field, false))
                                return -2;
                            // The existing fusion also serves standalone event fixtures
                            // that do not supply the CoreLib method body.
                            if (!inCell && !linked && LinkExchange(bundle, image, heaps, layout, owner, objects, records, slot)) continue;
                            if (!inCell) addressedFields.AddInstanceReference(assembly, field);
                            inCell = true;
                        }
                        if (inCell)
                        {
                            int vector = ObjectLayouts.RegisterArray(objects, ObjectReference);
                            if (vector < 0) return vector;
                            ObjectLayouts.SetReferenceFieldVector(objects, declaring, field, vector);
                            records[slot + BranchIl.Width + 2] = vector;
                            records[slot] = kind == BranchIl.GetField ? BranchIl.GetReferenceField :
                                kind == BranchIl.SetField ? BranchIl.SetReferenceField : BranchIl.TakeReferenceFieldAddress;
                            continue;
                        }
                    }
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
                                else if (!pointerField && ObjectLayouts.IsExplicitValue(objects, FieldAccess.Owner(descriptor)))
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
                        int fieldOwner = FieldAccess.Owner(descriptor), fieldStorage = FieldAccess.Storage(descriptor);
                        if (initializesField && ObjectLayouts.IsValueType(objects, fieldOwner) &&
                            !ObjectLayouts.IsExplicitValue(objects, fieldOwner) &&
                            (IsScalar(fieldStorage) || IsObjectReference(fieldStorage) ||
                                IsSpan(fieldStorage) && ObjectLayouts.IsByRefLikeValue(objects, fieldOwner)) &&
                            ValueOperands.CheckInitialization(bundle, image, layout, heaps, owner, objects, context,
                                methodArguments, records[nextSlot + 1], fieldStorage) == 0)
                        {
                            // Keep the parent address live. The child address is consumed
                            // immediately and has no independent lifetime.
                            records[slot] = BranchIl.ClearInteriorField;
                            records[slot + 2] = records[nextSlot + 2];
                            records[nextSlot] = 0;
                            continue;
                        }
                        if (ObjectLayouts.IsByRefLikeValue(objects, FieldAccess.Owner(descriptor)) &&
                            FieldAccess.Storage(descriptor) == I32 && ValueOperands.LowerScopedIntField(records, slot, clauses))
                            continue;
                        if (ObjectLayouts.IsByRefLikeValue(objects, fieldOwner) && IsSpan(fieldStorage))
                        {
                            int span = SpanOperands.Bind(bundle, image, layout, heaps, owner, context, methodArguments,
                                records, slot, fieldStorage, objects, clauses, frame, hostUse, fieldCaller: callerRow);
                            if (span < 0) return span;
                            if (span != 0) continue;
                        }
                        if (ObjectLayouts.IsValueType(objects, fieldStorage) &&
                            next < records.Length / BranchIl.Width && records[next * BranchIl.Width] == BranchIl.GetField &&
                            !ValueOperands.Entry(records, next, clauses))
                        {
                            records[slot] = BranchIl.GetField;
                            continue;
                        }
                        bool valueOwner = ObjectLayouts.IsValueType(objects, fieldOwner);
                        bool classField = fieldStorage == I32 || fieldStorage == I64 || fieldStorage == Unsigned8 || fieldStorage == Native;
                        int classScalar = !valueOwner && classField && !ObjectLayouts.IsInterface(objects, fieldOwner)
                            ? fieldStorage : 0;
                        if (classScalar != 0 || valueOwner && !ObjectLayouts.IsExplicitValue(objects, fieldOwner) &&
                            !ObjectLayouts.IsByRefLikeValue(objects, fieldOwner))
                        {
                            int projected = ValueFieldProjections.Bind(bundle, objects, fieldOwner, field, classScalar);
                            if (projected != -2)
                            {
                                if (projected < 0) return projected;
                                records[slot] = BranchIl.TakeProjectedFieldAddress;
                                records[slot + 3] = FieldAccess.Encode(fieldOwner,
                                    IsValueLocation(projected) ? LocatedValue(projected) : projected);
                                continue;
                            }
                        }
                        if (pointerField && !ObjectLayouts.IsValueType(objects, fieldStorage))
                            continue; // &p->F of a primitive field: native address arithmetic
                        if (ManagedValueLocationWasm.UsesCell(objects, fieldOwner, fieldStorage))
                        {
                            int status = ValueOperands.LowerClassField(image, layout, heaps, bases[owner + 1] - bases[owner],
                                records, slot, FieldAccess.Storage(descriptor), clauses);
                            if (status < 0)
                            {
                                if (status != -2) return status;
                                bool readOnly = linked ? readOnlyAddress :
                                    !ValueOperands.IsWritableInteriorField(image, layout, heaps, field, false);
                                if (readOnly && !ReadOnlyAggregateCalls.ReadsFinalArgument(bundle, image, layout, heaps, owner,
                                    bases[owner + 1] - bases[owner], records, slot, clauses, fieldStorage, objects)) return status;
                            }
                            records[slot] = BranchIl.TakeValueFieldAddress;
                        }
                        else if (linked || !LinkExchange(bundle, image, heaps, layout, owner, objects, records, slot)) return -2;
                    }
                    continue;
                }
                if (kind == BranchIl.SizeOf)
                {
                    int size = ManagedSizeOf.Read(bundle, image, layout, heaps, owner,
                        records[slot + 1], context, objects, methodArguments);
                    if (size < 0) return size;
                    records[slot] = BranchIl.Constant;
                    records[slot + 1] = size;
                    continue;
                }
                if (kind == BranchIl.LoadStaticField || kind == BranchIl.StoreStaticField || kind == BranchIl.StaticFieldAddress)
                {
                    var symbol = new int[7];
                    int field = records[slot + 1];
                    bool addressOf = kind == BranchIl.StaticFieldAddress;
                    bool write = kind == BranchIl.StoreStaticField || addressOf;
                    bool linked = records[slot + 3] != 0;
                    if (!write && !linked && StaticByteIntrinsics.IsLittleEndian(image, layout, heaps, field))
                    {
                        records[slot] = BranchIl.Constant;
                        records[slot + 1] = 1; // Wasm native data is little-endian.
                        continue;
                    }
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
                    if (linked && callerType == 0) callerType = ObjectLayouts.Owner(image, heaps, layout, callerRow, false);
                    if (callerType < 0) return -1;
                    bool store = kind == BranchIl.StoreStaticField;
                    bool callerInitializer = store && MethodNames.IsInitializer(image, layout, heaps, callerRow);
                    int status = linked ? AssemblyBundle.ResolveField(bundle, image, layout, heaps, owner,
                        field, store, true, objects, globals, symbol, context, callerType, methodArguments, callerInitializer: callerInitializer) :
                        StaticFields.ResolveLocal(bundle, image, layout, heaps, owner, field, store, objects, globals, symbol, callerRow, context);
                    if (status < 0) return status;
                    int fieldContext = StaticFields.Context(globals, symbol[0]);
                    bool referenceCell = IsReferenceLocation(LocationFromIdentity(objects, symbol[1]));
                    bool readOnly = (symbol[6] & StaticFields.InitOnly) != 0;
                    bool valueCell = readOnly && fieldContext == 0 &&
                        ObjectLayouts.IsValueType(objects, symbol[1]) && !ObjectLayouts.IsByRefLikeValue(objects, symbol[1]);
                    if (addressOf && readOnly && !valueCell && !(!linked &&
                        StaticFields.IsDeclaringInitializer(image, layout, heaps, field, callerRow))) return -2;
                    // A reference-cell demand is keyed by FieldDef across retries.
                    // Each reference-compatible closed layout still gets its own global.
                    // Scalar and native storage retain their existing generic boundary.
                    if (addressOf && fieldContext != 0 && !referenceCell) return -2;
                    if (symbol[3] != 0)
                    {
                        // Bound declaring owner, so an inherited field initializes only its base.
                        var initializer = new int[1];
                        functions = initializers.Ensure(bundle, symbol[2], symbol[4], symbol[3], owners, rows, contexts,
                            bases, indices, arities, offsets, signatures, objects, globals, functions, initializer, fieldContext);
                        if (functions < 0) return functions;
                        initializers.SetFieldInitializer(symbol[0], initializer[0]);
                    }
                    int staticOwner = StaticFields.Owner(globals, symbol[0]);
                    int staticRow = StaticFields.Row(globals, symbol[0]);
                    int address = fieldContext == 0 || referenceCell ? addressedFields.Find(staticOwner, staticRow) : -1;
                    if (address == AddressedFields.ValueCell || addressOf && valueCell)
                    {
                        if (address == -1) addressedFields.AddValueCell(staticOwner, staticRow);
                        StaticFields.UseValueCell(globals, symbol[0], symbol[1], records, slot,
                            addressOf ? StaticReferenceWasm.Address : store ? StaticReferenceWasm.Write : StaticReferenceWasm.Read);
                        continue;
                    }
                    if (address == AddressedFields.Int32Cell || addressOf && symbol[5] == 8)
                    {
                        if (address == -1) addressedFields.AddInt32Cell(staticOwner, staticRow);
                        StaticFields.UseInt32Cell(globals, symbol[0]);
                        records[slot] = BranchIl.StaticInt32;
                        records[slot + 1] = symbol[0];
                        records[slot + 3] = addressOf ? StaticInt32Wasm.Address : write ? StaticInt32Wasm.Write : StaticInt32Wasm.Read;
                        continue;
                    }
                    if (address == AddressedFields.ReferenceCell || addressOf && referenceCell)
                    {
                        if (address == -1) addressedFields.AddReferenceCell(staticOwner, staticRow);
                        status = StaticFields.UseReferenceCell(globals, symbol[0], symbol[1], objects, records, slot,
                            addressOf ? StaticReferenceWasm.Address : write ? StaticReferenceWasm.Write : StaticReferenceWasm.Read);
                        if (status < 0) return status;
                        continue;
                    }
                    if (addressOf || address >= 0)
                    {
                        int plan = NativeAggregates.Plan(bundle, objects, symbol[1], nativePlans);
                        if (plan < 0) return plan;
                        if (address < 0) address = addressedFields.Add(staticOwner, staticRow, NativeAggregates.Size(nativePlans.Plans, plan));
                        if (address < 0) return address;
                        records[slot] = addressOf ? BranchIl.StaticFieldAddress : write ? BranchIl.StoreNativeStatic : BranchIl.LoadNativeStatic;
                        records[slot + BranchIl.Width + 1] = plan;
                        records[slot + BranchIl.Width + 2] = address;
                    }
                    records[slot + 1] = symbol[0];
                    records[slot + 3] = symbol[1];
                    continue;
                }
                if (kind == BranchIl.StringFromSpan)
                {
                    int empty = StringLiterals.Empty(globals);
                    if (empty < 0) return empty;
                    records[slot + 1] = empty;
                    continue;
                }
                if (kind == BranchIl.LoadString)
                {
                    int status = StringLiterals.Lower(image, heaps, records, slot, literals, literalSize, globals);
                    if (status < 0) return status;
                    continue;
                }
                // Validate direct MethodDef operands before framework recognizers inspect their rows.
                int target = records[slot + 1];
                if ((kind == BranchIl.Call || kind == BranchIl.NewObject || kind == BranchIl.LoadFunction) &&
                    (target < 1 || target > bases[owner + 1] - bases[owner])) return -1;
                if ((kind == BranchIl.NewObject && records[slot + 3] == 0 ||
                    kind == BranchIl.MemberCall && records[slot + 3] == 0x73) &&
                    CoreStringCreation.IsReadOnlySpanConstructor(image, layout, heaps, records[slot + 1], kind == BranchIl.MemberCall))
                {
                    int empty = StringLiterals.Empty(globals);
                    if (empty < 0) return empty;
                    records[slot] = BranchIl.StringFromSpan;
                    records[slot + 1] = empty;
                    records[slot + 3] = CharacterSpan;
                    continue;
                }
                if (kind == BranchIl.NewObject && records[slot + 3] == 0 ||
                    kind == BranchIl.MemberCall && records[slot + 3] == 0x73 && records[slot + BranchIl.Width + 1] == 0)
                {
                    int conversion = NativePointerMetadata.ConstructorOperation(image, heaps, layout, target, kind == BranchIl.MemberCall);
                    if (conversion != 0)
                    {
                        // A primitive native integer has scalar storage. The
                        // wide constructors still perform their original overflow check.
                        bool identity = conversion == NativePointerMetadata.IdentityConstructor;
                        records[slot] = identity ? BranchIl.NativePointerConstructor : BranchIl.CheckedConversion;
                        records[slot + 1] = conversion;
                        records[slot + 3] = identity ? 0 : I64;
                        continue;
                    }
                }
                if ((kind == BranchIl.Call || kind == BranchIl.MemberCall) && records[slot + 3] ==
                    (kind == BranchIl.Call ? 0 : 0x28) && records[slot + BranchIl.Width + 1] == 0)
                {
                    int clear = ArrayClearMetadata.Arity(image, layout, heaps, records[slot + 1], kind == BranchIl.MemberCall);
                    if (clear != 0)
                    {
                        records[slot] = BranchIl.ArrayClear;
                        records[slot + 1] = clear;
                        records[slot + 3] = 0;
                        continue;
                    }
                    int arity = ArrayCopyMetadata.Arity(image, layout, heaps, records[slot + 1], kind == BranchIl.MemberCall);
                    if (arity != 0)
                    {
                        records[slot] = BranchIl.ArrayCopy;
                        records[slot + 1] = arity;
                        records[slot + 3] = arity == 5 && ArrayCopyMetadata.ChecksIndicesBeforeRanges(bundle) ? 1 : 0;
                        continue;
                    }
                }
                if ((kind == BranchIl.Call || kind == BranchIl.MemberCall) && records[slot + 3] ==
                    (kind == BranchIl.Call ? 0 : 0x28) && records[slot + BranchIl.Width + 1] == 0)
                {
                    int monitor = MonitorMetadata.Identify(image, heaps, layout, records[slot + 1], kind == BranchIl.MemberCall);
                    if (monitor == MonitorMetadata.Enter || monitor == MonitorMetadata.Exit)
                    {
                        records[slot] = BranchIl.Monitor;
                        records[slot + 1] = monitor == MonitorMetadata.Exit ? -1 : 0;
                        hostUse[0] = hostUse[0] | WasmImports.Monitor;
                        continue;
                    }
                    int arity = SpanMetadata.StringAsSpanArity(image, layout, heaps, records[slot + 1], kind == BranchIl.MemberCall);
                    if (arity != 0)
                    {
                        records[slot] = BranchIl.StringAsSpan;
                        records[slot + 1] = arity - 1;
                        continue;
                    }
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
                        int equality = opcode == 0x28 ? SpanMetadata.CharacterEquality(bundle, image, layout, heaps,
                            owner, member, context, methodArguments, objects) : 0;
                        if (equality != 0)
                        {
                            records[slot] = BranchIl.EqualCharacterSpans;
                            records[slot + 1] = equality < 0 ? 1 : 0;
                            records[slot + 3] = equality < 0 ? -equality : equality;
                            continue;
                        }
                        int span = SpanMetadata.ClassifySpanCall(bundle, image, layout, heaps,
                            owner, member, context, methodArguments, objects, opcode == 0x73);
                        if (span == BranchIl.NativeCharacterSpan && opcode == 0x73)
                        {
                            records[slot] = span;
                            records[slot + 3] = 0;
                            hostUse[0] = hostUse[0] | WasmImports.NativeCharacterViews;
                            continue;
                        }
                        if (span == BranchIl.WritableByteArraySpan && opcode == 0x73)
                        {
                            records[slot] = span;
                            records[slot + 1] = 2; // start and count following the owner array
                            records[slot + 3] = 0;
                            continue;
                        }
                        if (span == BranchIl.CharacterArrayAsSpan ||
                            (span == BranchIl.SpanAsReadOnly || span == BranchIl.ByteSpanAsReadOnly) && opcode == 0x28 ||
                            span == BranchIl.ByteArrayAsSpan)
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
                    if (opcode == 0x28 && !addressed &&
                        RuntimeInitializerMetadata.IsRunClassConstructor(image, layout, heaps, member, false)) return -2;
                    int objectTypeMember = addressed ? 0 : RuntimeTypeMetadata.ObjectTypeMember(image, heaps, layout, member);
                    if (objectTypeMember == 1 && (opcode == 0x28 || opcode == 0x6f))
                    {
                        records[slot] = BranchIl.GetObjectType;
                        continue;
                    }
                    if (objectTypeMember >= 2 && opcode == 0x28)
                    {
                        records[slot] = BranchIl.EqualRuntimeDescriptors;
                        records[slot + 1] = RuntimeTypeMetadata.Type;
                        records[slot + 3] = objectTypeMember == 3 ? 1 : 0;
                        continue;
                    }
                    int marshalError = !addressed && opcode == 0x28
                        ? MarshalErrorMetadata.Identify(image, heaps, layout, member) : 0;
                    if (marshalError != 0)
                    {
                        records[slot] = BranchIl.Win32Error;
                        records[slot + 1] = marshalError;
                        hostUse[0] = hostUse[0] | WasmImports.Win32Errors;
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
                        if (!ReflectionMembers.IsTypeText(reflectionMember))
                        {
                            int status = reflection.Bind(bundle, globals, literals, literalSize);
                            if (status < 0) return status;
                        }
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
                    int spanCallback = addressed ? 0 : CharacterSpanCallbackMetadata.Member(bundle, image, layout, heaps,
                        owner, member, context, methodArguments, objects);
                    if (spanCallback == CharacterSpanCallbackMetadata.Invoke && opcode == 0x6f)
                    {
                        records[slot] = BranchIl.InvokeDelegate;
                        records[slot + 1] = CharacterSpanCallback;
                        continue;
                    }
                    if (spanCallback == CharacterSpanCallbackMetadata.Construct && opcode == 0x73)
                        constructorType = CharacterSpanCallback;
                    // Stage 204 (decision 0091): System.Func`n / Action`n with a delegate key.
                    var generalKey = new int[1];
                    int generalMember = addressed ? 0 : GeneralDelegates.Member(bundle, image, layout, heaps,
                        owner, member, context, methodArguments, objects, generalKey);
                    if (generalMember == GeneralDelegates.Invoke && opcode == 0x6f)
                    {
                        functions = GeneralDelegates.RegisterInvokeHelper(objects, generalKey[0], owner, owners, rows, contexts,
                            arities, offsets, signatures, functions);
                        if (functions < 0) return functions;
                        records[slot] = BranchIl.Call;
                        records[slot + 1] = DelegateShapes.InvokeHelper(objects, generalKey[0]);
                        records[slot + 3] = 1; // callvirt null check: a null delegate raises NullReferenceException
                        continue;
                    }
                    if (generalMember == GeneralDelegates.Construct && opcode == 0x73)
                        constructorType = generalKey[0];
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
                    if (consoleMember == ConsoleMetadata.ReadLine)
                    {
                        // A static call; the host supplies the line (decision 0079).
                        if (opcode != 0x28) return -2;
                        records[slot] = BranchIl.ConsoleReadLine;
                        hostUse[0] = hostUse[0] | WasmImports.ConsoleInput;
                        continue;
                    }
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
                        var symbol = new int[8];
                        bool baseCall = opcode == 0x28 && callerReceiver >= ObjectLayouts.FirstClass &&
                            ObjectLayouts.IsConstructor(image, heaps, layout, callerRow);
                        if (callerType == 0) callerType = ObjectLayouts.Owner(image, heaps, layout, callerRow, false);
                        if (callerType < 0) return -1;
                        int resolved = AssemblyBundle.Resolve(bundle, image, layout, heaps, owner, member, opcode, baseCall, objects, symbol,
                            context, callerType, allowVirtual: !addressed, valueAddress: addressed, callerMethodArguments: methodArguments,
                            callerReceiver: addressed ? 0 : callerReceiver);
                        if (resolved < 0) return resolved;
                        int targetOwner = symbol[0], targetRow = symbol[1], targetContext = symbol[3];
                        if (opcode == 0x28 && finalizers != null && finalizers.IsSuppression(targetOwner, targetRow))
                        {
                            if (!tracksFinalizers) return Reemit;
                            records[slot] = BranchIl.SuppressFinalizer;
                            continue;
                        }
                        int primitiveReceiver = symbol[7];
                        if (primitiveReceiver != 0 && PrimitiveReceivers.IsNominalContext(objects, context, targetOwner, symbol[6]))
                            primitiveReceiver = 0;
                        if (address < 0) targetContext = ~targetContext;
                        else if (opcode == 0x28 && !addressed && symbol[6] != 0 && primitiveReceiver == 0)
                        {
                            int receiver = targetContext != 0 ? targetContext : ObjectLayouts.Find(objects, targetOwner, symbol[6], new int[0]);
                            if (UsesLocationReceiver(objects, receiver)) targetContext = ~targetContext;
                        }
                        int linked = Lookup(owners, rows, contexts, bases, indices, functions, targetOwner, targetRow, targetContext);
                        bool virtualMethod = (symbol[4] & 0x40) != 0;
                        // A final implementation in a closed class uses the closed method body.
                        // Keep callvirt's null check even when dispatch cannot select another body.
                        bool finalClosedMethod = targetContext > 0 && (symbol[4] & 0x20) != 0;
                        bool deferred = opcode == 0x6f && virtualMethod && !finalClosedMethod;
                        bool valueKindCandidate = !addressed && (opcode == 0x28 || opcode == 0x6f) &&
                            RuntimeTypeMetadata.IsValueKindCandidate(symbol[4]);
                        if (deferred || valueKindCandidate || linked < 0)
                        {
                            byte[] targetImage = bundle.Get(targetOwner);
                            var targetLayout = new int[4];
                            var targetHeaps = new int[8];
                            if (CompressedTables.FindWithHeaps(targetImage, targetLayout, targetHeaps) < 0) return -1;
                            if (deferred || valueKindCandidate &&
                                RuntimeTypeMetadata.CoreTypeDescriptorOperation(targetImage, targetHeaps, targetLayout, targetRow) ==
                                    RuntimeTypeMetadata.ValueKind)
                            {
                                int staged = StageVirtualCall(bundle, targetImage, targetLayout, targetHeaps,
                                    targetOwner, targetRow, objects, records, slot, virtualTypes, virtualUsed, typeCalls, targetContext);
                                if (staged < 0) return staged;
                                continue;
                            }
                            if (linked < 0)
                            {
                                functions = Register(bundle, targetImage, targetLayout, targetHeaps, targetOwner, targetRow,
                                    targetContext, owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions,
                                    allowVirtual: virtualMethod, receiverOverride: primitiveReceiver);
                                if (functions < 0) return functions;
                                linked = functions - 1;
                            }
                        }
                        if (!IsSharedBundleSignature(signatures, offsets[linked], arities[linked]) ||
                            primitiveReceiver != 0 && signatures[offsets[linked] + 1] != primitiveReceiver) return -2;
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
                        // Bit 0 is the callvirt null check. The upper bits carry the
                        // derived type required on the stack for protected access.
                        records[slot + 3] = (symbol[5] << 1) | (opcode == 0x6f ? 1 : 0);
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
                if (kind != BranchIl.LoadFunction)
                {
                    // Stage 204 (decision 0091): .ctor and Invoke of a delegate TypeDef in this image.
                    var definedKey = new int[1];
                    int defined = GeneralDelegates.DefinedMember(bundle, image, layout, heaps, owner, target, objects, definedKey);
                    if (defined < 0) return defined;
                    if (defined == GeneralDelegates.Invoke)
                    {
                        if (kind != BranchIl.Call || records[slot + 3] == 0 || records[slot + BranchIl.Width + 1] != 0)
                            return -2; // Invoke without callvirt or through an address: no lowering
                        functions = GeneralDelegates.RegisterInvokeHelper(objects, definedKey[0], owner, owners, rows, contexts,
                            arities, offsets, signatures, functions);
                        if (functions < 0) return functions;
                        records[slot + 1] = DelegateShapes.InvokeHelper(objects, definedKey[0]);
                        records[slot + 3] = 1; // callvirt null check: a null delegate raises NullReferenceException
                        continue;
                    }
                    if (defined == GeneralDelegates.Construct)
                    {
                        // ldftn (or ldvirtftn's absence: the pointer must be an ldftn) then newobj, as for
                        // the platform delegates; the ldftn linking checked the target's signature.
                        int pointer = slot - 6 * BranchIl.Width;
                        if (kind != BranchIl.NewObject || pointer < 0 || records[pointer] != BranchIl.LoadFunction ||
                            records[pointer + 2] != slot / BranchIl.Width)
                            return -2;
                        records[slot] = BranchIl.NewDelegate;
                        records[slot + 1] = definedKey[0];
                        records[slot + 3] = records[pointer + 3];
                        continue;
                    }
                }
                // Issue #304: ldstr "key"; call SR.GetResourceString(string) in CoreLib folds to the
                // neutral resource string, so SR properties compile without ResourceManager.
                if (kind == BranchIl.Call && records[slot + 3] == 0)
                {
                    int folded = ResourceStringFolding.FoldResourceCall(image, heaps, layout, records, slot, clauses, literals, literalSize);
                    if (folded < 0) return folded;
                    if (folded > 0) continue;
                    // Stage 181 (issue #256): CoreLib's own event accessors call Delegate.Combine/Remove
                    // by MethodDef and cast the result to EventHandler. Lower exactly that shape to the
                    // EventHandler composition records; any other Combine keeps its original body.
                    int composition = CoreLibDelegateDefinitions.CombineMember(image, heaps, layout, records[slot + 1]);
                    if (composition != 0)
                    {
                        int cast = records[slot + 2] * BranchIl.Width;
                        if (cast < records.Length && records[cast] == BranchIl.CastClass &&
                            !ValueOperands.Entry(records, cast / BranchIl.Width, clauses) &&
                            CoreLibDelegateDefinitions.IsEventHandlerTypeDef(image, heaps, layout, records[cast + 1]))
                        {
                            records[slot] = composition == EventMetadata.CombineHandler ? BranchIl.CombineHandler : BranchIl.RemoveHandler;
                            continue;
                        }
                    }
                }
                if (kind == BranchIl.Call)
                {
                    if (records[slot + 3] != 0 && ReflectionMembers.IsCoreAssemblyEqualityCall(image, heaps, layout, callerRow, target))
                    {
                        // Keep the operator's original reference and null branches. Only
                        // its Equals call on canonical descriptors needs runtime semantics.
                        records[slot] = BranchIl.EqualRuntimeDescriptors;
                        records[slot + 1] = RuntimeTypeMetadata.Assembly;
                        records[slot + 3] = 0;
                        continue;
                    }
                    if (arrayEnumerator && records[slot + 3] == 0 && SzArrayEnumeration.IsArrayLength(image, layout, heaps, target))
                    {
                        records[slot] = BranchIl.ArrayLength;
                        records[slot + 1] = I32; // Array.Length returns int, unlike ldlen.
                        continue;
                    }
                    if (records[slot + 3] == 0 && finalizers != null && finalizers.IsSuppression(owner, target))
                    {
                        if (!tracksFinalizers) return Reemit;
                        records[slot] = BranchIl.SuppressFinalizer;
                        continue;
                    }
                    if (records[slot + 3] == 0 && ReflectionMembers.CoreEntryAssemblyMember(image, heaps, layout, target) ==
                        ReflectionMembers.MonoEntryLeaf)
                    {
                        int token = PeLocator.ReadManagedEntryToken(bundle.Get(0));
                        if (token < 0 || (token & 0xffffff) > bases[1])
                        {
                            records[slot] = BranchIl.NullLiteral;
                            continue;
                        }
                        int status = reflection.Bind(bundle, globals, literals, literalSize);
                        if (status < 0) return status;
                        records[slot] = BranchIl.LoadStaticField;
                        records[slot + 1] = reflection.Slots[RuntimeReflectionPlan.Width + RuntimeReflectionPlan.Assembly];
                        records[slot + 3] = RuntimeTypeMetadata.Assembly;
                        continue;
                    }
                    if (records[slot + 3] == 0 && RuntimeTypeMetadata.IsCoreGetTypeFromHandle(image, heaps, layout, target))
                    {
                        records[slot] = BranchIl.TypeFromHandle;
                        continue;
                    }
                    if (records[slot + 3] == 0 &&
                        RuntimeInitializerMetadata.IsRunClassConstructor(image, layout, heaps, target, true)) return -2;
                    if (RuntimeTypeMetadata.IsCoreObjectGetType(image, heaps, layout, target))
                    {
                        records[slot] = BranchIl.GetObjectType;
                        continue;
                    }
                    if (records[slot + 3] != 0 && ReflectionMembers.IsCoreTypeAssemblyGetter(image, heaps, layout, target))
                    {
                        // CoreLib's virtual Type.Assembly uses the same descriptor
                        // as external callers that refer to this declaration.
                        int status = reflection.Bind(bundle, globals, literals, literalSize);
                        if (status < 0) return status;
                        records[slot] = BranchIl.ReadReflection;
                        records[slot + 1] = ReflectionMembers.TypeAssembly;
                        continue;
                    }
                    int coreTypeText = records[slot + 3] != 0 ? ReflectionMembers.CoreTypeTextGetter(image, heaps, layout, target) : 0;
                    if (coreTypeText != 0)
                    {
                        records[slot] = BranchIl.ReadReflection;
                        records[slot + 1] = coreTypeText;
                        continue;
                    }
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
                int targetFlags = MethodDefinitions.ReadColumn(image, layout, target, 2);
                bool virtualMethodLocal = (targetFlags & 0x40) != 0;
                if (virtualMethodLocal && kind == BranchIl.LoadFunction) return -2;
                if (kind == BranchIl.Call && (virtualMethodLocal && records[slot + 3] != 0 ||
                    RuntimeTypeMetadata.IsValueKindCandidate(targetFlags) &&
                    RuntimeTypeMetadata.CoreTypeDescriptorOperation(image, heaps, layout, target) == RuntimeTypeMetadata.ValueKind))
                {
                    int staged = StageVirtualCall(bundle, image, layout, heaps, owner, target,
                        objects, records, slot, virtualTypes, virtualUsed, typeCalls);
                    if (staged < 0) return staged;
                    continue;
                }
                int fieldReceiver = kind == BranchIl.Call ? records[slot + BranchIl.Width + 1] : 0;
                int key = fieldReceiver < 0 ? -1 : 0, localPrimitiveReceiver = 0;
                if (kind == BranchIl.Call && fieldReceiver == 0 && records[slot + 3] == 0 &&
                    (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x10) == 0 &&
                    !ObjectLayouts.IsConstructor(image, heaps, layout, target))
                {
                    int declaring = ObjectLayouts.Owner(image, heaps, layout, target, false);
                    if (declaring < 1) return -1;
                    if (!PrimitiveReceivers.IsNominalContext(objects, context, owner, declaring))
                        localPrimitiveReceiver = PrimitiveReceivers.DirectInt32(image, layout, heaps, declaring, target);
                    if (localPrimitiveReceiver == 0)
                    {
                        int receiver = ObjectLayouts.Find(objects, owner, declaring, new int[0]);
                        // A pointer can reach a value method before any managed
                        // use registers the struct. Leave reference receiver
                        // selection to MethodShape, including CoreLib's root.
                        if (receiver == 0 && !virtualMethodLocal)
                            receiver = BundleLayouts.RegisterValue(bundle, image, layout, heaps, owner, declaring, objects);
                        // An unrewritten IL receiver of a value method is a byref.
                        // Address scans that substituted a snapshot mark the call separately.
                        if (UsesLocationReceiver(objects, receiver)) key = -1;
                    }
                }
                int index = Lookup(owners, rows, contexts, bases, indices, functions, owner, target, key);
                if (index < 0)
                {
                    functions = Register(bundle, image, layout, heaps, owner, target, key, owners, rows, contexts, bases, indices,
                        arities, offsets, signatures, objects, functions, allowVirtual: virtualMethodLocal && kind == BranchIl.Call,
                        receiverOverride: localPrimitiveReceiver);
                    if (functions < 0) return functions;
                    index = functions - 1;
                }
                if (fieldReceiver < 0)
                {
                    if (!BindLocationCall(records, slot, index, -fieldReceiver, arities, offsets, signatures)) return -2;
                    continue;
                }
                int signature = offsets[index];
                if (localPrimitiveReceiver != 0 && signatures[signature + 1] != localPrimitiveReceiver) return -2;
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
                    // Decision 0078: an ldftn that feeds no delegate constructor takes a
                    // static method's address as a table-2 slot. Instance pointers stay diagnosed.
                    // Only the delegate constructor shape counts: an ordinary constructor can take
                    // a function pointer straight from ldftn (Codex review of c6b68c7).
                    int following = records[slot + 2] * BranchIl.Width;
                    if (!FeedsDelegateConstructor(bundle, image, heaps, layout, owner, records, following))
                    {
                        if ((MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x10) == 0) return -2;
                        records[slot] = BranchIl.FunctionAddress;
                        records[slot + 1] = pointers.Slot(index);
                        continue;
                    }
                    bool instance = (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x10) == 0;
                    int next = records[slot + 2] * BranchIl.Width;
                    var generalKey = new int[1];
                    // Stage 204: newobj of a delegate TypeDef's runtime .ctor (a MethodDef record).
                    bool defined = next < records.Length && records[next] == BranchIl.NewObject &&
                        GeneralDelegates.DefinedMember(bundle, image, layout, heaps, owner, records[next + 1], objects,
                            generalKey) == GeneralDelegates.Construct;
                    if (!defined && (next >= records.Length || records[next] != BranchIl.MemberCall || records[next + 3] != 0x73))
                        return -2;
                    bool spanCallback = !defined && CharacterSpanCallbackMetadata.Member(bundle, image, layout, heaps, owner,
                        records[next + 1], context, methodArguments, objects) == CharacterSpanCallbackMetadata.Construct;
                    bool general = defined || !spanCallback && GeneralDelegates.Member(bundle, image, layout, heaps, owner,
                        records[next + 1], context, methodArguments, objects, generalKey) == GeneralDelegates.Construct;
                    if (general)
                    {
                        if (instance && !hasReceiver ||
                            !GeneralDelegates.MatchesTarget(objects, generalKey[0], signatures, signature, arities[index], instance))
                            return -2;
                    }
                    else if (spanCallback)
                    {
                        if (instance && !hasReceiver ||
                            !CharacterSpanCallbackMetadata.MatchesTarget(signatures, signature, arities[index], instance))
                            return -2;
                    }
                    else if (EventMetadata.Member(image, heaps, layout, records[next + 1]) == EventMetadata.ConstructHandler)
                    {
                        int first = signature + (instance ? 2 : 1);
                        if (instance && !hasReceiver || arities[index] != (instance ? 3 : 2) ||
                            signatures[signature] != Void || signatures[first] != ObjectReference ||
                            signatures[first + 1] != EventArgsReference)
                            return -2;
                    }
                    else if (instance && !hasReceiver || arities[index] != (instance ? 1 : 0) || signatures[signature] != I32)
                        return -2;
                    int thunk = 0;
                    int thunkKey = general ? (instance ? generalKey[0] : -generalKey[0]) : 0;
                    while (thunk < thunks[0] && (thunks[thunk + 1] != index || GeneralDelegates.ThunkKey(thunks, thunk) != thunkKey))
                        thunk++;
                    if (thunk == thunks[0])
                    {
                        if (thunk + 1 >= thunks.Length / 2) return -3;
                        thunks[thunk + 1] = index;
                        thunks[0] = thunk + 1;
                        GeneralDelegates.SetThunkKey(thunks, thunk, general ? generalKey[0] : 0, !instance);
                    }
                    records[slot + 1] = thunk;
                    records[slot + 3] = instance ? signatures[signature + 1] : NullValue;
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
                    // The canonical String receiver has a GC representation
                    // even though it is below the nominal class type range.
                    if ((!hasReceiver && (arities[index] == 0 || signatures[signature + 1] != String)) ||
                        (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x10) != 0)
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

        private static int PrimitiveArrayAccess(int element, bool load)
        {
            // Reuse the normal array instructions after substituting !0 or !!0,
            // preserving the sign extension selected by the CLI element type.
            switch (ArrayTypeRefs.Storage(element))
            {
                case Bytes: return load ? element == 4 ? 0x90 : 0x91 : 0x9c;
                case Chars: return load ? element == 6 ? 0x92 : 0x93 : 0x9d;
                case Ints: return load ? 0x94 : 0x9e;
                case Longs: return load ? 0x96 : 0x9f;
                case NativeVector: return load ? 0x97 : 0x9b;
                case Singles: return load ? 0x98 : 0xa0;
                case Doubles: return load ? 0x99 : 0xa1;
                default: return 0;
            }
        }

        private static void RecordScopedNativeField(BundleImages bundle, int[] objects, int[] records, int slot,
            int type, int field)
        {
            if (records.Length <= slot + 2 * BranchIl.Width + 3) return;
            var native = new int[2];
            int owner = ObjectLayouts.AssemblyOf(objects, type);
            int definition = ObjectLayouts.TypeDef(objects, type);
            if (NativeValueLayouts.Field(bundle, owner, definition, field, native, scopedFrame: true) != 0) return;
            records[slot + 2 * BranchIl.Width + 2] = native[0] + 1;
            records[slot + 2 * BranchIl.Width + 3] = native[1];
        }

        private static bool UsesLocationReceiver(int[] objects, int receiver) =>
            ObjectLayouts.IsValueType(objects, receiver) && !ObjectLayouts.IsExplicitValue(objects, receiver);

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
            int[] types, int[] used, RuntimeTypeCalls typeCalls, int closed = 0)
        {
            int declaring = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (declaring < 1) return -1;
            int receiver = closed != 0 ? closed : ObjectLayouts.IsCoreObjectType(image, heaps, layout, declaring << 2) ? ObjectReference :
                PlatformTypeReferences.IsString(image, heaps, layout, declaring << 2) ? String :
                BundleLayouts.Register(bundle, image, layout, heaps, owner, declaring, objects);
            if (receiver < 0) return receiver;
            if (ObjectLayouts.IsValueType(objects, receiver)) return -2;
            int shape = used[0];
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int arity = SignatureTypes.Read(bundle, image, heaps, blob, types, shape,
                receiver, layout, objects, owner, closed);
            if (arity < 1) return arity < 0 ? arity : -1;
            int operation = arity == 1 ? RuntimeTypeMetadata.CoreTypeDescriptorOperation(image, heaps, layout, row) : 0;
            if (operation != RuntimeTypeMetadata.GenericDefinition && RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, types[shape]))
                types[shape] = RuntimeTypeMetadata.Type;
            typeCalls.ApplyVirtualArguments(bundle, owner, row, types, shape, objects);
            if (!IsSharedBundleSignature(types, shape, arity)) return -2;
            used[0] = shape + arity + 1;
            records[slot] = BranchIl.VirtualCall;
            records[slot + 1] = shape;
            records[slot + 3] = arity;
            // The following four IL bytes are the token operand. Their
            // record kinds remain zero, so no control-flow edge enters them.
            records[slot + BranchIl.Width + 1] = owner;
            if (operation == RuntimeTypeMetadata.GenericKind)
                records[slot + BranchIl.Width + 2] = BranchIl.RuntimeTypeIsGeneric;
            if (operation == RuntimeTypeMetadata.GenericDefinition)
                records[slot + BranchIl.Width + 2] = BranchIl.RuntimeTypeGenericDefinition;
            if (operation == RuntimeTypeMetadata.EnumKind)
                records[slot + BranchIl.Width + 2] = BranchIl.RuntimeTypeIsEnum;
            if (operation == RuntimeTypeMetadata.ValueKind)
                records[slot + BranchIl.Width + 2] = BranchIl.RuntimeTypeIsValue;
            if (operation == RuntimeTypeMetadata.GenericArguments)
            {
                int vector = RegisterArray(objects, RuntimeTypeMetadata.Type);
                if (vector < 0) return vector;
                records[slot + BranchIl.Width + 2] = BranchIl.RuntimeTypeGenericArguments;
                records[slot + 2 * BranchIl.Width + 1] = vector;
                records[slot + 2 * BranchIl.Width + 2] = types[shape];
            }
            records[slot + BranchIl.Width + 3] = row;
            return 0;
        }

        private static int LinkVirtualCalls(BundleImages bundle, int[] records,
            int[] depths, int[] states, byte[] allocatedStates, int maximum, int[] types, int[] owners, int[] rows, int[] contexts, int[] bases,
            int[] indices, int[] arities, int[] offsets, int[] signatures, int[] objects, int functions, int[] location,
            VirtualDispatch dispatch, bool externalObjects, int callerOwner, int callerRow, MethodInstantiations instantiations,
            RuntimeReflectionPlan reflection)
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
                int operation = records[slot + BranchIl.Width + 2];
                if ((operation == BranchIl.RuntimeTypeIsGeneric || operation == BranchIl.RuntimeTypeGenericDefinition ||
                    operation == BranchIl.RuntimeTypeGenericArguments || operation == BranchIl.RuntimeTypeIsEnum || operation == BranchIl.RuntimeTypeIsValue) &&
                    RuntimeTypeGenericKind.IsDescriptorReceiver(actual))
                {
                    // Select the descriptor path only after dataflow has seen
                    // every predecessor. Nominal receivers keep virtual dispatch.
                    if (operation == BranchIl.RuntimeTypeGenericDefinition)
                    {
                        functions = reflection.GenericDefinitions.BindFailure(bundle, owner, owners, rows, contexts,
                            bases, indices, arities, offsets, signatures, objects, functions);
                        if (functions < 0) return functions;
                        dispatch.AddAllocation(reflection.GenericDefinitions.ExceptionType);
                    }
                    if (operation == BranchIl.RuntimeTypeGenericArguments)
                    {
                        int vector = records[slot + 2 * BranchIl.Width + 1];
                        int result = reflection.GenericArguments.Bind(owner, vector);
                        if (result < 0) return result;
                        records[slot + 1] = vector;
                    }
                    records[slot] = operation;
                    continue;
                }
                if (actual == RuntimeTypeMetadata.Type && declared == ObjectReference && arity == 1 && types[shape] == String)
                {
                    byte[] source = bundle.Get(owner);
                    var layout = new int[4];
                    var heaps = new int[8];
                    if (CompressedTables.FindWithHeaps(source, layout, heaps) < 0) return -1;
                    if (RuntimeTypeMetadata.IsCoreObjectToString(source, heaps, layout, row))
                    {
                        records[slot] = BranchIl.RuntimeTypeDisplayName;
                        continue;
                    }
                }
                bool allocated = allocatedStates[receiver] != 0;
                int function;
                if (operation == BranchIl.RuntimeTypeIsValue)
                {
                    // A nominal receiver still executes the original wrapper.
                    // Its protected virtual call selects a user-defined override.
                    function = Lookup(owners, rows, contexts, bases, indices, functions, owner, row, 0);
                    if (function < 0)
                    {
                        byte[] source = bundle.Get(owner);
                        var layout = new int[4];
                        var heaps = new int[8];
                        if (CompressedTables.FindWithHeaps(source, layout, heaps) < 0) return -1;
                        int status = Register(bundle, source, layout, heaps, owner, row, 0,
                            owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions);
                        if (status < 0) return status;
                        function = functions;
                    }
                    if (!VirtualSignaturesMatch(arities, offsets, signatures, function, types, shape, arity, actual, objects))
                        return -2;
                }
                else if (allocated || bound == String || ObjectLayouts.IsSealed(objects, bound))
                {
                    function = BindVirtualBody(bundle, objects, actual, allocated, declared, owner, row,
                        owners, rows, contexts, bases, indices, arities, offsets, signatures, functions, instantiations);
                    if (function < 0) return function;
                    int signatureStatus = dispatch.BindShape(bundle, objects, types, shape, arity, signatures, offsets,
                        owners, rows, contexts, function);
                    if (signatureStatus < 0) return signatureStatus;
                    if (!VirtualSignaturesMatch(arities, offsets, signatures, function, types, shape, arity, actual, objects)) return -2;
                }
                else
                {
                    if (externalObjects || ObjectLayouts.IsValueType(objects, bound) ||
                        !Assignable(bound, declared, objects)) return -2;
                    if (bound < ObjectLayouts.FirstClass)
                    {
                        if (bound != ObjectReference || declared != ObjectReference || arity != 1 || types[shape] != String)
                            return -2;
                        byte[] source = bundle.Get(owner);
                        var layout = new int[4];
                        var heaps = new int[8];
                        if (CompressedTables.FindWithHeaps(source, layout, heaps) < 0) return -1;
                        if (!RuntimeTypeMetadata.IsCoreObjectToString(source, heaps, layout, row)) return -2;
                    }
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
            int[] indices, int[] arities, int[] offsets, int[] signatures, int functions, MethodInstantiations instantiations = null)
        {
            if (IsExactArrayEnumerable(objects, actual, declared))
                return SzArrayEnumeration.Bind(bundle, objects, actual, declared, owner, row,
                    owners, rows, contexts, bases, indices, arities, offsets, signatures, functions, instantiations);
            // The array is a valid interface argument even when this profile
            // cannot yet bind a VM-supplied mutable or readonly collection method.
            if (IsExactArrayList(objects, actual, declared)) return -2;
            var symbol = new int[3];
            int status = VirtualSlots.SelectVirtualBody(bundle, objects, actual, allocated, declared, owner, row, symbol);
            if (status < 0) return status;
            owner = symbol[0];
            row = symbol[1];
            int context = symbol[2];
            int function = Lookup(owners, rows, contexts, bases, indices, functions, owner, row, context);
            if (function >= 0) return function;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x400) != 0) return -2;
            status = Register(bundle, image, layout, heaps, owner, row, context, owners, rows, contexts, bases,
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
            type = ExchangeLeaf(objects, type);
            if (type >= ObjectLayouts.FirstClass) return !ObjectLayouts.IsValueType(objects, type);
            return type == ObjectLayouts.ObjectReference || type == Delegate || type == EventHandlerReference;
        }

        private static int ExchangeLeaf(int[] objects, int type)
        {
            while (type >= ObjectLayouts.FirstClass)
            {
                int element = ObjectLayouts.ArrayElement(objects, type);
                if (element == 0) break;
                type = element;
            }
            return type;
        }

        // A managed address consumed by ldobj or stobj (for example `this = new S(...)`, #271)
        // is diagnosed at that instruction, which is where it was rejected before Stage 145
        // decoded them. initobj keeps its diagnostic at the address load.
        private static int ManagedObjectCopy(int[] records, int slot, int[] location, int status)
        {
            int next = records[slot + 2] * BranchIl.Width;
            for (int step = 0; step < 16 && next < records.Length; step++)
            {
                int kind = records[next];
                if (kind == BranchIl.LoadObject || kind == BranchIl.StoreObject)
                {
                    if (location.Length != 0) location[2] = next / BranchIl.Width;
                    return status;
                }
                if (kind == BranchIl.Return || kind == BranchIl.Branch || kind == BranchIl.Conditional) break;
                next = records[next + 2] * BranchIl.Width;
            }
            return status;
        }

        // Decides whether an adjacent newobj builds a delegate from this ldftn (decision 0078).
        // The signature alone is not enough: an ordinary constructor may also take (object, nint)
        // (Codex review of ee7556a). A delegate is identified by its declaring type. Only
        // delegates have runtime-implemented methods (ECMA-335 II.14.6); an ordinary
        // (object, nint) constructor has an IL body. The declaring type is:
        // - a platform delegate MemberRef, as the delegate path itself recognizes them;
        // - a local MethodDef, whose own implementation flags say Runtime;
        // - a MemberRef to a TypeRef in another bundle assembly, resolved there and checked
        //   for any runtime-implemented method. A generic (TypeSpec) parent with the delegate
        //   shape keeps the delegate path and its existing diagnostic.
        private static bool FeedsDelegateConstructor(BundleImages bundle, byte[] image, int[] heaps, int[] layout,
            int owner, int[] records, int following)
        {
            if (following >= records.Length) return false;
            int kind = records[following], row = records[following + 1];
            if (kind == BranchIl.NewObject)
                return (MethodDefinitions.ReadColumn(image, layout, row, 1) & 3) == 3; // CodeTypeMask: Runtime
            if (kind != BranchIl.MemberCall || records[following + 3] != 0x73) return false;
            if (DelegateMetadata.Member(image, heaps, layout, row) == DelegateMetadata.Construct ||
                EventMetadata.Member(image, heaps, layout, row) == EventMetadata.ConstructHandler)
                return true;
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member, allowClosedParent: true) < 0 ||
                !HasDelegateConstructorShape(image, heaps, member[5]))
                return false;
            int parent = AssemblyReferences.MemberParent(image, heaps, layout, row);
            if (parent < 0 || (parent & 7) != 1 || bundle.Length == 0) return true;
            var identity = new int[2];
            if (AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, (parent >> 3) << 2 | 1, identity) < 0 ||
                identity[1] <= 0)
                return true; // unresolved: keep the delegate path's diagnostic
            byte[] target = bundle.Get(identity[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return true;
            var range = new int[2];
            if (ObjectLayouts.MethodRange(target, targetHeaps, targetLayout, identity[1], range) < 0) return true;
            for (int method = range[0]; method < range[1]; method++)
                if ((MethodDefinitions.ReadColumn(target, targetLayout, method, 1) & 3) == 3) return true;
            return false;
        }

        // `instance void .ctor(object, native int)` (ECMA-335 II.14.6.1).
        private static bool HasDelegateConstructorShape(byte[] image, int[] heaps, int blob)
        {
            if (blob <= 0 || blob >= heaps[1]) return false;
            var next = new int[1];
            int size = ImageBytes.CompressedUnsigned(image, heaps[0] + blob, heaps[0] + heaps[1], next);
            int at = next[0];
            return size == 5 && image[at] == 0x20 && image[at + 1] == 2 && image[at + 2] == 1 &&
                image[at + 3] == 0x1c && image[at + 4] == 0x18;
        }

        // Issue #170: binds CoreLib's internal TypeInitializationException(string, string, Exception)
        // for the initializer `source`, writes its type-name and message literals, and registers the
        // global that records the failure. failure[FailureCache] stays 0 (no wrapping, the trap
        // remains) only when the bundle has no CoreLib TypeInitializationException constructor.
        // Anything else that prevents wrapping is a diagnostic, never a silent trap.
        private static int BindInitializerFailure(BundleImages bundle, byte[] image, int[] heaps, int[] layout, int owner,
            int source, int context, int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets,
            int[] signatures, int[] objects, int functions, int[] globals, byte[] literals, int[] literalSize, int[] failure)
        {
            var found = new int[2];
            if (InitializerFailures.FindConstructor(bundle, found) < 0) return -1;
            if (found[0] < 0) return functions;
            int typeRow = ObjectLayouts.Owner(image, heaps, layout, source, false);
            if (typeRow < 1) return -1;
            int nameStart = literalSize[0];
            int nameLength = InitializerFailures.WriteInitializerName(image, heaps, layout, typeRow, literals, nameStart);
            if (nameLength < 0) return nameLength; // malformed metadata or literal capacity, never a silent trap
            if (nameLength == 0) return -1;
            int messageStart = nameStart + 2 * nameLength;
            int messageLength = InitializerFailures.WriteMessage(literals, messageStart, nameStart, nameLength);
            if (messageLength < 0) return messageLength;
            byte[] coreLib = bundle.Get(found[0]);
            var coreLayout = new int[4];
            var coreHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(coreLib, coreLayout, coreHeaps) < 0) return -1;
            int constructor = indices[bases[found[0]] + found[1]] - 1;
            if (constructor < 0)
            {
                functions = Register(bundle, coreLib, coreLayout, coreHeaps, found[0], found[1], 0, owners, rows, contexts,
                    bases, indices, arities, offsets, signatures, objects, functions);
                if (functions < 0) return functions;
                constructor = functions - 1;
            }
            int shape = offsets[constructor];
            if (arities[constructor] != 4) return -1;
            int exceptionType = signatures[shape + 4];
            // Owner 0 with a reserved negative row, as the shadow-stack and pin globals use: an
            // owner of -2 or below would be read as a runtime type descriptor.
            int cacheRow = InitializerFailures.CacheRow(owner, source);
            if (cacheRow == 0) return -2;
            // Each closed declaring layout owns its cached failure object.
            int cache = StaticFields.Register(globals, 0, cacheRow, ObjectReference, context);
            if (cache < 0) return cache;
            literalSize[0] = messageStart + 2 * messageLength;
            failure[TypeInitializerWasm.FailureConstructor] = constructor;
            failure[TypeInitializerWasm.FailureClass] = signatures[shape + 1];
            failure[TypeInitializerWasm.FailureNameStart] = nameStart;
            failure[TypeInitializerWasm.FailureNameLength] = nameLength;
            failure[TypeInitializerWasm.FailureMessageStart] = messageStart;
            failure[TypeInitializerWasm.FailureMessageLength] = messageLength;
            failure[TypeInitializerWasm.FailureExceptionClass] = exceptionType;
            failure[TypeInitializerWasm.FailureCache] = cache + 1;
            // Step 2: the CoreLib exception for each fixed tag, built with its parameterless
            // constructor (default message from SR, decision 0081). Unbound ones keep the trap.
            var fixedTable = new int[2 * TypeInitializerWasm.FixedFailureCount];
            functions = BindDefaultExceptions(bundle, found[0], owners, rows, contexts, bases, indices, arities, offsets,
                signatures, objects, functions, fixedTable);
            if (functions < 0) return functions;
            for (int i = 0; i < fixedTable.Length; i++) failure[TypeInitializerWasm.FailureFixed + i] = fixedTable[i];
            return functions;
        }

        // For fixed failure i, table[2 * i] is CoreLib's parameterless constructor (function index, or
        // -1) and table[2 * i + 1] its class. Only where the CLR's runtime-raised exception carries that
        // same default message: a failed cast names its types and the argument checks name their
        // parameter, so those stay -1 rather than build an object with a different Message.
        private static int BindDefaultExceptions(BundleImages bundle, int core, int[] owners, int[] rows, int[] contexts,
            int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures, int[] objects, int functions, int[] table)
        {
            for (int i = 0; i < table.Length; i += 2) table[i] = -1;
            byte[] coreLib = bundle.Get(core);
            var coreLayout = new int[4];
            var coreHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(coreLib, coreLayout, coreHeaps) < 0) return -1;
            for (int i = 0; 2 * i < table.Length; i++)
            {
                if (!TypeInitializerWasm.DefaultMessageMatches(i)) continue;
                int type = ObjectLayouts.FindTypeDefinition(coreLib, coreHeaps, coreLayout, "System", FixedFailureTypeName(i));
                int row = type == 0 ? 0 : InitializerFailures.FindDefaultConstructor(coreLib, coreHeaps, coreLayout, type);
                if (row == 0) continue;
                int linked = indices[bases[core] + row] - 1;
                if (linked < 0)
                {
                    functions = Register(bundle, coreLib, coreLayout, coreHeaps, core, row, 0, owners, rows, contexts,
                        bases, indices, arities, offsets, signatures, objects, functions);
                    if (functions < 0) return functions;
                    linked = functions - 1;
                }
                table[2 * i] = linked;
                table[2 * i + 1] = signatures[offsets[linked] + 1];
            }
            return functions;
        }

        // Stage 155 (decision 0082): a bound catch handler needs an object for everything its clause
        // receives. A thrown managed object is one already; a fixed tag the CLR raises with the default
        // message is built from CoreLib's parameterless constructor (bound here into fixedObjects). A
        // clause that cannot receive objects (no linked class), or that receives such a fixed tag while
        // CoreLib's constructor cannot be bound, is diagnosed at its handler.
        private static int BindCaughtExceptions(BundleImages bundle, int[] clauses, int[] location, int[] owners, int[] rows,
            int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures, int[] objects,
            int functions, int[] fixedObjects)
        {
            int needed = 0;
            for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
            {
                if (!ExceptionClauses.IsBound(clauses, clause)) continue;
                int mask = clauses[clause + ExceptionClauses.Mask];
                if (location.Length != 0) location[2] = ExceptionClauses.HandlerOffset(clauses, clause);
                if (!ManagedExceptionClauses.CatchesObjects(mask)) return -2;
                needed = needed | (ManagedExceptionClauses.FixedBits(mask) & ((1 << ExceptionDispatchWasm.FixedObjectCount) - 1));
            }
            if (needed != 0)
            {
                var found = new int[2];
                if (InitializerFailures.FindCoreLib(bundle, found) < 0) return -1;
                if (found[0] >= 0)
                {
                    functions = BindDefaultExceptions(bundle, found[0], owners, rows, contexts, bases, indices, arities, offsets,
                        signatures, objects, functions, fixedObjects);
                    if (functions < 0) return functions;
                }
                for (int clause = 0; clause < clauses.Length; clause += ExceptionClauses.Width)
                {
                    if (!ExceptionClauses.IsBound(clauses, clause)) continue;
                    int bits = ManagedExceptionClauses.FixedBits(clauses[clause + ExceptionClauses.Mask]);
                    if (location.Length != 0) location[2] = ExceptionClauses.HandlerOffset(clauses, clause);
                    for (int tag = 0; tag < ExceptionDispatchWasm.FixedObjectCount; tag++)
                        if ((bits & (1 << tag)) != 0 && fixedObjects[2 * tag] < 0) return -2;
                }
            }
            if (location.Length != 0) location[2] = -1;
            return functions;
        }

        private static string FixedFailureTypeName(int i)
        {
            if (i == 0) return "OverflowException";
            if (i == 1) return "DivideByZeroException";
            if (i == 2) return "IndexOutOfRangeException";
            if (i == 3) return "NullReferenceException";
            if (i == 4) return "InvalidCastException";
            if (i == 5) return "ArgumentException";
            return "ArgumentNullException";
        }

        // Managed calli uses the type carrier. Native call sites preserve their
        // loaded tokens and dispatch through the selected typed host route.
        private static int LinkCallIndirect(BundleImages bundle, byte[] image, int[] heaps, int[] layout, int count,
            int owner, int context, int[] records, int[] owners, int[] rows, int[] contexts, int[] arities,
            int[] offsets, int[] signatures, int[] objects, int functions, FunctionPointerPlan pointers, int[] location,
            NativeImportTable natives, int nativeBase)
        {
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
            {
                if (records[slot] != BranchIl.CallIndirect) continue;
                if (location.Length != 0) location[2] = slot / BranchIl.Width;
                int blob = StandaloneSignatures.BlobIndex(image, heaps, layout, count, 0x11000000 | records[slot + 1]);
                if (blob < 0) return -1;
                int nativeResult = NativeIndirectImports.ReadResult(bundle, image, heaps, layout, blob, owner, objects);
                if (nativeResult < 0) return nativeResult;
                if (nativeResult == NativeIndirectImports.DynamicUInt32)
                {
                    records[slot] = BranchIl.CallDynamicNative;
                    records[slot + 1] = nativeBase + NativeIndirectImports.RegisterDynamic(natives);
                    continue;
                }
                if (nativeResult != 0)
                {
                    records[slot] = BranchIl.CallNativeIndirect;
                    records[slot + 1] = nativeBase + NativeIndirectImports.Register(natives);
                    records[slot + 3] = nativeResult;
                    continue;
                }
                var types = new int[256];
                int arity = SignatureTypes.Read(bundle, image, heaps, blob, types, 0, 0, layout, objects, owner, context);
                if (arity < 0) return arity;
                int chain = pointers.FindCarrier(arities, offsets, signatures, types, 0, arity);
                if (chain < 0)
                {
                    if (functions >= owners.Length) return -3;
                    int offset = NextSignature(offsets, arities, functions);
                    if (arity >= signatures.Length - offset) return -3;
                    for (int i = 0; i <= arity; i++) signatures[offset + i] = types[i];
                    owners[functions] = owner;
                    rows[functions] = 0; // synthetic carrier, never compiled from IL
                    contexts[functions] = context;
                    arities[functions] = arity;
                    offsets[functions] = offset;
                    chain = pointers.AddCarrier(functions++);
                }
                records[slot + 1] = pointers.Carriers[chain];
                records[slot + 3] = chain;
            }
            if (location.Length != 0) location[2] = -1;
            return functions;
        }

        internal static int NextSignature(int[] offsets, int[] arities, int functions) =>
            functions == 0 ? 0 : offsets[functions - 1] + arities[functions - 1] + 1;

        private static bool LinkExchange(BundleImages bundle, byte[] image, int[] heaps, int[] layout, int owner, int[] objects,
            int[] records, int slot)
        {
            // Keep the interior reference confined to this fused operation.
            // Only local/argument loads can occur between ldflda and the call.
            int storage = FieldAccess.Storage(records[slot + 3]);
            if (storage != EventHandlerReference && !DelegateShapes.IsKey(storage)) return false;
            int replacement = records[slot + 2] * BranchIl.Width;
            if (replacement >= records.Length || records[replacement] != BranchIl.Load || records[replacement + 3] != 0)
                return false;
            int comparand = records[replacement + 2] * BranchIl.Width;
            if (comparand >= records.Length || records[comparand] != BranchIl.Load || records[comparand + 3] != 0)
                return false;
            int call = records[comparand + 2] * BranchIl.Width;
            // Stage 181: CoreLib's own accessors name CompareExchange<EventHandler> by MethodDef.
            if (call >= records.Length || records[call] != BranchIl.MethodSpecCall || records[call + 3] != 0) return false;
            if (storage == EventHandlerReference)
            {
                if (!EventMetadata.IsHandlerExchange(image, heaps, layout, records[call + 1]) &&
                    !CoreLibDelegateDefinitions.IsHandlerExchange(image, heaps, layout, records[call + 1]))
                    return false;
            }
            else
            {
                // Stage 221 (decision 0092): CompareExchange<K> on a field of general delegate key K.
                // Identity comparison of invocation lists is the CLR's reference comparison.
                int argument = EventMetadata.ExchangeArgument(image, heaps, layout, records[call + 1]);
                if (argument <= 0 || BundleLayouts.RegisterReference(bundle, image, layout, heaps, owner, argument, objects) != storage)
                    return false;
            }

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
        internal static int Lookup(int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int functions,
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
            int arity = MethodShape(bundle, image, layout, heaps, owner, row, NativeReceiverCalls.DeclaringContext(context),
                signatures, offset, objects, allowVirtual, initializer, receiverOverride, methodArguments);
            if (arity < 0) return arity;
            if (context < 0)
            {
                if (!IsValueMethod(image, layout, heaps, row, signatures, offset, arity, objects)) return -2;
                signatures[offset + 1] = ValueLocation(signatures[offset + 1]);
            }
            else if (NativeReceiverCalls.IsNativeKey(context))
            {
                if (!IsValueMethod(image, layout, heaps, row, signatures, offset, arity, objects)) return -2;
                signatures[offset + 1] = Native;
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
            RuntimeTypeMetadata.IsStorage(type) || IsManagedLocation(type) || IsSpan(type) || IsArray(StackType(type)) || IsValueLocation(type) ||
            // The platform event types share a compiler-owned ABI across assemblies.
            // A Delegate slot currently holds only supported EventHandler values.
            type == EventArgsReference || type == EventHandlerReference || type == DelegateReference ||
            // Stage 220: delegate keys live in the bundle-wide registry, so every assembly agrees.
            DelegateShapes.IsKey(type) ||
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
            bool imported = (attributes & 0x2000) != 0;
            if ((attributes & 0x400) != 0 || (attributes & 0x2010) == 0x2000 ||
                (!allowVirtual && (attributes & 0x40) != 0) || (implementation & 7) != 0)
                return -2;
            bool debuggerQuery = imported && DebuggerNativeMetadata.IsAttachmentQuery(image, heaps, layout, row, implementation);
            int nativeImport = imported && !debuggerQuery ? Win32ModuleBinding.FindImport(image, heaps, layout, row, implementation, bundle, assembly) : 0;
            int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (owner < 0) return -1;
            bool isStatic = (attributes & 0x10) != 0;
            // The initializer itself is reached only through its guard (decision 0057).
            if (isStatic && MethodNames.IsInitializer(image, layout, heaps, row) != initializer) return -2;
            var initializerFlags = new int[1];
            int initialization = TypeInitializers.Find(image, layout, heaps, owner, initializerFlags);
            if (initialization < 0) return initialization;
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
            if (!isStatic && receiver == 0 && allowVirtual && (attributes & 0x40) != 0 &&
                ObjectLayouts.IsCoreObjectType(image, heaps, layout, owner << 2))
                receiver = ObjectReference;
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
            }
            if (!isStatic && initialization != 0 && initializerFlags[0] == 0 && ObjectLayouts.IsValueType(objects, receiver) &&
                !ObjectLayouts.IsConstructor(image, heaps, layout, row)) return -2;
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int arity = SignatureTypes.Read(bundle, image, heaps, signature, types, offset, receiver, layout, objects, assembly, context, methodArguments);
            if ((arity == 3 || arity == 4) && ScopedPathBinding.FindWrapper(image, layout, heaps, row) != 0)
                types[offset + 1] = ReadOnlyCharacterLocation;
            if ((debuggerQuery || nativeImport != 0) && arity >= 0 && (debuggerQuery ? arity != 0 || types[offset] != I32 :
                !Win32ModuleBinding.MatchesSignature(bundle, objects, nativeImport, types, offset, arity))) return -2;
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
