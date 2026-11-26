using System.Reflection;
using System.Runtime.Loader;
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
    var type = Load(fixture).GetType("ReflectionFixture.Kernels")!;
    foreach (string method in new[] { "ModuleName", "DependencyModuleName", "ModuleIdentity", "AssemblyIdentity", "NullModule" })
    {
        MethodInfo info = type.GetMethod(method)!;
        results.Add(new { mode, method, row = info.MetadataToken & 0xffffff, expected = (int)info.Invoke(null, null)! });
    }
    context.Unload();
}
File.WriteAllText(Path.Combine(work, "oracle.json"),
    JsonSerializer.Serialize(new { fixture, dependency, results }, new JsonSerializerOptions { WriteIndented = true }));
