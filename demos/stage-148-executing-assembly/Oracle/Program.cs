using System.Runtime.InteropServices;
using System.Text.Json;
using ExecutingFixture;

var cases = new (string Method, int[] Arguments)[]
{
    (nameof(Kernels.Own), []),
    (nameof(Kernels.ImportedIdentity), []),
    (nameof(Kernels.Pick), [0]),
    (nameof(Kernels.Pick), [1]),
};
var results = cases.Select(test =>
{
    var method = typeof(Kernels).GetMethod(test.Method)!;
    return new
    {
        method = test.Method,
        row = method.MetadataToken & 0xffffff,
        arguments = test.Arguments,
        value = (int)method.Invoke(null, Array.ConvertAll(test.Arguments, value => (object)value))!,
    };
});
File.WriteAllText(args[0], JsonSerializer.Serialize(new { architecture = RuntimeInformation.ProcessArchitecture.ToString(), results }));
