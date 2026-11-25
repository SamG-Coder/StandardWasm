#nullable enable

using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ArrayLiterals;
using BootstrapLibrary;

internal static class ArrayLiteralCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Lookup).Assembly.Location);
        var encoded = Encode(image);
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        int metadataStart = pe.PEHeaders.MetadataStartOffset;

        CheckArray(nameof(Lookup.ByteAt), 8, value => Lookup.ByteAt(value));
        CheckArray(nameof(Lookup.ShortAt), 5, value => Lookup.ShortAt(value));
        CheckArray(nameof(Lookup.IntegerAt), 5, value => Lookup.IntegerAt(value));
        CheckArray(nameof(Lookup.LongAt), 5, value => new { i64 = Lookup.LongAt(value).ToString(CultureInfo.InvariantCulture) });
        CheckArray(nameof(Lookup.SingleAt), 5, value => Real(Lookup.SingleAt(value)));
        CheckArray(nameof(Lookup.DoubleAt), 5, value => Real(Lookup.DoubleAt(value)));
        CheckArray(nameof(Lookup.Mixed), 7, value => Lookup.Mixed(value));
        Check(nameof(Lookup.Fresh), new[] { int.MinValue, -23, 0, 123, int.MaxValue }
            .Select(value => (new[] { value }, (Func<object>)(() => Lookup.Fresh(value)))));
        Check(nameof(Lookup.Conditional), from empty in new[] { 0, 1 } from index in new[] { -1, 0, 3, 4 }
            select (new[] { empty, index }, (Func<object>)(() => Lookup.Conditional(empty, index))));

        int row = Row(nameof(Lookup.IntegerAt));
        var method = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
        var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
        int tokenOffset = IlOffsets.Find(il, OpCodes.Ldtoken);
        int field = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(tokenOffset + 1)) & 0xffffff;
        int table = metadataStart + metadata.GetTableMetadataOffset(TableIndex.FieldRva);
        int rowSize = metadata.GetTableRowSize(TableIndex.FieldRva);
        int fieldRvaOffset = Enumerable.Range(0, metadata.GetTableRowCount(TableIndex.FieldRva))
            .Select(index => table + index * rowSize)
            .Single(offset => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(offset + 4)) == field);
        byte[] invalidRva = (byte[])image.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(invalidRva.AsSpan(fieldRvaOffset), uint.MaxValue);
        Reject("unmapped field RVA", invalidRva, row, tokenOffset, -1);

        int fieldOffset = metadataStart + metadata.GetTableMetadataOffset(TableIndex.Field) +
            (field - 1) * metadata.GetTableRowSize(TableIndex.Field);
        byte[] mutableField = (byte[])image.Clone();
        mutableField[fieldOffset] &= 0xdf; // Clear InitOnly without changing the field data.
        Reject("writable initializer field", mutableField, row, tokenOffset, -2);

        var helper = metadata.MemberReferences.Select(metadata.GetMemberReference)
            .Single(member => metadata.GetString(member.Name) == "InitializeArray");
        byte[] otherMember = (byte[])image.Clone();
        otherMember[metadataStart + metadata.GetHeapMetadataOffset(HeapIndex.String) + MetadataTokens.GetHeapOffset(helper.Name)] = (byte)'X';
        Reject("different initializer helper", otherMember, row, tokenOffset, -2);

        row = Row(nameof(Lookup.Conditional));
        method = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
        var body = pe.GetMethodBody(method.RelativeVirtualAddress);
        il = body.GetILBytes()!;
        int branch = IlOffsets.Find(il, OpCodes.Brfalse_S);
        if (branch < 0) branch = IlOffsets.Find(il, OpCodes.Brtrue_S);
        if (branch < 0) throw new InvalidOperationException("Expected a short branch in the conditional literal fixture");
        int allocation = IlOffsets.Find(il, OpCodes.Newarr);
        var section = pe.PEHeaders.SectionHeaders.Single(header => method.RelativeVirtualAddress >= header.VirtualAddress &&
            method.RelativeVirtualAddress - header.VirtualAddress < header.SizeOfRawData);
        int bodyStart = section.PointerToRawData + method.RelativeVirtualAddress - section.VirtualAddress;
        int ilStart = bodyStart + body.Size - il.Length;
        byte[] branchIntoInitializer = (byte[])image.Clone();
        branchIntoInitializer[ilStart + branch + 1] = unchecked((byte)checked((sbyte)(allocation - branch - 2)));
        Reject("branch into initializer", branchIntoInitializer, row, IlOffsets.Find(il, OpCodes.Ldtoken), -2);

        void CheckArray(string name, int length, Func<int, object> oracle)
        {
            byte[] instructions = typeof(Lookup).GetMethod(name)!.GetMethodBody()!.GetILAsByteArray()!;
            if (IlOffsets.Find(instructions, OpCodes.Ldtoken) < 0)
                throw new InvalidOperationException($"{name} did not emit a field-backed array literal");
            Check(name, Enumerable.Range(-1, length + 2).Select(index => (new[] { index }, (Func<object>)(() => oracle(index)))));
        }

        void Check(string name, IEnumerable<(int[] Arguments, Func<object> Oracle)> inputs)
        {
            int methodRow = Row(name);
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, methodRow, output);
            if (length <= 0) throw new InvalidOperationException($"{name} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (arguments, oracle) in inputs)
            {
                object? expected;
                string? exception = null;
                try { expected = oracle(); }
                catch (IndexOutOfRangeException error)
                {
                    expected = null;
                    exception = error.GetType().FullName;
                }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = name,
                    inputs = new object[] { encoded, methodRow }, generated = new { capacity, arguments, hex }, expected, exception
                });
            }
        }

        void Reject(string name, byte[] input, int methodRow, int offset, int expected)
        {
            byte[] output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            int[] diagnostic = CompilerExports.CreateDiagnostic();
            int status = CompilerExports.CompileDetailed(input, methodRow, output, diagnostic);
            if (status != expected || output.Any(value => value != 0xa5) || !diagnostic.SequenceEqual(new[] { 0, methodRow, offset }))
                throw new InvalidOperationException($"{name} returned {status} at [{string.Join(", ", diagnostic)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = name,
                inputs = new[] { Encode(input), methodRow },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected
            });
        }
    }

    private static int Row(string name) => typeof(Lookup).GetMethod(name)!.MetadataToken & 0xffffff;
    private static object Encode(byte[] image) => new { bytes = Array.ConvertAll(image, value => (int)value) };
    private static object Real(double value) => new { f64 = value.ToString("G17", CultureInfo.InvariantCulture) };
}
