#:property PublishAot=false

using System.Reflection;

if (args is not [var compiler, var packed, var entries])
    throw new ArgumentException("Provide BootstrapLibrary.dll, bundle.bin and entries.bin.");

var compile = Assembly.LoadFrom(Path.GetFullPath(compiler))
    .GetType("BootstrapLibrary.CompilerExports", throwOnError: true)!
    .GetMethod("CompileBundleDetailed")!
    .CreateDelegate<Func<byte[], byte[], byte[], int[], int>>();
byte[] output = new byte[1 << 20];
Array.Fill(output, (byte)0xa5);
int[] location = new int[3];
int status = compile(File.ReadAllBytes(packed), File.ReadAllBytes(entries), output, location);
bool historical = Environment.GetEnvironmentVariable("STANDARDWASM_STAGE118_DIAGNOSTIC") == "1";
int[] expected = historical ? [0, 3, 361] : [1, 4340, 0];
if (status != (historical ? -2 : -4) || !location.AsSpan().SequenceEqual(expected))
    throw new InvalidOperationException($"Unexpected result {status} [{string.Join(", ", location)}].");
if (Array.Exists(output, value => value != 0xa5))
    throw new InvalidOperationException("Compilation modified the output buffer on rejection.");
Console.WriteLine($"Original WinForms returned {status} [{string.Join(", ", location)}] and left all 1,048,576 output bytes untouched.");
