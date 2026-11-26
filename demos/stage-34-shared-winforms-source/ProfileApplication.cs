#nullable enable annotations

using System;
using System.Windows.Forms;

namespace ProfileExample
{
    // This file is compiled against WinForms on Windows and the browser control
    // implementations when targeting Wasm. Both builds use these subscriptions.
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
            var events = new ProfileEvents(state, input, preview);
            events.RefreshPreview();
            input.TextChanged += events.NameChanged;
            clear.Click += events.ClearClicked;
            form.Controls.Add(input);
            form.Controls.Add(preview);
            form.Controls.Add(clear);
            form.Controls.Add(prompt);
            return form;
        }
    }

    internal sealed class ProfileEvents
    {
        private readonly ProfileState state;
        private readonly TextBox input;
        private readonly Label preview;

        internal ProfileEvents(ProfileState state, TextBox input, Label preview)
        {
            this.state = state;
            this.input = input;
            this.preview = preview;
        }

        internal void NameChanged(object? sender, EventArgs args) => RefreshPreview();

        internal void RefreshPreview()
        {
            ProfileHandlers.Edit(state, input.Text);
            preview.Text = ProfileHandlers.Preview(state);
        }

        internal void ClearClicked(object? sender, EventArgs args)
        {
            input.Text = "";
            input.Focus();
        }
    }
}
