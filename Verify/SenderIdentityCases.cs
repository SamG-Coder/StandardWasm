#nullable enable

using BootstrapLibrary;
using EventLibrary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static class SenderIdentityCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(SenderCases).Assembly.Location);
        var input = new { bytes = Array.ConvertAll(image, value => (int)value) };

        Check(nameof(SenderCases.SenderIdentity), [0, 1, 2], SenderCases.SenderIdentity);
        Check(nameof(SenderCases.SenderLocal), [-1, 0, 1, 3], SenderCases.SenderLocal);
        Check(nameof(SenderCases.ArgumentIdentity), [0, 1, 2, 3], SenderCases.ArgumentIdentity);
        Check(nameof(EventCases.InvokeBoundHandler), [0, 1], EventCases.InvokeBoundHandler, typeof(EventCases));
        Check(nameof(EventCases.InvokeEmptyHandler), [0], EventCases.InvokeEmptyHandler, typeof(EventCases));
        Check(nameof(EventCases.CombineCallbacks), [0, 1, 2], EventCases.CombineCallbacks, typeof(EventCases));
        Check(nameof(EventCases.RemoveCallbacks), [0, 1, 2, 3], EventCases.RemoveCallbacks, typeof(EventCases));
        Check(nameof(EventCases.CompareHandlerField), null, _ => EventCases.CompareHandlerField(), typeof(EventCases));
        Check(nameof(EventCases.SubscriptionOrder), null, _ => EventCases.SubscriptionOrder(), typeof(EventCases));
        Check(nameof(EventCases.RemoveDuringRaise), null, _ => EventCases.RemoveDuringRaise(), typeof(EventCases));
        Check(nameof(EventCases.EmptyAndUnmatched), null, _ => EventCases.EmptyAndUnmatched(), typeof(EventCases));
        Check(nameof(EventCases.DistinctTargets), null, _ => EventCases.DistinctTargets(), typeof(EventCases));
        RejectWrongAssembly();

        void RejectWrongAssembly()
        {
            using var pe = new PEReader(new MemoryStream(image));
            var metadata = pe.GetMetadataReader();
            var facade = metadata.AssemblyReferences.Select(metadata.GetAssemblyReference)
                .Single(reference => metadata.GetString(reference.Name) == "netstandard");
            int token = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
                MetadataTokens.GetHeapOffset(facade.PublicKeyOrToken);
            byte[] forged = (byte[])image.Clone();
            if (forged[token] != 8)
                throw new InvalidOperationException("Expected an eight-byte netstandard public key token");
            forged[token + 1] ^= 1;

            int row = Row(typeof(SenderCases), nameof(SenderCases.EmptyArgument));
            var output = Enumerable.Repeat((byte)0xa5, capacity).ToArray();
            var location = new int[3];
            int result = CompilerExports.CompileDetailed(forged, row, output, location);
            if (result != -2 || !location.SequenceEqual(new[] { 0, row, 0 }) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"Wrong EventArgs assembly returned {result}, location {string.Join(",", location)}");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = "EventArgs wrong AssemblyRef token",
                inputs = new object[] { new { bytes = Array.ConvertAll(forged, value => (int)value) }, row },
                generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic = new[] { 0, row, 0 } },
                expected = result
            });
        }

        void Check(string name, int[]? values, Func<int, int> oracle, Type? declaringType = null)
        {
            int row = Row(declaringType ?? typeof(SenderCases), name);
            var output = new byte[capacity];
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{name} returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();

            foreach (int value in values ?? [0])
            {
                int? expected;
                string? exception = null;
                try { expected = oracle(value); }
                catch (NullReferenceException error)
                {
                    expected = null;
                    exception = error.GetType().FullName;
                }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = name,
                    inputs = new object[] { input, row },
                    generated = new { capacity, arguments = values == null ? Array.Empty<int>() : new[] { value }, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }

        static int Row(Type type, string name) => type.GetMethod(name)!.MetadataToken & 0xffffff;
    }
}
