using System.Text.Json;
using UInt32ByrefFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("The fixture uses the x86 CLR oracle.");
var inputs = new[] { 0u, 7u, 0x80000000u, 0xfedcba98u, uint.MaxValue };
var methods = new (string Name, Func<uint, uint> Invoke)[]
{
    (nameof(Kernels.DirectOut), Kernels.DirectOut),
    (nameof(Kernels.DirectRef), Kernels.DirectRef),
    (nameof(Kernels.ClosedGenericOut), Kernels.ClosedGenericOut),
    (nameof(Kernels.ClosedGenericRef), Kernels.ClosedGenericRef),
    (nameof(Kernels.SharedRef), Kernels.SharedRef),
    (nameof(Kernels.NominalOverload), Kernels.NominalOverload)
};
var cases = methods.Select(method => new
{
    name = method.Name,
    row = typeof(Kernels).GetMethod(method.Name)!.MetadataToken & 0xffffff,
    values = inputs.Select(input => new { input, result = method.Invoke(input) })
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new { clr = Environment.Version.ToString(), cases }));
