using System.Windows.Forms;

namespace ProfileExample
{
    // This file is compiled against WinForms on Windows and the browser control
    // implementations when targeting Wasm. The hosts connect their own events.
    internal static class ProfileApplication
    {
        internal static Form Create(ProfileState state)
        {
            var form = new Form();
            var input = new TextBox();
            var preview = new Label();
            var clear = new Button();
            var prompt = new Label();
            form.Text = ProfileHandlers.Title();
            input.Name = "name";
            preview.Name = "preview";
            clear.Name = "clear";
            prompt.Name = "prompt";
            prompt.Text = ProfileHandlers.InputLabel();
            input.AutoSize = false;
            input.MaxLength = ProfileHandlers.InputLimit();
            clear.Text = ProfileHandlers.ClearLabel();
            input.SetBounds(ProfileHandlers.Left(ProfileHandlers.Input), ProfileHandlers.Top(ProfileHandlers.Input),
                ProfileHandlers.Width(ProfileHandlers.Input), ProfileHandlers.Height(ProfileHandlers.Input));
            preview.SetBounds(ProfileHandlers.Left(ProfileHandlers.PreviewBox), ProfileHandlers.Top(ProfileHandlers.PreviewBox),
                ProfileHandlers.Width(ProfileHandlers.PreviewBox), ProfileHandlers.Height(ProfileHandlers.PreviewBox));
            clear.SetBounds(ProfileHandlers.Left(ProfileHandlers.ClearButton), ProfileHandlers.Top(ProfileHandlers.ClearButton),
                ProfileHandlers.Width(ProfileHandlers.ClearButton), ProfileHandlers.Height(ProfileHandlers.ClearButton));
            prompt.SetBounds(32, 30, 238, 20);
            NameChanged(state, input, preview);
            form.Controls.Add(input);
            form.Controls.Add(preview);
            form.Controls.Add(clear);
            form.Controls.Add(prompt);
            return form;
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
