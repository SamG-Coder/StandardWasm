using ProfileExample;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var profile = new ProfileWindow();
        var form = profile.Form;
        if (args is ["--benchmark", ..])
        {
            ProfileTiming.Measure(profile, args);
            return;
        }
        if (args.Length != 1)
        {
            Application.Run(form);
            return;
        }

        form.Opacity = 0;
        form.ShowInTaskbar = false;
        form.Show();
        Application.DoEvents();
        var states = new List<object>();
        void Record() => states.Add(new { input = profile.Input.Text, preview = profile.Preview.Text });
        Record();
        profile.Input.Text = "Zo\u00eb \U0001f680!";
        Record();
        profile.Input.Select(0, 3);
        profile.Input.SelectedText = "Ada";
        Record();
        profile.Input.Select(profile.Input.TextLength, 0);
        SendMessage(profile.Input.Handle, 0x0102, (nint)8, 0); // WM_CHAR for Backspace.
        Record();
        profile.Clear.PerformClick();
        Record();
        if (form.ActiveControl != profile.Input)
            throw new InvalidOperationException("Clear did not return focus to the native text box.");
        static int[] Bounds(Control control) => [control.Left, control.Top, control.Width, control.Height];
        var controls = new
        {
            title = form.Text, limit = profile.Input.MaxLength, buttonText = profile.Clear.Text,
            inputBounds = Bounds(profile.Input), previewBounds = Bounds(profile.Preview), buttonBounds = Bounds(profile.Clear),
            promptBounds = Bounds(form.Controls["prompt"]!),
            promptText = form.Controls["prompt"]!.Text,
            children = form.Controls.Cast<Control>().Select(c => c.Name).ToArray()
        };
        File.WriteAllText(args[0], JsonSerializer.Serialize(new { states, controls }));
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
}

internal sealed class ProfileWindow : IDisposable
{
    internal Form Form { get; }
    internal TextBox Input { get; }
    internal Label Preview { get; }
    internal Button Clear { get; }

    internal ProfileWindow()
    {
        var state = ProfileHandlers.Create();
        Form = ProfileApplication.Create(state);
        Input = (TextBox)Form.Controls["name"]!;
        Preview = (Label)Form.Controls["preview"]!;
        Clear = (Button)Form.Controls["clear"]!;

        Form.ClientSize = new Size(ProfileHandlers.ClientWidth(), ProfileHandlers.ClientHeight());
        Form.FormBorderStyle = FormBorderStyle.FixedSingle;
        Form.MaximizeBox = false;
        Input.AccessibleName = ProfileHandlers.InputLabel();
        Input.Font = new Font("Segoe UI", 12);
        Preview.Font = new Font("Segoe UI", 19);
    }

    public void Dispose() => Form.Dispose();
}
