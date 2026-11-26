using System.Reflection;

namespace WinFormsReference;

public sealed class ReferenceForm : Form
{
    public ReferenceForm()
    {
        Text = "StandardWasm WinForms Reference";
        ClientSize = new Size(360, 144);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var name = new TextBox { Name = "name", MaxLength = 40, Dock = DockStyle.Fill };
        var submit = new Button { Name = "submit", Text = "Apply", AutoSize = true };
        var preview = new Label { Name = "preview", Text = ReadCaption(), AutoSize = true };
        var prompt = new Label { Text = "Name", AutoSize = true };
        layout.Controls.Add(prompt, 0, 0);
        layout.Controls.Add(name, 0, 1);
        layout.Controls.Add(submit, 1, 1);
        layout.Controls.Add(preview, 0, 2);
        layout.SetColumnSpan(preview, 2);
        Controls.Add(layout);
        AcceptButton = submit;

        submit.Click += (_, _) => preview.Text = "Hello, " + name.Text.Trim();
    }

    private static string ReadCaption()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WinFormsReference.Caption.txt")
            ?? throw new InvalidOperationException("The caption resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().TrimEnd();
    }
}
