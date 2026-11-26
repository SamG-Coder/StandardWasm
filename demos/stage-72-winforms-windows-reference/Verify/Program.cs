using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using WinFormsReference;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Expected ordinary output, single-file output and evidence directories.");
        var (ordinary, single, output) = (args[0], args[1], args[2]);
        Directory.CreateDirectory(output);
        string app = Path.Combine(ordinary, "WinFormsReference.dll");
        if (Inventory.Hash(app) != Inventory.Hash(typeof(ReferenceForm).Assembly.Location))
            throw new InvalidOperationException("The interaction verifier must use the original managed assembly.");

        var ordinaryLaunch = ObserveApphost(Path.Combine(ordinary, "WinFormsReference.exe"));
        var singleFile = Path.Combine(single, "WinFormsReference.exe");
        var bundledLaunch = ObserveApphost(singleFile);
        if (File.Exists(Path.Combine(single, "WinFormsReference.dll")))
            throw new InvalidOperationException("Single-file output unexpectedly contains a companion entry assembly.");

        ApplicationConfiguration.Initialize();
        var interaction = ObserveForm(output);
        var evidence = new
        {
            windows = RuntimeInformation.OSDescription,
            runtime = RuntimeInformation.FrameworkDescription,
            desktop = typeof(Form).Assembly.GetName().FullName,
            desktopFileVersion = FileVersionInfo.GetVersionInfo(typeof(Form).Assembly.Location).FileVersion,
            ordinaryLaunch,
            bundledLaunch,
            ordinaryFiles = Inventory.Files(ordinary),
            bundledFiles = Inventory.Files(single),
            interaction
        };
        WriteJson(Path.Combine(output, "windows.json"), evidence);
        WriteJson(Path.Combine(output, "dependencies.json"), Inventory.Read(app));
        Console.WriteLine("Windows reference: both apphosts launched, interaction and rendering passed, and the static inventory was written.");
    }

    private static object ObserveApphost(string path)
    {
        using var app = Process.Start(new ProcessStartInfo(path)
        {
            WorkingDirectory = Path.GetDirectoryName(path)!,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = true
        }) ?? throw new InvalidOperationException("Could not launch the apphost.");
        try
        {
            var wait = Stopwatch.StartNew();
            while (!app.HasExited && wait.Elapsed < TimeSpan.FromSeconds(10))
            {
                nint window = NativeWindowForProcess(app.Id);
                if (window != 0)
                {
                    if (!PostMessage(window, 0x0010, 0, 0) || !app.WaitForExit(5000) || app.ExitCode != 0)
                        throw new InvalidOperationException("The apphost did not close normally.");
                    return new { file = Path.GetFileName(path), windowTitle = "StandardWasm WinForms Reference", exitCode = app.ExitCode };
                }
                Thread.Sleep(50);
            }

            throw new InvalidOperationException($"The reference window was not observed for {path}.");
        }
        finally
        {
            if (!app.HasExited)
            {
                app.Kill();
                app.WaitForExit();
            }
        }
    }

    private static nint NativeWindowForProcess(int processId)
    {
        nint result = 0;
        // Process.MainWindowHandle excludes the hidden window we use for the launch probe.
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != (uint)processId) return true;
            var title = new StringBuilder(128);
            GetWindowText(window, title, title.Capacity);
            if (title.ToString() != "StandardWasm WinForms Reference") return true;
            result = window;
            return false;
        }, 0);
        return result;
    }

    private delegate bool WindowVisitor(nint window, nint data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowVisitor visitor, nint data);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder title, int capacity);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);

    private static object ObserveForm(string output)
    {
        using var form = new ReferenceForm { ShowInTaskbar = false, Opacity = 0 };
        var trace = new List<string>();
        var input = (TextBox)form.Controls.Find("name", true).Single();
        var submit = (Button)form.Controls.Find("submit", true).Single();
        var preview = (Label)form.Controls.Find("preview", true).Single();
        form.Load += (_, _) => trace.Add("Load");
        form.Shown += (_, _) => trace.Add("Shown");
        input.TextChanged += (_, _) => trace.Add("TextChanged");
        preview.TextChanged += (_, _) => trace.Add("PreviewChanged");
        submit.Click += (_, _) => trace.Add("Click");
        form.FormClosed += (_, _) => trace.Add("FormClosed");

        form.Show();
        Application.DoEvents();
        string before = preview.Text;
        Screenshot(form, Path.Combine(output, "initial.png"));
        input.Text = "  Ada  ";
        submit.PerformClick();
        Application.DoEvents();
        string after = preview.Text;
        Screenshot(form, Path.Combine(output, "edited.png"));
        var bounds = new[] { Bounds(input), Bounds(submit), Bounds(preview) };
        int dpi = form.DeviceDpi;
        var font = new { name = form.Font.Name, points = form.Font.SizeInPoints };
        form.Close();

        if (before != "Ready" || after != "Hello, Ada" ||
            !trace.SequenceEqual(new[] { "Load", "Shown", "TextChanged", "PreviewChanged", "Click", "FormClosed" }))
            throw new InvalidOperationException("The observed WinForms text or event sequence was unexpected.");
        if (Inventory.Hash(Path.Combine(output, "initial.png")) == Inventory.Hash(Path.Combine(output, "edited.png")))
            throw new InvalidOperationException("Interaction did not change the captured frame.");
        return new { before, after, trace, bounds, dpi, font };

        object Bounds(Control control)
        {
            Point location = form.PointToClient(control.PointToScreen(Point.Empty));
            return new { name = control.Name, x = location.X, y = location.Y, width = control.Width, height = control.Height };
        }
    }

    private static void Screenshot(Form form, string file)
    {
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
        image.Save(file, ImageFormat.Png);
    }

    private static void WriteJson(string path, object value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
