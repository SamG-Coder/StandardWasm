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
            int[] layout, int[] heaps, int member, int[] symbol)
        {
            var reference = new int[6];
            int status = AssemblyReferences.ReadMethod(caller, heaps, layout, member, reference);
            if (status < 0) return status;
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
            if (matched < 0) return -4;

            byte[] target = Load(main, bundle, ranges, matched);
            if (CompressedTables.FindWithHeaps(target, candidateLayout, candidateHeaps) < 0)
                return -1;
            int row = AssemblyReferences.FindMethod(caller, heaps, reference, target, candidateHeaps, candidateLayout);
            if (row < 0) return row;
            symbol[0] = matched;
            symbol[1] = row;
            return 0;
        }
    }
}
