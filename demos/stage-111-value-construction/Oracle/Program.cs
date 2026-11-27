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
var clearKey = typeof(EntryArrayProbe).GetMethod("ClearKey", BindingFlags.Static | BindingFlags.NonPublic)!;
byte[] clearIl = clearKey.GetMethodBody()!.GetILAsByteArray()!;
if (clearIl[1] != 0x7c || clearIl[6] != 0xfe || clearIl[7] != 0x15)
    throw new InvalidOperationException("The generic fixture must initialize the field through its managed address.");
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    inputs,
    values = inputs.Select(Kernels.InvokeValueOverride).ToArray(),
    typeBranches = inputs.Select(Kernels.CheckTypeBranches).ToArray(),
    debuggerStatus = DebuggerProbe.ReadManagedDebuggerStatus(),
    closedClasses = inputs.Select(ClosedClassProbe.CheckClosedClassSlots).ToArray(),
    closedClassNull = inputs.Select(ClosedClassProbe.CheckClosedClassNull).ToArray(),
    closedInterfaces = inputs.Select(ClosedInterfaceProbe.CheckClosedInterface).ToArray(),
    closedInterfaceNull = inputs.Select(ClosedInterfaceProbe.CheckClosedInterfaceNull).ToArray(),
    interfaceCasts = inputs.Select(InterfaceCastProbe.CheckInterfaceCasts).ToArray(),
    objectTypes = inputs.Select(ObjectTypeProbe.CheckObjectType).ToArray(),
    vectorTypes = inputs.Select(VectorTypeProbe.CheckVectorTypes).ToArray(),
    primitiveHashes = inputs.Select(PrimitiveTypeProbe.CheckPrimitiveHash).ToArray(),
    primitiveDescriptors = inputs.Select(PrimitiveTypeProbe.CheckPrimitiveDescriptors).ToArray(),
    closedValueTypes = inputs.Select(PrimitiveTypeProbe.CheckClosedValueType).ToArray(),
    specializedTypeResults = inputs.Select(PrimitiveTypeProbe.CheckSpecializedTypeResults).ToArray(),
    virtualTypeResults = inputs.Select(PrimitiveTypeProbe.CheckVirtualTypeResults).ToArray(),
    staticInt32Aliases = inputs.Select(StaticInt32Probe.CheckStaticInt32Aliases).ToArray(),
    referenceContainment = inputs.Select(ReferenceContainmentProbe.CheckReferenceContainment).ToArray(),
    entryAliases = inputs.Select(EntryArrayProbe.UpdateEntryArray).ToArray(),
    interiorFields = inputs.Select(EntryArrayProbe.ClearInteriorFields).ToArray(),
    mutableReceivers = inputs.Select(MutableReceiverProbe.CheckMutableReceiver).ToArray(),
    constrainedInterfaces = inputs.Select(MutableReceiverProbe.CheckConstrainedInterface).ToArray(),
    exceptionalMutation = inputs.Select(MutableReceiverProbe.CheckExceptionalMutation).ToArray(),
    closedReceivers = inputs.Select(MutableReceiverProbe.CheckClosedReceiver).ToArray(),
    frameworkReceivers = inputs.Select(MutableReceiverProbe.CheckFrameworkReceiver).ToArray(),
    entryFailures = inputs.Select(EntryArrayProbe.CheckEntryArrayFailure).ToArray(),
    bucketAliases = inputs.Select(BucketProbe.ReadManagedBuckets).ToArray(),
    bucketFailures = inputs.Select(BucketProbe.CheckBucketFailure).ToArray(),
    returnedLocations = inputs.Select(ByRefReturnProbe.CheckReturnedLocations).ToArray(),
    returnedLocationFailures = inputs.Select(ByRefReturnProbe.CheckReturnedLocationFailure).ToArray(),
    locationExportDiagnostic = new[] { 0, typeof(ByRefReturnProbe).GetMethod(nameof(ByRefReturnProbe.RejectExportedLocation))!.MetadataToken & 0xffffff, -1 },
    bucketExportDiagnostic = new[] { 0, typeof(BucketProbe).GetMethod(nameof(BucketProbe.RejectExportedBucket))!.MetadataToken & 0xffffff, -1 },
    boxingDiagnostic = new[] { 0, generic.MetadataToken & 0xffffff, 1 }
}));
