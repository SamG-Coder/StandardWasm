using PeLibrary;
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
            if (MethodNames.HasInitializer(image, layout, heaps, count))
                return -2;

            // A MethodDef row is local to its image. Give every row a disjoint
            // range in the global symbol index and record its owner on the queue.
            int modules = ranges.Length / 2 + 1;
            var bases = new int[modules + 1];
            bases[1] = count;
            for (int module = 1; module < modules; module++)
            {
                CompileLocation.Set(location, module, 0, -1);
                byte[] dependency = AssemblyBundle.Load(main, bundle, ranges, module);
                var dependencyLayout = new int[4];
                var dependencyHeaps = new int[8];
                int size = CompressedTables.FindWithHeaps(dependency, dependencyLayout, dependencyHeaps);
                if (size < 0) return size == MethodDefinitions.Unsupported ? -2 : -1;
                if (MethodNames.HasInitializer(dependency, dependencyLayout, dependencyHeaps, size))
                    return -2;
                if (size >= int.MaxValue - bases[module]) return -2;
                bases[module + 1] = bases[module] + size;
            }
            int symbols = bases[modules];
            var objects = new int[ObjectLayouts.FieldMapStart + main.Length / 6 + 1];
            var owners = new int[symbols];
            var rows = new int[symbols];
            var indices = new int[symbols + 1];
            var arities = new int[symbols];
            var offsets = new int[symbols];
            var sizes = new int[symbols];
            var thunks = new int[symbols + 1];
            var signatures = new byte[output.Length];
            var frame = new byte[output.Length];
            var moduleCode = new byte[output.Length];
            var literals = new byte[output.Length];
            var literalSize = new int[1];
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
                    functions = Register(image, layout, heaps, 0, (int)row, owners, rows, bases, indices,
                        arities, offsets, signatures, objects, functions);
                    if (functions < 0) return functions;
                    index = functions - 1;
                }
                exports[export] = index;
            }
            var body = new int[6];
            var frameShape = new int[6];

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
                if (body[5] != 0)
                    return -2;

                int parameters = arities[current];
                int signature = offsets[current];
                byte result = signatures[signature];
                bool scalar = result == I32;
                for (int i = 0; i < parameters; i++)
                {
                    byte type = signatures[signature + i + 1];
                    frame[i] = type;
                    if (type != I32) scalar = false;
                }
                int locals = 0;
                if (body[3] != 0)
                {
                    int localBlob = StandaloneSignatures.BlobIndex(image, heaps, layout, count, body[3]);
                    if (localBlob < 0)
                        return -1;
                    locals = SignatureTypes.Read(image, heaps, localBlob, frame, parameters, -1, layout, objects);
                    if (locals < 0)
                        return locals;
                    if (locals > 0 && body[4] == 0)
                        return -2;
                    if (owner != 0)
                        for (int i = 0; i < locals; i++)
                            if (!IsScalar(frame[parameters + i])) return -2;
                }
                if (body[1] > (int.MaxValue - 32) / 64)
                    return -2;

                var code = new byte[body[1] * 4 + 2];
                int used = scalar && locals == 0 ? StraightLineIl.Translate(image, body, parameters, code) : -2;
                if (used < 0)
                {
                    var records = new int[body[1] * BranchIl.Width];
                    status = BranchIl.Decode(image, body[0], body[1], parameters, locals, records, location);
                    if (status < 0)
                        return status;
                    functions = LinkOperands(main, bundle, ranges, image, layout, heaps, owner, records, owners, rows,
                        bases, indices, arities, offsets, signatures, objects, thunks, literals, literalSize, functions, location);
                    if (functions < 0)
                        return functions;

                    if (location.Length != 0) location[2] = -1;
                    int maximum = body[2];
                    if (maximum > 0 && body[1] > int.MaxValue / maximum)
                        return -2;
                    var depths = new int[body[1]];
                    var states = new byte[body[1] * maximum];
                    status = StackDepths.Analyze(records, depths, states, maximum, frame,
                        result, signatures, offsets, arities, location);
                    if (status < 0) return status;

                    if (location.Length != 0) location[2] = -1;
                    int capacity = body[1] * 64 + 32;
                    if (locals > (int.MaxValue - capacity) / 3)
                        return -3;
                    capacity += locals * 3;
                    int classBytes = objects[0] * 12;
                    if (classBytes > int.MaxValue - capacity) return -3;
                    capacity += classBytes;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        int kind = records[slot];
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
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        int kind = records[slot];
                        if (kind == BranchIl.JoinStrings || kind == BranchIl.StringFromChars)
                        {
                            frameShape[5] = 1;
                            break;
                        }
                    }
                    code = new byte[capacity];
                    used = BranchWasm.Emit(records, depths, states, frameShape, frame, signatures, offsets, arities, code);
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
                moduleCode, entries, entryOffsets, exports, objects, thunks, literals, literalSize[0]);
        }

        private static int LinkOperands(byte[] main, byte[] bundle, int[] ranges, byte[] image, int[] layout,
            int[] heaps, int owner, int[] records, int[] owners, int[] rows, int[] bases, int[] indices, int[] arities, int[] offsets, byte[] signatures, int[] objects, int[] thunks,
            byte[] literals, int[] literalSize, int functions, int[] location)
        {
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
            {
                int kind = records[slot];
                if (kind == 0) continue;
                if (location.Length != 0) location[2] = slot / BranchIl.Width;
                if (owner != 0 && kind >= BranchIl.NewArray && kind != BranchIl.Narrow && kind != BranchIl.MemberCall &&
                    kind != BranchIl.WideConstant && kind != BranchIl.ConvertInteger && kind != BranchIl.Switch &&
                    kind != BranchIl.SingleConstant && kind != BranchIl.DoubleConstant && kind != BranchIl.ConvertFloat)
                    return -2; // The dependency backend currently admits only scalar state.
                if (kind == BranchIl.NewArray)
                {
                    int type = ArrayTypeRefs.ElementType(image, heaps, layout, unchecked((uint)records[slot + 1]));
                    if (type < 0) return type;
                    records[slot + 1] = type;
                }
                if (kind == BranchIl.GetField || kind == BranchIl.SetField)
                {
                    int field = records[slot + 1];
                    int fieldOwner = ObjectLayouts.Owner(image, heaps, layout, field, true);
                    if (fieldOwner < 0) return -1;
                    int type = ObjectLayouts.Register(image, heaps, layout, objects, fieldOwner);
                    if (type < 0) return type;
                    int fieldIndex = ObjectLayouts.FieldOrdinal(objects, type, field);
                    if (fieldIndex < 0) return -1;
                    records[slot + 1] = fieldIndex;
                    records[slot + 3] = (type << 8) | ObjectLayouts.FieldType(objects, field);
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
                        continue;
                    }
                    if (delegateMember == 0 && stringMember == 0 && opcode == 0x28 && bundle.Length != 0)
                    {
                        var symbol = new int[2];
                        int resolved = AssemblyBundle.Resolve(main, bundle, ranges, image, layout, heaps, member, symbol);
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
                            functions = Register(targetImage, targetLayout, targetHeaps, targetOwner, targetRow,
                                owners, rows, bases, indices, arities, offsets, signatures, objects, functions);
                            if (functions < 0) return functions;
                            linked = functions - 1;
                        }
                        if (!IsScalarSignature(signatures, offsets[linked], arities[linked]))
                            return -2;
                        records[slot] = BranchIl.Call;
                        records[slot + 1] = linked;
                        records[slot + 3] = 0;
                        continue;
                    }
                    int pointer = slot - 6 * BranchIl.Width;
                    if (delegateMember != DelegateMetadata.Construct || opcode != 0x73 || pointer < 0 ||
                        records[pointer] != BranchIl.LoadFunction || records[pointer + 2] != slot / BranchIl.Width)
                        return -2;
                    records[slot] = BranchIl.NewDelegate;
                    records[slot + 3] = records[pointer + 3];
                    continue;
                }
                if (kind != BranchIl.Call && kind != BranchIl.NewObject && kind != BranchIl.LoadFunction)
                    continue;
                int target = records[slot + 1];
                if (target < 1 || target > bases[owner + 1] - bases[owner])
                    return -1;
                int index = indices[bases[owner] + target] - 1;
                if (index < 0)
                {
                    functions = Register(image, layout, heaps, owner, target, owners, rows, bases, indices,
                        arities, offsets, signatures, objects, functions);
                    if (functions < 0) return functions;
                    index = functions - 1;
                }
                int signature = offsets[index];
                bool hasReceiver = arities[index] > 0 && signatures[signature + 1] >= ObjectLayouts.FirstClass;
                if (kind == BranchIl.LoadFunction)
                {
                    if (!hasReceiver || arities[index] != 1 || signatures[signature] != I32 ||
                        (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x10) != 0)
                        return -2;
                    int next = records[slot + 2] * BranchIl.Width;
                    if (next >= records.Length || records[next] != BranchIl.MemberCall || records[next + 3] != 0x73)
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
                        !ObjectLayouts.IsConstructor(image, heaps, layout, target))
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

        private static int Register(byte[] image, int[] layout, int[] heaps, int owner, int row,
            int[] owners, int[] rows, int[] bases, int[] indices, int[] arities, int[] offsets, byte[] signatures, int[] objects, int functions)
        {
            if (owner != 0 && (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x10) == 0)
                return -2;
            int offset = functions == 0 ? 0 : offsets[functions - 1] + arities[functions - 1] + 1;
            int arity = MethodShape(image, layout, heaps, row, signatures, offset, objects);
            if (arity < 0)
                return arity;
            if (owner != 0 && !IsScalarSignature(signatures, offset, arity))
                return -2;
            owners[functions] = owner;
            rows[functions] = row;
            indices[bases[owner] + row] = functions + 1;
            arities[functions] = arity;
            offsets[functions] = offset;
            return functions + 1;
        }

        private static bool IsScalarSignature(byte[] types, int offset, int arity)
        {
            if (!IsScalar(types[offset]) && types[offset] != Void) return false;
            for (int index = 1; index <= arity; index++)
                if (!IsScalar(types[offset + index])) return false;
            return true;
        }

        private static int MethodShape(byte[] image, int[] layout, int[] heaps, int row, byte[] types, int offset, int[] objects)
        {
            int attributes = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            if ((attributes & 0x2440) != 0 || (implementation & 7) != 0)
                return -2;
            int receiver = 0;
            if ((attributes & 0x10) == 0)
            {
                int owner = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (owner < 0) return -1;
                receiver = ObjectLayouts.Register(image, heaps, layout, objects, owner);
                if (receiver < 0) return receiver;
            }
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            return SignatureTypes.Read(image, heaps, signature, types, offset, receiver, layout, objects);
        }
    }
}
