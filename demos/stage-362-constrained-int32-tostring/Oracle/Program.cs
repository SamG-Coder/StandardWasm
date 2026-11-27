using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using ConstrainedInt32ToString;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Each helper must contain the exact shape under test. Returns the IL offset of
// constrained. (FE 16 <token>) when it is followed by callvirt.
static int ConstrainedOffset(string name)
{
    byte[] il = typeof(Probe).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!
        .GetMethodBody()!.GetILAsByteArray()!;
    int prefix = Array.IndexOf(il, (byte)0xfe);
    if (prefix < 0 || prefix + 6 >= il.Length || il[prefix + 1] != 0x16 || il[prefix + 6] != 0x6f)
        throw new InvalidDataException(name + ": expected constrained. followed by callvirt.");
    return prefix;
}

static int Row(string name) =>
    typeof(Probe).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.MetadataToken & 0xffffff;

foreach (string name in new[] { "Format", "Same", "Hash", "Inherited" }) ConstrainedOffset(name);
byte[] boxed = typeof(Probe).GetMethod("FormatBoxed", BindingFlags.Static | BindingFlags.NonPublic)!
    .GetMethodBody()!.GetILAsByteArray()!;
if (Array.IndexOf(boxed, (byte)0x8c) < 0 || Array.IndexOf(boxed, (byte)0x6f) < 0)
    throw new InvalidDataException("FormatBoxed: expected box followed by callvirt.");

string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core)!;
string linkedCore = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ?? core;
Func<int>[] checks = [Probe.EqualsStillDirect, Probe.AuthoredValue, Probe.Int32Hash];
var cases = checks.Select(run => (object)new
{
    name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run()
}).ToList();

// The constrained and boxed Int32 ToString calls now link to the original
// Int32.ToString override. Its body reaches Number.NegativeInt32ToDecStr, which
// writes digits through a pinned char& into a fresh FastAllocateString result.
// Both CoreLib profiles keep that located diagnostic until fresh-string writes
// are supported. Bundle index 2 is the linked CoreLib.
int[] formatting = FreshStringPin(linkedCore, "Number", "NegativeInt32ToDecStr");
foreach (Func<int> run in new Func<int>[] { Probe.Zero, Probe.Positive, Probe.Minimum, Probe.Maximum,
    Probe.BoxedPositive, Probe.BoxedMinimum, Probe.UpdatedLocal })
    cases.Add(new
    {
        name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run(),
        rejection = new { status = -2, location = new[] { 2, formatting[0], formatting[1] } }
    });

// A value type that inherits Object.ToString needs a boxed receiver. It keeps
// its diagnostic at the constrained prefix.
cases.Add(new
{
    name = nameof(Probe.InheritedValue), row = ((Func<int>)Probe.InheritedValue).Method.MetadataToken & 0xffffff,
    expected = Probe.InheritedValue(),
    rejection = new { status = -2, location = new[] { 0, Row("Inherited"), ConstrainedOffset("Inherited") } }
});

File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "System.Runtime.dll"), linkedCore },
    cases
}));

// Returns [MethodDef row, IL offset] of the first String.GetPinnableReference
// call in System.{type}.{method} of the CoreLib image used for linking.
static int[] FreshStringPin(string path, string type, string method)
{
    using var pe = new PEReader(File.OpenRead(path));
    MetadataReader md = pe.GetMetadataReader();
    foreach (MethodDefinitionHandle handle in md.MethodDefinitions)
    {
        MethodDefinition definition = md.GetMethodDefinition(handle);
        TypeDefinition owner = md.GetTypeDefinition(definition.GetDeclaringType());
        if (md.GetString(owner.Namespace) != "System" || md.GetString(owner.Name) != type ||
            md.GetString(definition.Name) != method) continue;
        byte[] il = pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()!;
        for (int at = 0; at + 5 <= il.Length; at++)
        {
            if (il[at] != 0x28) continue; // call
            var target = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, at + 1));
            if (target.Kind != HandleKind.MethodDefinition) continue;
            MethodDefinition callee = md.GetMethodDefinition((MethodDefinitionHandle)target);
            if (md.GetString(callee.Name) == "GetPinnableReference" &&
                md.GetString(md.GetTypeDefinition(callee.GetDeclaringType()).Name) == "String")
                return [MetadataTokens.GetRowNumber(handle), at];
        }
    }
    throw new InvalidDataException("The linked CoreLib has no " + type + "." + method + " string pin.");
}
