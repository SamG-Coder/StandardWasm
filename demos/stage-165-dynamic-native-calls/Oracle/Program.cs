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

    private delegate nint CoreLibLookup(nint module, string symbol, bool throwOnError);

    private delegate nint CoreLibLoader(string name, nint reserved, int flags);

    private static object ObserveLoadLibrary()
    {
        const string tempPath = @"C:\StandardWasm\têmp-🛠\";
        Environment.SetEnvironmentVariable("TMP", tempPath);
        nint library = NativeLibrary.Load("kernel32.dll");
        try
        {
            nint target = NativeLibrary.GetExport(library, "GetTempPathW");
            int expected = tempPath.Length + 1;
            char* buffer = stackalloc char[expected];
            uint required = LibraryCalls.LoadAndInvoke(library, target, 0, null);
            uint length = LibraryCalls.LoadAndInvoke(library, target, expected, buffer);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var coreLibLoad = typeof(object).Assembly.GetType("Interop+Kernel32", throwOnError: true)!
                .GetMethod("LoadLibraryEx", flags, null, new[] { typeof(string), typeof(nint), typeof(int) }, null)!;
            var loadOriginal = coreLibLoad.CreateDelegate<CoreLibLoader>();
            nint original = loadOriginal(LibraryCalls.LibraryName(), 0, 0x800);
            int originalError = Marshal.GetLastPInvokeError();
            var getSymbol = typeof(NativeLibrary).GetMethod("GetSymbol", flags,
                null, new[] { typeof(nint), typeof(string), typeof(bool) }, null)!;
            var lookup = getSymbol.CreateDelegate<CoreLibLookup>();
            int originalSymbol = lookup(library, LibraryCalls.ExportName(0), false) == target ? 1 : 0;
            int originalMissing = lookup(library, LibraryCalls.ExportName(1), false) == 0 ? 0 : -1;
            var tryFind = typeof(LibraryCalls).GetMethod(nameof(LibraryCalls.TryFind))!;
            return new
            {
                original = new[] { original == library ? 1 : 0, originalError },
                nameRow = typeof(LibraryCalls).GetMethod(nameof(LibraryCalls.LibraryName))!.MetadataToken & 0x00ffffff,
                coreLibRow = coreLibLoad.MetadataToken & 0x00ffffff,
                symbolRow = typeof(LibraryCalls).GetMethod(nameof(LibraryCalls.Find))!.MetadataToken & 0x00ffffff,
                symbols = new[] { LibraryCalls.Find(0), LibraryCalls.Find(1) },
                symbolNameRow = typeof(LibraryCalls).GetMethod(nameof(LibraryCalls.ExportName))!.MetadataToken & 0x00ffffff,
                originalSymbolRow = getSymbol.MetadataToken & 0x00ffffff,
                originalSymbol = new[] { originalSymbol, originalMissing },
                trySymbolRow = tryFind.MetadataToken & 0x00ffffff,
                trySymbols = new[] { LibraryCalls.TryFind(0), LibraryCalls.TryFind(1) },
                loadRow = typeof(LibraryCalls).GetMethod(nameof(LibraryCalls.Load))!.MetadataToken & 0x00ffffff,
                combinedRow = typeof(LibraryCalls).GetMethod(nameof(LibraryCalls.LoadAndInvoke))!.MetadataToken & 0x00ffffff,
                cases = new[] { LibraryCalls.Load(0) == library ? 1 : 0, (int)LibraryCalls.Load(1),
                    (int)LibraryCalls.Load(2), (int)LibraryCalls.Load(3), (int)LibraryCalls.Load(6), (int)LibraryCalls.Load(10) },
                required,
                length,
                text = new string(buffer, 0, expected),
                tempPath
            };
        }
        finally { NativeLibrary.Free(library); }
    }

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
            loadLibrary = ObserveLoadLibrary(),
            first, second, nullPointer, units = new[] { (int)data[0], (int)data[1] }
        }));
    }
}
