using System.Runtime.Loader;
using System.Text.Json;

AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
var type = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0])).GetType("AggregateFixture.Kernels")!;
var steps = new (string Name, int[] Inputs)[]
{
    ("ReadDefault", []), ("Initializations", []), ("ReadFixed", []),
    ("ReadLocal", []), ("ReadCurrent", []),
    ("MutateCopy", [91]), ("ReadCurrent", []), ("ReadLocal", []),
    ("SnapshotWhileWriting", [int.MinValue, int.MaxValue]), ("ReadCurrent", []),
    ("WriteCurrent", [-72, 54]), ("ReadCurrent", []), ("ReadLocal", []),
    ("ReadNested", []), ("WriteNested", [13, -7, 3]), ("ReadNested", []),
    ("ReadFixed", []), ("Initializations", []),
};
var entries = steps.Select(step => step.Name).Distinct()
    .Select(name => new { name, row = type.GetMethod(name)!.MetadataToken & 0xffffff });
var calls = steps.Select(step => new { name = step.Name, inputs = step.Inputs }).ToArray();
var expected = calls.Select(call => (int)type.GetMethod(call.name)!.Invoke(null, call.inputs.Cast<object>().ToArray())!).ToArray();
File.WriteAllText(args[2], JsonSerializer.Serialize(new { entries, calls, expected }));
