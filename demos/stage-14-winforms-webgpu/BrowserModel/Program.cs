using System.Text;
using BootstrapLibrary;
using System.Text.Json;
using System.Windows.Forms;
using CounterExample;

if (args.Length != 2)
    throw new ArgumentException("Usage: BrowserModel <form.json> <handlers.wasm>");

var form = new CounterForm();
var controls = form.Controls.Select(control => new
{
    kind = control switch
    {
        Button => "button",
        Label => "label",
        _ => throw new InvalidOperationException("Unsupported browser control")
    },
    name = control.Name,
    text = control.Text,
    x = control.Bounds.X,
    y = control.Bounds.Y,
    width = control.Bounds.Width,
    height = control.Bounds.Height
});
var model = new
{
    title = form.Text,
    width = form.ClientSize.Width,
    height = form.ClientSize.Height,
    acceptButton = form.AcceptButton?.Name,
    controls,
    binding = new { form.Binding.Button, form.Binding.Label, form.Binding.Export, form.Binding.Prefix }
};
// Use the same portable compiler implementation as the self-reproducing Wasm compiler.
using var manifest = new MemoryStream();
using var writer = new BinaryWriter(manifest);
WriteEntry(form.Binding.Export, form.Binding.Row);
WriteEntry(nameof(CounterHandlers.Create), typeof(CounterHandlers).GetMethod(nameof(CounterHandlers.Create))!.MetadataToken & 0xffffff);
WriteEntry(nameof(CounterHandlers.Dispatch), typeof(CounterHandlers).GetMethod(nameof(CounterHandlers.Dispatch))!.MetadataToken & 0xffffff);
WriteEntry(nameof(CounterHandlers.Read), typeof(CounterHandlers).GetMethod(nameof(CounterHandlers.Read))!.MetadataToken & 0xffffff);

void WriteEntry(string export, int row)
{
    byte[] name = Encoding.ASCII.GetBytes(export);
    if (Encoding.ASCII.GetString(name) != export)
        throw new InvalidOperationException("The portable compiler requires ASCII export names.");
    writer.Write(row);
    writer.Write(name.Length);
    writer.Write(name);
}

byte[] dll = File.ReadAllBytes(typeof(CounterHandlers).Assembly.Location);
var wasm = new byte[16384];
int length = CompilerExports.CompileModule(dll, manifest.ToArray(), wasm);
if (length <= 0)
    throw new InvalidOperationException($"Handler compilation failed with status {length}.");
File.WriteAllBytes(args[1], wasm.AsSpan(0, length).ToArray());
File.WriteAllText(args[0], JsonSerializer.Serialize(model, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
}));
