using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using ComActivationFixture;

[SupportedOSPlatform("windows")]
internal static unsafe class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (IntPtr.Size != 4) throw new InvalidOperationException("Expected Windows x86 CLR.");
        Guid tableClass = new("00000323-0000-0000-c000-000000000046");
        Guid tableInterface = new("00000146-0000-0000-c000-000000000046");
        Guid unknown = new("00000000-0000-0000-c000-000000000046");
        Guid absent = new("2581a478-caf8-4a62-946a-4d81a7ee514a");
        Marshal.ThrowExceptionForHR(CoInitializeEx(0, 2));
        nint outer = 0;
        try
        {
            outer = Marshal.GetIUnknownForObject(new object());
            string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            File.WriteAllText(args[0], JsonSerializer.Serialize(new
            {
                clr = Environment.Version.ToString(),
                assembly = typeof(Calls).Assembly.Location,
                dependencies = new[] { Path.Combine(runtime, "netstandard.dll"),
                    Path.Combine(runtime, "System.Runtime.dll"), typeof(object).Assembly.Location },
                rows = new { activate = Row(nameof(Calls.Activate)), lookup = Row(nameof(Calls.Lookup)),
                    wrongOutput = Row(nameof(Calls.WrongOutput)), wrongLibrary = Row(nameof(Calls.WrongLibrary)),
                    badOutputLeaf = Row("BadOutput"), badLibraryLeaf = Row("BadLibrary") },
                ids = new { tableClass, tableInterface, unknown, absent },
                cases = new
                {
                    success = Activate(tableClass, 0, 1, tableInterface, lookupInterface: unknown),
                    unknownClass = Activate(absent, 0, 1, tableInterface),
                    unknownInterface = Activate(tableClass, 0, 1, absent),
                    wrongContext = Activate(tableClass, 0, 4, tableInterface),
                    aggregated = Activate(tableClass, outer, 1, unknown),
                    nullOutput = Activate(tableClass, 0, 1, tableInterface, nullOutput: true)
                }
            }));
        }
        finally
        {
            if (outer != 0) Marshal.Release(outer);
            CoUninitialize();
        }
    }

    private static int Row(string name) =>
        typeof(Calls).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .MetadataToken & 0xffffff;

    private static object Activate(Guid clsid, nint outer, uint context, Guid iid,
        Guid? lookupInterface = null, bool nullOutput = false)
    {
        void* result = (void*)17;
        int hr = Calls.Activate((nint)(&clsid), outer, context, (nint)(&iid), nullOutput ? 0 : (nint)(&result));
        bool activated = result != null && result != (void*)17;
        try
        {
            if (!activated || lookupInterface is not Guid lookupIid)
                return new { hr, activated, cleared = result == null };
            void* found = (void*)17;
            int lookup = Calls.Lookup((nint)result, uint.MaxValue, (nint)(&lookupIid), (nint)(&found));
            if (lookup >= 0 && found != null && found != (void*)17) Marshal.Release((nint)found);
            return new { hr, activated, cleared = false, lookup, lookupCleared = found == null };
        }
        finally
        {
            if (activated) Marshal.Release((nint)result);
        }
    }

    [DllImport("OLE32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(nint reserved, uint model);

    [DllImport("OLE32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();
}
