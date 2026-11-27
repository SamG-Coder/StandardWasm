using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using DynamicCallFixture;

internal static unsafe class Program
{
    [UnmanagedCallersOnly]
    private static uint Write(int input, char* address)
    {
        if (address == null) return uint.MaxValue;
        address[0] = (char)(address[0] + input);
        return unchecked((uint)input + 0xf1234567);
    }

    [UnmanagedCallersOnly]
    private static uint Other(int input, char* address) =>
        unchecked((uint)(address[0] - input));

    private static void Main(string[] args)
    {
        delegate* unmanaged<int, char*, uint> write = &Write, other = &Other;
        char* data = stackalloc char[2] { '\u03a9', '\uffee' };
        uint first = Calls.Invoke((nint)write, -7, data);
        uint second = Calls.Invoke((nint)other, 17, data);
        uint nullPointer = Calls.Invoke((nint)write, 5, null);
        uint pinnedResult = Calls.Pinned((nint)write, 1);
        Type type = typeof(Calls);
        int row = type.GetMethod(nameof(Calls.Invoke))!.MetadataToken & 0x00ffffff;
        var unsupported = type.GetMethod(nameof(Calls.Unsupported))!;
        File.WriteAllText(args[0], JsonSerializer.Serialize(new
        {
            assembly = type.Assembly.Location,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            runtime = Environment.Version.ToString(),
            row,
            unsupportedRow = unsupported.MetadataToken & 0x00ffffff,
            pinnedEntryRow = type.GetMethod(nameof(Calls.Pinned))!.MetadataToken & 0x00ffffff,
            pinnedRow = type.GetMethod("Pin", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .MetadataToken & 0x00ffffff,
            pinnedResult,
            arrayRow = type.GetMethod(nameof(Calls.PinnedArray))!.MetadataToken & 0x00ffffff,
            stackRow = type.GetMethod(nameof(Calls.PinnedStack))!.MetadataToken & 0x00ffffff,
            pinnedArray = Calls.PinnedArray((nint)write, 1),
            pinnedStack = Calls.PinnedStack((nint)write, 1),
            callOffset = Array.IndexOf(unsupported.GetMethodBody()!.GetILAsByteArray()!, (byte)0x29),
            first, second, nullPointer, units = new[] { (int)data[0], (int)data[1] }
        }));
    }
}
