using PeLibrary;

namespace PortableCompiler
{
    internal static class MethodCompiler
    {
        internal static int Compile(byte[] image, int row, byte[] output)
        {
            var layout = new int[4];
            var heaps = new int[6];
            int count = CompressedTables.FindWithHeaps(image, layout, heaps);
            if (row < 1 || row > count)
                return count == MethodDefinitions.Unsupported ? -2 : -1;
            // Preserve initialization semantics by rejecting images with module or
            // type initializers until the compiler can model their execution.
            if (MethodNames.HasInitializer(image, layout, heaps, count))
                return -2;
            int firstArity = Arity(image, layout, heaps, row);
            if (firstArity < 0)
                return -2;

            // Mark before following calls so shared targets and cycles keep one index.
            var rows = new int[count];
            var indices = new int[count + 1];
            var arities = new int[count];
            var sizes = new int[count];
            var moduleCode = new byte[output.Length];
            rows[0] = row;
            indices[row] = 1;
            arities[0] = firstArity;
            int functions = 1, length = 0;
            var body = new int[6];

            for (int current = 0; current < functions; current++)
            {
                int source = rows[current];
                uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, source, 0));
                int status = MethodBodies.Read(image, rva, body);
                if (status != 0)
                    return status == MethodBodies.Malformed ? -1 : -2;
                if (body[5] != 0)
                    return -2;
                int locals = 0;
                if (body[3] != 0)
                {
                    int localBlob = StandaloneSignatures.BlobIndex(image, heaps, layout, count, body[3]);
                    if (localBlob < 0)
                        return -1;
                    locals = LocalSignature.Count(image, heaps, localBlob);
                    if (locals < 0 || (locals > 0 && body[4] == 0))
                        return -2;
                }
                if (body[1] > (int.MaxValue - 16) / 64)
                    return -2;

                int parameters = arities[current];
                var code = new byte[body[1] * 4 + 2];
                int used = locals == 0 ? StraightLineIl.Translate(image, body, parameters, code) : -2;
                if (used < 0)
                {
                    var records = new int[body[1] * BranchIl.Width];
                    status = BranchIl.Decode(image, body[0], body[1], parameters, locals, records);
                    if (status < 0)
                        return status;
                    functions = LinkCalls(image, layout, heaps, records, rows, indices, arities, functions);
                    if (functions < 0)
                        return functions;
                    var depths = new int[body[1]];
                    if (!StackDepths.Analyze(records, depths, body[2]))
                        return -1;
                    int capacity = body[1] * 64 + 16;
                    // A call additionally emits one local.get per argument.
                    for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
                    {
                        if (records[slot] != BranchIl.Call || depths[slot / BranchIl.Width] == 0)
                            continue;
                        int arguments = records[slot + 3];
                        if (arguments > (int.MaxValue - capacity) / 6)
                            return -3;
                        capacity += arguments * 6;
                    }
                    code = new byte[capacity];
                    used = BranchWasm.Emit(records, depths, parameters, locals, body[2], code);
                }

                if (used > moduleCode.Length - length)
                    return -3;
                for (int i = 0; i < used; i++)
                    moduleCode[length + i] = code[i];
                sizes[current] = used;
                length += used;
            }
            return WasmBinary.WriteModule(output, arities, sizes, functions, moduleCode);
        }

        private static int LinkCalls(byte[] image, int[] layout, int[] heaps, int[] records,
            int[] rows, int[] indices, int[] arities, int functions)
        {
            for (int slot = 0; slot < records.Length; slot += BranchIl.Width)
            {
                if (records[slot] != BranchIl.Call)
                    continue;
                int target = records[slot + 1];
                if (target == 0 || target >= indices.Length)
                    return -1;
                int index = indices[target] - 1;
                if (index < 0)
                {
                    int arity = Arity(image, layout, heaps, target);
                    if (arity < 0)
                        return -2;
                    index = functions++;
                    rows[index] = target;
                    indices[target] = index + 1;
                    arities[index] = arity;
                }
                records[slot + 1] = index;
                records[slot + 3] = arities[index];
            }
            return functions;
        }

        private static int Arity(byte[] image, int[] layout, int[] heaps, int row)
        {
            int attributes = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            if ((attributes & 0x2010) != 0x10 || (implementation & 7) != 0)
                return -1;
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            return IntegerSignature.ParameterCount(image, heaps, signature);
        }
    }
}
