using System.Runtime.InteropServices;

internal static unsafe class RegistryOracle
{
    internal static object Observe(nint kernel32)
    {
        var load = (delegate* unmanaged[Stdcall]<char*, nint, uint, nint>)NativeLibrary.GetExport(kernel32, "LoadLibraryExW");
        var find = (delegate* unmanaged[Stdcall]<nint, byte*, nint>)NativeLibrary.GetExport(kernel32, "GetProcAddress");
        var free = (delegate* unmanaged[Stdcall]<nint, int>)NativeLibrary.GetExport(kernel32, "FreeLibrary");

        nint absent;
        int missingModuleError;
        fixed (char* name = "StandardWasm-Missing-Fixture.dll")
        {
            absent = load(name, 0, 0x800);
            missingModuleError = Marshal.GetLastSystemError();
        }
        nint module;
        fixed (char* name = "KERNEL32.DLL") module = load(name, 0, 0x800);
        if (module == 0) throw new InvalidOperationException("Kernel32 failed to load");
        try
        {
            nint entry;
            fixed (byte* name = "GetTempPathW\0"u8) entry = find(module, name);
            nint mismatchedCase;
            int missingExportError;
            fixed (byte* name = "gettemppathw\0"u8)
            {
                mismatchedCase = find(module, name);
                missingExportError = Marshal.GetLastSystemError();
            }
            nint additional;
            fixed (byte* name = "MulDiv\0"u8) additional = find(module, name);
            if (additional == 0) throw new InvalidOperationException("MulDiv export was missing");
            var mulDiv = (delegate* unmanaged[Stdcall]<int, int, int, int>)additional;
            return new
            {
                missingModule = absent == 0,
                missingModuleError,
                addressResolved = entry != 0,
                caseSensitive = mismatchedCase == 0,
                missingExportError,
                distinctExports = additional != entry,
                additionalResult = mulDiv(10, 3, 2)
            };
        }
        finally { free(module); }
    }
}
