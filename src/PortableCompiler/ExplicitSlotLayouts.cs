using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ExplicitSlotLayouts
    {
        internal static int Bind(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int type, int[] objects)
        {
            int fields = FieldCount(objects, type), first = FirstRow(objects, type);
            int table = TableOf(objects, type);
            var offsets = new int[fields];
            int read = ExplicitValueLayouts.Read(image, heaps, layout, TypeDef(objects, type), first, offsets, wide: true);
            if (read < 0) return read;
            var storage = new int[fields];
            var widths = new int[fields];
            var order = new int[fields];
            var slots = new int[fields];
            var reference = new int[3];
            int count = 0;
            for (int field = 0; field < fields; field++)
            {
                int stored = BundleLayouts.ReadFieldStorage(bundle, image, layout, heaps, owner, table, first + field,
                    type, objects, reference, false);
                if (stored < 0) return stored;
                if (stored == 0) continue;
                if (offsets[field] == 0) return -1;
                bool traced = IsTraced(objects, stored);
                // Use the larger CLR pointer width to prove separation. The
                // Wasm GC slot never stores or exposes the underlying address.
                int width = traced ? ExplicitSlotWidth : ExplicitValueLayouts.Width(stored);
                if (width == 0 || reference[2] == 2 || traced && (offsets[field] - 1) % width != 0) return -2;
                storage[field] = stored;
                widths[field] = width;
                int at = count++;
                while (at > 0 && offsets[order[at - 1]] > offsets[field])
                {
                    order[at] = order[at - 1];
                    at--;
                }
                order[at] = field;
            }

            int slot = 0;
            for (int start = 0; start < count;)
            {
                int firstField = order[start], begin = offsets[firstField], end = begin + widths[firstField];
                int physical = storage[firstField];
                bool traced = IsTraced(objects, physical);
                int limit = start + 1;
                while (limit < count && offsets[order[limit]] < end)
                {
                    int other = order[limit++];
                    if (traced != IsTraced(objects, storage[other])) return -2;
                    if (traced)
                    {
                        if (offsets[other] != begin) return -2;
                        if (physical != storage[other]) physical = ObjectReference;
                    }
                    else
                    {
                        int next = offsets[other] + widths[other];
                        if (next > end) end = next;
                        if (end - begin > ExplicitSlotWidth) return -2;
                        physical = I64;
                    }
                }
                slots[slot] = physical;
                for (int index = start; index < limit; index++)
                {
                    int field = order[index];
                    int projection = slot * ExplicitSlotWidth + offsets[field] - begin;
                    if (projection >= ushort.MaxValue) return -3;
                    SetExplicitField(objects, type, field, storage[field], projection);
                }
                slot++;
                start = limit;
            }
            return SetExplicitSlots(objects, type, slots, slot) ? type : -3;
        }

        private static bool IsTraced(int[] objects, int storage) =>
            IsObjectReference(storage) && !IsValueType(objects, storage) && !RuntimeTypeMetadata.IsReference(storage);
    }
}
