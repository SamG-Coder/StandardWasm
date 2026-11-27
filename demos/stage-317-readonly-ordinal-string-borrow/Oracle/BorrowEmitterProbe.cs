using System.Reflection;

internal static class BorrowEmitterProbe
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Assembly Compiler = typeof(BootstrapLibrary.CompilerExports).Assembly;

    internal static bool CheckMetadata(byte[] image, int caller, int raw)
    {
        var layout = new int[4];
        var heaps = new int[8];
        if (Invoke("PeLibrary.CompressedTables", "FindWithHeaps", image, layout, heaps) < 0) return false;
        return (bool)Compiler.GetType("PortableCompiler.ReadOnlyStringBorrow")!.GetMethod("IsOrdinalInput", Static)!
            .Invoke(null, new object[] { image, layout, heaps, caller, raw })!;
    }

    internal static void Write(string path)
    {
        int integer = Constant("PortableCompiler.SignatureTypes", "I32");
        int text = Constant("PortableCompiler.SignatureTypes", "String");
        int location = Constant("PortableCompiler.SignatureTypes", "ReadOnlyCharacterLocation");
        int chars = Constant("PortableCompiler.SignatureTypes", "Chars");
        const int stringArgument = 0, readArgument = 1, borrowed = 2;
        var body = new byte[512];
        int used = 2;
        body[0] = 1;
        body[1] = 1;
        used = Invoke("PortableCompiler.WasmBinary", "ValueType", body, used, location);
        used = Invoke("PortableCompiler.StringWasm", "BorrowCharacters", body, used, stringArgument);
        used = Local(body, used, 0x21, borrowed);
        used = Local(body, used, 0x20, readArgument);
        body[used++] = 0x04;
        body[used++] = 0x7f;
        used = Invoke("PortableCompiler.ManagedLocationWasm", "AccessElement", body, used, borrowed, chars);
        used = Invoke("PortableCompiler.BranchWasm", "Gc", body, used, 0x0d, chars);
        body[used++] = 0x05;
        used = Local(body, used, 0x20, borrowed);
        body[used++] = 0xd1;
        body[used++] = 0x45;
        body[used++] = 0x0b;
        body[used++] = 0x0b;

        var output = new byte[4096];
        int registry = Constant("PeLibrary.ObjectLayouts", "FieldMapStart") + 1;
        int tag = Constant("PeLibrary.ExceptionTypes", "NullReference");
        byte[] entry = [1, 0, 0, 0, 6, 0, 0, 0, 98, 111, 114, 114, 111, 119];
        int length = Invoke("PortableCompiler.WasmBinary", "WriteModule", output, new[] { 2 }, new[] { 0 },
            new[] { integer, text, integer }, new[] { used }, new[] { 0 }, 1, body, entry, new[] { 0 },
            new[] { 0 }, new int[registry], new[] { 0 }, Array.Empty<byte>(), 0, new int[20], 0, tag, 0L, 0,
            New("FunctionPointerPlan"), 0, 0, false, New("NativeImportTable"), New("PrimitiveArrayWasm"), new[] { 0 });
        if (length <= 0) throw new InvalidOperationException($"Borrow module returned {length}.");
        File.WriteAllBytes(path, output.AsSpan(0, length).ToArray());
    }

    private static int Local(byte[] code, int used, byte operation, int local) =>
        Invoke("PortableCompiler.BranchWasm", "Local", code, used, operation, local);

    private static object New(string type) => Activator.CreateInstance(Compiler.GetType("PortableCompiler." + type)!, true)!;

    private static int Constant(string type, string name) =>
        Convert.ToInt32(Compiler.GetType(type)!.GetField(name, Static)!.GetRawConstantValue());

    private static int Invoke(string type, string name, params object[] arguments) =>
        (int)Compiler.GetType(type)!.GetMethod(name, Static)!.Invoke(null, arguments)!;
}
