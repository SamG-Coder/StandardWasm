#nullable enable

using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using BootstrapLibrary;
using PortableMath;

// Stage 40: platform System.Math intrinsics in the self-hosted compiler. Each fixture method is compiled by the
// portable compiler running on the CLR. npm test replays those bytes, and the self-recompiled compiler's output,
// against the CLR results. Math.Sign and a third-party System.Math (extern alias) must stay diagnostics.
internal static class PortableMathCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Measures).Assembly.Location);
        var dll = EncodeBytes(image);
        var output = new byte[capacity];
        var compiled = new Dictionary<string, string>();

        double[] reals =
        [
            0.0, -0.0, 0.5, -0.5, 1.5, 2.5, -2.5, 0.49999999999999994, 2.675, 3.0, 4.0, 1e-310, 4503599627370495.5,
            1e300, double.MaxValue, double.Epsilon, double.PositiveInfinity, double.NegativeInfinity, double.NaN
        ];
        foreach (double a in reals)
        {
            Check(nameof(Measures.Abs), [a], () => Measures.Abs(a));
            Check(nameof(Measures.AbsSingle), [(float)a], () => Measures.AbsSingle((float)a));
            Check(nameof(Measures.Floor), [a], () => Measures.Floor(a));
            Check(nameof(Measures.Ceiling), [a], () => Measures.Ceiling(a));
            Check(nameof(Measures.Truncate), [a], () => Measures.Truncate(a));
            Check(nameof(Measures.Round), [a], () => Measures.Round(a));
            Check(nameof(Measures.RoundCents), [a], () => Measures.RoundCents(a));
            Check(nameof(Measures.Cell), [a, 0.75], () => Measures.Cell(a, 0.75));
            foreach (double b in new[] { -0.0, 0.0, 1.0, 3.0, double.NaN, double.NegativeInfinity })
            {
                Check(nameof(Measures.Hypotenuse), [a, b], () => Measures.Hypotenuse(a, b));
                Check(nameof(Measures.Clamp), [a, -1.0, b], () => Measures.Clamp(a, -1.0, b));
                Check(nameof(Measures.ClampSingle), [(float)a, -1f, (float)b], () => Measures.ClampSingle((float)a, -1f, (float)b));
            }
        }

        int[] ints = [int.MinValue, int.MinValue + 1, -2, -1, 0, 1, 2, int.MaxValue];
        foreach (int a in ints)
        {
            Check(nameof(Measures.AbsInt), [a], () => Measures.AbsInt(a));
            Check(nameof(Measures.AbsLong), [(long)a * 4294967296L], () => Measures.AbsLong((long)a * 4294967296L));
            foreach (int b in ints)
            {
                Check(nameof(Measures.MaxInt), [a, b], () => Measures.MaxInt(a, b));
                Check(nameof(Measures.MinInt), [a, b], () => Measures.MinInt(a, b));
                Check(nameof(Measures.MaxUnsigned), [a, b], () => Measures.MaxUnsigned(a, b));
                Check(nameof(Measures.MinUnsigned), [a, b], () => Measures.MinUnsigned(a, b));
                Check(nameof(Measures.AbsoluteDifference), [a, b], () => Measures.AbsoluteDifference(a, b));
            }
        }
        long[] longs = [long.MinValue, long.MinValue + 1, -(1L << 32), -1, 0, 1, 1L << 32, long.MaxValue];
        foreach (long a in longs)
        {
            Check(nameof(Measures.AbsLong), [a], () => Measures.AbsLong(a));
            foreach (long b in longs)
            {
                Check(nameof(Measures.MaxLong), [a, b], () => Measures.MaxLong(a, b));
                Check(nameof(Measures.MinLong), [a, b], () => Measures.MinLong(a, b));
                Check(nameof(Measures.MaxUnsignedLong), [a, b], () => Measures.MaxUnsignedLong(a, b));
                Check(nameof(Measures.MinUnsignedLong), [a, b], () => Measures.MinUnsignedLong(a, b));
            }
        }

        Reject(nameof(Measures.Sign), image, typeof(Measures).GetMethod(nameof(Measures.Sign))!);
        // The probe DLL is built by Verify.csproj without referencing it, so its third-party System.Math stays
        // invisible to this project.
        // npm test runs the oracle from the repository root.
        string probePath = "demos/stage-40-portable-math/identity/MathIdentityProbe/bin/Release/netstandard2.0/MathIdentityProbe.dll";
        var probe = Assembly.LoadFile(Path.GetFullPath(probePath));
        Reject("third-party System.Math.Sqrt", File.ReadAllBytes(probe.Location),
            probe.GetType("MathIdentityProbe.Probe")!.GetMethod("Compute")!);

        void Check(string method, object[] arguments, Func<object> evaluate)
        {
            if (!compiled.TryGetValue(method, out var hex))
            {
                int row = typeof(Measures).GetMethod(method)!.MetadataToken & 0xffffff;
                int size = CompilerExports.Compile(image, row, output);
                if (size <= 0) throw new InvalidOperationException($"{method}: compiler returned {size}");
                hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
                compiled.Add(method, hex);
            }
            object? expected;
            try { expected = Encode(evaluate()); }
            catch (OverflowException) { expected = null; } // Math.Abs(MinValue): Wasm must trap
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = "Math." + method,
                inputs = new object[] { dll, typeof(Measures).GetMethod(method)!.MetadataToken & 0xffffff },
                generated = new { capacity, arguments = Array.ConvertAll(arguments, Encode), hex },
                expected
            });
        }

        void Reject(string fixture, byte[] source, MethodInfo method)
        {
            int row = method.MetadataToken & 0xffffff;
            int offset = IlOffsets.Find(method.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Call);
            int[] location = CompilerExports.CreateDiagnostic();
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileDetailed(source, row, output, location);
            if (offset < 0 || status != -2 || !location.SequenceEqual(new[] { 0, row, offset }) || output.Any(x => x != 0xa5))
                throw new InvalidOperationException($"{fixture}: expected an unsupported diagnostic at IL_{offset:x4}, got {status} [{string.Join(", ", location)}]");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = "Math." + fixture,
                inputs = new[] { EncodeBytes(source), (object)row },
                generated = new { capacity, arguments = Array.Empty<int>(), diagnostic = location, reject = true },
                expected = -2
            });
        }
    }

    private static object EncodeBytes(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };

    private static object Encode(object value) => value switch
    {
        double real => new { f64 = real.ToString("G17", CultureInfo.InvariantCulture) },
        float real => new { f64 = ((double)real).ToString("G17", CultureInfo.InvariantCulture) },
        long bits => new { i64 = bits.ToString(CultureInfo.InvariantCulture) },
        _ => value
    };
}
