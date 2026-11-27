using PeLibrary;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal sealed class RuntimeGenericDefinitions
    {
        internal readonly int[] Fixups = new int[1];
        private int constructor = -1, message = -1, exception;
        internal int ExceptionType => exception;

        internal int BindFailure(BundleImages bundle, int owner, int[] owners, int[] rows, int[] contexts,
            int[] bases, int[] indices, int[] arities, int[] offsets, int[] signatures, int[] objects, int functions)
        {
            if (constructor >= 0) return functions;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int type = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System", "InvalidOperationException");
            int resources = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System", "SR");
            int constructorRow = FindFailureMethod(image, heaps, layout, type, ".ctor", false);
            int messageRow = FindFailureMethod(image, heaps, layout, resources, "get_InvalidOperation_NotGenericType", true);
            if (constructorRow <= 0 || messageRow <= 0) return -2;
            functions = Bind(bundle, image, layout, heaps, owner, constructorRow, owners, rows, contexts,
                bases, indices, arities, offsets, signatures, objects, functions);
            if (functions < 0) return functions;
            constructor = indices[bases[owner] + constructorRow] - 1;
            exception = signatures[offsets[constructor] + 1];
            functions = Bind(bundle, image, layout, heaps, owner, messageRow, owners, rows, contexts,
                bases, indices, arities, offsets, signatures, objects, functions);
            if (functions < 0) return functions;
            message = indices[bases[owner] + messageRow] - 1;
            return functions;
        }

        private static int FindFailureMethod(byte[] image, int[] heaps, int[] layout, int type, string name, bool getter)
        {
            var range = new int[2];
            if (type <= 0 || ObjectLayouts.MethodRange(image, heaps, layout, type, range) < 0) return 0;
            var cursor = new int[1];
            for (int row = range[0]; row < range[1]; row++)
            {
                int flags = MethodDefinitions.ReadColumn(image, layout, row, 2);
                if ((flags & 0x2050) != (getter ? 0x10 : 0) ||
                    !ImageBytes.MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name))
                    continue;
                int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
                int end = getter ? SignatureReader.ReadStatic(image, heaps, blob, 0, cursor) :
                    SignatureReader.ReadInstance(image, heaps, blob, 1, cursor);
                if (end < 0) continue;
                if (!getter && !SignatureReader.TakeElement(image, cursor, end, SignatureReader.Void)) continue;
                if (SignatureReader.TakeElement(image, cursor, end, 0x0e) && cursor[0] == end) return row;
            }
            return 0;
        }

        private static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner, int row,
            int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets,
            int[] signatures, int[] objects, int functions)
        {
            if (indices[bases[owner] + row] != 0) return functions;
            return MethodCompiler.Register(bundle, image, layout, heaps, owner, row, 0, owners, rows, contexts,
                bases, indices, arities, offsets, signatures, objects, functions);
        }

        internal int Write(BundleImages bundle, byte[] code, int used, int[] objects, int[] globals, int hostImports)
        {
            if (code.Length - used < 36) return -3;
            code[used++] = 1;
            code[used++] = 1;
            used = WasmBinary.ValueType(code, used, exception);
            var layout = new int[4];
            var heaps = new int[8];
            // Returning an open definition may add its canonical global.
            // Include those new globals so a second call returns the same reference.
            for (int index = 0; index < globals[0]; index++)
            {
                int key = StaticFields.Owner(globals, index), row = StaticFields.Row(globals, index);
                if (!RuntimeTypeTokens.IsDescriptorOwner(key) || row < 0) continue;
                int module = RuntimeTypeTokens.ModuleSlot(key) - 1;
                int definition = RuntimeTypeGenericKind.Definition(bundle, objects, module, row, layout, heaps);
                if (definition == -1) return -1;
                if (definition < 0) continue;
                if (code.Length - used < 38) return -3;
                used = Local(code, used, 0x20, 0);
                used = Global(code, used, index);
                code[used++] = 0xd3; // ref.eq
                code[used++] = 0x04;
                code[used++] = 0x40;
                if (definition == 0)
                {
                    // Match RuntimeType's original failure using the linked SR
                    // getter and a fresh InvalidOperationException(string).
                    used = Gc(code, used, 1, exception);
                    used = Local(code, used, 0x22, 1);
                    used = Call(code, used, hostImports + message);
                    used = Call(code, used, hostImports + constructor);
                    used = ExceptionDispatchWasm.ThrowObject(code, used, 1);
                }
                else
                {
                    int descriptor = definition == row ? index : RuntimeTypeTokens.BindIdentity(globals, module, definition);
                    if (descriptor < 0) return descriptor;
                    used = Global(code, used, descriptor);
                    code[used++] = 0x0f;
                }
                code[used++] = 0x0b;
            }
            code[used++] = 0x00; // Unknown descriptor representation.
            code[used++] = 0x0b;
            return used;
        }

        private static int Global(byte[] code, int used, int index)
        {
            code[used++] = 0x23;
            return WasmBinary.Unsigned(code, used, index);
        }

        private static int Call(byte[] code, int used, int index)
        {
            code[used++] = 0x10;
            return WasmBinary.Unsigned(code, used, index);
        }
    }
}
