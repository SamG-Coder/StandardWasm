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
            OriginalExceptionGetType, OriginalDerivedExceptionGetType, NullExceptionGetType, OriginalObjectDisplayName,
            ClosedTypeDisplayName, OpenTypeDisplayName, ArrayTypeDisplayName, RepeatedTypeDisplayName, NamespacedTypeDisplayName, NullTypeDisplayName,
            ExactRootDispatch, DynamicRootDispatch, NullRootDispatch, PrimitiveVectorRootDispatch,
            TypeSimpleNames, NullTypeSimpleName, TypeNameAndDisplayName,
            WritableSpanString, ReadOnlySpanString, EmptySpanString, NativeSpanString,
            PrimitiveVectorTypeIdentity, PrimitiveVectorAliases, InitializedPrimitiveVectorIdentity,
            ClosedValueIsNotNull, ClosedValueIsNull, ClosedReferenceNullCheck
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

    public static int ClosedValueIsNotNull() => IsPresent(new NullProbeValue(7)) ? 81 : 0;
    public static int ClosedValueIsNull() => IsMissing(new NullProbeValue(0)) ? 0 : 83;
    public static int ClosedReferenceNullCheck() =>
        IsPresent("value") && IsMissing<string?>(null) ? 89 : 0;

    private static bool IsPresent<T>(T item) => (object?)item != null;
    private static bool IsMissing<T>(T item) => (object?)item == null;

    private readonly struct NullProbeValue
    {
        internal readonly int Number;
        internal NullProbeValue(int number) => Number = number;
    }

    public static int TypeSimpleNames()
    {
        int result = typeof(NominalNameProbe.Élément<int>).Name == "Élément`1" ? 1 : 0;
        if (typeof(NominalNameProbe.Élément<>).Name == "Élément`1") result += 2;
        if (typeof(NominalNameProbe[]).Name == "NominalNameProbe[]") result += 4;
        if (typeof(byte[]).Name == "Byte[]") result += 8;
        if (typeof(Exception).Name == "Exception") result += 16;
        return result;
    }

    public static int NullTypeSimpleName()
    {
        try { return ReadTypeSimpleName(false).Length; }
        catch (NullReferenceException) { return 71; }
    }

    private static string ReadTypeSimpleName(bool useType)
    {
        Type? descriptor = useType ? typeof(NominalNameProbe) : null;
        return descriptor!.Name;
    }

    public static int TypeNameAndDisplayName()
    {
        Type type = typeof(NominalNameProbe);
        return type.Name == "NominalNameProbe" && type.ToString() == "Program+NominalNameProbe" &&
            ReferenceEquals(type.Name, type.Name) ? 73 : 0;
    }

    public static int WritableSpanString()
    {
        char[] data = { '!', '\ud800', 'Ω', '?' };
        var view = new Span<char>(data).Slice(1, 2);
        string snapshot = view.ToString();
        view[0] = 'x';
        return snapshot.Length * 100 + (snapshot[0] == '\ud800' ? 1 : 0) +
            (snapshot[1] == 'Ω' ? 2 : 0) + (data[1] == 'x' ? 4 : 0);
    }

    public static int ReadOnlySpanString()
    {
        ReadOnlySpan<char> view = "prefix😀suffix".AsSpan().Slice(6, 2);
        return view.ToString() == "😀" ? 91 : 0;
    }

    public static int EmptySpanString()
    {
        Span<char> writable = default;
        ReadOnlySpan<char> readOnly = default;
        return ReferenceEquals(writable.ToString(), "") && ReferenceEquals(readOnly.ToString(), "") ? 93 : 0;
    }

    public static unsafe int NativeSpanString()
    {
        char* data = stackalloc char[3];
        data[0] = 'x';
        data[1] = 'Ω';
        data[2] = 'z';
        var view = new Span<char>(data, 3).Slice(1, 1);
        string snapshot = view.ToString();
        view[0] = 'y';
        return snapshot == "Ω" && data[1] == 'y' ? 97 : 0;
    }

    public static int ExactRootDispatch() => new object().ToString() == "System.Object" ? 67 : 0;

    public static int DynamicRootDispatch()
    {
        int result = DescribeRoot(new object()) == "System.Object" ? 1 : 0;
        if (DescribeRoot("test") == "test") result += 2;
        if (DescribeRoot(new NamedOverride()) == "override") result += 4;
        if (DescribeRoot(new NominalNameProbe()) == "Program+NominalNameProbe") result += 8;
        if (DescribeRoot(typeof(NamedOverride)) == "Program+NamedOverride") result += 16;
        if (DescribeRoot(true) == "True") result += 32;
        if (DescribeRoot(new NamedOverride[1]) == "Program+NamedOverride[]") result += 64;
        return result;
    }

    public static int NullRootDispatch()
    {
        try { return DescribeRoot(null!).Length; }
        catch (NullReferenceException) { return 53; }
    }

    public static int PrimitiveVectorRootDispatch()
    {
        int result = DescribeRoot(new byte[1]) == "System.Byte[]" ? 1 : 0;
        if (DescribeRoot(new bool[1]) == "System.Boolean[]") result += 2;
        if (DescribeRoot(new char[1]) == "System.Char[]") result += 4;
        if (DescribeRoot(new ushort[1]) == "System.UInt16[]") result += 8;
        return result;
    }

    public static int PrimitiveVectorTypeIdentity()
    {
        int result = IsExactType(new byte[1], typeof(byte[])) ? 1 : 0;
        if (IsExactType(new bool[1], typeof(bool[]))) result += 2;
        if (IsExactType(new char[1], typeof(char[]))) result += 4;
        if (IsExactType(new ushort[1], typeof(ushort[]))) result += 8;
        if (!IsExactType(new bool[1], typeof(byte[]))) result += 16;
        if (!IsExactType(new char[1], typeof(ushort[]))) result += 32;
        return result;
    }

    public static int PrimitiveVectorAliases()
    {
        var bytes = new byte[1];
        var chars = new char[1];
        object byteOwner = bytes;
        object charOwner = chars;
        bytes[0] = 251;
        chars[0] = 'Z';
        int result = ReferenceEquals(byteOwner, bytes) && IsExactType(byteOwner, typeof(byte[])) ? bytes[0] : 0;
        if (ReferenceEquals(charOwner, chars) && IsExactType(charOwner, typeof(char[]))) result += chars[0];
        return result;
    }

    public static int InitializedPrimitiveVectorIdentity()
    {
        int[] numbers = [17, 21, 55, 61, 71, 81, 91, 101];
        return numbers.GetType() == typeof(int[]) ? numbers[5] : -1;
    }

    private static bool IsExactType(object value, Type expected) => value.GetType() == expected;

    private static string DescribeRoot(object value) => value.ToString()!;

    private sealed class NamedOverride
    {
        public override string ToString() => "override";
    }

    public static int OriginalObjectDisplayName() =>
        new NominalNameProbe().Read() == "Program+NominalNameProbe" ? 19 : 0;

    public static int ClosedTypeDisplayName() =>
        typeof(NominalNameProbe.Élément<int>).ToString() ==
            "Program+NominalNameProbe+Élément`1[System.Int32]" ? 23 : 0;

    public static int OpenTypeDisplayName() =>
        typeof(NominalNameProbe.Élément<>).ToString() ==
            "Program+NominalNameProbe+Élément`1[T]" ? 29 : 0;

    public static int ArrayTypeDisplayName() =>
        typeof(byte[]).ToString() == "System.Byte[]" &&
        typeof(NominalNameProbe[]).ToString() == "Program+NominalNameProbe[]" ? 31 : 0;

    public static int RepeatedTypeDisplayName()
    {
        Type descriptor = typeof(NominalNameProbe);
        return ReferenceEquals(descriptor.ToString(), descriptor.ToString()) ? 37 : 0;
    }

    public static int NamespacedTypeDisplayName() =>
        typeof(Exception).ToString() == "System.Exception" ? 41 : 0;

    public static int NullTypeDisplayName()
    {
        try { return ReadTypeName(false).Length; }
        catch (NullReferenceException) { return 43; }
    }

    private static string ReadTypeName(bool useType)
    {
        Type? descriptor = useType ? typeof(NominalNameProbe) : null;
        return descriptor!.ToString();
    }

    private sealed class NominalNameProbe
    {
        internal string Read() => base.ToString()!;
        internal sealed class Élément<T> { }
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
