using System.Drawing;
using System.Windows.Forms;

namespace CounterExample;

internal class CounterForm : Form
{
    internal readonly Button Increment = new() { Name = "increment", Text = CounterText.Increment };
    internal readonly Label Result = new() { Name = "result" };

    internal CounterForm()
    {
        Name = "counter";
        Text = CounterText.Title;
        var layout = CounterHandlers.BuildTree();
        ClientSize = new Size(CounterHandlers.ClientWidth(layout), CounterHandlers.ClientHeight(layout));
        ApplyLayout(layout);
        AcceptButton = Increment;
        var state = CounterHandlers.Create(0);
        var click = CounterHandlers.Bind(state);
        Result.Font = new Font("Segoe UI", 24, FontStyle.Bold);
        Result.Text = CounterHandlers.LabelText(state);
        Increment.Click += (_, _) =>
        {
            CounterHandlers.Dispatch(click, layout);
            Result.Text = CounterHandlers.LabelText(state);
            ApplyLayout(layout);
        };
    }

    private void ApplyLayout(ControlTree layout)
    {
        for (int index = 0; index < CounterHandlers.ControlCount(layout); index++)
        {
            int key = CounterHandlers.ControlValue(layout, index, ControlTree.Key);
            Control control = key == CounterLayout.Increment ? Increment : Result;
            control.Bounds = new Rectangle(
                CounterHandlers.ControlValue(layout, index, ControlTree.X),
                CounterHandlers.ControlValue(layout, index, ControlTree.Y),
                CounterHandlers.ControlValue(layout, index, ControlTree.Width),
                CounterHandlers.ControlValue(layout, index, ControlTree.Height));
            if (!Controls.Contains(control))
                Controls.Add(control);
        }
    }
}
