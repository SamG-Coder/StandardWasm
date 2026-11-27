using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Expected the x86 CLR.");
        string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var digits = typeof(int).Assembly.GetType("System.Number", throwOnError: true)!
            .GetMethod("TryNegativeInt32ToDecStr", BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(char));
        if (!digits.GetMethodBody()!.LocalVariables.Any(local =>
            local.IsPinned && local.LocalType == typeof(char).MakeByRefType()))
            throw new InvalidOperationException("The CoreLib formatting baseline changed.");
        int[] intDiagnostic = [2, digits.MetadataToken & 0xffffff, -1];

        var cases = new Func<int>[]
        {
            BoxedIntMatch, BoxedDoubleMatch, BoxedBooleanMiss, StringMiss, NullMiss, ReferenceMatch, BoxIdentity,
            OriginalIntWrites, OriginalIntTooShort, OriginalCharWrites, OriginalCharTooShort, ReferenceWrites,
            FormatProviderDescriptor, VirtualDescriptorArguments,
            ImplicitByteView, ConstructedByteView, ReturnedByteView, NullByteView, EmptyByteView, ByteViewBounds,
            WritableSpanReference, ClosedGenericSpanReference, DefaultSpanReference,
            StringObjectSlot, StringOwnSlot, NullStringObjectSlot,
            OriginalExceptionGetType, OriginalDerivedExceptionGetType, NullExceptionGetType
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

    public static int OriginalExceptionGetType() =>
        CheckExceptionType(new Exception("a"), typeof(Exception));

    public static int OriginalDerivedExceptionGetType() =>
        CheckExceptionType(new InvalidOperationException("b"), typeof(InvalidOperationException));

    public static int NullExceptionGetType()
    {
        try { return CheckExceptionType(null!, typeof(Exception)); }
        catch (NullReferenceException) { return 7; }
    }

    private static int CheckExceptionType(Exception value, Type type) =>
        value.GetType() == type ? 5 : 0;

    public static int StringObjectSlot() => CheckObjectSlot("abc");
    public static int StringOwnSlot() => CheckStringSlot("def");

    private static int CheckObjectSlot(string value) => ReferenceEquals(((object)value).ToString(), value) ? 1 : 0;
    private static int CheckStringSlot(string value) => ReferenceEquals(value.ToString(null), value) ? 2 : 0;

    public static int NullStringObjectSlot()
    {
        try { return CheckObjectSlot(null!); }
        catch (NullReferenceException) { return 3; }
    }

    public static int WritableSpanReference()
    {
        char[] data = ['a', 'b', 'c'];
        Span<char> view = new Span<char>(data).Slice(1);
        ref char start = ref MemoryMarshal.GetReference(view);
        start = 'x';
        return data[0] * 10000 + data[1] * 100 + view[1];
    }

    public static int ClosedGenericSpanReference()
    {
        char[] data = ['a', 'b', 'c'];
        Span<char> view = new Span<char>(data).Slice(2);
        WriteSpanStart(view, 'z');
        return data[1] * 100 + data[2];
    }

    public static int DefaultSpanReference()
    {
        try
        {
            ref char start = ref MemoryMarshal.GetReference(default(Span<char>));
            return start;
        }
        catch (NullReferenceException)
        {
            return -1;
        }
    }

    private static void WriteSpanStart<T>(Span<T> view, T value) =>
        MemoryMarshal.GetReference(view) = value;

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

    public static int ImplicitByteView()
    {
        var array = new byte[] { 11, 96, 202 };
        ReadOnlySpan<byte> view = array;
        ReadOnlySpan<byte> tail = view.Slice(1);
        array[1] = 241;
        return view[0] + tail[0] * 10 + tail.Length * 1000;
    }

    public static int ConstructedByteView()
    {
        var array = new byte[] { 4, 193 };
        ReadOnlySpan<byte> view = new ReadOnlySpan<byte>(array);
        array[0] = 212;
        return view[0] + view[1];
    }

    public static int NullByteView() => ByteViewLength(null);
    public static int EmptyByteView() => ByteViewLength(new byte[0]);

    public static int ReturnedByteView()
    {
        ReadOnlySpan<byte> view = CreateByteView();
        return view[1] + view.Length;
    }

    private static ReadOnlySpan<byte> CreateByteView() => new ReadOnlySpan<byte>(new byte[] { 7, 201 });

    private static int ByteViewLength(byte[]? array)
    {
        ReadOnlySpan<byte> view = array;
        return view.IsEmpty ? view.Length : -1;
    }

    public static int ByteViewBounds()
    {
        ReadOnlySpan<byte> view = new byte[] { 8 };
        try { return view[1]; }
        catch (IndexOutOfRangeException) { return 7; }
    }

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
