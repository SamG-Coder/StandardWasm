using System.Runtime.Loader;
using System.Text.Json;

var type = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0])).GetType("AddressedValueFixture.Kernels")!;
var inputs = new[] { new[] { 3, 9 }, new[] { -4, 3 }, new[] { int.MinValue, int.MaxValue } };
var methods = new[] { "Read", "MutateCopy", "ClearCopy" }.Select(name => type.GetMethod(name)!).ToArray();
var entries = methods.Select(method => new { name = method.Name, row = method.MetadataToken & 0xffffff });
var calls = methods.SelectMany(method => inputs.Select(args => new { name = method.Name, inputs = args })).ToArray();
var expected = calls.Select(call => (int)type.GetMethod(call.name)!.Invoke(null, call.inputs.Cast<object>().ToArray())!).ToArray();
File.WriteAllText(args[1], JsonSerializer.Serialize(new { entries, calls, expected }));
