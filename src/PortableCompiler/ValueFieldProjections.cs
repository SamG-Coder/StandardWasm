using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // A selector belongs to one emission pass: its owner is a closed Wasm
    // layout ID. Only the need for each kind of helper survives a retry.
    internal sealed class ValueFieldProjections
    {
        private const int Width = 3;
        private int[] fields = new int[12];
        internal readonly int KnownKinds;
        internal readonly int[] Fixups = new int[ValueFieldProjectionWasm.HelperCount];
        internal int Count, Kinds;
        internal int ReferenceVector;

        internal ValueFieldProjections(int knownKinds, int[] objects)
        {
            KnownKinds = knownKinds;
            if ((knownKinds & Reference) != 0)
                ReferenceVector = RegisterArray(objects, ObjectReference);
        }

        internal const int Scalar = 1, Reference = 2, Aggregate = 4, ClassScalar = 8, BooleanScalar = 16, NativeScalar = 32;

        internal static int Kind(int location) => location == Int32Location ? Scalar :
            location == BooleanLocation ? BooleanScalar : location == NativeIntLocation ? NativeScalar :
            IsReferenceLocation(location) ? Reference : IsValueLocation(location) ? Aggregate : 0;

        internal static int DecodeLocation(int[] objects, int encoded) =>
            IsValueType(objects, encoded) ? ValueLocation(encoded) : encoded;

        internal bool Uses(int location) =>
            (KnownKinds & (location == Int32Location ? Scalar | ClassScalar : Kind(location))) != 0;
        internal bool ClassScalars => (KnownKinds & ClassScalar) != 0;
        internal bool Aggregates => (KnownKinds & Aggregate) != 0;

        internal int Owner(int selector) => fields[selector * Width];
        internal int Ordinal(int selector) => fields[selector * Width + 1];
        internal int Location(int selector) => fields[selector * Width + 2];

        internal int BindReachable(int[] records, int[] depths, int[] states, int maximum, int[] objects, int[] diagnostic)
        {
            for (int pc = 0; pc < depths.Length; pc++)
            {
                int slot = pc * BranchIl.Width;
                if (depths[pc] == 0 || records[slot] != BranchIl.TakeProjectedFieldAddress) continue;
                if (IsNativeInteger(states[pc * maximum + depths[pc] - 2]))
                {
                    // The same metadata instruction can be a native field
                    // address. Preserve its existing offset representation.
                    records[slot] = BranchIl.FieldAddress;
                    continue;
                }
                int descriptor = records[slot + 3];
                int location = DecodeLocation(objects, FieldAccess.Storage(descriptor));
                int owner = FieldAccess.Owner(descriptor);
                int kind = IsValueType(objects, owner) ? Kind(location) : ClassScalar;
                int selector = Add(owner, records[slot + 1], location, kind);
                if (selector < 0)
                {
                    if (diagnostic.Length != 0) diagnostic[2] = pc;
                    return selector;
                }
                records[slot + 1] = selector;
            }
            return 0;
        }

        private int Add(int owner, int ordinal, int location, int kind)
        {
            Kinds |= kind;
            int entry = 0;
            while (entry < Count)
            {
                if (Owner(entry) == owner && Ordinal(entry) == ordinal && Location(entry) == location) return entry;
                entry++;
            }
            int at = entry * Width;
            if (at == fields.Length)
            {
                if (at > int.MaxValue / 2) return -3;
                var grown = new int[at * 2];
                for (int i = 0; i < at; i++) grown[i] = fields[i];
                fields = grown;
            }
            fields[at] = owner;
            fields[at + 1] = ordinal;
            fields[at + 2] = location;
            Count++;
            return entry;
        }

        // Read the exact CLI type in its closed declaring context before
        // choosing the location ABI. Int32 and UInt32 share field storage.
        internal static int Bind(BundleImages bundle, int[] objects, int owner, int row, bool classInt32 = false)
        {
            byte[] image = bundle.Get(AssemblyOf(objects, owner));
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            int table = AssemblyReferences.FieldTable(image, heaps, layout);
            if (table < 0) return table;
            var field = new int[3];
            AssemblyReferences.ReadField(image, layout, table, row, field);
            if ((field[0] & 0x30) != 0) return -2; // Keep static and init-only fields out of this mutable path.
            int blob = field[2];
            if (blob < 1 || blob >= heaps[1]) return -1;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = ImageBytes.CompressedUnsigned(image, heaps[0] + blob, limit, next);
            if (size < 2 || size > limit - next[0] || image[next[0]] != 6) return -1;
            int end = next[0] + size;
            next[0]++;
            int modifier = ReadFieldModifier(image, heaps, layout, next, end);
            if (modifier < 0) return modifier;
            // IsVolatile changes ordinary access ordering through the IL prefix,
            // not the address of the signed Int32 field in this single-agent heap.
            if (modifier != 0 && !classInt32) return -2;
            if (classInt32)
            {
                int type = ManagedReferenceSignatures.Read(bundle, image, layout, heaps,
                    AssemblyOf(objects, owner), next, end, owner, objects, 0);
                if (type < 0) return type;
                return type == GenericScalar.Key(8) && next[0] == end ? Int32Location : -2;
            }
            int location = ManagedReferenceSignatures.ReadLocation(bundle, image, layout, heaps,
                AssemblyOf(objects, owner), next, end, owner, objects);
            if (location < 0) return location;
            return next[0] == end && Kind(location) != 0 ? location : -2;
        }
    }
}
