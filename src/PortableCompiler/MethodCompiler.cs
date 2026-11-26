using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class MethodCompiler
    {
        internal static int Compile(byte[] image, int row, byte[] output)
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
            return CompileExports(image, entries, output);
        }

        internal static int CompileExports(byte[] image, byte[] entries, byte[] output)
        {
            var entryOffsets = new int[entries.Length / 9];
            int exportCount = EntryPoints.Read(entries, entryOffsets);
            if (exportCount < 0)
                return exportCount;
            var exports = new int[exportCount];
            var layout = new int[4];
            var heaps = new int[6];
            int count = CompressedTables.FindWithHeaps(image, layout, heaps);
            if (count < 0)
                return count == MethodDefinitions.Unsupported ? -2 : -1;
            if (MethodNames.HasInitializer(image, layout, heaps, count))
                return -2;

            var rows = new int[count];
            var indices = new int[count + 1];
            var arities = new int[count];
            var offsets = new int[count];
            var sizes = new int[count];
            var signatures = new byte[output.Length];
            var frame = new byte[output.Length];
            var moduleCode = new byte[output.Length];
            int functions = 0, length = 0;
            for (int export = 0; export < exportCount; export++)
            {
                uint row = ImageBytes.U32(entries, (uint)entryOffsets[export]);
                if (row == 0 || row > (uint)count)
                    return -1;
                int index = indices[row] - 1;
                if (index < 0)
                {
                    functions = Register(image, layout, heaps, (int)row, rows, indices,
                        arities, offsets, signatures, functions);
                    if (functions < 0) return functions;
                    index = functions - 1;
                }
                exports[export] = index;
            }
            var body = new int[6];
            var frameShape = new int[4];

            for (int current = 0; current < functions; current++)
            {
                int source = rows[current];
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
                    locals = SignatureTypes.Read(image, heaps, localBlob, frame, parameters, true);
                    if (locals < 0)
                        return locals;
                    if (locals > 0 && body[4] == 0)
                        return -2;
                }
                if (body[1] > (int.MaxValue - 32) / 64)
                    return -2;

                var code = new byte[body[1] * 4 + 2];
                int used = scalar && locals == 0 ? StraightLineIl.Translate(image, body, parameters, code) : -2;
                if (used < 0)
                {
                    var records = new int[body[1] * BranchIl.Width];
                    status = BranchIl.Decode(image, body[0], body[1], parameters, locals, records);
                    if (status < 0)
                        return status;
                    functions = LinkOperands(image, layout, heaps, records, rows, indices,
                        arities, offsets, signatures, functions);
                    if (functions < 0)
                        return functions;

                    int maximum = body[2];
                    if (maximum > 0 && body[1] > int.MaxValue / maximum)
                        return -2;
                    var depths = new int[body[1]];
                    var states = new byte[body[1] * maximum];
                    if (!StackDepths.Analyze(records, depths, states, maximum, frame,
                        result, signatures, offsets, arities))
                        return -1;

                    int capacity = body[1] * 64 + 32;
                    if (locals > (int.MaxValue - capacity) / 3)
                        return -3;
                    capacity += locals * 3;
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        if (records[slot] != BranchIl.Call || depths[slot / BranchIl.Width] == 0)
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
            return WasmBinary.WriteModule(output, arities, offsets, signatures, sizes, functions,
                moduleCode, entries, entryOffsets, exports);
        }

        private static int LinkOperands(byte[] image, int[] layout, int[] heaps, int[] records,
            int[] rows, int[] indices, int[] arities, int[] offsets, byte[] signatures, int functions)
        {
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
            {
                if (records[slot] == BranchIl.NewArray)
                {
                    int type = ArrayTypeRefs.ElementType(image, heaps, layout, unchecked((uint)records[slot + 1]));
                    if (type < 0) return type;
                    records[slot + 1] = type;
                }
                if (records[slot] != BranchIl.Call)
                    continue;
                int target = records[slot + 1];
                if (target == 0 || target >= indices.Length)
                    return -1;
                int index = indices[target] - 1;
                if (index < 0)
                {
                    functions = Register(image, layout, heaps, target, rows, indices,
                        arities, offsets, signatures, functions);
                    if (functions < 0) return functions;
                    index = functions - 1;
                }
                records[slot + 1] = index;
            }
            return functions;
        }

        private static int Register(byte[] image, int[] layout, int[] heaps, int row, int[] rows,
            int[] indices, int[] arities, int[] offsets, byte[] signatures, int functions)
        {
            int offset = functions == 0 ? 0 : offsets[functions - 1] + arities[functions - 1] + 1;
            int arity = MethodShape(image, layout, heaps, row, signatures, offset);
            if (arity < 0)
                return arity;
            rows[functions] = row;
            indices[row] = functions + 1;
            arities[functions] = arity;
            offsets[functions] = offset;
            return functions + 1;
        }

        private static int MethodShape(byte[] image, int[] layout, int[] heaps, int row, byte[] types, int offset)
        {
            int attributes = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            if ((attributes & 0x2010) != 0x10 || (implementation & 7) != 0)
                return -2;
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            return SignatureTypes.Read(image, heaps, signature, types, offset, false);
        }
    }
}
