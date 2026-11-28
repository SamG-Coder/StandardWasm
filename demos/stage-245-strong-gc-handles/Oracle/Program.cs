using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BootstrapLibrary;

if (IntPtr.Size != 4) throw new InvalidOperationException("Use the Windows x86 CLR.");
string coreLib = typeof(object).Assembly.Location;
string directory = Path.GetDirectoryName(coreLib)!;
string[] dependencies = [coreLib, Path.Combine(directory, "System.Runtime.dll"),
    Path.Combine(directory, "System.Runtime.InteropServices.dll"), Path.Combine(directory, "System.Threading.dll")];
var guest = typeof(Guest);
var names = new[] { nameof(Guest.Lifecycle), nameof(Guest.NullTarget), nameof(Guest.Distinct),
    nameof(Guest.FreeTwice), nameof(Guest.DefaultTarget), nameof(Guest.InvalidProfile), nameof(Guest.InvalidToken),
    nameof(Guest.Unsupported), nameof(Guest.HoldFinalizable), nameof(Guest.Release), nameof(Guest.ReadTarget),
    nameof(Guest.Finalized), nameof(Guest.ProbeTarget), nameof(Guest.PinnedAddress) };
var rows = names.ToDictionary(name => name, name => guest.GetMethod(name)!.MetadataToken & 0xffffff);
int pinnedAddressRow = typeof(GCHandle).GetMethod(nameof(GCHandle.AddrOfPinnedObject))!.MetadataToken & 0xffffff;

var normal = new { Lifecycle = Guest.Lifecycle(), NullTarget = Guest.NullTarget(), Distinct = Guest.Distinct(),
    FreeTwice = Guest.FreeTwice(), DefaultTarget = Guest.DefaultTarget(), InvalidProfile = Guest.InvalidProfile(),
    InvalidToken = Guest.InvalidToken(0) };
int[] profiles = [0, 1, 3];
int[] windowsProfiles = profiles.Select(Guest.Unsupported).ToArray();
int before = Guest.Finalized();
nint handle = Guest.HoldFinalizable(53);
Collect();
int held = Guest.Finalized() - before;
int value = Guest.ReadTarget(handle);
Guest.Release(handle);
Collect();
int released = Guest.Finalized() - before;
var roots = new { held, value, released };

using var bundled = new MemoryStream();
using var images = new BinaryWriter(bundled);
foreach (string path in new[] { guest.Assembly.Location }.Concat(dependencies))
{
    byte[] image = File.ReadAllBytes(path);
    images.Write(image.Length);
    images.Write(image);
}
using var entryBytes = new MemoryStream();
using var entries = new BinaryWriter(entryBytes);
foreach (string name in new[] { nameof(Guest.HoldFinalizable), nameof(Guest.ReadTarget), nameof(Guest.Release), nameof(Guest.Finalized) })
{
    byte[] label = Encoding.UTF8.GetBytes(name);
    entries.Write(rows[name]);
    entries.Write(label.Length);
    entries.Write(label);
}
var compile = typeof(CompilerExports).Assembly.GetType("PortableCompiler.MethodCompiler", throwOnError: true)!
    .GetMethod("CompileBundleWithRootProbe", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<CompileRoots>();
byte[] output = new byte[2 << 20];
int[] diagnostic = new int[3];
int length = compile(bundled.ToArray(), entryBytes.ToArray(), output, diagnostic);
if (length <= 0) throw new InvalidOperationException($"Root probe returned {length} at {string.Join(", ", diagnostic)}.");
string rootProbe = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "root-probe.wasm");
File.WriteAllBytes(rootProbe, output[..length]);
File.WriteAllText(args[0], JsonSerializer.Serialize(new
{
    runtime = Environment.Version + "/" + RuntimeInformation.ProcessArchitecture,
    assembly = guest.Assembly.Location, dependencies, rows, normal, profiles, windowsProfiles, roots, rootProbe, pinnedAddressRow
}));

static void Collect()
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
}

delegate int CompileRoots(byte[] bundle, byte[] entries, byte[] output, int[] diagnostic);
