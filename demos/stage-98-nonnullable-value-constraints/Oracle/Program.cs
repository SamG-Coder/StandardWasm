using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using NullableRuntimeFixture;
using Cecil = Mono.Cecil;

string output = Path.GetFullPath(args.Single());
string app = typeof(Kernels).Assembly.Location, core = typeof(object).Assembly.Location;
string facade = Path.Combine(Path.GetDirectoryName(core)!, "System.Runtime.dll");
string invalid = Path.Combine(Path.GetDirectoryName(output)!, "InvalidNestedNullable.dll");
var frame = new Frame(0);
if (frame.Flag.HasValue || frame.Count.HasValue ||
    typeof(bool?).GetField("hasValue", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType != typeof(bool) ||
    typeof(bool?).GetField("value", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType != typeof(bool))
    throw new InvalidOperationException("Unexpected installed CLR Nullable<Boolean> layout or default fields.");

using (var assembly = Cecil.AssemblyDefinition.ReadAssembly(app))
{
    var module = assembly.MainModule;
    var method = module.GetType(typeof(Kernels).FullName).Methods.Single(candidate => candidate.Name == nameof(Kernels.RejectNested));
    var constraint = (Cecil.GenericInstanceType)method.Parameters[0].ParameterType;
    constraint.GenericArguments[0] = module.ImportReference(typeof(int?));
    assembly.Write(invalid);
}

var context = new AssemblyLoadContext("Nested Nullable oracle", isCollectible: true);
try
{
    var method = context.LoadFromAssemblyPath(invalid).GetType(typeof(Kernels).FullName!)!.GetMethod(nameof(Kernels.RejectNested))!;
    method.Invoke(null, [null]);
    throw new InvalidOperationException("The CLR accepted Nullable<Int32> under a non-nullable constraint.");
}
catch (TypeLoadException) { }
catch (TargetInvocationException error) when (error.InnerException is TypeLoadException) { }
finally { context.Unload(); }

int Row(string name) => typeof(Kernels).GetMethod(name)!.MetadataToken & 0xffffff;
File.WriteAllText(output, JsonSerializer.Serialize(new
{
    app, core, facade, invalid, runtime = RuntimeInformation.FrameworkDescription,
    coreIdentity = typeof(object).Assembly.FullName, row = Row(nameof(Kernels.Run)), rejectedRow = Row(nameof(Kernels.RejectNested)),
    cases = new[] { int.MinValue, -7, 0, 13, int.MaxValue }.Select(value => new { value, expected = Kernels.Run(value) })
}));
