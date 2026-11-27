using System;
using System.Runtime.InteropServices;

namespace NativePointerFixture
{
    // Stage 365 (issue #275, decision 0083 step 2): unmanaged pointer parameters and results in
    // DllImports. On the x86 CLR each call goes to the real kernel32 or ucrtbase. In Wasm a T*
    // is an i32 address into memory 0, and scripts/native-imports.mjs reads and writes that
    // memory with the same C semantics.
    [StructLayout(LayoutKind.Sequential)]
    public struct FileTime { public uint Low; public uint High; }

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    // Layouts the compiler does not reproduce: Pack = 1 is 5 bytes on the x86 CLR, and auto
    // layout has no CLR-defined order.
    [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct Packed { public byte B; public int I; }
    [StructLayout(LayoutKind.Auto)] public struct Shuffled { public byte B; public int I; }

    public unsafe struct Buffer16 { public fixed byte Data[16]; }

    public delegate int Callback();

    static unsafe class Native
    {
        [DllImport("kernel32", SetLastError = true)]
        public static extern int FileTimeToSystemTime(FileTime* fileTime, SystemTime* systemTime);
        [DllImport("kernel32", SetLastError = true)]
        public static extern int SystemTimeToFileTime(SystemTime* systemTime, FileTime* fileTime);
        [DllImport("ucrtbase")] public static extern UIntPtr strlen(byte* text);
        [DllImport("ucrtbase")] public static extern void* memcpy(void* destination, void* source, UIntPtr count);
        // A pointer to a pointer is still one address.
        [DllImport("ucrtbase", EntryPoint = "memcpy")] public static extern byte** CopyPointers(byte** destination, byte** source, UIntPtr count);

        // These signatures need marshalling or layouts this step does not provide. Each one is a
        // located -2 at the import's MethodDef.
        [DllImport("kernel32", EntryPoint = "FileTimeToSystemTime")]
        public static extern int ByReference(ref FileTime fileTime, out SystemTime systemTime);
        [DllImport("ucrtbase", EntryPoint = "strlen")] public static extern UIntPtr ReadOnlyReference(in FileTime text);
        [DllImport("ucrtbase", EntryPoint = "strlen")] public static extern UIntPtr ByteArray(byte[] text);
        [DllImport("ucrtbase", EntryPoint = "strlen")] public static extern UIntPtr AnsiString(string text);
        [DllImport("ucrtbase", EntryPoint = "abs")] public static extern int DelegateArgument(Callback callback);
        [DllImport("ucrtbase", EntryPoint = "abs")] public static extern int BooleanArgument(bool value);
        [DllImport("ucrtbase", EntryPoint = "abs")] public static extern int CharacterArgument(char value);
        // x86 cdecl passes the struct on the stack, so abs reads its first field.
        [DllImport("ucrtbase", EntryPoint = "abs")] public static extern int StructByValue(FileTime fileTime);
        [DllImport("ucrtbase", EntryPoint = "strlen")] public static extern UIntPtr BooleanPointer(bool* text);
        [DllImport("ucrtbase", EntryPoint = "wcslen")] public static extern UIntPtr CharacterPointer(char* text);
        [DllImport("ucrtbase", EntryPoint = "memcpy")] public static extern void* PackedPointer(Packed* destination, void* source, UIntPtr count);
        [DllImport("ucrtbase", EntryPoint = "memcpy")] public static extern void* AutoLayoutPointer(Shuffled* destination, void* source, UIntPtr count);
    }

    public static unsafe class Kernels
    {
        // 2024-02-29T13:45:30.123Z as a FILETIME (100 ns ticks since 1601-01-01).
        private const long LeapDay = 133536879301230000;

        // Every field of the output in 19 decimal digits, negated when the call failed.
        private static long Pack(int ok, SystemTime time) =>
            (ok == 0 ? -1 : 1) * (((((((long)time.Year * 100 + time.Month) * 10 + time.DayOfWeek) * 100 + time.Day) * 100 +
                time.Hour) * 100 + time.Minute) * 100 + time.Second) * 1000 + time.Milliseconds;

        private static bool Untouched(SystemTime time) =>
            time.Year == 7 && time.Month == 7 && time.DayOfWeek == 7 && time.Day == 7 &&
            time.Hour == 7 && time.Minute == 7 && time.Second == 7 && time.Milliseconds == 7;

        private static SystemTime Sentinel() => new SystemTime
        {
            Year = 7, Month = 7, DayOfWeek = 7, Day = 7, Hour = 7, Minute = 7, Second = 7, Milliseconds = 7
        };

        // Two out-of-line structs through & of native-frame locals.
        public static long FileTimeToDate()
        {
            FileTime file = new FileTime { Low = unchecked((uint)LeapDay), High = (uint)(LeapDay >> 32) };
            SystemTime time = Sentinel();
            int ok = Native.FileTimeToSystemTime(&file, &time);
            return Pack(ok, time);
        }

        public static long FileTimeEpoch()
        {
            FileTime file = new FileTime();
            SystemTime time = Sentinel();
            int ok = Native.FileTimeToSystemTime(&file, &time);
            return Pack(ok, time);
        }

        // SetLastError = true clears the error before the call, so a success after a failure
        // reads back 0 unless the callee itself sets an error.
        public static int ErrorAfterSuccess()
        {
            FileTime bad = new FileTime { High = 0x80000000 };
            FileTime good = new FileTime { Low = 1 };
            SystemTime time;
            int failed = Native.FileTimeToSystemTime(&bad, &time);
            int first = Marshal.GetLastWin32Error();
            int ok = Native.FileTimeToSystemTime(&good, &time);
            return (failed * 10 + ok) * 10000 + first * 100 + Marshal.GetLastWin32Error();
        }

        // The largest FILETIME the conversion accepts: 30828-09-14T02:48:05.477.
        public static long FileTimeLargest()
        {
            FileTime file = new FileTime { Low = 0xffffffff, High = 0x7fffffff };
            SystemTime time = Sentinel();
            int ok = Native.FileTimeToSystemTime(&file, &time);
            return Pack(ok, time);
        }

        // Values with the top bit set are rejected with ERROR_INVALID_PARAMETER, and SetLastError
        // = true saves it. The output struct shows whether the callee wrote anything.
        public static int FileTimeNegative()
        {
            FileTime file = new FileTime { Low = 0, High = 0x80000000 };
            SystemTime time = Sentinel();
            int ok = Native.FileTimeToSystemTime(&file, &time);
            return (ok * 1000 + Marshal.GetLastWin32Error()) * 10 + (Untouched(time) ? 1 : 0);
        }

        public static long SystemTimeToFile()
        {
            SystemTime time = new SystemTime { Year = 2024, Month = 2, Day = 29, Hour = 13, Minute = 45, Second = 30, Milliseconds = 123 };
            FileTime file = new FileTime { Low = 7, High = 7 };
            int ok = Native.SystemTimeToFileTime(&time, &file);
            return ok == 0 ? -1 : (long)file.High << 32 | file.Low;
        }

        // SystemTimeToFileTime ignores DayOfWeek.
        public static long SystemTimeIgnoresWeekday()
        {
            SystemTime time = new SystemTime { Year = 1601, Month = 1, DayOfWeek = 6, Day = 1, Milliseconds = 1 };
            FileTime file = new FileTime { Low = 7, High = 7 };
            int ok = Native.SystemTimeToFileTime(&time, &file);
            return ok == 0 ? -1 : (long)file.High << 32 | file.Low;
        }

        // Round trip through both entries.
        public static long RoundTrip()
        {
            SystemTime time = new SystemTime { Year = 1999, Month = 12, Day = 31, Hour = 23, Minute = 59, Second = 59, Milliseconds = 999 };
            FileTime file;
            if (Native.SystemTimeToFileTime(&time, &file) == 0) return -1;
            SystemTime back = Sentinel();
            int ok = Native.FileTimeToSystemTime(&file, &back);
            return Pack(ok, back);
        }

        private static long Rejected(int year, int month, int day, int hour, int milliseconds)
        {
            SystemTime time = new SystemTime
            {
                Year = (ushort)year, Month = (ushort)month, Day = (ushort)day, Hour = (ushort)hour, Milliseconds = (ushort)milliseconds
            };
            FileTime file = new FileTime { Low = 7, High = 7 };
            int ok = Native.SystemTimeToFileTime(&time, &file);
            return (((long)ok * 10000 + Marshal.GetLastWin32Error()) * 100 + file.Low) * 100 + file.High;
        }

        public static long SystemTimeMonth13() => Rejected(2024, 13, 1, 0, 0);
        public static long SystemTimeNonLeapDay() => Rejected(2023, 2, 29, 0, 0);
        public static long SystemTimeHour24() => Rejected(2024, 1, 1, 24, 0);
        public static long SystemTimeYear1600() => Rejected(1600, 12, 31, 0, 0);
        public static long SystemTimeMilliseconds1000() => Rejected(2024, 1, 1, 0, 1000);

        // A fixed string (Stage 149): the pinned UTF-16 address passes through unchanged. "AB" is
        // the bytes 41 00 42 00, so strlen sees "A" at the start, "" one byte later and "B" at 2.
        public static int StringLengthOfFixedString()
        {
            fixed (char* text = "AB")
                return (int)Native.strlen((byte*)text) * 100 + (int)Native.strlen((byte*)text + 1) * 10 +
                    (int)Native.strlen((byte*)text + 2);
        }

        // A fixed-size buffer in a native-frame struct local, addressed through a pointer (decision 0095).
        public static int StringLengthOfFixedBuffer()
        {
            Buffer16 storage;
            Buffer16* buffer = &storage;
            for (int i = 0; i < 16; i++) buffer->Data[i] = (byte)(i < 11 ? 'a' + i : 0);
            return (int)Native.strlen(buffer->Data);
        }

        public static int StringLengthOfStackalloc()
        {
            byte* text = stackalloc byte[8];
            for (int i = 0; i < 8; i++) text[i] = (byte)(i == 5 ? 0 : 'x');
            return (int)Native.strlen(text);
        }

        public static int CopyBetweenFixedBuffers()
        {
            Buffer16 from, to;
            Buffer16* source = &from, destination = &to;
            for (int i = 0; i < 16; i++) { source->Data[i] = (byte)(i + 1); destination->Data[i] = 0; }
            void* result = Native.memcpy(destination->Data + 1, source->Data + 2, (UIntPtr)5);
            int sum = 0;
            for (int i = 0; i < 8; i++) sum = sum * 10 + destination->Data[i];
            return (result == destination->Data + 1 ? 1 : 0) * 100000000 + sum;
        }

        // UTF-16 code units from a fixed string into a fixed buffer.
        public static int CopyFromFixedString()
        {
            Buffer16 storage;
            Buffer16* buffer = &storage;
            for (int i = 0; i < 16; i++) buffer->Data[i] = 0;
            fixed (char* text = "Wasm") Native.memcpy(buffer->Data, text, (UIntPtr)8);
            return (int)Native.strlen(buffer->Data) * 1000 + buffer->Data[2] + buffer->Data[6];
        }

        public static int CopyIntoFixedBuffer()
        {
            Buffer16 storage;
            Buffer16* buffer = &storage;
            for (int i = 0; i < 16; i++) buffer->Data[i] = 0;
            int value = 0x04030201;
            Native.memcpy(buffer->Data + 3, &value, (UIntPtr)4);
            int sum = 0;
            for (int i = 0; i < 8; i++) sum = sum * 10 + buffer->Data[i];
            return sum;
        }

        // A struct copy with memcpy: FILETIME read back through the destination local.
        public static long CopyStruct()
        {
            FileTime source = new FileTime { Low = 0x89abcdef, High = 0x01234567 };
            FileTime destination;
            Native.memcpy(&destination, &source, (UIntPtr)sizeof(FileTime));
            return (long)destination.High << 32 | destination.Low;
        }

        public static int CopyPointer()
        {
            Buffer16 storage;
            Buffer16* buffer = &storage;
            buffer->Data[0] = 11;
            byte* source = buffer->Data;
            byte* destination = null;
            byte** result = Native.CopyPointers(&destination, &source, (UIntPtr)sizeof(byte*));
            return (result == &destination ? 1000 : 0) + (destination == buffer->Data ? 100 : 0) + *destination;
        }

        // A null argument and a null result: memcpy of zero bytes touches neither pointer and
        // returns its destination.
        public static int NullPointers() => Native.memcpy(null, null, UIntPtr.Zero) == null ? 1 : 0;

        // The diagnostic kernels. The CLR runs each one; Wasm rejects the import itself.
        public static int ByReference()
        {
            FileTime file = new FileTime();
            return Native.ByReference(ref file, out SystemTime time) * 10000 + time.Year;
        }

        public static int ReadOnlyReference()
        {
            FileTime text = new FileTime { Low = 0x4241 };
            return (int)Native.ReadOnlyReference(in text);
        }

        public static int ByteArray() => (int)Native.ByteArray(new byte[] { 0x41, 0x42, 0x43, 0 });
        public static int AnsiString() => (int)Native.AnsiString("abcd");
        public static int DelegateArgument() => Native.DelegateArgument(DelegateTarget) == 0 ? 0 : 1;
        private static int DelegateTarget() => 1;
        public static int BooleanArgument() => Native.BooleanArgument(true);
        public static int CharacterArgument() => Native.CharacterArgument('A');

        public static int StructByValue() => Native.StructByValue(new FileTime { Low = unchecked((uint)-5), High = 9 });

        // The pointer kernels pass addresses whose own construction compiles, so each
        // diagnostic is located at the import rather than at the caller.
        public static int BooleanPointer()
        {
            byte* text = stackalloc byte[2];
            text[0] = 1;
            text[1] = 0;
            return (int)Native.BooleanPointer((bool*)text);
        }

        public static int CharacterPointer()
        {
            fixed (char* text = "abc") return (int)Native.CharacterPointer(text);
        }

        public static int PackedPointer() => Native.PackedPointer(null, null, UIntPtr.Zero) == null ? 1 : 0;
        public static int AutoLayoutPointer() => Native.AutoLayoutPointer(null, null, UIntPtr.Zero) == null ? 1 : 0;
    }
}
