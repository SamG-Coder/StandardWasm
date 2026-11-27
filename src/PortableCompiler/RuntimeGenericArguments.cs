using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal sealed class RuntimeGenericArguments
    {
        private const int EmptyGlobalKey = int.MinValue + 64;
        internal readonly int[] Fixups = new int[1];
        internal int Vector { get; private set; }
        private int coreOwner = -1;

        internal int Bind(int owner, int vector)
        {
            if (coreOwner >= 0 && (coreOwner != owner || Vector != vector)) return -2;
            coreOwner = owner;
            Vector = vector;
            return 0;
        }

        internal int Write(BundleImages bundle, byte[] code, int used, int[] objects, int[] globals)
        {
            if (code.Length - used < 4) return -3;
            int empty = StaticFields.Register(globals, 0, EmptyGlobalKey, Vector);
            if (empty < 0) return empty;
            int profile = UsesMonoArraySemantics(bundle.Get(coreOwner));
            if (profile < 0) return profile;
            code[used++] = 0; // no local declarations
            var layout = new int[4];
            var heaps = new int[8];
            for (int global = 0; global < globals[0]; global++)
            {
                int owner = StaticFields.Owner(globals, global), row = StaticFields.Row(globals, global);
                if (!RuntimeTypeTokens.IsDescriptorOwner(owner) || row < 0) continue;
                int module = RuntimeTypeTokens.ModuleSlot(owner) - 1;
                int shape = Shape(bundle, objects, module, row, profile != 0, layout, heaps);
                if (shape == -2) continue;
                if (shape < 0) return shape;
                int count = shape == 0 ? 0 : ArgumentCount(objects, shape);
                if (code.Length - used < 44 || count > (code.Length - used - 44) / 7) return -3;

                used = Local(code, used, 0x20, 0);
                used = Global(code, used, 0x23, global);
                code[used++] = 0xd3; // ref.eq
                code[used++] = 0x04;
                code[used++] = 0x40;
                if (count == 0)
                    used = Empty(code, used, empty);
                else
                {
                    // CLR returns a fresh mutable Type[] for every nonempty
                    // result. Its elements remain canonical Type references.
                    for (int i = 0; i < count; i++)
                    {
                        int descriptor = RuntimeTypeTokens.BindLayout(bundle, objects, Argument(objects, shape, i), globals);
                        if (descriptor < 0) return descriptor;
                        used = Global(code, used, 0x23, descriptor);
                    }
                    used = Gc(code, used, 8, Vector); // array.new_fixed
                    used = WasmBinary.Unsigned(code, used, count);
                }
                code[used++] = 0x0f;
                code[used++] = 0x0b;
            }
            if (code.Length - used < 2) return -3;
            code[used++] = 0x00; // An unrepresented descriptor stays unsupported.
            code[used++] = 0x0b;
            return used;
        }

        private int Empty(byte[] code, int used, int empty)
        {
            used = Global(code, used, 0x23, empty);
            code[used++] = 0xd1;
            code[used++] = 0x04;
            code[used++] = 0x40;
            used = Gc(code, used, 8, Vector);
            code[used++] = 0;
            used = Global(code, used, 0x24, empty);
            code[used++] = 0x0b;
            return Global(code, used, 0x23, empty);
        }

        private static int Shape(BundleImages bundle, int[] objects, int owner, int row, bool mono,
            int[] layout, int[] heaps)
        {
            if (row >= RuntimeTypeTokens.PrimitiveVectorKey)
                return owner == AssemblyBundle.ObjectOwner && ArrayTypeRefs.Storage(row - RuntimeTypeTokens.PrimitiveVectorKey) != 0 ? 0 : -2;
            if (row >= RuntimeTypeTokens.ClosedTypeKey)
            {
                int type = row - RuntimeTypeTokens.ClosedTypeKey;
                if (type < FirstClass || type - FirstClass >= objects[0]) return -2;
                // CoreCLR asks the root element for the instantiation. Mono
                // tests the array class itself and reports no generic arguments.
                int element = ArrayElementKey(objects, type);
                while (!mono && element != 0)
                {
                    type = element;
                    element = ArrayElementKey(objects, type);
                }
                return element != 0 || ArgumentCount(objects, type) == 0 ? 0 : type;
            }
            if (owner == AssemblyBundle.ObjectOwner) return row == 0 ? 0 : -2;
            if (owner < 0 || owner >= bundle.Count) return -2;
            int definition = RuntimeTypeGenericKind.Definition(bundle, objects, owner, row, layout, heaps);
            // A generic definition requires canonical generic-parameter
            // descriptors, a different representation from closed arguments.
            return definition == 0 ? 0 : definition < 0 ? definition : -2;
        }

        private static int UsesMonoArraySemantics(byte[] image)
        {
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int runtime = FindTypeDefinition(image, heaps, layout, "System", "RuntimeType");
            var range = new int[2];
            if (runtime < 1 || MethodRange(image, heaps, layout, runtime, range) < 0) return -1;
            for (int row = range[0]; row < range[1]; row++)
                if (MethodDefinitions.ReadColumn(image, layout, row, 1) == 0x1000 &&
                    ImageBytes.MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "GetGenericArgumentsInternal"))
                    return 1;
            return 0;
        }

        private static int Global(byte[] code, int used, int opcode, int global)
        {
            code[used++] = (byte)opcode;
            return WasmBinary.Unsigned(code, used, global);
        }
    }
}
