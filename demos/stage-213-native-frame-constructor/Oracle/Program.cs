using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NativeFrameConstructor;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.PointerArgument, Probe.ZeroArgument, Probe.LoadedArgument, Probe.IndependentOwners,
    Probe.ReinitializeBorrowedOwner, Probe.SideEffectDiagnostic, Probe.TypeInitializerDiagnostic];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(object).Assembly.Location },
    cases = cases.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        int? unsupported = run.Method.Name switch
        {
            nameof(Probe.SideEffectDiagnostic) => 6,
            nameof(Probe.TypeInitializerDiagnostic) => 0,
            _ => null
        };
        return new
        {
            name = run.Method.Name, row, expected = run(), memory = !unsupported.HasValue,
            rejection = unsupported.HasValue ? new { status = -2, location = new[] { 0, row, unsupported.Value } } : null
        };
    })
}));
