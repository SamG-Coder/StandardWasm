using System.Windows.Forms;

namespace CounterExample;

internal partial class CounterForm
{
    internal BrowserBinding Binding { get; private set; } = null!;

    partial void Bind(Button button, Label label, Func<int, int> click, string prefix)
    {
        if (click.Target != null || click.Method.DeclaringType != typeof(CounterHandlers))
            throw new InvalidOperationException("The browser binding needs a static handler from CounterHandlers.");
        label.Text = prefix + "0";
        Binding = new BrowserBinding(button.Name, label.Name, click.Method.Name, prefix, click.Method.MetadataToken & 0xffffff);
    }
}

internal sealed record BrowserBinding(string Button, string Label, string Export, string Prefix, int Row);
