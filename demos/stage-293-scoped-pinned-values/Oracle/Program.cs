using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ScopedPinnedValues;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.WritesEvenOnFailure, Probe.ArrayElementAlias, Probe.ClassFieldAlias,
    Probe.DefaultAndRepeatedBorrow, Probe.Escapes];
MethodInfo typedNull = typeof(Probe).GetMethod("TypedNull");
if (typedNull != null) cases = [.. cases, typedNull.CreateDelegate<Func<int>>()];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_RUNTIME");
string facades = mono == null ? runtime : Path.Combine(mono, "lib", "net10.0");
string core = mono == null ? typeof(object).Assembly.Location : Path.Combine(mono, "native", "System.Private.CoreLib.dll");
int escapeRow = typeof(Probe).GetMethod("Escape", BindingFlags.Static | BindingFlags.NonPublic)!.MetadataToken & 0xffffff;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(facades, "netstandard.dll"), Path.Combine(facades, "System.Runtime.dll"), core },
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run.Method.Name == nameof(Probe.Escapes) ? 0 : run(),
        memory = true,
        rejection = run.Method.Name == nameof(Probe.Escapes) ? new { status = -1, location = new[] { 0, escapeRow, 1 } } : null
    })
}));
