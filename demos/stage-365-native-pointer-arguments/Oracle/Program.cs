using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 365 (issue #275, decision 0083 step 2). Run on the 32-bit CLR: the fixture's pointer
// DllImports bind to the real x86 kernel32 and ucrtbase, and their results are the expected
// values. The runtime directory supplies the CoreLib and facades the Wasm runner bundles.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]);
string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var files = new List<string> { fixture };
foreach (string name in new[] { "netstandard", "System.Runtime", "System.Runtime.InteropServices", "System.Private.CoreLib" })
    files.Add(Path.Combine(runtimeDirectory, name + ".dll"));
Assembly assembly = Assembly.LoadFrom(fixture);
Type kernels = assembly.GetType("NativePointerFixture.Kernels", throwOnError: true)!;
Type native = assembly.GetType("NativePointerFixture.Native", throwOnError: true)!;
var cases = new List<object>();
foreach (string name in new[] { "FileTimeToDate", "FileTimeEpoch", "FileTimeLargest", "FileTimeNegative", "ErrorAfterSuccess",
    "SystemTimeToFile", "SystemTimeIgnoresWeekday", "RoundTrip", "SystemTimeMonth13", "SystemTimeNonLeapDay",
    "SystemTimeHour24", "SystemTimeYear1600", "SystemTimeMilliseconds1000", "StringLengthOfFixedString",
    "StringLengthOfFixedBuffer", "StringLengthOfStackalloc", "CopyBetweenFixedBuffers", "CopyFromFixedString", "CopyIntoFixedBuffer",
    "CopyStruct", "CopyPointer", "NullPointers" })
{
    MethodInfo method = kernels.GetMethod(name)!;
    cases.Add(new { name, row = method.MetadataToken & 0xffffff, expected = method.Invoke(null, null)!.ToString() });
}
// Signatures this step does not lower stay a located -2 at the P/Invoke itself. The CLR still
// runs each kernel, so the fixture is ordinary code on both sides.
foreach (string name in new[] { "ByReference", "ReadOnlyReference", "ByteArray", "AnsiString", "DelegateArgument",
    "BooleanArgument", "CharacterArgument", "StructByValue", "BooleanPointer", "CharacterPointer", "PackedPointer",
    "AutoLayoutPointer" })
{
    MethodInfo method = kernels.GetMethod(name)!;
    MethodInfo import = native.GetMethod(name, BindingFlags.Static | BindingFlags.Public)!;
    cases.Add(new
    {
        name, row = method.MetadataToken & 0xffffff, expected = method.Invoke(null, null)!.ToString(),
        status = -2, diagnostic = new[] { 0, import.MetadataToken & 0xffffff, -1 }
    });
}
File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    files, cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString()
}));
