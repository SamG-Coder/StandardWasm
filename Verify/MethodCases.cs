#nullable enable

using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using MethodLibrary;

internal static class MethodCases
{
    internal static void Add(List<object> cases)
    {
        foreach (var assembly in new[]
        {
            typeof(Sample.Kernels).Assembly, typeof(PeLibrary.ReaderDemo).Assembly, typeof(MethodDemo).Assembly
        })
        {
            string fixture = assembly.GetName().Name!;
            byte[] image = File.ReadAllBytes(assembly.Location);
            using var pe = new PEReader(new MemoryStream(image));
            var reader = pe.GetMetadataReader();
            int count = reader.GetTableRowCount(TableIndex.MethodDef);
            int start = pe.PEHeaders.MetadataStartOffset + reader.GetTableMetadataOffset(TableIndex.MethodDef);
            int width = reader.GetTableRowSize(TableIndex.MethodDef);

            TestImage(0, 0, count);
            TestImage(0, 1, start);
            TestImage(0, 2, width);
            TestImage(count + 1, 0, -1);
            foreach (var handle in reader.MethodDefinitions)
            {
                int row = MetadataTokens.GetRowNumber(handle);
                var def = reader.GetMethodDefinition(handle);
                TestImage(row, 0, def.RelativeVirtualAddress);
                TestImage(row, 1, (int)def.ImplAttributes);
                TestImage(row, 2, (int)def.Attributes);
                TestImage(row, 3, MetadataTokens.GetHeapOffset(def.Name));
                TestImage(row, 4, MetadataTokens.GetHeapOffset(def.Signature));
            }

            // Rewrite the first stream's conventional compressed name in these compiler-produced DLLs.
            int root = pe.PEHeaders.MetadataStartOffset;
            int versionLength = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(root + 12));
            int name = root + 16 + versionLength + 4 + 8;
            if (image[name] != '#' || image[name + 1] != '~' || image[name + 2] != 0)
                throw new InvalidOperationException("Fixture did not place #~ first");
            byte[] changed = (byte[])image.Clone();
            changed[name + 1] = (byte)'-';
            TestImage(0, 0, -2, changed);
            byte[] missing = (byte[])image.Clone();
            missing[name + 1] = (byte)'?';
            TestImage(0, 0, -3, missing);

            void TestImage(int row, int field, int expected, byte[]? variant = null)
            {
                byte[] source = variant ?? image;
                Check(cases, "Inspect", fixture, source, [row, field], expected,
                    () => MethodDemo.Inspect(source, row, field));
            }
        }

        // A bounded prefix suffices for ReadLayout. Large declared counts in later tables
        // widen the ResolutionScope and Param indexes without needing those later rows.
        byte[] wide = new byte[24 + 5 * 4 + 12 + 20];
        wide[4] = 2;
        wide[6] = 7;
        Write(wide, 8, (1u << 1) | (1u << 6) | (1u << 8) | (1u << 26));
        Write(wide, 12, 1u << 3);
        Write(wide, 24, 1);     // TypeRef
        Write(wide, 28, 1);     // MethodDef
        Write(wide, 32, 65536); // Param widens ParamList
        Write(wide, 36, 0);     // ModuleRef
        Write(wide, 40, 16384); // AssemblyRef widens ResolutionScope
        Raw("wide row count", wide, 0, 1);
        Raw("wide row start", wide, 1, 56);
        Raw("wide row size", wide, 2, 20);
        Raw("wide string index", wide, 3, 4);
        Raw("wide blob index", wide, 4, 4);
        Raw("truncated header", wide[..23], 0, -1);
        Raw("truncated counts", wide[..43], 0, -1);
        Raw("truncated leading TypeRef row", wide[..55], 0, -1);
        Raw("truncated MethodDef row", wide[..^1], 0, -1);

        byte[] oversized = (byte[])wide.Clone();
        Write(oversized, 28, 0x1000000);
        Raw("oversized row count", oversized, 0, -1);
        byte[] pointer = (byte[])wide.Clone();
        pointer[8] |= 1 << 5;
        Raw("unsupported pointer table", pointer, 0, -2);
        byte[] flags = (byte[])wide.Clone();
        flags[6] |= 0x40;
        Raw("unsupported extra table header data", flags, 0, -2);

        void Raw(string fixture, byte[] source, int field, int expected) =>
            Check(cases, "InspectRawStream", fixture, source, [field], expected,
                () => MethodDemo.InspectRawStream(source, field));
    }

    private static void Check(List<object> cases, string method, string fixture, byte[] source,
        int[] indexes, int expected, Func<int> run)
    {
        int actual = run();
        if (actual != expected)
            throw new InvalidOperationException($"{fixture} {method} ({string.Join(", ", indexes)}): expected {expected}, CLR returned {actual}");
        object[] inputs = [new { bytes = Array.ConvertAll(source, value => (int)value) }, .. indexes.Cast<object>()];
        cases.Add(new { module = "MethodLibrary", method, fixture, inputs, expected });
    }

    private static void Write(byte[] target, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset), value);
}
