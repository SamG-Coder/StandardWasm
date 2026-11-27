using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ManagedReferenceJoins;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.InterfaceThenClass, Probe.ClassThenInterface, Probe.CoalescedReferences, Probe.ClassAndBase,
    Probe.NullAndLoop, Probe.SwitchAndLoop, Probe.UnrelatedDiagnostic];
int unrelated = typeof(Probe).GetMethod("UnrelatedJoin", BindingFlags.Static | BindingFlags.NonPublic)!
    .MetadataToken & 0xffffff;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(object).Assembly.Location },
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run(),
        rejection = run.Method.Name == nameof(Probe.UnrelatedDiagnostic)
            ? new { status = -1, location = new[] { 0, unrelated, 6 } } : null
    })
}));
