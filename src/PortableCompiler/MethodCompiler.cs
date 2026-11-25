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

            int attributes = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            if ((attributes & 0x2010) != 0x10 || (implementation & 7) != 0)
                return -2;
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int parameters = IntegerSignature.ParameterCount(image, heaps, signature);
            if (parameters < 0)
                return -2;

            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            var body = new int[6];
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
                // The Wasm local initializer cannot reproduce a non-init CLI frame.
                if (locals < 0 || (locals > 0 && body[4] == 0))
                    return -2;
            }

            if (locals == 0 && body[1] <= (int.MaxValue - 2) / 4)
            {
                var linearCode = new byte[body[1] * 4 + 2];
                int linearLength = StraightLineIl.Translate(image, body, parameters, linearCode);
                if (linearLength >= 0)
                    return WasmBinary.Write(output, parameters, linearCode, linearLength);
            }
            return CompileBranches(image, body, parameters, locals, output);
        }

        private static int CompileBranches(byte[] image, int[] body, int parameters, int locals, byte[] output)
        {
            int length = body[1];
            // Each IL byte contributes at most one dispatcher arm. An arm needs fewer
            // than 64 bytes even with five-byte signed offsets and local indices.
            if (length > (int.MaxValue - 16) / 64)
                return -2;
            var records = new int[length * BranchIl.Width];
            int status = BranchIl.Decode(image, body[0], length, parameters, locals, records);
            if (status < 0)
                return status;
            var depths = new int[length];
            if (!StackDepths.Analyze(records, depths, body[2]))
                return -1;
            var code = new byte[length * 64 + 16];
            int used = BranchWasm.Emit(records, depths, parameters, locals, body[2], code);
            return WasmBinary.Write(output, parameters, code, used);
        }
    }
}
