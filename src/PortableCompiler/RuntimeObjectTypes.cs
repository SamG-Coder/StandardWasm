using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    internal sealed class RuntimeObjectTypes
    {
        internal readonly int[] Fixups = new int[1];
        private byte[] receivers;

        internal void Include(int type)
        {
            if (type < FirstClass) return;
            if (receivers == null) receivers = new byte[MaxClasses];
            receivers[type - FirstClass] = 1;
        }

        internal int Write(byte[] code, int position, int[] objects, int[] globals)
        {
            if (code.Length - position < 3) return -3;
            code[position++] = 0; // no locals besides the receiver
            // Parents precede children in the nominal GC group. Test the most
            // derived layouts first, even if linked after the GetType call.
            for (int index = objects[0] - 1; index >= 0; index--)
            {
                int type = FirstClass + index;
                if (IsValueType(objects, type) || IsInterface(objects, type) ||
                    ArrayElement(objects, type) != 0 || !Includes(objects, type)) continue;
                int descriptor = RuntimeTypeTokens.BindLayout(objects, type, globals);
                if (descriptor < 0) return descriptor;
                if (code.Length - position < 24) return -3;
                position = BranchWasm.Local(code, position, 0x20, 0);
                code[position++] = 0xfb;
                code[position++] = 0x14; // ref.test non-null class
                position = WasmBinary.Signed(code, position, WasmBinary.HeapType(type));
                code[position++] = 0x04; // if
                code[position++] = 0x40; // no block result
                code[position++] = 0x23; // global.get
                position = WasmBinary.Unsigned(code, position, descriptor);
                code[position++] = 0x0f; // return
                code[position++] = 0x0b;
            }
            code[position++] = 0x00; // all verified receivers match a case
            code[position++] = 0x0b;
            return position;
        }

        private bool Includes(int[] objects, int type)
        {
            if (receivers == null) return false;
            for (int candidate = type; candidate != 0; candidate = Parent(objects, candidate))
                if (receivers[candidate - FirstClass] != 0) return true;
            return false;
        }
    }
}
