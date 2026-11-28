using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;

if (!OperatingSystem.IsWindows() || IntPtr.Size != 4)
    throw new InvalidOperationException("This oracle requires Windows x86.");

Type exception = typeof(Exception);
const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
Type kind = exception.GetNestedType("ExceptionMessageKind", BindingFlags.NonPublic)!;
MethodInfo lookup = exception.GetMethod("GetMessageFromNativeResources", Hidden, null, new[] { kind }, null)!;
var inputs = new[] { 1, 2, 3, 0, 4, -1, int.MaxValue };
var cases = inputs.Select(input =>
{
    object[] arguments = [Enum.ToObject(kind, input)];
    var first = (string)lookup.Invoke(null, arguments)!;
    var second = (string)lookup.Invoke(null, arguments)!;
    string decoded = new string(Enumerable.Range(0, StringWitness.Length(first))
        .Select(index => (char)StringWitness.Character(first, index)).ToArray());
    return new { input, text = decoded, same = ReferenceEquals(first, second), empty = ReferenceEquals(first, string.Empty) };
}).ToArray();
File.WriteAllText(args[0], JsonSerializer.Serialize(new { coreLib = exception.Assembly.Location,
    witness = typeof(StringWitness).Assembly.Location,
    row = MetadataTokens.GetRowNumber(MetadataTokens.EntityHandle(lookup.MetadataToken)),
    clr = Environment.Version.ToString(), cases }));
