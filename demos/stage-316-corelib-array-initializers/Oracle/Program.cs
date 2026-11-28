using System.Reflection.Metadata.Ecma335;
using System.Text.Json;

if (!OperatingSystem.IsWindows() || IntPtr.Size != 4)
    throw new InvalidOperationException("Expected Windows x86 CLR.");

var cases = new List<object>();
AddVector(nameof(Guest.PathCharacters), Guest.PathCharacters);
AddVector(nameof(Guest.FileCharacters), Guest.FileCharacters);
AddVector(nameof(Guest.LibraryLiteral), Guest.LibraryLiteral);
foreach (int value in new[] { 0, 65, 0x80ab, 0xffff })
    Add(nameof(Guest.FreshPathArray), Guest.FreshPathArray, value);
File.WriteAllText(args[0], JsonSerializer.Serialize(new {
    assembly = typeof(Guest).Assembly.Location, clr = Environment.Version.ToString(), cases
}));

void AddVector(string method, Func<int, int> call)
{
    int length = call(-1);
    for (int index = -1; index < length; index++) Add(method, call, index);
}

void Add(string method, Func<int, int> call, int argument)
{
    int row = MetadataTokens.GetRowNumber(MetadataTokens.EntityHandle(typeof(Guest).GetMethod(method)!.MetadataToken));
    cases.Add(new { method, row, arguments = new[] { argument }, value = call(argument) });
}
