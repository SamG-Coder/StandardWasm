using PeLibrary;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    internal sealed class RuntimeTypeFields
    {
        private int[] fields = new int[8];
        private int used;

        internal bool UsesDescriptor(int owner, int row)
        {
            for (int index = 0; index < used; index += 2)
                if (fields[index] == owner && fields[index + 1] == row) return true;
            return false;
        }

        internal static bool IsDeclaringConstructor(byte[] image, int[] heaps, int[] layout,
            int owner, int caller, int[] objects, int declaring) =>
            ObjectLayouts.AssemblyOf(objects, declaring) == owner &&
            (MethodDefinitions.ReadColumn(image, layout, caller, 2) & 0x1850) == 0x1800 &&
            ObjectLayouts.IsConstructor(image, heaps, layout, caller) &&
            ObjectLayouts.Owner(image, heaps, layout, caller, false) == ObjectLayouts.TypeDef(objects, declaring);

        internal bool RequireInstance(BundleImages bundle, byte[] image, int[] heaps, int[] layout,
            int owner, int caller, int[] objects, int descriptor, int ordinal, bool reading)
        {
            int declaring = FieldAccess.Owner(descriptor);
            if (ObjectLayouts.IsExplicitValue(objects, declaring) || ObjectLayouts.IsInlineArray(objects, declaring) ||
                !RuntimeTypeLocals.IsLinkedSystemType(bundle, objects, FieldAccess.Storage(descriptor)) ||
                !reading && !IsDeclaringConstructor(image, heaps, layout, owner, caller, objects, declaring)) return false;
            int declaringOwner = ObjectLayouts.AssemblyOf(objects, declaring);
            if (declaringOwner != owner)
            {
                image = bundle.Get(declaringOwner);
                heaps = new int[8];
                layout = new int[4];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return false;
            }
            int first = ObjectLayouts.FirstRow(objects, declaring);
            int end = first + ObjectLayouts.DeclaredFieldCount(objects, declaring);
            var field = new int[3];
            for (int row = first; row < end; row++)
            {
                if (ObjectLayouts.FieldOrdinal(objects, declaring, row) != ordinal) continue;
                AssemblyReferences.ReadField(image, layout, ObjectLayouts.TableOf(objects, declaring), row, field);
                if ((field[0] & 0x30) != 0x20 || UsesDescriptor(declaringOwner, row)) return false;
                var reference = new int[2];
                int storage = ObjectLayouts.ReadStorage(image, heaps, layout,
                    ObjectLayouts.TableOf(objects, declaring), row, reference, false);
                if (storage < 0 || reference[0] <= 0 || reference[1] == 1 ||
                    RuntimeTypeMetadata.Storage(image, heaps, layout, reference[0], false) != RuntimeTypeMetadata.Type) return false;
                // Layouts are recreated on re-emission. A stable FieldDef key
                // gives every reachable constructor and reader the same storage.
                // The verifier checks all generated stores against that type.
                if (used == fields.Length)
                {
                    var grown = new int[fields.Length * 2];
                    for (int index = 0; index < used; index++) grown[index] = fields[index];
                    fields = grown;
                }
                fields[used++] = declaringOwner;
                fields[used++] = row;
                return true;
            }
            return false;
        }

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
