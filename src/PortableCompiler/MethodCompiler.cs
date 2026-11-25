using PeLibrary;

namespace PortableCompiler
{
    internal static class MethodCompiler
    {
        internal static int Compile(byte[] image, int row, byte[] output)
        {
            var layout = new int[4];
            var heaps = new int[4];
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
            if (body[3] != 0 || body[5] != 0)
                return -2;
            // This instruction subset expands one IL byte to at most three Wasm bytes.
            // The conservative bound also leaves room for the local count and terminator.
            if (body[1] > (int.MaxValue - 2) / 4)
                return -2;
            var code = new byte[body[1] * 4 + 2];
            int length = StraightLineIl.Translate(image, body, parameters, code);
            return length < 0 ? length : WasmBinary.Write(output, parameters, code, length);
        }
    }
}
