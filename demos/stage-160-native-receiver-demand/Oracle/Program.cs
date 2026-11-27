using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using BootstrapLibrary;
using ReceiverDemandFixture;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
        string assembly = typeof(Probe).Assembly.Location;
        byte[] image = File.ReadAllBytes(assembly);
        var bundle = new byte[4 + image.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bundle, image.Length);
        image.CopyTo(bundle, 4);
        (Func<int> Run, int Carriers)[] tests = [
            (Probe.NativeOnly, 1), (Probe.Mixed, 0), (Probe.NativeGenerics, 2)
        ];
        var cases = tests.Select(test =>
        {
            int row = test.Run.Method.MetadataToken & 0xffffff;
            byte[] entries = [0, 0, 0, 0, 3, 0, 0, 0, 114, 117, 110];
            BinaryPrimitives.WriteInt32LittleEndian(entries, row);
            var output = new byte[1 << 20];
            int[] location = CompilerExports.CreateDiagnostic();
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1)
                throw new InvalidOperationException($"{test.Run.Method.Name} returned {length} at [{string.Join(", ", location)}].");
            return new
            {
                name = test.Run.Method.Name, row, value = test.Run(), carriers = test.Carriers,
                hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant()
            };
        }).ToArray();
        File.WriteAllText(args[0], JsonSerializer.Serialize(new {
            clr = Environment.Version.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            assembly, cases
        }));
    }
}
