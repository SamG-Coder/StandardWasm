using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string coreClr = typeof(string).Assembly.Location;
string mono = args[2];
var bindings = new[] { ReadProfile(coreClr), ReadProfile(mono) };
var real = typeof(string).GetMethod("EqualsOrdinalIgnoreCaseNoLengthCheck", BindingFlags.NonPublic | BindingFlags.Static)!;
bool[] comparisons = [
    (bool)real.Invoke(null, new object[] { "TeXt", "tExT" })!,
    (bool)real.Invoke(null, new object[] { "\u00c9", "\u00e9" })!,
    (bool)real.Invoke(null, new object[] { "\u00c9", "E" })!,
    (bool)real.Invoke(null, new object[] { "", "" })!
];
if (!comparisons.SequenceEqual(new[] { true, true, false, true }))
    throw new InvalidOperationException("Unexpected original ordinal comparison.");
var raw = typeof(string).GetMethod("GetRawStringData", BindingFlags.NonPublic | BindingFlags.Instance)!;
var outcomes = new List<int>();
for (int index = 0; index < 5; index++)
{
    string source = StringBorrowProbe.Input(index);
    if (source is null)
    {
        try { _ = source!.Length; }
        catch (NullReferenceException) { outcomes.Add(-1); }
    }
    else
    {
        int first = (char)raw.Invoke(source, Array.Empty<object>())!;
        outcomes.Add(source.Length == 0 ? first == 0 ? 1 : 0 : first);
    }
}
BorrowEmitterProbe.Write(args[1]);
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(StringBorrowProbe).Assembly.Location,
    coreClr,
    profiles = bindings,
    row = typeof(StringBorrowProbe).GetMethod(nameof(StringBorrowProbe.Input))!.MetadataToken & 0xffffff,
    outcomes
}));

static object ReadProfile(string file)
{
    using var pe = new PEReader(File.OpenRead(file));
    MetadataReader metadata = pe.GetMetadataReader();
    var stringType = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(type =>
        metadata.GetString(type.Namespace) == "System" && metadata.GetString(type.Name) == "String");
    int Find(string name) => MetadataTokens.GetRowNumber(stringType.GetMethods().Single(method =>
        metadata.GetString(metadata.GetMethodDefinition(method).Name) == name));
    int caller = Find("EqualsOrdinalIgnoreCaseNoLengthCheck");
    int raw = Find("GetRawStringData");
    int pinnable = Find("GetPinnableReference");
    byte[] image = File.ReadAllBytes(file);
    if (!BorrowEmitterProbe.CheckMetadata(image, caller, raw) ||
        BorrowEmitterProbe.CheckMetadata(image, caller, pinnable) ||
        BorrowEmitterProbe.CheckMetadata(image, pinnable, raw))
        throw new InvalidOperationException("Ordinal raw character metadata did not authenticate.");
    return new { caller, raw };
}
