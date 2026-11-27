using System.Reflection;
using System.Text.Json;
using ValueConstructionDemo;

int[] inputs = [int.MinValue, -5, 0, 5, int.MaxValue];
var generic = typeof(Kernels).GetMethod("ConstrainedHash", BindingFlags.Static | BindingFlags.NonPublic)!;
byte[] il = generic.GetMethodBody()!.GetILAsByteArray()!;
if (il[0] != 0x02 || il[1] != 0xfe || il[2] != 0x16)
    throw new InvalidOperationException("The fixture must take the constrained call path.");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    inputs,
    values = inputs.Select(Kernels.InvokeValueOverride).ToArray(),
    boxingDiagnostic = new[] { 0, generic.MetadataToken & 0xffffff, 1 }
}));
