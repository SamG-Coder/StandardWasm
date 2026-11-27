using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class StaticRvaPointers
    {
        private const int CacheOwner = int.MaxValue - 22;

        internal static int Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] records, int[] clauses, int slot,
            int targetOwner, int target, int targetContext, AddressedFields addressed, int[] globals, byte[] literals, int[] size)
        {
            if (slot != 0 || clauses.Length != 0) return 0;
            int asRef = Next(records, slot), asPointer = Next(records, asRef), finish = Next(records, asPointer);
            if (!DirectSpec(records, asRef) || !DirectSpec(records, asPointer) ||
                !At(records, finish, Return) || Next(records, finish) != records.Length) return 0;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            var spec = new int[MethodSpecifications.Width];
            var type = new int[1];
            if (!ReadSpec(image, layout, heaps, counts, records, asRef, spec) ||
                NativeAddressIntrinsics.Identify(image, layout, heaps, counts, spec, readOnlyReference: true) != NativeAddressView ||
                MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner, context, objects, methodArguments, spec, type) < 0)
                return 0;
            int value = type[0];
            if (!ObjectLayouts.IsValueType(objects, value) ||
                !ReadSpec(image, layout, heaps, counts, records, asPointer, spec) ||
                NativeAddressIntrinsics.Identify(image, layout, heaps, counts, spec) != NativeAddressView ||
                MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner, context, objects, methodArguments, spec, type) < 0 ||
                type[0] != value) return 0;

            var measured = new int[2];
            if (NativeValueLayouts.Measure(bundle, ObjectLayouts.AssemblyOf(objects, value), ObjectLayouts.TypeDef(objects, value), measured) < 0 ||
                measured[0] < 1 || measured[1] < 1 || measured[1] > 8) return 0;
            byte[] getter = bundle.Get(targetOwner);
            var getterLayout = new int[4];
            var getterHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(getter, getterLayout, getterHeaps) < 0) return -1;
            int field = FindField(bundle, getter, getterLayout, getterHeaps, targetOwner, target, targetContext, objects, value, measured[0]);
            if (field <= 0) return field;
            int source = ArrayInitializerMetadata.FindStaticData(getter, getterHeaps, getterLayout, field, measured[0], null, measured[1]);
            if (source < 0) return 0;
            int literal = size[0];
            if (measured[0] > literals.Length - literal) return -3;
            int key = int.MaxValue - field;
            int offset = addressed.Find(targetOwner, key);
            if (offset < 0) offset = addressed.Add(targetOwner, key, measured[0]);
            if (offset < 0) return offset;
            int cache = StaticFields.Register(globals, CacheOwner, field, SignatureTypes.I32, targetOwner);
            if (cache < 0) return cache;
            for (int i = 0; i < measured[0]; i++) literals[literal + i] = getter[source + i];
            size[0] += measured[0];
            records[slot] = LoadStaticRvaPointer;
            records[slot + 1] = cache;
            records[slot + 2] = finish / Width;
            records[slot + 3] = measured[0];
            records[slot + Width + 1] = literal;
            records[slot + Width + 2] = offset;
            records[asRef] = 0;
            records[asPointer] = 0;
            return 1;
        }

        private static int FindField(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int getter, int context, int[] objects, int value, int bytes)
        {
            const int aggressiveInlining = 0x100;
            if ((MethodDefinitions.ReadColumn(image, layout, getter, 1) & ~aggressiveInlining) != 0) return 0;
            var signature = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, MethodDefinitions.ReadColumn(image, layout, getter, 4), 0, signature);
            if (end < 0 || !ReadOnlyReferenceReturns.ReadModifier(bundle, image, layout, heaps, owner, signature, end)) return 0;
            int declaring = ObjectLayouts.Owner(image, heaps, layout, getter, false);
            if (declaring < 1) return 0;
            var initializerFlags = new int[1];
            int initializer = TypeInitializers.Find(image, layout, heaps, declaring, initializerFlags);
            // Preserve the normal static call trigger. A BeforeFieldInit type
            // may defer initialization until one of its own fields is touched.
            if (initializer < 0 || initializer != 0 && initializerFlags[0] == 0) return 0;
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, getter, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] > 32 || body[3] != 0 || body[5] != 0) return 0;
            var records = new int[body[1] * Width];
            if (Decode(image, body[0], body[1], 0, 0, records, new int[0], false) < 0 ||
                !At(records, 0, StaticFieldAddress) || records[3] != 0) return 0;
            int length = Next(records, 0), span = Next(records, length), reference = Next(records, span);
            int reinterpret = Next(records, reference), finish = Next(records, reinterpret);
            if (!At(records, length, Constant) || records[length + 1] != bytes ||
                !At(records, span, MemberCall) || records[span + 3] != 0x73 ||
                !SpanMetadata.IsBytePointerConstructor(bundle, image, layout, heaps, owner, records[span + 1], context, null, objects) ||
                !DirectSpec(records, reference) || !DirectSpec(records, reinterpret) ||
                !At(records, finish, Return) || Next(records, finish) != records.Length) return 0;
            var counts = new int[45];
            var spec = new int[MethodSpecifications.Width];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                !ReadSpec(image, layout, heaps, counts, records, reference, spec) ||
                StaticByteIntrinsics.Identify(image, layout, heaps, counts, spec, null) != TakeStaticByteLocation ||
                !ReadSpec(image, layout, heaps, counts, records, reinterpret, spec) || spec[MethodSpecifications.Arity] != 2 ||
                !ExactManagedReads.IsPlatformAs(image, heaps, layout, counts, spec[MethodSpecifications.Target])) return 0;
            var types = new int[2];
            if (MethodInstantiations.ReadArguments(bundle, image, layout, heaps, owner, context, objects, null, spec, types) < 0 ||
                types[0] != GenericScalar.Key(5) || types[1] != value) return 0;
            int fieldOwner = ObjectLayouts.Owner(image, heaps, layout, records[1], true);
            return fieldOwner > 0 && TypeInitializers.Find(image, layout, heaps, fieldOwner, new int[1]) == 0 ? records[1] : 0;
        }

        private static bool ReadSpec(byte[] image, int[] layout, int[] heaps, int[] counts, int[] records, int at, int[] spec) =>
            MethodSpecifications.Read(image, heaps, layout, counts, records[at + 1], spec) >= 0;

        private static bool DirectSpec(int[] records, int at) => At(records, at, MethodSpecCall) && records[at + 3] == 0;

        private static bool At(int[] records, int at, int kind) => at >= 0 && at < records.Length && records[at] == kind;

        private static int Next(int[] records, int at) => at >= 0 && at < records.Length ? records[at + 2] * Width : -1;
    }
}
