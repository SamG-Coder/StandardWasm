using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    internal static class GenericParameters
    {
        private const uint AllowByRefLike = 0x20;

        internal static int Check(byte[] image, int[] heaps, int[] methods, int typeRow, int[] arguments, int[] objects)
        {
            int arity = arguments.Length;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
            if (counts[42] == 0) return arity == 0 ? 0 : -2;
            int table = MetadataTableRows.TableStart(heaps, methods, counts, 42);
            if (table < 0) return -1;
            int ownerWidth = counts[2] < 32768 && counts[6] < 32768 ? 2 : 4;
            int width = 4 + ownerWidth + methods[2];
            int owner = typeRow << 1;
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
                // Permission widens the CLR's allowed argument set. Actual
                // byref-like value layouts are rejected before being registered.
                if ((flags & ~(7u | AllowByRefLike)) != 0) return -2;
                if ((flags & 4) != 0)
                {
                    int argument = arguments[low - first];
                    if (GenericScalar.IsScalar(argument) || ObjectLayouts.IsValueType(objects, argument)) return -2;
                }
                low++;
            }
            if (low - first != arity) return -2;
            if (arity == 0 || counts[44] == 0) return 0;
            int constraints = MetadataTableRows.TableStart(heaps, methods, counts, 44);
            if (constraints < 0) return -1;
            int indexWidth = counts[42] < 65536 ? 2 : 4;
            int constraintWidth = indexWidth + (counts[2] < 16384 && counts[1] < 16384 && counts[27] < 16384 ? 2 : 4);
            for (int i = 0; i < counts[44]; i++)
            {
                int parameter = Index(image, (uint)(constraints + i * constraintWidth), indexWidth);
                if (parameter > first && parameter <= low) return -2; // Nominal constraints require substitution.
            }
            return 0;
        }

        private static int Index(byte[] image, uint at, int width) =>
            width == 2 ? (int)U16(image, at) : unchecked((int)U32(image, at));
    }
}
