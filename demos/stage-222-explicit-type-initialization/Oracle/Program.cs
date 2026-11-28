using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ExplicitTypeInitialization;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.PreciselyOnce, Probe.ForcesBeforeFieldInit, Probe.OpenTypeAndArray, Probe.IndependentGenericInitializers,
    Probe.RecursiveInitializer, Probe.CachedFailure, Probe.AbsentInitializer, Probe.UnprovedHandleDiagnostic];
int unproved = typeof(Probe).GetMethod("TriggerUnprovedHandle", BindingFlags.Static | BindingFlags.NonPublic)!.MetadataToken & 0xffffff;
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[]
    {
        Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll"),
        Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll"),
        typeof(object).Assembly.Location
    },
    cases = cases.Select(run => new
    {
        name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run(),
        rejection = run.Method.Name == nameof(Probe.UnprovedHandleDiagnostic)
            ? new { status = -2, location = new[] { 0, unproved, 1 } } : null
    })
}));
