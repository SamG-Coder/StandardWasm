using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal sealed class AssemblyTypeLookup
    {
        private const int CacheOwner = int.MaxValue - 21;
        internal const int EntryWidth = 3, NameOffset = 0, NameLength = 1, Descriptor = 2;
        internal int Function { get; private set; } = -1;
        internal int Catalog { get; private set; }
        internal int CatalogStart { get; private set; }
        internal int CatalogLength { get; private set; }
        internal int[] Entries { get; private set; }
        internal int[] ModuleStarts { get; private set; }
        internal byte[] HasExports { get; private set; }

        internal static bool HasExactFlags(int[] records, int call, int[] clauses)
        {
            int ignoreCase = RuntimeTypeRelations.Previous(records, call, clauses);
            int throwOnError = ignoreCase < 0 ? -1 : RuntimeTypeRelations.Previous(records, ignoreCase, clauses);
            return throwOnError >= 0 && records[throwOnError] == Constant && records[throwOnError + 1] == 0 &&
                records[ignoreCase] == Constant && records[ignoreCase + 1] == 0;
        }

        internal int Lower(BundleImages bundle, RuntimeReflectionPlan reflection, int[] records, int call, int[] clauses,
            int[] globals, byte[] literals, int[] literalSize, int[] arities, int[] offsets, int[] signatures, int function)
        {
            if (!HasExactFlags(records, call, clauses)) return -2;
            int result = Bind(bundle, reflection, globals, literals, literalSize, arities, offsets, signatures, function);
            if (result < 0) return result;
            records[call] = Call;
            records[call + 1] = Function;
            records[call + 3] = 0; // null and argument checks remain in the helper
            return result;
        }

        internal bool ReturnsLookupResult(int[] records, int[] clauses)
        {
            if (Function < 0) return false;
            bool lookup = false;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != Return) continue;
                int value = RuntimeTypeRelations.Previous(records, slot, clauses);
                if (value < 0) return false;
                if (records[value] == NullLiteral) continue;
                if (records[value] != Call || records[value + 1] != Function) return false;
                lookup = true;
            }
            return lookup;
        }

        private int Bind(BundleImages bundle, RuntimeReflectionPlan reflection, int[] globals,
            byte[] literals, int[] literalSize, int[] arities, int[] offsets, int[] signatures, int function)
        {
            if (Function >= 0) return function;
            if (arities.Length - function < 2) return -3;
            int offset = MethodCompiler.NextSignature(offsets, arities, function);
            if (signatures.Length - offset < 11) return -3;
            int status = reflection.Bind(bundle, globals, literals, literalSize);
            if (status < 0) return status;
            status = BuildCatalog(bundle, globals, literals, literalSize);
            if (status < 0) return status;

            Function = function;
            offsets[function] = offset;
            arities[function] = 4;
            signatures[offset] = RuntimeTypeMetadata.Type;
            signatures[offset + 1] = RuntimeTypeMetadata.Assembly;
            signatures[offset + 2] = String;
            signatures[offset + 3] = I32;
            signatures[offset + 4] = I32;

            offsets[function + 1] = offset + 5;
            arities[function + 1] = 5;
            signatures[offset + 5] = I32;
            signatures[offset + 6] = Chars;
            signatures[offset + 7] = I32;
            signatures[offset + 8] = Chars;
            signatures[offset + 9] = I32;
            signatures[offset + 10] = I32;
            return function + 2;
        }

        private int BuildCatalog(BundleImages bundle, int[] globals, byte[] literals, int[] literalSize)
        {
            var modules = new int[bundle.Count + 1];
            var exported = new byte[bundle.Count];
            var records = new int[32 * EntryWidth];
            var layout = new int[4];
            var heaps = new int[8];
            int used = 0;
            int start = literalSize[0];
            int end = start;

            for (int owner = 0; owner < bundle.Count; owner++)
            {
                byte[] image = bundle.Get(owner);
                var counts = new int[45];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0 ||
                    MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
                modules[owner] = used;
                exported[owner] = counts[39] != 0 || counts[38] != 0 ? (byte)1 : (byte)0;
                int objectRow = PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)
                    ? ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System", "Object") : 0;
                if (objectRow < 0) return objectRow;

                for (int row = 2; row <= counts[2]; row++)
                {
                    int text = RuntimeTypeNames.AppendNested(image, layout, heaps, counts, row, literals, end, 0);
                    if (text < 0) return text;
                    int global = row == objectRow
                        ? RuntimeTypeTokens.BindIdentity(globals, AssemblyBundle.ObjectOwner, 0)
                        : RuntimeTypeTokens.BindIdentity(globals, owner, row);
                    if (global < 0) return global;
                    if (used > records.Length - EntryWidth)
                    {
                        if (records.Length > int.MaxValue / 2) return -3;
                        var grown = new int[records.Length * 2];
                        for (int i = 0; i < records.Length; i++) grown[i] = records[i];
                        records = grown;
                    }
                    records[used + NameOffset] = (end - start) / 2;
                    records[used + NameLength] = (text - end) / 2;
                    records[used + Descriptor] = global;
                    used += EntryWidth;
                    end = text;
                }
            }

            int cache = StaticFields.Register(globals, CacheOwner, 0, String);
            if (cache < 0) return cache;
            Catalog = cache;
            CatalogStart = start;
            CatalogLength = (end - start) / 2;
            literalSize[0] = end;
            modules[bundle.Count] = used;
            ModuleStarts = modules;
            HasExports = exported;
            Entries = records;
            return 0;
        }
    }
}
