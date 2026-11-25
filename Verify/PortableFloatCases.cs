#nullable enable

using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using BootstrapLibrary;
using PortableReals;

internal static class PortableFloatCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Signals).Assembly.Location);
        byte[] dependency = File.ReadAllBytes(typeof(FloatDependency.Scale).Assembly.Location);
        var dll = EncodeBytes(image);
        var bundle = EncodeBytes(BundleCases.Pack(image, dependency));
        var output = new byte[capacity];
        var compiled = new Dictionary<string, (object[] Inputs, string Hex)>();

        double[] values = [0.0, -0.0, double.Epsilon, -double.Epsilon, 0.1, -1.25, 1.5, 2147483648.0,
            -2147483649.0, 9223372036854775808.0, 18446744073709551616.0,
            double.MaxValue, double.NegativeInfinity, double.PositiveInfinity, double.NaN];

        foreach (double value in values)
        {
            Check(nameof(Signals.Negate), [value], () => Signals.Negate(value));
            Check(nameof(Signals.NegateSingle), [(float)value], () => Signals.NegateSingle((float)value));
            Check(nameof(Signals.Truncate), [value], () => Signals.Truncate(value));
            Check(nameof(Signals.TruncateUnsigned), [(float)value], () => unchecked((int)Signals.TruncateUnsigned((float)value)));
            Check(nameof(Signals.TruncateWide), [value], () => Signals.TruncateWide(value));
            Check(nameof(Signals.TruncateUnsignedWide), [value], () => Signals.TruncateUnsignedWide(value));
            Check(nameof(Signals.Demote), [value], () => Signals.Demote(value));
            Check(nameof(Signals.Filter), [value, 3], () => Signals.Filter(value, 3));
            Check(nameof(Signals.Linked), [(float)value, 0.7], () => Signals.Linked((float)value, 0.7), linked: true);
            foreach (double other in new[] { -0.0, 0.0, 1.0, double.NaN })
            {
                Check(nameof(Signals.Choose), [value, other], () => Signals.Choose(value, other));
                Check(nameof(Signals.UnorderedOrLess), [value, other], () => Signals.UnorderedOrLess(value, other));
                Check(nameof(Signals.Divide), [value, other], () => Signals.Divide(value, other));
                Check(nameof(Signals.Blend), [(float)value, other], () => Signals.Blend((float)value, other));
            }
        }

        foreach (float value in new[] { -0f, float.Epsilon, 16777216f, float.PositiveInfinity, float.NaN })
            Check(nameof(Signals.AddSingle), [value, 1f], () => Signals.AddSingle(value, 1f));

        double[] narrowValues = [0.0, -0.0, double.Epsilon, float.Epsilon, -1.9, -256.5, -65536.5,
            254.9, 255.5, 256.0, 300.7, 65534.9, 65535.5, 65536.0, 65537.9,
            int.MaxValue, 2147483648.0, int.MinValue, -2147483649.0, double.MaxValue,
            double.NegativeInfinity, double.PositiveInfinity, double.NaN];
        foreach (double value in narrowValues)
        {
            float single = (float)value;
            Check(nameof(Signals.ToByteDouble), [value], () => Signals.ToByteDouble(value));
            Check(nameof(Signals.ToByteSingle), [single], () => Signals.ToByteSingle(single));
            Check(nameof(Signals.ToUInt16Double), [value], () => Signals.ToUInt16Double(value));
            Check(nameof(Signals.ToUInt16Single), [single], () => Signals.ToUInt16Single(single));
        }

        foreach (ulong value in new[] { 0UL, 1UL, 0x8000008000000001UL, 0xfffffffffffff801UL, ulong.MaxValue })
        {
            Check(nameof(Signals.FloatFromUnsigned), [value], () => Signals.FloatFromUnsigned(value));
            Check(nameof(Signals.FloatThroughDouble), [value], () => Signals.FloatThroughDouble(value));
            Check(nameof(Signals.DoubleFromSigned), [unchecked((long)value)], () => Signals.DoubleFromSigned(unchecked((long)value)));
        }
        Check(nameof(Signals.TinyConstant), [], () => Signals.TinyConstant());
        Check(nameof(Signals.NegativeZero), [], () => Signals.NegativeZero());
        double[] remainderValues = [0.0, -0.0, -6.0, 3.0, double.Epsilon, float.Epsilon,
            double.MaxValue, 1.0e-300, double.PositiveInfinity, double.NegativeInfinity, double.NaN];
        foreach (double left in remainderValues)
        foreach (double right in remainderValues)
        {
            Check(nameof(Signals.Remainder), [left, right], () => Signals.Remainder(left, right));
            Check(nameof(Signals.RemainderSingle), [(float)left, (float)right],
                () => Signals.RemainderSingle((float)left, (float)right));
        }
        Check(nameof(Signals.RemainderWithCallback), [19.5, 4.0], () => Signals.RemainderWithCallback(19.5, 4.0));
        Reject(nameof(Signals.CheckedTruncate), OpCodes.Conv_Ovf_I4);
        Reject(nameof(Signals.CheckedToByte), OpCodes.Conv_Ovf_U1);

        void Check(string method, object[] arguments, Func<object> evaluate, bool linked = false)
        {
            if (!compiled.TryGetValue(method, out var fixture))
            {
                int row = Method(method).MetadataToken & 0xffffff;
                byte[] entries = new byte[11];
                BinaryPrimitives.WriteInt32LittleEndian(entries, row);
                entries[4] = 3;
                "run"u8.CopyTo(entries.AsSpan(8));
                int size = linked ? CompilerExports.CompileBundle(BundleCases.Pack(image, dependency), entries, output)
                    : CompilerExports.Compile(image, row, output);
                if (size <= 0) throw new InvalidOperationException($"{method}: compiler returned {size}");
                fixture = (linked ? [bundle, EncodeBytes(entries)] : [dll, row],
                    Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant());
                compiled.Add(method, fixture);
            }
            cases.Add(new
            {
                module = "BootstrapLibrary", method = linked ? "CompileBundle" : "Compile", fixture = method,
                inputs = fixture.Inputs,
                generated = new { capacity, arguments = Array.ConvertAll(arguments, Encode), hex = fixture.Hex, bundle = linked },
                expected = Encode(evaluate())
            });
        }

        void Reject(string method, OpCode opcode)
        {
            var fixture = Method(method);
            int row = fixture.MetadataToken & 0xffffff;
            int offset = IlOffsets.Find(fixture.GetMethodBody()!.GetILAsByteArray()!, opcode);
            int[] location = CompilerExports.CreateDiagnostic();
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileDetailed(image, row, output, location);
            if (offset < 0 || status != -2 || !location.SequenceEqual(new[] { 0, row, offset }) || output.Any(x => x != 0xa5))
                throw new InvalidOperationException($"{method}: expected an unsupported diagnostic at IL_{offset:x4}, got {status}");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = method, inputs = new[] { dll, (object)row },
                generated = new { capacity, arguments = Array.Empty<int>(), diagnostic = location, reject = true },
                expected = -2
            });
        }
    }

    private static MethodInfo Method(string name) => typeof(Signals).GetMethod(name)!;

    private static object EncodeBytes(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };

    private static object Encode(object value) => value switch
    {
        double real => new { f64 = real.ToString("G17", CultureInfo.InvariantCulture) },
        float real => new { f64 = ((double)real).ToString("G17", CultureInfo.InvariantCulture) },
        long bits => new { i64 = bits.ToString(CultureInfo.InvariantCulture) },
        ulong bits => new { i64 = unchecked((long)bits).ToString(CultureInfo.InvariantCulture) },
        bool flag => flag ? 1 : 0,
        _ => value
    };
}
