using System.Windows.Forms;

namespace CounterExample;

internal partial class CounterForm
{
    internal BrowserBinding Binding { get; private set; } = null!;

    partial void Bind(Button button, Label label, Func<CounterState, Func<int>> bind, string prefix)
    {
        if (bind.Target != null || bind.Method.DeclaringType != typeof(CounterHandlers))
            throw new InvalidOperationException("The browser binding needs a static binding factory from CounterHandlers.");
        label.Text = prefix + "0";
        Binding = new BrowserBinding(button.Name, label.Name, bind.Method.Name, prefix, bind.Method.MetadataToken & 0xffffff);
    }
}

internal sealed record BrowserBinding(string Button, string Label, string Export, string Prefix, int Row);
