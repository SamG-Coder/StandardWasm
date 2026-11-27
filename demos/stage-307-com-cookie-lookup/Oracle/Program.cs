using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.Json;
using ComCookieFixture;

[SupportedOSPlatform("windows")]
internal static unsafe class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
        Guid clsid = new("00000323-0000-0000-c000-000000000046");
        Guid tableId = new("00000146-0000-0000-c000-000000000046");
        Guid unknownId = new("00000000-0000-0000-c000-000000000046");
        int initialized = CoInitializeEx(0, 2);
        Marshal.ThrowExceptionForHR(initialized);

        InterfaceTable* table = null;
        nint instance = 0;
        try
        {
            Marshal.ThrowExceptionForHR(CoCreateInstance(&clsid, null, 1, &tableId, (void**)&table));
            instance = Marshal.GetIUnknownForObject(new object());
            var register = (delegate* unmanaged[Stdcall]<InterfaceTable*, nint, Guid*, uint*, int>)table->VTable[3];
            var revoke = (delegate* unmanaged[Stdcall]<InterfaceTable*, uint, int>)table->VTable[4];
            uint cookie = 0;
            Marshal.ThrowExceptionForHR(register(table, instance, &unknownId, &cookie));
            var live = Lookup(table, cookie, unknownId, instance);
            var missing = Lookup(table, uint.MaxValue, unknownId, instance);
            Marshal.ThrowExceptionForHR(revoke(table, cookie));
            var revoked = Lookup(table, cookie, unknownId, instance);
            string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            File.WriteAllText(args[0], JsonSerializer.Serialize(new
            {
                clr = Environment.Version.ToString(), assembly = typeof(Calls).Assembly.Location,
                dependencies = new[] { Path.Combine(runtime, "netstandard.dll"), Path.Combine(runtime, "System.Runtime.dll"), typeof(object).Assembly.Location },
                row = typeof(Calls).GetMethod(nameof(Calls.Lookup))!.MetadataToken & 0xffffff,
                wrappedRow = typeof(Calls).GetMethod(nameof(Calls.LookupWrapped))!.MetadataToken & 0xffffff,
                rejected = typeof(Calls).GetMethod(nameof(Calls.WrongOutput))!.MetadataToken & 0xffffff,
                iid = unknownId.ToByteArray().Select(value => (int)value),
                live, missing, revoked, wrapped = Wrapped(unknownId)
            }));
        }
        finally
        {
            if (instance != 0) Marshal.Release(instance);
            if (table != null) Marshal.Release((nint)table);
            CoUninitialize();
        }
    }

    private static object Lookup(InterfaceTable* table, uint cookie, Guid iid, nint identity)
    {
        void* result = (void*)17;
        int hr = Calls.Lookup((nint)table, cookie, (nint)(&iid), (nint)(&result));
        bool original = (nint)result == identity;
        if (hr >= 0 && result != null && result != (void*)17) Marshal.Release((nint)result);
        return new { hr, original, cleared = result == null };
    }

    private static object Wrapped(Guid iid)
    {
        void** slots = stackalloc void*[6];
        slots[5] = (delegate* unmanaged[Stdcall]<InterfaceTable*, uint, Guid*, void**, Result>)&WrappedLookup;
        var receiver = new InterfaceTable { VTable = slots };
        void* result = null;
        int hr = Calls.LookupWrapped((nint)(&receiver), uint.MaxValue, (nint)(&iid), (nint)(&result));
        return new { hr, marker = (int)(nint)result };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static Result WrappedLookup(InterfaceTable* receiver, uint cookie, Guid* iid, void** result)
    {
        if (receiver != null && cookie == uint.MaxValue && ((byte*)iid)[8] == 0xc0)
            *result = (void*)37;
        return new Result(unchecked((int)0x80004005));
    }

    [DllImport("OLE32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(nint reserved, uint model);

    [DllImport("OLE32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();

    [DllImport("OLE32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(Guid* clsid, void* outer, uint context, Guid* iid, void** instance);
}
