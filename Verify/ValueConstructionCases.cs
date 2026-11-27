using System.Buffers.Binary;
using System.Reflection;
using BootstrapLibrary;
using ValueConstructionDemo;
using ValueConstructionDependency;

// Stage 111: newobj and in-place construction of local and linked value types.
internal static class ValueConstructionCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location),
            File.ReadAllBytes(typeof(Extent).Assembly.Location));
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        Check(nameof(Kernels.Run), Kernels.Run);
        Check(nameof(Kernels.Defaults), Kernels.Defaults);
        Check(nameof(Kernels.Fields), Kernels.Fields);
        Check(nameof(Kernels.Copies), Kernels.Copies);
        Check(nameof(Kernels.Chained), Kernels.Chained);
        Check(nameof(Kernels.Argument), Kernels.Argument);
        Check(nameof(Kernels.Guarded), Kernels.Guarded);
        Check(nameof(Kernels.DisposeScoped), Kernels.DisposeScoped);

        Check(nameof(Kernels.BorrowExtent), Kernels.BorrowExtent);
        Check(nameof(ClosedClassProbe.CheckClosedClassSlots), ClosedClassProbe.CheckClosedClassSlots);
        Check(nameof(ClosedClassProbe.CheckClosedClassNull), ClosedClassProbe.CheckClosedClassNull);
        Check(nameof(ClosedInterfaceProbe.CheckClosedInterface), ClosedInterfaceProbe.CheckClosedInterface);
        Check(nameof(ClosedInterfaceProbe.CheckClosedInterfaceNull), ClosedInterfaceProbe.CheckClosedInterfaceNull);
        Check(nameof(InterfaceCastProbe.CheckInterfaceCasts), InterfaceCastProbe.CheckInterfaceCasts);
        Check(nameof(EntryArrayProbe.UpdateEntryArray), EntryArrayProbe.UpdateEntryArray);
        Check(nameof(EntryArrayProbe.ClearInteriorFields), EntryArrayProbe.ClearInteriorFields);
        Check(nameof(MutableReceiverProbe.CheckMutableReceiver), MutableReceiverProbe.CheckMutableReceiver);
        Check(nameof(MutableReceiverProbe.CheckExceptionalMutation), MutableReceiverProbe.CheckExceptionalMutation);
        Check(nameof(EntryArrayProbe.CheckEntryArrayFailure), EntryArrayProbe.CheckEntryArrayFailure);
        Check(nameof(BucketProbe.ReadManagedBuckets), BucketProbe.ReadManagedBuckets);
        Check(nameof(BucketProbe.CheckBucketFailure), BucketProbe.CheckBucketFailure);
        MethodInfo exportedBucket = Method(nameof(BucketProbe.RejectExportedBucket));
        Reject(exportedBucket, [0, Row(exportedBucket), -1]);

        // A call using the read-only receiver ABI still diagnoses a write
        // through this at the callee's stfld (decision 0049).
        MethodInfo assign = typeof(Rejected).GetMethod("Assign", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Reject(Method(nameof(Kernels.RejectReceiverCall)), [0, Row(assign), 0]);

        void Check(string name, Func<int, int> expectedResult)
        {
            MethodInfo run = Method(name);
            byte[] entries = Manifest(run);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length < 1) throw new InvalidOperationException($"Value construction {name} returned {length} at [{string.Join(", ", location)}].");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (int input in new[] { int.MinValue, -5, 0, 5, int.MaxValue })
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "CompileBundle", fixture = "value construction " + name,
                    inputs = new[] { Encode(bundle), Encode(entries) },
                    generated = new { capacity, bundle = true, arguments = new[] { input }, hex },
                    expected = expectedResult(input)
                });
        }

        void Reject(MethodBase method, int[] diagnostic)
        {
            byte[] entries = Manifest(method);
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{method.Name} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "value construction " + method.Name,
                inputs = new[] { Encode(bundle), Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic },
                expected = status
            });
        }
    }

    private static MethodInfo Method(string name) =>
        typeof(Kernels).GetMethod(name) ?? typeof(BucketProbe).GetMethod(name) ??
        typeof(EntryArrayProbe).GetMethod(name) ?? typeof(ClosedInterfaceProbe).GetMethod(name) ??
        typeof(ClosedClassProbe).GetMethod(name) ?? typeof(MutableReceiverProbe).GetMethod(name) ??
        typeof(InterfaceCastProbe).GetMethod(name)!;
    private static int Row(MethodBase method) => method.MetadataToken & 0xffffff;

    private static byte[] Manifest(MethodBase method)
    {
        byte[] entries = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(entries, Row(method));
        entries[4] = 3;
        "run"u8.CopyTo(entries.AsSpan(8));
        return entries;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
