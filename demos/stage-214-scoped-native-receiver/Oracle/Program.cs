using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ScopedNativeReceiver;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.ValueAndFinally, Probe.ExceptionalFinally, Probe.IndependentAliases,
    Probe.EscapedAddressDiagnostic, Probe.InitializedBorrowDiagnostic];
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version.ToString(),
    assembly = typeof(Probe).Assembly.Location,
    dependencies = new[] { typeof(object).Assembly.Location },
    cases = cases.Select(run =>
    {
        int row = run.Method.MetadataToken & 0xffffff;
        int[] location = run.Method.Name switch
        {
            nameof(Probe.EscapedAddressDiagnostic) => [0, row, 8],
            nameof(Probe.InitializedBorrowDiagnostic) => [0,
                typeof(InitializedBorrowScope).GetMethod(nameof(InitializedBorrowScope.Borrow))!.MetadataToken & 0xffffff, 1],
            _ => null
        };
        return new
        {
            name = run.Method.Name, row, expected = run(), memory = location == null,
            rejection = location != null ? new { status = -2, location } : null
        };
    })
}));
