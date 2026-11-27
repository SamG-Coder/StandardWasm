using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text.Json;

string evidence = args[0];
string pack = Path.GetFullPath(args.Length == 4 ? args[3] : Path.GetDirectoryName(typeof(object).Assembly.Location)!);
string[] dependencies = ["netstandard", "System.Private.CoreLib", "System.Runtime", "System.Collections"];
string[] files = [Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), ..dependencies.Select(name => Path.Combine(pack, name + ".dll"))];
byte[] facade = File.ReadAllBytes(files[2]);
using var pe = new PEReader(new MemoryStream(facade));
MetadataReader metadata = pe.GetMetadataReader();
AssemblyDefinition assembly = metadata.GetAssemblyDefinition();
if (metadata.GetString(assembly.Name) != "netstandard" || assembly.Version != new Version(2, 1, 0, 0))
    throw new InvalidOperationException("Expected a netstandard 2.1.0.0 runtime facade.");

int row = pe.PEHeaders.MetadataStartOffset + metadata.GetTableMetadataOffset(TableIndex.Assembly);
int width = metadata.GetTableRowSize(TableIndex.Assembly);
int strings = width >= 26 ? 4 : 2;
int blobs = width - 16 - 2 * strings;
int keyAt = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
    MetadataTokens.GetHeapOffset(assembly.PublicKey);
int keyPrefix = facade[keyAt] < 0x80 ? 1 : facade[keyAt] < 0xc0 ? 2 : 4;
string[] variants =
[
    WriteVariant("wrong-version", bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(row + 6), 2)),
    WriteVariant("wrong-key", bytes => bytes[keyAt + keyPrefix + metadata.GetBlobBytes(assembly.PublicKey).Length - 1] ^= 1),
    WriteVariant("non-neutral", bytes => bytes.AsSpan(row + 16 + blobs, strings).CopyTo(bytes.AsSpan(row + 16 + blobs + strings)))
];

var cases = new List<object>();
int[] dependencyIds = [2, 3, 4, 5];
for (int source = 0; source < 2; source++)
{
    var context = new AssemblyLoadContext("facade-oracle-" + source, isCollectible: true);
    try
    {
        Assembly input = context.LoadFromAssemblyPath(files[source]);
        AssemblyName reference = input.GetReferencedAssemblies().Single(a => a.Name == "netstandard");
        if (reference.Version != new Version(2, source, 0, 0))
            throw new InvalidOperationException($"Unexpected netstandard reference {reference}.");
        Type entry = input.GetType("FacadeConsumer.Entry", throwOnError: true)!;
        int[] packIds = [source, ..dependencyIds];
        MethodInfo nan = entry.GetMethod("IsNaN")!;
        MethodInfo surrogate = entry.GetMethod("IsHighSurrogate")!;
        MethodInfo list = entry.GetMethod("List")!;
        MethodInfo growth = entry.GetMethod("ListGrowth")!;
        string[] floating = ["NaN", "-Infinity", "Infinity", "-0", "12.5"];
        string[] codes = ["55295", "55296", "56319", "56320", "97"];
        Accept("IsNaN", nan, packIds, floating, floating.Select(value =>
            (int)nan.Invoke(null, [double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)])!).ToArray());
        Accept("IsHighSurrogate", surrogate, packIds, codes, codes.Select(value =>
            (int)surrogate.Invoke(null, [int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)])!).ToArray());

        Accept("List.Add after deferred initialization", list, packIds, ["0"], [(int)list.Invoke(null, null)!]);
        int[] seeds = [int.MinValue, -5, 0, 5, int.MaxValue];
        Accept("List capacity growth", growth, packIds, seeds.Select(value => value.ToString()).ToArray(),
            seeds.Select(value => (int)growth.Invoke(null, [value])!).ToArray());
        if (source != 0) continue;
        int call = Array.IndexOf(nan.GetMethodBody()!.GetILAsByteArray()!, (byte)0x28);
        if (call < 0) throw new InvalidOperationException("Expected a Double.IsNaN call.");
        Reject("missing facade", nan, [source], -4, call);
        Reject("duplicate facade", nan, [source, 2, 2], -5, call);
        Reject("unlisted facade version", nan, [source, 6], -4, call);
        Reject("wrong facade key", nan, [source, 7], -4, call);
        Reject("non-neutral facade", nan, [source, 8], -4, call);
    }
    finally { context.Unload(); }
}

File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files = files.Concat(variants).ToArray(), cases, runtime = Environment.Version.ToString(), pack
}));

string WriteVariant(string name, Action<byte[]> edit)
{
    byte[] modified = (byte[])facade.Clone();
    edit(modified);
    string path = Path.Combine(evidence, name + ".dll");
    File.WriteAllBytes(path, modified);
    return path;
}

static int MethodRow(MethodInfo method) => method.MetadataToken & 0xffffff;

void Accept(string label, MethodInfo method, int[] images, string[] inputs, int[] expected)
{
    cases.Add(new { name = label, row = MethodRow(method), images, inputs, expected });
}

void Reject(string name, MethodInfo method, int[] images, int status, int offset)
{
    cases.Add(new { name, row = MethodRow(method), images, status, offset });
}
