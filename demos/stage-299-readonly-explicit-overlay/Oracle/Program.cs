using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ReadOnlyExplicitOverlay;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.ClosedGenericOut, Probe.DirectUnsigned, Probe.FloatBits, Probe.DefaultAndIndependentSnapshots,
    Probe.NonzeroOffsetOnly, Probe.EscapingAddress];
string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
string mono = Environment.GetEnvironmentVariable("STANDARDWASM_MONO_RUNTIME");
string facades = mono == null ? runtime : Path.Combine(mono, "lib", "net10.0");
string core = mono == null ? typeof(object).Assembly.Location : Path.Combine(mono, "native", "System.Private.CoreLib.dll");
Assembly fixture = typeof(Probe).Assembly;
int RejectedRow(string type, string method) => fixture.GetType("ReadOnlyExplicitOverlay." + type, throwOnError: true)!
    .GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!.MetadataToken & 0xffffff;
int noView = RejectedRow("UnsupportedValue", "ReadUndeclaredView");
int escape = RejectedRow("TaggedValue", "ReturnAlias");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { Path.Combine(facades, "netstandard.dll"), Path.Combine(facades, "System.Runtime.dll"),
        Path.Combine(facades, "System.Runtime.CompilerServices.Unsafe.dll"), core },
    cases = cases.Select(run =>
    {
        string name = run.Method.Name;
        int rejected = name == nameof(Probe.NonzeroOffsetOnly) ? noView : name == nameof(Probe.EscapingAddress) ? escape : 0;
        return new
        {
            name,
            row = run.Method.MetadataToken & 0xffffff,
            expected = run(),
            rejection = rejected == 0 ? null : new { status = -2, location = new[] { 0, rejected, 1 } }
        };
    })
}));
