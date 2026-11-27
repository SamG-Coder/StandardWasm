using System.Reflection;
using System.Runtime.CompilerServices;
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
        int[] oddOffset = OffsetDiagnostic(nameof(OddOffset));
        int[] unknownOffset = OffsetDiagnostic(nameof(UnknownOffset));

        var cases = new Func<int>[]
        {
            BoxedIntMatch, BoxedDoubleMatch, BoxedBooleanMiss, StringMiss, NullMiss, ReferenceMatch, BoxIdentity,
            OriginalIntWrites, OriginalIntTooShort, OriginalCharWrites, OriginalCharTooShort, ReferenceWrites,
            FormatProviderDescriptor, VirtualDescriptorArguments,
            ImplicitByteView, ConstructedByteView, ReturnedByteView, NullByteView, EmptyByteView, ByteViewBounds,
            WritableSpanReference, ClosedGenericSpanReference, DefaultSpanReference,
            PositiveCharacterOffset, NegativeCharacterOffset, NativeCharacterOffset, OddCharacterOffset, UnknownCharacterOffset,
            StringObjectSlot, StringOwnSlot, NullStringObjectSlot,
            OriginalExceptionGetType, OriginalDerivedExceptionGetType, NullExceptionGetType, OriginalObjectDisplayName,
            ClosedTypeDisplayName, OpenTypeDisplayName, ArrayTypeDisplayName, RepeatedTypeDisplayName, NamespacedTypeDisplayName, NullTypeDisplayName,
            ExactRootDispatch, DynamicRootDispatch, NullRootDispatch, PrimitiveVectorRootDispatch,
            TypeSimpleNames, NullTypeSimpleName, TypeNameAndDisplayName,
            WritableSpanString, ReadOnlySpanString, EmptySpanString, NativeSpanString,
            PackedByteEnumRva, PackedShortEnumRva, IntEnumRva, LongEnumRva,
            PrimitiveVectorTypeIdentity, PrimitiveVectorAliases, InitializedPrimitiveVectorIdentity,
            ClosedValueIsNotNull, ClosedValueIsNull, ClosedReferenceNullCheck,
            NarrowEnumFlags, WideEnumFlags, UnsetEnumFlags
        };
        foreach (var run in cases)
        {
            if (run.Method.Name.EndsWith("EnumRva", StringComparison.Ordinal) && !HasArrayInitializer(run.Method))
                throw new InvalidOperationException(run.Method.Name + " did not emit a FieldRVA array initializer.");
        }
        var results = cases.Select(run => new {
            name = run.Method.Name, row = run.Method.MetadataToken & 0xffffff, expected = run(),
            acceptedDiagnostic = run.Method.Name.StartsWith("OriginalInt", StringComparison.Ordinal) ? intDiagnostic :
                run.Method.Name == nameof(OddCharacterOffset) ? oddOffset :
                run.Method.Name == nameof(UnknownCharacterOffset) ? unknownOffset : null
        });
        File.WriteAllText(args[0], JsonSerializer.Serialize(new {
            runtime = Environment.Version.ToString(), files = new[] {
                typeof(Program).Assembly.Location, Path.Combine(runtime, "System.Runtime.dll"),
                typeof(object).Assembly.Location,
                Path.Combine(runtime, "System.Memory.dll")
            }, cases = results
        }));
    }

    private enum NarrowFlag : sbyte { Low = 1, Other = 2, High = -128 }
    private enum WideFlag : long { Low = 4, Other = 8, High = long.MinValue }

    public static int NarrowEnumFlags() =>
        TestNarrow(NarrowFlag.High | NarrowFlag.Low, NarrowFlag.High) &&
        !TestNarrow(NarrowFlag.High, NarrowFlag.Other) && TestNarrow(NarrowFlag.High, 0) ? 101 : 0;

    public static int WideEnumFlags() =>
        TestWide(WideFlag.High | WideFlag.Low, WideFlag.High | WideFlag.Low) &&
        !TestWide(WideFlag.High | WideFlag.Low, WideFlag.Other) && TestWide(WideFlag.High, 0) ? 103 : 0;

    public static int UnsetEnumFlags() =>
        TestNarrow(NarrowFlag.Low, NarrowFlag.Low | NarrowFlag.Other) ||
        TestWide(WideFlag.Other, WideFlag.High) ? 0 : 107;

    private static bool TestNarrow(NarrowFlag value, NarrowFlag mask) =>
        ((Enum)(object)value).HasFlag((Enum)(object)mask);

    private static bool TestWide(WideFlag value, WideFlag mask) =>
        value.HasFlag(mask);

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

    private enum SignedByteData : sbyte { }
    private enum UnsignedShortData : ushort { }
    private enum SignedIntData { }
    private enum SignedLongData : long { }

    public static int PackedByteEnumRva()
    {
        SignedByteData[] values = [(SignedByteData)(-128), (SignedByteData)29, (SignedByteData)(-2),
            0, (SignedByteData)127, (SignedByteData)45, (SignedByteData)15, (SignedByteData)16];
        return (int)values[0] * 101 + (int)values[4] * 13 + (int)values[2];
    }

    public static int PackedShortEnumRva()
    {
        UnsignedShortData[] values = [(UnsignedShortData)65535, (UnsignedShortData)4097, 0,
            (UnsignedShortData)32769, (UnsignedShortData)17, (UnsignedShortData)2051];
        return (int)values[0] + (int)values[3] + (int)values[5];
    }

    public static int IntEnumRva()
    {
        SignedIntData[] values = [(SignedIntData)int.MinValue, 0, (SignedIntData)(-17),
            (SignedIntData)int.MaxValue, (SignedIntData)129];
        if (values.GetType() != typeof(SignedIntData[]) || values.GetType() == typeof(int[])) return 0;
        return (int)values[0] == int.MinValue && (int)values[3] == int.MaxValue ? (int)values[2] + 100 : 0;
    }

    public static int LongEnumRva()
    {
        SignedLongData[] values = [(SignedLongData)long.MinValue, (SignedLongData)(-1099511627776L),
            (SignedLongData)72057594037927937L, (SignedLongData)long.MaxValue, (SignedLongData)3141592653589L];
        return (long)values[0] == long.MinValue && (long)values[2] == 72057594037927937L &&
            (long)values[3] == long.MaxValue ? 97 : 0;
    }

    private static bool HasArrayInitializer(MethodInfo method)
    {
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
        for (int i = 0; i + 15 < il.Length; i++)
        {
            // newarr <type>; dup; ldtoken <field>; call <method>
            if (il[i] != 0x8d || il[i + 5] != 0x25 || il[i + 6] != 0xd0 ||
                il[i + 10] != 4 || il[i + 11] != 0x28 || il[i + 15] != 0x0a) continue;
            int token = BitConverter.ToInt32(il, i + 12);
            return method.Module.ResolveMethod(token) is { Name: "InitializeArray", DeclaringType: var declaring } &&
                declaring == typeof(System.Runtime.CompilerServices.RuntimeHelpers);
        }
        return false;
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

    private static int[] OffsetDiagnostic(string name)
    {
        var method = typeof(Program).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        int call = Array.IndexOf(method.GetMethodBody()!.GetILAsByteArray()!, (byte)0x28);
        return [0, method.MetadataToken & 0xffffff, call];
    }

    public static int PositiveCharacterOffset()
    {
        char[] data = ['a', 'b', 'c', 'd'];
        Span<char> view = new Span<char>(data).Slice(1);
        ref char origin = ref MemoryMarshal.GetReference(view);
        ref char destination = ref OffsetElements(ref origin, 2);
        destination = 'X';
        data[3] = 'Y';
        return data[0] * 100 + view[0] + destination;
    }

    public static int NegativeCharacterOffset()
    {
        char[] data = ['a', 'b', 'c', 'd'];
        Span<char> view = new Span<char>(data).Slice(2);
        ref char origin = ref MemoryMarshal.GetReference(view);
        ref char destination = ref Unsafe.AddByteOffset(ref origin, (nint)(-2));
        destination = 'X';
        return data[1] * 100 + view[0];
    }

    public static int NativeCharacterOffset()
    {
        Span<char> data = stackalloc char[3];
        data[0] = 'a';
        data[1] = 'b';
        data[2] = 'c';
        ref char origin = ref MemoryMarshal.GetReference(data.Slice(1));
        Unsafe.AddByteOffset(ref origin, (nint)(-2)) = 'X';
        Unsafe.AddByteOffset(ref origin, (nuint)2) = 'Y';
        return data[0] * 10000 + data[1] * 100 + data[2];
    }

    public static int OddCharacterOffset()
    {
        char[] data = ['\u1234', '\u5678'];
        return OddOffset(ref MemoryMarshal.GetReference(new Span<char>(data)));
    }

    public static int UnknownCharacterOffset()
    {
        char[] data = ['a', 'b'];
        return UnknownOffset(ref MemoryMarshal.GetReference(new Span<char>(data)), 2);
    }

    private static ref T OffsetElements<T>(ref T value, int elements) =>
        ref Unsafe.AddByteOffset(ref value, (nint)(elements * sizeof(char)));

    private static ref char OddOffset(ref char value) => ref Unsafe.AddByteOffset(ref value, (nint)1);
    private static ref char UnknownOffset(ref char value, nint offset) => ref Unsafe.AddByteOffset(ref value, offset);

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
