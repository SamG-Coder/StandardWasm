using PeLibrary;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class PrimitiveBoxing
    {
        private const int Owner = -2;
        internal const int TypeBytes = 8;

        internal static bool IsLayout(int[] objects, int type) => ObjectLayouts.AssemblyOf(objects, type) == Owner;

        internal static int Layout(int[] objects)
        {
            var arguments = new int[0];
            int type = ObjectLayouts.Find(objects, Owner, 0, arguments);
            if (type != 0) return type;
            var shape = new int[5];
            shape[4] = 0x100; // Sealed, with a private emitted shape.
            return ObjectLayouts.Reserve(objects, Owner, 0, shape, 0, arguments);
        }

        internal static int Element(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int token, int context, int[] methodArguments, int[] objects)
        {
            int table = (int)((uint)token >> 24), row = token & 0xffffff;
            if (table == 1 || table == 2)
                return ArrayTypeRefs.PrimitiveElement(image, heaps, layout, row << 2 | (table == 1 ? 1 : 0));
            if (table != 0x1b) return -2;
            int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, row);
            if (blob < 0) return blob;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = ImageBytes.CompressedUnsigned(image, heaps[0] + blob, limit, next);
            if (size < 1 || size > limit - next[0]) return -1;
            int end = next[0] + size;
            int type = ManagedReferenceSignatures.Read(bundle, image, layout, heaps, owner, next,
                end, context, objects, 0, methodArguments: methodArguments);
            if (type < 0) return type;
            if (next[0] != end) return -1;
            return GenericScalar.IsScalar(type) ? GenericScalar.Element(type) : -2;
        }

        internal static int Storage(int element) => GenericScalar.Storage(GenericScalar.Key(element));

        internal static int WriteType(byte[] output, int cursor)
        {
            output[cursor++] = 0x5f; // struct
            output[cursor++] = 3;
            output[cursor++] = 0x7f; // exact CLI primitive identity
            output[cursor++] = 0;
            output[cursor++] = 0x7e; // payload bits
            output[cursor++] = 0;
            output[cursor++] = 0x70; // private funcref marker
            output[cursor++] = 0;
            return cursor;
        }

        internal static int Box(byte[] code, int used, int local, int element, int type)
        {
            used = Const(code, used, element);
            used = Local(code, used, 0x20, local);
            int storage = Storage(element);
            used = NormalizeSlot(code, used, storage);
            if (storage == SignatureTypes.F64) code[used++] = 0xbd; // i64.reinterpret_f64
            else if (storage != SignatureTypes.I64)
            {
                if (storage == SignatureTypes.F32) code[used++] = 0xbc; // i32.reinterpret_f32
                code[used++] = 0xad; // i64.extend_i32_u
            }
            code[used++] = 0xd0; // ref.null func
            code[used++] = 0x70;
            return Gc(code, used, 0, type); // struct.new
        }

        internal static int Unbox(byte[] code, int used, int local, int element, int type)
        {
            used = GuardNull(code, used, local);
            used = TestType(code, used, local, type);
            used = ThrowUnless(code, used);
            used = TestElement(code, used, local, type, element);
            used = ThrowUnless(code, used);
            used = Field(code, used, local, type, 1);
            int storage = Storage(element);
            if (storage == SignatureTypes.F64) code[used++] = 0xbf; // f64.reinterpret_i64
            else if (storage != SignatureTypes.I64)
            {
                code[used++] = 0xa7; // i32.wrap_i64
                if (storage == SignatureTypes.F32) code[used++] = 0xbe; // f32.reinterpret_i32
            }
            return used;
        }

        internal static int Test(byte[] code, int used, int local, int element, int type)
        {
            used = TestType(code, used, local, type);
            code[used++] = 0x04; // if (result eqref)
            code[used++] = 0x6d;
            used = TestElement(code, used, local, type, element);
            code[used++] = 0x04;
            code[used++] = 0x6d;
            used = Local(code, used, 0x20, local);
            code[used++] = 0x05;
            used = NullObject(code, used);
            code[used++] = 0x0b;
            code[used++] = 0x05;
            used = NullObject(code, used);
            code[used++] = 0x0b;
            return used;
        }

        private static int NullObject(byte[] code, int used)
        {
            code[used++] = 0xd0; // ref.null eq
            code[used++] = 0x6d;
            return used;
        }

        private static int ThrowUnless(byte[] code, int used)
        {
            code[used++] = 0x45; // i32.eqz
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.InvalidCastTag);
        }

        private static int TestType(byte[] code, int used, int local, int type)
        {
            used = Local(code, used, 0x20, local);
            code[used++] = 0xfb;
            code[used++] = 20; // ref.test (non-null)
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
        }

        private static int TestElement(byte[] code, int used, int local, int type, int element)
        {
            used = Field(code, used, local, type, 0);
            used = Const(code, used, element);
            code[used++] = 0x46; // i32.eq
            return used;
        }

        private static int Field(byte[] code, int used, int local, int type, int field)
        {
            used = Local(code, used, 0x20, local);
            code[used++] = 0xfb;
            code[used++] = 22; // ref.cast (non-null)
            used = WasmBinary.Signed(code, used, WasmBinary.HeapType(type));
            used = Gc(code, used, 2, type); // struct.get
            return WasmBinary.Unsigned(code, used, field);
        }
    }
}
