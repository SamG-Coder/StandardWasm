#nullable enable

using System.Globalization;
using System.Reflection.Emit;
using BootstrapLibrary;
using WideStorageLibrary;

internal static class WideStorageCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        byte[] image = File.ReadAllBytes(typeof(Ledger).Assembly.Location);
        var encoded = new { bytes = Array.ConvertAll(image, value => (int)value) };
        var output = new byte[capacity];
        var methods = new Dictionary<string, (int Row, string Hex)>();

        foreach (long value in new[] { long.MinValue, -1L, 0L, 1L, 9007199254740993L, long.MaxValue })
        {
            foreach ((int index, int size) in new[] { (0, 1), (2, 3), (-1, 2), (2, 2), (0, 0), (0, -1) })
            {
                Check(nameof(Ledger.Apply), [Wide(value), index, size], () => Wide(Ledger.Apply(value, index, size)));
                Check(nameof(Ledger.ApplyTag), [Wide(value), index, size],
                    () => Wide(unchecked((long)Ledger.ApplyTag(unchecked((ulong)value), index, size))));
            }
        }

        foreach (int size in new[] { -1, 0, 1, 1024 })
        {
            Check(nameof(Ledger.Length), [size], () => Ledger.Length(size));
            Check(nameof(Ledger.ReadDefault), [0, size], () => Wide(Ledger.ReadDefault(0, size)));
        }
        Check(nameof(Ledger.NullRead), [], () => Wide(Ledger.NullRead()));

        var unsupported = typeof(Ledger).GetMethod(nameof(Ledger.RefUpdate))!;
        int unsupportedRow = unsupported.MetadataToken & 0xffffff;
        int offset = IlOffsets.Find(unsupported.GetMethodBody()!.GetILAsByteArray()!, OpCodes.Ldelema);
        int[] location = CompilerExports.CreateDiagnostic();
        Array.Fill(output, (byte)0xa5);
        int status = CompilerExports.CompileDetailed(image, unsupportedRow, output, location);
        if (offset < 0 || status != -2 || !location.SequenceEqual(new[] { 0, unsupportedRow, offset }) ||
            output.Any(value => value != 0xa5))
            throw new InvalidOperationException("Expected ldelema to be rejected at its IL offset without writing output.");
        cases.Add(new
        {
            module = "BootstrapLibrary", method = "Compile", fixture = nameof(Ledger.RefUpdate),
            inputs = new object[] { encoded, unsupportedRow },
            generated = new { capacity, arguments = Array.Empty<int>(), reject = true, diagnostic = location },
            expected = -2
        });

        void Check(string method, object[] arguments, Func<object> oracle)
        {
            if (!methods.TryGetValue(method, out var compiled))
            {
                int row = typeof(Ledger).GetMethod(method)!.MetadataToken & 0xffffff;
                int size = CompilerExports.Compile(image, row, output);
                if (size <= 0) throw new InvalidOperationException($"{method}: portable compiler returned {size}");
                compiled = (row, Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant());
                methods.Add(method, compiled);
            }

            object? expected;
            string? exception = null;
            try { expected = oracle(); }
            catch (Exception error) when (error is IndexOutOfRangeException or OverflowException or NullReferenceException)
            {
                expected = null;
                exception = error.GetType().FullName;
            }

            cases.Add(new
            {
                module = "BootstrapLibrary", method = "Compile", fixture = method,
                inputs = new object[] { encoded, compiled.Row },
                generated = new { capacity, arguments, hex = compiled.Hex },
                expected, exception
            });
        }
    }

    private static object Wide(long value) => new { i64 = value.ToString(CultureInfo.InvariantCulture) };
}
