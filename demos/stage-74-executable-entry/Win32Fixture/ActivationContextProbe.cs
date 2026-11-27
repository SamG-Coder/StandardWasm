using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Windows.Win32.Foundation
{
    internal unsafe struct HANDLE
    {
        internal readonly void* Value;
        internal HANDLE(void* value) => Value = value;
    }
}

public static unsafe class ActivationContextProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ACTCTXW
    {
        internal uint Size, Flags;
        internal nint Source;
        internal ushort ProcessorArchitecture, Language;
        internal nint AssemblyDirectory, Resource, ApplicationName, Module;
    }

    [DllImport("KERNEL32.dll", EntryPoint = "CreateActCtxW", ExactSpelling = true)]
    private static extern HANDLE CreateFromStruct(ACTCTXW* request);

    [DllImport("KERNEL32.dll", EntryPoint = "CreateActCtxW", ExactSpelling = true)]
    private static extern nint CreateFromPointer(void* request);

    public static nint Create(int scenario)
    {
        const string moduleName = "System.Windows.Forms.dll";
        char* name = stackalloc char[moduleName.Length + 1];
        for (int i = 0; i < moduleName.Length; i++) name[i] = moduleName[i];
        name[moduleName.Length] = '\0';
        nint module = NativeModuleProbe.FromPointer(name);

        ACTCTXW* request = stackalloc ACTCTXW[1];
        request->Size = (uint)sizeof(ACTCTXW);
        request->Flags = scenario == 2 ? 0x80000088u : 0x88u;
        request->Resource = scenario == 1 ? 65535 : 2;
        request->Module = module;

        Marshal.SetLastSystemError(0);
        nint result = scenario == 3 ? CreateFromPointer(request) : (nint)CreateFromStruct(request).Value;
        Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
        return result;
    }
}
