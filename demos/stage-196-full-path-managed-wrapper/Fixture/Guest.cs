using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: DisableRuntimeMarshalling]

public static partial class Guest
{
    public static int Resolve()
    {
        var buffer = new char[34];
        buffer[0] = 'L';
        buffer[33] = 'R';
        SetErrors();
        Span<char> destination = new Span<char>(buffer).Slice(1);
        uint count = GetFullPathNameW(ref MemoryMarshal.GetReference(Path()), 32, ref MemoryMarshal.GetReference(destination), 0);
        return count == 12 && buffer[1] == 'R' && buffer[12] == '\u03bb' && buffer[13] == '\0' &&
            buffer[0] == 'L' && buffer[33] == 'R' && CheckErrors(0) ? 1 : 0;
    }

    public static int Size()
    {
        var buffer = new char[1];
        buffer[0] = 'X';
        SetErrors();
        Span<char> destination = new Span<char>(buffer);
        uint count = GetFullPathNameW(ref MemoryMarshal.GetReference(Path()), 0, ref MemoryMarshal.GetReference(destination), 0);
        return count == 21 && buffer[0] == 'X' && CheckErrors(0) ? 1 : 0;
    }

    public static int SizeWithoutOutput()
    {
        Span<char> destination = default;
        SetErrors();
        uint count = GetFullPathNameW(ref MemoryMarshal.GetReference(Path()), 0, ref MemoryMarshal.GetReference(destination), 0);
        return count == 21 && CheckErrors(0) ? 1 : 0;
    }

    public static int Short()
    {
        var buffer = new char[16];
        buffer[0] = 'X';
        buffer[15] = 'R';
        SetErrors();
        Span<char> destination = new Span<char>(buffer);
        uint count = GetFullPathNameW(ref MemoryMarshal.GetReference(Path()), 15, ref MemoryMarshal.GetReference(destination), 0);
        return count == 21 && buffer[0] == '\0' && buffer[15] == 'R' && CheckErrors(0) ? 1 : 0;
    }

    public static int Failure()
    {
        var buffer = new char[16];
        buffer[0] = 'X';
        SetErrors();
        Span<char> destination = new Span<char>(buffer);
        uint count = GetFullPathNameW(ref MemoryMarshal.GetReference("  ".AsSpan()), 16, ref MemoryMarshal.GetReference(destination), 0);
        return count == 0 && buffer[0] == 'X' && CheckErrors(123) ? 1 : 0;
    }

    public static unsafe int NativeOutput()
    {
        Span<char> buffer = stackalloc char[32];
        nint* part = stackalloc nint[1];
        SetErrors();
        uint count = GetFullPathNameW(ref MemoryMarshal.GetReference(Path()), 32, ref MemoryMarshal.GetReference(buffer), (nint)part);
        return count == 12 && buffer[11] == '\u03bb' && buffer[12] == '\0' &&
            *(char*)*part == '\u03bb' && CheckErrors(0) ? 1 : 0;
    }

    public static int SharedArray()
    {
        var buffer = new char[66];
        Span<char> storage = new Span<char>(buffer);
        Path().CopyTo(storage);
        buffer[21] = 'X';
        buffer[65] = 'Y';
        SetErrors();
        Span<char> destination = storage.Slice(33);
        uint count = GetFullPathNameW(ref MemoryMarshal.GetReference(storage), 32, ref MemoryMarshal.GetReference(destination), 0);
        return count == 12 && buffer[44] == '\u03bb' && buffer[45] == '\0' &&
            buffer[0] == 'R' && buffer[21] == 'X' && buffer[65] == 'Y' && CheckErrors(0) ? 1 : 0;
    }

    private static ReadOnlySpan<char> Path() => "R:\\virtual\\work\\..\\\u03bb".AsSpan();

    private static void SetErrors()
    {
        Marshal.SetLastPInvokeError(25106);
        Marshal.SetLastSystemError(17185);
    }

    private static bool CheckErrors(int expected) =>
        Marshal.GetLastSystemError() == expected && Marshal.GetLastPInvokeError() == expected;

    [LibraryImport("kernel32.dll", EntryPoint = "GetFullPathNameW", SetLastError = true)]
    private static partial uint GetFullPathNameW(ref char path, uint capacity, ref char output, nint filePart);
}
