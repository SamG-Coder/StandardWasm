using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text.Json;

// Stage 127 (decision 0055). The CLR runs each caller against the provider under test
// and enforces InternalsVisibleTo itself: a granted pair returns values, a denied pair
// throws MethodAccessException or FieldAccessException.
string evidence = args[0], stage = Path.GetFullPath(args[1]);
string Bin(string kind, string variant, string name) =>
    Path.Combine(stage, kind, variant, "bin", "Release", "netstandard2.0", name + ".dll");

var files = new List<string>();
var cases = new List<object>();
int[] inputs = [-7, 0, 5, 1000];

Pair("unsigned grant", "Unsigned", "UnsignedGrant");
Pair("signed grant", "Signed", "SignedGrant");
Pair("missing grant", "Unsigned", "Ungranted");
Pair("wrong caller key", "WrongKey", "SignedGrant");

File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new { files, cases }));

void Pair(string label, string callerVariant, string providerVariant)
{
    string caller = Bin("Caller", callerVariant, "FriendCaller");
    string provider = Bin("Provider", providerVariant, "FriendProvider");
    int[] images = [Add(caller), Add(provider)];

    // A fresh context per pair, so each caller binds to exactly this provider.
    var context = new AssemblyLoadContext(label, isCollectible: true);
    context.Resolving += (_, name) => name.Name == "FriendProvider" ? context.LoadFromAssemblyPath(provider) : null;
    Type type = context.LoadFromAssemblyPath(caller).GetType("FriendFixture.Caller", throwOnError: true)!;
    foreach (string name in new[] { "Run", "ComputeOnly", "PublicOnly", "ProtectedOnly",
        "ReadInternalField", "WriteInternalField", "WriteProtectedInternalStatic" })
    {
        MethodInfo method = type.GetMethod(name)!;
        int row = method.MetadataToken & 0xffffff;
        var values = new List<string>();
        string? denied = null;
        foreach (int input in inputs)
        {
            try { values.Add(((int)method.Invoke(null, [input])!).ToString()); }
            catch (TargetInvocationException error) when (error.InnerException is MethodAccessException or FieldAccessException)
            {
                denied = error.InnerException.GetType().FullName;
                break;
            }
        }
        if (denied != null)
        {
            OpCode access = name switch
            {
                "ReadInternalField" => OpCodes.Ldfld,
                "WriteInternalField" => OpCodes.Stfld,
                "WriteProtectedInternalStatic" => OpCodes.Stsfld,
                _ => OpCodes.Call
            };
            int offset = IlOffset(method.GetMethodBody()!.GetILAsByteArray()!, access);
            int status = denied == typeof(FieldAccessException).FullName ? -2 : -6;
            cases.Add(new { label, name, row, images, clr = denied, status, diagnostic = new[] { 0, row, offset } });
        }
        else cases.Add(new { label, name, row, images, inputs, clr = values });
    }
    context.Unload();
}

int Add(string path)
{
    int index = files.IndexOf(path);
    if (index >= 0) return index;
    files.Add(path);
    return files.Count - 1;
}

// Offset of the first instruction with this one-byte opcode, walking operands.
static int IlOffset(byte[] il, OpCode target)
{
    for (int cursor = 0; cursor < il.Length;)
    {
        int offset = cursor, op = il[cursor++];
        if (op == target.Value) return offset;
        cursor += op switch
        {
            >= 0x02 and <= 0x0d or 0x14 or 0x25 or 0x26 or 0x2a or 0x58 or 0x59 or 0x5a or 0x5f or 0x60 or 0x61 => 0,
            0x0e or 0x0f or 0x10 or 0x11 or 0x12 or 0x13 or 0x1f or >= 0x2b and <= 0x37 => 1,
            _ => 4
        };
    }
    throw new InvalidOperationException($"No {target.Name} instruction found.");
}
