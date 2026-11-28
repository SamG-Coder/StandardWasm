using System.Text.Json;
using NativeUnary;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] probes = [Probe.PositiveComplement, Probe.ZeroComplement, Probe.LocalZeroComplement, Probe.NegativeComplement,
    Probe.UnsignedComplement, Probe.NegativeWord, Probe.MinimumNegation, Probe.ClearTaggedWord];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    cases = probes.Select(run => new
    {
        name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run()
    })
}));
