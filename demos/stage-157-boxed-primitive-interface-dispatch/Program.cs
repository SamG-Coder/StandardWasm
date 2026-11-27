using System.Text.Json;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var intFormatter = typeof(int).GetMethod(nameof(int.TryFormat),
            [typeof(Span<char>), typeof(int).MakeByRefType(), typeof(ReadOnlySpan<char>), typeof(IFormatProvider)])!;
        const int numberCallOffset = 8;
        byte[] formatterIl = intFormatter.GetMethodBody()!.GetILAsByteArray()!;
        if (formatterIl[numberCallOffset] != 0x28 ||
            intFormatter.Module.ResolveMethod(BitConverter.ToInt32(formatterIl, numberCallOffset + 1))?.Name != "TryFormatInt32")
            throw new InvalidOperationException("The CoreLib formatting baseline changed.");
        int[] intDiagnostic = [2, intFormatter.MetadataToken & 0xffffff, numberCallOffset];

        var cases = new Func<int>[]
        {
            BoxedIntMatch, BoxedDoubleMatch, BoxedBooleanMiss, StringMiss, NullMiss, ReferenceMatch, BoxIdentity,
            OriginalIntWrites, OriginalIntTooShort, OriginalCharWrites, OriginalCharTooShort, ReferenceWrites,
            FormatProviderDescriptor, VirtualDescriptorArguments
        };
        var results = cases.Select(run => new {
            name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run(),
            acceptedDiagnostic = run.Method.Name.StartsWith("OriginalInt", StringComparison.Ordinal) ? intDiagnostic : null
        });
        File.WriteAllText(args[0], JsonSerializer.Serialize(new {
            runtime = Environment.Version.ToString(), files = new[] {
                typeof(Program).Assembly.Location, Path.Combine(runtime, "System.Runtime.dll"),
                typeof(object).Assembly.Location,
                Path.Combine(runtime, "System.Memory.dll")
            }, cases = results
        }));
    }

    private static bool Matches(object value) => value is ISpanFormattable;

    public static int BoxedIntMatch() => Matches(42) ? 1 : 0;
    public static int BoxedDoubleMatch() => Matches(4.2) ? 1 : 0;
    public static int BoxedBooleanMiss() => Matches(true) ? 1 : 0;
    public static int BoxIdentity()
    {
        object value = 42;
        return ReferenceEquals(value, value as ISpanFormattable) ? 1 : 0;
    }

    public static int StringMiss() => Matches("42") ? 1 : 0;
    public static int NullMiss() => Matches(null!) ? 1 : 0;
    public static int ReferenceMatch() => Matches(new Formatter()) ? 1 : 0;
    public static int OriginalIntWrites() => Format(42, 2);
    public static int OriginalIntTooShort() => Format(42, 1);
    public static int OriginalCharWrites() => Format('Q', 1);
    public static int OriginalCharTooShort() => Format('Q', 0);
    public static int ReferenceWrites() => Format(new Formatter(), 2);

    public static int FormatProviderDescriptor() => ReadFormat(new Provider());

    private static int ReadFormat(IFormatProvider provider) =>
        ReferenceEquals(provider.GetFormat(typeof(Program)), "provider") ? 17 : -1;

    public static int VirtualDescriptorArguments()
    {
        DescriptorReader first = new FirstReader();
        DescriptorReader second = new SecondReader();
        return CompareDescriptors(first, typeof(string)) + CompareDescriptors(second, typeof(string)) +
            CompareDescriptors(second, null);
    }

    private static int CompareDescriptors(DescriptorReader reader, Type? second) =>
        reader.Compare(typeof(Program), second);

    private static int Format(object value, int capacity)
    {
        var buffer = new[] { 'a', '_', '_', 'z' };
        if (!(value is ISpanFormattable format)) return -1;
        bool ok = format.TryFormat(buffer.AsSpan().Slice(1, capacity), out int written, default, null);
        return (ok ? 1_000_000 : 0) + 10_000 * written +
            buffer[0] + 3 * buffer[1] + 7 * buffer[2] + 11 * buffer[3];
    }

    private sealed class Formatter : ISpanFormattable
    {
        public string ToString(string? format, IFormatProvider? provider) => "XY";

        public bool TryFormat(Span<char> destination, out int written, ReadOnlySpan<char> format,
            IFormatProvider? provider)
        {
            written = 0;
            if (destination.Length < 2) return false;
            destination[0] = 'X';
            destination[1] = 'Y';
            written = 2;
            return true;
        }
    }

    private sealed class Provider : IFormatProvider
    {
        public object? GetFormat(Type? formatType) => formatType == typeof(Program) ? "provider" : null;
    }

    private abstract class DescriptorReader
    {
        public abstract int Compare(Type first, Type? second);
    }

    private sealed class FirstReader : DescriptorReader
    {
        public override int Compare(Type first, Type? second) =>
            first == typeof(Program) && second == typeof(string) ? 23 : -1;
    }

    private sealed class SecondReader : DescriptorReader
    {
        public override int Compare(Type first, Type? second) =>
            first != typeof(Program) ? -1 : second == null ? 5 : second == typeof(string) ? 31 : -1;
    }
}
