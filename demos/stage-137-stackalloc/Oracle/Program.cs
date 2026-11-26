using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 137 (decision 0065). Run on the 32-bit CLR, whose 4-byte native size is the
// wasm32 width the compiler checks stackalloc sizes against. Every kernel is stateless,
// so one process gives the expected value for each fresh Wasm instance.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]);
Type kernels = Assembly.LoadFrom(fixture).GetType("StackAllocFixture.Kernels", throwOnError: true)!;
var cases = new List<object>();

Run("Bytes", 0);
Run("Bytes", 37);
Run("Bytes", 4096);
Run("Ints", 9);
Run("Ints", 1000);
Run("Nested", 3);
Run("Recursion", 20);
Run("Repeated", 2000);
Run("AfterException", 100);
Run("Uninitialized", 64);
Run("OversizedCount");
Run("Large");
Run("ZeroIsNull", 0);
Run("ZeroIsNull", 1);
Run("Untaken", 0);
Run("OuterAfterInner", 4);

MethodInfo exhaust = kernels.GetMethod("ExhaustAfterPartial")!, bytes = kernels.GetMethod("Large")!;
File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    cases, runtime = Environment.Version.ToString(), process = RuntimeInformation.ProcessArchitecture.ToString(),
    exhaust = new { row = Row(exhaust), after = Row(bytes), afterValue = ((int)bytes.Invoke(null, null)!).ToString() },
    zero = Row(kernels.GetMethod("ZeroIsNull")!), untaken = Row(kernels.GetMethod("Untaken")!),
    outer = Row(kernels.GetMethod("OuterAfterInner")!), outerValue = ((int)kernels.GetMethod("OuterAfterInner")!.Invoke(null, [4])!).ToString()
}));

void Run(string name, params int[] arguments)
{
    MethodInfo method = kernels.GetMethod(name)!;
    string value = ((int)method.Invoke(null, arguments.Cast<object>().ToArray())!).ToString();
    cases.Add(new { name, row = Row(method), arguments, expected = value });
}

static int Row(MethodInfo method) => method.MetadataToken & 0xffffff;
