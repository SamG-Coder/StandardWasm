using BootstrapLibrary;
using ProfileExample;

internal static class TextBoxCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 8192;
        int Row(Type type, string name) => type.GetMethod(name)!.MetadataToken & 0xffffff;

        byte[] image = File.ReadAllBytes(typeof(ProfileHandlers).Assembly.Location);
        byte[] manifest = BootstrapCases.Manifest(
        [
            ("run", Row(typeof(ProfileCases), nameof(ProfileCases.StoreAndRead))),
            ("TextLength", Row(typeof(ProfileHandlers), nameof(ProfileHandlers.TextLength))),
            ("TextCodeUnit", Row(typeof(ProfileHandlers), nameof(ProfileHandlers.TextCodeUnit)))
        ]);
        var output = new byte[capacity];
        int length = CompilerExports.CompileModule(image, manifest, output);
        if (length <= 0) throw new InvalidOperationException($"Profile fixture failed with compiler status {length}.");
        string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        var inputs = new[] { Encode(image), Encode(manifest) };

        foreach (int edit in Enumerable.Range(0, 6))
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileModule", fixture = $"Profile storage edit {edit}",
                inputs,
                generated = new
                {
                    capacity, arguments = new[] { edit }, hex, multiExport = true, resultType = "string"
                },
                expected = ProfileCases.StoreAndRead(edit)
            });
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
