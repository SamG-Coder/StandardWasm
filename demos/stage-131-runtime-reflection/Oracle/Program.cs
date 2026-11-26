using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

var modes = new[] { "file", "memory" };
var oldFixture = Path.GetFullPath(args[0]);
var oldDependency = Path.GetFullPath(args[1]);
string work = Path.GetFullPath(args[2]);
string fixture = Path.Combine(work, "東京-ReflectionFixture.dll");
string dependency = Path.Combine(work, "Dépendency.dll");
File.Copy(oldFixture, fixture, true);
File.Copy(oldDependency, dependency, true);
var results = new List<object>();
const string resourceName = "StandardWasm.Reflection.é.東京.𐐀";
var resourceResults = new List<object>();
foreach (string mode in modes)
{
    var context = new AssemblyLoadContext(mode, isCollectible: true);
    Assembly Load(string path)
    {
        if (mode == "file") return context.LoadFromAssemblyPath(path);
        using var stream = File.OpenRead(path);
        return context.LoadFromStream(stream);
    }

    Load(dependency);
    Assembly owner = Load(fixture);
    var type = owner.GetType("ReflectionFixture.Kernels")!;
    using (Stream resource = owner.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException("Missing resource fixture."))
    {
        resourceResults.Add(new { mode, length = resource.Length,
            digest = Convert.ToHexString(SHA256.HashData(resource)).ToLowerInvariant() });
    }
    foreach (string method in new[] { "ModuleName", "DependencyModuleName", "ModuleIdentity", "AssemblyIdentity", "NullModule", "ResourceBufferIdentity", "OrdinalResourceNames", "OrdinalNullAndEmpty", "OrdinalSeparateStrings" })
    {
        MethodInfo info = type.GetMethod(method)!;
        results.Add(new { mode, method, row = info.MetadataToken & 0xffffff, expected = (int)info.Invoke(null, null)! });
    }
    context.Unload();
}
File.WriteAllText(Path.Combine(work, "oracle.json"),
    JsonSerializer.Serialize(new { fixture, dependency, results, resourceName, resourceResults },
        new JsonSerializerOptions { WriteIndented = true }));
