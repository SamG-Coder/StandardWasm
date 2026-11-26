using System.Runtime.Loader;
using System.Text.Json;

string fixture = Path.GetFullPath(args[0]);
string dependency = Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency);
var type = AssemblyLoadContext.Default.LoadFromAssemblyPath(fixture).GetType("StringIdentityFixture.Kernels")!;
var methods = new[]
{
    "Local", "SameLiteral", "CrossAssembly", "DifferentLiteral", "Empty", "SameEmpty",
    "EmptyField", "EmptyConstructor", "EmptyConcat", "ConcatLeft", "ConcatRight", "ConcatNonempty",
    "ObjectReturn", "ObjectCast", "ObjectTest", "NullCast", "WrongTest", "WrongCast", "ManagedIdentity", "Dynamic"
}.Select(name => type.GetMethod(name)!).ToArray();
var entries = methods.Select(method => new { name = method.Name, row = method.MetadataToken & 0xffffff });
var values = methods.Select(method => method.Invoke(null, null)).ToArray();
var second = methods.Select(method => method.Invoke(null, null)).ToArray();
var identities = values.Select(left => second.Select(right => ReferenceEquals(left, right)));
File.WriteAllText(args[2], JsonSerializer.Serialize(new { entries, identities, nulls = values.Select(value => value is null) }));
