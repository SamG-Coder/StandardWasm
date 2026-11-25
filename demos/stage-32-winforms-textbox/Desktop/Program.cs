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
        File.WriteAllText(args[0], JsonSerializer.Serialize(states));
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
}

internal sealed class ProfileForm : Form
{
    private readonly ProfileState state = ProfileHandlers.Create();
    internal readonly TextBox Input = new() { MaxLength = ProfileHandlers.InputLimit(), AccessibleName = ProfileHandlers.InputLabel() };
    internal readonly Label Preview = new();
    internal readonly Button Clear = new() { Text = ProfileHandlers.ClearLabel() };

    internal ProfileForm()
    {
        Text = ProfileHandlers.Title();
        ClientSize = new Size(ProfileHandlers.ClientWidth(), ProfileHandlers.ClientHeight());
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        var prompt = new Label { Text = ProfileHandlers.InputLabel(), Left = 32, Top = 30 };
        Preview.Font = new Font("Segoe UI", 19);
        Preview.Bounds = BoundsFor(ProfileHandlers.PreviewBox);
        Input.Font = new Font("Segoe UI", 12);
        Input.Bounds = BoundsFor(ProfileHandlers.Input);
        Clear.Bounds = BoundsFor(ProfileHandlers.ClearButton);
        Input.TextChanged += (_, _) => RefreshPreview();
        Clear.Click += (_, _) =>
        {
            Input.Clear();
            Input.Focus();
        };
        Controls.AddRange([prompt, Input, Clear, Preview]);
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        ProfileHandlers.Edit(state, Input.Text);
        Preview.Text = ProfileHandlers.Preview(state);
    }

    private static Rectangle BoundsFor(int control) =>
        new(ProfileHandlers.Left(control), ProfileHandlers.Top(control),
            ProfileHandlers.Width(control), ProfileHandlers.Height(control));
}
