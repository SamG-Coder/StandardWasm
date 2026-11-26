using PeLibrary;

namespace PortableCompiler
{
    internal static class StringLiterals
    {
        // Real assembly slots are bounded by the packed input. This reserved
        // owner keeps literal globals separate from fields and type descriptors.
        private const int LiteralOwner = int.MaxValue;
        private const int HeaderSize = 4;

        internal static int Empty(int[] globals) =>
            StaticFields.Register(globals, LiteralOwner, -1, SignatureTypes.String);

        internal static int LowerEmpty(int[] records, int slot, int[] globals)
        {
            int global = Empty(globals);
            if (global < 0) return global;
            records[slot] = BranchIl.LoadString;
            WriteRecord(records, slot, 0, 0, global);
            return 0;
        }

        internal static int Lower(byte[] image, int[] heaps, int[] records, int slot,
            byte[] data, int[] used, int[] globals)
        {
            int start = used[0];
            if (data.Length - start < HeaderSize) return -3;
            int length = StringMetadata.CopyLiteral(image, heaps, records[slot + 1], data, start + HeaderSize);
            if (length < 0) return length;
            if (length == 0) return LowerEmpty(records, slot, globals);

            int global = Find(globals, data, start + HeaderSize, length);
            if (global < 0)
            {
                global = StaticFields.Register(globals, LiteralOwner, start, SignatureTypes.String);
                if (global < 0) return global;
                for (int index = 0; index < HeaderSize; index++)
                    data[start + index] = unchecked((byte)(length >> (index * 8)));
                used[0] = start + HeaderSize + length * 2;
            }
            WriteRecord(records, slot, StaticFields.Row(globals, global) + HeaderSize, length, global);
            return 0;
        }

        private static void WriteRecord(int[] records, int slot, int literal, int length, int global)
        {
            records[slot + 1] = literal;
            records[slot + 3] = length;
            // ldstr's first operand byte has no instruction. Keep the cache
            // index here so the primary record still holds the data range.
            records[slot + BranchIl.Width + 1] = global;
        }

        private static int Find(int[] globals, byte[] data, int start, int length)
        {
            for (int global = 0; global < globals[0]; global++)
            {
                if (StaticFields.Owner(globals, global) != LiteralOwner) continue;
                int candidate = StaticFields.Row(globals, global);
                if (candidate < 0) continue;
                if (ImageBytes.U32(data, (uint)candidate) != (uint)length) continue;
                int offset = 0;
                while (offset < length * 2 && data[candidate + HeaderSize + offset] == data[start + offset])
                    offset++;
                if (offset == length * 2) return global;
            }
            return -1;
        }
    }
}
