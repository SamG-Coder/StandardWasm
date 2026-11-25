using System.Windows.Forms;

namespace ProfileExample
{
    // This file is compiled against WinForms on Windows and the browser control
    // implementations when targeting Wasm. The hosts connect their own events.
    internal static class ProfileApplication
    {
        internal static void Initialize(ProfileState state, Form form, TextBox input, Label preview, Button clear)
        {
            form.Text = ProfileHandlers.Title();
            input.MaxLength = ProfileHandlers.InputLimit();
            clear.Text = ProfileHandlers.ClearLabel();
            input.SetBounds(ProfileHandlers.Left(ProfileHandlers.Input), ProfileHandlers.Top(ProfileHandlers.Input),
                ProfileHandlers.Width(ProfileHandlers.Input), ProfileHandlers.Height(ProfileHandlers.Input));
            preview.SetBounds(ProfileHandlers.Left(ProfileHandlers.PreviewBox), ProfileHandlers.Top(ProfileHandlers.PreviewBox),
                ProfileHandlers.Width(ProfileHandlers.PreviewBox), ProfileHandlers.Height(ProfileHandlers.PreviewBox));
            clear.SetBounds(ProfileHandlers.Left(ProfileHandlers.ClearButton), ProfileHandlers.Top(ProfileHandlers.ClearButton),
                ProfileHandlers.Width(ProfileHandlers.ClearButton), ProfileHandlers.Height(ProfileHandlers.ClearButton));
            NameChanged(state, input, preview);
        }

        internal static void NameChanged(ProfileState state, TextBox input, Label preview)
        {
            ProfileHandlers.Edit(state, input.Text);
            preview.Text = ProfileHandlers.Preview(state);
        }

        internal static void Clear(TextBox input)
        {
            input.Text = "";
            input.Focus();
        }
    }
}
