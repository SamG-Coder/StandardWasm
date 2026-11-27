using System;
using System.Runtime.InteropServices;

namespace DynamicCallFixture;

public static unsafe class LibraryCalls
{
    public static string LibraryName() => "KeRnEl32.DlL";

    public static nint Load(int scenario)
    {
        string name = (scenario & 8) != 0 ? null :
            (scenario & 1) == 0 ? LibraryName() : "StandardWasm-Missing-Fixture.dll";
        int flags = (scenario & 4) == 0 ? 0x800 : int.MinValue;
        nint module = LoadNative(name, 0, flags);
        return (scenario & 2) == 0 ? module : Marshal.GetLastPInvokeError();
    }

    public static uint LoadAndInvoke(nint expectedModule, nint target, int count, char* destination)
    {
        nint module = LoadNative("kernel32.dll", 0, 0x800);
        return module == expectedModule ? Calls.Invoke(target, count, destination) : 0;
    }

    public static string ExportName(int scenario) => scenario == 0 ? "GetTempPathW" : "gettemppathw";

    public static int TryFind(int scenario)
    {
        nint library = LoadNative("kernel32.dll", 0, 0x800);
        return NativeLibrary.TryGetExport(library, ExportName(scenario), out nint address) ? address != 0 ? 1 : -1 :
            address == 0 ? 0 : -1;
    }

    public static int Find(int scenario)
    {
        nint library = LoadNative("kernel32.dll", 0, 0x800);
        try { return NativeLibrary.GetExport(library, ExportName(scenario)) != 0 ? 2 : -1; }
        catch (EntryPointNotFoundException) { return 3; }
    }

    private static nint LoadNative(string libraryName, nint reserved, int flags)
    {
        fixed (char* name = libraryName)
        {
            Marshal.SetLastSystemError(0);
            nint result = LoadLibraryExW(name, reserved, flags);
            Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
            return result;
        }
    }

    // This signature and ImplMap match CoreLib's generated raw declaration.
    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", ExactSpelling = true)]
    private static extern nint LoadLibraryExW(char* libraryName, nint reserved, int flags);
}
