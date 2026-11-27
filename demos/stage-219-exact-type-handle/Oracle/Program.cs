#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ExactTypeHandle;

if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
Func<int>[] cases = [Probe.Nominal, Probe.ClosedGeneric, Probe.GenericParameter, Probe.OtherDeclaringType, Probe.UnprovedReceiverDiagnostic, Probe.BranchReceiverDiagnostic];
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
    cases = cases.Select(run =>
    {
        string? targetMethod = run.Method.Name switch
        {
            nameof(Probe.UnprovedReceiverDiagnostic) => "ReadHandle",
            nameof(Probe.BranchReceiverDiagnostic) => "BranchReceiver",
            _ => null
        };
        int target = targetMethod == null ? 0 :
            typeof(Probe).GetMethod(targetMethod, BindingFlags.NonPublic | BindingFlags.Static)!.MetadataToken & 0xffffff;
        int offset = targetMethod == "ReadHandle" ? 11 : 35;
        return new
        {
            name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run(),
            rejection = target != 0 ? new { status = -2, location = new[] { 0, target, offset } } : null
        };
    })
}));
