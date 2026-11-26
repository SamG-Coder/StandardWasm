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

        internal static int Resolve(byte[] main, byte[] bundle, int[] ranges, byte[] caller,
            int[] layout, int[] heaps, int owner, int member, int opcode, bool baseCall, int[] objects, int[] symbol)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            int status = AssemblyReferences.ReadMember(caller, heaps, layout, member, reference);
            if (status < 0) return status;
            var binding = new int[2];
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            var range = new int[2];
            byte[] target = ResolveType(main, bundle, ranges, caller, heaps, layout, reference, false,
                binding, targetLayout, targetHeaps, range);
            int matched = binding[0], declaring = binding[1];
            if (declaring < 0) return declaring;
            int row = 0;
            for (int candidate = range[0]; candidate < range[1]; candidate++)
            {
                int flags = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 2);
                int access = flags & 7;
                if (access != 6 && !(baseCall && opcode == 0x28 && (access == 4 || access == 5))) continue;
                bool isStatic = (flags & 0x10) != 0;
                if (isStatic && opcode != 0x28) continue;
                int name = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 3);
                if (!AssemblyReferences.SameString(caller, heaps, reference[4], target, targetHeaps, name))
                    continue;
                int signature = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 4);
                int match = BundleSignatures.Match(main, bundle, ranges, caller, layout, heaps, owner, reference[5],
                    target, targetLayout, targetHeaps, matched, signature, false, objects);
                if (match < 0) return match;
                if (match == 0) continue;
                // Direct calls can bind a known body. callvirt additionally
                // needs a null check and can only bind nonvirtual methods here.
                bool constructor = ObjectLayouts.IsConstructor(target, targetHeaps, targetLayout, candidate);
                if (opcode == 0x73)
                {
                    if (!constructor || (flags & 0x1800) != 0x1800) return -2;
                }
                else if (constructor ? !baseCall || opcode != 0x28 : access != 6) return -2;
                if (opcode == 0x6f && (flags & 0x40) != 0) return -2;
                if (row != 0) return -5;
                row = candidate;
                symbol[2] = constructor ? declaring : 0;
            }
            if (row == 0) return -6;
            symbol[0] = matched;
            symbol[1] = row;
            return 0;
        }

        internal static int ResolveField(byte[] main, byte[] bundle, int[] ranges, byte[] caller,
            int[] layout, int[] heaps, int owner, int member, bool write, int[] objects, int[] field)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            int status = AssemblyReferences.ReadMember(caller, heaps, layout, member, reference);
            if (status < 0) return status;
            var binding = new int[2];
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            var range = new int[2];
            byte[] target = ResolveType(main, bundle, ranges, caller, heaps, layout, reference, true,
                binding, targetLayout, targetHeaps, range);
            int matched = binding[0], declaringType = binding[1];
            if (declaringType < 0) return declaringType;

            int table = AssemblyReferences.FieldTable(target, targetHeaps, targetLayout);
            if (table < 0) return table;
            var definition = new int[3];
            int row = 0;
            for (int candidate = range[0]; candidate < range[1]; candidate++)
            {
                AssemblyReferences.ReadField(target, targetLayout, table, candidate, definition);
                if (!AssemblyReferences.SameString(caller, heaps, reference[4], target, targetHeaps, definition[1]))
                    continue;
                int match = BundleSignatures.Match(main, bundle, ranges, caller, layout, heaps, owner, reference[5],
                    target, targetLayout, targetHeaps, matched, definition[2], true, objects);
                if (match < 0) return match;
                if (match == 0) continue;
                if ((definition[0] & 0x17) != 6 || (write && (definition[0] & 0x20) != 0)) return -2;
                if (row != 0) return -5;
                row = candidate;
            }
            if (row == 0) return -6;
            int type = ObjectLayouts.Find(objects, matched, declaringType, new int[0]);
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
            int owner = FindOwner(main, bundle, ranges, caller, heaps, reference);
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
                owner = FindOwner(main, bundle, ranges, target, targetHeaps, next);
                // A fixed type name can visit each bundled owner only once.
                status = owner < 0 ? owner : -1;
            }
            binding[1] = status;
            return main;
        }

        private static int FindOwner(byte[] main, byte[] bundle, int[] ranges, byte[] caller, int[] heaps, int[] reference)
        {
            int matched = -1;
            var candidateLayout = new int[4];
            var candidateHeaps = new int[8];
            for (int owner = 0; owner <= ranges.Length / 2; owner++)
            {
                byte[] candidate = Load(main, bundle, ranges, owner);
                if (CompressedTables.FindWithHeaps(candidate, candidateLayout, candidateHeaps) < 0)
                    return -1;
                int match = AssemblyReferences.Matches(caller, heaps, reference, candidate, candidateHeaps, candidateLayout);
                if (match < 0) return match;
                if (match == 0) continue;
                if (matched >= 0) return -5;
                matched = owner;
            }
            return matched < 0 ? -4 : matched;
        }
    }
}
