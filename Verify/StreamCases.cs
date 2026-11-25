using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using StreamLibrary;

internal static class StreamCases
{
    internal static void Add(List<object> cases)
    {
        AddFixture(cases, typeof(Sample.Kernels).Assembly.Location);
        AddFixture(cases, typeof(PeLibrary.ReaderDemo).Assembly.Location);
    }

    private static void AddFixture(List<object> cases, string path)
    {
        byte[] image = File.ReadAllBytes(path);
        using var pe = new PEReader(new MemoryStream(image));
        int root = pe.PEHeaders.MetadataStartOffset;
        int size = pe.PEHeaders.MetadataSize;
        var reader = pe.GetMetadataReader();
        int header;
        var streams = new List<(string Name, int NameOffset, int DataOffset, int Size)>();

        // The host oracle uses a BinaryReader and framework PE mappings. The compiler core
        // uses neither dependency. Cross-check recognized heap starts with MetadataReader too.
        using (var input = new BinaryReader(new MemoryStream(image, root, size)))
        {
            input.BaseStream.Position = 12;
            int versionLength = input.ReadInt32();
            input.BaseStream.Position = 16 + versionLength;
            header = root + (int)input.BaseStream.Position;
            _ = input.ReadUInt16();
            int count = input.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                int start = input.ReadInt32();
                int length = input.ReadInt32();
                int name = (int)input.BaseStream.Position;
                var bytes = new List<byte>();
                for (byte value = input.ReadByte(); value != 0; value = input.ReadByte())
                    bytes.Add(value);
                input.BaseStream.Position = (input.BaseStream.Position + 3) & ~3;
                string label = Encoding.ASCII.GetString(bytes.ToArray());
                var heap = label switch
                {
                    "#Strings" => HeapIndex.String,
                    "#US" => HeapIndex.UserString,
                    "#GUID" => HeapIndex.Guid,
                    "#Blob" => HeapIndex.Blob,
                    _ => (HeapIndex?)null
                };
                if (heap.HasValue && reader.GetHeapMetadataOffset(heap.Value) != start)
                    throw new InvalidOperationException($"Heap offset disagrees with MetadataReader: {label}");
                streams.Add((label, root + name, root + start, length));
            }
        }

        const int capacity = 128;
        Case("stream count", image, capacity, -1, streams.Count);
        for (int i = 0; i < streams.Count; i++)
        {
            var (label, name, data, length) = streams[i];
            int slot = i * 4;
            Case(label + " name offset", image, capacity, slot, name);
            Case(label + " name length", image, capacity, slot + 1, label.Length);
            Case(label + " data offset", image, capacity, slot + 2, data);
            Case(label + " data length", image, capacity, slot + 3, length);
        }

        var first = streams[0];
        var second = streams[1];
        Mutation("unbounded version", changed => Write(changed, root + 12, uint.MaxValue));
        Mutation("version without terminal padding", changed => changed[header - 1] = 65);
        Mutation("unbounded stream count", changed =>
            BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(header + 2), ushort.MaxValue));
        Mutation("missing name terminator", changed =>
            changed.AsSpan(first.NameOffset, 32).Fill(65));
        Mutation("non-ASCII stream name", changed => changed[first.NameOffset] = 0xff);
        Mutation("nonzero name padding", changed => changed[first.NameOffset + first.Name.Length + 1] = 65);
        Mutation("unbounded stream size", changed => Write(changed, first.NameOffset - 4, uint.MaxValue));
        Mutation("stream points into header", changed => Write(changed, first.NameOffset - 8, 0));
        Mutation("overlapping streams", changed =>
        {
            Write(changed, second.NameOffset - 8, (uint)(first.DataOffset - root));
            Write(changed, second.NameOffset - 4, (uint)first.Size);
        });
        Mutation("duplicate stream name", changed =>
        {
            // Replace the name with the earlier one, retaining the original header width.
            // Two existing four-byte padded names are available in ordinary managed metadata.
            var shortNames = streams.Where(s => s.Name.Length <= 3).Take(2).ToArray();
            changed.AsSpan(shortNames[0].NameOffset, 4).CopyTo(changed.AsSpan(shortNames[1].NameOffset, 4));
        });
        Case("small output status", image, 1, -1, -2);
        Case("small output sentinel", image, 1, 0, -17);

        static void Write(byte[] target, int offset, uint value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset), value);

        void Mutation(string scenario, Action<byte[]> update)
        {
            var changed = (byte[])image.Clone();
            update(changed);
            Case(scenario, changed, capacity, -1, -1);
            Case(scenario + " sentinel", changed, capacity, 0, -17);
        }

        void Case(string scenario, byte[] source, int slots, int field, int expected)
        {
            int actual = StreamDemo.Inspect(source, slots, field);
            string fixture = $"{Path.GetFileName(path)} {scenario}";
            if (actual != expected)
                throw new InvalidOperationException($"{fixture}: expected {expected}, CLR returned {actual}");
            cases.Add(new
            {
                module = "StreamLibrary", method = "Inspect", fixture,
                inputs = new object[] { new { bytes = Array.ConvertAll(source, value => (int)value) }, slots, field },
                expected
            });
        }
    }
}
