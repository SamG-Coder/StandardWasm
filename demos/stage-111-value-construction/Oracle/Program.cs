using System.Reflection;
using System.Text.Json;
using ValueConstructionDemo;

int[] inputs = [int.MinValue, -5, 0, 5, int.MaxValue];
var generic = typeof(Kernels).GetMethod("ConstrainedHash", BindingFlags.Static | BindingFlags.NonPublic)!;
byte[] il = generic.GetMethodBody()!.GetILAsByteArray()!;
if (il[0] != 0x02 || il[1] != 0xfe || il[2] != 0x16)
    throw new InvalidOperationException("The fixture must take the constrained call path.");
var bucketProbe = typeof(BucketProbe).GetMethod(nameof(BucketProbe.ReadManagedBuckets))!;
if (!bucketProbe.GetMethodBody()!.LocalVariables.Any(local => local.LocalType == typeof(int).MakeByRefType()))
    throw new InvalidOperationException("The fixture must keep an Int32 reference in a managed local.");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    inputs,
    values = inputs.Select(Kernels.InvokeValueOverride).ToArray(),
    typeBranches = inputs.Select(Kernels.CheckTypeBranches).ToArray(),
    debuggerStatus = DebuggerProbe.ReadManagedDebuggerStatus(),
    entryAliases = inputs.Select(EntryArrayProbe.UpdateEntryArray).ToArray(),
    entryFailures = inputs.Select(EntryArrayProbe.CheckEntryArrayFailure).ToArray(),
    bucketAliases = inputs.Select(BucketProbe.ReadManagedBuckets).ToArray(),
    bucketFailures = inputs.Select(BucketProbe.CheckBucketFailure).ToArray(),
    bucketExportDiagnostic = new[] { 0, typeof(BucketProbe).GetMethod(nameof(BucketProbe.RejectExportedBucket))!.MetadataToken & 0xffffff, -1 },
    boxingDiagnostic = new[] { 0, generic.MetadataToken & 0xffffff, 1 }
}));
