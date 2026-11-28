#nullable enable

using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BootstrapLibrary;
using StringVectors;

// String vectors (newarr String, ldelem.ref, stelem.ref, ldlen) compared with
// the CLR, and Console members outside the host contract rejected with their
// MethodDef row and IL offset.
internal static class StringVectorCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        byte[] image = File.ReadAllBytes(typeof(Kernels).Assembly.Location);
        object encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        var output = new byte[capacity];
        int[] diagnostic = CompilerExports.CreateDiagnostic();

        (int, int)[] positions = [(1, 0), (3, 2), (3, 0), (0, 0), (2, -1), (2, 2), (-1, 0), (2, int.MaxValue)];
        Check(nameof(Kernels.Store), positions.Select(Pair(Kernels.Store)));
        Check(nameof(Kernels.ReadDefault), positions.Select(Pair(Kernels.ReadDefault)));
        Check(nameof(Kernels.Clear), new[] { 0, 1, 2, -1 }.Select(Single(Kernels.Clear)));
        Check(nameof(Kernels.NullVector), new[] { 0, 3, -1 }.Select(Single(Kernels.NullVector)));
        Check(nameof(Kernels.Reverse), new[] { 0, 1, 2, 5, -3 }.Select(Single(Kernels.Reverse)));

        Type unsupported = typeof(Unsupported);
        Reject(nameof(Unsupported.StaticWriteLine), At(unsupported, nameof(Unsupported.StaticWriteLine), OpCodes.Call));
        Reject(nameof(Unsupported.Read), At(unsupported, nameof(Unsupported.Read), OpCodes.Call));
        Reject(nameof(Unsupported.WriteCharacter), At(unsupported, nameof(Unsupported.WriteCharacter), OpCodes.Callvirt));
        // A TextWriter local reaches the ordinary class linker, which has no
        // TextWriter layout. The failure belongs to the local signature.
        MethodInfo local = unsupported.GetMethod(nameof(Unsupported.WriterLocal))!;
        Reject(nameof(Unsupported.WriterLocal), [0, Row(local), -1]);

        // A supported write is the only way a module gains imports. JavaScript
        // tests cannot instantiate it without a host, so inspect it here.
        int supported = CompilerExports.Compile(image, Row(unsupported.GetMethod(nameof(Unsupported.Supported))!), output);
        string emitted = Encoding.Latin1.GetString(output, 0, Math.Max(supported, 0));
        if (supported <= 0 || !emitted.Contains("\u0014standardwasm:console\u0005write") || !emitted.Contains("\u0007newLine"))
            throw new InvalidOperationException($"Console write did not import the host contract: {supported}");
        if (emitted.Contains("\u0008readLine"))
            throw new InvalidOperationException("A module that only writes imported Console.ReadLine.");
        // #326 (decision 0104): Console.Out.Flush() compiles to no code and imports the write contract.
        int flushed = CompilerExports.Compile(image, Row(unsupported.GetMethod(nameof(Unsupported.Flush))!), output);
        if (flushed <= 0 || !Encoding.Latin1.GetString(output, 0, flushed).Contains("standardwasm:consolewrite"))
            throw new InvalidOperationException($"Console.Out.Flush did not compile against the host contract: {flushed}");
        // Stage 151 (decision 0079): Console.ReadLine imports the separate input pair.
        int reads = CompilerExports.Compile(image, Row(unsupported.GetMethod(nameof(Unsupported.ReadLine))!), output);
        string input = Encoding.Latin1.GetString(output, 0, Math.Max(reads, 0));
        if (reads <= 0 || !input.Contains("\u0014standardwasm:console\u0008readLine") || !input.Contains("\u0008readUnit"))
            throw new InvalidOperationException($"Console.ReadLine did not import the input contract: {reads}");
        int storeLength = CompilerExports.Compile(image, Row(typeof(Kernels).GetMethod(nameof(Kernels.Store))!), output);
        if (Encoding.Latin1.GetString(output, 0, storeLength).Contains("standardwasm:console"))
            throw new InvalidOperationException("A module without Console calls imported the host contract.");

        Func<(int A, int B), (object?[], Func<int>)> Pair(Func<int, int, int> oracle) =>
            pair => (new object?[] { pair.A, pair.B }, () => oracle(pair.A, pair.B));

        Func<int, (object?[], Func<int>)> Single(Func<int, int> oracle) =>
            value => (new object?[] { value }, () => oracle(value));

        void Check(string method, IEnumerable<(object?[] Arguments, Func<int> Oracle)> fixtures)
        {
            int row = Row(typeof(Kernels).GetMethod(method)!);
            int length = CompilerExports.Compile(image, row, output);
            if (length <= 0) throw new InvalidOperationException($"{method} compiler returned {length}");
            string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
            foreach (var (arguments, oracle) in fixtures)
            {
                int? expected = null;
                string? exception = null;
                try { expected = oracle(); }
                catch (IndexOutOfRangeException error) { exception = error.GetType().FullName; }
                catch (NullReferenceException error) { exception = error.GetType().FullName; }
                catch (OverflowException error) { exception = error.GetType().FullName; }
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = method,
                    inputs = new[] { encoded, row }, generated = new { capacity, arguments, hex, exceptionType = exception },
                    expected, exception
                });
            }
        }

        void Reject(string method, int[] location)
        {
            int row = location[1];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileDetailed(image, row, output, diagnostic);
            if (status != -2 || !diagnostic.SequenceEqual(location) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{method}: status {status}, diagnostic [{string.Join(", ", diagnostic)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = method,
                inputs = new[] { encoded, row },
                generated = new { capacity, arguments = Array.Empty<int>(), diagnostic = location, reject = true },
                expected = status
            });
        }
    }

    private static int Row(MethodInfo method) => method.MetadataToken & 0xffffff;

    private static int[] At(Type type, string name, OpCode opcode)
    {
        MethodInfo method = type.GetMethod(name)!;
        int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, opcode);
        if (offset < 0) throw new InvalidOperationException($"{name} did not emit {opcode}");
        return [0, Row(method), offset];
    }
}
