using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 150 (decision 0078). The CLR runs each function-pointer kernel; the runner compiles
// the same rows and compares the results in Node and Edge.
string evidence = args[0], fixture = Path.GetFullPath(args[1]);
Type kernels = Assembly.LoadFrom(fixture).GetType("FunctionPointerFixture.Kernels", throwOnError: true)!;
var cases = new List<object>();
int Row(string name) => kernels.GetMethod(name)!.MetadataToken & 0xffffff;

void Run(string name, params int[] inputs)
{
    MethodInfo method = kernels.GetMethod(name)!;
    var parameters = method.GetParameters();
    var values = new object[inputs.Length];
    for (int i = 0; i < inputs.Length; i++) values[i] = Convert.ChangeType(inputs[i], parameters[i].ParameterType);
    object result = method.Invoke(null, values)!;
    cases.Add(new { name, row = Row(name), inputs, expected = result.ToString()! });
}

for (int which = 0; which <= 2; which++) Run("Select", which, 7, 5);
Run("Select", 1, int.MinValue, 1);
foreach (int count in new[] { 0, 1, 2, 5, 12 }) Run("Fold", count);
for (int which = 0; which <= 2; which++) Run("Field", which, 11);
Run("Constructed", 0, 21);
Run("Constructed", 1, 21);
Run("Wide", 3, 4);
Run("Wide", -7, 2000);
Run("Void", 42);
Run("Void", -1);
foreach (int n in new[] { 0, 1, 6, 12 }) Run("Recurse", n);
Run("Same", 0, 0);
Run("Same", 1, 1);
Run("Same", 0, 2);
Run("IsNull", 0);
Run("IsNull", 2);

// A null call ends the CLR process with an access violation, so it is checked in Wasm only.
var traps = new[] { new { name = "CallNull", row = Row("CallNull"), inputs = new[] { 3 } } };
// Unmanaged calling conventions stay a located diagnostic (#274).
var rejects = new[] { new { name = "RejectUnmanaged", row = Row("RejectUnmanaged"), status = -2, offset = 4 } };
File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    fixture, cases, traps, rejects, runtime = Environment.Version.ToString(),
    process = RuntimeInformation.ProcessArchitecture.ToString()
}));
