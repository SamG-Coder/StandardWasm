using System.Text;
using System.Text.Json;

namespace WinFormsCensus;

/// <summary>Renders the ranked Markdown summary from the same data written to census.json.</summary>
internal static class Summary
{
    public static string Render(object report, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(report, options));
        JsonElement root = document.RootElement;
        var text = new StringBuilder();
        JsonElement graph = root.GetProperty("graph"), compiler = root.GetProperty("compiler"), pinvoke = root.GetProperty("pinvoke");
        text.AppendLine("# WinForms blocker census");
        text.AppendLine();
        text.AppendLine($"Entry `{root.GetProperty("entry").GetProperty("name").GetString()}`. " +
            $"Reached {graph.GetProperty("reachedMethods")} methods ({graph.GetProperty("withIlBody")} with IL), " +
            $"{graph.GetProperty("instantiatedTypes")} instantiated types, {graph.GetProperty("initializedTypes")} initialized types, " +
            $"{graph.GetProperty("virtualSlots")} virtual slots, {graph.GetProperty("unresolvedReferences")} unresolved references.");
        text.AppendLine();
        JsonElement entry = compiler.GetProperty("entry");
        text.AppendLine($"Whole-program verdict from `Program.Main`: status {entry.GetProperty("status")} at {entry.GetProperty("site").GetString()} " +
            $"({entry.GetProperty("category").GetString()}: `{entry.GetProperty("detail").GetString()}`).");
        text.AppendLine();
        text.AppendLine("| Compiler verdict | Methods |");
        text.AppendLine("| --- | ---: |");
        foreach (string key in new[] { "judged", "compiles", "blockedInOwnBody", "blockedInClosure", "exportAbiNoVerdict", "notJudged" })
            text.AppendLine($"| {key} | {compiler.GetProperty(key)} |");
        text.AppendLine();

        text.AppendLine("## Blocker categories by reached methods whose own body stops there");
        text.AppendLine();
        Table(text, compiler.GetProperty("categoriesByOwnBody"), 40);
        text.AppendLine("## Blocker categories by reached methods whose compile stops there first (own or closure)");
        text.AppendLine();
        Table(text, compiler.GetProperty("categoriesByFirstBlocker"), 40);

        text.AppendLine("## Method-level suspects");
        text.AppendLine();
        text.AppendLine("Types and static features of method-level failures that occur in no reached method whose own body fully lowered (compiles or blocked only in a callee).");
        text.AppendLine();
        text.AppendLine("| Failed methods | Kind | Type |");
        text.AppendLine("| ---: | --- | --- |");
        foreach (JsonElement suspect in compiler.GetProperty("methodLevelSuspects").EnumerateArray().Take(40))
            text.AppendLine($"| {suspect.GetProperty("failedMethods")} | {suspect.GetProperty("kind").GetString()} | `{Cell(suspect.GetProperty("type").GetString())}` |");
        text.AppendLine();

        text.AppendLine("## Top blocker sites");
        text.AppendLine();
        text.AppendLine("| Blocks | Site | IL | Category | Operand |");
        text.AppendLine("| ---: | --- | --- | --- | --- |");
        foreach (JsonElement site in compiler.GetProperty("sites").EnumerateArray().Take(60))
            text.AppendLine($"| {site.GetProperty("blocksReachedMethods")} | `{Cell(site.GetProperty("method").GetString())}` ({site.GetProperty("site").GetString()}) | " +
                $"{Offset(site.GetProperty("offset").GetInt32())} | {site.GetProperty("category").GetString()} | `{Cell(site.GetProperty("detail").GetString())}` |");
        text.AppendLine();

        text.AppendLine("## Static features of reached methods");
        text.AppendLine();
        text.AppendLine("| Feature | Reached methods | Own-body compiler blocks |");
        text.AppendLine("| --- | ---: | ---: |");
        foreach (JsonElement feature in root.GetProperty("features").EnumerateArray())
            text.AppendLine($"| {feature.GetProperty("feature").GetString()} | {feature.GetProperty("reachedMethods")} | {feature.GetProperty("blockedInOwnBody")} |");
        text.AppendLine();

        text.AppendLine("## P/Invoke surface");
        text.AppendLine();
        text.AppendLine($"Reached {pinvoke.GetProperty("reachedDistinctEntryPoints")} distinct DLL entry points ({pinvoke.GetProperty("reachedMethods")} P/Invoke methods). " +
            $"The loaded Windows Desktop assemblies declare {pinvoke.GetProperty("declaredDistinctEntryPoints")} distinct entry points.");
        text.AppendLine();
        text.AppendLine("| DLL | Reached | Reached from Windows Desktop | Declared by Windows Desktop | Reached entry points |");
        text.AppendLine("| --- | ---: | ---: | ---: | --- |");
        foreach (JsonElement dll in pinvoke.GetProperty("byDll").EnumerateArray())
        {
            var names = dll.GetProperty("reachedEntryPoints").EnumerateArray().Select(e => e.GetString()).ToList();
            text.AppendLine($"| {dll.GetProperty("dll").GetString()} | {names.Count} | {dll.GetProperty("reachedFromWindowsDesktop")} | " +
                $"{dll.GetProperty("declaredByWindowsDesktop")} | {string.Join(", ", names)} |");
        }
        text.AppendLine();

        text.AppendLine("## Reached methods by assembly");
        text.AppendLine();
        text.AppendLine("| Assembly | Reached | First witness path from the entry |");
        text.AppendLine("| --- | ---: | --- |");
        foreach (JsonElement assembly in graph.GetProperty("byAssembly").EnumerateArray())
        {
            var path = assembly.GetProperty("firstReachedPath").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
            string shown = path.Count <= 6
                ? string.Join(" → ", path)
                : string.Join(" → ", path.Take(3)) + " → … → " + string.Join(" → ", path.TakeLast(2));
            text.AppendLine($"| {assembly.GetProperty("assembly").GetString()} | {assembly.GetProperty("reachedMethods")} | {Cell(shown)} |");
        }
        text.AppendLine();
        return text.ToString();
    }

    private static string Cell(string? value) => (value ?? "").Replace("|", "\\|");

    private static string Offset(int offset) => offset < 0 ? "method" : $"IL_{offset:x4}";

    private static void Table(StringBuilder text, JsonElement rows, int limit)
    {
        text.AppendLine("| Category | Methods |");
        text.AppendLine("| --- | ---: |");
        foreach (JsonElement row in rows.EnumerateArray().Take(limit))
            text.AppendLine($"| {row.GetProperty("category").GetString()} | {row.GetProperty("methods")} |");
        text.AppendLine();
    }
}
