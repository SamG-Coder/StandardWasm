using BootstrapLibrary;
using FieldlessSequentialValue;

internal static class FieldlessValueCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 65536;
        byte[] bundle = BundleCases.Pack(File.ReadAllBytes(typeof(Kernels).Assembly.Location));
        object encodedBundle = Encode(bundle);
        var output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();

        Check(nameof(Kernels.EmptyLocal), Kernels.EmptyLocal);
        Check(nameof(Kernels.ContainingCopy), Kernels.ContainingCopy);
        Check(nameof(Kernels.GenericArgument), Kernels.GenericArgument);
        Check(nameof(Kernels.BothGenericArguments), Kernels.BothGenericArguments);
        Check(nameof(Kernels.StoreCallResult), () => Kernels.StoreCallResult(2), [2]);
        Check(nameof(Kernels.StoreNestedCallResult), () => Kernels.StoreNestedCallResult(-3), [-3]);
        Check(nameof(Kernels.WriteThroughBorrow), Kernels.WriteThroughBorrow);
        Reject(nameof(Kernels.RejectPacked), Kernels.RejectPacked);
        Reject(nameof(Kernels.RejectNonempty), Kernels.RejectNonempty);

        void Check(string method, Func<int> oracle, int[] arguments = null)
        {
            byte[] entries = BundleCases.Manifest(typeof(Kernels), method);
            int length = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (length <= 0)
                throw new InvalidOperationException($"{method} returned {length} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "fieldless sequential " + method,
                inputs = new[] { encodedBundle, Encode(entries) },
                generated = new
                {
                    capacity, bundle = true, arguments = arguments ?? Array.Empty<int>(),
                    hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant()
                },
                expected = oracle()
            });
        }

        void Reject(string method, Func<int> oracle)
        {
            if (oracle() == 0) throw new InvalidOperationException($"The CLR did not run {method}.");
            byte[] entries = BundleCases.Manifest(typeof(Kernels), method);
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(bundle, entries, output, location);
            if (status != -2 || location[0] != 0 || location[1] < 1 || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{method} returned {status} at [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "fieldless sequential " + method,
                inputs = new[] { encodedBundle, Encode(entries) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), reject = true, diagnostic = location.ToArray() },
                expected = status
            });
        }
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
