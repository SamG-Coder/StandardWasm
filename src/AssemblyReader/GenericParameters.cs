using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class GenericParameters
    {
        private const uint Variance = 3, Reference = 4, NonNullableValue = 8,
            DefaultConstructor = 0x10, AllowByRefLike = 0x20;

        internal static int Check(byte[] image, int[] heaps, int[] methods, int typeRow, int[] arguments, int[] objects, int[] deferred, bool method = false)
        {
            int arity = arguments.Length;
            var counts = new int[45];
            int start = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (start < 0) return -1;
            if (counts[42] == 0) return arity == 0 ? 0 : -2;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 42);
            if (table < 0) return -1;
            int ownerWidth = counts[2] < 32768 && counts[6] < 32768 ? 2 : 4;
            int width = 4 + ownerWidth + methods[2];
            int owner = (typeRow << 1) | (method ? 1 : 0);
            // GenericParam is sorted by owner, then by parameter number.
            int low = 0, high = counts[42];
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (Index(image, (uint)(table + mid * width + 4), ownerWidth) < owner) low = mid + 1;
                else high = mid;
            }
            int first = low;
            while (low < counts[42])
            {
                uint at = (uint)(table + low * width);
                if (Index(image, at + 4, ownerWidth) != owner) break;
                if (low - first >= arity || (int)U16(image, at) != low - first) return -2;
                uint flags = U16(image, at + 2);
                // Permission widens the CLR's allowed argument set. The compiler
                // keeps scoped value layouts out of generic specialization for now.
                if (ObjectLayouts.IsByRefLikeValue(objects, arguments[low - first])) return -2;
                if ((flags & ~(Variance | Reference | NonNullableValue | DefaultConstructor | AllowByRefLike)) != 0)
                    return -2;
                bool requiresValue = (flags & NonNullableValue) != 0;
                if ((flags & DefaultConstructor) != 0 && !requiresValue) return -2;
                if ((flags & Reference) != 0 || requiresValue)
                {
                    int argument = arguments[low - first];
                    bool value = GenericScalar.IsScalar(argument) || ObjectLayouts.IsEnumKey(argument) ||
                        ObjectLayouts.IsValueType(objects, argument);
                    if ((flags & Reference) != 0 && value ||
                        requiresValue && (!value || ObjectLayouts.IsNullableValueType(objects, argument))) return -2;
                }
                low++;
            }
            if (low - first != arity) return -2;
            if (arity == 0 || counts[44] == 0) return 0;
            int constraints = MetadataTableRows.TableStart(heaps, methods, counts, 44);
            if (constraints < 0) return -1;
            int indexWidth = counts[42] < 65536 ? 2 : 4;
            int typeWidth = counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4;
            int constraintWidth = indexWidth + typeWidth;
            bool hasDeferred = false;
            for (int i = 0; i < counts[44]; i++)
            {
                uint at = (uint)(constraints + i * constraintWidth);
                int parameter = Index(image, at, indexWidth);
                if (parameter <= first || parameter > low) continue;
                uint flags = U16(image, (uint)(table + (parameter - 1) * width + 2));
                int type = Index(image, at + (uint)indexWidth, typeWidth);
                int tag = type & 3;
                if (type >> 2 < 1 || tag > 2) return -1;
                // A plain nominal ValueType row supplements the value-type bit.
                if (tag != 2 && (flags & NonNullableValue) != 0 &&
                    ObjectLayouts.IsPlatformValueType(image, heaps, methods, counts, start, type)) continue;
                hasDeferred = true;
            }
            if (!hasDeferred) return 0;
            deferred[0] = constraints;
            deferred[1] = indexWidth;
            deferred[2] = typeWidth;
            deferred[3] = counts[44];
            deferred[4] = first;
            deferred[5] = low;
            deferred[6] = table;
            deferred[7] = width;
            return 1;
        }

        internal static int ReadDeferred(byte[] image, int[] table, int index, int[] parameter)
        {
            uint at = (uint)(table[0] + index * (table[1] + table[2]));
            int owner = Index(image, at, table[1]);
            if (owner <= table[4] || owner > table[5]) return 0;
            int type = Index(image, at + (uint)table[1], table[2]);
            parameter[0] = owner - table[4] - 1;
            parameter[1] = (U16(image, (uint)(table[6] + (owner - 1) * table[7] + 2)) & NonNullableValue) != 0 ? 1 : 0;
            return type;
        }

        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
