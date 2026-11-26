using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Stage 132 (decision 0059). The 32-bit CLR runs each kernel over host memory from
// Marshal.AllocHGlobal. Pointer arguments are offsets into one image, so the Wasm
// runner can place the same bytes in its lent linear memory and pass the same offsets.
// The oracle records the return value and the image afterwards, so stores compare too.
if (Environment.Is64BitProcess || IntPtr.Size != 4)
    throw new InvalidOperationException("Run the oracle on a 32-bit CLR.");
string evidence = args[0], fixture = Path.GetFullPath(args[1]);
Type kernels = Assembly.LoadFrom(fixture).GetType("StructPointerFixture.Kernels", throwOnError: true)!;

const int ImageSize = 256;
var cases = new List<object>();
byte[] Pattern(int seed) => Enumerable.Range(0, ImageSize).Select(i => unchecked((byte)(i * 37 + seed))).ToArray();

// Argument kinds: "pointer" is an offset into the image; "int" is a plain value.
foreach (string name in new[] { "ByteLongSize", "ByteDoubleSize", "ByteIntSize", "ShortByteSize", "NestedSize", "WithPtrSize",
    "CharBoolSize", "EmptySize", "LongByteSize", "RectSize", "WithEnumSize", "WithStaticSize" })
    Run(name, Pattern(0), []);
Run("Area", Pattern(5), [Pointer(16)]);
Run("Grow", Pattern(5), [Pointer(32), Int(3)]);
Run("MixedSum", Pattern(5), [Pointer(64)]);
Run("FieldAddress", Pattern(5), [Pointer(8)]);
Run("NullRect", Pattern(5), []);
Run("Walk", Pattern(5), [Pointer(0), Int(8)]);
Run("Walk", Pattern(9), [Pointer(128), Int(0)]);
Run("ConstructorFieldStores", Pattern(9), [Pointer(32), Int(17)]);

// Pack and Size: the CLR measures them (5 and 12), and the compiler diagnoses the sizeof (-2).
foreach (string name in new[] { "PackedSize", "SizedSize" })
{
    MethodInfo method = kernels.GetMethod(name)!;
    int clr = (int)method.Invoke(null, null)!;
    int row = method.MetadataToken & 0xffffff;
    // sizeof is the first instruction: FE 1C <token> at offset 0.
    if (method.GetMethodBody()!.GetILAsByteArray()![0] != 0xfe) throw new InvalidOperationException(name + " does not start with sizeof");
    cases.Add(new { name, row, image = Pattern(0), arguments = Array.Empty<object>(), result = "Int32", expected = clr.ToString(),
        after = Pattern(0), status = -2, diagnostic = new[] { 0, row, 0 } });
}

File.WriteAllText(Path.Combine(evidence, "oracle.json"), JsonSerializer.Serialize(new
{
    fixture, imageSize = ImageSize, cases, runtime = Environment.Version.ToString(),
    process = RuntimeInformation.ProcessArchitecture.ToString()
}));

unsafe void Run(string name, byte[] image, object[][] arguments)
{
    MethodInfo method = kernels.GetMethod(name)!;
    IntPtr block = Marshal.AllocHGlobal(ImageSize);
    try
    {
        Marshal.Copy(image, 0, block, ImageSize);
        var parameters = method.GetParameters();
        var values = new object?[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            string kind = (string)arguments[i][0];
            int value = (int)arguments[i][1];
            values[i] = kind == "pointer"
                ? System.Reflection.Pointer.Box((void*)(block + value), parameters[i].ParameterType)
                : Convert.ChangeType(value, parameters[i].ParameterType);
        }
        object result = method.Invoke(null, values)!;
        var after = new byte[ImageSize];
        Marshal.Copy(block, after, 0, ImageSize);
        string expected = result is double d ? d.ToString("R") : result.ToString()!;
        cases.Add(new
        {
            name, row = method.MetadataToken & 0xffffff, image, arguments = arguments.Select(a => new { kind = a[0], value = a[1] }),
            result = result.GetType().Name, expected, after
        });
    }
    finally { Marshal.FreeHGlobal(block); }
}

static object[] Pointer(int offset) => ["pointer", offset];
static object[] Int(int value) => ["int", value];
