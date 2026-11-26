using System.Text.Json;

namespace WinFormsCensus;

/// <summary>One bundle entry in compiler order.</summary>
internal sealed record BundleIdentity(string name, string sha256);

/// <summary>
/// The inputs a recorded verdict depends on: the compiler assembly and the
/// ordered bundle images. A replayed census must match all of them exactly.
/// </summary>
internal sealed record VerdictIdentity(string compiler, string compilerSha256, List<BundleIdentity> bundle)
{
    /// <summary>Returns null when the recorded identity equals <paramref name="current"/>, else the first difference.</summary>
    public static string? Compare(JsonElement census, VerdictIdentity current)
    {
        if (!census.TryGetProperty("verdictIdentity", out JsonElement recorded) || recorded.ValueKind != JsonValueKind.Object)
            return "it records no verdict identity";
        if (recorded.GetProperty("compilerSha256").GetString() != current.compilerSha256)
            return $"compiler {current.compiler} differs (recorded {recorded.GetProperty("compilerSha256").GetString()}, current {current.compilerSha256})";
        JsonElement[] bundle = recorded.GetProperty("bundle").EnumerateArray().ToArray();
        if (bundle.Length != current.bundle.Count)
            return $"bundle has {bundle.Length} assemblies, current has {current.bundle.Count}";
        for (int i = 0; i < bundle.Length; i++)
        {
            string? name = bundle[i].GetProperty("name").GetString(), hash = bundle[i].GetProperty("sha256").GetString();
            if (name != current.bundle[i].name || hash != current.bundle[i].sha256)
                return $"bundle entry {i} is {name} {hash}, current is {current.bundle[i].name} {current.bundle[i].sha256}";
        }
        return null;
    }
}
