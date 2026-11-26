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
Type kernels = Assembly.LoadFrom(fixture).GetType("UnmanagedPointerFixture.Kernels", throwOnError: true)!;

const int ImageSize = 256;
var cases = new List<object>();
byte[] Pattern(int seed) => Enumerable.Range(0, ImageSize).Select(i => unchecked((byte)(i * 37 + seed))).ToArray();

// Argument kinds: "pointer" is an offset into the image; "int" is a plain value.
Run("SumBytes", Pattern(5), [Pointer(16), Int(40)]);
Run("SumBytes", Pattern(9), [Pointer(0), Int(0)]);
Run("SumBytes", Pattern(1), [Pointer(200), Int(56)]);
Run("WriteThenRead", Pattern(0), [Pointer(32), Int(24)]);
Run("SignedBytes", Pattern(200), [Pointer(8), Int(64)]);
Run("Words", Pattern(77), [Pointer(0), Pointer(128), Int(32)]);
Run("Integers", Pattern(3), [Pointer(0), Pointer(64), Int(16)]);
Run("Floats", Floats(), [Pointer(0), Pointer(64), Int(12)]);
Run("IndexOf", Pattern(11), [Pointer(10), Int(100), Int(Pattern(11)[10 + 63])]);
Run("IndexOf", Pattern(11), [Pointer(10), Int(5), Int(0x100 - 1)]);
Run("ReadNull", Pattern(0), []);
Run("InitializeNull", Pattern(0), []);

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

static byte[] Floats()
{
    var image = new byte[ImageSize];
    for (int i = 0; i < 12; i++) BitConverter.GetBytes(i * 1.25f - 3.5f).CopyTo(image, i * 4);
    return image;
}
