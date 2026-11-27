using PeLibrary;
using static PeLibrary.ImageBytes;
using static PeLibrary.ObjectLayouts;

namespace PortableCompiler
{
    // Each supported query keeps its own data keyed by canonical assembly slot.
    internal sealed class RuntimeAssemblyMetadata
    {
        internal readonly bool Single;
        private readonly string attributeNamespace;
        private readonly string attributeName;
        private readonly int constructorArity;
        internal int AttributeWidth => constructorArity * 2;
        // Distinct from ordinary static fields, string literals and Type names.
        private const int CacheOwner = int.MaxValue - 2;
        private int function = -1;
        internal int EmptyString { get; private set; } = -1;
        internal int EmptyVector { get; private set; } = -1;
        internal int Constructor { get; private set; }
        internal int Attribute { get; private set; }
        internal int Vector { get; private set; }
        internal int[] Starts { get; private set; }
        internal int[] Values { get; private set; }

        internal RuntimeAssemblyMetadata(bool single, string attributeNamespace, string attributeName, int constructorArity)
        {
            Single = single;
            this.attributeNamespace = attributeNamespace;
            this.attributeName = attributeName;
            this.constructorArity = constructorArity;
        }

        internal bool IsQuery(byte[] image, int[] layout, int[] heaps, int owner, int row, int[] objects, int argument)
        {
            if (argument < FirstClass || argument - FirstClass >= objects[0] ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3),
                    Single ? "GetCustomAttribute" : "GetCustomAttributes") ||
                (MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2057) != 0x16 ||
                AssemblyOf(objects, argument) != owner || !IsSealed(objects, argument)) return false;
            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (start < 0 || !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) ||
                !NamedDefinition(image, heaps, layout, counts, start, Owner(image, heaps, layout, row, false),
                    "System.Reflection", "CustomAttributeExtensions") ||
                !NamedDefinition(image, heaps, layout, counts, start, TypeDef(objects, argument),
                    attributeNamespace, attributeName)) return false;

            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob >= heaps[1]) return false;
            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            int length = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            int at = cursor[0];
            if (length < (Single ? 7 : 10) || length > limit - at) return false;
            int end = at + length;
            if (!SignatureReader.TakeElement(image, cursor, end, 0x10) ||
                !SignatureReader.TakeElement(image, cursor, end, 1) ||
                !SignatureReader.TakeElement(image, cursor, end, 1)) return false;
            if (!Single)
            {
                if (!SignatureReader.TakeElement(image, cursor, end, 0x15)) return false;
                var head = new int[2];
                if (SignatureReader.ReadGenericHeader(image, cursor, end, head) != 1 || head[1] != 0x12 || (head[0] & 3) != 0 ||
                    !NamedDefinition(image, heaps, layout, counts, start, head[0] >> 2, "System.Collections.Generic", "IEnumerable`1")) return false;
            }
            if (!SignatureReader.TakeElement(image, cursor, end, 0x1e) ||
                !SignatureReader.TakeElement(image, cursor, end, 0)) return false;
            int receiver = SignatureReader.ReadClass(image, cursor, end);
            return cursor[0] == end && RuntimeTypeMetadata.IsAssemblyDefinition(image, heaps, layout, receiver);
        }

        internal static int CheckConstraint(byte[] image, int[] layout, int[] heaps, int owner,
            int[] objects, int argument, int[] table)
        {
            // The ordinary generic constraint path supports nominal interfaces.
            // The assembly attribute overloads instead constrain their argument to
            // the Attribute class, whose base path is already in the layout.
            var parameter = new int[2];
            int type = 0;
            for (int index = 0; index < table[3]; index++)
            {
                int coded = GenericParameters.ReadDeferred(image, table, index, parameter);
                if (coded == 0) continue;
                if (type != 0 || parameter[0] != 0 || (coded & 3) != 0) return -2;
                type = coded >> 2;
            }
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return -1;
            if (type == 0 || !NamedDefinition(image, heaps, layout, counts, first, type, "System", "Attribute")) return -2;
            int baseClass = Find(objects, owner, type, new int[0]);
            return baseClass != 0 && Inherits(objects, argument, baseClass) ? 0 : -2;
        }

        internal int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner, int row,
            int attribute, int[] objects, int[] globals, byte[] literals, int[] literalSize, RuntimeReflectionPlan reflection,
            int[] owners, int[] rows, int[] contexts, int[] bases, int[] indices, int[] arities, int[] offsets,
            int[] signatures, int functions, int[] result)
        {
            if (function >= 0)
            {
                result[0] = function;
                return functions;
            }
            int status = reflection.Bind(bundle, globals, literals, literalSize);
            if (status < 0) return status;
            int attributeRow = TypeDef(objects, attribute);
            var range = new int[2];
            status = VirtualSlotMetadata.ReadMethodRange(image, heaps, layout, attributeRow, range);
            if (status < 0) return status;
            int ctor = 0;
            for (int candidate = range[0]; candidate < range[1]; candidate++)
            {
                if (!IsConstructor(image, heaps, layout, candidate) ||
                    !AssemblyAttributeRecords.IsStringConstructor(image, heaps, layout, candidate << 3 | 2, constructorArity)) continue;
                if (ctor != 0) return -5;
                ctor = candidate;
            }
            if (ctor == 0) return -2;
            int constructor = MethodCompiler.Lookup(owners, rows, contexts, bases, indices, functions, owner, ctor, 0);
            if (constructor < 0)
            {
                constructor = functions;
                functions = MethodCompiler.Register(bundle, image, layout, heaps, owner, ctor, 0,
                    owners, rows, contexts, bases, indices, arities, offsets, signatures, objects, functions);
                if (functions < 0) return functions;
            }

            int vector = Single ? 0 : RegisterArray(objects, attribute);
            if (vector < 0) return vector;
            int offset = MethodCompiler.NextSignature(offsets, arities, functions);
            if (functions >= owners.Length || signatures.Length - offset < 2) return -3;
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            var arguments = new[] { attribute };
            int arity = SignatureTypes.Read(bundle, image, heaps, blob, signatures, offset,
                0, layout, objects, owner, 0, arguments);
            if (arity < 0) return arity;
            if (arity != 1 || signatures[offset + 1] != RuntimeTypeMetadata.Assembly ||
                (Single ? signatures[offset] != attribute : !IsExactArrayEnumerable(objects, vector, signatures[offset]))) return -2;

            status = ReadAttributes(bundle, owner, attributeRow, globals, literals, literalSize);
            if (status < 0) return status;
            for (int module = 1; !Single && module <= bundle.Count; module++)
            {
                if (Starts[module] != Starts[module + 1]) continue;
                // CoreCLR's reflection cache supplies a shared typed empty
                // array. Mono allocates its empty array for every query.
                var helper = new int[2];
                status = RuntimeTypeTokens.FindCoreType(bundle, "SZArrayHelper", helper);
                if (status < 0 && status != -6) return status;
                if (status == 0 && helper[0] == owner)
                {
                    EmptyVector = StaticFields.Register(globals, CacheOwner, 0, vector);
                    if (EmptyVector < 0) return EmptyVector;
                }
                break;
            }
            Attribute = attribute;
            Vector = vector;
            Constructor = constructor;
            arities[functions] = 1;
            offsets[functions] = offset;
            result[0] = function = functions;
            return functions + 1;
        }

        private int ReadAttributes(BundleImages bundle, int core, int attribute, int[] globals, byte[] literals, int[] size)
        {
            Starts = new int[bundle.Count + 2];
            Values = new int[AttributeWidth * 4];
            int used = 0;
            var layout = new int[4];
            var heaps = new int[8];
            var table = new int[3];
            var entry = new int[2];
            var pair = new int[AttributeWidth];
            for (int owner = 0; owner < bundle.Count; owner++)
            {
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                var counts = new int[45];
                int count = AssemblyAttributeRecords.Find(image, layout, heaps, counts, table);
                if (count < 0) return count;
                for (int row = 0; row < count; row++)
                {
                    if (!AssemblyAttributeRecords.Read(image, layout, table, row, entry)) continue;
                    int constructor = entry[0];
                    int type = AttributeConstructors.ReadType(image, heaps, layout, counts, constructor);
                    if (type < 0) return type;
                    int match = IdentifyAttribute(bundle, image, layout, heaps, owner, type, core, attribute);
                    if (match < 0) return match;
                    if (match == 0) continue;
                    // This plan supports one instance of the neutral language attribute.
                    if (Single && used != Starts[owner + 1]) return -2;
                    if (!AssemblyAttributeRecords.IsStringConstructor(image, heaps, layout, constructor, constructorArity)) return -2;
                    int status = AssemblyAttributeRecords.ReadStrings(image, heaps, entry[1], literals, size, pair, constructorArity);
                    if (status < 0) return status;
                    for (int index = 0; EmptyString < 0 && index < AttributeWidth; index += 2)
                    {
                        if (pair[index] < 0 || pair[index + 1] != 0) continue;
                        EmptyString = StringLiterals.Empty(globals);
                        if (EmptyString < 0) return EmptyString;
                    }
                    if (used == Values.Length)
                    {
                        if (used > int.MaxValue / 2) return -3;
                        var grown = new int[used * 2];
                        for (int index = 0; index < used; index++) grown[index] = Values[index];
                        Values = grown;
                    }
                    for (int index = 0; index < AttributeWidth; index++) Values[used++] = pair[index];
                }
                Starts[owner + 2] = used;
            }
            return 0;
        }

        private int IdentifyAttribute(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int type, int core, int attribute)
        {
            if ((type & 7) == 0) return owner == core && type >> 3 == attribute ? 1 : 0;
            if ((type & 7) != 1) return 0;
            var reference = new int[AssemblyReferences.SymbolWidth];
            int status = AssemblyReferences.ReadType(image, heaps, layout, type >> 3, reference);
            if (status < 0) return status;
            if (reference[AssemblyReferences.Depth] != 0 ||
                !MatchesAscii(image, heaps, (uint)reference[2], attributeName) ||
                !MatchesAscii(image, heaps, (uint)reference[3], attributeNamespace)) return 0;
            var identity = new int[2];
            status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, (type >> 3) << 2 | 1, identity);
            if (status < 0) return status;
            return identity[0] == core && identity[1] == attribute ? 1 : 0;
        }

        internal int Write(byte[] code, int used, int[] offsets, int[] sizes, int imports, RuntimeReflectionPlan reflection)
        {
            if (function < 0) return used;
            offsets[function] = used;
            int end = RuntimeAssemblyMetadataWasm.Write(code, used, imports, reflection, this);
            if (end >= 0) sizes[function] = end - used;
            return end;
        }
    }
}
