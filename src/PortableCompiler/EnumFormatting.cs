using PeLibrary;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class EnumFormatting
    {
        // Reserved owner/row pairs distinguish metadata names from ordinary
        // interned strings and from the shared invariant decimal cache.
        private const int CacheOwner = int.MaxValue - 1, DecimalRow = -1;
        internal const int CacheLimit = 300;
        internal const int Header = 12, Entry = 20;

        internal static int LowerFrame(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int context, int[] methodArguments, int[] objects, int[] records, int[] clauses, int address,
            int frameType, int[] globals, byte[] literals, int[] used)
        {
            int call = records[address + 2] * Width;
            if (call >= records.Length || records[call] != ConstrainedInstance ||
                ValueOperands.Entry(records, call / Width, clauses) ||
                !IsObjectToString(image, layout, heaps, records[call + 3])) return 0;

            int token = records[call + 1], row = token & 0xffffff, table = token >> 24;
            int type = table == 0x1b ? ManagedReferenceSignatures.TypeSpecIdentity(bundle, image, layout, heaps,
                owner, row, context, objects, methodArguments) :
                table == 1 || table == 2 ? BundleLayouts.RegisterType(bundle, image, layout, heaps,
                    owner, row << 2 | (table == 1 ? 1 : 0), objects, valueType: true, enumIdentity: true) : 0;
            if (!ObjectLayouts.IsEnumKey(type)) return 0;
            // Check the actual frame slot before Load normalizes packed values
            // to the evaluation-stack category.
            if (frameType != ObjectLayouts.SignatureStorage(objects, type)) return -2;

            var identity = new int[2];
            ObjectLayouts.EnumIdentity(objects, type, identity);
            int plan = used[0];
            int element = Bind(bundle.Get(identity[0]), identity[0], identity[1], objects, globals, literals, used);
            if (element < 0) return element;
            // The canonical operation reads the value without exposing the
            // receiver's address. The normal Load still observes any live cell.
            records[address] = Load;
            records[call] = FormatEnum;
            records[call + 1] = plan;
            records[call + 3] = element;
            return 1;
        }

        internal static int StackStorage(int element) => element >= 10 ? I64 : I32;

        private static bool IsObjectToString(byte[] image, int[] layout, int[] heaps, int token)
        {
            int row = token & 0xffffff, table = token >> 24;
            if (table == 6)
            {
                var counts = new int[45];
                return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                    row > 0 && row <= counts[6] &&
                    RuntimeTypeMetadata.IsCoreObjectToString(image, heaps, layout, row);
            }
            if (table != 0x0a) return false;
            var member = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, member) < 0 ||
                member[AssemblyReferences.Depth] != 0 ||
                !ImageBytes.MatchesAscii(image, heaps, (uint)member[4], "ToString") ||
                !ObjectLayouts.IsCoreObjectType(image, heaps, layout, member[AssemblyReferences.Leaf] << 2 | 1)) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadInstance(image, heaps, member[5], 0, cursor);
            return end >= 0 && SignatureReader.TakeElement(image, cursor, end, 0x0e) && cursor[0] == end;
        }

        private static int Bind(byte[] image, int owner, int row, int[] objects, int[] globals, byte[] literals, int[] used)
        {
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var shape = new int[5];
            int status = ObjectLayouts.ReadShape(image, heaps, layout, row, shape, true);
            if (status <= 0 || shape[2] == 0) return status < 0 ? status : -2;

            var values = new ulong[shape[2]];
            var names = new int[shape[2]];
            var fields = new int[shape[2]];
            int element = EnumLiteralMetadata.Read(image, heaps, layout, row, shape, values, names, fields);
            if (element < 0) return element;

            var order = new int[shape[2] - 1];
            int count = 0;
            bool sorted = true;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] == 0) continue;
                if (count != 0 && values[order[count - 1]] > values[i]) sorted = false;
                order[count++] = i;
            }
            if (!sorted) Sort(values, order, new int[count], 0, count);

            int distinct = 0;
            for (int i = 0; i < count; i++)
            {
                if (i == 0 || values[order[i - 1]] != values[order[i]]) distinct++;
                // An unstable upstream sort can reorder unsorted aliases.
                // Recognize known choices only when metadata is already sorted.
                else if (!sorted) return -2;
            }

            int cacheVector = ObjectLayouts.RegisterArray(objects, String);
            if (cacheVector < 0) return cacheVector;
            int cache = StaticFields.Register(globals, CacheOwner, DecimalRow, cacheVector);
            if (cache < 0) return cache;
            int plan = used[0];
            if (literals.Length - plan < Header || distinct > (literals.Length - plan - Header) / Entry) return -3;
            int cursor = plan + Header;
            int chars = cursor + distinct * Entry;
            Int(literals, plan, distinct);
            Int(literals, plan + 4, cache);
            Int(literals, plan + 8, cacheVector);

            for (int i = 0; i < count; )
            {
                ulong value = values[order[i]];
                int selected = count <= 32 ? order[i] : Search(values, order, count, value);
                int bytes = EnumLiteralMetadata.CopyName(image, heaps, -names[selected], literals, chars);
                if (bytes < 0) return bytes;
                int global = StaticFields.Register(globals, CacheOwner, fields[selected], String, owner);
                if (global < 0) return global;
                Int(literals, cursor, unchecked((int)value));
                Int(literals, cursor + 4, unchecked((int)(value >> 32)));
                Int(literals, cursor + 8, chars);
                Int(literals, cursor + 12, bytes / 2);
                Int(literals, cursor + 16, global);
                cursor += Entry;
                chars += bytes;
                do { i++; } while (i < count && values[order[i]] == value);
            }
            used[0] = chars;
            return element;
        }

        private static int Search(ulong[] values, int[] order, int count, ulong value)
        {
            int low = 0, high = count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                int index = order[middle];
                if (values[index] == value) return index;
                if (values[index] < value) low = middle + 1;
                else high = middle - 1;
            }
            return -1;
        }

        private static void Sort(ulong[] values, int[] order, int[] staging, int first, int end)
        {
            if (end - first < 2) return;
            int middle = first + (end - first) / 2;
            Sort(values, order, staging, first, middle);
            Sort(values, order, staging, middle, end);
            int left = first, right = middle;
            for (int i = first; i < end; i++)
                staging[i] = left < middle && (right == end || values[order[left]] <= values[order[right]])
                    ? order[left++] : order[right++];
            for (int i = first; i < end; i++) order[i] = staging[i];
        }

        private static void Int(byte[] bytes, int offset, int value)
        {
            for (int i = 0; i < 4; i++) bytes[offset + i] = unchecked((byte)(value >> (8 * i)));
        }
    }
}
