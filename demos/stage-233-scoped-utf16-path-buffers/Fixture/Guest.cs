using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: DisableRuntimeMarshalling]

public static partial class Guest
{
    private const string LongPath = "Long Folder \u03bb\\Long File \u03a9\U0001f642.txt";
    private const string MissingLeafPath = "Long Folder \u03bb\\not-present.txt";
    private const string MissingDirectoryPath = "not-present\\not-present.txt";
    private const int PathUnits = 31; // Includes two UTF-16 units for the supplementary character.
    private const int SystemSentinel = 17185, PInvokeSentinel = 25090;

    public static int Resolve()
    {
        var buffer = new char[40];
        buffer[0] = 'L';
        buffer[PathUnits + 2] = 'R';
        buffer[39] = 'Z';
        SetErrors();
        Span<char> output = new Span<char>(buffer).Slice(1);
        uint count = GetLongPathNameW(ref MemoryMarshal.GetReference(LongPath.AsSpan()), ref MemoryMarshal.GetReference(output), 38);
        return count == PathUnits && buffer[0] == 'L' && buffer[1] == 'L' &&
            buffer[PathUnits - 6] == '\u03a9' && buffer[PathUnits + 1] == '\0' &&
            buffer[PathUnits + 2] == 'R' && buffer[39] == 'Z' && CheckErrors(0, 0) ? 1 : 0;
    }

    public static int Exact()
    {
        var buffer = new char[PathUnits + 1];
        buffer[PathUnits] = '!';
        SetErrors();
        Span<char> output = new Span<char>(buffer);
        uint count = GetLongPathNameW(ref MemoryMarshal.GetReference(LongPath.AsSpan()), ref MemoryMarshal.GetReference(output), (uint)(PathUnits + 1));
        return count == PathUnits && buffer[0] == 'L' && buffer[PathUnits] == '\0' && CheckErrors(0, 0) ? 1 : 0;
    }

    public static int Short()
    {
        var buffer = new char[PathUnits];
        buffer[0] = 'L';
        buffer[1] = 'X';
        buffer[PathUnits - 1] = 'R';
        SetErrors();
        Span<char> output = new Span<char>(buffer).Slice(1);
        uint count = GetLongPathNameW(ref MemoryMarshal.GetReference(LongPath.AsSpan()), ref MemoryMarshal.GetReference(output), (uint)(PathUnits - 1));
        return count == PathUnits + 1 && buffer[0] == 'L' && buffer[1] == 'X' &&
            buffer[PathUnits - 1] == 'R' && CheckErrors(0, 0) ? 1 : 0;
    }

    public static int Query()
    {
        Span<char> output = default;
        SetErrors();
        uint count = GetLongPathNameW(ref MemoryMarshal.GetReference(LongPath.AsSpan()), ref MemoryMarshal.GetReference(output), 0);
        return count == PathUnits + 1 && CheckErrors(0, 0) ? 1 : 0;
    }

    public static int MissingLeaf() => Missing(MissingLeafPath, 2);

    public static int MissingDirectory() => Missing(MissingDirectoryPath, 3);

    private static int Missing(string input, int error)
    {
        var buffer = new char[16];
        buffer[0] = 'X';
        buffer[15] = 'R';
        SetErrors();
        Span<char> output = new Span<char>(buffer);
        uint count = GetLongPathNameW(ref MemoryMarshal.GetReference(input.AsSpan()), ref MemoryMarshal.GetReference(output), 16);
        return count == 0 && buffer[0] == 'X' && buffer[15] == 'R' && CheckErrors(error, error) ? 1 : 0;
    }

    public static int InPlace()
    {
        var buffer = new char[PathUnits + 3];
        Span<char> storage = new Span<char>(buffer).Slice(1);
        LongPath.AsSpan().CopyTo(storage);
        buffer[0] = 'L';
        buffer[PathUnits + 2] = 'R';
        SetErrors();
        uint count = GetLongPathNameW(ref MemoryMarshal.GetReference(storage), ref MemoryMarshal.GetReference(storage), (uint)(PathUnits + 1));
        return count == PathUnits && buffer[0] == 'L' && buffer[1] == 'L' &&
            buffer[PathUnits + 1] == '\0' && buffer[PathUnits + 2] == 'R' && CheckErrors(0, 0) ? 1 : 0;
    }

    public static int NativeInPlace()
    {
        Span<char> storage = stackalloc char[40];
        for (int index = 0; index < PathUnits; index++)
            storage[index] = LongPath[index];
        storage[PathUnits] = '\0';
        storage[PathUnits + 1] = 'R';
        SetErrors();
        uint count = GetLongPathNameW(ref MemoryMarshal.GetReference(storage), ref MemoryMarshal.GetReference(storage), (uint)(PathUnits + 1));
        return count == PathUnits && storage[0] == 'L' && storage[PathUnits] == '\0' &&
            storage[PathUnits + 1] == 'R' && CheckErrors(0, 0) ? 1 : 0;
    }

    public static unsafe int RawQuery()
    {
        char* source = stackalloc char[PathUnits + 1];
        CopyInput(source);
        SetErrors();
        uint count = Raw(source, null, 0);
        return count == PathUnits + 1 && CheckErrors(SystemSentinel, PInvokeSentinel) ? 1 : 0;
    }

    private static unsafe void CopyInput(char* source)
    {
        for (int index = 0; index < PathUnits; index++)
            source[index] = LongPath[index];
        source[PathUnits] = '\0';
    }

    private static void SetErrors()
    {
        Marshal.SetLastPInvokeError(PInvokeSentinel);
        Marshal.SetLastSystemError(SystemSentinel);
    }

    private static bool CheckErrors(int system, int pinvoke) =>
        Marshal.GetLastSystemError() == system && Marshal.GetLastPInvokeError() == pinvoke;

    [LibraryImport("kernel32.dll", EntryPoint = "GetLongPathNameW", SetLastError = true)]
    private static partial uint GetLongPathNameW(ref char path, ref char output, uint capacity);

    [DllImport("kernel32.dll", EntryPoint = "GetLongPathNameW", ExactSpelling = true)]
    private static extern unsafe uint Raw(char* source, char* output, uint capacity);
}
