using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal static class RuntimeTypeFields
    {
        // An initonly static can only be written by its declaring initializer.
        // A straight-line ldtoken / GetTypeFromHandle / stsfld proves the
        // slot holds a canonical descriptor or the initial null. The normal
        // initializer and field access still run at their existing IL points.
        internal static bool HasCanonicalInitializer(byte[] image, int[] heaps, int[] methods, int initializer, int field)
        {
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, methods, initializer, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[3] != 0 || body[5] != 0 ||
                body[1] > int.MaxValue / Width) return false;
            var records = new int[body[1] * Width];
            if (Decode(image, body[0], body[1], 0, 0, records, new int[0], false) < 0) return false;
            int previous = -1, beforeCall = -1;
            bool found = false;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                if (kind == 0 || kind == Nop) continue;
                if (kind == Branch || kind == Conditional || kind == Switch || kind == Leave) return false;
                if (kind == StoreStaticField && records[slot + 3] == 0 && records[slot + 1] == field)
                {
                    if (beforeCall < 0 || records[beforeCall] != LoadTypeToken ||
                        records[previous] != MemberCall || records[previous + 3] != 0x28 ||
                        !RuntimeTypeMetadata.IsGetTypeFromHandle(image, heaps, methods, records[previous + 1])) return false;
                    found = true;
                }
                beforeCall = previous;
                previous = slot;
            }
            return found;
        }
    }
}
