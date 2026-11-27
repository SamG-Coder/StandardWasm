using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class AggregateBoxing
    {
        private const int Owner = -3;
        private const byte DescriptorField = 0, PayloadField = 1;

        internal static bool IsLayout(int[] objects, int type) => AssemblyOf(objects, type) == Owner;

        internal static int FindLayout(int[] objects) => Find(objects, Owner, 0, new int[0]);

        internal static int Layout(int[] objects)
        {
            int type = FindLayout(objects);
            if (type != 0) return type;
            var shape = new int[5];
            shape[2] = 2; // Descriptor and payload are both traced by the root walker.
            shape[4] = 0x100; // Sealed compiler-owned layout.
            type = Reserve(objects, Owner, 0, shape, 0, new int[0]);
            if (type < 0) return type;
            SetFieldType(objects, type, DescriptorField, ObjectReference);
            SetFieldType(objects, type, PayloadField, ObjectReference);
            return type;
        }

        internal static int ValueIdentity(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int token, int context, int[] methodArguments, int[] objects)
        {
            uint table = (uint)token >> 24;
            int row = token & 0xffffff;
            if (table == 0x1b)
                return ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                    owner, row, context, objects, methodArguments);
            if (table != 1 && table != 2) return -2;
            int coded = row << 2 | (table == 1 ? 1 : 0);
            if (RuntimeTypeMetadata.Storage(image, heaps, layout, coded, true) != 0) return -2;
            return BundleLayouts.RegisterType(bundle, image, layout, heaps, owner, coded, objects, true, true);
        }

        internal static int TypeBytes => 8 + WasmBinary.HeapTypeSize(WasmBinary.ValueLocationHeap);

        internal static int WriteType(byte[] output, int cursor)
        {
            output[cursor++] = 0x5f; // struct
            output[cursor++] = 3;
            output[cursor++] = 0x6d; // module-local canonical CLR Type descriptor
            output[cursor++] = 0;
            cursor = ManagedValueLocationWasm.WriteFieldType(output, cursor);
            output[cursor++] = 1; // installed after allocation when the cell needs an owner back edge
            output[cursor++] = 0x70; // private marker, distinct from ordinary user types
            output[cursor++] = 0;
            return cursor;
        }

        internal static int Box(byte[] code, int used, int source, int destination, int box,
            int descriptor, bool tracksRoots)
        {
            used = DescriptorGlobal(code, used, descriptor);
            if (tracksRoots)
            {
                code[used++] = 0xd0; // Typed empty cell, filled once the box exists.
                used = WasmBinary.Signed(code, used, WasmBinary.ValueLocationHeap);
            }
            else
            {
                used = Local(code, used, 0x20, source);
                used = ManagedValueLocationWasm.CreateCell(code, used, false);
            }
            code[used++] = 0xd0; // ref.null func
            code[used++] = 0x70;
            used = Gc(code, used, 0, box);
            used = Local(code, used, 0x21, destination);
            if (!tracksRoots) return used;
            used = BoxReference(code, used, destination, box);
            used = Local(code, used, 0x20, source);
            used = ManagedValueLocationWasm.CreateCell(code, used, true, destination);
            used = Gc(code, used, 5, box);
            code[used++] = PayloadField;
            return used;
        }

        internal static int Unbox(byte[] code, int used, int source, int type, int box, int descriptor, bool address)
        {
            used = GuardNull(code, used, source);
            used = TestCarrier(code, used, source, box);
            used = ThrowUnless(code, used);
            used = TestDescriptor(code, used, source, box, descriptor);
            used = ThrowUnless(code, used);
            used = Field(code, used, source, box, PayloadField);
            // unbox exposes the actual mutable cell. unbox.any returns its
            // immutable snapshot, with no writable alias into the box.
            return address ? used : ManagedValueLocationWasm.ReadSnapshot(code, used, type);
        }

        internal static int Test(byte[] code, int used, int source, int box, int descriptor)
        {
            used = TestCarrier(code, used, source, box);
            code[used++] = 0x04;
            code[used++] = 0x6d; // if (result eqref)
            used = TestDescriptor(code, used, source, box, descriptor);
            code[used++] = 0x04;
            code[used++] = 0x6d;
            used = Local(code, used, 0x20, source);
            code[used++] = 0x05;
            used = NullObject(code, used);
            code[used++] = 0x0b;
            code[used++] = 0x05;
            used = NullObject(code, used);
            code[used++] = 0x0b;
            return used;
        }

        internal static int Descriptor(byte[] code, int used, int source, int box) =>
            Field(code, used, source, box, DescriptorField);

        internal static int TestCarrier(byte[] code, int used, int source, int box)
        {
            used = Local(code, used, 0x20, source);
            code[used++] = 0xfb;
            code[used++] = 0x14; // ref.test, nonnull
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(box));
        }

        internal static int TestDescriptor(byte[] code, int used, int source, int box, int descriptor)
        {
            used = Descriptor(code, used, source, box);
            used = DescriptorGlobal(code, used, descriptor);
            code[used++] = 0xd3; // ref.eq; a foreign instance cannot supply this canonical identity.
            return used;
        }

        internal static int DescriptorGlobal(byte[] code, int used, int descriptor)
        {
            code[used++] = 0x23;
            return WasmBinary.Unsigned(code, used, descriptor);
        }

        private static int Field(byte[] code, int used, int source, int box, byte field)
        {
            used = BoxReference(code, used, source, box);
            used = Gc(code, used, 2, box);
            code[used++] = field;
            return used;
        }

        private static int BoxReference(byte[] code, int used, int source, int box)
        {
            used = Local(code, used, 0x20, source);
            code[used++] = 0xfb;
            code[used++] = 0x16; // ref.cast, nonnull
            return WasmBinary.Signed(code, used, WasmBinary.HeapType(box));
        }

        private static int ThrowUnless(byte[] code, int used)
        {
            code[used++] = 0x45;
            return ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.InvalidCastTag);
        }

        private static int NullObject(byte[] code, int used)
        {
            code[used++] = 0xd0;
            code[used++] = 0x6d;
            return used;
        }
    }
}
