using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using PeLibrary;

internal static class PeCases
{
    internal static void Add(List<object> cases)
    {
        AddFixture(cases, typeof(Sample.Kernels).Assembly.Location, PEMagic.PE32);
        AddFixture(cases, typeof(ArrayLibrary.Sequences).Assembly.Location, PEMagic.PE32);
        AddFixture(cases, typeof(ReaderDemo).Assembly.Location, PEMagic.PE32Plus);
    }

    private static void AddFixture(List<object> cases, string path, PEMagic magic)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using var pe = new PEReader(new MemoryStream(bytes));
        var headers = pe.PEHeaders;
        var header = headers.PEHeader!;
        if (header.Magic != magic)
            throw new InvalidOperationException($"Expected a {magic} fixture: {path}");

        int section = headers.PEHeaderStartOffset + headers.CoffHeader.SizeOfOptionalHeader;
        int optional = headers.PEHeaderStartOffset;
        int directory = magic == PEMagic.PE32 ? 96 : 112;
        int cliEntry = optional + directory + 14 * 8;
        int cli = headers.CorHeaderStartOffset;
        int metadata = headers.MetadataStartOffset;
        int size = headers.MetadataSize;
        int cliRva = header.CorHeaderTableDirectory.RelativeVirtualAddress;
        int index = headers.GetContainingSectionIndex(cliRva);
        int cliSection = section + index * 40;
        // Create aliases independent of section ordering in the fixture.
        int otherSection = section + (index == 0 ? 1 : 0) * 40;
        uint va = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cliSection + 12));

        Expect("original", bytes, 0, metadata, size);
        Expect("truncated DOS header", bytes[..63], 1);
        Expect("truncated section table", bytes[..(section + headers.CoffHeader.NumberOfSections * 40 - 1)], 1);
        Expect("truncated metadata", bytes[..(metadata + size - 1)], 1);
        var shortOptional = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(shortOptional.AsSpan(optional - 4), (ushort)(directory + 14 * 8));
        Expect("optional header excludes CLI entry", shortOptional, 1);
        Expect("CLI entry omitted", Change((optional + directory - 4, 14)), 3);
        Expect("CLI entry all zero", Change((cliEntry, 0), (cliEntry + 4, 0)), 3);
        Expect("CLI entry half zero", Change((cliEntry + 4, 0)), 1);
        Expect("CLI header mapped into PE headers", Change((cliEntry, 1)), 2);
        Expect("short CLI cb", Change((cli, 71)), 1);
        Expect("RVA range wraps", Change((cli + 8, uint.MaxValue - 1)), 1);
        Expect("oversize metadata", Change((cli + 12, uint.MaxValue)), 1);
        Expect("wrong metadata signature", Change((metadata, 0)), 1);
        Expect("raw section beyond EOF", Change((cliSection + 20, (uint)bytes.Length - 1)), 1);
        Expect("unbacked virtual section", Change((cliSection + 16, 0)), 1);
        Expect("ambiguous CLI RVA", Change((otherSection + 12, va), (otherSection + 8, uint.MaxValue)), 1);

        byte[] Change(params (int Offset, uint Value)[] edits)
        {
            var copy = (byte[])bytes.Clone();
            foreach (var (offset, value) in edits)
                BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(offset), value);
            return copy;
        }

        void Expect(string scenario, byte[] image, int status, int offset = -17, int length = -29)
        {
            var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
            int[] expectation = [status, offset, length];
            for (int field = 0; field < expectation.Length; field++)
            {
                int expected = expectation[field];
                int actual = ReaderDemo.Inspect(image, field);
                if (actual != expected)
                    throw new InvalidOperationException($"{Path.GetFileName(path)} {scenario} field {field}: expected {expected}, CLR returned {actual}");
                cases.Add(new {
                    module = "PeLibrary", method = "Inspect", fixture = $"{Path.GetFileName(path)} {scenario}",
                    inputs = new object[] { encoded, field }, expected
                });
            }
        }
    }
}
