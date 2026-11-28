using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using MathFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
string packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
string core = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_CORELIB") ??
    Path.Combine(packages, "microsoft.netcore.app.runtime.mono.browser-wasm", Environment.Version.ToString(),
        "runtimes", "browser-wasm", "native", "System.Private.CoreLib.dll");
using var pe = new PEReader(File.OpenRead(core));
var metadata = pe.GetMetadataReader();
var math = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(type =>
    metadata.GetString(type.Namespace) == "System" && metadata.GetString(type.Name) == "Math");
byte[] signature = [0, 1, 0x0d, 0x0d];
int row = MetadataTokens.GetRowNumber(math.GetMethods().Single(handle =>
{
    var method = metadata.GetMethodDefinition(handle);
    return metadata.GetString(method.Name) == "Sqrt" && method.RelativeVirtualAddress == 0 &&
        (method.ImplAttributes & MethodImplAttributes.InternalCall) != 0 &&
        metadata.GetBlobBytes(method.Signature).SequenceEqual(signature);
}));
double[] inputs = [0.0, -0.0, 1.0, 2.0, 144.0, double.Epsilon, double.MaxValue,
    double.PositiveInfinity, double.NegativeInfinity, -1.0, double.NaN];
string Bits(double value) => BitConverter.DoubleToInt64Bits(value).ToString("x16");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    clr = Environment.Version.ToString(),
    fixture = typeof(Probe).Assembly.Location,
    fixtureRow = typeof(Probe).GetMethod(nameof(Probe.Sqrt))!.MetadataToken & 0xffffff,
    core, coreRow = row,
    cases = inputs.Select(input => new
    {
        input = Bits(input),
        expected = double.IsNaN(Probe.Sqrt(input)) ? "nan" : Bits(Probe.Sqrt(input))
    })
}));
