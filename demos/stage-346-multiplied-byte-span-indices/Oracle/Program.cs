using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MultipliedByteSpanFixture;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.ProductAndBias, Probe.WrapToValidIndex, Probe.UnsignedShiftAfterMultiply, Probe.ProductOutOfBounds];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run => new
    {
        name = run.Method.Name,
        row = run.Method.MetadataToken & 0xffffff,
        expected = run()
    })
}));
