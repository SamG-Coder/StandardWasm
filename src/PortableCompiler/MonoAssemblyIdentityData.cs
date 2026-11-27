using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // The opaque module slot never crosses an unmanaged call boundary. The
    // authentic Mono identity leaf translates it into this instance's record.
    internal sealed class MonoAssemblyIdentityData
    {
        private const int CacheOwner = int.MaxValue - 22;
        internal const int RecordSize = 68;
        internal int Function { get; private set; } = -1;
        internal int Cache { get; private set; }
        internal int DataStart { get; private set; }
        internal int DataSize { get; private set; }
        internal int Modules { get; private set; }
        internal int[] Relocations { get; private set; }
        internal int RelocationCount { get; private set; }

        internal int Bind(BundleImages bundle, int core, int nativeRecord, int[] globals, byte[] literals,
            int[] literalSize, int[] arities, int[] offsets, int[] signatures, int functions)
        {
            if (Function >= 0) return functions;
            int offset = MethodCompiler.NextSignature(offsets, arities, functions);
            if (functions >= arities.Length || signatures.Length - offset < 2) return -3;
            var measured = new int[2];
            int status = NativeValueLayouts.Measure(bundle, core, nativeRecord, measured);
            if (status < 0) return status;
            if (measured[0] != RecordSize || measured[1] != 4 || !HasRecordFields(bundle, core, nativeRecord)) return -2;
            if (bundle.Count > (literals.Length - literalSize[0]) / RecordSize) return -3;

            DataStart = literalSize[0];
            Modules = bundle.Count;
            int extent = Modules * RecordSize;
            var relocations = new int[Modules * 3];
            var fields = new int[AssemblyDefinitionRecord.Width];
            var layout = new int[4];
            var heaps = new int[8];
            int used = DataStart + extent;
            for (int i = DataStart; i < used; i++) literals[i] = 0;
            for (int owner = 0; owner < Modules; owner++)
            {
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                status = AssemblyDefinitionRecord.Read(image, heaps, layout, fields);
                if (status < 0) return status;
                int at = DataStart + owner * RecordSize;
                PutWord(literals, at, used - DataStart);
                relocations[RelocationCount++] = at - DataStart;
                used = AppendString(image, heaps, fields[AssemblyDefinitionRecord.Name], literals, used);
                if (used < 0) return used;
                PutWord(literals, at + 4, used - DataStart);
                relocations[RelocationCount++] = at + 4 - DataStart;
                used = AppendString(image, heaps, fields[AssemblyDefinitionRecord.Culture], literals, used);
                if (used < 0) return used;

                int length = fields[AssemblyDefinitionRecord.KeyLength];
                if (length != 0)
                {
                    int start = fields[AssemblyDefinitionRecord.KeyStart];
                    int blob = fields[AssemblyDefinitionRecord.KeyBlob];
                    int bytes = start - blob + length;
                    if (bytes > literals.Length - used) return -3;
                    PutWord(literals, at + 12, used - DataStart);
                    relocations[RelocationCount++] = at + 12 - DataStart;
                    for (int i = 0; i < bytes; i++) literals[used++] = image[blob + i];
                    long token = StrongNames.TokenWords(image, start, length);
                    for (int i = 0; i < 8; i++)
                    {
                        int value = (int)(token >> (i * 8)) & 255;
                        literals[at + 16 + i * 2] = Hex(value >> 4);
                        literals[at + 17 + i * 2] = Hex(value & 15);
                    }
                }

                PutWord(literals, at + 36, fields[AssemblyDefinitionRecord.HashAlgorithm]);
                PutWord(literals, at + 44, fields[AssemblyDefinitionRecord.Flags]);
                PutWord(literals, at + 48, fields[AssemblyDefinitionRecord.Major]);
                PutWord(literals, at + 52, fields[AssemblyDefinitionRecord.Minor]);
                PutWord(literals, at + 56, fields[AssemblyDefinitionRecord.Build]);
                PutWord(literals, at + 60, fields[AssemblyDefinitionRecord.Revision]);
            }

            int cache = StaticFields.Register(globals, CacheOwner, 0, Native);
            if (cache < 0) return cache;
            Cache = cache;
            DataSize = used - DataStart;
            Relocations = relocations;
            literalSize[0] = used;
            Function = functions;
            offsets[functions] = offset;
            arities[functions] = 1;
            signatures[offset] = Native;
            signatures[offset + 1] = Native;
            return functions + 1;
        }

        private static bool HasRecordFields(BundleImages bundle, int core, int definition)
        {
            byte[] image = bundle.Get(core);
            var layout = new int[4];
            var heaps = new int[8];
            var shape = new int[5];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0 ||
                ObjectLayouts.ReadShape(image, heaps, layout, definition, shape, true) != 0 ||
                shape[2] != 13) return false;
            var names = new string[] { "name", "culture", "hash_value", "public_key", "public_key_token",
                "hash_alg", "hash_len", "flags", "major", "minor", "build", "revision", "arch" };
            var field = new int[3];
            var native = new int[2];
            for (int index = 0; index < names.Length; index++)
            {
                int row = shape[1] + index;
                AssemblyReferences.ReadField(image, layout, shape[0], row, field);
                if ((field[0] & 0x10) != 0 || !MatchesAscii(image, heaps, (uint)field[1], names[index]) ||
                    NativeValueLayouts.Field(bundle, core, definition, row, native) != 0) return false;
                int offset = index * 4 + (index > 4 ? 16 : 0);
                int opcode = index < 4 ? 0x4d : index == 4 ? 0 : index < 8 ? 0x4b : 0x4a;
                if (native[0] != offset || native[1] != opcode) return false;
            }
            // The intervening byte buffer occupies exactly 17 bytes. Its
            // proven declaration includes the final NUL, followed by three ABI pad bytes.
            AssemblyReferences.ReadField(image, layout, shape[0], shape[1] + 4, field);
            int blob = field[2];
            var cursor = new int[1];
            int limit = heaps[0] + heaps[1];
            if (blob <= 0 || blob >= heaps[1]) return false;
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            if (size < 3 || size > limit - cursor[0]) return false;
            int end = cursor[0] + size;
            if (!SignatureReader.TakeElement(image, cursor, end, 6) ||
                !SignatureReader.TakeElement(image, cursor, end, 0x11)) return false;
            int backing = CompressedUnsigned(image, cursor[0], end, cursor);
            return backing > 0 && (backing & 3) == 0 && cursor[0] == end &&
                NativeValueLayouts.Measure(bundle, core, backing >> 2, native) == 0 &&
                native[0] == 17 && native[1] == 1;
        }

        private static byte Hex(int value) => (byte)(value < 10 ? '0' + value : 'a' + value - 10);

        private static int AppendString(byte[] image, int[] heaps, int offset, byte[] data, int used)
        {
            int start = heaps[2] + offset, limit = heaps[2] + heaps[3], end = start;
            while (end < limit && image[end] != 0) end++;
            if (end == limit) return -1;
            int size = end - start + 1;
            if (size > data.Length - used) return -3;
            for (int i = 0; i < size; i++) data[used + i] = image[start + i];
            return used + size;
        }

        private static void PutWord(byte[] data, int at, int value)
        {
            for (int i = 0; i < 4; i++) data[at + i] = unchecked((byte)(value >> (i * 8)));
        }
    }
}
