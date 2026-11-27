using System;
using System.Buffers.Binary;
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

const int handleConstruction = 0x13;
byte[] activationBody = activate.GetMethodBody().GetILAsByteArray();
if (activationBody.Length < handleConstruction + 5 || activationBody[handleConstruction] != 0x73 ||
    activate.Module.ResolveMethod(BinaryPrimitives.ReadInt32LittleEndian(activationBody.AsSpan(handleConstruction + 1)))
        .DeclaringType?.FullName != "System.Runtime.CompilerServices.QCallTypeHandle")
    throw new InvalidOperationException("The CoreCLR handle-construction checkpoint changed.");

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
        rejection = new { status = -2, location = new[] { 5, activate.MetadataToken & 0xffffff, handleConstruction } }
    })
}));
