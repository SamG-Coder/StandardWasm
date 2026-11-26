using System.Text;
using BootstrapLibrary;

internal static class BootstrapCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 2 << 20;
        var type = typeof(CompilerExports);
        byte[] dll = File.ReadAllBytes(type.Assembly.Location);
        string[] exports = ["CompileModule", "CompileBundle", "Compile", "CreateBytes", "SetByte", "ReadByte", "FindRow",
            "CreateDiagnostic", "ReadDiagnostic", "CompileDetailed", "CompileModuleDetailed", "CompileBundleDetailed", "ReadWebcilEntryToken"];
        byte[] manifest = Manifest(exports.Select(name => (name, Row(name))));
        var image = Encode(dll);
        var compiler = new byte[capacity];
        int compilerSize = CompilerExports.CompileModule(dll, manifest, compiler);
        if (compilerSize <= 0)
            throw new InvalidOperationException($"CLR self-compilation returned {compilerSize}");
        cases.Add(new
        {
            module = "BootstrapLibrary", bootstrap = true, inputs = new[] { image, Encode(manifest) }, exports, capacity,
            expectedHex = Convert.ToHexString(compiler.AsSpan(0, compilerSize)).ToLowerInvariant()
        });

        int kernel = Row(nameof(CompilerExports.BootstrapKernel));
        byte[] aliases = Manifest([("first", kernel), ("Café東京𐐀", kernel)]);
        var module = new byte[capacity];
        int size = CompilerExports.CompileModule(dll, aliases, module);
        if (size <= 0)
            throw new InvalidOperationException($"Alias module compilation returned {size}");
        string hex = Convert.ToHexString(module.AsSpan(0, size)).ToLowerInvariant();
        foreach (string name in new[] { "first", "Café東京𐐀" })
        {
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileModule", fixture = name,
                inputs = new[] { image, Encode(aliases) },
                generated = new { capacity = 512, arguments = new[] { 21 }, hex, multiExport = true, export = name },
                expected = CompilerExports.BootstrapKernel(21)
            });
        }

        Reject("duplicate export", Manifest([("first", kernel), ("first", kernel)]), -1);
        Reject("truncated export name", aliases[..^1], -1);
        Reject("missing method definition", Manifest([("missing", 0)]), -1);
        Reject("unsupported name bytes", Manifest([("line\nbreak", kernel)]), -2);
        foreach (byte[] name in new byte[][] { [0xc0, 0x80], [0xed, 0xa0, 0x80], [0xf0, 0x90, 0x80] })
        {
            byte[] invalid = Manifest([(new string('x', name.Length), kernel)]);
            name.CopyTo(invalid, 8);
            Reject("malformed UTF-8 export name", invalid, -2);
        }
        Reject("reserved overflow export", Manifest([("\0overflow", kernel)]), -2);

        int Row(string name) => type.GetMethod(name)!.MetadataToken & 0xffffff;

        void Reject(string fixture, byte[] entries, int expected)
        {
            byte[] output = Enumerable.Repeat((byte)0xa5, 512).ToArray();
            if (CompilerExports.CompileModule(dll, entries, output) != expected || output.Any(x => x != 0xa5))
                throw new InvalidOperationException($"Compiler did not reject {fixture} without writes");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileModule", fixture,
                inputs = new[] { image, Encode(entries) },
                generated = new { capacity = output.Length, arguments = Array.Empty<int>(), reject = true, multiExport = true },
                expected
            });
        }
    }

    internal static byte[] Manifest(IEnumerable<(string Name, int Row)> entries)
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        foreach (var (name, row) in entries)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            writer.Write(row);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        return buffer.ToArray();
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
