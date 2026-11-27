using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DefaultEqualityComparerFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
var cases = new (Func<int> Run, object Comparer, Type Template, Type Argument)[]
{
    (Probe.Strings, EqualityComparer<string>.Default, null, null),
    (Probe.EquatableValues, EqualityComparer<Bucket>.Default, typeof(string), typeof(Bucket)),
    (Probe.NullableValues, EqualityComparer<Bucket?>.Default, typeof(int), typeof(Bucket)),
    (Probe.EnumValues, EqualityComparer<Severity>.Default, null, typeof(Severity)),
    (Probe.ObjectFallback, EqualityComparer<NamedValue>.Default, typeof(object), typeof(NamedValue)),
    (Probe.CachedIdentity, EqualityComparer<Bucket>.Default, typeof(string), typeof(Bucket))
};
var coreAssembly = typeof(object).Assembly;
var runtimeType = coreAssembly.GetType("System.RuntimeType", throwOnError: true);
var activate = typeof(RuntimeTypeHandle).GetMethod("CreateInstanceForAnotherGenericParameter",
    BindingFlags.NonPublic | BindingFlags.Static, [runtimeType, runtimeType]);
if (activate == null) throw new InvalidOperationException("The CoreCLR activation wrapper was not found.");

// Confirm the real wrapper accepts both open definitions and closed templates.
foreach (var test in cases)
{
    if (test.Argument == null) continue;
    var definition = test.Comparer.GetType().GetGenericTypeDefinition();
    var template = test.Template == null ? definition : definition.MakeGenericType(test.Template);
    var constructed = activate.Invoke(null, [template, test.Argument]);
    if (constructed.GetType() != test.Comparer.GetType() || ReferenceEquals(constructed, test.Comparer))
        throw new InvalidOperationException("Unexpected activation for " + test.Run.Method.Name);
}

string core = typeof(object).Assembly.Location;
string runtime = Path.GetDirectoryName(core);
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"),
        Path.Combine(runtime, "System.Collections.dll"), Path.Combine(runtime, "System.Numerics.Vectors.dll"), core },
    cases = cases.Select(test => new
    {
        name = test.Run.Method.Name,
        row = test.Run.Method.MetadataToken & 0xffffff,
        expected = test.Run(),
        comparer = test.Comparer.GetType().Name,
        rejection = ExpectedDiagnostic(test.Run.Method.Name)
    })
}));

object ExpectedDiagnostic(string fixture)
{
    var target = fixture switch
    {
        nameof(Probe.Strings) => ("GenericEqualityComparer", "Equals", -1, 19),
        nameof(Probe.NullableValues) => ("ObjectEqualityComparer", "Equals", -2, 1),
        nameof(Probe.EnumValues) => ("ObjectEqualityComparer", "Equals", -2, 16),
        nameof(Probe.ObjectFallback) => ("ObjectEqualityComparer", "GetHashCode", -2, 37),
        _ => (null, null, 0, 0)
    };
    if (target.Item1 == null) return null;
    var declaring = coreAssembly.GetType("System.Collections.Generic." + target.Item1 + "`1", throwOnError: true);
    int parameters = target.Item2 == "Equals" ? 2 : 1;
    var method = declaring.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Single(candidate => candidate.Name == target.Item2 && candidate.GetParameters().Length == parameters);
    return new { status = target.Item3, location = new[] { 5, method.MetadataToken & 0xffffff, target.Item4 } };
}
