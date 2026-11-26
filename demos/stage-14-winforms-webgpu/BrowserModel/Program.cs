using System.Text;
using BootstrapLibrary;
using System.Text.Json;
using CounterExample;

if (args.Length != 2)
    throw new ArgumentException("Usage: BrowserModel <resources.json> <handlers.wasm>");

var resources = new
{
    keys = new { result = CounterLayout.Result },
    kinds = new { button = ControlTree.Button, label = ControlTree.Label },
    columns = new
    {
        key = ControlTree.Key, kind = ControlTree.Kind, x = ControlTree.X, y = ControlTree.Y,
        width = ControlTree.Width, height = ControlTree.Height
    }
};

// The browser creates controls at runtime. This build step only packages control schema IDs and compiles exports.
using var manifest = new MemoryStream();
using var writer = new BinaryWriter(manifest);
var handlers = typeof(CounterHandlers);
foreach (var method in handlers.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
{
    byte[] name = Encoding.ASCII.GetBytes(method.Name);
    if (Encoding.ASCII.GetString(name) != method.Name)
        throw new InvalidOperationException("The portable compiler requires ASCII export names.");
    writer.Write(method.MetadataToken & 0xffffff);
    writer.Write(name.Length);
    writer.Write(name);
}
byte[] dll = File.ReadAllBytes(handlers.Assembly.Location);
var wasm = new byte[32768];
int length = CompilerExports.CompileModule(dll, manifest.ToArray(), wasm);
if (length <= 0)
    throw new InvalidOperationException($"Handler compilation failed with status {length}.");
File.WriteAllBytes(args[1], wasm.AsSpan(0, length).ToArray());
File.WriteAllText(args[0], JsonSerializer.Serialize(resources));
