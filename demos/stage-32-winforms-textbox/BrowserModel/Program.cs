using System.Reflection;
using System.Text;
using BootstrapLibrary;
using ProfileBrowser;

if (args.Length != 1)
    throw new ArgumentException("Usage: BrowserModel <profile.wasm>");

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
byte[] output = new byte[32768];
int[] diagnostic = CompilerExports.CreateDiagnostic();
int length = CompilerExports.CompileModuleDetailed(dll, manifest.ToArray(), output, diagnostic);
if (length <= 0)
    throw new InvalidOperationException($"Profile compilation failed ({length}), MethodDef {diagnostic[1]}, IL offset {diagnostic[2]}.");
File.WriteAllBytes(args[0], output.AsSpan(0, length).ToArray());
