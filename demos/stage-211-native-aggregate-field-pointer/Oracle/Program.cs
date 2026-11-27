using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NativeAggregateFieldPointer;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.PointerWrites, Probe.ClearAndReborrow, Probe.IndependentOwners,
    Probe.TypedReplacement, Probe.ManagedFieldDiagnostic];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run =>
    {
        string name = run.Method.Name;
        bool rejected = name == nameof(Probe.ManagedFieldDiagnostic);
        return new
        {
            name,
            row = run.Method.MetadataToken & 0xffffff,
            expected = run(),
            memory = !rejected,
            rejection = rejected ? new
            {
                status = -2,
                location = new[] { 0, typeof(MixedPair).GetMethod(nameof(MixedPair.Borrow))!.MetadataToken & 0xffffff, 1 }
            } : null
        };
    })
}));
