using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BodyLibrary;

internal static class BodyCases
{
    internal static void Add(List<object> cases)
    {
        TestMethods(typeof(Sample.Kernels).Assembly.Location, ["Square", "SumSquares"]);
        TestMethods(typeof(BodySnippets).Assembly.Location, ["WithHandler"]);
        return;

        void TestMethods(string path, string[] names)
        {
            byte[] image = File.ReadAllBytes(path);
            using var pe = new PEReader(new MemoryStream(image));
            var metadata = pe.GetMetadataReader();
            var headers = pe.PEHeaders;
            foreach (var handle in metadata.MethodDefinitions)
            {
                var method = metadata.GetMethodDefinition(handle);
                string name = metadata.GetString(method.Name);
                if (!names.Contains(name))
                    continue;
                int row = MetadataTokens.GetRowNumber(handle);
                int rva = method.RelativeVirtualAddress;
                var body = pe.GetMethodBody(rva);
                byte[] il = body.GetILBytes()!;
                int sectionIndex = headers.GetContainingSectionIndex(rva);
                var section = headers.SectionHeaders[sectionIndex];
                int delta = rva - section.VirtualAddress;
                int start = section.PointerToRawData + delta;
                bool hasSections = body.ExceptionRegions.Length != 0;
                int headerLength = hasSections ? 12 : body.Size - il.Length;
                if (name == "Square" ? headerLength != 1 : headerLength != 12)
                    throw new InvalidOperationException($"{name} did not use its expected method header");

                Row(-1, 0);
                Row(0, start + headerLength);
                Row(1, il.Length);
                Row(2, body.MaxStack);
                Row(3, body.LocalSignature.IsNil ? 0 : MetadataTokens.GetToken(body.LocalSignature));
                Row(4, body.LocalVariablesInitialized ? 1 : 0);
                Row(5, hasSections ? 1 : 0);
                for (int i = 0; i < il.Length; i++)
                    Row(6 + i, il[i]);

                int sectionHeader = headers.PEHeaderStartOffset + headers.CoffHeader.SizeOfOptionalHeader + sectionIndex * 40;
                Rva("zero RVA", image, 0, -3);
                Rva("virtual tail", Change(sectionHeader + 16, (uint)delta), rva, -1);
                Rva("truncated code", Change(sectionHeader + 16, (uint)(delta + headerLength + il.Length - 1)), rva, -1);
                Rva("unsupported format", Change(start, 0), rva, -2);
                if (headerLength == 12)
                {
                    Rva("short fat header", Change(sectionHeader + 16, (uint)(delta + 11)), rva, -1);
                    Rva("wrapping code range", Change(start + 4, uint.MaxValue), rva, -1);
                }

                byte[] Change(int offset, uint value)
                {
                    var copy = (byte[])image.Clone();
                    BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(offset), value);
                    return copy;
                }

                void Row(int field, int expected) =>
                    Check("InspectRow", name, image, row, field, expected, BodyDemo.InspectRow(image, row, field));

                void Rva(string variant, byte[] bytes, int address, int expected) =>
                    Check("InspectRva", $"{name} {variant}", bytes, address, -1, expected, BodyDemo.InspectRva(bytes, (uint)address, -1));
            }
        }

        void Check(string method, string fixture, byte[] image, int index, int field, int expected, int actual)
        {
            if (actual != expected)
                throw new InvalidOperationException($"{fixture} field {field}: expected {expected}, CLR returned {actual}");
            object[] inputs = [new { bytes = Array.ConvertAll(image, value => (int)value) }, index, field];
            cases.Add(new { module = "BodyLibrary", method, fixture, inputs, expected });
        }
    }
}
