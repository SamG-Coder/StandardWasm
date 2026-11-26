using PeLibrary;

namespace PortableCompiler
{
    internal static class AssemblyBundle
    {
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
            int[] layout, int[] heaps, int owner, int member, int opcode, int[] symbol)
        {
            var reference = new int[6];
            int status = AssemblyReferences.ReadMember(caller, heaps, layout, member, reference);
            if (status < 0) return status;
            int matched = FindOwner(main, bundle, ranges, caller, heaps, reference);
            if (matched < 0) return matched;
            byte[] target = Load(main, bundle, ranges, matched);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            var range = new int[2];
            status = AssemblyReferences.FindType(caller, heaps, reference, target, targetHeaps, targetLayout, range, false);
            if (status < 0) return status;
            int row = 0;
            for (int candidate = range[0]; candidate < range[1]; candidate++)
            {
                int flags = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 2);
                if ((flags & 7) != 6) continue; // Public.
                bool isStatic = (flags & 0x10) != 0;
                if (isStatic && opcode != 0x28) continue;
                int name = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 3);
                if (!AssemblyReferences.SameString(caller, heaps, reference[4], target, targetHeaps, name))
                    continue;
                int signature = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 4);
                int match = BundleSignatures.Match(main, bundle, ranges, caller, layout, heaps, owner, reference[5],
                    target, targetLayout, targetHeaps, matched, signature, false);
                if (match < 0) return match;
                if (match == 0) continue;
                // Direct calls can bind a known body. callvirt additionally
                // needs a null check and can only bind nonvirtual methods here.
                bool constructor = ObjectLayouts.IsConstructor(target, targetHeaps, targetLayout, candidate);
                if (opcode == 0x73)
                {
                    if (!constructor || (flags & 0x1800) != 0x1800) return -2;
                }
                else if (constructor) return -2;
                if (opcode == 0x6f && (flags & 0x40) != 0) return -2;
                if (row != 0) return -5;
                row = candidate;
            }
            if (row == 0) return -6;
            symbol[0] = matched;
            symbol[1] = row;
            return 0;
        }

        internal static int ResolveField(byte[] main, byte[] bundle, int[] ranges, byte[] caller,
            int[] layout, int[] heaps, int owner, int member, bool write, int[] objects, int[] field)
        {
            var reference = new int[6];
            int status = AssemblyReferences.ReadMember(caller, heaps, layout, member, reference);
            if (status < 0) return status;
            int matched = FindOwner(main, bundle, ranges, caller, heaps, reference);
            if (matched < 0) return matched;
            byte[] target = Load(main, bundle, ranges, matched);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            var range = new int[2];
            int declaringType = AssemblyReferences.FindType(caller, heaps, reference,
                target, targetHeaps, targetLayout, range, true);
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
                    target, targetLayout, targetHeaps, matched, definition[2], true);
                if (match < 0) return match;
                if (match == 0) continue;
                if ((definition[0] & 0x17) != 6 || (write && (definition[0] & 0x20) != 0)) return -2;
                if (row != 0) return -5;
                row = candidate;
            }
            if (row == 0) return -6;
            int type = ObjectLayouts.Register(target, targetHeaps, targetLayout, objects, matched, declaringType);
            if (type < 0) return type;
            int index = ObjectLayouts.FieldOrdinal(objects, type, row);
            if (index < 0) return index;
            field[0] = index;
            // The receiver layout and value type come from the same owning DLL.
            field[1] = (type << 8) | ObjectLayouts.FieldType(objects, type, row);
            return 0;
        }

        internal static int ClassIdentity(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] symbol)
        {
            int row = coded >> 2;
            if (row < 1) return -1;
            if ((coded & 3) == 0)
            {
                symbol[0] = owner;
                symbol[1] = row;
                return 0;
            }
            if ((coded & 3) != 1 || bundle.Length == 0) return -2;
            var reference = new int[4];
            int status = AssemblyReferences.ReadType(image, heaps, layout, row, reference);
            if (status < 0) return status;
            int matched = FindOwner(main, bundle, ranges, image, heaps, reference);
            if (matched < 0) return matched;
            byte[] target = Load(main, bundle, ranges, matched);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            row = AssemblyReferences.FindType(image, heaps, reference, target, targetHeaps, targetLayout, new int[2], false);
            if (row < 0) return row;
            symbol[0] = matched;
            symbol[1] = row;
            return 0;
        }

        internal static int RegisterClassReference(byte[] main, byte[] bundle, int[] ranges, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] objects)
        {
            var symbol = new int[2];
            int status = ClassIdentity(main, bundle, ranges, image, layout, heaps, owner, coded, symbol);
            if (status < 0) return status;
            if (symbol[0] == owner) return ObjectLayouts.Register(image, heaps, layout, objects, owner, symbol[1]);
            byte[] target = Load(main, bundle, ranges, symbol[0]);
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
            return ObjectLayouts.Register(target, targetHeaps, targetLayout, objects, symbol[0], symbol[1]);
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
