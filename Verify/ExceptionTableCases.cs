using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using ExceptionTables;

internal static class ExceptionTableCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 512;
        byte[] image = File.ReadAllBytes(typeof(Examples).Assembly.Location);
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        var methods = new Dictionary<string, (int Row, int Rva, int Table)>();
        string[] names = [nameof(Examples.SmallCatch), nameof(Examples.TwoCatches), nameof(Examples.FilterAndFinally),
            nameof(Examples.FatCatch), nameof(Examples.WithoutClauses)];
        foreach (var handle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(handle);
            string name = metadata.GetString(method.Name);
            if (!names.Contains(name)) continue;
            int row = MetadataTokens.GetRowNumber(handle);
            int rva = method.RelativeVirtualAddress;
            var body = pe.GetMethodBody(rva);
            var regions = body.ExceptionRegions;
            Record("InspectRow", name, image, regions.Length, ExceptionDemo.InspectRow(image, row, -1, 0), row, -1, 0);
            if (regions.Length == 0) continue;

            int sectionIndex = pe.PEHeaders.GetContainingSectionIndex(rva);
            var section = pe.PEHeaders.SectionHeaders[sectionIndex];
            int methodStart = section.PointerToRawData + rva - section.VirtualAddress;
            int headerSize = (BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(methodStart)) >> 12) * 4;
            int table = (methodStart + headerSize + body.GetILBytes()!.Length + 3) & ~3;
            bool fat = (image[table] & 0x40) != 0;
            if (fat != (name == nameof(Examples.FatCatch)))
                throw new InvalidOperationException($"{name} used the wrong EH table width");
            methods.Add(name, (row, rva, table));
            for (int clause = 0; clause < regions.Length; clause++)
            {
                var region = regions[clause];
                int data = region.Kind == ExceptionRegionKind.Filter ? region.FilterOffset :
                    region.Kind == ExceptionRegionKind.Catch ? MetadataTokens.GetToken(region.CatchType) : 0;
                int[] fields = [(int)region.Kind, region.TryOffset, region.TryLength, region.HandlerOffset, region.HandlerLength, data];
                for (int field = 0; field < fields.Length; field++)
                    Record("InspectRow", name, image, fields[field], ExceptionDemo.InspectRow(image, row, clause, field), row, clause, field);
            }
        }

        var small = methods[nameof(Examples.SmallCatch)];
        byte[] badToken = (byte[])image.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(badToken.AsSpan(small.Table + 12), 0x06000001);
        Probe("catch token with wrong table", badToken, small.Rva, 6, -1);
        RejectCompiler("malformed EH is diagnosed", badToken, small.Row, -1);
        byte[] badLength = (byte[])image.Clone();
        badLength[small.Table + 1]--;
        Probe("nonintegral compact table length", badLength, small.Rva, 6, -1);

        var fatBody = methods[nameof(Examples.FatCatch)];
        byte[] overlong = (byte[])image.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(overlong.AsSpan(fatBody.Table + 12), uint.MaxValue);
        Probe("fat try range exceeds IL", overlong, fatBody.Rva, 6, -1);

        var filtered = methods[nameof(Examples.FilterAndFinally)];
        int filterClause = Enumerable.Range(0, (image[filtered.Table + 1] - 4) / 12)
            .Select(index => filtered.Table + 4 + index * 12)
            .Single(offset => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(offset)) == 1);
        uint handler = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(filterClause + 5));
        RejectCompiler("filters and finally remain unsupported", image, filtered.Row, -2, (int)handler);
        byte[] badFilter = (byte[])image.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(badFilter.AsSpan(filterClause + 8), handler);
        Probe("filter starts at handler", badFilter, filtered.Rva, 12, -1);

        int sectionNumber = pe.PEHeaders.GetContainingSectionIndex(small.Rva);
        var smallSection = pe.PEHeaders.SectionHeaders[sectionNumber];
        int sectionHeader = pe.PEHeaders.PEHeaderStartOffset + pe.PEHeaders.CoffHeader.SizeOfOptionalHeader + sectionNumber * 40;
        byte[] truncated = (byte[])image.Clone();
        uint rawSize = (uint)(small.Table - smallSection.PointerToRawData + 4);
        BinaryPrimitives.WriteUInt32LittleEndian(truncated.AsSpan(sectionHeader + 16), rawSize);
        Probe("table leaves file-backed section", truncated, small.Rva, 6, -1);

        var multiple = methods[nameof(Examples.TwoCatches)];
        Probe("short destination", image, multiple.Rva, 6, -3);
        if (image[multiple.Table + 1] != 28)
            throw new InvalidOperationException("Expected two clauses in one compact table");
        // Keep the original two records and split their table into two sections.
        byte[] chained = (byte[])image.Clone();
        image.AsSpan(multiple.Table + 16, 12).CopyTo(chained.AsSpan(multiple.Table + 20));
        chained[multiple.Table] |= 0x80;
        chained[multiple.Table + 1] = 16;
        BinaryPrimitives.WriteUInt32LittleEndian(chained.AsSpan(multiple.Table + 16), 0x1001);
        using var chainedPe = new PEReader(new MemoryStream(chained));
        var active = chainedPe.GetMethodBody(multiple.Rva).ExceptionRegions.Single();
        Probe("first table in a chain", chained, multiple.Rva, 12, 1);
        int[] activeFields = [(int)active.Kind, active.TryOffset, active.TryLength, active.HandlerOffset,
            active.HandlerLength, MetadataTokens.GetToken(active.CatchType)];
        for (int field = 0; field < 6; field++)
            Record("InspectRva", "active clause in chained sections", chained, activeFields[field],
                ExceptionDemo.InspectRva(chained, (uint)multiple.Rva, 0, field), multiple.Rva, 0, field);
        Record("InspectRva", "second table is inactive", chained, -1,
            ExceptionDemo.InspectRva(chained, (uint)multiple.Rva, 1, 0), multiple.Rva, 1, 0);
        chained[multiple.Table + 17] = 3;
        Probe("malformed second section preserves destination", chained, multiple.Rva, 12, -1);

        void Probe(string fixture, byte[] bytes, int rva, int slots, int expected) =>
            Record("ProbeWrites", fixture, bytes, expected, ExceptionDemo.ProbeWrites(bytes, (uint)rva, slots), rva, slots);

        void Record(string method, string fixture, byte[] bytes, int expected, int actual, params int[] arguments)
        {
            if (actual != expected) throw new InvalidOperationException($"{fixture} returned {actual}, expected {expected}");
            cases.Add(new
            {
                module = "ExceptionTables", method, fixture, inputs = (object[])[Encode(bytes), .. arguments.Cast<object>()], expected
            });
        }

        void RejectCompiler(string fixture, byte[] bytes, int row, int expected, int ilOffset = -1)
        {
            int[] location = CompilerExports.CreateDiagnostic();
            byte[] output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            int actual = CompilerExports.CompileDetailed(bytes, row, output, location);
            if (actual != expected || !location.SequenceEqual(new[] { 0, row, ilOffset }) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{fixture} returned {actual} at [{string.Join(", ", location)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture, inputs = new[] { Encode(bytes), row },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic = location }, expected
            });
        }
    }

    private static object Encode(byte[] image) => new { bytes = Array.ConvertAll(image, value => (int)value) };
}
