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
        HANDLE current;
        nuint cookie = 0;
        if (GetCurrentActCtx(&current).Value == 0)
        {
            ReleaseActCtx(handle);
            return 1;
        }
        void* before = ReadHandle(current);
        if (before != null) ReleaseActCtx(new HANDLE(before));
        if (ActivateActCtx(handle, &cookie).Value == 0)
        {
            ReleaseActCtx(handle);
            return 2;
        }
        int result = 4;
        if (GetCurrentActCtx(&current).Value != 0)
        {
            void* active = ReadHandle(current);
            if (active == handle.Value) result |= 8;
            if (active != null) ReleaseActCtx(new HANDLE(active));
        }
        if (DeactivateActCtx(0, cookie).Value != 0) result |= 16;
        if (GetCurrentActCtx(&current).Value != 0)
        {
            void* restored = ReadHandle(current);
            if (restored == before) result |= 32;
            if (restored != null) ReleaseActCtx(new HANDLE(restored));
        }
        ReleaseActCtx(handle);
        return result;
    }

    private static void* ReadHandle(HANDLE handle) => handle.Value;

    public static int NativeAggregateAlias(int seed)
    {
        HANDLE value;
        HANDLE* alias = &value;
        *alias = MakeHandle(17);
        int byLocal = (int)(nint)ReadHandle(value);
        value = MakeHandle(seed);
        return byLocal + (int)(nint)ReadHandle(*alias);
    }

    private static HANDLE MakeHandle(int value) => new HANDLE((void*)(nint)value);

    public static int NativeLocalAlias(int seed)
    {
        nuint word = unchecked((nuint)(uint)seed);
        nuint* alias = &word;
        *alias = 17;
        int byLocal = unchecked((int)word);
        word = unchecked((nuint)(uint)seed);
        return byLocal + unchecked((int)*alias);
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
