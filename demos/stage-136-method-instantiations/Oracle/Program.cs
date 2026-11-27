using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

var cases = new List<object>();
foreach (var (source, path) in new[] {
    ("fixture", args[0]),
    ("core-x86", args[1]),
    ("core-default", args[2])
})
{
    using var pe = new PEReader(File.OpenRead(path));
    var md = pe.GetMetadataReader();
    int count = md.GetTableRowCount(TableIndex.MethodSpec);
    var rows = source == "fixture" ? Enumerable.Range(1, count) :
        Enumerable.Range(0, 12).Select(i => 1 + i * (count - 1) / 11).Append(FindRangeGuard(md, count));
    rows = rows.Append(0).Append(count + 1).Distinct();
    foreach (var row in rows)
    {
        if (row < 1 || row > count)
        {
            cases.Add(new { source, path, row, expected = new[] { -1 } });
            continue;
        }
        var spec = md.GetMethodSpecification(MetadataTokens.MethodSpecificationHandle(row));
        int target = MetadataTokens.GetRowNumber(spec.Method) * 2 + (spec.Method.Kind == HandleKind.MemberReference ? 1 : 0);
        var signature = md.GetBlobReader(spec.Signature);
        if (signature.ReadByte() != 0x0a) throw new InvalidOperationException("Invalid MethodSpec header.");
        int arity = signature.ReadCompressedInteger();
        var expected = new List<int> { 0, target, arity };
        while (signature.RemainingBytes > 0) expected.Add(signature.ReadByte());
        cases.Add(new { source, path, row, expected });
    }
}
File.WriteAllText(args[3], JsonSerializer.Serialize(cases));

static int FindRangeGuard(MetadataReader metadata, int count)
{
    for (int row = 1; row <= count; row++)
    {
        var spec = metadata.GetMethodSpecification(MetadataTokens.MethodSpecificationHandle(row));
        if (spec.Method.Kind != HandleKind.MethodDefinition) continue;
        var method = metadata.GetMethodDefinition((MethodDefinitionHandle)spec.Method);
        if (metadata.GetString(method.Name) != "ThrowIfNegative") continue;
        var owner = metadata.GetTypeDefinition(method.GetDeclaringType());
        if (metadata.GetString(owner.Namespace) == "System" &&
            metadata.GetString(owner.Name) == "ArgumentOutOfRangeException" &&
            metadata.GetBlobBytes(spec.Signature).AsSpan().SequenceEqual(new byte[] { 0x0a, 1, 0x0a }))
            return row;
    }
    throw new InvalidOperationException("CoreLib has no ThrowIfNegative<long> MethodSpec.");
}
