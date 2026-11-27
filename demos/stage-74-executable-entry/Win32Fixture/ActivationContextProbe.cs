using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Windows.Win32.Foundation
{
    internal struct BOOL
    {
        // The Windows SDK declares static fields before its instance payload.
        internal const int Size = 4;
        internal static readonly BOOL True = new BOOL(1);
        internal static readonly BOOL False = new BOOL(0);
        internal readonly int Value;
        internal BOOL(int value) => Value = value;
    }

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

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern BOOL GetCurrentActCtx(HANDLE* current);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern BOOL ActivateActCtx(HANDLE handle, nuint* cookie);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern BOOL DeactivateActCtx(uint flags, nuint cookie);

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    private static extern void ReleaseActCtx(HANDLE handle);

    public static int PointerRoundTrip(int value, int unsigned)
    {
        void* pointer = (void*)(nint)value;
        return unsigned == 0 ? (int)new nint(pointer) : unchecked((int)new nuint(pointer));
    }

    public static int Lifecycle()
    {
        nint created = Create(0);
        if (created == -1) return 0;
        HANDLE handle = new HANDLE((void*)created);
        HANDLE* current = stackalloc HANDLE[1];
        ulong* cookieStorage = stackalloc ulong[1];
        nuint* cookie = (nuint*)cookieStorage;
        if (GetCurrentActCtx(current).Value == 0)
        {
            ReleaseActCtx(handle);
            return 1;
        }
        void* before = current->Value;
        if (before != null) ReleaseActCtx(new HANDLE(before));
        if (ActivateActCtx(handle, cookie).Value == 0)
        {
            ReleaseActCtx(handle);
            return 2;
        }
        int result = 4;
        if (GetCurrentActCtx(current).Value != 0)
        {
            if (current->Value == handle.Value) result |= 8;
            if (current->Value != null) ReleaseActCtx(new HANDLE(current->Value));
        }
        if (DeactivateActCtx(0, *cookie).Value != 0) result |= 16;
        if (GetCurrentActCtx(current).Value != 0)
        {
            if (current->Value == before) result |= 32;
            if (current->Value != null) ReleaseActCtx(new HANDLE(current->Value));
        }
        ReleaseActCtx(handle);
        return result;
    }

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
