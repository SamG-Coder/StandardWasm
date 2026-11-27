using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ObsoleteStaticFields;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.ReadInitializer, Probe.PreserveArrayAlias, Probe.UpdateScalar, Probe.RejectOtherAttribute];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        return new
        {
            name = run.Method.Name, row, expected = run(),
            rejection = run.Method.Name == nameof(Probe.RejectOtherAttribute)
                ? new { status = -2, location = new[] { 0, row, 0 } } : null
        };
    })
}));
