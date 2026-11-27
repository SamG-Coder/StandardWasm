using System.Reflection;
using System.Text;

namespace StandardWasm;

internal static class TransferEmitterProbe
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Assembly Compiler = typeof(BootstrapLibrary.CompilerExports).Assembly;

    internal static void Write(string path)
    {
        var body = new byte[512];
        var sizes = new int[1];
        var offsets = new int[1];
        int length = Invoke("NativeByteTransferWasm", "Write", body, 0, 0, offsets, sizes);
        if (length <= 0) throw new InvalidOperationException($"Transfer body returned {length}.");
        var output = new byte[4096];
        var signature = new[]
        {
            Constant("PortableCompiler.SignatureTypes", "Void"), Constant("PortableCompiler.SignatureTypes", "Native"),
            Constant("PortableCompiler.SignatureTypes", "Bytes"), Constant("PortableCompiler.SignatureTypes", "I32"),
            Constant("PortableCompiler.SignatureTypes", "I32")
        };
        const string name = "transfer";
        byte[] entry = [1, 0, 0, 0, (byte)name.Length, 0, 0, 0, .. Encoding.ASCII.GetBytes(name)];
        int registrySize = Constant("PeLibrary.ObjectLayouts", "FieldMapStart") + 1;
        long memory = Constant("PortableCompiler.WasmImports", "Memory");
        int exception = Constant("PeLibrary.ExceptionTypes", "ArgumentOutOfRange");
        // Use the same module writer and managed byte vector ABI as a linked library.
        length = Invoke("WasmBinary", "WriteModule", output, new[] { 4 }, new[] { 0 }, signature, sizes, offsets,
            1, body, entry, new[] { 0 }, new[] { 0 }, new int[registrySize], new[] { 0 }, Array.Empty<byte>(), 0,
            new int[20], 0, exception, memory, 0, New("FunctionPointerPlan"), 0, 0, false, New("NativeImportTable"),
            New("PrimitiveArrayWasm"), new[] { 0 });
        if (length <= 0) throw new InvalidOperationException($"Transfer module returned {length}.");
        File.WriteAllBytes(path, output.AsSpan(0, length).ToArray());
    }

    private static object New(string type) => Activator.CreateInstance(Compiler.GetType("PortableCompiler." + type)!, true)!;

    private static int Constant(string type, string field) =>
        Convert.ToInt32(Compiler.GetType(type)!.GetField(field, Static)!.GetRawConstantValue());

    private static int Invoke(string type, string method, params object[] arguments) =>
        (int)Compiler.GetType("PortableCompiler." + type)!.GetMethod(method, Static)!.Invoke(null, arguments)!;
}
