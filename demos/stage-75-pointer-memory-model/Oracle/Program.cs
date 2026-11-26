using System.Text.Json;
using PointerPatterns;

// Writes the CLR result of every scenario and input as JSON: [{ "scenario", "input", "expected" }].
var inputs = new Dictionary<string, int[]>
{
    ["FixedSum"] = [0, 1, 7, 256, 4096],
    ["FixedAliasing"] = [0, 1, 7, 100, 254],
    ["PointerIntoCallee"] = [0, 1, 5, -3],
    ["FixedString"] = [0, 1, 3, 40],
    ["SpanSlice"] = [0, 1, 9, 127],
    ["StackBuffer"] = [0, 1, 3, -2],
    ["LocalStructAddress"] = [0, 1, 7, -4],
    ["StructArrayElement"] = [0, 1, 7, -4],
};
var cases = new List<object>();
foreach (var (scenario, values) in inputs)
{
    var method = typeof(Scenarios).GetMethod(scenario)
        ?? throw new InvalidOperationException("Missing scenario " + scenario);
    foreach (int input in values)
        cases.Add(new { scenario, input, expected = (int)method.Invoke(null, [input])! });
}
File.WriteAllText(args.Single(), JsonSerializer.Serialize(cases));
Console.WriteLine($"CLR oracle: {cases.Count} cases");
