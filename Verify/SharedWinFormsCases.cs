extern alias BrowserForms;

using BootstrapLibrary;
using BrowserExports = BrowserForms::ProfileBrowser.BrowserExports;
using FormCases = BrowserForms::ProfileBrowser.FormCases;

internal static class SharedWinFormsCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        int Row(Type type, string name) => type.GetMethod(name)!.MetadataToken & 0xffffff;
        byte[] image = File.ReadAllBytes(typeof(BrowserExports).Assembly.Location);
        byte[] manifest = BootstrapCases.Manifest(
        [
            ("run", Row(typeof(FormCases), nameof(FormCases.Run))),
            ("TextLength", Row(typeof(BrowserExports), nameof(BrowserExports.TextLength))),
            ("TextCodeUnit", Row(typeof(BrowserExports), nameof(BrowserExports.TextCodeUnit)))
        ]);
        byte[] output = new byte[capacity];
        int length = CompilerExports.CompileModule(image, manifest, output);
        if (length <= 0)
            throw new InvalidOperationException($"Shared form fixture returned {length}.");
        string hex = Convert.ToHexString(output.AsSpan(0, length)).ToLowerInvariant();
        var inputs = new[] { Encode(image), Encode(manifest) };
        foreach (int action in Enumerable.Range(0, 5))
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileModule", fixture = $"Shared WinForms source {action}",
                inputs,
                generated = new { capacity, arguments = new[] { action }, hex, multiExport = true, resultType = "string" },
                expected = FormCases.Run(action)
            });
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
