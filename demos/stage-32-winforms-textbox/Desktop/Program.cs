using ProfileExample;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var form = new ProfileForm();
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
        void Record() => states.Add(new { input = form.Input.Text, preview = form.Preview.Text });
        Record();
        form.Input.Text = "Zo\u00eb \U0001f680!";
        Record();
        form.Input.Select(0, 3);
        form.Input.SelectedText = "Ada";
        Record();
        form.Input.Select(form.Input.TextLength, 0);
        SendMessage(form.Input.Handle, 0x0102, (nint)8, 0); // WM_CHAR for Backspace.
        Record();
        form.Clear.PerformClick();
        Record();
        if (form.ActiveControl != form.Input)
            throw new InvalidOperationException("Clear did not return focus to the native text box.");
        static int[] Bounds(Control control) => [control.Left, control.Top, control.Width, control.Height];
        var controls = new
        {
            title = form.Text, limit = form.Input.MaxLength, buttonText = form.Clear.Text,
            inputBounds = Bounds(form.Input), previewBounds = Bounds(form.Preview), buttonBounds = Bounds(form.Clear)
        };
        File.WriteAllText(args[0], JsonSerializer.Serialize(new { states, controls }));
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
}

internal sealed class ProfileForm : Form
{
    private readonly ProfileState state = ProfileHandlers.Create();
    internal readonly TextBox Input = new() { AutoSize = false, AccessibleName = ProfileHandlers.InputLabel() };
    internal readonly Label Preview = new();
    internal readonly Button Clear = new();

    internal ProfileForm()
    {
        ClientSize = new Size(ProfileHandlers.ClientWidth(), ProfileHandlers.ClientHeight());
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        var prompt = new Label { Text = ProfileHandlers.InputLabel(), Left = 32, Top = 30 };
        Preview.Font = new Font("Segoe UI", 19);
        Input.Font = new Font("Segoe UI", 12);
        ProfileApplication.Initialize(state, this, Input, Preview, Clear);
        Input.TextChanged += (_, _) => ProfileApplication.NameChanged(state, Input, Preview);
        Clear.Click += (_, _) => ProfileApplication.Clear(Input);
        Controls.AddRange([prompt, Input, Clear, Preview]);
    }
}
