using System.Diagnostics;
using System.Text.Json;

internal static class ProfileTiming
{
    internal static void Measure(ProfileWindow profile, string[] args)
    {
        if (args.Length != 6 || !int.TryParse(args[2], out int samples) || samples is < 1 or > 10000 ||
            !int.TryParse(args[3], out int warmup) || warmup is < 1 or > 10000 || args[4] == args[5])
            throw new ArgumentException("Expected --benchmark output samples warmup first-text second-text.");

        // Exercise the real HWNDs and their paint messages with an invisible test form.
        // This measures synchronous WinForms work, without desktop composition.
        profile.Form.Opacity = 0;
        profile.Form.ShowInTaskbar = false;
        profile.Form.Show();
        Application.DoEvents();

        var update = new double[samples];
        var refresh = new double[samples];
        for (int index = 0; index < warmup + samples; index++)
        {
            string value = args[4 + (index & 1)];
            long beforeEdit = Stopwatch.GetTimestamp();
            profile.Input.Text = value;
            long afterEdit = Stopwatch.GetTimestamp();
            profile.Form.Refresh();
            long afterPaint = Stopwatch.GetTimestamp();
            if (profile.Preview.Text != "Hello, " + value)
                throw new InvalidOperationException("Native TextChanged did not update the preview.");

            if (index < warmup) continue;
            update[index - warmup] = Stopwatch.GetElapsedTime(beforeEdit, afterEdit).TotalMilliseconds;
            refresh[index - warmup] = Stopwatch.GetElapsedTime(afterEdit, afterPaint).TotalMilliseconds;
        }

        File.WriteAllText(args[1], JsonSerializer.Serialize(new
        {
            runtime = Environment.Version.ToString(),
            platform = Environment.OSVersion.VersionString,
            sampleCount = samples,
            warmup,
            updateMs = update,
            refreshMs = refresh
        }));
    }
}
