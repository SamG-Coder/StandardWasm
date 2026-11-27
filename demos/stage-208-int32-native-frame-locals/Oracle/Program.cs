using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Int32NativeFrameLocals;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
Func<int>[] cases = [Probe.SignedAlias, Probe.UnsignedAlias, Probe.ReusedFrame, Probe.IndependentCells,
    Probe.MixedAddress, Probe.BooleanAddress, Probe.ShortAddress];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = cases.Select(run =>
    {
        string name = run.Method.Name;
        int row = run.Method.MetadataToken & 0xffffff;
        int offset = name == nameof(Probe.MixedAddress) ? 6 :
            name == nameof(Probe.BooleanAddress) ? 4 :
            name == nameof(Probe.ShortAddress) ? 2 : -1;
        return new
        {
            name,
            row,
            expected = run(),
            memory = offset < 0,
            rejection = offset < 0 ? null : new
            {
                status = name == nameof(Probe.BooleanAddress) ? -1 : -2,
                location = new[] { 0, row, offset }
            }
        };
    })
}));
