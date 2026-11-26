using PeLibrary;

namespace PortableCompiler
{
    internal static class AssemblyBundle
    {
        internal const int ObjectOwner = -1; // Canonical root has no emitted TypeDef slot.
        // An explicit sequence of [little-endian length, complete PE image].
        internal static int Count(byte[] bundle)
        {
            int count = 0, cursor = 0;
            while (cursor < bundle.Length)
            {
                if (bundle.Length - cursor < 4) return -1;
                uint length = ImageBytes.U32(bundle, (uint)cursor);
                cursor += 4;
                if (length == 0 || length > (uint)(bundle.Length - cursor)) return -1;
                cursor += (int)length;
                count++;
            }
            return count == 0 ? -1 : count;
        }

        internal static byte[] Load(byte[] main, byte[] bundle, int[] ranges, int owner)
        {
            if (owner == 0) return main;
            int slot = (owner - 1) * 2;
            return Copy(bundle, ranges[slot], ranges[slot + 1]);
        }

        internal static byte[] Copy(byte[] bundle, int position, int length)
        {
            var image = new byte[length];
            for (int i = 0; i < length; i++)
                image[i] = bundle[position + i];
            return image;
        }

        // symbol receives [owner, MethodDef, constructor declaring TypeDef or 0,
        // closed declaring layout or 0]. context is the caller's own closed
        // declaring layout, which substitutes VAR in a TypeSpec parent.
        internal static int Resolve(byte[] main, byte[] bundle, int[] ranges, byte[] caller,
            int[] layout, int[] heaps, int owner, int member, int opcode, bool baseCall, int[] objects, int[] symbol, int context, int callerType, bool allowVirtual = false)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            int status = AssemblyReferences.ReadMember(caller, heaps, layout, member, reference, allowClosedParent: true);
            if (status < 0) return status;
            var binding = new int[2];
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            var range = new int[2];
            byte[] target;
            int matched, declaring, closed = 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent != 0)
            {
                // The closed layout already proved the definition's identity,
                // type accessibility and argument constraints.
                closed = ManagedReferenceSignatures.ReadInstantiation(main, bundle, ranges, caller, layout, heaps,
                    owner, parent, objects, context);
                if (closed < ObjectLayouts.FirstClass) return closed < 0 ? closed : -2;
                // Value receivers are managed pointers, and interface members
                // need dispatch. Both are outside direct specialization.
                if (ObjectLayouts.IsValueType(objects, closed) || ObjectLayouts.IsInterface(objects, closed)) return -2;
                matched = ObjectLayouts.AssemblyOf(objects, closed);
                declaring = ObjectLayouts.TypeDef(objects, closed);
                target = Load(main, bundle, ranges, matched);
                if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
                status = ObjectLayouts.MethodRange(target, targetHeaps, targetLayout, declaring, range);
                if (status < 0) return status;
            }
            else
            {
                target = ResolveType(main, bundle, ranges, caller, heaps, layout, reference, false,
                    binding, targetLayout, targetHeaps, range);
                matched = binding[0];
                declaring = binding[1];
                if (declaring < 0) return declaring;
            }
            int row = 0;
            for (int candidate = range[0]; candidate < range[1]; candidate++)
            {
                int flags = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 2);
                int access = flags & 7;
                // A closed parent in the caller's own assembly may name assembly
                // members, and private members of the caller's own definition.
                // Nested-type and friend access are not modeled and stay unbound.
                bool local = closed != 0 && matched == owner &&
                    (access == 3 || access == 5 || access == 1 && callerType == declaring);
                if (access != 6 && !local && !(baseCall && opcode == 0x28 && (access == 4 || access == 5))) continue;
                bool isStatic = (flags & 0x10) != 0;
                if (isStatic && opcode != 0x28) continue;
                int name = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 3);
                if (!AssemblyReferences.SameString(caller, heaps, reference[4], target, targetHeaps, name))
                    continue;
                int signature = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 4);
                int match = BundleSignatures.Match(main, bundle, ranges, caller, layout, heaps, owner, reference[5],
                    target, targetLayout, targetHeaps, matched, signature, false, objects, closed);
                if (match < 0) return match;
                if (match == 0) continue;
                // Bind the declaration. The caller may defer virtual slot
                // selection until it has proved the receiver's type.
                bool constructor = ObjectLayouts.IsConstructor(target, targetHeaps, targetLayout, candidate);
                if (opcode == 0x73)
                {
                    if (!constructor || (flags & 0x1800) != 0x1800) return -2;
                }
                else if (constructor ? !baseCall || opcode != 0x28 : access != 6 && !local) return -2;
                if (opcode == 0x6f && (flags & 0x40) != 0 && !allowVirtual) return -2;
                if (row != 0) return -5;
                row = candidate;
                symbol[2] = constructor ? declaring : 0;
                if (allowVirtual) symbol[4] = flags & 0x40;
            }
            if (row == 0) return -6;
            symbol[0] = matched;
            symbol[1] = row;
            symbol[3] = closed;
            return 0;
        }

        internal static int ResolveField(byte[] main, byte[] bundle, int[] ranges, byte[] caller,
            int[] layout, int[] heaps, int owner, int member, bool write, bool staticField, int[] objects, int[] globals, int[] field, int context, int callerType)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            int status = AssemblyReferences.ReadMember(caller, heaps, layout, member, reference, allowClosedParent: true);
            if (status < 0) return status;
            var binding = new int[2];
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            var range = new int[2];
            byte[] target;
            int matched, declaringType, type = 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent != 0)
            {
                if (staticField) return -2;
                type = ManagedReferenceSignatures.ReadInstantiation(main, bundle, ranges, caller, layout, heaps,
                    owner, parent, objects, context);
                if (type < ObjectLayouts.FirstClass) return type < 0 ? type : -2;
                matched = ObjectLayouts.AssemblyOf(objects, type);
                declaringType = ObjectLayouts.TypeDef(objects, type);
                target = AssemblyBundle.Load(main, bundle, ranges, matched);
                if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
                range[0] = ObjectLayouts.FirstRow(objects, type);
                range[1] = range[0] + ObjectLayouts.FieldCount(objects, type);
            }
            else
            {
                target = ResolveType(main, bundle, ranges, caller, heaps, layout, reference, true,
                    binding, targetLayout, targetHeaps, range);
                matched = binding[0];
                declaringType = binding[1];
                if (declaringType < 0) return declaringType;
            }

            int table = AssemblyReferences.FieldTable(target, targetHeaps, targetLayout);
            if (table < 0) return table;
            var definition = new int[3];
            int row = 0, flags = 0;
            for (int candidate = range[0]; candidate < range[1]; candidate++)
            {
                AssemblyReferences.ReadField(target, targetLayout, table, candidate, definition);
                if (!AssemblyReferences.SameString(caller, heaps, reference[4], target, targetHeaps, definition[1]))
                    continue;
                int match = BundleSignatures.Match(main, bundle, ranges, caller, layout, heaps, owner, reference[5],
                    target, targetLayout, targetHeaps, matched, definition[2], true, objects, type);
                if (match < 0) return match;
                if (match == 0) continue;
                // Closed parents in the caller's assembly follow the same rule
                // as methods: assembly members, and private members of the
                // caller's own definition.
                flags = definition[0];
                int access = flags & 7;
                bool local = parent != 0 && matched == owner &&
                    (access == 3 || access == 5 || access == 1 && callerType == declaringType);
                if (((flags & 0x10) != 0) != staticField || (access != 6 && !local) ||
                    (write && (flags & 0x20) != 0)) return -2;
                if (row != 0) return -5;
                row = candidate;
            }
            if (row == 0) return -6;
            if (staticField)
                return StaticFields.Bind(main, bundle, ranges, target, targetLayout, targetHeaps, matched, declaringType,
                    row, table, flags, write, objects, globals, field);
            if (type == 0) type = ObjectLayouts.Find(objects, matched, declaringType, new int[0]);
            if (type == 0)
                type = BundleLayouts.Register(main, bundle, ranges, target, targetLayout, targetHeaps, matched, declaringType, objects);
            if (type < 0) return type;
            int index = ObjectLayouts.FieldOrdinal(objects, type, row);
            if (index < 0) return index;
            field[0] = index;
            // The receiver layout and value type come from the same owning DLL.
            field[1] = FieldAccess.Encode(type, ObjectLayouts.FieldType(objects, type, row));
            return 0;
        }

        internal static int ClassIdentity(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] symbol)
        {
            int row = coded >> 2;
            if (row < 1) return -1;
            if (ObjectLayouts.IsCoreObjectType(image, heaps, layout, coded))
            {
                symbol[0] = ObjectOwner;
                symbol[1] = 0;
                return 0;
            }
            if ((coded & 3) == 0)
            {
                symbol[0] = owner;
                symbol[1] = row;
                return 0;
            }
            if ((coded & 3) != 1 || bundle.Length == 0) return -2;
            var reference = new int[AssemblyReferences.SymbolWidth];
            int status = AssemblyReferences.ReadType(image, heaps, layout, row, reference);
            if (status < 0) return status;
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            byte[] target = ResolveType(main, bundle, ranges, image, heaps, layout, reference, false,
                symbol, targetLayout, targetHeaps, new int[2]);
            if (symbol[1] < 0) return symbol[1];
            if (ObjectLayouts.IsCoreObjectType(target, targetHeaps, targetLayout, symbol[1] << 2))
            {
                symbol[0] = ObjectOwner;
                symbol[1] = 0;
            }
            return 0;
        }

        // Find the final TypeDef. Keep the caller's type/member names and
        // signature fixed while each facade supplies a fresh AssemblyRef.
        // binding carries (owner, TypeDef) on success, or a negative status in slot 1.
        private static byte[] ResolveType(byte[] main, byte[] bundle, int[] ranges, byte[] caller,
            int[] heaps, int[] layout, int[] reference, bool fields, int[] binding, int[] targetLayout,
            int[] targetHeaps, int[] range)
        {
            int owner = FindOwner(main, bundle, ranges, caller, heaps, layout, reference);
            int status = owner;
            for (int hop = 0; owner >= 0 && hop <= ranges.Length / 2; hop++)
            {
                status = -1;
                byte[] target = Load(main, bundle, ranges, owner);
                if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) break;
                status = AssemblyReferences.FindType(caller, heaps, layout, reference,
                    target, targetHeaps, targetLayout, range, fields);
                if (status > 0)
                {
                    binding[0] = owner;
                    binding[1] = status;
                    return target;
                }
                if (status != -6) break;

                int forward = TypeForwarders.Find(caller, heaps, reference, target, targetHeaps, targetLayout);
                status = forward;
                if (status < 0) break;
                var next = new int[AssemblyReferences.SymbolWidth];
                status = AssemblyReferences.ReadAssembly(target, targetHeaps, targetLayout, forward, next);
                if (status < 0) break;
                owner = FindOwner(main, bundle, ranges, target, targetHeaps, targetLayout, next);
                // A fixed type name can visit each bundled owner only once.
                status = owner < 0 ? owner : -1;
            }
            binding[1] = status;
            return main;
        }

        private static int FindOwner(byte[] main, byte[] bundle, int[] ranges, byte[] caller, int[] heaps, int[] layout, int[] reference)
        {
            int matched = -1;
            var candidateLayout = new int[4];
            var candidateHeaps = new int[8];
            for (int owner = 0; owner <= ranges.Length / 2; owner++)
            {
                byte[] candidate = Load(main, bundle, ranges, owner);
                if (CompressedTables.FindWithHeaps(candidate, candidateLayout, candidateHeaps) < 0)
                    return -1;
                int match = AssemblyReferences.Matches(caller, heaps, layout, reference, candidate, candidateHeaps, candidateLayout);
                if (match < 0) return match;
                if (match == 0) continue;
                if (matched >= 0) return -5;
                matched = owner;
            }
            return matched < 0 ? -4 : matched;
        }
    }
}
