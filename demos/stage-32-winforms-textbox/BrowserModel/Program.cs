using System.Reflection;
using System.Text;
using BootstrapLibrary;
using ProfileBrowser;

bool writeInputs = args is ["--inputs", _] or ["--inputs", _, _];
if (args.Length != 1 && !writeInputs)
    throw new ArgumentException("Usage: BrowserModel <profile.wasm> or BrowserModel --inputs <directory> [<reference.wasm>]");

using var manifest = new MemoryStream();
using var writer = new BinaryWriter(manifest);
var handlers = typeof(BrowserExports);
foreach (var method in handlers.GetMethods(BindingFlags.Public | BindingFlags.Static))
{
    byte[] name = Encoding.ASCII.GetBytes(method.Name);
    writer.Write(method.MetadataToken & 0xffffff);
    writer.Write(name.Length);
    writer.Write(name);
}
byte[] dll = File.ReadAllBytes(handlers.Assembly.Location);
byte[] entries = manifest.ToArray();
if (writeInputs)
{
    string directory = Directory.CreateDirectory(args[1]).FullName;
    File.WriteAllBytes(Path.Combine(directory, "Profile.dll"), dll);
    File.WriteAllBytes(Path.Combine(directory, "Profile.exports"), entries);
    if (args.Length == 2) return;
}

byte[] output = new byte[32768];
int[] diagnostic = CompilerExports.CreateDiagnostic();
int length = CompilerExports.CompileModuleDetailed(dll, entries, output, diagnostic);
if (length <= 0)
    throw new InvalidOperationException($"Profile compilation failed ({length}), MethodDef {diagnostic[1]}, IL offset {diagnostic[2]}.");
File.WriteAllBytes(writeInputs ? args[2] : args[0], output.AsSpan(0, length).ToArray());
