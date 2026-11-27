using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 149 (decision 0077). The CLR runs every kernel of both fixtures: the netstandard2.0
// build pins through RuntimeHelpers.OffsetToStringData and the net10.0 build through
// string.GetPinnableReference(). The runner compiles the same rows and compares results.
string evidence = args[0];
var modules = new List<object>();
for (int i = 1; i < args.Length; i++)
{
    string fixture = Path.GetFullPath(args[i]);
    Type kernels = Assembly.LoadFrom(fixture).GetType("FixedStringFixture.Kernels", throwOnError: true)!;
    var cases = new List<object>();
    int Row(string name) => kernels.GetMethod(name)!.MetadataToken & 0xffffff;
    void Run(string name, params int[] inputs)
    {
        object? result = kernels.GetMethod(name)!.Invoke(null, inputs.Cast<object>().ToArray());
        cases.Add(new { name, row = Row(name), inputs, expected = ((int)result!).ToString() });
    }
    foreach (string name in new[] { "First", "Weighted", "Terminator", "Measure", "Reverse", "Repeated", "Thrown", "ReadAfterCall" })
        for (int selection = 0; selection <= 5; selection++) Run(name, selection);
    foreach (var (left, right) in new[] { (2, 3), (2, 2), (3, 5), (1, 2), (5, 5), (5, 2), (4, 1), (0, 2) })
        Run("Compare", left, right);
    // Pinned arrays and element addresses stay diagnosed (#271, #273).
    var rejects = new[] { new { name = "RejectArray", row = Row("RejectArray"), status = -2, offset = -1 },
        new { name = "RejectElement", row = Row("RejectElement"), status = -2, offset = 2 } };
    modules.Add(new { fixture, cases, rejects });
}
File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    modules, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));
